namespace PFWolf.Loaders;

/// <summary>
/// Finds a file whatever the case of its name, on file systems that care (Linux's, and
/// case-sensitive macOS volumes): gamepack-info names vswap.wl6, while a Steam or GOG copy is
/// VSWAP.WL6. Only the file's own name is matched; its folder must be right as given.
/// </summary>
public static class CaseInsensitiveFile
{
    /// <summary>
    /// The path with the file's name as it is on disk; the path as given when the file is there
    /// as given (always so on Windows) or not there at all
    /// </summary>
    public static string Find(string path)
    {
        if (File.Exists(path))
            return path;

        var folder = Path.GetDirectoryName(path);
        var name = Path.GetFileName(path);
        var searchFolder = string.IsNullOrEmpty(folder) ? "." : folder;
        if (name.Length == 0 || !Directory.Exists(searchFolder))
            return path;

        return Match(name, Directory.EnumerateFiles(searchFolder).Select(Path.GetFileName)!) is { } found
            ? (string.IsNullOrEmpty(folder) ? found : Path.Combine(folder, found))
            : path;
    }

    /// <summary>Which of a folder's file names is <paramref name="name"/> in another case; the first in ordinal order when several are</summary>
    internal static string? Match(string name, IEnumerable<string> fileNames)
        => fileNames.Where(file => file.Equals(name, StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal).FirstOrDefault();
}
