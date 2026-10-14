using PFWolf.Assets;

namespace PFWolf.Gameplay;

internal class HighScore
{
    internal const int MaxHighName = 57;

    /// <summary>How many high scores config.cfg's original block holds; a longer table is appended after the settings</summary>
    internal const int LegacyScoreCount = 7;

    public string name;
    public int score;
    public ushort completed, episode;

    /// <summary>The last floor's rating, 0 to 100 (Blake Stone's mission ratio column)</summary>
    public ushort ratio;

    public HighScore()
    {
        name = "";
    }

    /// <summary>Wolf3D's own high scores</summary>
    internal static HighScore[] WolfDefaults() =>
    [
        new HighScore {name = "id software-'92", score = 10000,completed = 1},
        new HighScore {name = "Adrian Carmack", score = 10000,completed = 1},
        new HighScore {name = "John Carmack", score = 10000,completed = 1},
        new HighScore {name = "Kevin Cloud", score = 10000,completed = 1},
        new HighScore {name = "Tom Hall", score = 10000,completed = 1},
        new HighScore {name = "John Romero", score = 10000,completed = 1},
        new HighScore {name = "Jay Wilbur", score = 10000,completed = 1},
    ];

    /// <summary>
    /// The game pack's own default high scores (game-info high-scores defaults and count) in
    /// place of <paramref name="scores"/> (Wolf3D's), before the config's are read over them;
    /// <paramref name="scores"/> itself when the game pack doesn't change them
    /// </summary>
    internal static HighScore[] TableFor(HighScoresInfo layout, HighScore[] scores)
    {
        int count = Math.Clamp(layout.Count ?? (layout.Defaults.Count > 0 ? layout.Defaults.Count : LegacyScoreCount), 1, 100);
        if (layout.Defaults.Count == 0 && count == scores.Length)
            return scores;

        var table = new HighScore[count];
        for (int i = 0; i < count; i++)
        {
            var d = i < layout.Defaults.Count ? layout.Defaults[i] : null;
            table[i] = d == null
                ? (layout.Defaults.Count == 0 && i < scores.Length ? scores[i] : new HighScore { score = 10000, completed = 1 })
                : new HighScore { name = d.Name, score = d.Score, completed = (ushort)d.Level };
        }
        return table;
    }

    // The name is a fixed, zero-padded run of Latin-1 bytes. (Written as chars it was UTF-8,
    // where an accented letter takes two bytes and shifts everything after it.)
    internal void Read(BinaryReader br)
    {
        var nameBytes = br.ReadBytes(MaxHighName + 1);
        if (nameBytes.Length != MaxHighName + 1)
            throw new EndOfStreamException();
        name = System.Text.Encoding.Latin1.GetString(nameBytes).TrimEnd('\0');
        score = br.ReadInt32();
        completed = br.ReadUInt16();
        episode = br.ReadUInt16();
    }

    internal void Write(BinaryWriter bw)
    {
        var nameBytes = new byte[MaxHighName + 1];
        System.Text.Encoding.Latin1.GetBytes(name, 0, Math.Min(name.Length, MaxHighName), nameBytes, 0);
        bw.Write(nameBytes);
        bw.Write(score);
        bw.Write(completed);
        bw.Write(episode);
    }
}
