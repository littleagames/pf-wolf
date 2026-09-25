using SDL2;
using System.Text;

namespace Wolf3D;

internal partial class Program
{
    const ConsoleCommandFlags Cheat = ConsoleCommandFlags.Cheat;
    const ConsoleCommandFlags InLevel = ConsoleCommandFlags.RequiresLevel;

    /// <summary>
    /// Registers the in-game console's commands. Commands that need Program's game state live
    /// here so they can reach it directly, the same way the actor actions do.
    /// </summary>
    internal static void RegisterConsoleCommands()
    {
        _consoleManager.CheatsEnabled = () => DebugOk != 0;
        _consoleManager.LevelLoaded = () => _mapManager.Player != null;
        _consoleManager.KeyName = KeyName;

        //
        // console
        //
        Register("help", "Lists commands, or describes one.", "help [command]", Cmd_Help, aliases: ["?"],
            complete: (_, i) => i == 0 ? CommandNames() : []);
        Register("echo", "Prints its arguments.", "echo <text...>",
            args => _consoleManager.Print(string.Join(' ', args)));
        Register("clear", "Clears the console output.", "clear", _ => _consoleManager.Clear(), aliases: ["cls"]);
        Register("history", "Lists previously entered lines.", "history", Cmd_History);
        Register("con_pause", "Whether the game pauses while the console is open.", "con_pause [0|1]", Cmd_ConPause,
            complete: Values("0", "1"));
        Register("exec", "Runs the commands in a script file (in the config folder unless a full path is given).",
            "exec <file>", Cmd_Exec, complete: (_, i) => i == 0 ? ConfigScriptNames() : []);

        //
        // key binds (saved to binds.cfg on exit)
        //
        Register("bind", "Binds a key to a command run when it's pressed in play, or shows its bind.",
            "bind <key> [command]", Cmd_Bind, complete: CompleteBind);
        Register("unbind", "Removes a key's bind.", "unbind <key>", Cmd_Unbind,
            complete: (_, i) => i == 0 ? _consoleManager.Binds.Keys.Select(KeyName) : []);
        Register("unbindall", "Removes every bind.", "unbindall", _ =>
        {
            _consoleManager.UnbindAll();
            _consoleManager.Print("All binds removed");
        });
        Register("binds", "Lists the key binds.", "binds", Cmd_Binds);

        //
        // automap
        //
        Register("automap", "Opens or closes the automap.", "automap", _ => ToggleAutomap(), InLevel);

        //
        // cheats (the old Tab debug keys, plus a few new ones)
        //
        Register("god", "God mode: 1 = on, 2 = on without the damage flash.", "god [0|1|2]", Cmd_God, Cheat | InLevel,
            complete: Values("0", "1", "2"));
        Register("noclip", "Walk through walls.", "noclip [0|1]", Cmd_Noclip, Cheat | InLevel,
            complete: Values("0", "1"));
        Register("give", "Gives an item, or health, points, keys, weapons, ammo or all.",
            "give <item|health|points|keys|weapons|ammo|all> [amount]", Cmd_Give, Cheat | InLevel,
            complete: (_, i) => i == 0 ? ["all", "health", "points", "keys", "weapons", "ammo", .. GivableItems()] : []);
        Register("map", "Warps to a level.", "map <MAP##>", Cmd_Map, Cheat | InLevel, aliases: ["warp"],
            complete: (_, i) => i == 0 ? _gameEngineManager.GetGameInfo().Maps.Keys : []);
        Register("exitlevel", "Completes the current level.", "exitlevel",
            _ => playstate = playstatetypes.ex_completed, Cheat | InLevel);
        Register("tp", "Teleports to a tile.", "tp <tilex> <tiley> [angle]", Cmd_Teleport, Cheat | InLevel);
        Register("killall", "Kills every enemy on the level.", "killall", Cmd_KillAll, Cheat | InLevel);
        Register("overhead", "Shows the raw and filtered map overview.", "overhead", Cmd_Overhead, Cheat | InLevel);

        //
        // debugging aids and information
        //
        Register("hurt", "Damages the player.", "hurt [points]", Cmd_Hurt, InLevel);
        Register("where", "Shows the player's position and what's at their tile.", "where", Cmd_Where, InLevel, aliases: ["pos"]);
        Register("count", "Counts doors and actors.", "count", Cmd_Count, InLevel);
        Register("actors", "Lists actors, optionally only those whose name contains the filter.", "actors [filter]", Cmd_Actors, InLevel,
            complete: (_, i) => i == 0 ? _mapManager.GetActors().Select(a => a.Name) : []);
        Register("maps", "Lists the levels that map can warp to.", "maps", Cmd_Maps);
        Register("save", "Saves the game to a load/save menu slot.", "save <slot 0-9> [name]", Cmd_Save, InLevel,
            complete: CompleteSaveSlot);
        Register("load", "Loads the game saved in a load/save menu slot.", "load <slot 0-9>", Cmd_Load, InLevel,
            complete: CompleteSaveSlot);
        Register("fps", "Toggles the frame rate counter.", "fps [0|1]", Cmd_Fps, complete: Values("0", "1"));
        Register("slowmo", "Waits extra VBLs every frame (0 = off).", "slowmo [0-50]", Cmd_SlowMo);
        Register("vbls", "Adds extra VBLs per frame (0 = off).", "vbls [0-8]", Cmd_Vbls);
        Register("screenshot", "Saves the screen, without the console, to WSHOT###.BMP in the screenshots folder.", "screenshot", _ => screenshotPending = true);
        Register("quit", "Quits the game immediately.", "quit", _ => _gameEngineManager.Quit(""));
    }

    static void Register(string name, string help, string usage, Action<string[]> run,
        ConsoleCommandFlags flags = ConsoleCommandFlags.None, string[]? aliases = null, ConsoleCompleter? complete = null) =>
        _consoleManager.Register(new ConsoleCommand(name, help, usage, run, flags, aliases, complete));

    /*
    =============================================================================

                                TAB COMPLETION

    =============================================================================
    */

    /// <summary>A completer offering a fixed set of values for the first argument.</summary>
    static ConsoleCompleter Values(params string[] values) => (_, i) => i == 0 ? values : [];

    static IEnumerable<string> CommandNames() => _consoleManager.Commands.Select(c => c.Name);

    /// <summary>The item classes `give` accepts by name (see GiveItem).</summary>
    static IEnumerable<string> GivableItems() =>
        new[] { "Weapon", "Ammo", "Key" }
            .SelectMany(_inventoryManager.GetClassesDerivedFrom)
            .Where(item => _inventoryManager.FindClass(item, "WeaponGiver") == null);

    static IEnumerable<string> CompleteBind(string[] args, int index) => index switch
    {
        0 => AllKeyNames(),
        1 => CommandNames(),
        _ => [],
    };

    static IEnumerable<string> ConfigScriptNames()
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(_gameEngineManager.GetConfigFilePath("x.cfg")));
        return dir != null && Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.cfg").Select(Path.GetFileName).OfType<string>()
            : [];
    }

    /*
    =============================================================================

                                    KEY NAMES

    Binds use SDL's scancode names ("F5", "Keypad 1", "Left Shift"), matched
    without regard to case, so they read the same in binds.cfg as on screen.

    =============================================================================
    */

    static string KeyName(ScanCodes key)
    {
        var name = SDL.SDL_GetScancodeName((SDL.SDL_Scancode)key);
        return string.IsNullOrEmpty(name) ? $"#{(int)key}" : name;
    }

    static ScanCodes ParseKey(string name)
    {
        var scancode = SDL.SDL_GetScancodeFromName(name);
        if (scancode == SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN)
            throw new ArgumentException($"unknown key \"{name}\"");

        // Binds are looked up by the key InputManager reports, which folds right-hand modifiers
        // (and keypad arrows without Num Lock) into their left-hand/arrow equivalents.
        return _inputManager.MapKey((ScanCodes)scancode);
    }

    static IEnumerable<string> AllKeyNames() =>
        Enumerable.Range(1, (int)SDL.SDL_Scancode.SDL_NUM_SCANCODES - 1)
            .Select(i => SDL.SDL_GetScancodeName((SDL.SDL_Scancode)i))
            .Where(name => !string.IsNullOrEmpty(name));

    /// <summary>Quotes an argument if Tokenize would otherwise split it.</summary>
    static string QuoteArg(string arg) =>
        arg.Length == 0 || arg.Any(c => char.IsWhiteSpace(c) || c == ';') ? $"\"{arg}\"" : arg;

    /*
    =============================================================================

                                ARGUMENT PARSING

    Throwing ArgumentException is the way to reject bad input: ConsoleManager
    prints the message and the command does nothing else.

    =============================================================================
    */

    static int ParseInt(string arg, int min, int max)
    {
        if (!int.TryParse(arg, out var value) || value < min || value > max)
            throw new ArgumentException($"expected a number from {min} to {max}, got \"{arg}\"");
        return value;
    }

    static bool ParseBool(string arg) => arg.ToLowerInvariant() switch
    {
        "1" or "on" or "true" => true,
        "0" or "off" or "false" => false,
        _ => throw new ArgumentException($"expected 0 or 1, got \"{arg}\""),
    };

    /// <summary>Sets a toggle from args[0] if given, otherwise flips it.</summary>
    static bool Toggle(string[] args, bool current) => args.Length > 0 ? ParseBool(args[0]) : !current;

    /*
    =============================================================================

                                    CONSOLE

    =============================================================================
    */

    private static void Cmd_Help(string[] args)
    {
        if (args.Length > 0)
        {
            if (!_consoleManager.TryGetCommand(args[0], out var command))
            {
                _consoleManager.Print($"Unknown command \"{args[0]}\".");
                return;
            }

            _consoleManager.Print($"{command.Name} - {command.Help}");
            _consoleManager.Print($"  usage: {command.Usage}");
            if (command.Aliases is { Length: > 0 })
                _consoleManager.Print($"  aliases: {string.Join(", ", command.Aliases)}");
            if (command.Flags.HasFlag(ConsoleCommandFlags.Cheat))
                _consoleManager.Print("  (cheat)");
            return;
        }

        foreach (var command in _consoleManager.Commands)
        {
            var cheat = command.Flags.HasFlag(ConsoleCommandFlags.Cheat) ? " (cheat)" : "";
            _consoleManager.Print($"{command.Name,-12}{command.Help}{cheat}");
        }
    }

    private static void Cmd_History(string[] args)
    {
        var history = _consoleManager.History;
        for (int i = 0; i < history.Count; i++)
            _consoleManager.Print($"{i + 1,3}  {history[i]}");
    }

    private static void Cmd_ConPause(string[] args)
    {
        if (args.Length > 0)
            _consoleManager.PauseWhenOpen = ParseBool(args[0]);

        _consoleManager.Print($"con_pause is {(_consoleManager.PauseWhenOpen ? 1 : 0)}");
    }

    private static void Cmd_Exec(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("exec which file?");

        var path = Path.IsPathRooted(args[0]) ? args[0] : _gameEngineManager.GetConfigFilePath(args[0]);
        if (!_consoleManager.ExecFile(path))
            throw new ArgumentException($"couldn't find \"{path}\"");
    }

    /*
    =============================================================================

                                    KEY BINDS

    =============================================================================
    */

    private static void Cmd_Bind(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: bind <key> [command]");

        var key = ParseKey(args[0]);

        if (args.Length == 1)
        {
            _consoleManager.Print(_consoleManager.Binds.TryGetValue(key, out var bound)
                ? $"\"{KeyName(key)}\" = \"{bound}\""
                : $"\"{KeyName(key)}\" is not bound");
            return;
        }

        // ` always toggles the console and Escape always opens the menu, before binds are seen.
        if (key is ScanCodes.sc_Grave or ScanCodes.sc_Escape)
            throw new ArgumentException($"\"{KeyName(key)}\" is reserved");

        // One argument is the command line as-is (`bind F5 "god; noclip"`); several are
        // rejoined, re-quoting any that need it (`bind F5 give GoldKey`).
        var command = args.Length == 2 ? args[1] : string.Join(' ', args[1..].Select(QuoteArg));

        _consoleManager.Bind(key, command);
        _consoleManager.Print($"\"{KeyName(key)}\" = \"{command}\"");
    }

    private static void Cmd_Unbind(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: unbind <key>");

        var key = ParseKey(args[0]);
        _consoleManager.Print(_consoleManager.Unbind(key)
            ? $"\"{KeyName(key)}\" unbound"
            : $"\"{KeyName(key)}\" is not bound");
    }

    private static void Cmd_Binds(string[] args)
    {
        if (_consoleManager.Binds.Count == 0)
        {
            _consoleManager.Print("No keys are bound");
            return;
        }

        foreach (var (key, command) in _consoleManager.Binds.OrderBy(b => KeyName(b.Key), StringComparer.OrdinalIgnoreCase))
            _consoleManager.Print($"\"{KeyName(key)}\" = \"{command}\"");
    }

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

    private static void Cmd_Give(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("give what? e.g. give all, give health 50, give GoldKey");

        int? amount = args.Length > 1 ? ParseInt(args[1], 1, 1_000_000) : null;

        switch (args[0].ToLowerInvariant())
        {
            case "all":
                HealSelf(100);
                GiveAllWeapons();
                GiveAmmo(AmmoType, int.MaxValue);
                GiveAllKeys();
                _consoleManager.Print("Gave everything");
                break;

            case "health":
                HealSelf(amount ?? 100);
                _consoleManager.Print($"Health is {gamestate.health}");
                break;

            case "points":
                GivePoints(amount ?? 100000);
                _consoleManager.Print($"Score is {gamestate.score}");
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
                var addedAmmo = GiveAmmo(AmmoType, amount ?? int.MaxValue);
                _consoleManager.Print($"Gave {addedAmmo} {AmmoType}");
                break;

            default:
                GiveItem(args[0], amount ?? 1);
                break;
        }

        DrawKeys();
        DrawWeapon();
        DrawAmmo();
    }

    /// <summary>Gives one named actordefs item: a key, some ammo or a weapon.</summary>
    static void GiveItem(string name, int amount)
    {
        // Health and treasure aren't held; they're applied on pickup, hence "give health/points".
        // A WeaponGiver only names the weapon to hand out, so it's never an item itself.
        var item = _inventoryManager.FindClass(name, "Weapon", "Ammo", "Key");
        if (item == null || _inventoryManager.FindClass(item, "WeaponGiver") != null)
            throw new ArgumentException($"\"{name}\" is not a weapon, ammo or key");

        if (_inventoryManager.FindClass(item, "Weapon") != null)
        {
            // Goes through the pickup path so a better weapon also gets selected.
            TryGiveWeapon(item, "None", 0);
            _consoleManager.Print($"Gave {item}");
        }
        else if (_inventoryManager.FindClass(item, "Ammo") != null)
            _consoleManager.Print($"Gave {GiveAmmo(item, amount)} {item}");
        else
            _consoleManager.Print($"Gave {_inventoryManager.Give(item, amount)} {item}");
    }

    static void GiveAllWeapons()
    {
        foreach (var weapon in WeaponSlotItems)
            TryGiveWeapon(weapon, "None", 0);
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

        // Only onto open floor: a solid tile, a door or a blocking object would trap the player,
        // and an area number is needed for the sight/sound area bookkeeping.
        if (_mapManager.tilemap[x, y] != 0 || _mapManager.actorat[x, y] != null
            || !MapManager.VALIDAREA(_mapManager.MAPSPOT(x, y, 0)))
            throw new ArgumentException($"tile {x},{y} is not open floor");

        player.SetPosition(x, y);
        player.AreaNumber = (byte)(_mapManager.MAPSPOT(x, y, 0) - MapDataConstants.AREATILE);
        if (args.Length > 2)
            player.Angle = (short)ParseInt(args[2], 0, ANGLES - 1);
        ConnectAreas();

        _consoleManager.Print($"Teleported to {x},{y}");
    }

    private static void Cmd_KillAll(string[] args)
    {
        // Snapshot first: KillActor can drop items, which adds to the actor list.
        var enemies = _mapManager.GetActors()
            .Where(a => MapManager.IsEnemy(a) && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE))
            .ToList();

        foreach (var enemy in enemies)
            KillActor(enemy);

        _consoleManager.Print($"Killed {enemies.Count} enemies");
    }

    private static void Cmd_Overhead(string[] args)
    {
        // BasicOverhead is a blocking screen that waits for a key, so it can't run while the
        // console owns the keyboard. Close it and run the view from the play loop instead.
        _consoleManager.Close();
        _consoleManager.Defer(() =>
        {
            BasicOverhead();
            if (viewsize < 20)
                DrawPlayBorder();
            lasttimecount = (int)GameEngineManager.GetTimeCount();
        });
    }

    /*
    =============================================================================

                            DEBUGGING AIDS AND INFORMATION

    =============================================================================
    */

    private static void Cmd_Hurt(string[] args)
    {
        TakeDamage(args.Length > 0 ? ParseInt(args[0], 1, 1000) : 16, null!);
        _consoleManager.Print($"Health is {gamestate.health}");
    }

    private static void Cmd_Where(string[] args)
    {
        int x = player.TileX, y = player.TileY;

        _consoleManager.Print($"X: {player.X} ({player.X % MapConstants.TILEGLOBAL})  Y: {player.Y} ({player.Y % MapConstants.TILEGLOBAL})  Angle: {player.Angle}");
        _consoleManager.Print($"Tile: {x},{y}  Area: {player.AreaNumber}  Map: {gamestate.mapon}");
        _consoleManager.Print($"tilemap: {_mapManager.tilemap[x, y]}  plane 1: {_mapManager.MAPSPOT(x, y, 1)}  spotvis: {_mapManager.spotvis[x, y]}");
        _consoleManager.Print($"actorat: {_mapManager.actorat[x, y]?.GetType().Name ?? "(nothing)"}");
    }

    private static void Cmd_Count(string[] args)
    {
        var actors = _mapManager.GetActors();
        int enemies = actors.Count(MapManager.IsEnemy);
        int alive = actors.Count(a => MapManager.IsEnemy(a) && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE));
        int active = actors.Count(a => a.Active != activetypes.ac_no);

        _consoleManager.Print($"Doors: {doornum}");
        _consoleManager.Print($"Actors: {actors.Count}  active: {active}");
        _consoleManager.Print($"Enemies: {enemies}  alive: {alive}");
        _consoleManager.Print($"Kills: {gamestate.killcount}/{gamestate.killtotal}  Secrets: {gamestate.secretcount}/{gamestate.secrettotal}  Treasure: {gamestate.treasurecount}/{gamestate.treasuretotal}");
    }

    private static void Cmd_Actors(string[] args)
    {
        string filter = args.Length > 0 ? args[0] : "";
        int shown = 0;

        foreach (var actor in _mapManager.GetActors())
        {
            if (!actor.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            var line = new StringBuilder($"{actor.Name} at {actor.TileX},{actor.TileY}");
            if (actor.CurrentState != null)
                line.Append($"  state {actor.CurrentState.StateName}");
            if (MapManager.IsEnemy(actor))
                line.Append($"  hp {actor.Hitpoints}");
            if (actor.Active != activetypes.ac_no)
                line.Append("  active");

            _consoleManager.Print(line.ToString());
            shown++;
        }

        _consoleManager.Print($"{shown} actor(s)");
    }

    private static void Cmd_Maps(string[] args)
    {
        // Ten to a row (an episode's worth in Wolf3D) so a whole game fits in the console.
        var names = _gameEngineManager.GetGameInfo().Maps.Keys
            .Select(name => name == gamestate?.mapon ? $"[{name}]" : name)
            .ToList();

        for (int i = 0; i < names.Count; i += 10)
            _consoleManager.Print(string.Join(' ', names.Skip(i).Take(10)));

        if (_mapManager.Player != null)
            _consoleManager.Print($"Current map is {gamestate.mapon}");
    }

    static IEnumerable<string> CompleteSaveSlot(string[] args, int index) =>
        index == 0 ? Enumerable.Range(0, SaveGamesAvail.Length).Select(i => i.ToString()) : [];

    private static void Cmd_Save(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: save <slot 0-9> [name]");

        var slot = ParseInt(args[0], 0, SaveGamesAvail.Length - 1);
        var name = args.Length > 1 ? string.Join(' ', args.Skip(1))
            : SaveGamesAvail[slot] != 0 ? SaveGameNames[slot]
            : gamestate.mapon;
        if (name.Length > MaxGameName - 1)
            name = name[..(MaxGameName - 1)];

        if (!SaveTheGame(GetSaveGamePath(slot), name, 0, 0))
            throw new ArgumentException($"couldn't write {GetSaveGamePath(slot)}");

        SaveGamesAvail[slot] = 1;
        SaveGameNames[slot] = name;
        _consoleManager.Print($"Saved \"{name}\" to slot {slot}");
    }

    private static void Cmd_Load(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: load <slot 0-9>");

        var slot = ParseInt(args[0], 0, SaveGamesAvail.Length - 1);
        if (SaveGamesAvail[slot] == 0)
            throw new ArgumentException($"slot {slot} is empty");

        // Between frames, not mid-command: the load replaces every actor, the player included.
        _consoleManager.Defer(() =>
        {
            loadedgame = true;
            if (LoadTheGame(GetSaveGamePath(slot), 0, 0))
                playstate = playstatetypes.ex_abort;    // GameLoop redraws and restarts music for the loaded level
            else
                loadedgame = false;
        });
    }

    private static void Cmd_Fps(string[] args)
    {
        fpscounter = Toggle(args, fpscounter);
        _consoleManager.Print(fpscounter ? "FPS counter ON" : "FPS counter OFF");
    }

    private static void Cmd_SlowMo(string[] args)
    {
        if (args.Length > 0)
            singlestep = (byte)ParseInt(args[0], 0, 50);
        _consoleManager.Print($"slowmo is {singlestep}");
    }

    private static void Cmd_Vbls(string[] args)
    {
        if (args.Length > 0)
            extravbls = ParseInt(args[0], 0, 8);
        _consoleManager.Print($"vbls is {extravbls}");
    }
}
