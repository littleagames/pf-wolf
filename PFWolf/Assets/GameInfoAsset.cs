namespace PFWolf.Assets;

internal record GameInfoAsset : Asset
{
    public DefaultMapInfo DefaultMap { get; init; } = new();
    public Dictionary<string, SkillInfo> Skills { get; init; } = [];

    /// <summary>
    /// The classes a new game can be played as, keyed by actordefs class (Player or one with
    /// Player as a parent), in menu order. The first is the default; none means Player.
    /// </summary>
    public Dictionary<string, PlayerClassInfo> PlayerClasses { get; init; } = [];

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

    /// <summary>
    /// Screens shown once as the game starts (Program.TitleScreens.cs), in place of Wolf3D's
    /// notice and PG-13 screen; a key skips the one it's pressed on
    /// </summary>
    public List<TitleScreenInfo> Intro { get; init; } = [];

    /// <summary>
    /// Screens shown over and over until a key goes to the menu, in place of Wolf3D's title,
    /// credits, high scores and demo. Played to intro-music when nothing else is playing.
    /// </summary>
    public List<TitleScreenInfo> TitleLoop { get; init; } = [];

    public string? HighScoresMusic { get; init; }

    /// <summary>
    /// The control panel's music in place of what each menudef plays (a pack built on another
    /// pack's menus, with its own song); the menudefs' own when unset
    /// </summary>
    public string? MenuMusic { get; init; }

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
    /// The color the view fades to when the player dies: a color name, #RRGGBB or palette
    /// index (default Maroon)
    /// </summary>
    public string? DeathFadeColor { get; init; }

    /// <summary>
    /// How fast the dead player turns to face their killer, in degrees a tic (default 2)
    /// </summary>
    public int? DeathTurnSpeed { get; init; }

    /// <summary>
    /// How high the dead player's view drops to, as Doom's does, while they turn to face their
    /// killer: in texels above the floor (64 a story, 32 standing; 4-60). Left out, the view
    /// stays where it is, as in Wolf3D.
    /// </summary>
    public int? DeathDropHeight { get; init; }

    /// <summary>
    /// How fast the dead player's view drops, in texels a tic (default 1)
    /// </summary>
    public int? DeathDropSpeed { get; init; }

    /// <summary>
    /// The weapon in hand's height as a share of the view's, standing on the view's bottom edge
    /// (default 1, as Wolf3D; Planet Strike's are drawn at 88 of Aliens of Gold's 128)
    /// </summary>
    public double? WeaponScale { get; init; }

    /// <summary>
    /// Whether the weapon in hand bobs as the player walks forwards or back, as Planet Strike's
    /// (default false)
    /// </summary>
    public bool? WeaponBob { get; init; }

    /// <summary>
    /// How long the death fade's color stays up before the level restarts or the game ends,
    /// in tics, unless a key is pressed (default 100)
    /// </summary>
    public int? DeathHoldTics { get; init; }

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

    /// <summary>
    /// The skill (a key in skills) demos are recorded and played back on; the hardest (last)
    /// one when unset. A demo only replays right on the skill it was recorded on.
    /// </summary>
    public string? DemoSkill { get; init; }

    /// <summary>
    /// A presenter script (presenter.yaml's message box) shown as a level loads, in place of the
    /// "get psyched" picture: Blake Stone's "Get Ready, Blake!"
    /// </summary>
    public string? LevelStartMessage { get; init; }

    /// <summary>
    /// A presenter script (a VGAGRAPH text) shown in presenter.yaml's message box when an episode
    /// with an end-briefing is won, before the briefing
    /// </summary>
    public string? MissionWonMessage { get; init; }

    /// <summary>
    /// Dying puts the player back as they came into the level (health, score, items), less a
    /// life, as Blake Stone does; otherwise they restart with the starting health and items
    /// </summary>
    public bool DeathRestoresLevelStart { get; init; }

    /// <summary>
    /// A presenter script (a VGAGRAPH text) F1 shows in presenter.yaml's briefing window, in
    /// place of Wolf3D's help article
    /// </summary>
    public string? HelpText { get; init; }

    /// <summary>
    /// What's shown when the game is lost, the last life gone, before the high scores (Blake
    /// Stone's transmission from Goldfire); nothing when unset
    /// </summary>
    public LoseScreenInfo? LoseScreen { get; init; }

    /// <summary>
    /// The box the menus' questions and messages are shown in (Blake Stone's bevelled one); the
    /// Wolf3D window when unset
    /// </summary>
    public MenuMessageInfo? MenuMessage { get; init; }

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

    /// <summary>How many scores there are (default 7, or as many as the defaults)</summary>
    public int? Count { get; init; }

    /// <summary>The scores a new player's table starts with, top first; Wolf3D's when empty</summary>
    public List<HighScoreDefault> Defaults { get; init; } = [];

    /// <summary>
    /// A menudef whose components are drawn as the screen's frame (Blake Stone's LINC terminal),
    /// in place of the menu stripes and title picture; its pic and headers are still drawn
    /// </summary>
    public string? Frame { get; init; }

    /// <summary>Text drawn over the frame, such as column headings</summary>
    public List<HighScoreLabel> Labels { get; init; } = [];

    /// <summary>Pixels from one row to the next</summary>
    public int RowHeight { get; init; } = 16;

    /// <summary>A shadow color for the rows' text, one pixel right and down; none when unset</summary>
    public string? Shadow { get; init; }

    /// <summary>Whether the level column shows</summary>
    public bool ShowLevel { get; init; } = true;

    /// <summary>Where the ratio column ends (Blake Stone's mission ratio); no column when 0</summary>
    public int RatioRight { get; init; }
}

internal record HighScoreDefault
{
    public string Name { get; init; } = "";
    public int Score { get; init; } = 10000;
    public int Level { get; init; } = 1;
}

internal record HighScoreLabel
{
    public string Text { get; init; } = "";
    public int X { get; init; }
    public int Y { get; init; }
    public string? Font { get; init; }
    public string? Color { get; init; }
}

/// <summary>
/// One of game-info's intro or title-loop screens. It's a movie, a demo, or else drawn from a
/// background color, the high scores, the title, a picture and presenter text (each when set,
/// in that order), then held for its seconds or until its music ends.
/// </summary>
internal record TitleScreenInfo
{
    /// <summary>A movie to play (gamepack-info's JamMovieFileLoader names them)</summary>
    public string? Movie { get; init; }

    /// <summary>Plays the next demo</summary>
    public bool Demo { get; init; }

    /// <summary>Fills the screen first: a color name, #RRGGBB or palette index</summary>
    public string? Background { get; init; }

    public bool HighScores { get; init; }

    /// <summary>Draws game-info's title-pics</summary>
    public bool Title { get; init; }

    public string? Pic { get; init; }
    public int X { get; init; }
    public int Y { get; init; }

    /// <summary>Presenter text printed into a window</summary>
    public TitleTextInfo? Text { get; init; }

    /// <summary>The palette the screen fades in with; the game palette when unset</summary>
    public string? Palette { get; init; }

    /// <summary>Music started as the screen shows; what's playing carries on when unset</summary>
    public string? Music { get; init; }

    /// <summary>false plays the music once</summary>
    public bool MusicLoop { get; init; } = true;

    /// <summary>Stops the music as the screen shows</summary>
    public bool StopMusic { get; init; }

    /// <summary>How long the screen stays up, unless a key is pressed</summary>
    public double Seconds { get; init; }

    /// <summary>Stays up until the music ends (when music is on), unless a key is pressed</summary>
    public bool UntilMusicEnds { get; init; }

    /// <summary>#RRGGBB the screen fades to before it's shown (Blake Stone's blue)</summary>
    public string? FadeFrom { get; init; }

    /// <summary>#RRGGBB the screen fades to when it's done, before black (Blake Stone's red flash)</summary>
    public string? FadeTo { get; init; }

    /// <summary>A picture the screen fizzles over to once it's faded in, before it's held (Planet Strike's title)</summary>
    public string? FizzlePic { get; init; }

    /// <summary>Tics the fizzle takes</summary>
    public int FizzleTics { get; init; } = 70;
}

/// <summary>Presenter text on a title screen: a VGAGRAPH text, its window and colors (palette indices)</summary>
internal record TitleTextInfo
{
    public string Script { get; init; } = "";
    public int X1 { get; init; }
    public int Y1 { get; init; }
    public int X2 { get; init; } = 319;
    public int Y2 { get; init; } = 199;
    public int Font { get; init; }
    public int Color { get; init; }
    public int Background { get; init; }
    public int Light { get; init; }
    public int Dark { get; init; }
    public int Shadow { get; init; }
}

/// <summary>
/// The screen shown when the game is lost: a picture, with presenter text typed out a letter at
/// a time into a window on it (a key prints the rest), scrolling as it fills. Colors are
/// palette indices.
/// </summary>
internal record LoseScreenInfo
{
    public string? Pic { get; init; }
    public string Script { get; init; } = "";
    public int X1 { get; init; }
    public int Y1 { get; init; }
    public int X2 { get; init; } = 319;
    public int Y2 { get; init; } = 199;
    public int Font { get; init; }
    public int Color { get; init; }
    public int Background { get; init; }
    public int Light { get; init; }
    public int Dark { get; init; }
    public int Shadow { get; init; }

    /// <summary>Tics between letters</summary>
    public int PrintDelay { get; init; } = 2;

    /// <summary>Played as each letter is typed</summary>
    public string? TypeSound { get; init; }
}

/// <summary>
/// The menus' message and question box (Blake Stone's): a bevelled box in its hi, med and lo
/// colors, with the text in color over a shadow. Colors are color names, #RRGGBB or palette indices.
/// </summary>
internal record MenuMessageInfo
{
    public string Font { get; init; } = "LargeFont";
    public string Color { get; init; } = "White";
    public string Shadow { get; init; } = "Black";
    public string Hi { get; init; } = "White";
    public string Med { get; init; } = "Grey";
    public string Lo { get; init; } = "Black";
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

    /// <summary>
    /// The open area of the pic the startup info (engine, game, mods, video, input, sound,
    /// content, warnings) is printed in; none is printed when left out
    /// </summary>
    public SignonTextArea? TextArea { get; init; }
}

/// <summary>Where the signon's startup info goes, in 320x200 coordinates, and its colors</summary>
internal record SignonTextArea
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    public string Font { get; init; } = "SmallFont";
    // Each line's name ("Video:"), its value, and the warnings
    public string LabelColor { get; init; } = "TEXTCOLOR";
    public string Color { get; init; } = "HIGHLIGHT";
    public string WarningColor { get; init; } = "READHCOLOR";
    // The area's own color on the pic, which the text is drawn over
    public string Background { get; init; } = "BKGDCOLOR";
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

    /// <summary>
    /// Distance shading (night, fog) on every level; null leaves levels unshaded
    /// </summary>
    public ShadingInfo? Shading { get; init; } = null;

    /// <summary>
    /// Light zones every level has, by zone id (the zone plane's value); a map's own zones
    /// override these value by value
    /// </summary>
    public Dictionary<int, ZoneInfo> Zones { get; init; } = [];
}

/// <summary>
/// How the tiles of a light zone (those with its id on the zone plane, plane 5) are lit, in
/// place of the level's light. Distance still fades them as the level's shading says.
/// </summary>
internal record ZoneInfo
{
    /// <summary>Light, 0 (black) to 255 (full, the default)</summary>
    public int? Light { get; init; }

    /// <summary>#RRGGBB the light is tinted with (default white: no tint), e.g. "#FF4040" for red emergency lighting</summary>
    public string? Color { get; init; }

    /// <summary>
    /// How the light moves by itself: none (the default), flicker (jumps between low and the
    /// light at random, up to tics apart), pulse (glows down to low and back every tics) or
    /// strobe (the light for bright-tics, then low, every tics)
    /// </summary>
    public string? Effect { get; init; }

    /// <summary>The effect's dark end, 0 to 255 (default half the light)</summary>
    public int? Low { get; init; }

    /// <summary>The effect's timing, 70 a second (default flicker 8, pulse 70, strobe 35)</summary>
    public int? Tics { get; init; }

    /// <summary>How long a strobe stays at the light each time (default 5)</summary>
    public int? BrightTics { get; init; }
}

/// <summary>
/// How a level is shaded: everything is dimmed to its light, then fades toward the fade color
/// with distance. Each value left out comes from the default map's shading, else the default
/// here. A level with neither map nor default map shading isn't shaded.
/// </summary>
internal record ShadingInfo
{
    /// <summary>#RRGGBB distance fades toward: black for night, grey for fog (default black)</summary>
    public string? FadeColor { get; init; }

    /// <summary>Tiles away, along the view, the fade starts (default 0)</summary>
    public double? FadeStart { get; init; }

    /// <summary>Tiles away the fade reaches max-fade (default 16)</summary>
    public double? FadeEnd { get; init; }

    /// <summary>How far toward the fade color, in percent, the view fades at fade-end and beyond (default 100)</summary>
    public int? MaxFade { get; init; }

    /// <summary>Light everywhere, 0 (black) to 255 (full, the default)</summary>
    public int? Light { get; init; }

    /// <summary>
    /// How the fade grows past fade-start: linear (the default), reaching max-fade at fade-end;
    /// or inverse, as Blake Stone's, max-fade × (1 − fade-start / distance), nearing max-fade
    /// far away (fade-end isn't used)
    /// </summary>
    public string? FadeCurve { get; init; }
}

/// <summary>
/// A skill in game-info's skills, which are listed easiest first. A skill's place in that list
/// (0 for the first) is what saves record and what mapdefs min-skill compares against.
/// </summary>
internal record SkillInfo
{
    public string Name { get; init; } = null!;
    public string PicName { get; init; } = null!;

    /// <summary>How much of the damage the player is dealt they take (default 1; the easiest skill's 0.25)</summary>
    public float DamageTaken { get; init; } = 1;

    /// <summary>
    /// The actordefs property enemies take their health from on this skill (e.g. health.normal);
    /// an enemy without it, or a skill without one, uses the enemy's plain `health`
    /// </summary>
    public string? EnemyHealth { get; init; }
}

/// <summary>A class in game-info's player-classes: how the class menu shows it</summary>
internal record PlayerClassInfo
{
    public string? Name { get; init; }
    public string? PicName { get; init; }
}

internal record EpisodeInfo
{
    /// <summary>
    /// Text displayed to title the episode
    /// </summary>
    public string Name { get; init; } = null!;

    /// <summary>
    /// Map asset value of which map to start the episode (none for a locked one)
    /// </summary>
    public string StartMap { get; init; } = null!;

    /// <summary>
    /// "locked-message": the episode is listed but can't be played (the shareware's episodes 2-6);
    /// picking it shows this text (or $LANGUAGE key) instead
    /// </summary>
    public string? LockedMessage { get; init; }

    public bool Locked => !string.IsNullOrWhiteSpace(LockedMessage);

    /// <summary>
    /// Graphic asset that is used to display on the episode menu
    /// </summary>
    public string PicName { get; init; } = null!;

    /// <summary>
    /// Single key press to auto jump to the episode in the menu list
    /// </summary>
    public char Key { get; init; }

    /// <summary>
    /// A presenter script (a VGAGRAPH text) shown as the episode starts; Esc there goes back to
    /// the menu
    /// </summary>
    public string? Briefing { get; init; }

    /// <summary>A presenter script shown when the episode is won</summary>
    public string? EndBriefing { get; init; }

    /// <summary>A movie played when the episode is won, before its end briefing</summary>
    public string? EndMovie { get; init; }
}

/// <summary>
/// What's shown when a cluster is won. The win tally always shows; around it come, in order,
/// the victory frames, the tally, the end text and the end screens, each when set.
/// </summary>
internal record ClusterInfo
{
    /// <summary>
    /// The cluster's levels are kept as they were left (Blake Stone's floors): going back to one
    /// finds it as the player left it, until the game leaves the cluster. Levels are left
    /// without the level-end screen.
    /// </summary>
    public bool Hub { get; init; }

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

    /// <summary>
    /// A deathmatch arena: picked by name in the multiplayer lobby. It's in no episode, so it's
    /// only played alone by warping to it (the map command), to try it out
    /// </summary>
    public bool Deathmatch { get; init; }

    //public string Current { get; set; }
    public string Next { get; init; } = null!;
    public string? SecretNext { get; init; } = null;

    /// <summary>
    /// Text (or a $language key) that makes leaving this level end the game, as the Spear of
    /// Destiny demo's last floor does: the intermission, then this message, then the high scores.
    /// The game isn't won, so there's no victory.
    /// </summary>
    public string? EndMessage { get; init; } = null;

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

    /// <summary>
    /// Distance shading; each value left out uses the default map's (see ShadingInfo)
    /// </summary>
    public ShadingInfo? Shading { get; init; } = null;

    /// <summary>
    /// Light zones by zone id; each value left out uses the default map's zone of that id (see ZoneInfo)
    /// </summary>
    public Dictionary<int, ZoneInfo>? Zones { get; init; } = null;
}