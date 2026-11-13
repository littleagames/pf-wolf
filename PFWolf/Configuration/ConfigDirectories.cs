
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
    /// The running game's folder in the player's PFWolf settings folder ("Wolfenstein3D"; see
    /// Loaders.UserSettingsFolder for where that is on each system)
    /// </param>
    internal static ConfigDirectories Default(string defaultModDirectory)
    {
        string gameDirectory = Path.Combine(Loaders.UserSettingsFolder.Path, defaultModDirectory);
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
