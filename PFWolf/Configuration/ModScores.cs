using PFWolf.Gameplay;
using PFWolf.Loaders;

namespace PFWolf.Configuration;

/// <summary>
/// modscores.dat in the game's config folder: a high score table for each set of mods played
/// with, so a mod's scores don't land in the game's own (config.cfg's, which only playing
/// without mods uses). A set is keyed by its mods' ids, sorted, so the load order doesn't
/// matter and a new version of a mod keeps its table.
/// </summary>
internal static class ModScores
{
    public const string FileName = "modscores.dat";

    private const uint Signature = 0x53484650;     // "PFHS"
    private const byte Version = 1;

    /// <summary>
    /// The key the loaded mods' table goes by: their ids, lower case and sorted, joined by "+";
    /// empty with no mods. The mod a standalone game comes from is the game, not a mod on it.
    /// </summary>
    public static string KeyFor(IEnumerable<ModSource> mods, string? gameModPath)
    {
        string? gameMod = gameModPath == null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameModPath));
        return string.Join("+", mods
            .Where(mod => gameMod == null
                || !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(mod.FullPath)), gameMod, StringComparison.OrdinalIgnoreCase))
            .Select(mod => mod.Id.ToLowerInvariant())
            .Distinct()
            .Order(StringComparer.Ordinal));
    }

    /// <summary>Every table in the file by its key; none when it's missing or unreadable</summary>
    public static Dictionary<string, HighScore[]> Read(string path)
    {
        var tables = new Dictionary<string, HighScore[]>(StringComparer.Ordinal);
        if (!File.Exists(path))
            return tables;

        try
        {
            using var br = new BinaryReader(File.OpenRead(path));
            if (br.ReadUInt32() != Signature || br.ReadByte() != Version)
            {
                Console.WriteLine($"{path} isn't a mod scores file; the mods' high scores start over.");
                return tables;
            }

            int tableCount = br.ReadUInt16();
            for (int t = 0; t < tableCount; t++)
            {
                var key = br.ReadString();
                var scores = new HighScore[br.ReadByte()];
                for (int i = 0; i < scores.Length; i++)
                {
                    scores[i] = new HighScore();
                    scores[i].Read(br);
                    scores[i].Ratio = br.ReadUInt16();
                }
                tables[key] = scores;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left in place, so the scores in it can still be recovered by hand, until a write replaces it
            Console.WriteLine($"Couldn't read {path} ({e.Message}); the mods' high scores start over.");
            tables.Clear();
        }
        return tables;
    }

    public static void Write(Stream stream, IReadOnlyDictionary<string, HighScore[]> tables)
    {
        using var bw = new BinaryWriter(stream);
        bw.Write(Signature);
        bw.Write(Version);
        bw.Write((ushort)tables.Count);
        foreach (var (key, scores) in tables.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            bw.Write(key);
            bw.Write((byte)scores.Length);
            foreach (var s in scores)
            {
                s.Write(bw);
                bw.Write(s.Ratio);
            }
        }
    }
}
