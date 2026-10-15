using Microsoft.Extensions.DependencyInjection;
using SDL2;
using System;
using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Constants;
using PFWolf.Extensions;
using PFWolf.Fonts;
using PFWolf.Loaders;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    private static VideoManager _videoManager;
    internal static AudioManager _audioManager;
    private static InputManager _inputManager;
    internal static GameEngineManager _gameEngineManager;
    private static GraphicManager _graphicManager;
    private static FontManager _fontManager;
    internal static MapManager _mapManager;
    internal static AssetManager _assetManager;
    internal static InventoryManager _inventoryManager;
    private static ConsoleManager _consoleManager;
    private static AutomapManager _automapManager;
    internal static HudMessageManager _hudMessageManager;
    internal static FactionManager _factionManager;
    internal static TranslationManager _translationManager;

    // Who's playing, the acting player, tics, gamestate and the camera (Gameplay/GameSession.cs)
    internal static readonly GameSession session = new();

    /*
    =============================================================================

                        TEXT, WINDOWS, LINE INPUT, RANDOM NUMBERS

    What ID_US gave the rest of the game, now in Ui/ (text and line input), Gameplay/RandomTable.cs
    and Gameplay/HighScore.cs; these forward to them.

    =============================================================================
    */

    static UiText ui = null!;               // made in InitServices
    static LineInput lineInput = null!;

    internal const string SMALL_FONT = UiFonts.SMALL_FONT;
    internal const string LARGE_FONT = UiFonts.LARGE_FONT;
    internal const string MENU_FONT = UiFonts.MENU_FONT;
    internal static TextStyle MenuStyle(string color) => UiFonts.MenuStyle(color);

    internal const int MaxX = 320;
    internal const int MaxY = 200;
    internal const int MaxHelpLines = 500;
    internal const int MaxHighName = HighScore.MaxHighName;
    internal const int LegacyScoreCount = HighScore.LegacyScoreCount;
    internal const int MaxGameName = 32;    // a save's name, as typed in the save menu, is one less
    internal const int MaxString = LineInput.MaxString;

    internal static TextWindow TextAt(int x, int y, TextStyle style) => ui.TextAt(x, y, style);
    internal static TextWindow CenteredText(int x, int width, int y, TextStyle style) => ui.CenteredText(x, width, y, style);
    internal static void MeasureText(string text, string font, out int width, out int height) => ui.MeasureText(text, font, out width, out height);
    internal static int TextWidth(string text, string font) => ui.TextWidth(text, font);
    internal static string FitText(string text, int width, string font) => ui.FitText(text, width, font);
    internal static TextWindow US_DrawWindow(int x, int y, int w, int h, TextStyle style) => ui.DrawWindow(x, y, w, h, style);
    internal static void USL_XORICursor(int x, int y, string s, int cursor, TextStyle style) => ui.XORICursor(x, y, s, cursor, style);
    internal static bool US_LineInput(int x, int y, ref string buf, string def, bool escok, int maxchars, int maxwidth, TextStyle style)
        => lineInput.Run(x, y, ref buf, def, escok, maxchars, maxwidth, style);
    internal static char USL_RotateChar(char ch, int dir) => LineInput.RotateChar(ch, dir);

    // The high score table: Wolf3D's, until InitHighScores puts in the game pack's
    internal static HighScore[] Scores = HighScore.WolfDefaults();
    internal static int MaxScores => Scores.Length;
    internal static void InitHighScores() => Scores = HighScore.TableFor(_gameEngineManager.GetGameInfo().HighScores, Scores);

    internal static int rndindex { get => session.random.rndindex; set => session.random.rndindex = value; }
    internal static void US_InitRndT(bool randomize) => session.random.InitRndT(randomize);
    internal static int US_RndT() => session.random.RndT();

    // Everything written to stdout from the start until the signon screen is done
    private static StartupOutput? startupOutput;

    // The signon screen's startup info (Ui/SignonPanel.cs)
    static SignonPanel signonPanel = null!;   // made in InitServices
    private static void PrintSignonInfo() => signonPanel.PrintSignonInfo();
    private static void FinishSignonInfo(List<string> startupLines) => signonPanel.FinishSignonInfo(startupLines);

    private static void CaptureStartupOutput() => startupOutput = StartupOutput.Capture();

    /// <summary>
    /// Stops keeping stdout and returns what startup wrote to it, which the console's
    /// scrollback starts with
    /// </summary>
    private static List<string> StopCapturingStartupOutput()
    {
        var lines = startupOutput?.Stop() ?? [];
        startupOutput = null;
        _consoleManager.AddToScrollback(lines);
        return lines;
    }

    // Blake Stone's text presenter and Wolf3D's articles (Ui/TextPresenter.cs, Ui/ArticleReader.cs,
    // made in InitServices)
    static TextPresenter textPresenter = null!;
    static ArticleReader articleReader = null!;
    internal static string? PresenterScript(string textName) => textPresenter.PresenterScript(textName);
    internal static void TP_Presenter(PresenterInfo pi) => textPresenter.TP_Presenter(pi);
    internal static Fonts.Font? PresenterFont(int number) => textPresenter.PresenterFont(number);
    internal static void PresenterMessageBox(string script) => textPresenter.PresenterMessageBox(script);
    internal static bool ShowBriefing(string textName) => textPresenter.ShowBriefing(textName);
    internal static void ShowLoseScreen(LoseScreenInfo lose) => textPresenter.ShowLoseScreen(lose);
    internal static void HelpScreens() => articleReader.HelpScreens();
    internal static void EndText() => articleReader.EndText();
    internal static void ShowArticle(string article) => articleReader.ShowArticle(article);

    /// <summary>
    /// A menu item's pages of presenter text (Blake Stone's instructions, story and ordering
    /// info) in the briefing window, with its pages-info-line and pages-music, until Esc
    /// </summary>
    internal static int CP_TextScreen(string textName)
    {
        textPresenter.ShowTextPages(textName, fromMenu: true);
        StartCPMusic(MENUSONG);
        return 0;
    }
    // The high scores, and the level-end and victory screens (Ui/HighScoresScreen.cs,
    // Ui/IntermissionScreen.cs, made in InitServices)
    static HighScoresScreen highScoresScreen = null!;
    static IntermissionScreen intermissionScreen = null!;
    internal static Dictionary<string, LRstruct> LevelRatios { get => session.LevelRatios; set => session.LevelRatios = value; }
    internal static void DrawHighScores() => highScoresScreen.DrawHighScores();
    internal static void CheckHighScore(int score, bool won) => highScoresScreen.CheckHighScore(score, won);
    internal static void LevelCompleted() => intermissionScreen.LevelCompleted();
    internal static ClusterInfo WonCluster() => intermissionScreen.WonCluster();
    internal static Entities.Color VictoryFadeColor(ClusterInfo cluster) => IntermissionScreen.VictoryFadeColor(cluster);
    internal static void Victory() => intermissionScreen.Victory();
    static IntermissionAsset Intermission => intermissionScreen.Intermission;           // the death cam's text uses these too
    static TextStyle IntermissionTextStyle => intermissionScreen.IntermissionTextStyle;
    internal static void NonShareware() => titleScreens.NonShareware();
    internal static void PG13() => titleScreens.PG13();

    static IntermissionHooks MakeIntermissionHooks() => new(
        ViewColorBorder: () =>
        {
            if (bordercol != "VIEWCOLOR")
                DrawStatusBorder("VIEWCOLOR");
        },
        GiveEveryone: bonus =>
        {
            // Everyone's bonus, with others: each machine gives each player theirs alike
            foreach (var p in players)
            {
                using var _ = ActAs(p);
                GivePoints(bonus);
            }
        },
        DrawScore: DrawScore,
        DrawPlayBorder: DrawPlayBorder,
        GameWon: () => { FindMenuItem(MainMenu, "savegame")?.active = 0; },
        EndText: EndText);

    // game-info's intro and title-loop screens, and Blake Stone's movies (Ui/TitleScreens.cs,
    // Ui/MoviePlayer.cs)
    static MoviePlayer moviePlayer = null!;     // made in InitServices
    static TitleScreens titleScreens = null!;
    internal static bool RunTitleScreens(List<TitleScreenInfo> screens, bool titleLoop) => titleScreens.RunTitleScreens(screens, titleLoop);
    static int TakeNextTitleDemo() => titleScreens.TakeNextTitleDemo();
    internal static bool PlayMovie(string name) => moviePlayer.PlayMovie(name);

    /// <summary>A title screen's presenter text, printed into its window</summary>
    static void DrawTitleText(TitleTextInfo text)
    {
        if (PresenterScript(text.Script) is not { } script)
            return;

        TP_Presenter(new PresenterInfo
        {
            Flags = PresenterFlags.CacheNoGfx,
            Script = script,
            X1 = text.X1,
            Y1 = text.Y1,
            X2 = text.X2,
            Y2 = text.Y2,
            Font = text.Font,
            FontColor = text.Color,
            Background = text.Background,
            Light = text.Light,
            Dark = text.Dark,
            Shadow = text.Shadow,
        });
    }

    static bool US_Started;

    internal static void US_Startup()
    {
        if (US_Started)
            return;

        US_InitRndT(true);

        US_Started = true;
    }

    /// <summary>Builds the managers (Startup/GameServices.cs) and hooks them up</summary>
    private static void InitServices()
    {
        var serviceProvider = GameServices.Build();

        _gameEngineManager = serviceProvider.GetRequiredService<GameEngineManager>();
        _videoManager = serviceProvider.GetRequiredService<VideoManager>();
        _audioManager = serviceProvider.GetRequiredService<AudioManager>();
        _inputManager = serviceProvider.GetRequiredService<InputManager>();
        _graphicManager = serviceProvider.GetRequiredService<GraphicManager>();
        _fontManager = serviceProvider.GetRequiredService<FontManager>();
        _mapManager = serviceProvider.GetRequiredService<MapManager>();
        _assetManager = serviceProvider.GetRequiredService<AssetManager>();
        _inventoryManager = serviceProvider.GetRequiredService<InventoryManager>();
        _inventoryManager.PlayerClass = () => playerstate.playerclass;
        session.ActingChanged = p => _inventoryManager.SetHolder(p.Items);
        session.GameModeChanged = mode => _inventoryManager.ShareKeys = mode == GameMode.Coop;
        SetActing(playerstate);         // the inventory reads the acting player's items
        _consoleManager = serviceProvider.GetRequiredService<ConsoleManager>();
        _automapManager = serviceProvider.GetRequiredService<AutomapManager>();
        _hudMessageManager = serviceProvider.GetRequiredService<HudMessageManager>();
        _factionManager = serviceProvider.GetRequiredService<FactionManager>();
        _translationManager = serviceProvider.GetRequiredService<TranslationManager>();

        ui = new UiText(_graphicManager, _fontManager);
        menuInput = new MenuInput(_inputManager, _audioManager, _gameEngineManager, () => mouseenabled, () => joystickenabled);
        menuDrawing = new MenuDrawing(_graphicManager, _videoManager, _assetManager, _fontManager, _gameEngineManager, ui, () => gameInfoSettings);
        menuLoop = new MenuLoop(menuDrawing, menuInput, _graphicManager, _videoManager, _inputManager, _audioManager, _assetManager);
        lineInput = new LineInput(_graphicManager, _inputManager, _videoManager, ui, menuInput.ReadAnyControl);
        doors = new Doors(_gameEngineManager, _audioManager, _mapManager, _inventoryManager, _hudMessageManager, session,
            new DoorHooks(PlaySoundLocTile, DrawKeys));
        pushWalls = new PushWalls(_mapManager, _audioManager, session, () => demoplayback || demorecord);
        wallSpriteBlocking = new WallSpriteBlocking(_mapManager);
        lineOfSight = new LineOfSight(_mapManager, doors, session, WallSpriteBlocksLine);
        switchActions = MakeSwitchActions();
        smartSwitches = MakeSmartSwitches();
        hubs = new Hubs(_gameEngineManager, _mapManager, session);
        projectileActions = MakeProjectileActions();
        lighting = MakeLighting();
        actorLights = MakeActorLights();
        playerView = MakePlayerView();
        statusBarDrawing = MakeStatusBarDrawing();
        deathmatch = MakeDeathmatch();
        playerClassRules = new PlayerClassRules(_gameEngineManager, _inventoryManager, session);
        playerWeapons = MakePlayerWeapons();
        playerStats = MakePlayerStats();
        playerUse = MakePlayerUse();
        playerMovement = MakePlayerMovement();
        playerAttacks = MakePlayerAttacks();
        projectiles = MakeProjectiles();
        actorSounds = new ActorSounds(_mapManager, _audioManager, session, PlaySoundLocActor);
        bjVictory = new BJVictory(_mapManager, session, state => playstate = state);
        missiles = MakeMissiles();
        weaponInHand = MakeWeaponInHand();
        pickups = MakePickups();
        playerThink = MakePlayerThink();
        playersManager = MakePlayers();
        scoreboard = MakeScoreboard();
        netPlay = MakeNetPlay();
        netOverlay = MakeNetOverlay();
        gameScripts = MakeGameScripts();
        menuDefs = new MenuDefs(_assetManager, _gameEngineManager, _graphicManager, _audioManager, ui);
        RegisterMenuDefs();
        modsMenu = new ModsMenu(_gameEngineManager, _assetManager, _inputManager, _audioManager, _videoManager,
            menuDrawing, menuLoop, menuInput, menuDefs, ui, () => ingame);
        videoMenu = new VideoMenu(_assetManager, _inputManager, _audioManager, _videoManager, menuDrawing, menuLoop, menuInput, menuDefs);
        soundMenu = new SoundMenu(_audioManager, _videoManager, menuDrawing, menuLoop, menuInput, menuDefs);
        controlMenus = new ControlMenus(_assetManager, _inputManager, _audioManager, _videoManager, menuDrawing, menuLoop, menuInput, menuDefs, ui, controlSettings);
        customizeScreen = new CustomizeControlsScreen(_assetManager, _inputManager, _audioManager, _videoManager,
            menuDrawing, menuLoop, menuInput, menuDefs, ui, controls);
        textPresenter = new TextPresenter(_assetManager, _inputManager, _audioManager, _videoManager,
            _graphicManager, _fontManager, menuDrawing, menuInput);
        articleReader = new ArticleReader(_gameEngineManager, _assetManager, _inputManager, _audioManager, _videoManager,
            _graphicManager, menuInput, session, textPresenter);
        highScoresScreen = new HighScoresScreen(_gameEngineManager, _assetManager, _inputManager, _audioManager, _videoManager,
            _graphicManager, menuDrawing, menuDefs, ui, lineInput, session, () => Scores, FloorScore);
        intermissionScreen = new IntermissionScreen(_gameEngineManager, _assetManager, _inputManager, _audioManager, _videoManager,
            _graphicManager, menuDrawing, ui, session, MakeIntermissionHooks());
        loadSaveScreen = new LoadSaveScreen(_gameEngineManager, _assetManager, _inputManager, _audioManager, _videoManager,
            _graphicManager, menuDrawing, menuLoop, menuInput, menuDefs, ui, lineInput, MakeLoadSaveHooks());
        multiplayerMenu = new MultiplayerMenu(_gameEngineManager, _assetManager, _inputManager, _audioManager, _videoManager,
            _inventoryManager, _consoleManager, menuDrawing, menuLoop, menuInput, menuDefs, ui, lineInput, session, netStart, MakeMultiplayerHooks());
        elevatorScreens = new ElevatorScreens(_gameEngineManager, _assetManager, _inputManager, _audioManager, _videoManager, _graphicManager,
            _inventoryManager, _mapManager, menuDrawing, textPresenter, hubs, session, new ElevatorHooks(RadarTileColor, GetMapDisplayName));
        changeViewScreen = new ChangeViewScreen(_assetManager, _inputManager, _audioManager, _videoManager, menuDrawing, menuLoop, menuInput, ui,
            new ChangeViewHooks(() => viewsize, ShowViewSize, NewViewSize, () => bordercol));
        optionsMenu = new OptionsMenu(_videoManager, _hudMessageManager, _automapManager, menuDrawing, menuLoop, menuInput, menuDefs);
        newGameMenu = new NewGameMenu(_gameEngineManager, _assetManager, _inputManager, _audioManager, _videoManager, _graphicManager,
            menuDrawing, menuLoop, menuInput, menuDefs, textPresenter, () => ingame, StartNewGame);
        controlPanel = new ControlPanel(_gameEngineManager, _assetManager, _inputManager, _audioManager, _videoManager, menuDrawing, menuLoop,
            menuDefs, articleReader, highScoresScreen, soundMenu, controlMenus, changeViewScreen, () => gameInfoSettings, MakeControlPanelHooks());
        moviePlayer = new MoviePlayer(_assetManager, _videoManager, _inputManager, _audioManager, menuInput);
        titleScreens = new TitleScreens(_videoManager, _audioManager, _inputManager, _graphicManager, _assetManager, menuDrawing, ui, moviePlayer,
            new TitleScreenHooks(
                DemoExists,
                demonumber => { PlayDemo(demonumber); return playstate == playstatetypes.ex_abort; },
                ClearMargins,
                DrawHighScores,
                DrawTitle,
                DrawTitleText));
        signonPanel = new SignonPanel(_gameEngineManager, _assetManager, _videoManager, _inputManager, _audioManager,
            _graphicManager, _fontManager, ui, () => mouseenabled, () => _scripts?.Loaded.Count ?? 0);

        // TODO: Remove circular dependencies here
    }

    /*
    =============================================================================

                                 LOCAL CONSTANTS

    =============================================================================
    */
    internal const long FOCALLENGTH = (0x5700L);               // in global coordinates
    internal const int VIEWGLOBAL = 0x10000;               // globals visable flush to wall

    const int VIEWWIDTH = 256;                   // size of view window
    const int VIEWHEIGHT = 144;

    /*
    =============================================================================

                                GLOBAL VARIABLES

    =============================================================================
    */

    internal static readonly int[] dirangle ={0,ANGLES/8,2*ANGLES/8,3*ANGLES/8,4*ANGLES/8,
                       5*ANGLES/8,6*ANGLES/8,7*ANGLES/8,ANGLES};

    //
    // proejection variables
    //
    static int focallength;
    static uint screenofs;
    static int viewscreenx, viewscreeny;
    static int viewwidth;
    static int viewheight;
    static short centerx, centery;   // centery is this frame's horizon: basecentery moved by viewpitch
    static short basecentery;        // the horizon looking straight ahead, mid view
    static int shootdelta;          // pixels away from centerx a target can be
    static int projectionwidth;     // the view width the field of view is worked out for (see ProjectionWidth)
    static int scale;
    static int heightnumerator;

    static bool startgame;
    internal static bool loadedgame;
    internal static int mouseadjustment { get => controlSettings.mouseadjustment; set => controlSettings.mouseadjustment = value; }

    //
    // Command line parameter variables
    //
    internal static bool param_nowait = false;
    internal static int param_mission = 0;
    internal static bool param_goodtimes = false;
    static readonly WarpRequest warpRequest = new();    // --warp, --skill, --start

    private static void Main(string[] args)
    {
        CrashReport.Install();

        // What startup prints is shown on the signon screen as warnings
        CaptureStartupOutput();

        // Relative mod paths are made full here, before GameFolder.Use can move away from the
        // folder they mean
        var (gameParams, modPaths) = StartupArgs.Parse(args, Console.Error);
        param_nowait = gameParams.NoWait;
        GameFolder.Use();

        InitServices();
        _gameEngineManager.Init(gameParams, modPaths);
        SetNetParams(gameParams);     // --host, --join, --port, --name (Program.Multiplayer.cs)
        warpRequest.SetParams(gameParams);  // --warp, --skill, --start (Gameplay/WarpRequest.cs)

        // The Mods menu's choices (mods.cfg, per game), then the command line's
        var configMods = ModsConfig.Read(_gameEngineManager.GetConfigFilePath(ModsConfig.FileName));
        try
        {
            _assetManager.Load(_gameEngineManager.GamePackId, _gameEngineManager.GameReleaseId, configMods.Concat(modPaths));
            // Animated pictures in menus and the status bar play in real time; the view's walls,
            // flats and sky go by the level's clock instead (WorldTexture)
            _assetManager.AnimationClock = () => (int)GameEngineManager.GetTimeCount();
        }
        catch (DataFilesException e)
        {
            // Nothing is set up yet to shut down, and there are no settings to keep
            Console.Error.WriteLine(e.Message);
            GameEngineManager.Error(e.Message);
            Environment.Exit(1);
        }
        RegisterActorActions();
        LoadScripts();      // after the built-in actions, which a script's can't replace (Program.Scripting.cs)
        Entities.Actors.Monster.CheckClasses(_assetManager.GetActorMetadata());
        _factionManager.CheckClasses(_assetManager.GetActorMetadata());
        _translationManager.CheckDefinitions(_assetManager.GetActorMetadata(), _factionManager.Table);
        RegisterConsoleCommands();

        //CheckParameters(args); // Remove

        CheckForEpisodes();

        InitGame();

        // After the config is read, so these can override its settings: the controls and binds
        // saved on the last exit, then the player's own autoexec.cfg, then any --exec commands.
        _consoleManager.ExecFile(_gameEngineManager.GetConfigFilePath(GameEngineManager.ControlsFileName));
        _consoleManager.ExecFile(_gameEngineManager.GetConfigFilePath(GameEngineManager.BindsFileName));
        _gameEngineManager.SettingsLoaded = true;      // from here on, saving them keeps what was loaded
        _consoleManager.ExecFile(_gameEngineManager.GetConfigFilePath(GameEngineManager.AutoexecFileName));

        // --cheats: the debug keys and cheat commands from the start, as Shift+Alt+Backspace opens them
        if (gameParams.Cheats)
            DebugOk = 1;

        if (!string.IsNullOrWhiteSpace(gameParams.Exec))
            _consoleManager.Execute(gameParams.Exec);

        DemoLoop();

        _gameEngineManager.Quit("Demo loop exited???");
    }

    private static void InitGame()
    {
        bool didjukebox = false;
        var theme = _assetManager.FindInGamePack<ColorThemeAsset>("colors");
        _videoManager.Init(theme, _gameEngineManager.ReadVideoConfig());
        _graphicManager.MenuBackdrop = _gameEngineManager.GetGameInfo().MenuBackdrop;
        _graphicManager.MenuStripe = _gameEngineManager.GetGameInfo().MenuStripe;
        ReadFadeStyles();
        _inputManager.Init(_videoManager.fullscreen);
        
        pixelangle = new short[_videoManager.screenWidth];
        wallheight = new short[_videoManager.screenWidth];
        _videoManager.VideoModeChanged += OnVideoModeChanged;

        //AppDomain.CurrentDomain.ProcessExit += (s, e) => SDL.SDL_Quit();

        SignonScreen();

        _videoManager.Update();

        US_Startup();

        //
        // build some tables
        //
        InitDigiMap();

        InitHighScores();
        _gameEngineManager.ReadConfig();

        //
        // HOLDING DOWN 'M' KEY?
        //

        if (_inputManager.IsKeyDown(ScanCodes.sc_M))
        {
            DoJukebox();
            didjukebox = true;
        }

        //
        // load in and lock down some basic chunks
        //
        BuildTables();          // trig tables

        NewViewSize(viewsize);

        //
        // initialize variables
        //
        _videoManager.InitRedShifts();

        var startupLines = StopCapturingStartupOutput();
        if (!didjukebox)
            FinishSignon(startupLines);
    }

    // A new render size needs per-column tables for the new width and the view placed again;
    // fullscreen takes the mouse, a window gives it back.
    private static void OnVideoModeChanged(object? sender, VideoSettings previous)
    {
        var current = _videoManager.Settings;

        if (current.RenderWidth != previous.RenderWidth || current.RenderHeight != previous.RenderHeight
            || current.EffectiveUiScale != previous.EffectiveUiScale)
        {
            pixelangle = new short[_videoManager.screenWidth];
            wallheight = new short[_videoManager.screenWidth];
            NewViewSize(viewsize);
        }

        if (current.Fullscreen != previous.Fullscreen)
            _inputManager.SetMouseGrab(current.Fullscreen);
    }

    // Fade styles and lengths, the death screen and the weapon in hand, from game-info
    // (Gameplay/GameInfoSettings.cs); ordinary fades use _videoManager.FadeStyle and FadeTics
    static GameInfoSettings gameInfoSettings = new();
    internal static FadeStyle menuFadeStyle => gameInfoSettings.menuFadeStyle;
    internal static FadeStyle deathFadeStyle => gameInfoSettings.deathFadeStyle;
    internal static FadeStyle levelFadeStyle => gameInfoSettings.levelFadeStyle;
    internal static int menuFadeTics => gameInfoSettings.menuFadeTics;
    internal static int deathFadeTics => gameInfoSettings.deathFadeTics;
    internal static int levelFadeTics => gameInfoSettings.levelFadeTics;
    internal static string deathFadeColor => gameInfoSettings.deathFadeColor;
    internal static int deathTurnSpeed => gameInfoSettings.deathTurnSpeed;
    internal static int deathHoldTics => gameInfoSettings.deathHoldTics;
    internal static int? deathDropHeight => gameInfoSettings.deathDropHeight;
    internal static int deathDropSpeed => gameInfoSettings.deathDropSpeed;
    internal static double weaponScale => gameInfoSettings.weaponScale;
    internal static readonly WeaponBob weaponBob = new();

    private static void ReadFadeStyles()
    {
        gameInfoSettings = GameInfoSettings.Read(_gameEngineManager.GetGameInfo());
        _videoManager.FadeStyle = gameInfoSettings.screenFadeStyle;
        _videoManager.FadeTics = gameInfoSettings.screenFadeTics;
        weaponBob.Enabled = gameInfoSettings.weaponBob;
    }

    private static void SignonScreen()
    {
        _graphicManager.DrawPic(_gameEngineManager.GetGameInfo().Signon.Pic ?? "", 0, 0);
        PrintSignonInfo();
    }

    /// <summary>
    /// Blanks the screen to black when it's bigger than the 320x200 layout, so a full-screen
    /// picture about to be drawn in the middle doesn't keep the last screen round its edges
    /// </summary>
    internal static void ClearMargins()
    {
        if (_videoManager.HasMargins)
            _videoManager.ClearScreen(0);
    }

    /// <summary>
    /// The game-info title-pics, each drawn below the one before
    /// </summary>
    private static void DrawTitle()
    {
        ClearMargins();
        int y = 0;
        foreach (var pic in _gameEngineManager.GetGameInfo().TitlePics)
        {
            var graphic = _assetManager.Find<GraphicAsset>(pic);
            if (graphic == null)
                continue;

            _graphicManager.DrawPic(pic, 0, y);
            y += graphic.Height;
        }
    }

    /// <summary>
    /// Fades the title in with its own palette (game-info title-palette), or the game palette.
    /// The next screen's fade-in puts the game palette back.
    /// </summary>
    private static void FadeInTitle()
    {
        var paletteName = _gameEngineManager.GetGameInfo().TitlePalette;
        var palette = string.IsNullOrEmpty(paletteName) ? null : _assetManager.Find<Palette>(paletteName);
        if (palette == null)
            _videoManager.FadeIn();
        else
            _videoManager.FadeIn(new GamePalette { Colors = palette.ToSDLColors() }, 30);
    }

    /// <param name="startupLines">What startup printed, shown as warnings in the signon's text area</param>
    private static void FinishSignon(List<string> startupLines)
    {
        FinishSignonInfo(startupLines);

        if (!_gameEngineManager.GetGameInfo().Signon.PressAKey)
        {
            _videoManager.Update();

            if (!param_nowait)
                GameEngineManager.WaitVBL(3 * 70);
        }
        else
        {
            _videoManager.Bar(0, 189, 300, 11, "Maroon");
            CenteredText(0, 320, 190, new TextStyle(SMALL_FONT, "Bright Yellow", "Maroon")).CPrint("Press a key"); // "Oprima una tecla"

            _videoManager.Update();

            if (!param_nowait)
                _inputManager.Ack();

            _videoManager.Bar(0, 189, 300, 11, "Maroon");
            CenteredText(0, 320, 190, new TextStyle(SMALL_FONT, "Lime", "Maroon")).CPrint("Working..."); // "pensando..."

            _videoManager.Update();
        }
    }

    private static void DemoLoop()
    {
        var gameInfo = _gameEngineManager.GetGameInfo();

        //
        // main game cycle
        //
        if (HasPendingNetStart)
        {
            // --host or --join: straight to the lobby, with the menu's music
        }
        else if (gameInfo.Intro.Count > 0)
        {
            // The game pack's own intro (Blake Stone's), which starts its own music
            if (param_nowait)
                StartCPMusic(INTROSONG);
            else
                RunTitleScreens(gameInfo.Intro, titleLoop: false);
        }
        else
        {
            if (!param_nowait && gameInfo.NonSharewareNotice)
                NonShareware();

            StartCPMusic(INTROSONG);

            if (!param_nowait && _assetManager.Exists<GraphicAsset>("pg13"))    // a standalone game may have none
                PG13();
        }

        while (true)
        {
            if (RunPendingNetStart() || RunPendingWarp())
            {
                RunStartedGame();
                continue;
            }

            // recorddemo and playdemo, from the command line's --exec or a game they ended
            if (RecordPendingDemo())
            {
                RunStartedGame();       // New Game from the menu ends a recording and starts one
                continue;
            }
            PlayPendingDemo();

            while (!param_nowait && gameInfo.TitleLoop.Count > 0)
            {
                if (string.IsNullOrEmpty(_audioManager.CurrentMusicTrack))
                    StartCPMusic(INTROSONG);
                if (RunTitleScreens(gameInfo.TitleLoop, titleLoop: true))
                    break;
            }

            while (!param_nowait && gameInfo.TitleLoop.Count == 0)
            {
                //
                // title page
                //
                DrawTitle();
                _videoManager.Update();
                FadeInTitle();

                if (_inputManager.UserInput(Timing.TickBase * 15))
                    break;
                _videoManager.FadeOut();

                //
                // credits page
                //
                ClearMargins();
                _graphicManager.DrawPic("credits", 0, 0);
                _videoManager.Update();
                _videoManager.FadeIn();
                if (_inputManager.UserInput(Timing.TickBase * 10))
                    break;
                _videoManager.FadeOut();

                //
                // high scores
                //
                DrawHighScores();
                _videoManager.Update();
                _videoManager.FadeIn();

                if (_inputManager.UserInput(Timing.TickBase * 10))
                    break;

                //
                // demo
                //
                PlayDemo(TakeNextTitleDemo());
                if (playstate == playstatetypes.ex_abort)
                    break;
                _videoManager.FadeOut();
                if (_videoManager.HasMargins)
                    _videoManager.ClearScreen(0x00); // 0x00 = Black
                StartCPMusic(INTROSONG);
            }

            _videoManager.FadeOut();

            if (_inputManager.IsKeyDown(ScanCodes.sc_Tab))
                RecordDemo();
            else
                US_ControlPanel(0);

            RunStartedGame();
        }
    }

    /// <summary>Plays the game the menu started or loaded, if it did, then puts the title music back</summary>
    private static void RunStartedGame()
    {
        if (!startgame && !loadedgame)
            return;

        GameLoop();
        if (netgame)
            EndNetGame();       // a game with others is over: leave it (Program.Multiplayer.cs)
        if (!param_nowait)
        {
            _videoManager.FadeOut();
            StartCPMusic(INTROSONG);
        }
    }

    /// <summary>The skill being played (game-info skills, by gamestate.difficulty); an ordinary one if it's missing</summary>
    internal static SkillInfo CurrentSkill =>
        _gameEngineManager.GetGameInfo().Skills.Values.ElementAtOrDefault(gamestate.difficulty) ?? new SkillInfo();

    /// <summary>
    /// The skill demos are recorded and played back on (they only replay right on the skill they
    /// were made on): game-info's demo-skill, else the hardest (last) skill
    /// </summary>
    internal static short DemoSkill
    {
        get
        {
            var gameInfo = _gameEngineManager.GetGameInfo();
            var keys = gameInfo.Skills.Keys.ToList();
            var index = gameInfo.DemoSkill == null ? -1
                : keys.FindIndex(k => k.Equals(gameInfo.DemoSkill, StringComparison.OrdinalIgnoreCase));
            if (index < 0 && gameInfo.DemoSkill != null)
                Console.WriteLine($"game-info demo-skill '{gameInfo.DemoSkill}' isn't one of the skills; using the hardest");
            return (short)(index >= 0 ? index : Math.Max(keys.Count - 1, 0));
        }
    }

    /// <summary>Starts the new game --warp asked for (once): true if it did, for the demo loop to play</summary>
    internal static bool RunPendingWarp()
    {
        var gameInfo = _gameEngineManager.GetGameInfo();
        if (warpRequest.TakeWarp(gameInfo, map => _assetManager.Exists<MapAsset>(map)) is not { } warp)
            return false;

        NewGame(warp.Difficulty, warp.Episode, gameInfo.Maps[warp.Map]);
        gamestate.mapon = warp.Map;
        Console.WriteLine($"Warping to {warp.Map}, skill {warp.Difficulty + 1}");
        return true;
    }

    /// <summary>Puts the player where --start said, once, after the warped-to level has loaded</summary>
    static void ApplyPendingWarpStart()
    {
        if (warpRequest.TakeStart() is not { } start)
            return;

        if (TryTeleportPlayer(start.X, start.Y, start.Angle, out var problem))
            Console.WriteLine($"Starting on {start.X},{start.Y}");
        else
            Console.WriteLine($"--start {start.X},{start.Y}: {problem}; starting on the level's player start");
    }

    /// <summary>The class the next new game is played as (the `playerclass` command); null for the default</summary>
    internal static string? newGamePlayerClass;

    /// <summary>
    /// Starts a game as <paramref name="playerClass"/>: when null, the `playerclass` command's pick,
    /// else game-info's first player class
    /// </summary>
    internal static void NewGame(short difficulty, EpisodeInfo epInfo, MapInfo mapInfo, string? playerClass = null)
    {
        gamestate = new gametype();
        LevelRatios = [];       // the win tally averages only this game's floors
        ResetHubs();            // no levels kept, no floors been on (Program.Hubs.cs)
        gamestate.difficulty = difficulty;
        ResetPlayers();         // just the local player, until anyone joins (Program.Players.cs)
        _inventoryManager.ClearShared();
        StartPlayer(playerClass ?? newGamePlayerClass ?? DefaultPlayerClass);
        gamestate.cluster = mapInfo.Cluster;
        gamestate.mapon = epInfo.StartMap;

        startgame = true;
    }

    private record digimap
    {
        public digimap(string sound, int index, int channel)
        {
            this.sound = sound;
            this.index = index;
            this.channel = channel;
        }

        public string sound;
        public int index;
        public int channel;
    }

    static void InitDigiMap()
    {
      //  foreach (var sound in DigitizedSoundMappings.NameIndexMap)
      //  {
         //   _audioManager.SD_PrepareSound(sound);
     //   }
    }


    internal static void NewViewSize(int width)
    {
        viewsize = width;
        if (viewsize == 21)
            SetViewSize((uint)_videoManager.screenWidth, (uint)_videoManager.screenHeight);
        else if (viewsize == 20)
            SetViewSize((uint)_videoManager.screenWidth, (uint)PlayAreaHeight);
        else
            SetViewSize((uint)(width * 16 * _videoManager.screenWidth / 320), (uint)BorderedViewHeight(width));
    }

    /// <summary>The screen lines between the top status bar (if any) and the bottom one, in screen pixels</summary>
    internal static int PlayAreaHeight => _videoManager.ScreenYAboveBottom(STATUSLINES) - PlayAreaTop;

    /// <summary>The play area's height and the bottom status bar's first line, in screen pixels</summary>
    internal static int PlayAreaAndStatusLine => _videoManager.ScreenYAboveBottom(STATUSLINES - 1) - PlayAreaTop;

    /// <summary>Where the play area (view and border) starts down the screen, in screen pixels: below any top status bar</summary>
    internal static int PlayAreaTop => _videoManager.ToScreenLength(TOPLINES);

    /// <summary>
    /// A bordered view's height at view size <paramref name="width"/>. The sizes are laid out for
    /// Wolf3D's 40 line status bar: with a taller one (Blake Stone's), the view stops short of it,
    /// leaving room for the border lines round it.
    /// </summary>
    private static int BorderedViewHeight(int width)
    {
        int height = (int)(width * 16 * HEIGHTRATIO * _videoManager.screenHeight / 200);
        int room = PlayAreaHeight - _videoManager.ToScreenLength(2);
        return Math.Min(height, room);
    }

    internal static bool SetViewSize(uint width, uint height)
    {
        viewwidth = (int)(width & ~15);                  // must be divisable by 16
        viewheight = (int)(height & ~1);                 // must be even
        centerx = (short)(viewwidth / 2 - 1);
        centery = basecentery = (short)(viewheight / 2);
        projectionwidth = ProjectionWidth(viewwidth, viewheight);
        shootdelta = projectionwidth / 10;               // the same angle either side, however wide the view
        if (viewheight == _videoManager.screenHeight)
            viewscreenx = viewscreeny = (int)(screenofs = 0);
        else
        {
            viewscreenx = (_videoManager.screenWidth - viewwidth) / 2;
            viewscreeny = PlayAreaTop + (PlayAreaHeight - viewheight) / 2;
            screenofs = (uint)(viewscreeny * _videoManager.screenWidth + viewscreenx);
        }

        //
        // calculate trace angles and projection constants
        //
        CalcProjection(FOCALLENGTH);

        return true;
    }

    /*
    ====================
    =
    = CalcProjection
    =
    = Uses focallength
    =
    ====================
    */

    static void CalcProjection(long focal)
    {
        int i;
        int intang;
        float angle;
        double tang;
        int halfview;
        double facedist;

        focallength = (int)focal;
        facedist = focal + MINDIST;
        halfview = viewwidth / 2;                                 // half view in pixels

        //
        // calculate scale value for vertical height calculations
        // and sprite x calculations: from the projection width, so a view wider than the
        // classic shape sees further to the sides rather than having its walls made taller
        //
        scale = (int) (projectionwidth / 2 * facedist / (VIEWGLOBAL / 2));

        //
        // divide heightnumerator by a posts distance to get the posts height for
        // the heightbuffer.  The pixel height is height>>2
        //
        heightnumerator = (int)((MapConstants.TILEGLOBAL * scale) >> 6);

        //
        // calculate the angle offset from view angle of each pixel's ray
        //

        for (i = 0; i < halfview; i++)
        {
            // start 1/2 pixel over, so viewangle bisects two middle pixels
            tang = (int)i * VIEWGLOBAL / projectionwidth / facedist;
            angle = (float)Math.Atan(tang);
            intang = (int)(angle * radtoint);
            pixelangle[halfview - 1 - i] = (short)intang;
            pixelangle[halfview + i] = (short)-intang;
        }
    }

    /// <summary>
    /// The width a view's field of view is worked out for. Up to the shape the view size has on
    /// a 320x200 screen, that's the view's own width, as it always was: a narrower screen (4:3)
    /// shows the same across and more above and below. A wider view is worked out from its
    /// height instead, so it shows the same above and below and more to the sides (Hor+).
    /// </summary>
    static int ProjectionWidth(int width, int height)
    {
        // SetViewSize rounds the height down to even, so allow for the line that may lose:
        // otherwise a classic layout could come out a hair too wide and change
        int classicWidth = (int)Math.Ceiling((height + 1) * ClassicViewAspect(viewsize) - 1e-9);
        return Math.Max(1, Math.Min(width, classicWidth));
    }

    /// <summary>A view size's width over its height on a 320x200 screen, as NewViewSize lays it out there.</summary>
    static double ClassicViewAspect(int size)
    {
        int playArea = VideoSettings.BaseHeight - STATUSLINES - TOPLINES;
        if (size >= 21)
            return (double)VideoSettings.BaseWidth / VideoSettings.BaseHeight;
        if (size == 20)
            return (double)VideoSettings.BaseWidth / Math.Max(playArea, 1);

        double height = Math.Min(size * 16 * HEIGHTRATIO, playArea - 2);
        return size * 16 / Math.Max(height, 1);
    }

    internal static void DoJukebox()
    {
        int which, lastsong = -1;

        _inputManager.ClearKeysDown();
        //if (!AdLibPresent && !SoundBlasterPresent)
        //    return;

        MenuFadeOut();

        // Show a random page of songs
        int pages = Math.Max(1, (MusicMenu.Length + JukeboxPageSize - 1) / JukeboxPageSize);
        int start = (int)((SDL.SDL_GetTicks() / 10) % (uint)pages) * JukeboxPageSize;
        var page = MusicMenu.Skip(start).Take(JukeboxPageSize).ToArray();
        MusicItems.amount = (short)page.Length;
        MusicItems.curpos = 0;

        //CA_LoadAllSounds();

        DrawMenuComponents("jukebox");

        DrawMenu(MusicItems, page);

        _videoManager.Update();
        MenuFadeIn();

        do
        {
            which = HandleMenu(MusicItems, page, null);
            if (which >= 0)
            {
                if (lastsong >= 0)
                    page[lastsong].active = 1;

                if (page[which].data is string song)
                    StartCPMusic(song);
                page[which].active = 2;
                DrawMenu(MusicItems, page);
                _videoManager.Update();
                lastsong = which;
            }
        } while (which >= 0);

        MenuFadeOut();
        _inputManager.ClearKeysDown();
    }
    
    const float radtoint = FINEANGLES / 2 / PI;
    internal static void BuildTables()
    {

        //
        // calculate fine tangents
        //

        int i;
        for (i = 0; i < FINEANGLES / 8; i++)
        {
            double tang = Math.Tan((i + 0.5d) / radtoint);
            finetangent[i] = (int)(tang * MapConstants.GLOBAL1);
            finetangent[FINEANGLES / 4 - 1 - i] = (int)((1 / tang) * MapConstants.GLOBAL1);
        }

        //
        // costable overlays sintable with a quarter phase shift
        // ANGLES is assumed to be divisable by four
        //

        float angle = 0;
        float anglestep = (float)(PI / 2 / ANGLEQUAD);
        for (i = 0; i < ANGLEQUAD; i++)
        {
            int value= (int)(MapConstants.GLOBAL1 * Math.Sin(angle));
            sintable[i] = sintable[i + ANGLES] = sintable[ANGLES / 2 - i] = value;
            sintable[ANGLES - i] = sintable[ANGLES / 2 + i] = -value;
            angle += anglestep;
        }
        sintable[ANGLEQUAD] = 65536;
        sintable[3 * ANGLEQUAD] = -65536;

        //defined(USE_STARSKY) || defined(USE_RAIN) || defined(USE_SNOW)
        //Init3DPoints();
    }

    internal static void ShowViewSize(int width)
    {
        int oldwidth, oldheight;

        oldwidth = viewwidth;
        oldheight = viewheight;

        if (width == 21)
        {
            viewwidth = _videoManager.screenWidth;
            viewheight = _videoManager.screenHeight;
            _videoManager.BarScaledCoord(0, 0, _videoManager.screenWidth, _videoManager.screenHeight, "Black");
        }
        else if (width == 20)
        {
            viewwidth = _videoManager.screenWidth;
            viewheight = PlayAreaHeight;
            DrawPlayBorder();
        }
        else
        {
            viewwidth = width * 16 * _videoManager.screenWidth / 320;
            viewheight = BorderedViewHeight(width);
            DrawPlayBorder();
        }

        viewwidth = oldwidth;
        viewheight = oldheight;
    }
}
