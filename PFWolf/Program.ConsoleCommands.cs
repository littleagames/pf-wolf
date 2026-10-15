using SDL2;
using System.Text;
using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Configuration;
using PFWolf.Enums;
using PFWolf.Loaders;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf;

internal partial class Program
{

    /// <summary>
    /// Registers the in-game console's commands. Commands that need Program's game state live
    /// here so they can reach it directly, the same way the actor actions do.
    /// </summary>
    internal static void RegisterConsoleCommands()
    {
        _consoleManager.CheatsEnabled = () => DebugOk != 0 && !netgame;      // never with others: every machine has to play alike
        _consoleManager.PlayingWithOthers = () => netgame;
        _consoleManager.LevelLoaded = () => _mapManager.Player != null;

        // the console itself, binds and controls, video, messages and assets (Commands/)
        new CoreCommands(_consoleManager, _gameEngineManager.GetConfigFilePath).Register();
        new BindCommands(_consoleManager, controls, controlSettings).Register();
        new VideoCommands(_consoleManager, _videoManager, RedrawAfterVideoChange).Register();
        new MessageCommands(_consoleManager, _hudMessageManager).Register();
        new AssetCommands(_consoleManager, _assetManager, _audioManager, _gameEngineManager).Register();

        //
        // automap
        //
        Register("automap", "Opens or closes the automap.", "automap", _ => ToggleAutomap(), InLevel);
        Register("am_style", "How the automap draws the level: wall textures and sprites, or flat colors.",
            "am_style [graphic|color]", Cmd_AmStyle, complete: Values("graphic", "color"));
        Register("am_overlay", "Whether the automap is drawn over the dimmed game view (1) or a solid backdrop (0).",
            "am_overlay [0|1]", Cmd_AmOverlay, complete: Values("0", "1"));
        Register("am_grid", "Whether the automap marks every tile edge with a faint grid when zoomed in.",
            "am_grid [0|1]", Cmd_AmGrid, complete: Values("0", "1"));
        Register("am_stats", "Whether the automap shows the level's name, kills, treasure, secrets and time.",
            "am_stats [0|1]", Cmd_AmStats, complete: Values("0", "1"));
        Register("am_reveal", "Shows the whole map on the automap, seen or not.", "am_reveal [0|1]", Cmd_AmReveal, Cheat,
            complete: Values("0", "1"));

        //
        // messages over the view
        Register("net_find", "Looks for games hosted on the local network (two seconds) and lists them.", "net_find", Cmd_NetFind);
        Register("net_portcheck", "Checks whether players on the internet can reach a game hosted here (the UDP port, default 10645): needs the port free, so not while hosting.", "net_portcheck [port]", Cmd_NetPortCheck);
        Register("net_status", "Shows the game with others: the level and frame, the players, and (hosting) how many checks found every machine agreeing.", "net_status", Cmd_NetStatus);
        Register("scoreboard", "Shows or hides the scoreboard over the view, playing with others (bind it to a key).", "scoreboard [0|1]", Cmd_Scoreboard, complete: Values("0", "1"));

        //
        // cheats (the old Tab debug keys, plus a few new ones)
        //
        Register("god", "God mode: 1 = on, 2 = on without the damage flash.", "god [0|1|2]", Cmd_God, Cheat | InLevel,
            complete: Values("0", "1", "2"));
        Register("noclip", "Walk through walls.", "noclip [0|1]", Cmd_Noclip, Cheat | InLevel,
            complete: Values("0", "1"));
        Register("give", "Gives an item (weapon, ammo, key or armor), or health, points, keys, weapons, ammo or all.",
            "give <item|health|points|keys|weapons|ammo|all> [amount]", Cmd_Give, Cheat | InLevel,
            complete: (_, i) => i == 0 ? ["all", "health", "points", "keys", "weapons", "ammo", .. GivableItems()] : []);
        Register("map", "Warps to a level.", "map <MAP##>", Cmd_Map, Cheat | InLevel, aliases: ["warp"],
            complete: (_, i) => i == 0 ? _gameEngineManager.GetGameInfo().Maps.Keys : []);
        Register("exitlevel", "Completes the current level.", "exitlevel",
            _ => playstate = playstatetypes.ex_completed, Cheat | InLevel);
        Register("tp", "Teleports to a tile.", "tp <tilex> <tiley> [angle]", Cmd_Teleport, Cheat | InLevel);
        Register("killall", "Kills every enemy on the level.", "killall", Cmd_KillAll, Cheat | InLevel);
        debugCommands.Register();       // overhead, where, count, actors, maps, fps, slowmo, vbls, fade, screenshot (Commands/DebugCommands.cs)
        Register("diag", "Makes a wall tile (default: the one you face) a 45 degree wall, named by its solid corner.",
            "diag <square|solidnw|solidne|solidsw|solidse> [tilex tiley]", Cmd_Diag, Cheat | InLevel,
            complete: (_, i) => i == 0 ? Enum.GetNames<WallShape>().Select(n => n.ToLowerInvariant()) : []);
        Register("summon", "Spawns an actor on an open tile (default: the one you face), facing an angle (default: back at you).",
            "summon <class> [angle] [tilex tiley]", Cmd_Summon, Cheat | InLevel,
            complete: (_, i) => i == 0 ? _assetManager.GetActorMetadata().Actors.Keys : i == 1 ? ["0", "45", "90", "135", "180", "225", "270", "315"] : []);
        factionCommands.Register();     // factions, setfaction, settranslation, translations, playercolor (Commands/FactionCommands.cs)
        levelEditCommands.Register();       // wallheight, sky, weather, fog, light(s), zone*, height, tag, patch, flat(s) (Commands/LevelEditCommands.cs)
        Register("spectate", "Watches through another actor's eyes while you play on: the next or previous enemy (default next), the next of a class, or back to the player. Not saved; each level starts on the player.",
            "spectate [next|prev|player|<class>]", Cmd_Spectate, Cheat | InLevel,
            complete: (_, i) => i == 0 ? ["next", "prev", "player", .. SpectateCandidates().Select(a => a.Name).Distinct()] : []);
        playerCommands.Register();      // addplayer, controlplayer, gamemode (Commands/PlayerCommands.cs)

        //
        // debugging aids and information
        //
        Register("pitch", "Looks up (positive) or down (negative) by that many degrees, as far as the view allows; 0 looks straight ahead.",
            "pitch [degrees]", Cmd_Pitch, InLevel);
        Register("eyeheight", $"Sets how high the view is above the floor, in texels (64 a story, {EYEDEFAULT} standing), until the level is left or reloaded.",
            $"eyeheight [{MINEYE}-{MAXEYE}]", Cmd_EyeHeight, InLevel);
        Register("hurt", "Damages the player.", "hurt [points]", Cmd_Hurt, InLevel | Solo);
        Register("playerclass", "Shows the class being played as and the classes there are; with a name, the class new games are played as.",
            "playerclass [class]", Cmd_PlayerClass, complete: (_, i) => i == 0 ? PlayerClasses() : []);
        Register("scripts", "Lists the Lua scripts that loaded (scripts/*.lua), those left out and why, and the actions they give.", "scripts", Cmd_Scripts);
        Register("lua", "Runs Lua in the scripts' state (`me` is the player). Strings take 'single quotes'; separate statements with spaces, not ;.",
            "lua <code>", Cmd_Lua, Cheat);
        demoCommands.Register();        // playdemo, demotest, recorddemo (Commands/DemoCommands.cs)
        saveCommands.Register();        // saves, autosave, save, load (Commands/SaveCommands.cs)
        Register("quit", "Quits the game immediately.", "quit", _ => _gameEngineManager.Quit(""));
    }

    // What commands act on, and how they describe it (Commands/ConsoleTargets.cs)
    static ConsoleTargets consoleTargets = null!;       // made in InitServices

    static (int x, int y) TileArg(string[] args, int index) => consoleTargets.TileArg(args, index);
    static List<Entities.Actors.Monster> ChosenActors(string? className) => consoleTargets.ChosenActors(className);
    static string WallSpriteDescription(Entities.Actors.Actor actor) => consoleTargets.WallSpriteDescription(actor);
    static string DescribePatch(int patch) => consoleTargets.DescribePatch(patch);
    static string DescribeZone(int zone) => consoleTargets.DescribeZone(zone);

    static void Register(string name, string help, string usage, Action<string[]> run,
        ConsoleCommandFlags flags = ConsoleCommandFlags.None, string[]? aliases = null, ConsoleCompleter? complete = null) =>
        _consoleManager.Register(name, help, usage, run, flags, aliases, complete);

    /// <summary>The play screen round the view, drawn again after a video mode change blanks it</summary>
    static void RedrawAfterVideoChange()
    {
        if (_mapManager.Player != null && viewsize != 21)
            DrawPlayScreen();
    }

    /// <summary>The mouse look and controller settings as console commands, for saving to controls.cfg.</summary>
    internal static IEnumerable<string> GetControllerSettingCommands() => controlSettings.SettingCommands();

    /// <summary>The item classes `give` accepts by name (see GiveItem).</summary>
    static IEnumerable<string> GivableItems() =>
        new[] { "Weapon", "Ammo", "Key", "BasicArmor" }
            .SelectMany(_inventoryManager.GetClassesDerivedFrom)
            .Where(item => _inventoryManager.FindClass(item, "WeaponGiver") == null);

    /*
    =============================================================================

                                    CHEATS

    =============================================================================
    */

    private static void Cmd_God(string[] args)
    {
        // 0 = off, 1 = on, 2 = on without the damage flash (ApplyDamageToPlayer)
        godmode = (byte)(args.Length > 0 ? ParseInt(args[0], 0, 2) : godmode == 0 ? 1 : 0);

        _consoleManager.Print(godmode switch
        {
            0 => "God mode OFF",
            1 => "God mode ON",
            _ => "God mode ON (no flash)",
        });
    }

    private static void Cmd_Noclip(string[] args)
    {
        noclip = (byte)(Toggle(args, noclip != 0) ? 1 : 0);
        _consoleManager.Print(noclip != 0 ? "No clipping ON" : "No clipping OFF");
    }

    private static void Cmd_Pitch(string[] args)
    {
        double max = MaxPitch();
        if (args.Length > 0)
        {
            if (!double.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double degrees))
                throw new ArgumentException("usage: pitch [degrees]");
            playerpitch = Math.Clamp(degrees, -max, max);
        }
        _consoleManager.Print($"Pitch is {playerpitch:0.#} degrees (up to {max:0.#} either way)");
    }

    private static void Cmd_EyeHeight(string[] args)
    {
        if (args.Length > 0)
        {
            if (!int.TryParse(args[0], out int z))
                throw new ArgumentException($"usage: eyeheight [{MINEYE}-{MAXEYE}]");
            SetEyeHeight(z);
        }
        _consoleManager.Print($"Eye height is {playereyez} ({MINEYE}-{MAXEYE}, 64 a story, {EYEDEFAULT} standing)");
    }

    private static void Cmd_PlayerClass(string[] args)
    {
        var classes = PlayerClasses();
        if (args.Length > 0)
        {
            // Any class descended from Player works, listed in game-info or not (for trying one out)
            newGamePlayerClass = FindPlayerClass(args[0])
                ?? throw new ArgumentException($"no player class {args[0]}; there's {string.Join(", ", classes)}");
        }

        if (ingame)
            _consoleManager.Print($"Playing as {playerstate.playerclass}");
        _consoleManager.Print($"New games are played as {newGamePlayerClass ?? DefaultPlayerClass}");
        _consoleManager.Print($"Classes: {string.Join(", ", classes)}");
    }

    private static void Cmd_Give(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("give what? e.g. give all, give health 50, give GoldKey");

        int? amount = args.Length > 1 ? ParseInt(args[1], 1, 1_000_000) : null;

        switch (args[0].ToLowerInvariant())
        {
            case "all":
                HealSelf(MaxHealth);
                GiveAllWeapons();
                GiveAllAmmo(int.MaxValue);
                GiveAllKeys();
                _consoleManager.Print("Gave everything");
                break;

            case "health":
                HealSelf(amount ?? MaxHealth);
                _consoleManager.Print($"Health is {playerstate.health}");
                break;

            case "points":
                GivePoints(amount ?? 100000);
                _consoleManager.Print($"Score is {playerstate.score}");
                break;

            case "keys":
                GiveAllKeys();
                _consoleManager.Print("Gave all keys");
                break;

            case "weapons":
                GiveAllWeapons();
                _consoleManager.Print("Gave all weapons");
                break;

            case "ammo":
                _consoleManager.Print($"Gave {GiveAllAmmo(amount ?? int.MaxValue)} ammo");
                break;

            default:
                GiveItem(args[0], amount ?? 1);
                break;
        }

        DrawKeys();
        DrawWeapon();
        DrawAmmo();
    }

    /// <summary>Gives one named actordefs item: a key, some ammo, a weapon or armor.</summary>
    static void GiveItem(string name, int amount)
    {
        // Health and treasure aren't held; they're applied on pickup, hence "give health/points".
        // A WeaponGiver only names the weapon to hand out, so it's never an item itself.
        var item = _inventoryManager.FindClass(name, "Weapon", "Ammo", "Key", "BasicArmor");
        if (item == null || _inventoryManager.FindClass(item, "WeaponGiver") != null)
            throw new ArgumentException($"\"{name}\" is not a weapon, ammo, key or armor");

        if (_inventoryManager.FindClass(item, "BasicArmor") != null)
        {
            TryGiveArmor(item);
            _consoleManager.Print($"Armor is {playerstate.armor} ({playerstate.armorpercent}%)");
        }
        else if (_inventoryManager.FindClass(item, "Weapon") != null)
        {
            // Goes through the pickup path so a better weapon also gets selected.
            TryGiveWeapon(item, 0);
            _consoleManager.Print($"Gave {item}");
        }
        else if (_inventoryManager.FindClass(item, "Ammo") != null)
            _consoleManager.Print($"Gave {GiveAmmo(item, amount)} {item}");
        else
            _consoleManager.Print($"Gave {_inventoryManager.Give(item, amount)} {item}");
    }

    static void GiveAllWeapons()
    {
        foreach (var weapon in AllWeapons())
            TryGiveWeapon(weapon, 0);
    }

    static void GiveAllKeys()
    {
        foreach (var key in _inventoryManager.GetClassesDerivedFrom("Key"))
            _inventoryManager.Give(key, 1);
    }

    private static void Cmd_Map(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("which map? type \"maps\" for a list");

        var maps = _gameEngineManager.GetGameInfo().Maps;
        var map = maps.Keys.FirstOrDefault(k => string.Equals(k, args[0], StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"no map \"{args[0]}\"; type \"maps\" for a list");

        gamestate.mapon = map;
        playstate = playstatetypes.ex_warped;
    }

    private static void Cmd_Teleport(string[] args)
    {
        if (args.Length < 2)
            throw new ArgumentException("usage: tp <tilex> <tiley> [angle]");

        int x = ParseInt(args[0], 0, _mapManager.mapwidth - 1);
        int y = ParseInt(args[1], 0, _mapManager.mapheight - 1);
        int? angle = args.Length > 2 ? ParseInt(args[2], 0, ANGLES - 1) : null;

        if (!TryTeleportPlayer(x, y, angle, out var problem))
            throw new ArgumentException(problem);

        _consoleManager.Print($"Teleported to {x},{y}");
    }

    /// <summary>
    /// Moves the player to a tile, facing <paramref name="angle"/> (degrees, 0 east) when given.
    /// Only onto open floor: a solid tile, a door or a blocking object would trap the player, and
    /// an area number is needed for the sight/sound area bookkeeping.
    /// </summary>
    internal static bool TryTeleportPlayer(int x, int y, int? angle, out string problem)
    {
        problem = "";
        if (x < 0 || y < 0 || x >= _mapManager.mapwidth || y >= _mapManager.mapheight)
        {
            problem = $"tile {x},{y} is off the level";
            return false;
        }
        if (_mapManager.tilemap[x, y] != 0 || _mapManager.actorat[x, y] != null
            || !_mapManager.VALIDAREA(_mapManager.MAPSPOT(x, y, 0)))
        {
            problem = $"tile {x},{y} is not open floor";
            return false;
        }

        player.SetPosition(x, y);
        player.AreaNumber = (byte)(_mapManager.MAPSPOT(x, y, 0) - _mapManager.Floors.AreaTile);
        if (angle is { } degrees)
            player.Angle = (short)(((degrees % ANGLES) + ANGLES) % ANGLES);
        ConnectAreas();
        return true;
    }

    private static void Cmd_Diag(string[] args)
    {
        if (args.Length == 0 || !Enum.TryParse<WallShape>(args[0], ignoreCase: true, out var shape)
            || !Enum.IsDefined(shape))
            throw new ArgumentException("usage: diag <square|solidnw|solidne|solidsw|solidse> [tilex tiley]");

        var (x, y) = TileArg(args, 1);

        // A trigger shares the object plane, so a pushwall can't also be a diagonal.
        if (!_mapManager.IsPlainWall(x, y) || _mapManager.GetTrigger(x, y) != null)
            throw new ArgumentException($"tile {x},{y} is not a plain wall");

        var marker = _mapManager.SetWallShape(x, y, shape)
            ?? throw new ArgumentException($"the mapdefs have no diagonal marker for {shape}");

        _consoleManager.Print($"Tile {x},{y} is now {shape} (plane 1: {marker})");
    }

    private static void Cmd_Summon(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: summon <class> [angle] [tilex tiley]");

        var className = _assetManager.GetActorMetadata().Actors.Keys
            .FirstOrDefault(k => k.Equals(args[0], StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"no actor class named {args[0]}");

        // Facing back at the player by default, so a panel is seen from its front.
        int angle = args.Length > 1 ? ParseInt(args[1], 0, 359) : FacingDir(player.Angle) switch
        {
            controldirs.di_east => 180,
            controldirs.di_north => 270,
            controldirs.di_west => 0,
            _ => 90,
        };
        var (x, y) = TileArg(args, 2);

        if (_mapManager.tilemap[x, y] != 0 || _mapManager.actorat[x, y] != null
            || !_mapManager.VALIDAREA(_mapManager.MAPSPOT(x, y, 0)))
            throw new ArgumentException($"tile {x},{y} is not open floor");

        var before = _mapManager.GetActors().Last;
        _mapManager.SpawnThing(x, y, new MapActorTranslation { Class = className, Angles = angle });
        var actor = _mapManager.GetActors().Last;
        if (actor == before || actor == null)
            throw new ArgumentException($"{className} can't be spawned");

        _consoleManager.Print($"Spawned {className} at {x},{y} facing {angle}{WallSpriteDescription(actor.Value)}");
    }

    private static void Cmd_KillAll(string[] args)
    {
        // Snapshot first: Kill can drop items, which adds to the actor list.
        var enemies = _mapManager.GetActors().OfType<Entities.Actors.Monster>()
            .Where(a => MapManager.IsEnemy(a) && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE))
            .ToList();

        foreach (var enemy in enemies)
            enemy.Kill();

        _consoleManager.Print($"Killed {enemies.Count} enemies");
    }

    /*
    =============================================================================

                            DEBUGGING AIDS AND INFORMATION

    =============================================================================
    */

    private static void Cmd_Hurt(string[] args)
    {
        TakeDamage(args.Length > 0 ? ParseInt(args[0], 1, 1000) : 16, null!);
        _consoleManager.Print($"Health is {playerstate.health}, armor {playerstate.armor} ({playerstate.armorpercent}%)");
    }


}
