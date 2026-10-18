using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>What the player's attacks need from the game</summary>
/// <param name="ViewOrigin">The acting player's view point and its sine and cosine (PlayerViewOrigin)</param>
/// <param name="CenterX">The view's middle column, as the renderer last drew it</param>
/// <param name="ShootDelta">How many pixels away from the middle a target can be</param>
/// <param name="HurtPlayer">Hurts another player, as the acting player while it does (deathmatch)</param>
/// <param name="PlayPlayerSound">A sound the acting player makes</param>
/// <param name="DamageDealt">How much of their weapons' damage the player deals (player.damagedealt)</param>
/// <param name="IsDecoy">
/// Whether the match mode makes a decoration something the acting player's shot can hit, to
/// their own cost (Prop Hunt's hunters: it might have been a prop)
/// </param>
internal sealed record PlayerAttackHooks(
    Func<(int x, int y, int sin, int cos)> ViewOrigin,
    Func<int> CenterX,
    Func<int> ShootDelta,
    Action<Entities.Actors.PlayerPawn, int, Entities.Actors.Actor> HurtPlayer,
    Action<string> PlayPlayerSound,
    Func<double> DamageDealt,
    Func<Entities.Actors.Actor, bool>? IsDecoy = null);

/// <summary>Something a player's shot could hit, and how far in front of them it is (TransX)</summary>
internal readonly record struct ShotTarget(Entities.Actors.Actor Actor, int Depth);

/// <summary>
/// The acting player's knife and gun: what a shot could hit (alone, the enemies the renderer
/// drew near the middle of the view; with others playing, worked out from where the player
/// stands), and the damage it does.
/// </summary>
internal sealed class PlayerAttacks(
    MapManager map,
    InventoryManager inventory,
    FactionManager factions,
    LineOfSight lineOfSight,
    GameSession session,
    PlayerAttackHooks hooks)
{
    /// <summary>Whether the weapon in hand is a silent one (weapon.silent): its shots don't alert anyone</summary>
    internal bool PlayerWeaponIsSilent() =>
        session.PlayerState.Weapon != null && inventory.GetProperty(session.PlayerState.Weapon, "weapon.silent") is { } silent
        && (silent is true || bool.TryParse(silent.ToString(), out var b) && b);

    // What the acting player's shot could hit, nearest first, leaving out their own side's
    // (factions.yaml: no friendly fire; a shot passes them by). Alone, as vanilla: the enemies
    // (Entities.Actors.Monster) the renderer last drew near the middle of the view -- the player
    // pawn, projectiles and the BJ-victory actor share _actors with the enemies but have no
    // "Chase" state, so they never qualify. With others playing, ShootCandidatesFrom.
    internal List<ShotTarget> FindShootCandidates()
    {
        if (session.GameMode != GameMode.Single)
            return ShootCandidatesFrom(session.Player);

        var candidates = new List<Entities.Actors.Monster>();

        foreach (var actor in map.GetActors().OfType<Entities.Actors.Monster>())
        {
            if (actor == null || !actor.ResolvedStates.ContainsKey("Chase")) continue;
            if (factions.IsAlly(session.Player, actor)) continue;     // the player's side: shots pass them by
            if (actor.RuntimeFlags.Has(ObjFlags.Shootable) && actor.RuntimeFlags.Has(ObjFlags.Visible)
                && Math.Abs(actor.ViewX - hooks.CenterX()) < hooks.ShootDelta())
            {
                candidates.Add(actor);
            }
        }

        candidates.Sort((a, b) => a.TransX.CompareTo(b.TransX));
        return candidates.Select(c => new ShotTarget(c, c.TransX)).ToList();
    }

    /// <summary>
    /// What a player's shot could hit, worked out from where they stand rather than from what
    /// any screen shows (another player's machine draws another view, and a game played on
    /// several machines has to come out the same on each): the living enemies -- and, in
    /// deathmatch, the other living players -- within the shot's angle either side of straight
    /// ahead (the renderer's ShootDelta, which comes to the same angle however wide the view),
    /// with a clear line to them. Nearest first. In Prop Hunt, a hunter's shot can hit the
    /// decorations too (IsDecoy), so a prop beside one is covered by it.
    /// </summary>
    internal List<ShotTarget> ShootCandidatesFrom(Entities.Actors.PlayerPawn shooter)
    {
        var (fromx, fromy, fromsin, fromcos) = hooks.ViewOrigin();
        var candidates = new List<ShotTarget>();

        foreach (var actor in map.GetActors())
        {
            bool enemy = actor is Entities.Actors.Monster && !actor.IsRemoved && actor.ResolvedStates.ContainsKey("Chase")
                && actor.RuntimeFlags.Has(ObjFlags.Shootable) && !factions.IsAlly(shooter, actor);
            bool rival = actor is Entities.Actors.PlayerPawn pawn && pawn.State.Health > 0
                && session.AreRivals(shooter.State, pawn.State);     // no friendly fire in a team game
            if (!enemy && !rival && hooks.IsDecoy?.Invoke(actor) != true)
                continue;

            // As TransformActorFrom: how far in front (nx) and to the side (ny)
            int gx = actor.X - fromx, gy = actor.Y - fromy;
            int nx = MathUtils.FixedMul(gx, fromcos) - MathUtils.FixedMul(gy, fromsin) - GameConstants.ActorSize;
            int ny = MathUtils.FixedMul(gy, fromcos) + MathUtils.FixedMul(gx, fromsin);
            if (nx < GameConstants.MinDist)
                continue;

            // |ny / nx| * scale < ShootDelta, with the view's width taken out of both
            if (Math.Abs((long)ny) * 5 * (GameConstants.FocalLength + GameConstants.MinDist) >= (long)nx * (GameConstants.ViewGlobal / 2))
                continue;

            if (!lineOfSight.CheckLine(actor, shooter))
                continue;

            candidates.Add(new ShotTarget(actor, nx));
        }

        return candidates.OrderBy(c => c.Depth).ToList();
    }

    /// <summary>
    /// A player's shot or blow lands on an enemy, or (deathmatch) another player; or on a decoy
    /// (Prop Hunt: a decoration that wasn't a prop), which costs the shooter DecoyShare of it
    /// </summary>
    internal void HitShotTarget(Entities.Actors.Actor target, uint damage)
    {
        if (target is Entities.Actors.PlayerPawn pawn)
            hooks.HurtPlayer(pawn, (int)damage, session.Player);
        else if (target is Entities.Actors.Monster monster)
            monster.Damage(damage, session.Player);
        else if (hooks.IsDecoy?.Invoke(target) == true)
            hooks.HurtPlayer(session.Player, Math.Max(1, (int)damage / DecoyShare), target);
    }

    /// <summary>A shot at a decoy hurts the shooter by this part of its damage (a quarter)</summary>
    internal const int DecoyShare = 4;

    // The weapon in hand's `attacksound`.
    internal void PlayAttackSound()
    {
        if (session.PlayerState.Weapon != null && inventory.GetStringProperty(session.PlayerState.Weapon, "attacksound") is { Length: > 0 } sound)
            hooks.PlayPlayerSound(sound);
    }

    internal void KnifeAttack(Entities.Actors.Actor ob)
    {
        PlayAttackSound();

        var closest = FindShootCandidates().FirstOrDefault(c => c.Depth <= 0x18000L).Actor;
        if (closest == null)
            return; // missed

        HitShotTarget(closest, PlayerDamage(session.Random.RndT() >> 4));
    }

    // A player attack's damage, scaled by the class's player.damagedealt
    internal uint PlayerDamage(int damage) => (uint)Math.Round(damage * hooks.DamageDealt());

    /// <summary>
    /// One shot that hits the closest target in its sights, with a clear line to it, for min..max
    /// whatever the distance, never missing (Midnight Assault's FireBullet, as its zoomed sniper
    /// rifle's 400-499). Silent: no one hears it.
    /// </summary>
    internal void FireBullet(int min, int max, bool silent)
    {
        PlayAttackSound();
        if (!silent && !PlayerWeaponIsSilent())
            session.MadeNoise = true;

        var closest = FindShootCandidates().FirstOrDefault().Actor;
        if (closest == null || !lineOfSight.CheckLine(closest))
            return;

        int damage = max > min ? min + session.Random.RndT() * (max - min + 1) / 256 : min;
        HitShotTarget(closest, PlayerDamage(damage));
    }

    // near, mid, far: what a random 0-255 is divided by for a hit within 2 tiles, within 4, and
    // beyond (where it can also miss); Wolf3D's are 4, 6, 6
    internal void GunAttack(Entities.Actors.Actor ob, int near = 4, int mid = 6, int far = 6)
    {
        int damage;
        int dx, dy, dist;

        PlayAttackSound();

        // A silent weapon (weapon.silent, Blake Stone's auto-charge pistol) alerts no one
        if (!PlayerWeaponIsSilent())
            session.MadeNoise = true;

        //
        // find the closest potential target, and confirm a clear line to it
        // (the legacy version of this re-scanned in a loop, but since nothing marks a
        // failed candidate as excluded, re-scanning always finds the same actor again --
        // it only ever gives the single closest actor one shot at passing CheckLine)
        //
        var closest = FindShootCandidates().FirstOrDefault().Actor;
        if (closest == null)
            return; // no targets, missed
        if (!lineOfSight.CheckLine(closest))
            return;

        //
        // hit something
        //
        var player = session.Player;
        dx = Math.Abs(closest.TileX - player.TileX);
        dy = Math.Abs(closest.TileY - player.TileY);
        dist = dx > dy ? dx : dy;
        if (dist < 2)
            damage = session.Random.RndT() / near;
        else if (dist < 4)
            damage = session.Random.RndT() / mid;
        else
        {
            if ((session.Random.RndT() / 12) < dist)           // missed
                return;
            damage = session.Random.RndT() / far;
        }

        HitShotTarget(closest, PlayerDamage(damage));
    }
}
