namespace PFWolf.Loaders;

/// <summary>
/// The player's PFWolf folder for settings, saves, screenshots and recorded demos (each game in a
/// folder of its own), and the editor's settings: %APPDATA%\PFWolf on Windows,
/// ~/.config/PFWolf on Linux ($XDG_CONFIG_HOME/PFWolf), ~/Library/Application Support/PFWolf on
/// macOS (beside UserDataFolder's files there, as Mac apps keep them)
/// </summary>
public static class UserSettingsFolder
{
    public static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(OperatingSystem.IsMacOS() ? Environment.SpecialFolder.LocalApplicationData : Environment.SpecialFolder.ApplicationData),
        "PFWolf");
}
