namespace PFWolf.Assets;

/// <summary>
/// A game pack's level-end screen (gamepacks/{pack}/intermission.yaml): its layout and sounds.
/// A pack that builds on a base-pack starts from its settings; the pack's own replace them one
/// by one.
/// </summary>
public record IntermissionAsset : Asset
{
    /// <summary>The music while the screen is up; none keeps what was playing</summary>
    public string? Music { get; set; }

    /// <summary>The text's font (fonts.yaml)</summary>
    public string? Font { get; set; }

    /// <summary>The text's color: a color name, #RRGGBB or palette index</summary>
    public string? Color { get; set; }

    /// <summary>
    /// The numbers' color where it isn't the text's: a color name, #RRGGBB or palette index
    /// (Midnight Assault's dark red figures beside black labels)
    /// </summary>
    public string? ValueColor { get; set; }

    /// <summary>Pictures drawn as the screen opens, under BJ and the text (Midnight Assault's case folder)</summary>
    public List<IntermissionPic>? Pics { get; set; }

    /// <summary>BJ, breathing; a level's game-info intermission-pics in place of his pics</summary>
    public IntermissionBj? Bj { get; set; }

    /// <summary>The text drawn as the screen opens</summary>
    public List<IntermissionLabel>? Labels { get; set; }

    /// <summary>
    /// Where each value goes, by name: floor, time, par, bonus, kill, secret, treasure, and the
    /// level's name and author (game-info's map name and author; no author, nothing drawn).
    /// After the treasure ratio, in the order they're listed, more can be counted up:
    /// objective.NAME (a level objective's done of total, game-info objectives: Midnight
    /// Assault's intel and Kilroys) and stealth (stealth kills of the level's kills, shown only
    /// when it was finished unseen with stealth kills or no kills at all, as Midnight Assault's).
    /// They're percentages, 100 when there was nothing to do, or done/total with count: true (a
    /// count of nothing isn't shown). item.NAME is how many of an inventory item the player holds
    /// of its inventory.maxamount, as done/total.
    /// </summary>
    public Dictionary<string, IntermissionValue> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A color each number's place is cleared to before it's drawn (the screen's own, e.g.
    /// VIEWCOLOR), so a ratio counting up doesn't pile its numbers on each other: what a
    /// see-through font needs. Left out, numbers are drawn over what's there, as Wolf3D's solid
    /// number pictures can be.
    /// </summary>
    public string? ValueBackground { get; set; }

    /// <summary>
    /// One of pics redrawn (from its place) under each number's place before it's drawn, in place
    /// of a value-background color: numbers counting up over a backdrop (Midnight Assault's
    /// VWB_DrawPicMask of L_BACKDROPPIC)
    /// </summary>
    public string? ValueBackgroundPic { get; set; }

    /// <summary>Stars beside the figures once they're all shown (Midnight Assault's reward stars)</summary>
    public IntermissionStars? Stars { get; set; }

    /// <summary>A row of pictures, one for each of some unlocks, as it's unlocked or not (Midnight Assault's dog tags)</summary>
    public IntermissionUnlockPics? UnlockPics { get; set; }

    /// <summary>A line under the tally for a level finished unseen (Midnight Assault's stealth bonus)</summary>
    public IntermissionStealthBonus? StealthBonus { get; set; }

    /// <summary>The sounds of the tally as the time bonus and ratios count up</summary>
    public IntermissionSounds Sounds { get; set; } = new();

    /// <summary>The bonus points added to the score: for time under par, and for perfect ratios</summary>
    public IntermissionScoring Scoring { get; set; } = new();

    /// <summary>The screen shown when a cluster is won, in the same font and color</summary>
    public VictoryScreen? Victory { get; set; }

    /// <summary>
    /// The text over the view before a boss's death is replayed (A_StartDeathCam), in the same
    /// font and color
    /// </summary>
    public DeathCamScreen? DeathCam { get; set; }
}

/// <summary>The level-end bonus. Any left out is 0.</summary>
public record IntermissionScoring
{
    /// <summary>Points for each second the level was finished under par</summary>
    public int? TimeBonus { get; set; }

    /// <summary>Points for each ratio (kill, secret, treasure) at 100%</summary>
    public int? PerfectBonus { get; set; }
}

/// <summary>BJ on the level-end screen: drawn at (x, y), turning to the next pic every breath-tics</summary>
public record IntermissionBj
{
    /// <summary>His left edge, in 320x200 pixels</summary>
    public int X { get; set; }

    /// <summary>His top edge, in 320x200 pixels</summary>
    public int Y { get; set; }

    /// <summary>The pictures he turns through, in order, starting again after the last (Wolf3D's L_GUY, L_GUY2)</summary>
    public List<string> Pics { get; set; } = [];

    /// <summary>Tics (70 a second) between one pic and the next</summary>
    public int BreathTics { get; set; } = 35;
}

/// <summary>
/// The victory screen: its pictures and text, and where the won cluster's total time and
/// average ratios go (values time, kill, secret, treasure). objective.NAME is the cluster's
/// levels' average percentage of it, or with count: true their done of total; item.NAME as on
/// the level-end screen. A label with only-with shows with its value.
/// </summary>
public record VictoryScreen
{
    /// <summary>The music while the screen is up; none keeps what was playing</summary>
    public string? Music { get; set; }

    /// <summary>Pictures drawn over the labels (Wolf3D's L_BJWINS)</summary>
    public List<IntermissionPic> Pics { get; set; } = [];

    /// <summary>The screen's text</summary>
    public List<IntermissionLabel> Labels { get; set; } = [];

    /// <summary>Where each of the won cluster's values goes, by name (time, kill, secret, treasure...)</summary>
    public Dictionary<string, IntermissionValue> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>The death cam's text, drawn over the faded view</summary>
public record DeathCamScreen
{
    /// <summary>The text ("Let's see that again!")</summary>
    public List<IntermissionLabel> Labels { get; set; } = [];

    /// <summary>
    /// Milliseconds after the boss dies before the view fades; after the replay, before the
    /// level ends
    /// </summary>
    public int PauseMs { get; set; }

    /// <summary>Tics (70 a second) the text stays up before the replay, unless a key is pressed</summary>
    public int HoldTics { get; set; }
}

/// <summary>
/// Stars beside the level-end screen's figures once they're all shown: gold where one's perfect
/// (time: under par; a ratio or objective: 100%), silver where it betters the level's best before
/// this finish (records.cfg; time: quicker; a level never finished before has nothing to better).
/// at: where each goes, by value name (time, kill, secret, treasure, objective.NAME).
/// </summary>
public record IntermissionStars
{
    /// <summary>The picture for a perfect figure</summary>
    public string Gold { get; set; } = "";

    /// <summary>The picture for a figure that betters the level's best</summary>
    public string Silver { get; set; } = "";

    /// <summary>Where each figure's star goes (its top left corner), by value name</summary>
    public Dictionary<string, IntermissionPlace> At { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// A row of pictures once the tally's shown, one for each of the unlocks in turn from (x, y),
/// step-x apart: unlocked's picture if it's unlocked (progress.cfg), else locked's. clusters:
/// only on those clusters' levels; on every level when left out.
/// </summary>
public record IntermissionUnlockPics
{
    /// <summary>The unlock names, one picture each, left to right</summary>
    public List<string> Unlocks { get; set; } = [];

    /// <summary>The picture for one that's unlocked</summary>
    public string Unlocked { get; set; } = "";

    /// <summary>The picture for one that isn't yet</summary>
    public string Locked { get; set; } = "";

    /// <summary>The first picture's left edge, in 320x200 pixels</summary>
    public int X { get; set; }

    /// <summary>The pictures' top edge, in 320x200 pixels</summary>
    public int Y { get; set; }

    /// <summary>Pixels from one picture's left edge to the next</summary>
    public int StepX { get; set; }

    /// <summary>The clusters whose levels show the row; every level when left out</summary>
    public List<int> Clusters { get; set; } = [];
}

/// <summary>
/// A line centred at y once the tally's shown, for a level finished unseen with a stealth tally
/// (stealth kills, or none killed of some there), with its sound: all-kills when every enemy was
/// killed unseen, no-kills when none was killed, some-kills when every kill was unseen but some
/// were left. Texts are $language keys or the text.
/// </summary>
public record IntermissionStealthBonus
{
    /// <summary>The line's top edge, in 320x200 pixels</summary>
    public int Y { get; set; }

    /// <summary>Its font; the screen's when left out</summary>
    public string? Font { get; set; }

    /// <summary>Its color; the screen's when left out</summary>
    public string? Color { get; set; }

    /// <summary>Played as the line shows; silent when left out</summary>
    public string? Sound { get; set; }

    /// <summary>The line when every enemy was killed unseen</summary>
    public string AllKills { get; set; } = "";

    /// <summary>The line when no one was killed</summary>
    public string NoKills { get; set; } = "";

    /// <summary>The line when every kill was unseen but some enemies were left</summary>
    public string SomeKills { get; set; } = "";
}

/// <summary>A place on the screen</summary>
public record IntermissionPlace
{
    /// <summary>Across, in 320x200 pixels</summary>
    public int X { get; set; }

    /// <summary>Down, in 320x200 pixels</summary>
    public int Y { get; set; }
}

/// <summary>A picture at (x, y)</summary>
public record IntermissionPic
{
    /// <summary>The picture</summary>
    public string Pic { get; set; } = "";

    /// <summary>Its left edge, in 320x200 pixels</summary>
    public int X { get; set; }

    /// <summary>Its top edge, in 320x200 pixels</summary>
    public int Y { get; set; }
}

/// <summary>
/// Text on the level-end screen: a $NAME language key or the text, at (x, y). With only-with,
/// drawn only when that value is shown (stealth, objective.NAME), e.g. its name and "%".
/// </summary>
public record IntermissionLabel
{
    /// <summary>The text, or a $NAME language key; \n starts a new line</summary>
    public string Text { get; set; } = "";

    /// <summary>Its left edge, in 320x200 pixels</summary>
    public int X { get; set; }

    /// <summary>Its top edge, in 320x200 pixels</summary>
    public int Y { get; set; }

    /// <summary>A value name (stealth, objective.NAME): the label is drawn only when that value is shown</summary>
    public string? OnlyWith { get; set; }

    /// <summary>Its own color, in place of the screen's (Midnight Assault's "%" in the figures' red)</summary>
    public string? Color { get; set; }
}

/// <summary>
/// Where a value goes on the level-end screen: starting at x, with its right edge at right (a
/// number counts up in place) or its middle at center. y is its top. format puts the value in
/// a text: a $NAME language key or the text, with {0} where the value goes (e.g. "by {0}").
/// </summary>
public record IntermissionValue
{
    /// <summary>Its left edge, when it has no right or center</summary>
    public int X { get; set; }

    /// <summary>Its right edge, so a number counting up stays in place (over center and x)</summary>
    public int? Right { get; set; }

    /// <summary>Its middle (over x)</summary>
    public int? Center { get; set; }

    /// <summary>Its top edge, in 320x200 pixels</summary>
    public int Y { get; set; }

    /// <summary>A text the value is put in, with {0} where it goes ("by {0}"); without {0}, the value follows it</summary>
    public string? Format { get; set; }

    /// <summary>objective.NAME, stealth: shown as done/total, counted up, not as a percentage</summary>
    public bool Count { get; set; }
}

/// <summary>The sounds of the ratio and time bonus tally. Any left out are silent.</summary>
public record IntermissionSounds
{
    /// <summary>Every 10% as a ratio counts up, and as the time bonus counts up</summary>
    public string? Tally { get; set; }

    /// <summary>The time bonus, or a ratio between 1% and 99%, finished counting</summary>
    public string? TallyDone { get; set; }

    /// <summary>A ratio reached 100% (and its bonus is added)</summary>
    public string? Perfect { get; set; }

    /// <summary>A ratio finished at 0%</summary>
    public string? None { get; set; }

    /// <summary>Milliseconds of silence, the tally cut off, before <see cref="Perfect"/> or <see cref="None"/></summary>
    public int? PauseMs { get; set; }
}
