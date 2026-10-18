using PFWolf.Assets;
using PFWolf.Assets.Demos;

namespace PFWolf.Editor.Data;

/// <summary>Where a demo the browser lists comes from</summary>
public enum DemoSource { Game, Mod, Recorded, File }

/// <summary>A demo the browser lists: the game's own, a mod's, one recorded with the game, or a .dmo opened</summary>
/// <param name="Number">0-9 for the demos the game plays by number (the title loop's)</param>
/// <param name="FilePath">The file it was read from; null for the game's and mods' own</param>
/// <param name="Mod">The mod file it's in (demos/NAME.dmo), for a mod's</param>
public sealed record DemoEntry(string Name, DemoSource Source, DemoFile Demo, int? Number, string? FilePath, ModEntry? Mod = null)
{
    public string SourceText => Source switch
    {
        DemoSource.Game => "game data",
        DemoSource.Mod => Mod?.ModName ?? "mod",
        DemoSource.Recorded => "recorded",
        _ => "file",
    };
}

/// <summary>The demos of a loaded game, as the game finds them (Gameplay/Demos.cs)</summary>
public static class DemoCatalog
{
    public const int MaxDemos = 10;

    /// <summary>
    /// The game's and mods' DEMO0-9 (the title loop's), their other demos by name (a mod's
    /// demos/NAME.dmo, for playdemo), then those recorded in <paramref name="demosFolder"/>
    /// (DEMO0-9.dmo), which the game plays in place of its own of the same number
    /// </summary>
    public static List<DemoEntry> Build(GameContent content, string? demosFolder)
    {
        var entries = new List<DemoEntry>();
        var named = content.Assets.NamesOf<DemoAsset>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var numbered = Enumerable.Range(0, MaxDemos).Select(number => $"demo{number}").Where(named.Contains);
        foreach (var name in numbered.Concat(named.Where(name => !IsNumbered(name)).Order(StringComparer.OrdinalIgnoreCase)))
        {
            if (content.Find<DemoAsset>(name) is not { } asset)
                continue;
            var mod = content.ModFileOf(name, nameof(DemoAsset));
            int? number = IsNumbered(name) ? name[^1] - '0' : null;
            entries.Add(new DemoEntry(number != null ? name.ToUpperInvariant() : name, mod != null ? DemoSource.Mod : DemoSource.Game,
                DemoFile.Parse(asset.RawData), number, null, mod));
        }

        if (!string.IsNullOrEmpty(demosFolder) && Directory.Exists(demosFolder))
        {
            for (int number = 0; number < MaxDemos; number++)
            {
                var path = Path.Combine(demosFolder, $"DEMO{number}.dmo");
                if (Read(path, DemoSource.Recorded, number) is { } recorded)
                    entries.Add(recorded);
            }
        }
        return entries;
    }

    /// <summary>A demo file, or null when it can't be read</summary>
    public static DemoEntry? Read(string path, DemoSource source = DemoSource.File, int? number = null)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            return new DemoEntry(Path.GetFileName(path), source, DemoFile.Parse(File.ReadAllBytes(path)), number, path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Whether a recorded demo plays in place of this one of the game's or a mod's</summary>
    public static bool IsReplaced(DemoEntry entry, IEnumerable<DemoEntry> all)
        => entry.Source is DemoSource.Game or DemoSource.Mod && entry.Number != null
           && all.Any(other => other.Source == DemoSource.Recorded && other.Number == entry.Number);

    // DEMO0-9: the ones the title loop plays
    private static bool IsNumbered(string name)
        => name.Length == 5 && name.StartsWith("demo", StringComparison.OrdinalIgnoreCase) && char.IsAsciiDigit(name[4]);
}
