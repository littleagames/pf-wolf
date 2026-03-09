
namespace Wolf3D.Configuration;

internal record ConfigDirectories
{
    public string ConfigDirectory { get; set; } = null!;
    public string SaveGameDirectory { get; set; } = null!;
    public string ScreenshotsDirectory { get; set; } = null!;
    public string ErrorLogsDirectory { get; set; } = null!;


    internal static ConfigDirectories Default()
    {
        string defaultBaseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        const string gameDirectory = "PFWolf";
        const string defaultModDirectory = "Wolfenstein3D"; // TODO: Replace with "gamepack name" in the future
        return new ConfigDirectories
        {
            ConfigDirectory = $"{defaultBaseDirectory}\\{gameDirectory}\\{defaultModDirectory}\\configs",
            SaveGameDirectory = $"{defaultBaseDirectory}\\{gameDirectory}\\{defaultModDirectory}\\savegames",
            ScreenshotsDirectory = $"{defaultBaseDirectory}\\{gameDirectory}\\{defaultModDirectory}\\screenshots",
            ErrorLogsDirectory = $"{defaultBaseDirectory}\\{gameDirectory}\\{defaultModDirectory}\\logs"
        };
    }
}
