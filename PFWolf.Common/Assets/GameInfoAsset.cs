namespace PFWolf.Assets;

public record GameInfoAsset : Asset
{
    public DefaultMapInfo DefaultMap { get; init; } = new();
    public Dictionary<string, SkillInfo> Skills { get; init; } = [];

    /// <summary>
    /// The classes a new game can be played as, keyed by actordefs class (Player or one with
    /// Player as a parent), in menu order. The first is the default; none means Player.
    /// </summary>
    public Dictionary<string, PlayerClassInfo> PlayerClasses { get; init; } = [];

    /// <summary>
    /// Whether a single-player new game asks which of the player-classes to play as (default
    /// true). False keeps them for the multiplayer lobby's "Play as" only; new games are then
    /// played as the first.
    /// </summary>
    public bool ClassMenu { get; init; } = true;

    /// <summary>
    /// The colors a player can pick to be seen in, playing with others: translations from
    /// translations.yaml, which recolor the player's sprites, in menu order. The first is the
    /// default; none means players are seen in their class's own colors.
    /// </summary>
    public List<string> PlayerColors { get; init; } = [];

    /// <summary>The waves of enemies a multiplayer Horde game sends at the players</summary>
    public HordeInfo Horde { get; init; } = new();

    public Dictionary<string, EpisodeInfo> Episodes { get; init; } = [];
    public Dictionary<int, ClusterInfo> Clusters { get; init; } = [];
    public Dictionary<string, MapInfo> Maps { get; init; } = [];
    public List<string> EndStrings { get; init; } = [];

    /// <summary>
    /// Kinds of rain and snow a level's weather can name, added to (or replacing) the built-in
    /// rain, heavy-rain, snow and blizzard
    /// </summary>
    public Dictionary<string, WeatherInfo> WeatherTypes { get; init; } = [];

    public SignonInfo Signon { get; init; } = new();

    /// <summary>
    /// How many tiles a pushwall slides, unless something stops it sooner (default 2, Wolf3D's;
    /// End of Destiny's go 3)
    /// </summary>
    public int? PushwallDistance { get; init; }

    /// <summary>
    /// Music for the title, demo loop and control panel
    /// </summary>
    public string? IntroMusic { get; init; }

    /// <summary>The screens from the signon to the menu: the intro, then the title loop</summary>
    public TitleSequenceInfo TitleSequence { get; init; } = new();

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
    /// The menus' gun cursor (c_cursor1/c_cursor2): "none" leaves it out, the chosen item shown
    /// by its color alone (End of Destiny's HIDEMENUSTUFF). Left out, the gun.
    /// </summary>
    public string? MenuCursor { get; init; }

    /// <summary>
    /// What the Game Options menu's settings are before the player changes them: a mod can turn
    /// looking up and down off, or messages on. Each one left out keeps the engine's default.
    /// </summary>
    public SettingDefaultsInfo SettingDefaults { get; init; } = new();

    /// <summary>The crosshair's color: a color name, #RRGGBB or palette index (default White)</summary>
    public string? CrosshairColor { get; init; }

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
    /// Going unseen and in disguise (Gameplay/Stealth.cs): its messages, how long a suspicious
    /// enemy waits for papers, and how far off it stops caring. Defaults are Midnight Assault's.
    /// </summary>
    public StealthInfo Stealth { get; init; } = new();

    /// <summary>
    /// Kills in quick succession (Gameplay/KillChains.cs): how close together they count, the
    /// messages and the prizes by the chain's length. None when unset.
    /// </summary>
    public KillChainInfo KillChain { get; init; } = new();

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
    /// Lines of text over the "get psyched" screen, each centred, the first at the top (2 down)
    /// and each next one under it (End of Destiny's "Now entering" and the level's name). %m in a
    /// line is the level's name as the player sees it. None: the picture alone, as Wolf3D's.
    /// </summary>
    public List<LevelStartLine> LevelStartText { get; init; } = [];

    /// <summary>
    /// How long the "get psyched" screen holds, in tics (70 a second), unless a key's pressed:
    /// Wolf3D's 70 when unset (Midnight Assault's "Now entering..." held 3 seconds)
    /// </summary>
    public int? LevelStartTics { get; init; }

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
    /// Whether the game counts lives. False (Midnight Assault): dying never costs one, so the
    /// level just starts again as often as it takes and the game's never over.
    /// </summary>
    public bool Lives { get; init; } = true;

    /// <summary>
    /// Items a death gives back as the player had them coming into the level, after the
    /// starting items (Midnight Assault's medkits: oldmedkits). Not with death-restores-level-start,
    /// which puts everything back.
    /// </summary>
    public List<string> DeathKeeps { get; init; } = [];

    /// <summary>
    /// Level objectives by name (Gameplay/Objectives.cs): things a level counts and the player
    /// completes (switches with `objective`, actors with an `objective` property, A_CompleteObjective),
    /// with their messages, and whether the exits wait for them (Midnight Assault's sabotage
    /// targets and goal enemies)
    /// </summary>
    public Dictionary<string, ObjectiveInfo> Objectives { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A presenter script (a VGAGRAPH text) F1 shows in presenter.yaml's briefing window, in
    /// place of Wolf3D's help article
    /// </summary>
    public string? HelpText { get; init; }

    /// <summary>
    /// How articles (Read This, end texts, briefings shown as articles) lay out their pages: the
    /// pictures framing every page, the text's margins and color, and the page number. Wolf3D's
    /// help window by default.
    /// </summary>
    public ArticleInfo Article { get; init; } = new();

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
}

/// <summary>
/// A solid band with a line under it, both measured down from the top the menu asks for
/// </summary>
public record MenuStripeInfo
{
    public int Height { get; init; } = 24;
    public string Color { get; init; } = "Black";

    /// <summary>
    /// Row of the line, counted from the band's top
    /// </summary>
    public int LineY { get; init; } = 22;

    public string LineColor { get; init; } = "STRIPE";
}

/// <summary>
/// game-info setting-defaults: the Game Options menu's settings before the player chooses. null
/// keeps the engine's default (given with each).
/// </summary>
public record SettingDefaultsInfo
{
    /// <summary>Whether the player can look up and down at all: keys, stick and mouse look (default true)</summary>
    public bool? Look { get; init; }

    /// <summary>Messages over the view (default false)</summary>
    public bool? Messages { get; init; }

    /// <summary>The automap's level name, kills, treasure, secrets and time (default true)</summary>
    public bool? AutomapStats { get; init; }

    /// <summary>Each new level saving itself as it starts (default true)</summary>
    public bool? Autosave { get; init; }

    /// <summary>The weapon in hand bobbing as the player walks (default false; Planet Strike's true)</summary>
    public bool? WeaponBob { get; init; }

    /// <summary>Running without holding Run; holding it walks (default false)</summary>
    public bool? AlwaysRun { get; init; }

    /// <summary>A crosshair in the middle of the view (default false)</summary>
    public bool? Crosshair { get; init; }
}

/// <summary>The style each kind of message over the view is shown in (whether they're on is setting-defaults messages)</summary>
public record HudMessagesInfo
{
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
public record HighScoresInfo
{
    /// <summary>
    /// The pictures drawn over the stripes (or frame), in order: Wolf3D's title and column
    /// headings by default (Spear's headings are part of its title picture); none for a screen
    /// headed by labels alone
    /// </summary>
    public List<PicPlacement> Pics { get; init; } =
    [
        new() { Pic = "HighScores", X = 48, Y = 0 },
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
    /// in place of the menu stripes; its pics are still drawn
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

    /// <summary>
    /// In place of the score table, a row for each of these episodes with its levels' bests from
    /// records.cfg (Midnight Assault's awards); a finished game then has no name to type, the
    /// screen is just shown
    /// </summary>
    public HighScoresEpisodes? Episodes { get; init; }
}

/// <summary>
/// The high scores as each episode's best (Midnight Assault's DrawAwardsScreen): its name, its
/// levels' times added up, and lines of its stats as a menu stats-panel has them (percentages
/// averaged with a level never finished as nothing); the incomplete text in their place where
/// none of its levels has been finished. Names and labels are in the screen's color, the time and
/// values in value-color.
/// </summary>
public record HighScoresEpisodes
{
    /// <summary>The rows, top first</summary>
    public List<HighScoresEpisodeRow> Rows { get; init; } = [];

    /// <summary>Top of the first row</summary>
    public int Y { get; init; } = 54;

    public int RowHeight { get; init; } = 40;
    public int NameX { get; init; } = 20;

    /// <summary>Where the time goes (h:mm:ss); no time when below 0</summary>
    public int TimeX { get; init; } = 152;

    public int LabelX { get; init; } = 222;
    public int ValueX { get; init; } = 280;

    /// <summary>The time's and the values' color; the screen's when unset</summary>
    public string? ValueColor { get; init; }

    /// <summary>The stats, each a label and a value (as a stats-panel line's) at y from the row's top</summary>
    public List<HighScoresEpisodeLine> Lines { get; init; } = [];

    /// <summary>Shown at label-x for an episode none of whose levels has been finished</summary>
    public string Incomplete { get; init; } = "Incomplete";
}

public record HighScoresEpisodeRow
{
    /// <summary>The episode (game-info episodes key)</summary>
    public string Episode { get; init; } = "";

    /// <summary>The name shown; the episode's own when unset</summary>
    public string? Name { get; init; }
}

public record HighScoresEpisodeLine
{
    public string Label { get; init; } = "";

    /// <summary>time, kill, secret or objective.NAME (its percentage; with count, how many)</summary>
    public string Value { get; init; } = "";

    public bool Count { get; init; }

    /// <summary>From the row's top</summary>
    public int Y { get; init; }
}

public record HighScoreDefault
{
    public string Name { get; init; } = "";
    public int Score { get; init; } = 10000;
    public int Level { get; init; } = 1;
}

public record HighScoreLabel
{
    public string Text { get; init; } = "";
    public int X { get; init; }
    public int Y { get; init; }
    public string? Font { get; init; }
    public string? Color { get; init; }
}

/// <summary>
/// game-info's title-sequence: the screens from the signon to the menu (Ui/TitleScreens.cs)
/// </summary>
public record TitleSequenceInfo
{
    /// <summary>Screens shown once as the game starts; a key goes to the next one unless a screen's on-key says otherwise</summary>
    public List<TitleScreenInfo> Intro { get; init; } = [];

    /// <summary>
    /// Screens shown over and over until a key goes to the menu (unless a screen's on-key says
    /// otherwise). Played to intro-music when nothing else is playing.
    /// </summary>
    public List<TitleScreenInfo> Loop { get; init; } = [];
}

/// <summary>What a key does on a title-sequence screen (its on-key)</summary>
public enum TitleKeyAction
{
    /// <summary>Goes on to the next screen (the intro's default)</summary>
    Next,

    /// <summary>Leaves for the menu (the loop's default)</summary>
    Menu,

    /// <summary>Skips the rest of the intro into the loop</summary>
    EndIntro,

    /// <summary>Completes the screen's effect (its fizzle) and holds on; a key after that does the list's default</summary>
    Finish,

    /// <summary>Keys do nothing: the screen runs its full time</summary>
    Ignore,
}

public static class TitleKeyActions
{
    /// <summary>The on-key names, as written in game-info</summary>
    public static readonly IReadOnlyDictionary<string, TitleKeyAction> Names = new Dictionary<string, TitleKeyAction>(StringComparer.OrdinalIgnoreCase)
    {
        ["next"] = TitleKeyAction.Next,
        ["menu"] = TitleKeyAction.Menu,
        ["end-intro"] = TitleKeyAction.EndIntro,
        ["finish"] = TitleKeyAction.Finish,
        ["ignore"] = TitleKeyAction.Ignore,
    };

    /// <summary>The action an on-key names; false for an unset or unknown one</summary>
    public static bool TryParse(string? name, out TitleKeyAction action)
    {
        action = default;
        return !string.IsNullOrWhiteSpace(name) && Names.TryGetValue(name.Trim(), out action);
    }
}

/// <summary>
/// One of game-info's title-sequence screens. It's a movie, a demo, or else drawn from a
/// background color (or the menu's background and stripe), the high scores, pictures, lines of
/// text and presenter text (each when set, in that order), then held for its seconds, until its
/// music ends or until a key. A screen with pictures the game has none of is left out.
/// </summary>
public record TitleScreenInfo
{
    /// <summary>
    /// What a key does here (next, menu, end-intro, finish, ignore); the list's default when
    /// unset (next in the intro, menu in the loop). A movie has no effect to finish, so there
    /// finish does the default; a demo always ends on a key, so there ignore does too.
    /// </summary>
    public string? OnKey { get; init; }

    /// <summary>A movie to play (gamepack-info's JamMovieFileLoader names them)</summary>
    public string? Movie { get; init; }

    /// <summary>Plays the next demo</summary>
    public bool Demo { get; init; }

    /// <summary>Fills the screen first: a color name, #RRGGBB or palette index</summary>
    public string? Background { get; init; }

    /// <summary>Draws the control panel's background (menu-backdrop, or its border color) in place of a background</summary>
    public bool MenuBackground { get; init; }

    /// <summary>Draws the menu's stripe (game-info menu-stripe) across the screen at this y</summary>
    public int? Stripe { get; init; }

    /// <summary>Text printed at points on the screen (Wolf3D's non-shareware notice)</summary>
    public List<TitleLineInfo> Lines { get; init; } = [];

    public bool HighScores { get; init; }

    /// <summary>
    /// Pictures drawn in order, each with its top left corner at x, y (Spear's title is two
    /// halves, Midnight Assault's credits page several pictures). One the game hasn't got is
    /// skipped; when it has none of them, the screen is left out.
    /// </summary>
    public List<PicPlacement> Pics { get; init; } = [];

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

    /// <summary>Stays up until a key is pressed, however long (its seconds are ignored, and so is on-key ignore)</summary>
    public bool WaitForKey { get; init; }

    /// <summary>#RRGGBB the screen fades to before it's shown (Blake Stone's blue)</summary>
    public string? FadeFrom { get; init; }

    /// <summary>#RRGGBB the screen fades to when it's done, before black (Blake Stone's red flash)</summary>
    public string? FadeTo { get; init; }

    /// <summary>
    /// A picture the screen fizzles over to once it's faded in, before it's held (Planet Strike's
    /// title). A key during the fizzle does the screen's on-key: finish shows the picture at once.
    /// </summary>
    public string? FizzlePic { get; init; }

    /// <summary>Tics the fizzle takes</summary>
    public int FizzleTics { get; init; } = 70;
}

/// <summary>Text printed on a title screen from x, y; a \n in it comes back to x on the next line</summary>
public record TitleLineInfo
{
    public string Text { get; init; } = "";
    public int X { get; init; }
    public int Y { get; init; }

    /// <summary>The font; the menu font (LargeFont) when unset</summary>
    public string? Font { get; init; }

    /// <summary>A color name (such as the menus' HIGHLIGHT), #RRGGBB or palette index</summary>
    public string Color { get; init; } = "HIGHLIGHT";
}

/// <summary>Presenter text on a title screen: a VGAGRAPH text, its window and colors (palette indices)</summary>
public record TitleTextInfo
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
public record LoseScreenInfo
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
public record MenuMessageInfo
{
    public string Font { get; init; } = "LargeFont";
    public string Color { get; init; } = "White";
    public string Shadow { get; init; } = "Black";
    public string Hi { get; init; } = "White";
    public string Med { get; init; } = "Grey";
    public string Lo { get; init; } = "Black";
}

/// <summary>A picture drawn with its top left corner at x, y</summary>
public record PicPlacement
{
    public string Pic { get; init; } = "";
    public int X { get; init; }
    public int Y { get; init; }
}

public record SignonInfo
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
public record SignonTextArea
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

public record DefaultMapInfo
{
    // Wolf3D's, for a game-info that doesn't say (a new stand-alone game's)
    public string FloorColor { get; init; } = "#717171";
    public string CeilingColor { get; init; } = "#383838";

    /// <summary>
    /// How many 64 unit stories tall the walls are
    /// </summary>
    public int WallHeight { get; init; } = 1;

    /// <summary>
    /// Graphic (or wall texture) drawn in place of the ceiling color; null for none
    /// </summary>
    public string? Sky { get; init; } = null;

    /// <summary>A star sky above the horizon in place of a sky picture (and behind clouds); null for none</summary>
    public StarSkyInfo? StarSky { get; init; } = null;

    /// <summary>Clouds drifting over the sky (or the ceiling color); null for none</summary>
    public CloudSkyInfo? CloudSky { get; init; } = null;

    /// <summary>
    /// Graphic (or wall texture) drawn below the horizon in place of the floor color, showing
    /// where no floor flat is drawn (Midnight Assault's floor parallax); null for none
    /// </summary>
    public string? FloorSky { get; init; } = null;

    /// <summary>The floor sky drawn upside down, a reflection of a sky in water</summary>
    public bool FloorSkyMirror { get; init; }

    /// <summary>
    /// Texture on every floor tile the flat plane doesn't give one (see mapdefs flats); null
    /// leaves them the floor color
    /// </summary>
    public string? DefaultFloor { get; init; } = null;

    /// <summary>
    /// Texture on every ceiling tile the flat plane doesn't give one; null leaves them the
    /// ceiling color (or the sky). A sky is behind ceiling flats, so this covers it but for see-through pixels.
    /// </summary>
    public string? DefaultCeiling { get; init; } = null;

    /// <summary>
    /// Rain or snow falling on every level: a name in weather-types (or a built-in one: rain,
    /// heavy-rain, snow, blizzard); null or none for clear weather
    /// </summary>
    public string? Weather { get; init; } = null;

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

/// <summary>A line of game-info level-start-text: its text (a $language key or the text; %m the level's name), font and color</summary>
public record LevelStartLine
{
    public string Text { get; init; } = "";
    public string Font { get; init; } = "SmallFont";

    /// <summary>A color name from colors.yaml, #RRGGBB or a palette index</summary>
    public string Color { get; init; } = "White";

    /// <summary>How far down it is, in 320x200 pixels; left out, under the line before (the first 2 down)</summary>
    public int? Y { get; init; }
}

/// <summary>
/// A night sky of stars, drawn above the horizon in place of a sky picture (Wolf4SDL's
/// wl_atmos DrawStarSky, End of Destiny's sky levels): stars all around, nearer ones brighter,
/// turning with the view, on black; and a moon in one direction
/// </summary>
public record StarSkyInfo
{
    /// <summary>How many stars</summary>
    public int Stars { get; init; } = 500;

    /// <summary>The brightest star's palette index; fainter ones are the next 15 (Wolf3D's white to dark grey)</summary>
    public int Color { get; init; } = 15;

    /// <summary>The moon, in the direction of moon-angle (degrees, 0 east, counterclockwise); false for none</summary>
    public bool Moon { get; init; } = true;

    public double MoonAngle { get; init; } = 45;

    /// <summary>What's behind the stars: a color name, #RRGGBB or palette index</summary>
    public string Background { get; init; } = "0";
}

/// <summary>
/// Clouds drifting high over the level (Wolf4SDL's wl_cloudsky, End of Destiny's sky levels):
/// a cloud picture made from a seed, as wl_cloudsky makes it, on a plane four stories above the
/// eye, moving the way angle says at speed. Drawn over a star sky (or the ceiling color), which
/// shows through where its colors say none.
/// </summary>
public record CloudSkyInfo
{
    /// <summary>The cloud picture's seed (wl_cloudsky's: any number makes its own clouds)</summary>
    public uint Seed { get; init; } = 626;

    /// <summary>How fast they move, in global units a tic (65536 a tile)</summary>
    public int Speed { get; init; } = 100;

    /// <summary>Which way they move, in degrees</summary>
    public int Angle { get; init; } = 200;

    /// <summary>
    /// Palette indices the cloud picture's values map to, darkest cloud first in wl_cloudsky's
    /// order; "none" leaves that much of it see-through. End of Destiny's: 31, 30, 29, 28, then
    /// three see-through.
    /// </summary>
    public List<string> Colors { get; init; } = ["31", "30", "29", "28", "none", "none", "none"];
}

/// <summary>
/// Rain or snow: particles falling all around the view, but not under a ceiling flat or an
/// arch, nor inside walls. Speeds are in texels a tic (64 a story, 70 tics a second).
/// </summary>
public record WeatherInfo
{
    /// <summary>The kinds every game has, which game-info weather-types can add to or replace</summary>
    public static IReadOnlyDictionary<string, WeatherInfo> BuiltIn { get; } = new Dictionary<string, WeatherInfo>(StringComparer.OrdinalIgnoreCase)
    {
        ["rain"] = new() { Kind = "rain", Count = 500, FallSpeed = 7, Length = 12, Color = "#A0A8B8" },
        ["heavy-rain"] = new() { Kind = "rain", Count = 1500, FallSpeed = 9, WindX = 1.5, Gust = 0.6, Length = 20, Color = "#A8B0C0" },
        ["snow"] = new() { Kind = "snow", Count = 800, FallSpeed = 0.7, Size = 1.5, Drift = 6, Color = "#E8E8F0" },
        ["blizzard"] = new() { Kind = "snow", Count = 4000, FallSpeed = 1.4, WindX = 5, WindY = 1.5, Gust = 0.7, GustTics = 200, Size = 1.5, Drift = 4,
            Color = "#F0F0F8", Distance = 6 },
    };

    /// <summary>What "none" is called: no weather, in place of the default map's</summary>
    public const string None = "none";

    /// <summary>The kinds a game's levels can name: the built-in ones, with its game-info's weather-types over them</summary>
    public static Dictionary<string, WeatherInfo> TypesOf(GameInfoAsset? gameInfo)
    {
        var types = new Dictionary<string, WeatherInfo>(BuiltIn, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, info) in gameInfo?.WeatherTypes ?? [])
            types[name] = info;
        return types;
    }

    /// <summary>rain (streaks) or snow (flakes)</summary>
    public string Kind { get; init; } = "rain";

    /// <summary>How many particles fall in the box around the view (2 x distance tiles across)</summary>
    public int Count { get; init; } = 600;

    /// <summary>How fast they fall</summary>
    public double FallSpeed { get; init; } = 6;

    /// <summary>How fast the wind carries them east (negative: west)</summary>
    public double WindX { get; init; }

    /// <summary>How fast the wind carries them south (negative: north)</summary>
    public double WindY { get; init; }

    /// <summary>How much the wind rises and falls, as a share of it: 0 steady, 1 from still to twice as strong</summary>
    public double Gust { get; init; }

    /// <summary>How long a gust takes to come and go, in tics</summary>
    public int GustTics { get; init; } = 280;

    /// <summary>Rain only: whether drops splash where they land on open floor</summary>
    public bool Splashes { get; init; } = true;

    /// <summary>Rain only: how long a streak is, in texels</summary>
    public double Length { get; init; } = 12;

    /// <summary>Snow only: how big a flake is, in texels</summary>
    public double Size { get; init; } = 1.5;

    /// <summary>Snow only: how far a flake sways from side to side, in texels</summary>
    public double Drift { get; init; }

    /// <summary>A color name, #RRGGBB or palette index; dimmed with distance</summary>
    public string Color { get; init; } = "#B0B8C8";

    /// <summary>How far away, in tiles, they're drawn</summary>
    public int Distance { get; init; } = 8;

    /// <summary>
    /// The amount falling changes as the level's played (End of Destiny's variable weather): it
    /// starts with none (40 in 100), some (20 in 100) or all of count (40 in 100); every
    /// vary-every seconds there's a vary-chance it starts to thicken or thin (whichever it can,
    /// else either), by vary-rate of count a second, until it's all or none
    /// </summary>
    public bool Vary { get; init; }

    /// <summary>Seconds between the chances for the amount to start changing</summary>
    public double VaryEvery { get; init; } = 20;

    /// <summary>The chance each time, 0 to 1</summary>
    public double VaryChance { get; init; } = 0.2;

    /// <summary>How fast it changes, as a share of count a second</summary>
    public double VaryRate { get; init; } = 0.02;
}

/// <summary>
/// How the tiles of a light zone (those with its id on the zone plane, plane 5) are lit, in
/// place of the level's light. Distance still fades them as the level's shading says.
/// </summary>
public record ZoneInfo
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

    /// <summary>
    /// A sound (sound-seq name) looped, unplaced, while the player is in the zone: rain, crickets,
    /// a train (Gameplay/ZoneAmbience.cs). "none" is silence; a zone without one leaves whatever's
    /// playing as it is.
    /// </summary>
    public string? Ambient { get; init; }

    /// <summary>Played in place of ambient while the player's in disguise</summary>
    public string? AmbientDisguised { get; init; }

    /// <summary>
    /// Played in place of ambient while no enemy has noticed the player yet on the level
    /// (Gameplay/Stealth.cs: Midnight Assault's piano bar, its stealthActive); in disguise,
    /// ambient-disguised comes first
    /// </summary>
    public string? AmbientUndetected { get; init; }

    /// <summary>
    /// Whether it says anything about light. A zone with only ambient sound isn't a light zone:
    /// its tiles keep the level's light.
    /// </summary>
    public bool HasLight => Light != null || Color != null || Effect != null || Low != null || Tics != null || BrightTics != null;
}

/// <summary>
/// How the textures of the tiles with its tag (the tag plane, plane 4) slide along, as Midnight
/// Assault's scrolling walls and flats: conveyor belts, waterfalls, moving water. Speeds are in
/// texels a tic (64 a tile, 70 tics a second); negative goes the other way. Switches and actor
/// states start, stop and re-speed it (A_StartScroll, A_StopScroll, A_ToggleScroll, A_SetScroll).
/// </summary>
public record ScrollerInfo
{
    /// <summary>On a wall face, to the right as you look at it; on a floor or ceiling, east</summary>
    public double X { get; init; }

    /// <summary>On a wall face, down; on a floor or ceiling, south</summary>
    public double Y { get; init; }

    /// <summary>Whether the faces of tagged walls scroll (not door jambs, doors or wall patches)</summary>
    public bool Walls { get; init; } = true;

    /// <summary>Whether the floor flats of tagged tiles scroll</summary>
    public bool Floor { get; init; } = true;

    /// <summary>Whether the ceiling flats of tagged tiles scroll</summary>
    public bool Ceiling { get; init; } = true;

    /// <summary>Whether it's scrolling as the level starts; false waits for a switch</summary>
    public bool Active { get; init; } = true;
}

/// <summary>
/// How a level is shaded: everything is dimmed to its light, then fades toward the fade color
/// with distance. Each value left out comes from the default map's shading, else the default
/// here. A level with neither map nor default map shading isn't shaded.
/// </summary>
public record ShadingInfo
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
public record SkillInfo
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

/// <summary>
/// game-info `article`: the page every article is laid out on (320x200). The screen is filled
/// with background, then the frame's pictures are drawn in order, then the text goes between the
/// margins, in lines of 10 pixels, and the page number last. Each value left out is Wolf3D's.
/// </summary>
public record ArticleInfo
{
    public const int ScreenWidth = 320, ScreenHeight = 200, FontHeight = 10;

    /// <summary>The color the screen is filled with first (a colors.yaml name)</summary>
    public string Background { get; init; } = "BACKCOLOR";

    /// <summary>Pictures drawn on every page, before the text: Wolf3D's help window by default, or one full-screen picture</summary>
    public List<PicPlacement> Frame { get; init; } =
    [
        new() { Pic = "h_topwindow", X = 0, Y = 0 },
        new() { Pic = "h_leftwindow", X = 0, Y = 8 },
        new() { Pic = "h_rightwindow", X = 312, Y = 8 },
        new() { Pic = "h_bottominfo", X = 8, Y = 176 },
    ];

    /// <summary>Pixels kept clear of text at each edge</summary>
    public ArticleMargins Margins { get; init; } = new();

    /// <summary>The text's color until a ^C changes it (a colors.yaml name)</summary>
    public string TextColor { get; init; } = "Black";

    /// <summary>"pg 1 of 2": where, in what color, and how it's written ({0} the page, {1} how many)</summary>
    public ArticlePageNumber PageNumber { get; init; } = new();

    /// <summary>The lines of text a page has between its margins</summary>
    public int TextRows => Math.Max((ScreenHeight - Margins.Top - Margins.Bottom) / FontHeight, 1);
}

public record ArticleMargins
{
    public int Top { get; init; } = 16;
    public int Bottom { get; init; } = 32;
    public int Left { get; init; } = 16;
    public int Right { get; init; } = 16;
}

public record ArticlePageNumber
{
    /// <summary>false leaves it out</summary>
    public bool Show { get; init; } = true;
    public int X { get; init; } = 213;
    public int Y { get; init; } = 183;
    public string Color { get; init; } = "Dark Yellow";
    public string Format { get; init; } = "pg {0} of {1}";

    public string Text(int page, int pages) => Format.Replace("{0}", page.ToString()).Replace("{1}", pages.ToString());
}

/// <summary>A class in game-info's player-classes: how the class menu shows it</summary>
public record PlayerClassInfo
{
    public string? Name { get; init; }
    public string? PicName { get; init; }
}

public record EpisodeInfo
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
    /// "starts-locked": the episode is greyed out, and can't be picked, until the player unlocks
    /// it (its key, by A_Unlock, Lua game.unlock or the unlock command), from then on in every
    /// game (progress.cfg: Gameplay/Unlocks.cs)
    /// </summary>
    public bool StartsLocked { get; init; }

    /// <summary>"locked-name": shown in place of the name while it's locked (text or $LANGUAGE key)</summary>
    public string? LockedName { get; init; }

    /// <summary>
    /// "level-select": the maps the player can start the episode from, picked on a "Choose
    /// Level" menu after this one (menudefs new-level) in this order; none starts at start-map.
    /// A map that starts-locked is greyed out (as its locked-name) until its name is unlocked.
    /// </summary>
    public List<string> LevelSelect { get; init; } = [];

    /// <summary>
    /// "level-select-after": the level menu only shows once this name is unlocked (Midnight
    /// Assault's campaign, once it's been finished); until then the episode starts at start-map
    /// </summary>
    public string? LevelSelectAfter { get; init; }

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
public record ClusterInfo
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
    /// The end-text comes after the end-screens rather than before them (End of Destiny's
    /// EndSpear: the pictures, then ENDART1)
    /// </summary>
    public bool EndTextLast { get; init; }

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

public record VictoryFrameInfo
{
    public string Pic { get; init; } = null!;
    public int X { get; init; }
    public int Y { get; init; }

    /// <summary>
    /// How long the frame stays up (70 a second)
    /// </summary>
    public int Tics { get; init; }
}

public record EndScreenInfo
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

/// <summary>
/// A map's `loadout:` (Gameplay/MapLoadouts.cs), applied in this order: the weapons taken, the
/// items given, then the health set and capped
/// </summary>
public record MapLoadout
{
    /// <summary>
    /// Every weapon taken, with the ammo the weapons fire (but not what's loaded in a weapon's
    /// magazine, which waits for the weapon to be picked up again, nor an Ammo item no weapon
    /// fires, as Midnight Assault's medkits: the original's RemoveAllWeapons)
    /// </summary>
    public bool TakeWeapons { get; init; }

    /// <summary>Items given (class: amount), weapons included</summary>
    public Dictionary<string, int> Give { get; init; } = new();

    /// <summary>The player's health set to this (P.O.W.'s 1); null leaves it</summary>
    public int? Health { get; init; }

    /// <summary>The player's health brought down to this if it's more (Hidden Tunnels' 50); null for no cap</summary>
    public int? HealthCap { get; init; }
}

/// <summary>
/// A level objective (game-info `objectives:`). Its messages are text or $language keys, each %d
/// filled in turn: <see cref="Start"/> the total, <see cref="Progress"/> and <see cref="Stats"/>
/// done and total, <see cref="Refused"/> those left. None shows nothing.
/// </summary>
public record ObjectiveInfo
{
    /// <summary>Whether the level's exits (A_Exit, A_SecretExit) are shut until it's all done</summary>
    public bool BlocksExit { get; init; }

    /// <summary>Shown as a level with any to do starts (not a saved game loaded)</summary>
    public string? Start { get; init; }

    /// <summary>The fewest there must be for <see cref="Start"/> to be shown (the original's goal enemies: 2)</summary>
    public int StartFrom { get; init; } = 1;

    /// <summary>Shown as one more is done</summary>
    public string? Progress { get; init; }

    /// <summary>Shown as the last is done, after <see cref="Progress"/></summary>
    public string? Done { get; init; }

    /// <summary>Shown when an exit's refused while some are left</summary>
    public string? Refused { get; init; }

    /// <summary>A line in the automap's stats, while the level has any</summary>
    public string? Stats { get; init; }
}

/// <summary>Game-info `stealth:` (Gameplay/Stealth.cs). Messages are text or $language keys; none shows nothing.</summary>
public record StealthInfo
{
    /// <summary>An enemy has started to suspect the disguised player</summary>
    public string? Suspected { get; init; }

    /// <summary>Noticed in disguise: the disguise is lost</summary>
    public string? Blown { get; init; }

    /// <summary>Noticed out of disguise after killing unseen</summary>
    public string? Lost { get; init; }

    /// <summary>Noticed on a stealth-only map: the mission's failed</summary>
    public string? Caught { get; init; }

    /// <summary>A disguise refused (A_Disguise) while enemies are after the player</summary>
    public string? Alert { get; init; }

    /// <summary>Tics a suspicious enemy gives the player to show papers (the original's 10 seconds)</summary>
    public int SuspicionTics { get; init; } = 700;

    /// <summary>Tiles away (either way) past which a disguised player is ignored</summary>
    public int Range { get; init; } = 10;

    /// <summary>
    /// A level finished unseen without killing anyone, of some there, counts every kill: its kill
    /// ratio shows 100% at the end of the tally and its record has them all (Midnight Assault's
    /// "Infiltration successful!")
    /// </summary>
    public bool UnseenNoKillsCountsAll { get; init; }
}

/// <summary>
/// Game-info `kill-chain:` (Gameplay/KillChains.cs). Messages are text or $language keys. Each
/// prize is an item class dropped where the kill was, "Item if Weapon" only while the player
/// holds that weapon (nothing otherwise), or "heal N" to heal the player N points.
/// </summary>
public record KillChainInfo
{
    /// <summary>Tics after a kill within which the next one carries the chain on (Midnight Assault's 50); 0 for no chains</summary>
    public int Tics { get; init; }

    /// <summary>Shown from a chain of two kills on, one each; the last for any longer chain</summary>
    public List<string> Messages { get; init; } = [];

    /// <summary>From a chain of two kills on, one each, the last for any longer: a prize picked at random from it</summary>
    public List<List<string>> Prizes { get; init; } = [];

    /// <summary>A kill made unseen (Gameplay/Stealth.cs) gives the prize of a chain of 1 (none) up to this many, at random; 0 for none</summary>
    public int StealthPrize { get; init; }
}

/// <summary>
/// Horde (multiplayer): waves of enemies coming at the players, who win by outlasting the
/// lobby's number of waves (or play on until they all die at once). Each wave sends its
/// enemies, a few at a time, from the map's horde spawns (mapdefs) or, without any, open floor
/// out of the players' way; once they're all dead a break follows, when the dead come back.
/// </summary>
public record HordeInfo
{
    /// <summary>
    /// The waves, first to last: each the enemies it sends (class: how many). Past the last,
    /// each wave is the last one again with <see cref="Grow"/> more of each class per wave.
    /// More players bring more: half as many again for each one past the first.
    /// </summary>
    public List<HordeWave> Waves { get; init; } = [];

    /// <summary>How many more of each class every wave past the listed ones brings</summary>
    public int Grow { get; init; } = 2;

    /// <summary>The break between waves (seconds), when dead players can come back</summary>
    public int BreakSeconds { get; init; } = 10;

    /// <summary>Tics between one enemy coming in and the next</summary>
    public int SpawnTics { get; init; } = 35;

    /// <summary>The most of a wave's enemies alive at once; the rest wait their turn</summary>
    public int MaxAlive { get; init; } = 12;
}

/// <summary>A Horde wave: the enemies it sends, by actordefs class</summary>
public record HordeWave
{
    public Dictionary<string, int> Enemies { get; init; } = [];
}

public record MapInfo
{
    /// <summary>
    /// Name shown for the level (e.g. on saves), or a $language key; when unset it's
    /// "Episode X, Floor Y" (just "Floor Y" in a single-episode game)
    /// </summary>
    public string? Name { get; init; }

    /// <summary>"select-name": the level's row on an episode's level select menu; its name when unset</summary>
    public string? SelectName { get; init; }

    /// <summary>
    /// "starts-locked": on an episode's level select menu, the level is greyed out and can't be
    /// picked until its name is unlocked (A_Unlock, Lua game.unlock or the unlock command)
    /// </summary>
    public bool StartsLocked { get; init; }

    /// <summary>"locked-name": the level's row while it's locked (text or $LANGUAGE key)</summary>
    public string? LockedName { get; init; }

    /// <summary>
    /// Who made the level (or a $language key), for crediting a map pack's authors: the
    /// level-end screen shows it where intermission.yaml has an author value. Optional.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// A deathmatch arena: picked by name in the multiplayer lobby. It's in no episode, so it's
    /// only played alone by warping to it (the map command), to try it out
    /// </summary>
    public bool Deathmatch { get; init; }

    /// <summary>
    /// The multiplayer modes the map is an arena for, by id (deathmatch, team-deathmatch, ctf,
    /// heist, domination, king-of-the-hill, horde, prop-hunt). Left out, a `deathmatch: true`
    /// arena is one for every mode but heist, which needs treasure placed for it: the others find
    /// their flags, points and spawns on any map when it has none placed.
    /// </summary>
    public List<string> Modes { get; init; } = [];

    /// <summary>Whether the map is an arena for a multiplayer mode (its id)</summary>
    public bool IsArenaFor(string mode) =>
        Modes.Count > 0
            ? Modes.Contains(mode, StringComparer.OrdinalIgnoreCase)
            : Deathmatch && mode is "deathmatch" or "team-deathmatch" or "ctf" or "domination" or "king-of-the-hill" or "horde" or "prop-hunt";

    //public string Current { get; set; }
    public string Next { get; init; } = null!;
    public string? SecretNext { get; init; } = null;

    /// <summary>
    /// The entrance the next level (or the secret one) is entered by: its mapdefs player start
    /// with that entrance, and its entrance-briefings' briefing (Midnight Assault's Laboratories
    /// leading into Base by its other way in). Null: the level's own start.
    /// </summary>
    public string? NextEntrance { get; init; } = null;
    public string? SecretNextEntrance { get; init; } = null;

    /// <summary>The briefing shown in place of <see cref="Briefing"/> when the level is entered by an entrance, by its name</summary>
    public Dictionary<string, string> EntranceBriefings { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Text (or a $language key) that makes leaving this level end the game, as the Spear of
    /// Destiny demo's last floor does: the intermission, then this message, then the high scores.
    /// The game isn't won, so there's no victory.
    /// </summary>
    public string? EndMessage { get; init; } = null;

    /// <summary>
    /// Leaving this level ends the game, though it isn't won: its intermission and debriefing,
    /// then the high scores, with no victory (Midnight Assault's Dog Tag, Challenge and
    /// Completionist missions, each a game of its own). An end-message does too.
    /// </summary>
    public bool EndsGame { get; init; }

    /// <summary>
    /// An article (a text, in Wolf3D's help text format: ^P pages, ^C colors, ^G pictures) shown
    /// before the level, as Midnight Assault's mission briefings were: when it's entered afresh,
    /// not when it's loaded, returned to or restarted after dying, nor in a demo or a game with
    /// others
    /// </summary>
    public string? Briefing { get; init; } = null;

    /// <summary>An article shown once the level's won: after its level-end screen, or before the victory tally of a cluster it ends</summary>
    public string? Debriefing { get; init; } = null;

    /// <summary>
    /// The pictures the level-end screen's BJ (intermission.yaml bj) breathes through for this
    /// level, in place of his own, at his place and pace: Midnight Assault's three photos of each
    /// level (LevelPhotoAnimate)
    /// </summary>
    public List<string> IntermissionPics { get; init; } = [];

    public int FloorNumber { get; init; }
    public int ParTime { get; init; } = 0;
    public string Music { get; init; } = null!;
    public short Cluster { get; init; }

    public string? FloorColor { get; init; } = null;
    public string? CeilingColor { get; init; } = null;

    /// <summary>
    /// The second pair of colors, swapped with the first by A_Warp's "swapcolors" (End of
    /// Destiny's teleporters, levelinfo floor2/ceiling2): each warp swaps them back and forth.
    /// Left out, the swap keeps the first one.
    /// </summary>
    public string? AltFloorColor { get; init; } = null;
    public string? AltCeilingColor { get; init; } = null;

    /// <summary>
    /// How many 64 unit stories tall the walls are; null uses the default map's
    /// </summary>
    public int? WallHeight { get; init; } = null;

    /// <summary>
    /// Graphic (or wall texture) drawn in place of the ceiling color; null uses the default map's
    /// </summary>
    public string? Sky { get; init; } = null;

    /// <summary>A star sky above the horizon in place of a sky picture (and behind clouds); null for none</summary>
    public StarSkyInfo? StarSky { get; init; } = null;

    /// <summary>Clouds drifting over the sky (or the ceiling color); null for none</summary>
    public CloudSkyInfo? CloudSky { get; init; } = null;

    /// <summary>
    /// Graphic (or wall texture) drawn below the horizon in place of the floor color, showing
    /// where no floor flat is drawn: the land below a bridge, water under the shore (Midnight
    /// Assault's floor parallax). It runs down from the horizon, turning with the view as the sky
    /// does; with floor-sky-mirror it's drawn upside down from the horizon, a reflection. Null
    /// uses the default map's ("none" for none).
    /// </summary>
    public string? FloorSky { get; init; } = null;

    /// <summary>The floor sky drawn upside down from the horizon (a sky reflected in water)</summary>
    public bool FloorSkyMirror { get; init; }

    /// <summary>
    /// Texture for floor tiles the flat plane doesn't give one; null uses the default map's
    /// </summary>
    public string? DefaultFloor { get; init; } = null;

    /// <summary>
    /// Texture for ceiling tiles the flat plane doesn't give one; null uses the default map's
    /// </summary>
    public string? DefaultCeiling { get; init; } = null;

    /// <summary>
    /// Rain or snow (a weather-types name); null uses the default map's, none for clear weather
    /// </summary>
    public string? Weather { get; init; } = null;

    /// <summary>
    /// The view rumbles through the level, as Midnight Assault's train chase did: a quake of up to
    /// this power (0-200; the original's was 10) every 20 to 140 tics. 0 (the default) for none.
    /// </summary>
    public int Shake { get; init; }

    /// <summary>
    /// The level has to be got through unseen (Midnight Assault's Stealth Challenge): the first
    /// enemy to notice the player ends it, as a death, with game-info stealth's caught message
    /// </summary>
    public bool StealthOnly { get; init; }

    /// <summary>
    /// Seconds the level gives the player (Midnight Assault's Timed Challenge: 600), counted on
    /// the level's own clock, so it starts again with the level (after a death too) and a saved
    /// game keeps what's left. When it's up the player dies. statusbar.yaml's level-timer shows
    /// what's left. 0 (the default) for no limit.
    /// </summary>
    public int TimeLimit { get; init; }

    /// <summary>
    /// With a time-limit: the time running out, or the player dying before it does, completes the
    /// level instead (End of Destiny's timed levels): they go on to the next with their health
    /// back to full and no life lost
    /// </summary>
    public bool TimeLimitCompletes { get; init; }

    /// <summary>
    /// The status bar's radar shows on this map, where statusbar.yaml's radar is maps-only
    /// (Midnight Assault's Stealth Challenge, whose map of what's been seen stays on screen)
    /// </summary>
    public bool Radar { get; init; }

    /// <summary>
    /// What the player has as the level starts afresh (a new game, the level before, a death),
    /// not a saved game loaded: Midnight Assault's SetupMapSpecialFeatures. Null: what they had.
    /// </summary>
    public MapLoadout? Loadout { get; init; }

    /// <summary>
    /// Distance shading; each value left out uses the default map's (see ShadingInfo)
    /// </summary>
    public ShadingInfo? Shading { get; init; } = null;

    /// <summary>
    /// Light zones by zone id; each value left out uses the default map's zone of that id (see ZoneInfo)
    /// </summary>
    public Dictionary<int, ZoneInfo>? Zones { get; init; } = null;

    /// <summary>Scrolling walls and flats by tag (the tag plane's value; see ScrollerInfo)</summary>
    public Dictionary<int, ScrollerInfo>? Scrollers { get; init; } = null;
}