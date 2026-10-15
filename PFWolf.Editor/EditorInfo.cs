using System.Reflection;

namespace PFWolf.Editor;

/// <summary>
/// Who made the editor and which build it is, for the splash screen: read from the assembly's
/// attributes, so PFWolf.Editor.csproj stays the one place they're written
/// </summary>
public static class EditorInfo
{
    private static readonly Assembly Assembly = typeof(EditorInfo).Assembly;

    public const string Name = "PFWolf Editor";

    /// <summary>"0.6.0", with the commit's first 7 characters after a "+" when the build knows it</summary>
    public static string Version { get; } = ReadVersion();

    public static string Authors { get; } = ReadMetadata("Authors") ?? "";

    public static string Copyright { get; } = Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

    public static string Website { get; } = ReadMetadata("Website") ?? "";

    /// <summary>Resources/credits.txt, one credit a line</summary>
    public static IReadOnlyList<string> Credits { get; } = ReadCredits();

    /// <summary>What the editor runs on: .NET and Avalonia</summary>
    public static string Runtime { get; } =
        $".NET {Environment.Version} · Avalonia {typeof(Avalonia.Application).Assembly.GetName().Version?.ToString(3)}";

    private static string ReadVersion()
    {
        var version = Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? Assembly.GetName().Version?.ToString(3) ?? "";
        var plus = version.IndexOf('+');
        return plus >= 0 && version.Length > plus + 8 ? version[..(plus + 8)] : version;
    }

    private static string? ReadMetadata(string key)
        => Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(metadata => metadata.Key == key)?.Value;

    private static List<string> ReadCredits()
    {
        using var stream = Assembly.GetManifestResourceStream("credits.txt");
        if (stream == null)
            return [];
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
    }
}
