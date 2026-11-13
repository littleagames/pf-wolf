namespace PFWolf.Loaders;

/// <summary>
/// The player's own folder for what otherwise goes beside the exe, for when that folder can't be
/// written to or isn't theirs (an install in /opt, an AppImage, a macOS app): the game's data
/// files, mods and crash logs. Looked in after the game folder, so a game unzipped somewhere
/// writable works as it always has. %LOCALAPPDATA%\PFWolf on Windows, ~/.local/share/PFWolf on
/// Linux ($XDG_DATA_HOME/PFWolf), ~/Library/Application Support/PFWolf on macOS.
/// </summary>
public static class UserDataFolder
{
    /// <summary>A folder in its place, for tests: so a player's own files there aren't picked up</summary>
    internal static string? Override { get; set; }

    public static string Path => Override
        ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PFWolf");

    /// <summary>Its mods/ folder, looked in after the game folder's</summary>
    public static string ModsFolder => System.IO.Path.Combine(Path, "mods");

    /// <summary>
    /// A file the game looks for in its own folder (the working folder): there, else in this
    /// folder, either way whatever the case of its name; the name as given when it's in neither
    /// </summary>
    public static string FindBesideGame(string file)
    {
        var inGameFolder = CaseInsensitiveFile.Find(file);
        if (File.Exists(inGameFolder))
            return inGameFolder;
        var inUserFolder = CaseInsensitiveFile.Find(System.IO.Path.Combine(Path, file));
        return File.Exists(inUserFolder) ? inUserFolder : inGameFolder;
    }
}
