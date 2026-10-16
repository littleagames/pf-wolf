using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>What the cheat commands need from the game</summary>
/// <param name="GodMode">God mode: 0 off, 1 on, 2 on without the damage flash</param>
/// <param name="SetGodMode">Sets it</param>
/// <param name="NoClip">Whether the player walks through walls</param>
/// <param name="SetNoClip">Sets it</param>
/// <param name="InGame">Whether a level is being played</param>
/// <param name="SetPlayState">Ends the level (exitlevel, map)</param>
/// <param name="NewGamePlayerClass">The class new games are played as, if one was picked</param>
/// <param name="SetNewGamePlayerClass">Picks it</param>
/// <param name="SetEyeHeight">How high the view is above the floor</param>
internal sealed record CheatCommandsHooks(
    Func<byte> GodMode,
    Action<byte> SetGodMode,
    Func<byte> NoClip,
    Action<byte> SetNoClip,
    Func<bool> InGame,
    Action<playstatetypes> SetPlayState,
    Func<string?> NewGamePlayerClass,
    Action<string?> SetNewGamePlayerClass,
    Action<int> SetEyeHeight);

/// <summary>
/// The cheats (the old Tab debug keys, plus a few new ones): god, noclip, give, map, exitlevel,
/// tp, killall, diag, summon, pitch, eyeheight, hurt and playerclass
/// </summary>
internal sealed class CheatCommands(
    ConsoleManager console,
    GameEngineManager engine,
    AssetManager assets,
    InventoryManager inventory,
    MapManager mapManager,
    Doors doors,
    StatusBarDrawing statusBar,
    PlayerWeapons weapons,
    PlayerClassRules classRules,
    PlayerStats stats,
    FrameRefresh frame,
    ConsoleTargets targets,
    GameSession session,
    CheatCommandsHooks hooks)
{
    internal void Register()
    {
        console.Register("god", "God mode: 1 = on, 2 = on without the damage flash.", "god [0|1|2]", Cmd_God, Cheat | InLevel,
            complete: Values("0", "1", "2"));
        console.Register("noclip", "Walk through walls.", "noclip [0|1]", Cmd_Noclip, Cheat | InLevel,
            complete: Values("0", "1"));
        console.Register("give", "Gives an item (weapon, ammo, key or armor), or health, points, keys, weapons, ammo or all.",
            "give <item|health|points|keys|weapons|ammo|all> [amount]", Cmd_Give, Cheat | InLevel,
            complete: (_, i) => i == 0 ? ["all", "health", "points", "keys", "weapons", "ammo", .. GivableItems()] : []);
        console.Register("map", "Warps to a level.", "map <MAP##>", Cmd_Map, Cheat | InLevel, aliases: ["warp"],
            complete: (_, i) => i == 0 ? engine.GetGameInfo().Maps.Keys : []);
        console.Register("exitlevel", "Completes the current level.", "exitlevel",
            _ => hooks.SetPlayState(playstatetypes.ex_completed), Cheat | InLevel);
        console.Register("tp", "Teleports to a tile.", "tp <tilex> <tiley> [angle]", Cmd_Teleport, Cheat | InLevel);
        console.Register("killall", "Kills every enemy on the level.", "killall", Cmd_KillAll, Cheat | InLevel);
        console.Register("diag", "Makes a wall tile (default: the one you face) a 45 degree wall, named by its solid corner.",
            "diag <square|solidnw|solidne|solidsw|solidse> [tilex tiley]", Cmd_Diag, Cheat | InLevel,
            complete: (_, i) => i == 0 ? Enum.GetNames<WallShape>().Select(n => n.ToLowerInvariant()) : []);
        console.Register("summon", "Spawns an actor on an open tile (default: the one you face), facing an angle (default: back at you).",
            "summon <class> [angle] [tilex tiley]", Cmd_Summon, Cheat | InLevel,
            complete: (_, i) => i == 0 ? assets.GetActorMetadata().Actors.Keys : i == 1 ? ["0", "45", "90", "135", "180", "225", "270", "315"] : []);
        console.Register("pitch", "Looks up (positive) or down (negative) by that many degrees, as far as the view allows; 0 looks straight ahead.",
            "pitch [degrees]", Cmd_Pitch, InLevel);
        console.Register("eyeheight", $"Sets how high the view is above the floor, in texels (64 a story, {GameConstants.EYEDEFAULT} standing), until the level is left or reloaded.",
            $"eyeheight [{GameConstants.MINEYE}-{GameConstants.MAXEYE}]", Cmd_EyeHeight, InLevel);
        console.Register("hurt", "Damages the player.", "hurt [points]", Cmd_Hurt, InLevel | Solo);
        console.Register("playerclass", "Shows the class being played as and the classes there are; with a name, the class new games are played as.",
            "playerclass [class]", Cmd_PlayerClass, complete: (_, i) => i == 0 ? classRules.PlayerClasses() : []);
    }
    /// <summary>The item classes `give` accepts by name (see GiveItem).</summary>
    internal IEnumerable<string> GivableItems() =>
        new[] { "Weapon", "Ammo", "Key", "BasicArmor" }
            .SelectMany(inventory.GetClassesDerivedFrom)
            .Where(item => inventory.FindClass(item, "WeaponGiver") == null);

    internal void Cmd_God(string[] args)
    {
        // 0 = off, 1 = on, 2 = on without the damage flash (ApplyDamageToPlayer)
        hooks.SetGodMode((byte)(args.Length > 0 ? ParseInt(args[0], 0, 2) : hooks.GodMode() == 0 ? 1 : 0));

        console.Print(hooks.GodMode() switch
        {
            0 => "God mode OFF",
            1 => "God mode ON",
            _ => "God mode ON (no flash)",
        });
    }

    internal void Cmd_Noclip(string[] args)
    {
        hooks.SetNoClip((byte)(Toggle(args, hooks.NoClip() != 0) ? 1 : 0));
        console.Print(hooks.NoClip() != 0 ? "No clipping ON" : "No clipping OFF");
    }

    internal void Cmd_Pitch(string[] args)
    {
        double max = frame.MaxPitch();
        if (args.Length > 0)
        {
            if (!double.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double degrees))
                throw new ArgumentException("usage: pitch [degrees]");
            session.playerstate.Pitch = Math.Clamp(degrees, -max, max);
        }
        console.Print($"Pitch is {session.playerstate.Pitch:0.#} degrees (up to {max:0.#} either way)");
    }

    internal void Cmd_EyeHeight(string[] args)
    {
        if (args.Length > 0)
        {
            if (!int.TryParse(args[0], out int z))
                throw new ArgumentException($"usage: eyeheight [{GameConstants.MINEYE}-{GameConstants.MAXEYE}]");
            hooks.SetEyeHeight(z);
        }
        console.Print($"Eye height is {session.playerstate.EyeZ} ({GameConstants.MINEYE}-{GameConstants.MAXEYE}, 64 a story, {GameConstants.EYEDEFAULT} standing)");
    }

    internal void Cmd_PlayerClass(string[] args)
    {
        var classes = classRules.PlayerClasses();
        if (args.Length > 0)
        {
            // Any class descended from Player works, listed in game-info or not (for trying one out)
            hooks.SetNewGamePlayerClass(classRules.FindPlayerClass(args[0])
                ?? throw new ArgumentException($"no player class {args[0]}; there's {string.Join(", ", classes)}"));
        }

        if (hooks.InGame())
            console.Print($"Playing as {session.playerstate.playerclass}");
        console.Print($"New games are played as {hooks.NewGamePlayerClass() ?? classRules.DefaultPlayerClass}");
        console.Print($"Classes: {string.Join(", ", classes)}");
    }

    internal void Cmd_Give(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("give what? e.g. give all, give health 50, give GoldKey");

        int? amount = args.Length > 1 ? ParseInt(args[1], 1, 1_000_000) : null;

        switch (args[0].ToLowerInvariant())
        {
            case "all":
                stats.HealSelf(classRules.MaxHealth);
                GiveAllWeapons();
                weapons.GiveAllAmmo(int.MaxValue);
                GiveAllKeys();
                console.Print("Gave everything");
                break;

            case "health":
                stats.HealSelf(amount ?? classRules.MaxHealth);
                console.Print($"Health is {session.playerstate.health}");
                break;

            case "points":
                stats.GivePoints(amount ?? 100000);
                console.Print($"Score is {session.playerstate.score}");
                break;

            case "keys":
                GiveAllKeys();
                console.Print("Gave all keys");
                break;

            case "weapons":
                GiveAllWeapons();
                console.Print("Gave all weapons");
                break;

            case "ammo":
                console.Print($"Gave {weapons.GiveAllAmmo(amount ?? int.MaxValue)} ammo");
                break;

            default:
                GiveItem(args[0], amount ?? 1);
                break;
        }

        statusBar.DrawKeys();
        weapons.DrawWeapon();
        weapons.DrawAmmo();
    }

    /// <summary>Gives one named actordefs item: a key, some ammo, a weapon or armor.</summary>
    internal void GiveItem(string name, int amount)
    {
        // Health and treasure aren't held; they're applied on pickup, hence "give health/points".
        // A WeaponGiver only names the weapon to hand out, so it's never an item itself.
        var item = inventory.FindClass(name, "Weapon", "Ammo", "Key", "BasicArmor");
        if (item == null || inventory.FindClass(item, "WeaponGiver") != null)
            throw new ArgumentException($"\"{name}\" is not a weapon, ammo, key or armor");

        if (inventory.FindClass(item, "BasicArmor") != null)
        {
            stats.TryGiveArmor(item);
            console.Print($"Armor is {session.playerstate.armor} ({session.playerstate.armorpercent}%)");
        }
        else if (inventory.FindClass(item, "Weapon") != null)
        {
            // Goes through the pickup path so a better weapon also gets selected.
            weapons.TryGiveWeapon(item, 0);
            console.Print($"Gave {item}");
        }
        else if (inventory.FindClass(item, "Ammo") != null)
            console.Print($"Gave {weapons.GiveAmmo(item, amount)} {item}");
        else
            console.Print($"Gave {inventory.Give(item, amount)} {item}");
    }

    internal void GiveAllWeapons()
    {
        foreach (var weapon in weapons.AllWeapons())
            weapons.TryGiveWeapon(weapon, 0);
    }

    internal void GiveAllKeys()
    {
        foreach (var key in inventory.GetClassesDerivedFrom("Key"))
            inventory.Give(key, 1);
    }

    internal void Cmd_Map(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("which map? type \"maps\" for a list");

        var maps = engine.GetGameInfo().Maps;
        var map = maps.Keys.FirstOrDefault(k => string.Equals(k, args[0], StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"no map \"{args[0]}\"; type \"maps\" for a list");

        session.gamestate.mapon = map;
        hooks.SetPlayState(playstatetypes.ex_warped);
    }

    internal void Cmd_Teleport(string[] args)
    {
        if (args.Length < 2)
            throw new ArgumentException("usage: tp <tilex> <tiley> [angle]");

        int x = ParseInt(args[0], 0, mapManager.mapwidth - 1);
        int y = ParseInt(args[1], 0, mapManager.mapheight - 1);
        int? angle = args.Length > 2 ? ParseInt(args[2], 0, GameConstants.ANGLES - 1) : null;

        if (!TryTeleportPlayer(x, y, angle, out var problem))
            throw new ArgumentException(problem);

        console.Print($"Teleported to {x},{y}");
    }

    /// <summary>
    /// Moves the player to a tile, facing <paramref name="angle"/> (degrees, 0 east) when given.
    /// Only onto open floor: a solid tile, a door or a blocking object would trap the player, and
    /// an area number is needed for the sight/sound area bookkeeping.
    /// </summary>
    internal bool TryTeleportPlayer(int x, int y, int? angle, out string problem)
    {
        problem = "";
        if (x < 0 || y < 0 || x >= mapManager.mapwidth || y >= mapManager.mapheight)
        {
            problem = $"tile {x},{y} is off the level";
            return false;
        }
        if (mapManager.tilemap[x, y] != 0 || mapManager.actorat[x, y] != null
            || !mapManager.VALIDAREA(mapManager.MAPSPOT(x, y, 0)))
        {
            problem = $"tile {x},{y} is not open floor";
            return false;
        }

        session.player.SetPosition(x, y);
        session.player.AreaNumber = (byte)(mapManager.MAPSPOT(x, y, 0) - mapManager.Floors.AreaTile);
        if (angle is { } degrees)
            session.player.Angle = (short)(((degrees % GameConstants.ANGLES) + GameConstants.ANGLES) % GameConstants.ANGLES);
        doors.ConnectAreas();
        return true;
    }

    internal void Cmd_Diag(string[] args)
    {
        if (args.Length == 0 || !Enum.TryParse<WallShape>(args[0], ignoreCase: true, out var shape)
            || !Enum.IsDefined(shape))
            throw new ArgumentException("usage: diag <square|solidnw|solidne|solidsw|solidse> [tilex tiley]");

        var (x, y) = targets.TileArg(args, 1);

        // A trigger shares the object plane, so a pushwall can't also be a diagonal.
        if (!mapManager.IsPlainWall(x, y) || mapManager.GetTrigger(x, y) != null)
            throw new ArgumentException($"tile {x},{y} is not a plain wall");

        var marker = mapManager.SetWallShape(x, y, shape)
            ?? throw new ArgumentException($"the mapdefs have no diagonal marker for {shape}");

        console.Print($"Tile {x},{y} is now {shape} (plane 1: {marker})");
    }

    internal void Cmd_Summon(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: summon <class> [angle] [tilex tiley]");

        var className = assets.GetActorMetadata().Actors.Keys
            .FirstOrDefault(k => k.Equals(args[0], StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"no actor class named {args[0]}");

        // Facing back at the player by default, so a panel is seen from its front.
        int angle = args.Length > 1 ? ParseInt(args[1], 0, 359) : PlayerMovement.FacingDir(session.player.Angle) switch
        {
            controldirs.di_east => 180,
            controldirs.di_north => 270,
            controldirs.di_west => 0,
            _ => 90,
        };
        var (x, y) = targets.TileArg(args, 2);

        if (mapManager.tilemap[x, y] != 0 || mapManager.actorat[x, y] != null
            || !mapManager.VALIDAREA(mapManager.MAPSPOT(x, y, 0)))
            throw new ArgumentException($"tile {x},{y} is not open floor");

        var before = mapManager.GetActors().Last;
        mapManager.SpawnThing(x, y, new MapActorTranslation { Class = className, Angles = angle });
        var actor = mapManager.GetActors().Last;
        if (actor == before || actor == null)
            throw new ArgumentException($"{className} can't be spawned");

        console.Print($"Spawned {className} at {x},{y} facing {angle}{targets.WallSpriteDescription(actor.Value)}");
    }

    internal void Cmd_KillAll(string[] args)
    {
        // Snapshot first: Kill can drop items, which adds to the actor list.
        var enemies = mapManager.GetActors().OfType<Entities.Actors.Monster>()
            .Where(a => MapManager.IsEnemy(a) && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE))
            .ToList();

        foreach (var enemy in enemies)
            enemy.Kill();

        console.Print($"Killed {enemies.Count} enemies");
    }

    internal void Cmd_Hurt(string[] args)
    {
        stats.TakeDamage(args.Length > 0 ? ParseInt(args[0], 1, 1000) : 16, null!);
        console.Print($"Health is {session.playerstate.health}, armor {session.playerstate.armor} ({session.playerstate.armorpercent}%)");
    }
}
