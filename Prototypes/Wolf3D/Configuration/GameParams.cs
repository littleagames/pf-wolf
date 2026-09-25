namespace Wolf3D.Configuration;

using CommandLine;

internal class GameParams
{
    [Option("configdir", Required = false, HelpText = "Directory where the configuration settings are located. Default in %APPDATA%.")]
    public string ConfigDir { get; set; } = "";

    [Option("savedir", Required = false, HelpText = "Directory where the game saves are located. Default in %APPDATA%.")]
    public string SavesDir { get; set; } = "";

    [Option("exec", Required = false, HelpText = "Console commands to run once the game has started, separated by ';'.")]
    public string Exec { get; set; } = "";

    [Option("game", Required = false, HelpText = "Game pack to run: wolf3d (default) or spear.")]
    public string Game { get; set; } = "";

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
