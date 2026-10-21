using SDL2;
using System.Text;
using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Configuration;
using PFWolf.Enums;
using PFWolf.Loaders;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf;

internal static partial class GameHost
{
    /// <summary>
    /// Registers the in-game console's commands: each group in Commands/ registers its own, and a few
    /// that belong to their screens or systems are registered here.
    /// </summary>
    internal static void RegisterConsoleCommands()
    {
        _consoleManager.CheatsEnabled = () => DebugOk != 0 && !NetGame;      // never with others: every machine has to play alike
        _consoleManager.PlayingWithOthers = () => NetGame;
        _consoleManager.LevelLoaded = () => MapManager.Player != null;

        // Each group's commands, for the documentation (each set before its commands register)
        void Group(string name) => _consoleManager.Group = name;

        // the console itself, binds and controls, video, messages and assets (Commands/)
        Group("The console");
        new CoreCommands(_consoleManager, GameEngineManager.GetConfigFilePath).Register();
        Register("quit", "Quits the game immediately.", "quit", _ => GameEngineManager.Quit(""));
        Group("Keys and controls");
        new BindCommands(_consoleManager, Controls, ControlSettings).Register();
        Group("Video");
        new VideoCommands(_consoleManager, _videoManager, RedrawAfterVideoChange).Register();
        Group("Messages");
        new MessageCommands(_consoleManager, HudMessageManager).Register();
        Group("Game options");
        new GameOptionCommands(_consoleManager, Settings.Options).Register();     // freelook, am_stats, autosave, weaponbob, alwaysrun, crosshair
        Group("Mods, sounds and music");
        new AssetCommands(_consoleManager, AssetManager, AudioManager, GameEngineManager).Register();

        //
        // automap
        //
        Group("The automap");
        Register("automap", "Opens or closes the automap.", "automap", _ => ToggleAutomap(), InLevel);
        Register("am_style", "How the automap draws the level: wall textures and sprites, or flat colors.",
            "am_style [graphic|color]", AmStyleCommand, complete: Values("graphic", "color"));
        Register("am_overlay", "Whether the automap is drawn over the dimmed game view (1) or a solid backdrop (0).",
            "am_overlay [0|1]", AmOverlayCommand, complete: Values("0", "1"));
        Register("am_grid", "Whether the automap marks every tile edge with a faint grid when zoomed in.",
            "am_grid [0|1]", AmGridCommand, complete: Values("0", "1"));
        Register("am_reveal", "Shows the whole map on the automap, seen or not.", "am_reveal [0|1]", AmRevealCommand, Cheat,
            complete: Values("0", "1"));

        //
        // games with others
        //
        Group("Playing with others");
        netCommands.Register();     // net_find, net_portcheck, net_status, scoreboard (Commands/NetCommands.cs)

        //
        // cheats (the old Tab debug keys, plus a few new ones)
        //
        Group("Cheats");
        cheatCommands.Register();       // god, NoClip, give, map, exitlevel, tp, killall, diag, summon, pitch, eyeheight, hurt, PlayerClass (Commands/CheatCommands.cs)
        Group("Information and debugging");
        debugCommands.Register();       // overhead, where, count, actors, maps, fps, slowmo, vbls, fade, screenshot (Commands/DebugCommands.cs)
        Group("Factions and colors");
        factionCommands.Register();     // factions, setfaction, settranslation, translations, playercolor (Commands/FactionCommands.cs)
        Group("Changing the level");
        levelEditCommands.Register();       // WallHeight, sky, weather, fog, light(s), zone*, height, tag, patch, flat(s) (Commands/LevelEditCommands.cs)
        Group("Players, views and game modes");
        Register("spectate", "Watches through another actor's eyes while you play on: the next or previous enemy (default next), the next of a class, or back to the player. Not saved; each level starts on the player.",
            "spectate [next|prev|player|<class>]", SpectateCommand, Cheat | InLevel,
            complete: (_, i) => i == 0 ? ["next", "prev", "player", .. SpectateCandidates().Select(a => a.Name).Distinct()] : []);
        playerCommands.Register();      // addplayer, controlplayer, GameMode (Commands/PlayerCommands.cs)

        //
        // scripts, unlocks, demos and saves
        //
        Group("Scripts");
        Register("scripts", "Lists the Lua scripts that loaded (scripts/*.lua), those left out and why, and the actions they give.", "scripts", ScriptsCommand);
        Register("lua", "Runs Lua in the scripts' state (`me` is the player). Strings take 'single quotes'; separate statements with spaces, not ;.",
            "lua <code>", LuaCommand, Cheat);
        Group("Unlocks and records");
        new Commands.UnlockCommands(_consoleManager, Unlocks, LevelRecords).Register();     // unlock, lock, records (Commands/UnlockCommands.cs)
        Group("Demos");
        demoCommands.Register();        // playdemo, demotest, recorddemo (Commands/DemoCommands.cs)
        Group("Saved games");
        saveCommands.Register();        // saves, save, load (Commands/SaveCommands.cs)
        Group("");      // anything registered later, elsewhere, isn't in one
    }

    /// <summary>
    /// Every console command, once each (aliases with it), by name: for the documentation
    /// (PFWolf.DocGen) and the tests. Off the game, it builds the managers first; nothing is run.
    /// </summary>
    internal static IReadOnlyList<ConsoleCommand> ConsoleCommandList()
    {
        if (_consoleManager is null)
            InitServices();
        RegisterConsoleCommands();
        return [.. _consoleManager.Commands];
    }

    /// <summary>The console commands' groups, in the order they register (ConsoleCommandList's first)</summary>
    internal static IReadOnlyList<string> ConsoleCommandGroups()
    {
        ConsoleCommandList();
        return [.. _consoleManager.Groups.Where(group => group.Length > 0)];
    }

    // The cheats (Commands/CheatCommands.cs)
    static CheatCommands cheatCommands = null!;     // made in InitServices

    static void GiveAllKeys() => cheatCommands.GiveAllKeys();

    // What commands act on, and how they describe it (Commands/ConsoleTargets.cs)
    static ConsoleTargets consoleTargets = null!;       // made in InitServices

    static void Register(string name, string help, string usage, Action<string[]> run,
        ConsoleCommandFlags flags = ConsoleCommandFlags.None, string[]? aliases = null, ConsoleCompleter? complete = null) =>
        _consoleManager.Register(name, help, usage, run, flags, aliases, complete);

    /// <summary>The play screen round the view, drawn again after a video mode change blanks it</summary>
    static void RedrawAfterVideoChange()
    {
        if (MapManager.Player != null && ViewSize != 21)
            DrawPlayScreen();
    }
}
