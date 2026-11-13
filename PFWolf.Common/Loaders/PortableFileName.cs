namespace PFWolf.Loaders;

/// <summary>
/// File names that work on every system: Windows refuses more characters than Linux and macOS
/// do (where Path.GetInvalidFileNameChars is only '/' and NUL), and a mod, demo or folder named
/// on one should still unzip and open on the others
/// </summary>
public static class PortableFileName
{
    /// <summary>Windows' list, which takes in the others': control characters and "&lt;&gt;:"/\|?*</summary>
    public static readonly char[] InvalidChars =
        [.. Enumerable.Range(0, 32).Select(c => (char)c), '"', '<', '>', '|', ':', '*', '?', '\\', '/'];

    /// <summary>The name with each character no system takes as <paramref name="replacement"/></summary>
    public static string Clean(string name, string replacement = "_")
        => string.Join(replacement, name.Split(InvalidChars));
}
