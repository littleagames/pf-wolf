namespace PFWolf.Configuration;

using CommandLine;

internal class GameParams
{
    [Option("configdir", Required = false, HelpText = "Directory where the configuration settings are located (config.cfg, binds.cfg, autoexec.cfg). Default: %APPDATA%\\PFWolf\\<game>\\configs.")]
    public string ConfigDir { get; set; } = "";

    [Option("savedir", Required = false, HelpText = "Directory where the game saves are located. Default: %APPDATA%\\PFWolf\\<game>\\savegames.")]
    public string SavesDir { get; set; } = "";

    [Option("demodir", Required = false, HelpText = "Directory where recorded demos are kept (and played ahead of the game's own). Default: %APPDATA%\\PFWolf\\<game>\\demos.")]
    public string DemoDir { get; set; } = "";

    [Option("nowait", Required = false, HelpText = "Don't wait at the signon screen or show the intro screens or level briefings; with --exec, for scripted runs.")]
    public bool NoWait { get; set; }

    /// <summary>Read before parsing, by Startup.ConsoleWindow: here so the parser accepts it</summary>
    [Option("console", Required = false, HelpText = "Open a console window beside the game for its output.")]
    public bool Console { get; set; }

    [Option("exec", Required = false, HelpText = "Console commands to run once the game has started, separated by ';'.")]
    public string Exec { get; set; } = "";

    // GameTypes.KnownGamePackIds, which CommandLineOptionsTests holds this to
    [Option("game", Required = false, HelpText = "Game pack to run: wolf3d (default), wolf3d-apogee, wolf3d-shareware, spear, spear-demo, blake, blake-shareware, planetstrike, "
        + "or a game a mod on the command line adds. Without that game's data files here, the one whose files are.")]
    public string Game { get; set; } = "";

    [Option("cheats", Required = false, HelpText = "Turn on the cheat commands and debug keys from the start.")]
    public bool Cheats { get; set; }

    [Option("warp", Required = false, HelpText = "Start a new game on this level (one game-info lists, e.g. MAP01), straight from the command line.")]
    public string Warp { get; set; } = "";

    [Option("skill", Required = false, HelpText = "With --warp: the skill, 1 (the easiest) and up. Default: the third (Wolf3D's Bring 'em on).")]
    public int? Skill { get; set; }

    [Option("start", Required = false, HelpText = "With --warp: start on this tile instead of the level's player start, as x,y or x,y,angle (0 east, 90 north).")]
    public string Start { get; set; } = "";

    [Option("host", Required = false, HelpText = "Host a game with others: straight to its lobby (on --port, default 10645).")]
    public bool Host { get; set; }

    [Option("join", Required = false, HelpText = "Join the game hosted at this address (address or address:port): straight to its lobby.")]
    public string Join { get; set; } = "";

    [Option("port", Required = false, HelpText = "The port --host hosts on.")]
    public int? Port { get; set; }

    [Option("name", Required = false, HelpText = "Your name in games with others (saved for next time).")]
    public string Name { get; set; } = "";

    [Option("make-mod", Required = false, HelpText = "Make a raw data mod from this folder of WDC/ChaosEdit-edited data files (VSWAP, VGAGRAPH, AUDIOT, GAMEMAPS...): "
        + "writes its gamepack-info.yaml and raw-data-map.yaml (new chunks marked to name) and a report, then quits. See --base, --out, --title.")]
    public string MakeMod { get; set; } = "";

    [Option("base", Required = false, HelpText = "With --make-mod: the game the files are built on (wolf3d, wolf3d-apogee, spear, blake...). Default: the one they match best.")]
    public string Base { get; set; } = "";

    [Option("out", Required = false, HelpText = "With --make-mod: the mod folder to write (the data files are copied in). Default: mods\\<the data folder's name>.")]
    public string Out { get; set; } = "";

    [Option("title", Required = false, HelpText = "With --make-mod: the game's title. Default: the data folder's name.")]
    public string Title { get; set; } = "";

    [Option("data-file", Required = false, HelpText = "With --make-mod: which file is one of the data files, when it isn't named as the game names them, "
        + "as KIND=FILE (VSWAP=FOXTROT1.WL6; kinds VSWAP, VGADICT, VGAHEAD, VGAGRAPH, AUDIOHED, AUDIOT, MAPHEAD, GAMEMAPS). Default: by name, else by what's in them.")]
    public IEnumerable<string> DataFiles { get; set; } = [];

    [Option("palette", Required = false, HelpText = "With --make-mod: the mod's own palette (what its exe had), put in as the game palette: "
        + "a raw 768-byte palette (6-bit VGA or 0-255), .act, JASC-PAL or GIMP .gpl. Default: the base game's, or the one palette file in the folder.")]
    public string Palette { get; set; } = "";

    [Option("overwrite", Required = false, HelpText = "With --make-mod: write over the mod's files when it's made already (running it again after editing the data files).")]
    public bool Overwrite { get; set; }

    [Option("file", Required = false, HelpText = "Mods to load over pfwolf.pk3: pk3 or zip files, or folders, found as given or in the mods folder. Later ones win.")]
    public IEnumerable<string> Files { get; set; } = [];

    /// <summary>
    /// Mods named without --file, which is how files dropped on the exe arrive
    /// </summary>
    [Value(0, Required = false, MetaName = "mods", HelpText = "More mods, as for --file.")]
    public IEnumerable<string> Paths { get; set; } = [];

    // Video: each overrides the saved setting, and is saved in its place

    [Option("fullscreen", Required = false, HelpText = "Start in borderless fullscreen.")]
    public bool Fullscreen { get; set; }

    [Option("windowed", Required = false, HelpText = "Start in a window (wins over --fullscreen).")]
    public bool Windowed { get; set; }

    [Option("res", Required = false, HelpText = "Window size, e.g. 960x600 (at least 320x200).")]
    public string Resolution { get; set; } = "";

    [Option("scale", Required = false, HelpText = "Draw at 320x200 times this (1-8): sharper, not bigger.")]
    public int? Scale { get; set; }
}
