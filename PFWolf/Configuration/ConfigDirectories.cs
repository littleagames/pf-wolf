
namespace PFWolf.Configuration;

internal record ConfigDirectories
{
    public string ConfigDirectory { get; set; } = null!;
    public string SaveGameDirectory { get; set; } = null!;
    public string ScreenshotsDirectory { get; set; } = null!;
    public string ErrorLogsDirectory { get; set; } = null!;

    /// <summary>Where recorded demos (DEMO0.dmo...) are written, and played from ahead of the game's own</summary>
    public string DemosDirectory { get; set; } = null!;

    /// <summary>Where the exportmap command writes levels</summary>
    public string ExportsDirectory { get; set; } = null!;

    /// <param name="defaultModDirectory">
    /// The running game's folder under PFWolf in the user's settings folder ("Wolfenstein3D"):
    /// %APPDATA% on Windows, ~/.config (or $XDG_CONFIG_HOME) elsewhere
    /// </param>
    internal static ConfigDirectories Default(string defaultModDirectory)
    {
        string gameDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PFWolf", defaultModDirectory);
        return new ConfigDirectories
        {
            ConfigDirectory = Path.Combine(gameDirectory, "configs"),
            SaveGameDirectory = Path.Combine(gameDirectory, "savegames"),
            ScreenshotsDirectory = Path.Combine(gameDirectory, "screenshots"),
            ErrorLogsDirectory = Path.Combine(gameDirectory, "logs"),
            DemosDirectory = Path.Combine(gameDirectory, "demos"),
            ExportsDirectory = Path.Combine(gameDirectory, "exports")
        };
    }
}
