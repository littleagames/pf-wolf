namespace Wolf3D.Assets;

internal record GameInfoAsset : Asset
{
    public DefaultMapInfo DefaultMap { get; init; } = new();
    public Dictionary<string, SkillInfo> Skills { get; init; } = [];
    public Dictionary<string, EpisodeInfo> Episodes { get; init; } = [];
    public Dictionary<int, ClusterInfo> Clusters { get; init; } = [];
    public Dictionary<string, MapInfo> Maps { get; init; } = [];
    public List<string> EndStrings { get; init; } = [];

    public SignonInfo Signon { get; init; } = new();

    /// <summary>
    /// Show the "This game is NOT shareware" notice before the title (Wolf3D's registered versions)
    /// </summary>
    public bool NonSharewareNotice { get; init; }

    /// <summary>
    /// Title screen graphics, drawn stacked top to bottom (Spear's title is two halves)
    /// </summary>
    public List<string> TitlePics { get; init; } = [];

    /// <summary>
    /// Palette the title fades in with, when it isn't drawn in the game palette (Spear's TITLEPAL)
    /// </summary>
    public string? TitlePalette { get; init; }

    /// <summary>
    /// Music for the title, demo loop and control panel
    /// </summary>
    public string? IntroMusic { get; init; }

    public string? HighScoresMusic { get; init; }

    /// <summary>
    /// #RRGGBB the screen fades to when leaving the control panel
    /// </summary>
    public string? MenuFadeColor { get; init; }

    // Fade styles are palette, fizzle, melt or mosaic (see FadeStyle). Each has a length in
    // tics (70 a second) beside it; a palette fade takes a step every 2 tics or so.

    /// <summary>
    /// How the screen fades out and back in between screens (default palette)
    /// </summary>
    public string? ScreenFadeStyle { get; init; }

    /// <summary>
    /// Length of every screen fade, when set; otherwise each keeps its own (mostly 60 tics)
    /// </summary>
    public int? ScreenFadeTics { get; init; }

    /// <summary>
    /// How the control panel fades out and in (default screen-fade-style)
    /// </summary>
    public string? MenuFadeStyle { get; init; }

    /// <summary>
    /// Length of the control panel's fades (default 20)
    /// </summary>
    public int? MenuFadeTics { get; init; }

    /// <summary>
    /// How the view changes over to red when the player dies, and to the death cam after a
    /// boss dies (default fizzle). Palette has nothing to fade here, so it cuts straight over.
    /// </summary>
    public string? DeathFadeStyle { get; init; }

    /// <summary>
    /// Length of the death fade (default 70)
    /// </summary>
    public int? DeathFadeTics { get; init; }

    /// <summary>
    /// How the view appears when a level starts, or restarts after dying (default fizzle)
    /// </summary>
    public string? LevelFadeStyle { get; init; }

    /// <summary>
    /// Length of the level start fade (default 20)
    /// </summary>
    public int? LevelFadeTics { get; init; }

    /// <summary>
    /// Graphic drawn behind the menus in place of their background color, when set
    /// </summary>
    public string? MenuBackdrop { get; init; }

    /// <summary>
    /// The band across the top of the menus and high scores; Wolf3D's when unset
    /// </summary>
    public MenuStripeInfo MenuStripe { get; init; } = new();

    /// <summary>
    /// The messages shown over the view (hud-messages.yaml): whether they're on until the
    /// player says otherwise, and the style each kind is shown in when its actor or door
    /// doesn't give one
    /// </summary>
    public HudMessagesInfo HudMessages { get; init; } = new();

    /// <summary>
    /// How the high scores screen is laid out; Wolf3D's layout when unset
    /// </summary>
    public HighScoresInfo HighScores { get; init; } = new();

    /// <summary>
    /// The player gets an extra life each time their score passes another this many points;
    /// 0 (or unset) for never
    /// </summary>
    public int ExtraLifeScore { get; init; }

    public override void Merge(Asset other)
    {
        // TODO: Overwrite or merge the data
    }
}

/// <summary>
/// A solid band with a line under it, both measured down from the top the menu asks for
/// </summary>
internal record MenuStripeInfo
{
    public int Height { get; init; } = 24;
    public string Color { get; init; } = "Black";

    /// <summary>
    /// Row of the line, counted from the band's top
    /// </summary>
    public int LineY { get; init; } = 22;

    public string LineColor { get; init; } = "STRIPE";
}

internal record HudMessagesInfo
{
    /// <summary>Whether messages are shown before the player turns them on or off (msg_enabled)</summary>
    public bool Enabled { get; init; }

    /// <summary>The style for an item's pickup message</summary>
    public string PickupStyle { get; init; } = "Default";

    /// <summary>The style for a locked door's message</summary>
    public string LockStyle { get; init; } = "Center";

    /// <summary>The style for what killed the player</summary>
    public string ObituaryStyle { get; init; } = "Obituary";
}

/// <summary>
/// The high scores screen: a title picture and column headings over the menu stripes, then a
/// row every 16 pixels of name, level and score. The defaults are Wolf3D's layout.
/// </summary>
internal record HighScoresInfo
{
    public string Pic { get; init; } = "HighScores";
    public int PicX { get; init; } = 48;
    public int PicY { get; init; }

    /// <summary>
    /// Column heading pictures (Spear's are part of its title picture)
    /// </summary>
    public List<PicPlacement> Headers { get; init; } =
    [
        new() { Pic = "C_Name", X = 4 * 8, Y = 68 },
        new() { Pic = "C_Level", X = 20 * 8, Y = 68 },
        new() { Pic = "C_Score", X = 28 * 8, Y = 68 },
    ];

    public string Font { get; init; } = "SmallFont";
    public string Color { get; init; } = "White";

    /// <summary>
    /// Top of the first row
    /// </summary>
    public int RowY { get; init; } = 76;

    public int NameX { get; init; } = 4 * 8;

    /// <summary>
    /// Where the level number ends; with show-episode, "E#/L" goes before it and it ends 6 pixels short
    /// </summary>
    public int LevelRight { get; init; } = 22 * 8;

    public bool ShowEpisode { get; init; } = true;

    /// <summary>
    /// Drawn in place of the level for a score from a game that was won (Spear's C_WonSpear)
    /// </summary>
    public string? WonPic { get; init; }

    public int ScoreRight { get; init; } = 34 * 8 - 8;

    /// <summary>
    /// Text color the new high score's name is typed in
    /// </summary>
    public string EntryColor { get; init; } = "White";

    /// <summary>
    /// Color behind the name being typed
    /// </summary>
    public string EntryBackground { get; init; } = "BORDCOLOR";

    /// <summary>
    /// Width of a bar in entry-background drawn behind the name before typing; none when 0
    /// </summary>
    public int EntryBarWidth { get; init; }

    /// <summary>
    /// How wide the typed name can get
    /// </summary>
    public int EntryWidth { get; init; } = 100;
}

internal record PicPlacement
{
    public string Pic { get; init; } = null!;
    public int X { get; init; }
    public int Y { get; init; }
}

internal record SignonInfo
{
    public string? Pic { get; init; }

    /// <summary>
    /// Wait for "Press a key" before continuing, instead of a short pause
    /// </summary>
    public bool PressAKey { get; init; }
}

internal record DefaultMapInfo
{
    public string FloorColor { get; init; } = null!;
    public string CeilingColor { get; init; } = null!;

    /// <summary>
    /// How many 64 unit stories tall the walls are
    /// </summary>
    public int WallHeight { get; init; } = 1;

    /// <summary>
    /// Graphic (or wall texture) drawn in place of the ceiling color; null for none
    /// </summary>
    public string? Sky { get; init; } = null;

    /// <summary>
    /// Texture on every floor tile the flat plane doesn't give one (see mapdefs flats); null
    /// leaves them the floor color
    /// </summary>
    public string? DefaultFloor { get; init; } = null;

    /// <summary>
    /// Texture on every ceiling tile the flat plane doesn't give one; null leaves them the
    /// ceiling color. A sky shows in place of ceiling flats.
    /// </summary>
    public string? DefaultCeiling { get; init; } = null;
}

internal record SkillInfo
{
    public string Name { get; init; } = null!;
    public string PicName { get; init; } = null!;

    // Not sure if I want the filtering of things here, or each tile would hold that info
    // or this would be a category that both things listen to a spawnfilters list
    public List<int> SpawnFilter { get; init; } = [];
}

internal record EpisodeInfo
{
    /// <summary>
    /// Text displayed to title the episode
    /// </summary>
    public string Name { get; init; } = null!;

    /// <summary>
    /// Map asset value of which map to start the episode
    /// </summary>
    public string StartMap { get; init; } = null!;

    /// <summary>
    /// Graphic asset that is used to display on the episode menu
    /// </summary>
    public string PicName { get; init; } = null!;

    /// <summary>
    /// Single key press to auto jump to the episode in the menu list
    /// </summary>
    public char Key { get; init; }
}

/// <summary>
/// What's shown when a cluster is won. The win tally always shows; around it come, in order,
/// the victory frames, the tally, the end text and the end screens, each when set.
/// </summary>
internal record ClusterInfo
{
    /// <summary>
    /// Article shown after the win tally (Wolf3D's ENDARTn), when set
    /// </summary>
    public string? EndText { get; init; }

    /// <summary>
    /// #RRGGBB the game slowly fades to when the cluster is won, and the victory frames fade
    /// out to; black when unset
    /// </summary>
    public string? VictoryFadeColor { get; init; }

    /// <summary>
    /// Music over the victory frames
    /// </summary>
    public string? VictoryMusic { get; init; }

    /// <summary>
    /// Pictures shown one after another on the view color before the win tally (Spear's BJ collapsing)
    /// </summary>
    public List<VictoryFrameInfo> VictoryFrames { get; init; } = [];

    /// <summary>
    /// Full screen pictures shown one after another after the win tally, each faded in with
    /// its own palette (Spear's ending)
    /// </summary>
    public List<EndScreenInfo> EndScreens { get; init; } = [];
}

internal record VictoryFrameInfo
{
    public string Pic { get; init; } = null!;
    public int X { get; init; }
    public int Y { get; init; }

    /// <summary>
    /// How long the frame stays up (70 a second)
    /// </summary>
    public int Tics { get; init; }
}

internal record EndScreenInfo
{
    public string Pic { get; init; } = null!;

    /// <summary>
    /// Palette the picture is drawn in; the game palette when unset
    /// </summary>
    public string? Palette { get; init; }

    /// <summary>
    /// Text shown in turn along the bottom of the picture, each until a key is pressed or
    /// caption-tics pass; with none, the picture stays until a key is pressed
    /// </summary>
    public List<string> Captions { get; init; } = [];

    // Caption colors are looked up in the game palette's theme, but drawn in the screen's own
    // palette, so a raw palette index ("208") is usually what's wanted

    public string CaptionColor { get; init; } = "White";

    /// <summary>
    /// Top of the caption area, which is cleared to caption-background before each caption
    /// </summary>
    public int CaptionY { get; init; } = 180;

    public string CaptionBackground { get; init; } = "0";

    public int CaptionTics { get; init; } = 700;
}

internal record MapInfo
{
    /// <summary>
    /// Name shown for the level (e.g. on saves), or a $language key; when unset it's
    /// "Episode X, Floor Y" (just "Floor Y" in a single-episode game)
    /// </summary>
    public string? Name { get; init; }

    //public string Current { get; set; }
    public string Next { get; init; } = null!;
    public string? SecretNext { get; init; } = null;
    public int FloorNumber { get; init; }
    public int ParTime { get; init; } = 0;
    public string Music { get; init; } = null!;
    public short Cluster { get; init; }

    public string? FloorColor { get; init; } = null;
    public string? CeilingColor { get; init; } = null;

    /// <summary>
    /// How many 64 unit stories tall the walls are; null uses the default map's
    /// </summary>
    public int? WallHeight { get; init; } = null;

    /// <summary>
    /// Graphic (or wall texture) drawn in place of the ceiling color; null uses the default map's
    /// </summary>
    public string? Sky { get; init; } = null;

    /// <summary>
    /// Texture for floor tiles the flat plane doesn't give one; null uses the default map's
    /// </summary>
    public string? DefaultFloor { get; init; } = null;

    /// <summary>
    /// Texture for ceiling tiles the flat plane doesn't give one; null uses the default map's
    /// </summary>
    public string? DefaultCeiling { get; init; } = null;
}