using PFWolf.Loaders;

namespace PFWolf.Configuration;

/// <summary>
/// mods.cfg in the game's config folder: the mods switched on in the Mods menu, one a line, in
/// load order. A name is a pk3, zip or folder in the mods folder; anything else is a full path.
/// Blank lines and lines starting with # are skipped.
/// </summary>
internal static class ModsConfig
{
    public const string FileName = "mods.cfg";

    public static List<string> Read(string path)
    {
        if (!File.Exists(path))
            return [];

        try
        {
            return File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't read {path} ({e.Message}); no mods are switched on.");
            return [];
        }
    }

    /// <returns>Whether it was written</returns>
    public static bool Write(string path, IEnumerable<string> mods)
    {
        try
        {
            File.WriteAllLines(path,
            [
                "# Mods loaded over pfwolf.pk3, in this order (later ones win). Set in the Options menu's",
                "# Mods screen; a name is in the mods folder beside the game (or in your PFWolf data folder's),",
                "# anything else is a full path.",
                .. mods,
            ]);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't write {path}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// How a mod is written in mods.cfg: its name when it's directly in one of the mods folders
    /// (so the game folder can be moved) and the name finds it; its full path otherwise (one in
    /// the player's mods folder that a mod of the same name beside the game hides)
    /// </summary>
    public static string ConfigName(string fullPath)
    {
        static string Trimmed(string path) => Path.TrimEndingDirectorySeparator(path);

        var folder = Path.GetDirectoryName(Trimmed(fullPath));
        if (!ModSource.ModsFolders.Any(modsFolder => string.Equals(folder, Trimmed(modsFolder), StringComparison.OrdinalIgnoreCase)))
            return fullPath;

        var name = Path.GetFileName(Trimmed(fullPath));
        return ModSource.ResolvePath(name) is not { } found || string.Equals(Trimmed(found), Trimmed(Path.GetFullPath(fullPath)), StringComparison.OrdinalIgnoreCase)
            ? name
            : fullPath;
    }
}
