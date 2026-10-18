using PFWolf.Assets;
using PFWolf.Assets.Demos;

namespace PFWolf.Editor.Data;

/// <summary>Where a demo the browser lists comes from</summary>
public enum DemoSource { Game, Recorded, File }

/// <summary>A demo the browser lists: the game's own, one recorded with the game, or a .dmo opened</summary>
/// <param name="Number">0-9 for the game's and recorded demos, which the game plays by number</param>
/// <param name="FilePath">The file it was read from; null for the game's own</param>
public sealed record DemoEntry(string Name, DemoSource Source, DemoFile Demo, int? Number, string? FilePath)
{
    public string SourceText => Source switch
    {
        DemoSource.Game => "game data",
        DemoSource.Recorded => "recorded",
        _ => "file",
    };
}

/// <summary>The demos of a loaded game, as the game finds them (Gameplay/Demos.cs)</summary>
public static class DemoCatalog
{
    public const int MaxDemos = 10;

    /// <summary>
    /// The game's DEMO0-9, then those recorded in <paramref name="demosFolder"/> (DEMO0-9.dmo),
    /// which the game plays in place of its own of the same number
    /// </summary>
    public static List<DemoEntry> Build(GameContent content, string? demosFolder)
    {
        var entries = new List<DemoEntry>();
        for (int number = 0; number < MaxDemos; number++)
        {
            if (content.Find<DemoAsset>($"demo{number}") is { } asset)
                entries.Add(new DemoEntry($"DEMO{number}", DemoSource.Game, DemoFile.Parse(asset.RawData), number, null));
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

    /// <summary>Whether a recorded demo plays in place of this one of the game's</summary>
    public static bool IsReplaced(DemoEntry entry, IEnumerable<DemoEntry> all)
        => entry.Source == DemoSource.Game && all.Any(other => other.Source == DemoSource.Recorded && other.Number == entry.Number);
}
