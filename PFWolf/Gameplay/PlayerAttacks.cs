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
/// <param name="HitWall">
/// A player's shot or blow striking the wall on a tile, of a damage type: the wall takes the damage
/// the roll gives if that type hurts it (Gameplay/DestructibleWalls.cs)
/// </param>
internal sealed record PlayerAttackHooks(
    Func<(int x, int y, int sin, int cos)> ViewOrigin,
    Func<int> CenterX,
    Func<int> ShootDelta,
    Action<Entities.Actors.PlayerPawn, int, Entities.Actors.Actor> HurtPlayer,
    Action<string> PlayPlayerSound,
    Func<double> DamageDealt,
    Func<Entities.Actors.Actor, bool>? IsDecoy = null,
    Action<int, int, string, Func<int>>? HitWall = null);

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
    // A power-up that put the weapon in hand can have its own (powerup.attacksound: Knife Fury's swish).
    internal void PlayAttackSound()
    {
        if (Powerups.ForWeapon(inventory, session.PlayerState.Weapon) is { } powerup
            && inventory.GetStringProperty(powerup, "powerup.attacksound") is { Length: > 0 } powerupSound)
            hooks.PlayPlayerSound(powerupSound);
        else if (session.PlayerState.Weapon != null && inventory.GetStringProperty(session.PlayerState.Weapon, "attacksound") is { Length: > 0 } sound)
            hooks.PlayPlayerSound(sound);
    }

    // How far in front a knife reaches (vanilla's KnifeAttack: 1.5 tiles)
    const long KnifeReach = 0x18000L;

    internal void KnifeAttack(Entities.Actors.Actor ob)
    {
        PlayAttackSound();

        var closest = FindShootCandidates().FirstOrDefault(c => c.Depth <= KnifeReach).Actor;
        if (closest == null)
        {
            // missed: a wall in reach takes the blow, if it's one the knife breaks
            if (hooks.HitWall != null)
            {
                var player = session.Player;
                HitWall(lineOfSight.TraceShot(player.X, player.Y, player.Angle, (int)KnifeReach), DestructibleWalls.MeleeType,
                    () => session.Random.RndT() >> 4);
            }
            return;
        }

        // A power-up's weapon that kills with every blow (powerup.weaponkills: End of Destiny's Knife
        // Fury), the death its powerup.damagetype's
        if (closest is Entities.Actors.Monster victim && Powerups.ForWeapon(inventory, session.PlayerState.Weapon) is { } fury
            && Powerups.Flag(inventory, fury, "powerup.weaponkills"))
        {
            victim.BlowType = inventory.GetStringProperty(fury, "powerup.damagetype") is { Length: > 0 } furyType ? furyType : null;
            try
            {
                HitShotTarget(victim, (uint)Math.Max((int)victim.Hitpoints, 1));
            }
            finally
            {
                victim.BlowType = null;
            }
            return;
        }

        // A weapon with weapon.backstab kills an enemy outright from behind while it's unaware
        // (Midnight Assault's knife: KnifeAttack's "insta-kill if back-stabbed")
        if (closest is Entities.Actors.Monster target && WeaponIs("weapon.backstab") && BackTurned(target))
        {
            // its own damage type, for the death it dies (weapon.backstabtype, Backstab when not set:
            // Monster DEATH STATES); End of Destiny's are stealth kills
            target.BlowType = session.PlayerState.Weapon is { } weapon && inventory.GetStringProperty(weapon, "weapon.backstabtype") is { Length: > 0 } type
                ? type : "Backstab";
            try
            {
                HitShotTarget(target, (uint)Math.Max((int)target.Hitpoints, 1));
            }
            finally
            {
                target.BlowType = null;
            }
            return;
        }

        HitShotTarget(closest, PlayerDamage(session.Random.RndT() >> 4));
    }

    // Whether the weapon in hand has a true flag property
    bool WeaponIs(string property) =>
        session.PlayerState.Weapon is { } weapon && inventory.GetProperty(weapon, property) is { } value
        && (value is true || bool.TryParse(value.ToString(), out var b) && b);

    // The nearest enemy in the knife's reach, if it isn't going after anyone (AttackMode)
    Entities.Actors.Monster? UnawareInReach() =>
        FindShootCandidates().FirstOrDefault(c => c.Depth <= KnifeReach).Actor is Entities.Actors.Monster monster
        && !monster.RuntimeFlags.Has(ObjFlags.AttackMode) && monster.Hitpoints > 0 ? monster : null;

    // Degrees between two facings, 0 to 180
    static int Apart(int a, int b)
    {
        int d = ((a - b) % 360 + 360) % 360;
        return Math.Min(d, 360 - d);
    }

    bool BackTurned(Entities.Actors.Monster monster) => BackTurned(ProjectileActions.FacingAngle(monster), session.Player.Angle);

    /// <summary>An enemy facing within 30 degrees of the way the player does has its back to them</summary>
    internal static bool BackTurned(int enemyFacing, int playerAngle) => Apart(enemyFacing, playerAngle) < 30;

    /// <summary>An enemy facing within 40 degrees of straight at the player faces them</summary>
    internal static bool FacesPlayer(int enemyFacing, int playerAngle) => Apart(enemyFacing, playerAngle + 180) < 40;

    /// <summary>
    /// The enemy a blow would stab in the back now: the nearest in the knife's reach, unaware of
    /// the player, with its back to them (Midnight Assault's CheckForAttackState); null if none
    /// </summary>
    internal Entities.Actors.Monster? BackstabTarget() =>
        UnawareInReach() is { } monster && BackTurned(monster) ? monster : null;

    /// <summary>
    /// The enemy in the knife's reach that's unaware and facing the player (within 40 degrees):
    /// one papers could be shown to (CheckForAttackState); null if none
    /// </summary>
    internal Entities.Actors.Monster? FacingTarget() =>
        UnawareInReach() is { } monster && FacesPlayer(ProjectileActions.FacingAngle(monster), session.Player.Angle) ? monster : null;

    // A player attack's damage, scaled by the class's player.damagedealt
    internal uint PlayerDamage(int damage) => (uint)Math.Round(damage * hooks.DamageDealt());

    /*
    =============================================================================

                                  HIT PUFFS

    Something to see where a player's gunshot lands (Midnight Assault's SpawnBloodPuff and
    SpawnBulletPuff). A hit leaves the target's `hitpuff` (an actordefs class: blood for a
    soldier, crumbs for a tombstone; nothing for one without) just in front of it. A shot that
    hits nothing, misses or is blocked leaves the weapon's `weapon.puff` where the line it was
    fired along meets a wall, or a door that isn't open; none on a wall with walls.yaml
    `puffs: false` (the original's jungle, forest and horizon), nor past the map's edge. A
    weapon without `weapon.puff` leaves none, as Wolf3D's. The shotgun's pellets
    (A_FirePellets) each leave their own, each at a height of its own.

    =============================================================================
    */

    // How far a puff is put out in front of a target, toward the shooter
    const int HitPuffOut = 0x6000;

    /// <summary>A hit's puff: the target's `hitpuff`, between it and the shooter (or at a point), so high</summary>
    internal void HitPuff(Entities.Actors.Actor target, (int X, int Y)? at = null, double z = 0)
    {
        if (!target.Properties.TryGetValue("hitpuff", out var cls) || cls?.ToString() is not { Length: > 0 } puff
            || puff.Equals("none", StringComparison.OrdinalIgnoreCase))
            return;
        if (at == null)
        {
            var player = session.Player;
            long dx = player.X - target.X, dy = player.Y - target.Y;
            long dist = Math.Max(Math.Max(Math.Abs(dx), Math.Abs(dy)), 1);
            int out_ = (int)Math.Min(HitPuffOut, dist / 2);
            at = (target.X + (int)(dx * out_ / dist), target.Y + (int)(dy * out_ / dist));
        }
        SpawnPuff(puff, at.Value.X, at.Value.Y, z);
    }

    /// <summary>
    /// A shot that hit nothing: the weapon's `weapon.puff` where its line meets a wall, so high.
    /// With <paramref name="wallDamage"/> (the damage at so many tiles away), that wall takes the
    /// shot if it's one a Bullet (or the weapon's damage type) breaks.
    /// </summary>
    internal void MissPuff(double? angle = null, int reach = LineOfSight.ShotReach, double z = 0, Func<int, int>? wallDamage = null)
    {
        var puff = WeaponPuff();
        if (puff == null && (wallDamage == null || hooks.HitWall == null))
            return;
        var player = session.Player;
        var trace = lineOfSight.TraceShot(player.X, player.Y, angle ?? player.Angle, reach);
        if (trace.End == ShotEnd.Wall && puff != null)
            SpawnPuff(puff, trace.X, trace.Y, z);
        if (wallDamage != null)
            HitWall(trace, DestructibleWalls.BulletType,
                () => wallDamage(Math.Max(Math.Abs(trace.TileX - player.TileX), Math.Abs(trace.TileY - player.TileY))));
    }

    string? WeaponPuff() =>
        session.PlayerState.Weapon is { } weapon && inventory.GetStringProperty(weapon, "weapon.puff") is { Length: > 0 } puff ? puff : null;

    /// <summary>
    /// The wall a traced shot or blow struck takes it, of the weapon's `weapon.damagetype` (else
    /// <paramref name="defaultType"/>), if that's a type that breaks it; the damage, scaled as the
    /// player's is, is only rolled then
    /// </summary>
    void HitWall(ShotTrace trace, string defaultType, Func<int> damage)
    {
        if (hooks.HitWall == null || trace.End is not (ShotEnd.Wall or ShotEnd.NoPuffs) || trace.TileX < 0)
            return;
        var type = session.PlayerState.Weapon is { } weapon && inventory.GetStringProperty(weapon, "weapon.damagetype") is { Length: > 0 } own
            ? own : defaultType;
        hooks.HitWall(trace.TileX, trace.TileY, type, () => (int)PlayerDamage(damage()));
    }

    // A puff of that class at the point (global coordinates), so many texels up or down
    void SpawnPuff(string className, int x, int y, double z = 0)
    {
        if (map.SpawnAt(className, x, y, session.Player) is not { } puff)
        {
            Console.WriteLine($"Hit puff: '{className}' isn't an actor.");
            return;
        }
        puff.Z = z;
    }

    /*
    =============================================================================

                                  PELLETS

    A_FirePellets(count, spread, min, max[, range[, height]]): the shotgun (Midnight Assault's
    FireShotgunBullets: 10 pellets, 5 degrees, 20-44, 6 tiles). The pellets fly from the
    player at the closest target in the sights with a clear line (the original's assisted aim),
    or straight ahead with none, each turned by up to `spread` whole degrees either way at random.
    Each follows its own line, out to `range` tiles: the first enemy it passes close to (within
    the original's ENEMYCOLSIZE) takes min..max, rolled once for the whole shot as the original
    did, and leaves its `hitpuff` there; a pellet that meets a wall first leaves the weapon's
    `weapon.puff`; one that goes out of range leaves nothing. Every puff is drawn up or down by
    up to `height` texels at random (16, the original's random height for a many-shot weapon).

    =============================================================================
    */

    // How close a pellet passes to an enemy to hit it, and to another player (the original's)
    const int EnemyColSize = 0x5000, PlayerColSize = 0x3000;

    internal void FirePellets(int count, int spread, int min, int max, double rangeTiles, double height)
    {
        PlayAttackSound();
        if (!PlayerWeaponIsSilent())
            session.MadeNoise = true;

        var player = session.Player;
        var aim = FindShootCandidates().Select(c => c.Actor).FirstOrDefault(a => lineOfSight.CheckLine(a));
        double baseAngle = aim != null ? AngleTo(player, aim) : player.Angle;
        int damage = max > min ? min + session.Random.RndT() * (max - min + 1) / 256 : min;
        int reach = (int)(rangeTiles * (1 << MapConstants.TileShift));

        for (int i = 0; i < count; i++)
        {
            int turn = spread > 0 ? session.Random.RndT() % (spread + 1) : 0;
            double angle = baseAngle + (session.Random.RndT() > 127 ? -turn : turn);
            double z = height > 0 ? (session.Random.RndT() / 255.0 * 2 - 1) * height : 0;

            Entities.Actors.Actor? hit = null;
            var trace = lineOfSight.TraceShot(player.X, player.Y, angle, reach, (x, y) => (hit = PelletTarget(x, y)) != null);
            if (hit != null)
            {
                HitShotTarget(hit, PlayerDamage(damage));
                HitPuff(hit, (trace.X, trace.Y), z);
            }
            else
            {
                if (trace.End == ShotEnd.Wall && WeaponPuff() is { } puff)
                    SpawnPuff(puff, trace.X, trace.Y, z);
                HitWall(trace, DestructibleWalls.BulletType, () => damage);
            }
        }
    }

    // What a pellet at the point hits: a living enemy it passes close to (not on the player's
    // side), or with others playing a rival player; null for none
    Entities.Actors.Actor? PelletTarget(int x, int y)
    {
        var shooter = session.Player;
        foreach (var actor in map.GetActors())
        {
            if (actor.IsRemoved || actor == shooter)
                continue;
            int size;
            if (actor is Entities.Actors.Monster monster)
            {
                if (!monster.RuntimeFlags.Has(ObjFlags.Shootable) || monster.Hitpoints <= 0 || factions.IsAlly(shooter, monster))
                    continue;
                size = EnemyColSize;
            }
            else if (actor is Entities.Actors.PlayerPawn pawn && pawn.State.Health > 0 && session.AreRivals(shooter.State, pawn.State))
                size = PlayerColSize;
            else
                continue;
            if (Math.Abs(x - actor.X) < size && Math.Abs(y - actor.Y) < size)
                return actor;
        }
        return null;
    }

    // Degrees from one actor to another (east 0, north 90)
    static double AngleTo(Entities.Actors.Actor from, Entities.Actors.Actor to)
    {
        double angle = Math.Atan2(from.Y - to.Y, to.X - from.X) * 180 / Math.PI;
        return angle < 0 ? angle + 360 : angle;
    }

    /// <summary>
    /// One shot that hits the closest target in its sights, with a clear line to it, for min..max
    /// whatever the distance, never missing (Midnight Assault's FireBullet, as its zoomed sniper
    /// rifle's 400-499). Silent: no one hears it.
    /// <para/>
    /// The weapon in hand can change what a hit does (End of Destiny's stealth rifle):
    /// `weapon.spares` names an actor flag (BOSS) whose holders it doesn't hurt at all, and with
    /// `weapon.killrange` (tiles) a hit within that range kills outright, and one beyond it does
    /// nothing.
    /// </summary>
    internal void FireBullet(int min, int max, bool silent)
    {
        PlayAttackSound();
        if (!silent && !PlayerWeaponIsSilent())
            session.MadeNoise = true;

        int Roll() => max > min ? min + session.Random.RndT() * (max - min + 1) / 256 : min;
        var closest = FindShootCandidates().FirstOrDefault().Actor;
        if (closest == null || !lineOfSight.CheckLine(closest))
        {
            MissPuff(wallDamage: _ => Roll());
            return;
        }

        if (session.PlayerState.Weapon is { } weapon)
        {
            if (inventory.GetStringProperty(weapon, "weapon.spares") is { Length: > 0 } spared && closest.HasFlag(spared))
                return;
            if (inventory.GetIntProperty(weapon, "weapon.killrange", 0) is > 0 and var killRange)
            {
                int dist = Math.Max(Math.Abs(closest.TileX - session.Player.TileX), Math.Abs(closest.TileY - session.Player.TileY));
                if (dist <= killRange && closest is Entities.Actors.Monster victim)
                    HitShotTarget(victim, (uint)Math.Max((int)victim.Hitpoints, 1));
                return;
            }
        }

        HitShotTarget(closest, PlayerDamage(Roll()));
        HitPuff(closest);
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
        // A shot at a wall: the roll for a hit at that distance (no miss roll: walls stand still)
        int WallDamage(int tiles) => session.Random.RndT() / (tiles < 2 ? near : tiles < 4 ? mid : far);

        var closest = FindShootCandidates().FirstOrDefault().Actor;
        if (closest == null || !lineOfSight.CheckLine(closest))
        {
            MissPuff(wallDamage: WallDamage);     // no target, or none in the clear: the shot hits the wall
            return;
        }

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
            {
                MissPuff(wallDamage: WallDamage);
                return;
            }
            damage = session.Random.RndT() / far;
        }

        HitShotTarget(closest, PlayerDamage(damage));
        HitPuff(closest);
    }
}
