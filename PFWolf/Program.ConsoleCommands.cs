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
        cheatCommands.Register();       // god, noclip, give, map, exitlevel, tp, killall, diag, summon, pitch, eyeheight, hurt, playerclass (Commands/CheatCommands.cs)
        debugCommands.Register();       // overhead, where, count, actors, maps, fps, slowmo, vbls, fade, screenshot (Commands/DebugCommands.cs)
        factionCommands.Register();     // factions, setfaction, settranslation, translations, playercolor (Commands/FactionCommands.cs)
        levelEditCommands.Register();       // wallheight, sky, weather, fog, light(s), zone*, height, tag, patch, flat(s) (Commands/LevelEditCommands.cs)
        Register("spectate", "Watches through another actor's eyes while you play on: the next or previous enemy (default next), the next of a class, or back to the player. Not saved; each level starts on the player.",
            "spectate [next|prev|player|<class>]", Cmd_Spectate, Cheat | InLevel,
            complete: (_, i) => i == 0 ? ["next", "prev", "player", .. SpectateCandidates().Select(a => a.Name).Distinct()] : []);
        playerCommands.Register();      // addplayer, controlplayer, gamemode (Commands/PlayerCommands.cs)

        //
        // debugging aids and information
        //
        Register("scripts", "Lists the Lua scripts that loaded (scripts/*.lua), those left out and why, and the actions they give.", "scripts", Cmd_Scripts);
        Register("lua", "Runs Lua in the scripts' state (`me` is the player). Strings take 'single quotes'; separate statements with spaces, not ;.",
            "lua <code>", Cmd_Lua, Cheat);
        demoCommands.Register();        // playdemo, demotest, recorddemo (Commands/DemoCommands.cs)
        saveCommands.Register();        // saves, autosave, save, load (Commands/SaveCommands.cs)
        Register("quit", "Quits the game immediately.", "quit", _ => _gameEngineManager.Quit(""));
    }

    // The cheats (Commands/CheatCommands.cs)
    static CheatCommands cheatCommands = null!;     // made in InitServices

    internal static bool TryTeleportPlayer(int x, int y, int? angle, out string problem) => cheatCommands.TryTeleportPlayer(x, y, angle, out problem);
    static void GiveAllKeys() => cheatCommands.GiveAllKeys();
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

    /*
    =============================================================================

                                    CHEATS

    =============================================================================
    */

    /*
    =============================================================================

                            DEBUGGING AIDS AND INFORMATION

    =============================================================================
    */


}
