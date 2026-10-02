using Wolf3D.Constants;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                            MISSILES, BLASTS AND BARRIERS

    The player's weapons can throw missiles (A_FireMissile) that fly until they hit a wall, a
    solid thing or an enemy, then go to their Death state, which can blow up (A_Explode) and
    hurt everything shootable around it. A weapon with `weapon.chargetics` only fires with a
    full charge and takes that long to charge again (Blake Stone's auto charge pistol).

    Actors that block their tile only part of the time (Blake Stone's barriers) block and free it
    with A_BlockTile / A_UnblockTile, and A_ShockNearby hurts a player who comes too close.

    =============================================================================
    */

    /// <summary>Tics until the weapon in hand is charged again; 0 when it's ready (weapon.chargetics)</summary>
    internal static int weaponcharge;

    /// <summary>The weapon in hand's `weapon.chargetics`: tics to charge between shots, 0 for none</summary>
    internal static int WeaponChargeTics(string? weapon) =>
        weapon == null ? 0 : _inventoryManager.GetIntProperty(weapon, "weapon.chargetics", 0);

    /// <summary>
    /// Whether the weapon in hand can shoot now as far as its charge goes; if it can and it
    /// charges, starts the next charge (the shot empties it)
    /// </summary>
    static bool TakeCharge()
    {
        int charge = WeaponChargeTics(gamestate.weapon);
        if (charge <= 0)
            return true;
        if (weaponcharge > 0)
            return false;
        weaponcharge = charge;
        DrawCharge();
        return true;
    }

    /// <summary>Counts the weapon's charge down, redrawing the status bar's charge light once it's ready</summary>
    static void TickWeaponCharge()
    {
        if (weaponcharge <= 0)
            return;
        weaponcharge = Math.Max(weaponcharge - (int)tics, 0);
        if (weaponcharge == 0)
            DrawCharge();
    }

    static void RegisterMissileActions()
    {
        Entities.Actors.ActorActionRegistry.Register("A_FireMissile", A_FireMissile);
        Entities.Actors.ActorActionRegistry.Register("A_Missile", A_Missile);
        Entities.Actors.ActorActionRegistry.Register("A_Explode", A_Explode);
        Entities.Actors.ActorActionRegistry.Register("A_BlockTile", A_BlockTile);
        Entities.Actors.ActorActionRegistry.Register("A_UnblockTile", A_UnblockTile);
        Entities.Actors.ActorActionRegistry.Register("A_ShockNearby", A_ShockNearby);
    }

    /// <summary>
    /// A_FireMissile("Grenade"[, spread]): the weapon in hand throws a missile the way the player
    /// faces, turned up to spread degrees either way at random, from just in front of them, if
    /// there's ammo (and charge) for it. The missile's `speed` is in 1/65536 tile a tic, and
    /// `speed.random` adds up to that much more at random.
    /// </summary>
    static void A_FireMissile(Entities.Actors.Actor weapon, string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("A_FireMissile: no missile given.");
            return;
        }
        if (!CanFire(gamestate.weapon) || !TakeCharge())
            return;

        var missile = _mapManager.SpawnAtActor(args[0], player);
        if (missile == null)
            return;

        int spread = args.Length > 1 && int.TryParse(args[1], out var s) ? Math.Max(s, 0) : 0;
        int angle = player.Angle + (spread > 0 ? US_RndT() % (2 * spread + 1) - spread : 0);
        missile.Angle = (short)((angle % ANGLES + ANGLES) % ANGLES);
        missile.Speed = ReadMissileSpeed(missile);
        missile.Shooter = player;
        missile.TicCount = 1;

        // Just in front of the player, so one fired point blank at a wall still shows its blast
        const int ahead = (int)(9L * MINDIST / 8);
        missile.X += MathUtils.FixedMul(ahead, costable[missile.Angle]);
        missile.Y -= MathUtils.FixedMul(ahead, sintable[missile.Angle]);
        missile.TileX = (byte)(missile.X >> MapConstants.TILESHIFT);
        missile.TileY = (byte)(missile.Y >> MapConstants.TILESHIFT);

        PlayAttackSound();
        madenoise = true;
        UseAmmo();
    }

    static int ReadMissileSpeed(Entities.Actors.Actor missile)
    {
        int speed = missile.Properties.TryGetValue("speed", out var v) && int.TryParse(v?.ToString(), out var n) ? n : 0x1c00;
        if (missile.Properties.TryGetValue("speed.random", out var r) && int.TryParse(r?.ToString(), out var extra) && extra > 0)
            speed += US_RndT() * extra / 256;
        return speed;
    }

    /// <summary>
    /// A_Missile(min, max): a player's missile's think. It flies along its angle; meeting a wall,
    /// a solid thing or something shootable (not the player), it hurts that min..max and goes to
    /// its Death state.
    /// </summary>
    static void A_Missile(Entities.Actors.Actor ob, string[] args)
    {
        int speed = (int)(ob.Speed * tics);
        ob.X += MathUtils.FixedMul(speed, costable[ob.Angle]);
        ob.Y -= MathUtils.FixedMul(speed, sintable[ob.Angle]);
        ob.TileX = (byte)(ob.X >> MapConstants.TILESHIFT);
        ob.TileY = (byte)(ob.Y >> MapConstants.TILESHIFT);

        Entities.Actors.Actor? victim = null;
        bool blocked = !ProjectileTryMove(ob);
        if (!blocked)
        {
            victim = _mapManager.ShootableActorsNear(ob.X, ob.Y, PROJECTILESIZE / 2)
                .FirstOrDefault(a => !ReferenceEquals(a, ob.Shooter) && a is not Entities.Actors.PlayerPawn);
            if (victim == null)
                return;
        }

        if (victim != null)
        {
            int min = args.Length > 0 && int.TryParse(args[0], out var a0) ? a0 : 0;
            int max = args.Length > 1 && int.TryParse(args[1], out var a1) ? a1 : min;
            int damage = max > min ? min + US_RndT() * (max - min + 1) / 256 : min;
            if (damage > 0)
                DamageActor(victim, PlayerDamage(damage), ob);
        }

        Detonate(ob);
    }

    /// <summary>Sends a missile to its Death state (with its deathsound), or takes it away if it has none</summary>
    static void Detonate(Entities.Actors.Actor ob)
    {
        if (!ob.ResolvedStates.ContainsKey("Death"))
        {
            _mapManager.MarkForRemoval(ob);
            return;
        }
        if (ob.Properties.TryGetValue("deathsound", out var sound) && sound is string name)
            PlaySoundLocActor(name, ob);
        NewActorState(ob, "Death");
    }

    /// <summary>
    /// A_Explode(min, max[, radius[, "hurtplayer"]]): a blast spreading from the actor's tile
    /// through open floor (not walls or shut doors) up to radius tiles away (default 2): each
    /// shootable thing in it takes min..max, rolled once. With hurtplayer the player takes it
    /// too, if standing in it. A thing whose `monster.blastedby` names the actor's class breaks
    /// (goes to its Death state).
    /// </summary>
    static void A_Explode(Entities.Actors.Actor ob, string[] args)
    {
        int min = args.Length > 0 && int.TryParse(args[0], out var a0) ? a0 : 0;
        int max = args.Length > 1 && int.TryParse(args[1], out var a1) ? a1 : min;
        int radius = args.Length > 2 && int.TryParse(args[2], out var a2) ? Math.Max(a2, 0) : 2;
        bool hurtPlayer = args.Skip(3).Any(a => a.Equals("hurtplayer", StringComparison.OrdinalIgnoreCase));
        int damage = max > min ? min + US_RndT() * (max - min + 1) / 256 : min;

        int cx = ob.TileX, cy = ob.TileY;
        if (IsSolidForBlast(cx, cy))
            return;

        var seen = new HashSet<(int, int)> { (cx, cy) };
        var open = new Queue<(int X, int Y)>();
        open.Enqueue((cx, cy));
        while (open.Count > 0)
        {
            var (x, y) = open.Dequeue();

            foreach (var target in _mapManager.ShootableActorsAt(x, y).ToList())
                if (!ReferenceEquals(target, ob) && target is not Entities.Actors.PlayerPawn)
                    DamageActor(target, ReferenceEquals(ob.Shooter, player) ? PlayerDamage(damage) : (uint)damage, ob);

            if (hurtPlayer && player.TileX == x && player.TileY == y)
                TakeDamage(damage, ob);

            // `monster.blastedby: Class`: only a blast from that class breaks it (Planet Strike's
            // security cube, by the fission detonator): it goes to its Death state
            foreach (var target in _mapManager.GetActors().Where(a => !a.IsRemoved && a.TileX == x && a.TileY == y
                && a.Properties.TryGetValue("monster.blastedby", out var by) && string.Equals(by?.ToString(), ob.Name, StringComparison.OrdinalIgnoreCase)
                && a.CurrentState?.StateName != "Death").ToList())
                NewActorState(target, "Death");

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

    // A wall, a moving pushwall or a door that isn't open stops a blast
    static bool IsSolidForBlast(int x, int y) => _mapManager.actorat[x, y] switch
    {
        Wall => true,
        Door door => door.door < 0 || doorobjlist[door.door].action != dooractiontypes.dr_open,
        _ => _mapManager.tilemap[x, y] != 0 && (_mapManager.tilemap[x, y] & BIT_DOOR) == 0,
    };

    /// <summary>A_BlockTile: the actor's tile blocks movement, as a solid thing's does</summary>
    static void A_BlockTile(Entities.Actors.Actor ob)
    {
        if (_mapManager.actorat[ob.TileX, ob.TileY] == null)
            _mapManager.actorat[ob.TileX, ob.TileY] = new BlockingActor();
    }

    /// <summary>A_UnblockTile: the actor's tile no longer blocks (if a solid thing was all that blocked it)</summary>
    static void A_UnblockTile(Entities.Actors.Actor ob)
    {
        if (_mapManager.actorat[ob.TileX, ob.TileY] is BlockingActor and not WallSpriteBlocker)
            _mapManager.actorat[ob.TileX, ob.TileY] = null;
    }

    /// <summary>
    /// A_ShockNearby(damage, distance, chance[, "sound"]): a think for something that zaps the
    /// player within distance tiles of it (each way): each tic it's run, a chance in 256 of a
    /// shock for damage, with the sound from where it is.
    /// </summary>
    static void A_ShockNearby(Entities.Actors.Actor ob, string[] args)
    {
        int damage = args.Length > 0 && int.TryParse(args[0], out var d) ? d : 0;
        double distance = args.Length > 1 && double.TryParse(args[1], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 1;
        int chance = args.Length > 2 && int.TryParse(args[2], out var c) ? c : 16;
        if (damage <= 0 || playstate == playstatetypes.ex_died || US_RndT() >= chance)
            return;

        long reach = (long)(distance * MapConstants.TILEGLOBAL);
        if (Math.Abs(player.X - ob.X) > reach || Math.Abs(player.Y - ob.Y) > reach)
            return;

        if (args.Length > 3 && !string.IsNullOrEmpty(args[3]))
            PlaySoundLocActor(args[3], ob);
        TakeDamage(damage, ob);
    }
}
