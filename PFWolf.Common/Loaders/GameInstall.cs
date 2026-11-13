namespace PFWolf.Loaders;

/// <summary>
/// Where the game's own files are (pfwolf.pk3, mods/, the docs XML): beside the exe, or, in a
/// macOS app (packaging/macos), in its Contents/Resources. Only programs belong in
/// Contents/MacOS, and nothing inside an app should be written to: that breaks its signature.
/// </summary>
public static class GameInstall
{
    /// <summary>Whether the program is running from inside a macOS .app</summary>
    public static bool IsAppBundle => AppBundleResources(AppContext.BaseDirectory) != null;

    /// <summary>The folder with the game's own files: Contents/Resources in an app, else the exe's</summary>
    public static string Folder => AppBundleResources(AppContext.BaseDirectory) ?? AppContext.BaseDirectory;

    /// <summary>Whether a folder is inside a macOS app (anything under a folder named X.app)</summary>
    public static bool IsInsideApp(string folder)
    {
        for (var dir = new DirectoryInfo(Path.GetFullPath(folder)); dir != null; dir = dir.Parent)
        {
            if (dir.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>A game folder as a player picks it: a macOS app means its Contents/Resources</summary>
    public static string ContentFolder(string folder)
    {
        var resources = Path.Combine(folder, "Contents", "Resources");
        return Path.TrimEndingDirectorySeparator(folder).EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(resources)
            ? resources
            : folder;
    }

    /// <summary>
    /// The game's program for a game folder: PFWolf.exe on Windows; elsewhere PFWolf, in the
    /// app's Contents/MacOS when the folder is (or is the Contents/Resources of) PFWolf.app
    /// </summary>
    public static string ExeIn(string gameFolder)
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(gameFolder, "PFWolf.exe");

        var folder = ContentFolder(gameFolder);
        var inApp = Path.GetFullPath(Path.Combine(folder, "..", "MacOS", "PFWolf"));
        return AppBundleResources(Path.GetDirectoryName(inApp)!) != null && File.Exists(inApp)
            ? inApp
            : Path.Combine(folder, "PFWolf");
    }

    /// <summary>".../X.app/Contents/Resources" for an exe folder ".../X.app/Contents/MacOS"; else null</summary>
    internal static string? AppBundleResources(string exeFolder)
    {
        var macOS = new DirectoryInfo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(exeFolder)));
        if (macOS.Name != "MacOS" || macOS.Parent is not { Name: "Contents" } contents
            || contents.Parent?.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase) != true)
            return null;
        return Path.Combine(contents.FullName, "Resources");
    }
}
