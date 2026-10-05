using SDL2;
using System.Text;
using Wolf3D.Assets;
using Wolf3D.Configuration;
using Wolf3D.Enums;
using Wolf3D.Loaders;

namespace Wolf3D;

internal partial class Program
{
    const ConsoleCommandFlags Cheat = ConsoleCommandFlags.Cheat;
    const ConsoleCommandFlags InLevel = ConsoleCommandFlags.RequiresLevel;
    const ConsoleCommandFlags Solo = ConsoleCommandFlags.SinglePlayer;

    /// <summary>
    /// Registers the in-game console's commands. Commands that need Program's game state live
    /// here so they can reach it directly, the same way the actor actions do.
    /// </summary>
    internal static void RegisterConsoleCommands()
    {
        _consoleManager.CheatsEnabled = () => DebugOk != 0 && !netgame;      // never with others: every machine has to play alike
        _consoleManager.PlayingWithOthers = () => netgame;
        _consoleManager.LevelLoaded = () => _mapManager.Player != null;

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
        // binds (saved to binds.cfg on exit)
        //
        Register("bind", "Binds a key, mouse button, wheel turn or controller button to a command run when it's pressed in play, or shows its bind.",
            "bind <key or button> [command]", Cmd_Bind, complete: CompleteBind);
        Register("unbind", "Removes a key or button's bind.", "unbind <key or button>", Cmd_Unbind,
            complete: (_, i) => i == 0 ? _consoleManager.Binds.Keys.Select(input => input.ToString()) : []);
        Register("unbindall", "Removes every bind.", "unbindall", _ =>
        {
            _consoleManager.UnbindAll();
            _consoleManager.Print("All binds removed");
        });
        Register("binds", "Lists the key binds.", "binds", Cmd_Binds);

        //
        // controls (saved to controls.cfg on exit)
        //
        Register("bindaction", "Sets a control's keys and buttons: up to two keys, mouse buttons or wheel turns and one controller button. Lists them all with no control.",
            "bindaction [control] [key|\"Mouse 1-5\"|\"Wheel Up\"|\"Pad A\"|\"Joy 1-32\"...|none]", Cmd_BindAction, complete: CompleteBindAction);
        Register("resetcontrols", "Puts every control back to its default keys and buttons.", "resetcontrols", _ =>
        {
            controls.SetDefaults();
            _consoleManager.Print("Every control is back to its default");
        });
        Register("joy_deadzone", "How much of a controller stick's travel around the middle is ignored, in percent.",
            "joy_deadzone [0-50]", args => SetOrShow("joy_deadzone", args, ref joydeadzone, 0, 50));
        Register("joy_turnspeed", "How fast a controller's stick turns you, pushed all the way.",
            "joy_turnspeed [0-9]", args => SetOrShow("joy_turnspeed", args, ref joyturnspeed, 0, JOYTURNSPEEDS - 1));
        Register("m_look", "Whether moving the mouse up and down looks up and down (1) or walks (0).",
            "m_look [0|1]", args => ToggleOrShow("m_look", args, ref mouselook), complete: Values("0", "1"));
        Register("m_invert", "Whether mouse look is upside down: pushing the mouse away looks down.",
            "m_invert [0|1]", args => ToggleOrShow("m_invert", args, ref mouseinvert), complete: Values("0", "1"));
        Register("joy_sticks", "Which controller stick turns: the right (modern) or the left, with the right strafing (classic).",
            "joy_sticks [modern|classic]", Cmd_JoySticks, complete: Values("modern", "classic"));

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
        //
        Register("msg", "Shows a message over the view, in a style from hud-messages.yaml if the first word names one.",
            "msg [style] <text...>", Cmd_Msg, InLevel, complete: (_, i) => i == 0 ? _hudMessageManager.StyleNames : []);
        Register("msg_enabled", "Whether item pickups, locked doors and deaths show messages over the view; default goes back to the game's own choice.",
            "msg_enabled [0|1|default]", Cmd_MsgEnabled, complete: Values("0", "1", "default"));
        Register("msg_clear", "Takes away the messages shown over the view.", "msg_clear", _ => _hudMessageManager.Clear());
        Register("msg_styles", "Lists the message styles and where each puts its messages.", "msg_styles", Cmd_MsgStyles);
        Register("net_find", "Looks for games hosted on the local network (two seconds) and lists them.", "net_find", Cmd_NetFind);
        Register("net_status", "Shows the game with others: the level and frame, the players, and (hosting) how many checks found every machine agreeing.", "net_status", Cmd_NetStatus);
        Register("scoreboard", "Shows or hides the scoreboard over the view, playing with others (bind it to a key).", "scoreboard [0|1]", Cmd_Scoreboard, complete: Values("0", "1"));

        //
        // video
        //
        Register("vid_mode", "Shows the video mode.", "vid_mode", _ => PrintVideoMode());
        Register("vid_fullscreen", "Borderless fullscreen (1) or a window (0).", "vid_fullscreen [0|1]",
            args => ApplyVideo(s => s with { Fullscreen = Toggle(args, s.Fullscreen) }), complete: Values("0", "1"));
        Register("vid_scale", "Draws the game at 320x200 times this: sharper, not bigger.", "vid_scale <1-8>",
            args => ApplyVideo(s => s with { RenderScale = args.Length > 0 ? ParseInt(args[0], 1, 8) : throw new ArgumentException("usage: vid_scale <1-8>"), RenderSize = null, MatchWindow = false }),
            complete: Values("1", "2", "3", "4", "5", "6"));
        Register("vid_render", "Draws the game at any size and shape in place of 320x200 times vid_scale: auto follows the window (or the desktop when fullscreen); default goes back to vid_scale.",
            "vid_render <width> <height> | auto | default", Cmd_VidRender, complete: Values("auto", "default"));
        Register("vid_uiscale", "How big the menus, text and status bar are: screen pixels to each of their 320x200 pixels (2.5 is fine), or auto for the most whole number that fits.",
            "vid_uiscale <n|auto>", Cmd_VidUiScale, complete: Values("auto", "1", "1.5", "2", "2.5", "3", "4"));
        Register("vid_window", "The window's size when it isn't fullscreen.", "vid_window <width> <height>", Cmd_VidWindow);
        Register("vid_vsync", "Waits for the display's refresh before showing each frame.", "vid_vsync [0|1]",
            args => ApplyVideo(s => s with { VSync = Toggle(args, s.VSync) }), complete: Values("0", "1"));
        Register("vid_aspect", "Shows the picture at 4:3, the shape it had on a CRT (1), or with square pixels (0).",
            "vid_aspect [0|1]", args => ApplyVideo(s => s with { AspectCorrect = Toggle(args, s.AspectCorrect) }),
            complete: Values("0", "1"));
        Register("vid_filter", "How the picture is smoothed when scaled up to the window.", "vid_filter [nearest|linear]",
            args => ApplyVideo(s => s with { Filter = args.Length > 0 ? ParseFilter(args[0]) : s.Filter == ScaleFilter.Nearest ? ScaleFilter.Linear : ScaleFilter.Nearest }),
            complete: Values("nearest", "linear"));

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
        Register("overhead", "Shows the raw and filtered map overview.", "overhead", Cmd_Overhead, Cheat | InLevel);
        Register("diag", "Makes a wall tile (default: the one you face) a 45 degree wall, named by its solid corner.",
            "diag <square|solidnw|solidne|solidsw|solidse> [tilex tiley]", Cmd_Diag, Cheat | InLevel,
            complete: (_, i) => i == 0 ? Enum.GetNames<WallShape>().Select(n => n.ToLowerInvariant()) : []);
        Register("summon", "Spawns an actor on an open tile (default: the one you face), facing an angle (default: back at you).",
            "summon <class> [angle] [tilex tiley]", Cmd_Summon, Cheat | InLevel,
            complete: (_, i) => i == 0 ? _assetManager.GetActorMetadata().Actors.Keys : i == 1 ? ["0", "45", "90", "135", "180", "225", "270", "315"] : []);
        Register("wallheight", "How many stories tall the level's walls are, until the level is left or reloaded.",
            $"wallheight [1-{MAXWALLSTORIES}]", Cmd_WallHeight, Cheat | InLevel);
        Register("sky", "Draws a graphic (or wall texture) as the level's sky, until the level is left or reloaded; none for the ceiling color.",
            "sky [name|none]", Cmd_Sky, InLevel);
        Register("fog", "Fades the view toward a color with distance, from start to end tiles away, up to max percent, until the level is left or reloaded; none for no shading.",
            "fog [#RRGGBB|none] [start] [end] [max 0-100]", Cmd_Fog, Cheat | InLevel,
            complete: (_, i) => i == 0 ? ["none", "#000000", "#707070"] : []);
        Register("light", "Sets the level's light, 0 (black) to 255 (full), until the level is left or reloaded.",
            "light [0-255]", Cmd_Light, Cheat | InLevel);
        Register("lights", "Lists the actors giving off light (actordefs light.*, A_SetLight), and their light now.",
            "lights", Cmd_Lights, InLevel);
        Register("zone", "Shows or sets the light zone (plane 5) of the tile you stand on, a tile, or a rectangle of tiles; 0 takes them out of any zone.",
            "zone [0-65535 [tilex tiley [tilex2 tiley2]]]", Cmd_Zone, Cheat | InLevel);
        Register("zonelight", "Lists the level's light zones, or shows or sets how one is lit (light 0-255, optional #RRGGBB tint or none, optional fade tics; none as the light removes the zone).",
            "zonelight [zone [0-255|none] [#RRGGBB|none] [tics]]", Cmd_ZoneLight, Cheat | InLevel);
        Register("zoneeffect", "Makes a light zone flicker, pulse or strobe (none stops it), down to low, timed in tics (70 a second).",
            "zoneeffect <zone> <none|flicker|pulse|strobe> [low 0-255] [tics] [bright-tics]", Cmd_ZoneEffect, Cheat | InLevel,
            complete: (_, i) => i == 1 ? ["none", "flicker", "pulse", "strobe"] : []);
        Register("height","Sets how many stories tall a tile's wall is (default: the one you face); 0 uses the level's height. On open floor, 2 or more makes an arch.",
            $"height <0-{MAXWALLSTORIES}> [tilex tiley]", Cmd_Height, Cheat | InLevel);
        Register("tag", "Shows or sets a tile's tag on the tag plane (default: the one you face), and the tag of any actors on it; 0 clears it.",
            "tag [0-65535 [tilex tiley]]", Cmd_Tag, Cheat | InLevel);
        Register("flat", "Sets a tile's floor and ceiling flat indices (mapdefs flats) on the flat plane (default: the tile you stand on).",
            "flat <floor 0-255> <ceiling 0-255> [tilex tiley]", Cmd_Flat, Cheat | InLevel);
        Register("flats", "Sets the level's default floor and ceiling textures, for tiles the flat plane doesn't give one (not saved).",
            "flats [floor|none] [ceiling|none]", Cmd_Flats, InLevel);
        Register("spectate", "Watches through another actor's eyes while you play on: the next or previous enemy (default next), the next of a class, or back to the player. Not saved; each level starts on the player.",
            "spectate [next|prev|player|<class>]", Cmd_Spectate, Cheat | InLevel,
            complete: (_, i) => i == 0 ? ["next", "prev", "player", .. SpectateCandidates().Select(a => a.Name).Distinct()] : []);
        Register("addplayer", $"Adds a co-op player beside you, who stands still until you take them over with controlplayer (up to {MAXPLAYERS}; testing without the network).",
            "addplayer [class]", Cmd_AddPlayer, Cheat | InLevel, complete: (_, i) => i == 0 ? PlayerClasses() : []);
        Register("controlplayer", "Makes another player yours: your view, status bar and controls.",
            "controlplayer <n>", Cmd_ControlPlayer, Cheat | InLevel);
        Register("gamemode", "Shows or sets how the players play together: co-op (keys shared, no friendly fire) or deathmatch.",
            "gamemode [coop|deathmatch [fraglimit]]", Cmd_GameMode, Cheat | InLevel, complete: (_, i) => i == 0 ? ["coop", "deathmatch"] : []);

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
        Register("where", "Shows the player's position and what's at their tile.", "where", Cmd_Where, InLevel, aliases: ["pos"]);
        Register("count", "Counts doors and actors.", "count", Cmd_Count, InLevel);
        Register("actors", "Lists actors, optionally only those whose name contains the filter.", "actors [filter]", Cmd_Actors, InLevel,
            complete: (_, i) => i == 0 ? _mapManager.GetActors().Select(a => a.Name) : []);
        Register("maps", "Lists the levels that map can warp to.", "maps", Cmd_Maps);
        Register("mods", "Lists the mods loaded over pfwolf.pk3, in load order, and anything wrong with them.", "mods", Cmd_Mods);
        Register("assetinfo", "Shows where an asset came from: each file that added, replaced or merged into it, in order.",
            "assetinfo <name>", Cmd_AssetInfo, complete: (_, i) => i == 0 ? _assetManager.AssetNames : []);
        Register("exportmap", "Writes a level, or all of them, as ECWolf binary maps (NAME.wad) for a mod's maps/ folder: to the exports folder, or the folder given.",
            "exportmap <MAP##|all> [folder]", Cmd_ExportMap,
            complete: (_, i) => i == 0 ? ["all", .. _gameEngineManager.GetGameInfo().Maps.Keys] : []);
        Register("playdemo", "Plays a demo: one recorded with that number (DEMO#.dmo in the demos folder), or else the game's own. Ends the game in progress, then goes back to the title.",
            "playdemo <0-9>", Cmd_PlayDemo, Solo,
            complete: (_, i) => i == 0 ? Enumerable.Range(0, 10).Where(DemoExists).Select(n => n.ToString()) : []);
        Register("demotest", "Plays demos (all of them, or those given) back to back without waiting, and prints how each ended: score, kills, where the player and every actor finished. For checking a change to the game hasn't changed how it plays; run it from --exec.",
            "demotest [0-9 ...]", Cmd_DemoTest, Solo,
            complete: (_, _) => Enumerable.Range(0, 10).Where(DemoExists).Select(n => n.ToString()));
        Register("recorddemo", "Records a demo on a level, on the hardest skill, until the level ends or you die; saves it as that demo number, or asks for one. Ends the game in progress first.",
            "recorddemo <MAP##> [0-9]", Cmd_RecordDemo, Solo,
            complete: (_, i) => i switch
            {
                0 => Enumerable.Range(1, DemoMapCount()).Select(n => $"MAP{n:D2}"),
                1 => Enumerable.Range(0, 10).Select(n => n.ToString()),
                _ => []
            });
        Register("saves", "Lists the saved games, newest first, numbered for load.", "saves", Cmd_Saves);
        Register("autosave", "Whether each new level saves itself as it starts, to the Autosave.", "autosave [0|1]",
            Cmd_AutoSave, complete: Values("0", "1"));
        Register("save", "Saves the game. A name that's already saved is saved over; with none, it's a new save named for the level.",
            "save [name]", Cmd_Save, InLevel | Solo, complete: CompleteSaveName);
        Register("load", "Loads a saved game, by its number in saves or its name.", "load <number|name>", Cmd_Load, InLevel | Solo,
            complete: CompleteSaveName);
        Register("fps", "Toggles the frame rate counter.", "fps [0|1]", Cmd_Fps, complete: Values("0", "1"));
        Register("slowmo", "Waits extra VBLs every frame (0 = off).", "slowmo [0-50]", Cmd_SlowMo);
        Register("vbls", "Adds extra VBLs per frame (0 = off).", "vbls [0-8]", Cmd_Vbls);
        Register("fade", "Fades the view out to black and back in, to try a fade style.",
            "fade <palette|fizzle|melt|mosaic> [tics]", Cmd_Fade, InLevel,
            complete: Values(Enum.GetNames<FadeStyle>().Select(n => n.ToLowerInvariant()).ToArray()));
        Register("screenshot","Saves the screen, without the console, to WSHOT###.BMP in the screenshots folder.", "screenshot", _ => screenshotPending = true);
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
        new[] { "Weapon", "Ammo", "Key", "BasicArmor" }
            .SelectMany(_inventoryManager.GetClassesDerivedFrom)
            .Where(item => _inventoryManager.FindClass(item, "WeaponGiver") == null);

    static IEnumerable<string> CompleteBind(string[] args, int index) => index switch
    {
        0 => InputCode.AllNames(),
        1 => CommandNames(),
        _ => [],
    };

    static IEnumerable<string> CompleteBindAction(string[] args, int index) =>
        index == 0 ? ControlAction.All.Select(action => action.Name) : ["none", .. InputCode.AllNames()];

    static IEnumerable<string> ConfigScriptNames()
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(_gameEngineManager.GetConfigFilePath("x.cfg")));
        return dir != null && Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.cfg").Select(Path.GetFileName).OfType<string>()
            : [];
    }

    /*
    =============================================================================

                                KEY AND BUTTON NAMES

    Binds and controls use SDL's key names ("F5", "Keypad 1", "Left Shift"),
    "Mouse 1", "Wheel Up", "Joy 1" and "Pad A" (see InputCode), matched without
    regard to case, so they read the same in the .cfg files as on screen.

    =============================================================================
    */

    /// <summary>
    /// A key or button by name. Keys are folded as InputManager reports them: right-hand
    /// modifiers into left-hand ones, keypad Enter into Enter. Not the keypad arrows, which
    /// InputManager only folds while Num Lock is off: a keypad key stays itself, and works as
    /// that key when it's pressed with Num Lock on.
    /// </summary>
    static InputCode ParseInput(string name)
    {
        if (!InputCode.TryParse(name, out var code))
            throw new ArgumentException($"unknown key or button \"{name}\"");

        return code.Device == InputDevice.Key ? InputCode.FromKey(InputManager.FoldKey(code.Key)) : code;
    }

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
            throw new ArgumentException("usage: bind <key or button> [command]");

        var input = ParseInput(args[0]);

        if (args.Length == 1)
        {
            _consoleManager.Print(_consoleManager.Binds.TryGetValue(input, out var bound)
                ? $"\"{input}\" = \"{bound}\""
                : $"\"{input}\" is not bound");
            return;
        }

        // ` always toggles the console and Escape always opens the menu, before binds are seen.
        if (input.Key is ScanCodes.sc_Grave or ScanCodes.sc_Escape)
            throw new ArgumentException($"\"{input}\" is reserved");

        // One argument is the command line as-is (`bind F5 "god; noclip"`); several are
        // rejoined, re-quoting any that need it (`bind F5 give GoldKey`).
        var command = args.Length == 2 ? args[1] : string.Join(' ', args[1..].Select(QuoteArg));

        _consoleManager.Bind(input, command);
        _consoleManager.Print($"\"{input}\" = \"{command}\"");
    }

    private static void Cmd_Unbind(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: unbind <key or button>");

        var input = ParseInput(args[0]);
        _consoleManager.Print(_consoleManager.Unbind(input)
            ? $"\"{input}\" unbound"
            : $"\"{input}\" is not bound");
    }

    private static void Cmd_BindAction(string[] args)
    {
        if (args.Length == 0)
        {
            foreach (var each in ControlAction.All)
                _consoleManager.Print($"{each.Name} = {controls.FormatInputs(each)}");
            return;
        }

        if (!ControlAction.TryParse(args[0], out var action))
            throw new ArgumentException($"unknown control \"{args[0]}\"");

        if (args.Length > 1)
        {
            // All read before any is set, so a bad name leaves the control as it was
            List<InputCode> codes = args[1..] is ["none"] ? [] : args[1..].Select(ParseInput).ToList();
            if (codes.Count(code => !code.IsController) > ControlBindings.KeySlots || codes.Count(code => code.IsController) > 1)
                throw new ArgumentException($"a control takes up to {ControlBindings.KeySlots} keys or mouse buttons and one joystick button");

            controls.Clear(action);
            foreach (var code in codes)
                controls.Add(action, code);

            if (_consoleManager.IsRunningScript)    // controls.cfg sets every control at startup
                return;
        }

        _consoleManager.Print($"{action.Name} = {controls.FormatInputs(action)}");
    }

    /// <summary>
    /// Sets a number setting from args[0] if given, then shows it (quietly when controls.cfg
    /// sets it at startup).
    /// </summary>
    static void SetOrShow(string name, string[] args, ref int setting, int min, int max)
    {
        if (args.Length > 0)
        {
            setting = ParseInt(args[0], min, max);
            if (_consoleManager.IsRunningScript)
                return;
        }

        _consoleManager.Print($"{name} = {setting}");
    }

    /// <summary>
    /// Sets an on/off setting from args[0] if given (with none, shows it), quietly when
    /// controls.cfg sets it at startup.
    /// </summary>
    static void ToggleOrShow(string name, string[] args, ref bool setting)
    {
        if (args.Length > 0)
        {
            setting = Toggle(args, setting);
            if (_consoleManager.IsRunningScript)
                return;
        }

        _consoleManager.Print($"{name} = {(setting ? 1 : 0)}");
    }

    private static void Cmd_JoySticks(string[] args)
    {
        if (args.Length > 0)
        {
            joyclassicsticks = args[0].ToLowerInvariant() switch
            {
                "modern" => false,
                "classic" => true,
                _ => throw new ArgumentException($"expected modern or classic, got \"{args[0]}\""),
            };
            if (_consoleManager.IsRunningScript)
                return;
        }

        _consoleManager.Print($"joy_sticks = {JoySticksName}");
    }

    static string JoySticksName => joyclassicsticks ? "classic" : "modern";

    /// <summary>The mouse look and controller settings as console commands, for saving to controls.cfg.</summary>
    internal static IEnumerable<string> GetControllerSettingCommands() =>
    [
        $"m_look {(mouselook ? 1 : 0)}",
        $"m_invert {(mouseinvert ? 1 : 0)}",
        $"joy_deadzone {joydeadzone}",
        $"joy_turnspeed {joyturnspeed}",
        $"joy_sticks {JoySticksName}",
    ];

    private static void Cmd_Mods(string[] args)
    {
        var mods = _assetManager.LoadedMods;
        if (mods.Count == 0)
            _consoleManager.Print("No mods loaded");

        for (var i = 0; i < mods.Count; i++)
        {
            var mod = mods[i];
            var version = string.IsNullOrWhiteSpace(mod.Info.Version) ? "" : $" {mod.Info.Version}";
            var author = string.IsNullOrWhiteSpace(mod.Info.Author) ? "" : $" by {mod.Info.Author}";
            _consoleManager.Print($"{i + 1}. {mod.DisplayName}{version}{author} ({mod.FullPath})");
        }

        foreach (var warning in _assetManager.ModWarnings)
            _consoleManager.Print(warning);
    }

    private static void Cmd_AssetInfo(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: assetinfo <name>");

        var found = false;
        foreach (var (type, name, origins) in _assetManager.FindAssetOrigins(args[0]))
        {
            found = true;
            _consoleManager.Print($"{name} ({type})");
            foreach (var origin in origins)
            {
                _consoleManager.Print(origin.Action == AssetOrigin.LeftOut
                    ? $"  left out, couldn't be loaded: {origin.Source}: {origin.Path}"
                    : $"  {origin.Action} by {origin.Source}: {origin.Path}");
            }
        }

        if (!found)
            _consoleManager.Print($"No asset named {args[0]}");
    }

    private static void Cmd_ExportMap(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: exportmap <MAP##|all> [folder]");

        var mapNames = args[0].Equals("all", StringComparison.OrdinalIgnoreCase)
            ? _gameEngineManager.GetGameInfo().Maps.Keys.ToList()
            : [args[0]];
        var folder = args.Length > 1 ? args[1] : _gameEngineManager.ConfigDirectories.ExportsDirectory;
        Directory.CreateDirectory(folder);

        var written = 0;
        foreach (var mapName in mapNames)
        {
            // The level as loaded, before a game changes it
            var map = _assetManager.Find<MapAsset>(mapName);
            if (map == null)
            {
                _consoleManager.Print($"No level named {mapName}");
                continue;
            }

            File.WriteAllBytes(Path.Combine(folder, $"{mapName.ToUpperInvariant()}.wad"), EcWolfMapLoader.Save(map, mapName));
            written++;
        }

        _consoleManager.Print($"Wrote {written} level{(written == 1 ? "" : "s")} to {Path.GetFullPath(folder)}");
    }

    private static void Cmd_Binds(string[] args)
    {
        if (_consoleManager.Binds.Count == 0)
        {
            _consoleManager.Print("Nothing is bound");
            return;
        }

        foreach (var (input, command) in _consoleManager.Binds.OrderBy(b => b.Key.ToString(), StringComparer.OrdinalIgnoreCase))
            _consoleManager.Print($"\"{input}\" = \"{command}\"");
    }

    /*
    =============================================================================

                                    VIDEO

    =============================================================================
    */

    /// <summary>Switches to the video mode <paramref name="change"/> makes of the current one, then shows it.</summary>
    static void ApplyVideo(Func<VideoSettings, VideoSettings> change)
    {
        var next = change(_videoManager.Settings);
        if (!_videoManager.ApplyVideoSettings(next))
            _consoleManager.Print("Couldn't switch to that mode; kept the old one");

        // A new screen buffer starts blank; the view redraws itself every frame, the rest doesn't
        if (_mapManager.Player != null && viewsize != 21)
            DrawPlayScreen();

        PrintVideoMode();
    }

    static void PrintVideoMode()
    {
        var s = _videoManager.Settings;
        var shown = s.Fullscreen ? "fullscreen" : $"{s.WindowWidth}x{s.WindowHeight} window";
        var size = s.MatchWindow ? "auto" : s.RenderSize != null ? "custom size" : $"scale {s.RenderScale}";
        var effective = VideoSettings.FormatScale(s.EffectiveUiScale);
        var ui = s.UiScale <= 0 ? $"auto ({effective})" : effective;
        _consoleManager.Print($"{s.RenderWidth}x{s.RenderHeight} ({size}) in a {shown}, ui scale {ui}");
        _consoleManager.Print($"vsync {(s.VSync ? 1 : 0)}  aspect {(s.AspectCorrect ? 1 : 0)}  filter {s.Filter.ToString().ToLowerInvariant()}");
    }

    private static void Cmd_VidWindow(string[] args)
    {
        if (args.Length < 2)
            throw new ArgumentException("usage: vid_window <width> <height>");

        int width = ParseInt(args[0], VideoSettings.BaseWidth, 16384);
        int height = ParseInt(args[1], VideoSettings.BaseHeight, 16384);
        ApplyVideo(s => s with { WindowWidth = width, WindowHeight = height });
    }

    private static void Cmd_VidRender(string[] args)
    {
        if (args.Length == 1 && args[0].Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            ApplyVideo(s => s with { RenderSize = null, MatchWindow = false });
            return;
        }
        if (args.Length == 1 && args[0].Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            ApplyVideo(s => s with { MatchWindow = true });
            return;
        }
        if (args.Length < 2)
            throw new ArgumentException("usage: vid_render <width> <height> | auto | default");

        int width = ParseInt(args[0], VideoSettings.BaseWidth, VideoSettings.MaxRenderWidth);
        int height = ParseInt(args[1], VideoSettings.BaseHeight, VideoSettings.MaxRenderHeight);
        ApplyVideo(s => s with { RenderSize = (width, height), MatchWindow = false });
    }

    private static void Cmd_VidUiScale(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: vid_uiscale <n|auto>");

        double scale = 0;
        if (!args[0].Equals("auto", StringComparison.OrdinalIgnoreCase)
            && !(double.TryParse(args[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scale)
                 && scale >= 1 && scale <= 32))
            throw new ArgumentException($"expected auto or a scale from 1 to 32 (2.5, say), got \"{args[0]}\"");

        // Kept to hundredths, as config.cfg saves it
        ApplyVideo(s => s with { UiScale = Math.Round(scale, 2) });
    }

    static ScaleFilter ParseFilter(string arg) =>
        Enum.TryParse<ScaleFilter>(arg, ignoreCase: true, out var filter) && Enum.IsDefined(filter)
            ? filter
            : throw new ArgumentException($"expected nearest or linear, got \"{arg}\"");

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

        // Only onto open floor: a solid tile, a door or a blocking object would trap the player,
        // and an area number is needed for the sight/sound area bookkeeping.
        if (_mapManager.tilemap[x, y] != 0 || _mapManager.actorat[x, y] != null
            || !_mapManager.VALIDAREA(_mapManager.MAPSPOT(x, y, 0)))
            throw new ArgumentException($"tile {x},{y} is not open floor");

        player.SetPosition(x, y);
        player.AreaNumber = (byte)(_mapManager.MAPSPOT(x, y, 0) - _mapManager.Floors.AreaTile);
        if (args.Length > 2)
            player.Angle = (short)ParseInt(args[2], 0, ANGLES - 1);
        ConnectAreas();

        _consoleManager.Print($"Teleported to {x},{y}");
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

    /// <summary>"  panel ..." for a wall sprite, else nothing.</summary>
    private static string WallSpriteDescription(Entities.Actors.Actor actor)
    {
        if (!IsWallSprite(actor))
            return "";
        if (GetWallSpriteSpan(actor) is not { } span)
            return "  panel (off its tile)";

        static string Point(double x, double y) =>
            $"{x / MapConstants.TILEGLOBAL:0.##},{y / MapConstants.TILEGLOBAL:0.##}";
        return $"  panel {WallSpriteAxis(actor.Dir)} facing {actor.Dir}, {Point(span.X1, span.Y1)} to {Point(span.X2, span.Y2)}";
    }

    /// <summary>
    /// The tile named by args[index] and args[index + 1], or when they aren't given, the tile in
    /// front of the player, as Cmd_Use picks it.
    /// </summary>
    private static (int x, int y) TileArg(string[] args, int index)
    {
        if (args.Length >= index + 2)
            return (ParseInt(args[index], 0, _mapManager.mapwidth - 1), ParseInt(args[index + 1], 0, _mapManager.mapheight - 1));

        int x = player.TileX, y = player.TileY;
        switch (FacingDir(player.Angle))
        {
            case controldirs.di_east: x++; break;
            case controldirs.di_north: y--; break;
            case controldirs.di_west: x--; break;
            default: y++; break;
        }
        return (x, y);
    }

    private static void Cmd_Height(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException($"usage: height <0-{MAXWALLSTORIES}> [tilex tiley]");

        int stories = ParseInt(args[0], 0, MAXWALLSTORIES);
        var (x, y) = TileArg(args, 1);

        _mapManager.SetWallStories(x, y, stories);
        _consoleManager.Print(stories == 0
            ? $"Tile {x},{y} is back to the level's height ({wallstories})"
            : $"Tile {x},{y} is now {stories} {(stories == 1 ? "story" : "stories")} tall");
    }

    private static void Cmd_Tag(string[] args)
    {
        if (args.Length is 2 or > 3)
            throw new ArgumentException("usage: tag [0-65535 [tilex tiley]]");

        var (x, y) = TileArg(args, 1);
        // The actors standing on the tile (not the player), which keep the tag they spawned with
        var actors = _mapManager.GetActors()
            .Where(a => a is not Entities.Actors.PlayerPawn && !a.IsRemoved && a.TileX == x && a.TileY == y)
            .ToList();

        if (args.Length > 0)
        {
            var tag = (ushort)ParseInt(args[0], 0, ushort.MaxValue);
            _mapManager.SetTag(x, y, tag);
            foreach (var actor in actors)
                actor.Tag = tag;
        }

        var tagged = actors.Count == 0 ? ""
            : "  actors: " + string.Join(", ", actors.Select(a => $"{a.Name} {a.Tag}"));
        _consoleManager.Print($"Tile {x},{y}: tag {_mapManager.GetTag(x, y)}{tagged}");
    }

    private static void Cmd_Flat(string[] args)
    {
        if (args.Length < 2)
            throw new ArgumentException("usage: flat <floor 0-255> <ceiling 0-255> [tilex tiley]");

        int floor = ParseInt(args[0], 0, 255), ceiling = ParseInt(args[1], 0, 255);
        var (x, y) = args.Length >= 4 ? TileArg(args, 2) : (player.TileX, player.TileY);

        _mapManager.SetFlats(x, y, (ushort)(floor | ceiling << 8));
        _consoleManager.Print($"Tile {x},{y}: floor {_mapManager.FlatName(x, y, false)}, ceiling {_mapManager.FlatName(x, y, true)}");
    }

    private static void Cmd_Flats(string[] args)
    {
        string? Texture(int i)
        {
            if (args[i].Equals("none", StringComparison.OrdinalIgnoreCase))
                return null;
            if (_assetManager.Find<TextureAsset>(args[i]) == null)
                throw new ArgumentException($"no texture named {args[i]}");
            return args[i];
        }

        if (args.Length > 0)
            _mapManager.BuildFlats(Texture(0), args.Length > 1 ? Texture(1) : _mapManager.DefaultCeiling);
        _consoleManager.Print($"Default floor: {_mapManager.DefaultFloor ?? "(color)"}  ceiling: {_mapManager.DefaultCeiling ?? "(color)"}");
    }

    private static void Cmd_Zone(string[] args)
    {
        if (args.Length is 2 or 4 or > 5)
            throw new ArgumentException("usage: zone [0-65535 [tilex tiley [tilex2 tiley2]]]");

        // the tile stood on, one given, or a rectangle between two
        var (x1, y1) = args.Length >= 3 ? TileArg(args, 1) : (player.TileX, player.TileY);
        var (x2, y2) = args.Length == 5 ? TileArg(args, 3) : (x1, y1);
        if (args.Length > 0)
        {
            var zone = (ushort)ParseInt(args[0], 0, ushort.MaxValue);
            for (int y = Math.Min(y1, y2); y <= Math.Max(y1, y2); y++)
                for (int x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
                    _mapManager.SetZone(x, y, zone);
        }

        int shown = _mapManager.GetZone(x1, y1);
        var tiles = (x1, y1) == (x2, y2) ? $"Tile {x1},{y1}" : $"Tiles {x1},{y1} to {x2},{y2}";
        _consoleManager.Print($"{tiles}: zone {shown} ({DescribeZone(shown)})");
    }

    private static void Cmd_ZoneLight(string[] args)
    {
        if (args.Length == 0)
        {
            if (levelzones.Count == 0)
                _consoleManager.Print("The level has no light zones");
            foreach (var id in levelzones.Keys.Order())
                _consoleManager.Print($"Zone {id}: {DescribeZone(id)}");
            return;
        }

        int zone = ParseInt(args[0], 1, ushort.MaxValue);
        if (args.Length > 1)
        {
            if (args[1].Equals("none", StringComparison.OrdinalIgnoreCase))
                RemoveZone(zone);
            else
            {
                int light = ParseInt(args[1], 0, 255);
                if (args.Length > 2)
                {
                    if (args[2].Equals("none", StringComparison.OrdinalIgnoreCase))
                        SetZoneColor(zone, null);
                    else if (IsRgb(args[2]))
                        SetZoneColor(zone, args[2]);
                    else
                        throw new ArgumentException($"expected a #RRGGBB color or none, got \"{args[2]}\"");
                }
                FadeZoneLight(zone, light, args.Length > 3 ? ParseInt(args[3], 0, 7000) : 0);
            }
        }
        _consoleManager.Print($"Zone {zone}: {DescribeZone(zone)}");
    }

    private static void Cmd_Lights(string[] args)
    {
        var lights = DescribeActorLights().ToList();
        if (lights.Count == 0)
            _consoleManager.Print("Nothing on the level gives off light");
        foreach (var line in lights)
            _consoleManager.Print(line);
        if (lights.Count > 0 && !shading)
            _consoleManager.Print("(the level is at full light, so they don't show)");
    }

    private static void Cmd_ZoneEffect(string[] args)
    {
        if (args.Length < 2 || !TryParseZoneEffect(args[1], out var effect))
            throw new ArgumentException("usage: zoneeffect <zone> <none|flicker|pulse|strobe> [low 0-255] [tics] [bright-tics]");

        int zone = ParseInt(args[0], 1, ushort.MaxValue);
        var state = ZoneFor(zone);
        if (args.Length > 2) state.LowSetting = ParseInt(args[2], 0, 255);
        if (args.Length > 3) state.TicsSetting = ParseInt(args[3], 1, 7000);
        if (args.Length > 4) state.BrightTics = ParseInt(args[4], 0, 7000);
        SetZoneEffect(zone, effect);
        _consoleManager.Print($"Zone {zone}: {DescribeZone(zone)}");
    }

    static string DescribeZone(int zone)
    {
        if (zone == 0)
            return $"the level's light, {levelshading?.Light ?? 255}";
        if (!levelzones.TryGetValue(zone, out var z))
            return "not in the map's zones: the level's light";

        var text = $"light {z.Light}";
        if (z.FadeLeft > 0)
            text += $" (fading, now {z.BaseLight}, {z.FadeLeft} tics to go)";
        if (z.Color != null)
            text += $", tinted {z.Color}";
        if (z.Effect != ZoneEffect.None)
            text += $", {z.Effect.ToString().ToLowerInvariant()} to {z.Low} every {z.Tics} tics"
                + (z.Effect == ZoneEffect.Strobe ? $" ({z.BrightTics} bright)" : "");
        return text;
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
        _consoleManager.Print($"Health is {playerstate.health}, armor {playerstate.armor} ({playerstate.armorpercent}%)");
    }

    private static void Cmd_Where(string[] args)
    {
        int x = player.TileX, y = player.TileY;

        _consoleManager.Print($"X: {player.X} ({player.X % MapConstants.TILEGLOBAL})  Y: {player.Y} ({player.Y % MapConstants.TILEGLOBAL})  Angle: {player.Angle}");
        _consoleManager.Print($"Tile: {x},{y}  Area: {player.AreaNumber}  Map: {gamestate.mapon}");
        _consoleManager.Print($"tilemap: {_mapManager.tilemap[x, y]}  plane 1: {_mapManager.MAPSPOT(x, y, 1)}  spotvis: {_mapManager.spotvis[x, y]}");
        _consoleManager.Print($"actorat: {_mapManager.actorat[x, y]?.GetType().Name ?? "(nothing)"}");

        var diagonals = new[] { (x + 1, y), (x, y - 1), (x - 1, y), (x, y + 1) }
            .Where(t => _mapManager.wallshape[t.Item1, t.Item2] != WallShape.Square)
            .Select(t => $"{t.Item1},{t.Item2} {_mapManager.wallshape[t.Item1, t.Item2]}");
        if (diagonals.Any())
            _consoleManager.Print($"diagonal walls beside: {string.Join("  ", diagonals)}");

        var (fx, fy) = TileArg([], 0);
        _consoleManager.Print($"wall heights: level {wallstories}  tallest {_mapManager.MaxWallStories}  "
            + $"faced tile {fx},{fy}: {_mapManager.WallStories(fx, fy)} (plane 3: {_mapManager.MAPSPOT(fx, fy, MapManager.HEIGHTPLANE)})");

        _consoleManager.Print($"flats: floor {_mapManager.FlatName(x, y, false)}  ceiling {_mapManager.FlatName(x, y, true)}  "
            + $"(plane 2: {_mapManager.MAPSPOT(x, y, MapManager.FLATPLANE)})");

        _consoleManager.Print($"tags (plane 4): tile {_mapManager.GetTag(x, y)}  faced tile {fx},{fy}: {_mapManager.GetTag(fx, fy)}");

        int zone = _mapManager.GetZone(x, y);
        _consoleManager.Print($"light zone (plane 5): {zone} ({DescribeZone(zone)})");
    }

    private static void Cmd_Count(string[] args)
    {
        var actors = _mapManager.GetActors();
        int enemies = actors.Count(MapManager.IsEnemy);
        int alive = actors.Count(a => MapManager.IsEnemy(a) && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE));
        int active = actors.Count(a => a.Active != activetypes.ac_no);

        _consoleManager.Print($"Doors: {doornum}");
        for (int i = 0; i < doornum; i++)
        {
            var door = doorobjlist[i];
            if (!string.IsNullOrEmpty(door.Lock))
                _consoleManager.Print($"  locked ({door.Lock}) at {door.tilex},{door.tiley}");
        }
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
            if (actor.Tag != 0)
                line.Append($"  tag {actor.Tag}");
            line.Append(WallSpriteDescription(actor));

            _consoleManager.Print(line.ToString());
            shown++;
        }

        _consoleManager.Print($"{shown} actor(s)");
    }

    // Queues the demo for the title loop; in a level the game ends first (PlayLoop exits on the
    // abort, and GameLoop, seeing pendingDemo, goes back to the title without a death or scores)
    private static void Cmd_PlayDemo(string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out int demonumber) || demonumber < 0 || demonumber > 9)
            throw new ArgumentException("usage: playdemo <0-9>");

        if (!DemoExists(demonumber))
        {
            _consoleManager.Print($"There's no demo {demonumber}");
            return;
        }

        pendingDemo = demonumber;
        if (ingame)
            playstate = playstatetypes.ex_abort;
    }

    private static void Cmd_DemoTest(string[] args)
    {
        if (ingame)
        {
            _consoleManager.Print("demotest runs from the title or --exec, not during a game");
            return;
        }

        var demos = args.Length > 0
            ? args.Select(a => int.TryParse(a, out var n) ? n : -1).ToList()
            : Enumerable.Range(0, 10).Where(DemoExists).ToList();

        demoTesting = true;
        try
        {
            foreach (var demonumber in demos)
            {
                if (demonumber < 0 || demonumber > 9 || !DemoExists(demonumber))
                    Console.WriteLine($"demotest {demonumber}: no such demo");
                else
                    PlayDemo(demonumber);
            }
        }
        finally
        {
            demoTesting = false;
        }
    }

    /// <summary>Set while demotest runs: demos play without waiting, and each prints <see cref="DemoTestReport"/> as it ends</summary>
    internal static bool demoTesting;

    /// <summary>
    /// How a demo ended, for demotest: how far it got, the player's stats and spot, and a hash of
    /// every actor's class, position, health and state, so two builds can be compared line for line
    /// </summary>
    internal static string DemoTestReport(int demonumber)
    {
        var actors = _mapManager.GetActors().Where(a => !a.IsRemoved && !ReferenceEquals(a, player)).ToList();
        ulong hash = 14695981039346656037UL;    // FNV-1a
        foreach (var a in actors)
            foreach (var c in $"{a.Name},{a.X},{a.Y},{a.Hitpoints},{a.CurrentState?.StateName}|")
                hash = (hash ^ c) * 1099511628211UL;

        return $"demotest {demonumber}: data {demoptr}/{lastdemoptr} score {playerstate.score} health {playerstate.health} "
            + $"kills {gamestate.killcount}/{gamestate.killtotal} treasure {gamestate.treasurecount}/{gamestate.treasuretotal} "
            + $"player {player.X},{player.Y},{player.Angle} actors {actors.Count} hash {hash:x16}";
    }

    // Queued like playdemo. The map is MAP## or just its number; a demo can only name MAP01 onwards,
    // one byte's worth, so it has to be one of the maps counting up from MAP01
    private static void Cmd_RecordDemo(string[] args)
    {
        const string usage = "usage: recorddemo <MAP##> [0-9]";
        if (args.Length == 0)
            throw new ArgumentException(usage);

        var mapArg = args[0].StartsWith("MAP", StringComparison.OrdinalIgnoreCase) ? args[0][3..] : args[0];
        int maps = DemoMapCount();
        if (!int.TryParse(mapArg, out int level) || level < 1 || level > maps)
        {
            _consoleManager.Print(maps == 0 ? "No map can be recorded: there's no MAP01" : $"A demo can be recorded on MAP01 to MAP{maps:D2}");
            return;
        }

        int? demonumber = null;
        if (args.Length > 1)
        {
            if (!int.TryParse(args[1], out int number) || number < 0 || number > 9)
                throw new ArgumentException(usage);
            demonumber = number;
        }

        pendingRecord = new DemoRecordRequest(level, demonumber);
        if (ingame)
            playstate = playstatetypes.ex_abort;
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

    static IEnumerable<string> CompleteSaveName(string[] args, int index) =>
        index == 0 ? ListSaveGames().Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase) : [];

    private static void Cmd_Saves(string[] args)
    {
        var saves = ListSaveGames();
        if (saves.Count == 0)
        {
            _consoleManager.Print($"No saved games in {_gameEngineManager.ConfigDirectories.SaveGameDirectory}");
            return;
        }

        for (int i = 0; i < saves.Count; i++)
        {
            var s = saves[i];
            var kind = s.Kind == SaveKind.Normal ? "" : $" [{s.Kind.ToString().ToLowerInvariant()}]";
            _consoleManager.Print($"{i + 1,3}. {s.Name}{kind} - {s.MapName}, {s.SavedAt.ToLocalTime():yyyy-MM-dd HH:mm}, played {FormatPlayTime(s.PlayTime)}");
        }
    }

    private static void Cmd_Msg(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: msg [style] <text...>");

        // A first word naming a style is the style, as long as there's text after it
        if (args.Length > 1 && _hudMessageManager.StyleExists(args[0]))
            _hudMessageManager.Show(HudMessageKind.Other, string.Join(' ', args[1..]), args[0]);
        else
            _hudMessageManager.Show(HudMessageKind.Other, string.Join(' ', args));
    }

    private static void Cmd_MsgEnabled(string[] args)
    {
        if (args.Length > 0)
        {
            _hudMessageManager.EnabledSetting = args[0].Equals("default", StringComparison.OrdinalIgnoreCase)
                ? null
                : ParseBool(args[0]);
            if (!_hudMessageManager.Enabled)
                _hudMessageManager.Clear();
        }
        _consoleManager.Print($"msg_enabled is {(_hudMessageManager.Enabled ? 1 : 0)}" +
            (_hudMessageManager.EnabledSetting == null ? " (the game's default)" : ""));
    }

    private static void Cmd_MsgStyles(string[] args)
    {
        foreach (var name in _hudMessageManager.StyleNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var s = _hudMessageManager.FindStyle(name);
            _consoleManager.Print($"{s.Name}: {s.Anchor} {s.X:+0;-0;0},{s.Y:+0;-0;0} margin {s.Margin}, {s.Font} in {s.Color}, " +
                $"{s.Duration} tics, {s.MaxLines} line{(s.MaxLines == 1 ? "" : "s")}");
        }
    }

    private static void Cmd_AutoSave(string[] args)
    {
        if (args.Length > 0)
            autosaveEnabled = ParseBool(args[0]);

        _consoleManager.Print($"autosave is {(autosaveEnabled ? 1 : 0)}");
    }

    // A save by its number in the saves list or its name (the newest, if several share it)
    private static SaveInfo FindSaveGame(string arg)
    {
        var saves = ListSaveGames();
        if (int.TryParse(arg, out var number))
        {
            if (number < 1 || number > saves.Count)
                throw new ArgumentException($"there's no save {number}; saves lists them");
            return saves[number - 1];
        }

        return saves.FirstOrDefault(s => string.Equals(s.Name, arg, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"there's no save called \"{arg}\"; saves lists them");
    }

    private static void Cmd_Save(string[] args)
    {
        var name = args.Length > 0 ? string.Join(' ', args) : GetMapDisplayName(gamestate.mapon);
        if (name.Length > MaxGameName - 1)
            name = name[..(MaxGameName - 1)];

        var path = args.Length > 0
            && ListSaveGames().FirstOrDefault(s => s.Kind == SaveKind.Normal
                && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) is { } existing
            ? existing.Path
            : NewSaveGamePath();

        if (!SaveTheGame(path, name, 0, 0))
            throw new ArgumentException($"couldn't write {path}");

        _consoleManager.Print($"Saved \"{name}\" to {Path.GetFileName(path)}");
    }

    private static void Cmd_Load(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("usage: load <number|name>");

        var save = FindSaveGame(string.Join(' ', args));

        // Between frames, not mid-command: the load replaces every actor, the player included.
        _consoleManager.Defer(() =>
        {
            loadedgame = true;
            if (LoadTheGame(save.Path, 0, 0))
                playstate = playstatetypes.ex_abort;    // GameLoop redraws and restarts music for the loaded level
            else
                loadedgame = false;
        });
    }

    private static void Cmd_WallHeight(string[] args)
    {
        if (args.Length > 0)
            wallstories = ParseInt(args[0], 1, MAXWALLSTORIES);
        _consoleManager.Print($"Walls are {wallstories} {(wallstories == 1 ? "story" : "stories")} tall");
    }

    private static void Cmd_Sky(string[] args)
    {
        if (args.Length > 0)
        {
            var name = args[0].Equals("none", StringComparison.OrdinalIgnoreCase) ? null : args[0];
            if (name != null && _assetManager.Find<GraphicAsset>(name) == null && _assetManager.Find<TextureAsset>(name) == null)
                throw new ArgumentException($"no graphic or texture named {name}");
            levelsky = name;
        }
        _consoleManager.Print(levelsky == null ? "No sky: the ceiling is a color" : $"Sky: {levelsky}");
    }

    // The level's shading, or what shading starts from when it has none
    static ShadingSettings CurrentShading => levelshading ?? new ShadingSettings("#000000", 0, 16, 0, 255);

    private static void Cmd_Fog(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("none", StringComparison.OrdinalIgnoreCase))
            SetShading(null);
        else if (args.Length > 0)
        {
            var color = args[0];
            try { Entities.Color.FromHexRGBA(color); }
            catch (Exception) { throw new ArgumentException($"expected a #RRGGBB color, got \"{color}\""); }

            var s = CurrentShading with { FadeColor = color, MaxFade = 100 };
            if (args.Length > 1) s = s with { FadeStart = ParseTiles(args[1]) };
            if (args.Length > 2) s = s with { FadeEnd = ParseTiles(args[2]) };
            if (args.Length > 3) s = s with { MaxFade = ParseInt(args[3], 0, 100) };
            SetShading(s);
        }

        _consoleManager.Print(levelshading is { MaxFade: > 0 } f
            ? $"Fog: {f.FadeColor} from {f.FadeStart} to {f.FadeEnd} tiles, up to {f.MaxFade}%"
            : "No fog");
    }

    private static void Cmd_Light(string[] args)
    {
        if (args.Length > 0)
            SetShading(CurrentShading with { Light = ParseInt(args[0], 0, 255) });
        _consoleManager.Print($"Light: {levelshading?.Light ?? 255}");
    }

    static double ParseTiles(string arg)
    {
        if (!double.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tiles)
            || tiles < 0 || tiles > MapManager.MAPSIZE)
            throw new ArgumentException($"expected a number of tiles from 0 to {MapManager.MAPSIZE}, got \"{arg}\"");
        return tiles;
    }

    private static void Cmd_Fps(string[] args)
    {
        fpscounter = Toggle(args, fpscounter);
        _consoleManager.Print(fpscounter ? "FPS counter ON" : "FPS counter OFF");
    }

    private static void Cmd_Fade(string[] args)
    {
        if (args.Length == 0 || !Enum.TryParse<FadeStyle>(args[0], ignoreCase: true, out var style))
            throw new ArgumentException("usage: fade <palette|fizzle|melt|mosaic> [tics]");

        // Without a length, as long as a screen fade
        int? tics = args.Length > 1 ? ParseInt(args[1], 1, 700) : _videoManager.FadeTics;

        // Fading blocks, so run it from the play loop with the console out of the picture.
        _consoleManager.Close();
        _consoleManager.Defer(() =>
        {
            ThreeDRefresh();
            _videoManager.FadeOut(style, new Color { Alpha = 255 }, 30, tics);
            _videoManager.FadeIn(style, 30, tics);
            lasttimecount = (int)GameEngineManager.GetTimeCount();
        });
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
