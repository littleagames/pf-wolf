using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>What missiles, blasts and barriers need from the game</summary>
/// <param name="ProjectileTryMove">Whether a projectile can be where it is (not in a wall or a solid thing)</param>
/// <param name="PlaySoundAt">A sound from where an actor is</param>
/// <param name="HurtPlayer">Hurts a particular player, as the acting player while it does</param>
/// <param name="MadeNoise">A shot was heard: enemies who hear it come</param>
/// <param name="PlayerDied">Whether the player has just died this tic</param>
internal sealed record MissileHooks(
    Func<Entities.Actors.Actor, bool> ProjectileTryMove,
    Action<string, Entities.Actors.Actor> PlaySoundAt,
    Action<Entities.Actors.PlayerPawn, int, Entities.Actors.Actor> HurtPlayer,
    Action MadeNoise,
    Func<bool> PlayerDied);

/// <summary>
/// The player's weapons can throw missiles (A_FireMissile) that fly until they hit a wall, a
/// solid thing or an enemy, then go to their Death state, which can blow up (A_Explode) and
/// hurt everything shootable around it. A weapon with `weapon.chargetics` only fires with a
/// full charge and takes that long to charge again (Blake Stone's auto charge pistol).
///
/// Actors that block their tile only part of the time (Blake Stone's barriers) block and free it
/// with A_BlockTile / A_UnblockTile, and A_ShockNearby hurts a player who comes too close.
/// </summary>
internal sealed class Missiles(
    MapManager map,
    FactionManager factions,
    Doors doors,
    PlayerWeapons weapons,
    PlayerAttacks attacks,
    PlayerStats stats,
    GameSession session,
    MissileHooks hooks)
{
    internal void Register()
    {
        Entities.Actors.ActorActionRegistry.Register("A_FireMissile", A_FireMissile);
        Entities.Actors.ActorActionRegistry.Register("A_Missile", A_Missile);
        Entities.Actors.ActorActionRegistry.Register("A_Explode", A_Explode);
        Entities.Actors.ActorActionRegistry.Register("A_BlockTile", A_BlockTile);
        Entities.Actors.ActorActionRegistry.Register("A_UnblockTile", A_UnblockTile);
        Entities.Actors.ActorActionRegistry.Register("A_ShockNearby", A_ShockNearby);
    }

    /// <summary>
    /// A_FireMissile("Grenade"[, spread[, "power"]]): the weapon in hand throws a missile the way
    /// the player faces, turned up to spread degrees either way at random, from just in front of
    /// them, if there's ammo (and charge) for it. The missile's `speed` is in 1/65536 tile a tic,
    /// and `speed.random` adds up to that much more at random. With "power" it's thrown as hard
    /// as the throw was cooked (A_HoldCharge): that share of its speed, a quarter at least
    /// (Midnight Assault's GrenadeAttack), and the cooking starts again.
    /// </summary>
    internal void A_FireMissile(Entities.Actors.Actor weapon, string[] args)
    {
        const int ANGLES = GameConstants.ANGLES;
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("A_FireMissile: no missile given.");
            return;
        }
        if (!weapons.IsLoaded(session.playerstate.weapon) || !weapons.TakeCharge())
            return;

        var player = session.player;
        var missile = map.SpawnAtActor(args[0], player);
        if (missile == null)
            return;

        int spread = args.Length > 1 && int.TryParse(args[1], out var s) ? Math.Max(s, 0) : 0;
        int angle = player.Angle + (spread > 0 ? session.random.RndT() % (2 * spread + 1) - spread : 0);
        missile.Angle = (short)((angle % ANGLES + ANGLES) % ANGLES);
        missile.Speed = ReadMissileSpeed(missile);
        if (args.Skip(2).Any(a => a.Trim().Equals("power", StringComparison.OrdinalIgnoreCase)))
        {
            missile.Speed = ThrowSpeed(missile.Speed, session.playerstate.WeaponPower);
            session.playerstate.WeaponPower = 0;
        }
        missile.Shooter = player;
        missile.TicCount = 1;

        // Just in front of the player, so one fired point blank at a wall still shows its blast
        const int ahead = (int)(9L * GameConstants.MINDIST / 8);
        missile.X += MathUtils.FixedMul(ahead, TrigTables.costable[missile.Angle]);
        missile.Y -= MathUtils.FixedMul(ahead, TrigTables.sintable[missile.Angle]);
        missile.TileX = (byte)(missile.X >> MapConstants.TILESHIFT);
        missile.TileY = (byte)(missile.Y >> MapConstants.TILESHIFT);

        attacks.PlayAttackSound();
        hooks.MadeNoise();
        weapons.UseAmmo();
    }

    /// <summary>A cooked throw's speed: power (0-100) per cent of the full speed, 25 at least</summary>
    internal static int ThrowSpeed(int fullSpeed, int power) => fullSpeed * Math.Clamp(power, 25, 100) / 100;

    internal int ReadMissileSpeed(Entities.Actors.Actor missile)
    {
        int speed = missile.Properties.TryGetValue("speed", out var v) && int.TryParse(v?.ToString(), out var n) ? n : 0x1c00;
        if (missile.Properties.TryGetValue("speed.random", out var r) && int.TryParse(r?.ToString(), out var extra) && extra > 0)
            speed += session.random.RndT() * extra / 256;
        return speed;
    }

    /// <summary>
    /// A_Missile(min, max[, "bounce"]): a player's missile's think. It flies along its angle;
    /// meeting a wall, a solid thing or something shootable (not the player), it hurts that
    /// min..max and goes to its Death state. With bounce, a wall turns it back off it at half the
    /// speed, with its `bouncesound`, as Midnight Assault's grenade (its Death then comes from its
    /// states running out: its fuse).
    /// </summary>
    internal void A_Missile(Entities.Actors.Actor ob, string[] args)
    {
        int speed = (int)(ob.Speed * session.tics);
        int dx = MathUtils.FixedMul(speed, TrigTables.costable[ob.Angle]);
        int dy = -MathUtils.FixedMul(speed, TrigTables.sintable[ob.Angle]);
        ob.X += dx;
        ob.Y += dy;
        ob.TileX = (byte)(ob.X >> MapConstants.TILESHIFT);
        ob.TileY = (byte)(ob.Y >> MapConstants.TILESHIFT);

        Entities.Actors.Actor? victim = null;
        bool blocked = !hooks.ProjectileTryMove(ob);
        if (blocked && args.Skip(2).Any(a => a.Trim().Equals("bounce", StringComparison.OrdinalIgnoreCase)))
        {
            Bounce(ob, dx, dy);
            return;
        }
        if (!blocked)
        {
            // the player's side's (factions.yaml) it flies past: no friendly fire
            victim = (Entities.Actors.Actor?)map.ShootableActorsNear(ob.X, ob.Y, GameConstants.PROJECTILESIZE / 2)
                .FirstOrDefault(a => !ReferenceEquals(a, ob.Shooter) && !factions.IsAlly(ob, a))
                ?? RivalHitBy(ob);
            if (victim == null)
                return;
        }

        if (victim != null)
        {
            int min = args.Length > 0 && int.TryParse(args[0], out var a0) ? a0 : 0;
            int max = args.Length > 1 && int.TryParse(args[1], out var a1) ? a1 : min;
            int damage = max > min ? min + session.random.RndT() * (max - min + 1) / 256 : min;
            if (damage > 0)
            {
                using var _ = ActAsShooter(ob);
                HurtByMissile(victim, attacks.PlayerDamage(damage), ob);
            }
        }

        Detonate(ob);
    }

    /// <summary>
    /// A missile that's just moved dx, dy into a wall goes back to where it was and turns off it:
    /// its x reversed if only moving along x is blocked, its y if only y, both in a corner (as the
    /// original's grenade did, trying each way on its own). It slows to half, with its bouncesound.
    /// </summary>
    internal void Bounce(Entities.Actors.Actor ob, int dx, int dy)
    {
        const int ANGLES = GameConstants.ANGLES;
        ob.X -= dx;
        ob.Y -= dy;

        ob.X += dx;
        bool xClear = hooks.ProjectileTryMove(ob);
        ob.X -= dx;
        ob.Y += dy;
        bool yClear = hooks.ProjectileTryMove(ob);
        ob.Y -= dy;
        ob.TileX = (byte)(ob.X >> MapConstants.TILESHIFT);
        ob.TileY = (byte)(ob.Y >> MapConstants.TILESHIFT);

        ob.Angle = (short)(BounceAngle(ob.Angle, xClear, yClear) % ANGLES);
        ob.Speed /= 2;
        if (ob.Properties.TryGetValue("bouncesound", out var sound) && sound is string name && name.Length > 0)
            hooks.PlaySoundAt(name, ob);
    }

    /// <summary>
    /// The angle (degrees, 0 east, 90 north) a missile turns to off a wall: x reversed if moving
    /// along y alone was clear, y reversed if along x alone was, straight back otherwise
    /// </summary>
    internal static int BounceAngle(int angle, bool xClear, bool yClear)
    {
        const int ANGLES = GameConstants.ANGLES;
        int turned = (xClear, yClear) switch
        {
            (false, true) => 180 - angle,       // a wall across x: x reversed
            (true, false) => -angle,            // a wall across y: y reversed
            _ => angle + 180,                   // a corner, or nothing clear: straight back
        };
        return (turned % ANGLES + ANGLES) % ANGLES;
    }

    // In deathmatch, another living player a player's missile has flown into (as an enemy's
    // projectile hits a player: within PROJECTILESIZE), else null. Co-op missiles fly through.
    internal Entities.Actors.PlayerPawn? RivalHitBy(Entities.Actors.Actor ob) =>
        session.gamemode != GameMode.Deathmatch ? null
        : map.Players.FirstOrDefault(pawn => !ReferenceEquals(pawn, ob.Shooter) && pawn.State.health > 0
            && Math.Abs((long)pawn.X - ob.X) < GameConstants.PROJECTILESIZE && Math.Abs((long)pawn.Y - ob.Y) < GameConstants.PROJECTILESIZE);

    /// <summary>
    /// Makes the player who fired a missile the acting player while it does its damage, so the
    /// points, kills and their class's damage factor are theirs whichever machine runs it (with
    /// others, the acting player outside their own think is just this machine's). No change for
    /// a missile no player fired.
    /// </summary>
    internal GameSession.ActingScope ActAsShooter(Entities.Actors.Actor ob) =>
        session.ActAs(ob.Shooter is Entities.Actors.PlayerPawn { State: { } shooter } ? shooter : session.playerstate);

    // A missile's (or its blast's) damage landing: on an enemy, or a player (deathmatch rivals,
    // or someone caught in a blast), with the missile as the attacker (frags go to its Shooter)
    internal void HurtByMissile(Entities.Actors.Actor victim, uint damage, Entities.Actors.Actor missile)
    {
        if (victim is Entities.Actors.PlayerPawn pawn)
            hooks.HurtPlayer(pawn, (int)damage, missile);
        else if (victim is Entities.Actors.Monster monster)
            monster.Damage(damage, missile);
    }

    /// <summary>Sends a missile to its Death state (with its deathsound), or takes it away if it has none</summary>
    internal void Detonate(Entities.Actors.Actor ob)
    {
        if (!ob.ResolvedStates.ContainsKey("Death"))
        {
            map.MarkForRemoval(ob);
            return;
        }
        if (ob.Properties.TryGetValue("deathsound", out var sound) && sound is string name)
            hooks.PlaySoundAt(name, ob);
        ob.SetState("Death");
    }

    /// <summary>
    /// A_Explode(min, max[, radius[, "hurtplayer"][, "falloff"][, "playermax:N"]]): a blast
    /// spreading from the actor's tile through open floor (not walls or shut doors) up to radius
    /// tiles away (default 2): each shootable thing in it takes min..max, rolled once. With
    /// hurtplayer the players take it too, if standing in it: from a player's missile, the one
    /// who fired it (and in deathmatch everyone else; co-op has no friendly fire). A thing whose
    /// `monster.blastedby` names the actor's class breaks (goes to its Death state).
    /// With falloff it reaches radius tiles straight out from the actor rather than tile by tile,
    /// and weakens with the distance as Midnight Assault's SplashDamage did: the roll within a
    /// tile, a quarter of it two tiles out, a ninth at three. playermax:N caps what a player takes
    /// of it (the original's quarter of the most).
    /// </summary>
    internal void A_Explode(Entities.Actors.Actor ob, string[] args)
    {
        int min = args.Length > 0 && int.TryParse(args[0], out var a0) ? a0 : 0;
        int max = args.Length > 1 && int.TryParse(args[1], out var a1) ? a1 : min;
        int radius = args.Length > 2 && int.TryParse(args[2], out var a2) ? Math.Max(a2, 0) : 2;
        var options = args.Skip(3).Select(a => a.Trim()).ToList();
        bool hurtPlayer = options.Any(a => a.Equals("hurtplayer", StringComparison.OrdinalIgnoreCase));
        bool falloff = options.Any(a => a.Equals("falloff", StringComparison.OrdinalIgnoreCase));
        int playerMax = options.Select(a => a.StartsWith("playermax:", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(a["playermax:".Length..], out var cap) ? cap : -1).FirstOrDefault(cap => cap >= 0, int.MaxValue);
        int damage = max > min ? min + session.random.RndT() * (max - min + 1) / 256 : min;

        // What a thing standing where it does takes of the blast (0 for out of its reach)
        int DamageTo(Entities.Actors.Actor target) => falloff ? BlastFalloff(damage, radius, ob, target) : damage;

        int cx = ob.TileX, cy = ob.TileY;
        if (IsSolidForBlast(cx, cy))
            return;

        var shooter = ob.Shooter as Entities.Actors.PlayerPawn;
        var blasted = !hurtPlayer ? []
            : map.Players.Where(pawn => (session.gamemode == GameMode.Single || pawn.State.health > 0) && (shooter == null
                || ReferenceEquals(pawn, shooter) || session.gamemode == GameMode.Deathmatch)).ToList();
        using var _ = ActAsShooter(ob);

        var seen = new HashSet<(int, int)> { (cx, cy) };
        var open = new Queue<(int X, int Y)>();
        open.Enqueue((cx, cy));
        while (open.Count > 0)
        {
            var (x, y) = open.Dequeue();

            // a player's blast leaves their side's be (no friendly fire)
            foreach (var target in map.ShootableActorsAt(x, y).ToList())
                if (!ReferenceEquals(target, ob) && !(shooter != null && factions.IsAlly(shooter, target))
                    && DamageTo(target) is > 0 and var hit)
                    target.Damage(shooter != null ? attacks.PlayerDamage(hit) : (uint)hit, ob);

            foreach (var pawn in blasted.Where(p => p.TileX == x && p.TileY == y))
                if (Math.Min(DamageTo(pawn), playerMax) is > 0 and var hit)
                    HurtByMissile(pawn, shooter != null ? attacks.PlayerDamage(hit) : (uint)hit, ob);

            // `monster.blastedby: Class`: only a blast from that class breaks it (Planet Strike's
            // security cube, by the fission detonator): it goes to its Death state
            foreach (var target in map.GetActors().Where(a => !a.IsRemoved && a.TileX == x && a.TileY == y
                && a.Properties.TryGetValue("monster.blastedby", out var by) && string.Equals(by?.ToString(), ob.Name, StringComparison.OrdinalIgnoreCase)
                && a.CurrentState?.StateName != "Death").ToList())
                target.SetState("Death");

            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (Math.Abs(nx - cx) > radius || Math.Abs(ny - cy) > radius
                    || nx < 0 || ny < 0 || nx >= MapManager.MAPSIZE || ny >= MapManager.MAPSIZE
                    || !seen.Add((nx, ny)) || IsSolidForBlast(nx, ny))
                    continue;
                open.Enqueue((nx, ny));
            }
        }
    }

    /// <summary>
    /// A falloff blast's damage to a thing (Midnight Assault's SplashDamage): all of it within a
    /// tile, then divided by the square of the distance in tiles; nothing radius tiles away or more
    /// </summary>
    internal static int BlastFalloff(int damage, int radius, Entities.Actors.Actor blast, Entities.Actors.Actor target)
    {
        double dx = (double)target.X - blast.X, dy = (double)target.Y - blast.Y;
        double tiles = Math.Sqrt(dx * dx + dy * dy) / MapConstants.TILEGLOBAL;
        if (tiles >= radius)
            return 0;
        return tiles <= 1 ? damage : (int)(damage / (tiles * tiles));
    }

    // A wall, a moving pushwall or a door that isn't open stops a blast
    internal bool IsSolidForBlast(int x, int y) => map.actorat[x, y] switch
    {
        Wall => true,
        Door door => door.door < 0 || doors.doorobjlist[door.door].action != dooractiontypes.dr_open,
        _ => map.tilemap[x, y] != 0 && (map.tilemap[x, y] & GameConstants.BIT_DOOR) == 0,
    };

    /// <summary>A_BlockTile: the actor's tile blocks movement, as a solid thing's does</summary>
    internal void A_BlockTile(Entities.Actors.Actor ob)
    {
        if (map.actorat[ob.TileX, ob.TileY] == null)
            map.actorat[ob.TileX, ob.TileY] = new BlockingActor();
    }

    /// <summary>A_UnblockTile: the actor's tile no longer blocks (if a solid thing was all that blocked it)</summary>
    internal void A_UnblockTile(Entities.Actors.Actor ob)
    {
        if (map.actorat[ob.TileX, ob.TileY] is BlockingActor and not WallSpriteBlocker)
            map.actorat[ob.TileX, ob.TileY] = null;
    }

    /// <summary>
    /// A_ShockNearby(damage, distance, chance[, "sound"]): a think for something that zaps the
    /// player within distance tiles of it (each way): each tic it's run, a chance in 256 of a
    /// shock for damage, with the sound from where it is.
    /// </summary>
    internal void A_ShockNearby(Entities.Actors.Actor ob, string[] args)
    {
        int damage = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 0;
        double distance = args.Length > 1 && double.TryParse(args[1], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 1;
        int chance = args.Length > 2 && int.TryParse(args[2], out var c) ? c : 16;
        if (damage <= 0 || hooks.PlayerDied() || session.random.RndT() >= chance)
            return;

        var player = session.player;
        long reach = (long)(distance * MapConstants.TILEGLOBAL);
        if (Math.Abs(player.X - ob.X) > reach || Math.Abs(player.Y - ob.Y) > reach)
            return;

        if (args.Length > 3 && !string.IsNullOrEmpty(args[3]))
            hooks.PlaySoundAt(args[3], ob);
        stats.TakeDamage(damage, ob);
    }
}
