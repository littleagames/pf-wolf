namespace Wolf3D.Assets;

/// <summary>
/// A game pack's level-end screen (gamepacks/{pack}/intermission.yaml): its layout and sounds.
/// A pack that builds on a base-pack starts from its settings; the pack's own replace them one
/// by one.
/// </summary>
internal record IntermissionAsset : Asset
{
    /// <summary>The music while the screen is up; none keeps what was playing</summary>
    public string? Music { get; set; }

    /// <summary>The text's font (fonts.yaml)</summary>
    public string? Font { get; set; }

    /// <summary>The text's color: a color name, #RRGGBB or palette index</summary>
    public string? Color { get; set; }

    /// <summary>BJ, breathing</summary>
    public IntermissionBj? Bj { get; set; }

    /// <summary>The text drawn as the screen opens</summary>
    public List<IntermissionLabel>? Labels { get; set; }

    /// <summary>Where each number goes, by name: floor, time, par, bonus, kill, secret, treasure</summary>
    public Dictionary<string, IntermissionValue> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public IntermissionSounds Sounds { get; set; } = new();

    public IntermissionScoring Scoring { get; set; } = new();

    /// <summary>The screen shown when a cluster is won, in the same font and color</summary>
    public VictoryScreen? Victory { get; set; }

    public override void Merge(Asset other)
    {
        if (other is IntermissionAsset otherAsset)
        {
            Victory = otherAsset.Victory ?? Victory;
            Music = otherAsset.Music ?? Music;
            Font = otherAsset.Font ?? Font;
            Color = otherAsset.Color ?? Color;
            Bj = otherAsset.Bj ?? Bj;
            Labels = otherAsset.Labels ?? Labels;
            foreach (var (name, value) in otherAsset.Values)
                Values[name] = value;
            Sounds = Sounds.MergedWith(otherAsset.Sounds);
            Scoring = Scoring.MergedWith(otherAsset.Scoring);
        }
    }
}

/// <summary>The level-end bonus. Any left out is 0.</summary>
internal record IntermissionScoring
{
    /// <summary>Points for each second the level was finished under par</summary>
    public int? TimeBonus { get; set; }

    /// <summary>Points for each ratio (kill, secret, treasure) at 100%</summary>
    public int? PerfectBonus { get; set; }

    public IntermissionScoring MergedWith(IntermissionScoring other) => new()
    {
        TimeBonus = other.TimeBonus ?? TimeBonus,
        PerfectBonus = other.PerfectBonus ?? PerfectBonus,
    };
}

/// <summary>BJ on the level-end screen: drawn at (x, y), turning to the next pic every breath-tics</summary>
internal record IntermissionBj
{
    public int X { get; set; }
    public int Y { get; set; }
    public List<string> Pics { get; set; } = [];

    /// <summary>Tics (70 a second) between one pic and the next</summary>
    public int BreathTics { get; set; } = 35;
}

/// <summary>
/// The victory screen: its pictures and text, and where the won cluster's total time and
/// average ratios go (values time, kill, secret, treasure)
/// </summary>
internal record VictoryScreen
{
    /// <summary>The music while the screen is up; none keeps what was playing</summary>
    public string? Music { get; set; }

    public List<IntermissionPic> Pics { get; set; } = [];
    public List<IntermissionLabel> Labels { get; set; } = [];
    public Dictionary<string, IntermissionValue> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A picture at (x, y)</summary>
internal record IntermissionPic
{
    public string Pic { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>Text on the level-end screen: a $NAME language key or the text, at (x, y)</summary>
internal record IntermissionLabel
{
    public string Text { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
}

/// <summary>
/// Where a number goes on the level-end screen: starting at x, or with its right edge at right
/// (it counts up in place). y is its top.
/// </summary>
internal record IntermissionValue
{
    public int X { get; set; }
    public int? Right { get; set; }
    public int Y { get; set; }
}

/// <summary>The sounds of the ratio and time bonus tally. Any left out are silent.</summary>
internal record IntermissionSounds
{
    /// <summary>Every 10% as a ratio counts up, and as the time bonus counts up</summary>
    public string? Tally { get; set; }

    /// <summary>The time bonus, or a ratio between 1% and 99%, finished counting</summary>
    public string? TallyDone { get; set; }

    /// <summary>A ratio reached 100% (and its bonus is added)</summary>
    public string? Perfect { get; set; }

    /// <summary>A ratio finished at 0%</summary>
    public string? None { get; set; }

    public IntermissionSounds MergedWith(IntermissionSounds other) => new()
    {
        Tally = other.Tally ?? Tally,
        TallyDone = other.TallyDone ?? TallyDone,
        Perfect = other.Perfect ?? Perfect,
        None = other.None ?? None,
    };
}
