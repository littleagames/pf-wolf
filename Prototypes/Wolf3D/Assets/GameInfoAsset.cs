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

    public override void Merge(Asset other)
    {
        // TODO: Overwrite or merge the data
    }
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

internal record ClusterInfo
{
    public string EndText { get; init; } = null!;
}

internal record MapInfo
{
    //public string Current { get; set; }
    public string Next { get; init; } = null!;
    public string? SecretNext { get; init; } = null;
    public int FloorNumber { get; init; }
    public int ParTime { get; init; } = 0;
    public string Music { get; init; } = null!;
    public short Cluster { get; init; }

    public string? FloorColor { get; init; } = null;
    public string? CeilingColor { get; init; } = null;
}