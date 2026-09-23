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
}
