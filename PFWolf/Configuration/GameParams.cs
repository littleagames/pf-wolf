namespace PFWolf.Configuration;

using CommandLine;

internal class GameParams
{
    [Option("configdir", Required = false, HelpText = "Directory where the configuration settings are located. Default in %APPDATA%.")]
    public string ConfigDir { get; set; } = "";

    [Option("savedir", Required = false, HelpText = "Directory where the game saves are located. Default in %APPDATA%.")]
    public string SavesDir { get; set; } = "";

    [Option("demodir", Required = false, HelpText = "Directory where recorded demos are kept (and played ahead of the game's own). Default in %APPDATA%.")]
    public string DemoDir { get; set; } = "";

    [Option("nowait", Required = false, HelpText = "Don't wait at the signon screen or show the intro screens; with --exec, for scripted runs.")]
    public bool NoWait { get; set; }

    [Option("exec", Required = false, HelpText = "Console commands to run once the game has started, separated by ';'.")]
    public string Exec { get; set; } = "";

    [Option("game", Required = false, HelpText = "Game pack to run: wolf3d (default), spear, blake or planetstrike.")]
    public string Game { get; set; } = "";

    [Option("cheats", Required = false, HelpText = "Turn on the cheat commands and debug keys from the start.")]
    public bool Cheats { get; set; }

    [Option("host", Required = false, HelpText = "Host a game with others: straight to its lobby (on --port, default 10645).")]
    public bool Host { get; set; }

    [Option("join", Required = false, HelpText = "Join the game hosted at this address (address or address:port): straight to its lobby.")]
    public string Join { get; set; } = "";

    [Option("port", Required = false, HelpText = "The port --host hosts on.")]
    public int? Port { get; set; }

    [Option("name", Required = false, HelpText = "Your name in games with others (saved for next time).")]
    public string Name { get; set; } = "";

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

    [Option("res", Required = false, HelpText = "Window size, e.g. 960x600.")]
    public string Resolution { get; set; } = "";

    [Option("scale", Required = false, HelpText = "Draw at 320x200 times this (1-8): sharper, not bigger.")]
    public int? Scale { get; set; }
}
