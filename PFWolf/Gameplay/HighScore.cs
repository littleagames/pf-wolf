using PFWolf.Assets;

namespace PFWolf.Gameplay;

internal class HighScore
{
    internal const int MaxHighName = 57;

    /// <summary>How many high scores config.cfg's original block holds; a longer table is appended after the settings</summary>
    internal const int LegacyScoreCount = 7;

    /// <summary>
    /// Set in a high score's completed level when the game was won, so it ranks above a score
    /// that ended part way and can show the game-info high-scores won-pic
    /// </summary>
    internal const ushort Won = 0x8000;

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

    /// <summary>
    /// Puts <paramref name="myscore"/> in its place in the table, the ones below it moving down
    /// (the last falling off): its row, or -1 when it didn't make the table. A tie goes to the
    /// one that got further.
    /// </summary>
    internal static int Insert(HighScore[] scores, HighScore myscore)
    {
        ushort i, j;
        int n;
        for (i = 0, n = -1; i < scores.Length; i++)
        {
            if ((myscore.score > scores[i].score)
                || ((myscore.score == scores[i].score) && (myscore.completed > scores[i].completed)))
            {
                for (j = (ushort)scores.Length; --j > i;)
                    scores[j] = scores[j - 1];
                scores[i] = myscore;
                n = i;
                break;
            }
        }
        return n;
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
