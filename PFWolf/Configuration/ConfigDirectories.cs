
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

    /// <param name="defaultModDirectory">The running game's folder under %APPDATA%\PFWolf ("Wolfenstein3D")</param>
    internal static ConfigDirectories Default(string defaultModDirectory)
    {
        string defaultBaseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        const string gameDirectory = "PFWolf";
        return new ConfigDirectories
        {
            ConfigDirectory = $"{defaultBaseDirectory}\\{gameDirectory}\\{defaultModDirectory}\\configs",
            SaveGameDirectory = $"{defaultBaseDirectory}\\{gameDirectory}\\{defaultModDirectory}\\savegames",
            ScreenshotsDirectory = $"{defaultBaseDirectory}\\{gameDirectory}\\{defaultModDirectory}\\screenshots",
            ErrorLogsDirectory = $"{defaultBaseDirectory}\\{gameDirectory}\\{defaultModDirectory}\\logs",
            DemosDirectory = $"{defaultBaseDirectory}\\{gameDirectory}\\{defaultModDirectory}\\demos",
            ExportsDirectory = $"{defaultBaseDirectory}\\{gameDirectory}\\{defaultModDirectory}\\exports"
        };
    }
}
