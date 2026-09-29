using Wolf3D.Loaders;

namespace Wolf3D.Configuration;

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
                "# Mods screen; a name is in the mods folder beside the game, anything else is a full path.",
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
    /// How a mod is written in mods.cfg: its name when it's directly in the mods folder, so the
    /// game folder can be moved; its full path otherwise
    /// </summary>
    public static string ConfigName(string fullPath)
    {
        var folder = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(fullPath));
        return string.Equals(folder, Path.TrimEndingDirectorySeparator(ModSource.ModsFolder), StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(Path.TrimEndingDirectorySeparator(fullPath))
            : fullPath;
    }
}
