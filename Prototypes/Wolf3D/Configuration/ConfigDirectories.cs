
namespace Wolf3D.Configuration;

internal record ConfigDirectories
{
    public string ConfigDirectory { get; set; } = null!;
    public string SaveGameDirectory { get; set; } = null!;
    public string ScreenshotsDirectory { get; set; } = null!;
    public string ErrorLogsDirectory { get; set; } = null!;


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
            ErrorLogsDirectory = $"{defaultBaseDirectory}\\{gameDirectory}\\{defaultModDirectory}\\logs"
        };
    }
}
