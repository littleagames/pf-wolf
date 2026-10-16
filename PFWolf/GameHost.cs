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

internal static partial class GameHost
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

    internal const int MaxX = 320;
    internal const int MaxY = 200;
    internal const int MaxHelpLines = 500;
    internal const int MaxHighName = HighScore.MaxHighName;
    internal const int LegacyScoreCount = HighScore.LegacyScoreCount;
    internal const int MaxGameName = GameConstants.MaxGameName;
    internal const int MaxString = LineInput.MaxString;

    internal static TextWindow US_DrawWindow(int x, int y, int w, int h, TextStyle style) => ui.DrawWindow(x, y, w, h, style);
    internal static bool US_LineInput(int x, int y, ref string buf, string def, bool escok, int maxchars, int maxwidth, TextStyle style)
        => lineInput.Run(x, y, ref buf, def, escok, maxchars, maxwidth, style);

    // The high score table: Wolf3D's, until InitHighScores puts in the game pack's
    // The settings and high scores config.cfg and controls.cfg keep (Configuration/GameSettings.cs)
    internal static readonly GameSettings settings = new();

    internal static HighScore[] Scores { get => settings.Scores; set => settings.Scores = value; }
    internal static void InitHighScores() => Scores = HighScore.TableFor(_gameEngineManager.GetGameInfo().HighScores, Scores);

    internal static void US_InitRndT(bool randomize) => session.random.InitRndT(randomize);
    internal static int US_RndT() => session.random.RndT();

    // Everything written to stdout from the start until the signon screen is done
    private static StartupOutput? startupOutput;

    // The signon screen's startup info (Ui/SignonPanel.cs)
    static SignonPanel signonPanel = null!;   // made in InitServices

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
    internal static void PresenterMessageBox(string script) => textPresenter.PresenterMessageBox(script);
    internal static void EndText() => articleReader.EndText();

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
    internal static void DrawHighScores() => highScoresScreen.DrawHighScores();
    static IntermissionAsset Intermission => intermissionScreen.Intermission;           // the death cam's text uses these too

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

    static void DrawTitleText(TitleTextInfo text) => textPresenter.DrawTitleText(text);

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
        var serviceProvider = GameServices.Build(session, settings);

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

        positionalSounds = new PositionalSounds(_audioManager, session);

        debugScreens = new DebugScreens(_gameEngineManager, _videoManager, _inputManager, _mapManager, session);
        menuInput = new MenuInput(_inputManager, _audioManager, _gameEngineManager, () => mouseenabled, () => joystickenabled);
        menuDrawing = new MenuDrawing(_graphicManager, _videoManager, _assetManager, _fontManager, _gameEngineManager, ui, () => gameInfoSettings);
        menuLoop = new MenuLoop(menuDrawing, menuInput, _graphicManager, _videoManager, _inputManager, _audioManager, _assetManager);
        lineInput = new LineInput(_graphicManager, _inputManager, _videoManager, ui, menuInput.ReadAnyControl);
        doors = new Doors(_gameEngineManager, _audioManager, _mapManager, _inventoryManager, _hudMessageManager, session,
            new DoorHooks(PlaySoundLocTile, DrawKeys));
        pushWalls = new PushWalls(_mapManager, _audioManager, session, () => demoplayback || demorecord);
        wallSpriteBlocking = new WallSpriteBlocking(_mapManager);
        lineOfSight = new LineOfSight(_mapManager, doors, session, WallSpriteBlocksLine);
        levelAI = new LevelAI(_mapManager, lineOfSight, positionalSounds, session);
        switchActions = MakeSwitchActions();
        smartSwitches = MakeSmartSwitches();
        hubs = new Hubs(_gameEngineManager, _mapManager, session);
        projectileActions = MakeProjectileActions();
        lighting = MakeLighting();
        levelBodies = new LevelBodies(_mapManager, doors, pushWalls, lighting, session);
        consoleTargets = new ConsoleTargets(_mapManager, lighting, session);
        actorLights = MakeActorLights();
        weather = MakeWeather();
        spriteScaler = MakeSpriteScaler();
        wallSpriteDrawing = MakeWallSpriteDrawing();
        skyAndFlats = MakeSkyAndFlats();
        wallPosts = MakeWallPosts();
        wallHits = MakeWallHits();
        rayTrace = MakeRayTrace();
        spriteDrawing = MakeSpriteDrawing();
        playerBodies = new PlayerBodies(_assetManager, _mapManager, _inventoryManager, spriteDrawing, session);
        playerView = MakePlayerView();
        levelEditCommands = new LevelEditCommands(_consoleManager, _assetManager, _mapManager, lighting, actorLights, weather, skyAndFlats, render,
            consoleTargets, session);
        factionCommands = new FactionCommands(_consoleManager, _assetManager, _mapManager, _factionManager, _translationManager, consoleTargets, session);
        frameRefresh = MakeFrameRefresh();
        inputPolling = MakeInputPolling();
        automapInput = MakeAutomapInput();
        statusBarDrawing = MakeStatusBarDrawing();
        playArea = new PlayArea(_videoManager, statusBarDrawing);
        deathmatch = MakeDeathmatch();
        radar = MakeRadar();
        automapDrawing = MakeAutomapDrawing();
        playerClassRules = new PlayerClassRules(_gameEngineManager, _inventoryManager, session);
        playerWeapons = MakePlayerWeapons();
        playScreen = MakePlayScreen();
        viewSetup = new ViewSetup(_videoManager, playScreen, render, playArea, new ViewSetupHooks(() => viewsize, size => viewsize = size));
        consoleDrawing = new ConsoleDrawing(_videoManager, _graphicManager, _fontManager, _consoleManager, playScreen, statusBarDrawing, playArea);
        debugCommands = new DebugCommands(_consoleManager, _gameEngineManager, _videoManager, _mapManager, doors, frameRefresh, debugScreens,
            playScreen, render, consoleTargets, session,
            new DebugCommandsHooks(() => viewsize, () => singlestep, step => singlestep = step, () => extravbls, vbls => extravbls = vbls));
        playerStats = MakePlayerStats();
        playerActions = MakePlayerActions();
        cheatCommands = new CheatCommands(_consoleManager, _gameEngineManager, _assetManager, _inventoryManager, _mapManager, doors, statusBarDrawing,
            playerWeapons, playerClassRules, playerStats, frameRefresh, consoleTargets, session,
            new CheatCommandsHooks(() => godmode, on => godmode = on, () => noclip, on => noclip = on, () => ingame, s => playstate = s,
                () => newGamePlayerClass, playerClass => newGamePlayerClass = playerClass, SetEyeHeight));
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
        newGames = new NewGames(_gameEngineManager, _inventoryManager, hubs, playersManager, playerClassRules, session,
            new NewGamesHooks(() => startgame = true));
        hudMessageDrawing = MakeHudMessageDrawing();
        spectating = new Spectating(_mapManager, _consoleManager, _hudMessageManager, hudMessageDrawing, session);
        playerCommands = new PlayerCommands(_consoleManager, _mapManager, doors, playerClassRules, playersManager, playerThink, deathmatch, session,
            new PlayerCommandsHooks(() => viewsize, DrawPlayScreen));
        scoreboard = MakeScoreboard();
        netPlay = MakeNetPlay();
        netOverlay = MakeNetOverlay();
        gameScripts = MakeGameScripts();
        levelMusic = new LevelMusic(_gameEngineManager, _audioManager, session);
        playKeys = MakePlayKeys();
        levelLoop = MakeLevelLoop();
        levelSetup = MakeLevelSetup();
        levelEntry = MakeLevelEntry();
        gameSaves = MakeGameSaves();
        saveCommands = new SaveCommands(_consoleManager, _gameEngineManager, gameSaves, playScreen, session,
            new SaveCommandsHooks(on => loadedgame = on, s => playstate = s));
        midGameJoin = MakeMidGameJoin();
        demos = MakeDemos();
        demoCommands = new DemoCommands(_consoleManager, _mapManager, demos, demoBuffer, session,
            new DemoCommandsHooks(() => ingame, s => playstate = s));
        menuDefs = new MenuDefs(_assetManager, _gameEngineManager, _graphicManager, _audioManager, ui);
        jukebox = new Jukebox(_inputManager, _videoManager, _audioManager, menuDrawing, menuLoop, menuDefs);
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
            _graphicManager, menuDrawing, ui, session, playArea, MakeIntermissionHooks());
        loadSaveScreen = new LoadSaveScreen(_gameEngineManager, _assetManager, _inputManager, _audioManager, _videoManager,
            _graphicManager, menuDrawing, menuLoop, menuInput, menuDefs, ui, lineInput, MakeLoadSaveHooks());
        multiplayerMenu = new MultiplayerMenu(_gameEngineManager, _assetManager, _inputManager, _audioManager, _videoManager,
            _inventoryManager, _consoleManager, menuDrawing, menuLoop, menuInput, menuDefs, ui, lineInput, session, netStart, MakeMultiplayerHooks());
        netGames = MakeNetGames();
        netCommands = new NetCommands(_consoleManager, multiplayerMenu, scoreboard, netPlay, session, new NetCommandsHooks(() => netgame));
        elevatorScreens = new ElevatorScreens(_gameEngineManager, _assetManager, _inputManager, _audioManager, _videoManager, _graphicManager,
            _inventoryManager, _mapManager, menuDrawing, textPresenter, hubs, session, new ElevatorHooks(RadarTileColor, GetMapDisplayName));
        changeViewScreen = new ChangeViewScreen(_assetManager, _inputManager, _audioManager, _videoManager, menuDrawing, menuLoop, menuInput, ui,
            new ChangeViewHooks(() => viewsize, ShowViewSize, NewViewSize, () => bordercol));
        optionsMenu = new OptionsMenu(_videoManager, _hudMessageManager, _automapManager, menuDrawing, menuLoop, menuInput, menuDefs);
        playerDeath = MakePlayerDeath();
        newGameMenu = new NewGameMenu(_gameEngineManager, _assetManager, _inputManager, _audioManager, _videoManager, _graphicManager,
            menuDrawing, menuLoop, menuInput, menuDefs, textPresenter, _inventoryManager, () => ingame, StartNewGame, PlayerClasses);
        controlPanel = new ControlPanel(_gameEngineManager, _assetManager, _inputManager, _audioManager, _videoManager, menuDrawing, menuLoop,
            menuDefs, articleReader, highScoresScreen, soundMenu, controlMenus, changeViewScreen, () => gameInfoSettings, MakeControlPanelHooks());
        moviePlayer = new MoviePlayer(_assetManager, _videoManager, _inputManager, _audioManager, menuInput);
        mainGameLoop = MakeMainGameLoop();
        quickKeys = new QuickKeys(_gameEngineManager, _assetManager, _videoManager, _audioManager, _inputManager, menuDrawing, menuLoop,
            controlPanel, gameSaves, levelMusic, frameRefresh, playScreen, session,
            new QuickKeysHooks(() => ingame, offset => lastgamemusicoffset = offset, () => lastgamemusicoffset, s => playstate = s,
                () => viewsize, () => startgame, () => loadedgame, UiRandom));
        hubActions = MakeHubActions();
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

        titleLoop = new TitleLoop(_gameEngineManager, _assetManager, _videoManager, _graphicManager, _inputManager, _audioManager, ui,
            signonPanel, titleScreens, highScoresScreen, demos, netGames, quickKeys, mainGameLoop, cheatCommands, warpRequest, session,
            new TitleLoopHooks(() => param_nowait, () => INTROSONG, () => startgame, () => loadedgame, () => netgame, () => playstate,
                (skill, episode, map) => NewGame(skill, episode, map)));

        // TODO: Remove circular dependencies here
    }

    /*
    =============================================================================

                                 LOCAL CONSTANTS

    =============================================================================
    */
    internal const long FOCALLENGTH = GameConstants.FOCALLENGTH;
    internal const int VIEWGLOBAL = GameConstants.VIEWGLOBAL;

    const int VIEWWIDTH = 256;                   // size of view window
    const int VIEWHEIGHT = 144;

    /*
    =============================================================================

                                GLOBAL VARIABLES

    =============================================================================
    */

    //
    // proejection variables
    //
    static int focallength { get => render.focallength; set => render.focallength = value; }
    static int viewscreenx { get => render.viewscreenx; set => render.viewscreenx = value; }
    static int viewscreeny { get => render.viewscreeny; set => render.viewscreeny = value; }
    static int viewwidth { get => render.viewwidth; set => render.viewwidth = value; }
    static int viewheight { get => render.viewheight; set => render.viewheight = value; }
    static short centerx { get => render.centerx; set => render.centerx = value; }
    static short centery { get => render.centery; set => render.centery = value; }   // centery is this frame's horizon: basecentery moved by viewpitch
    static short basecentery { get => render.basecentery; set => render.basecentery = value; }   // the horizon looking straight ahead, mid view
    static int shootdelta { get => render.shootdelta; set => render.shootdelta = value; }   // pixels away from centerx a target can be
    static int projectionwidth { get => render.projectionwidth; set => render.projectionwidth = value; }   // the view width the field of view is worked out for (see ProjectionWidth)
    static int heightnumerator { get => render.heightnumerator; set => render.heightnumerator = value; }

    static bool startgame;
    internal static bool loadedgame { get => session.loadedgame; set => session.loadedgame = value; }
    internal static int mouseadjustment { get => controlSettings.mouseadjustment; set => controlSettings.mouseadjustment = value; }

    //
    // Command line parameter variables
    //
    internal static bool param_nowait = false;
    internal static int param_mission = 0;
    internal static bool param_goodtimes = false;
    static readonly WarpRequest warpRequest = new();    // --warp, --skill, --start

    /// <summary>Runs the game: what Main does (GameHost.cs)</summary>
    internal static void Run(string[] args)
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
        SetNetParams(gameParams);     // --host, --join, --port, --name (GameHost.Multiplayer.cs)
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
        LoadScripts();      // after the built-in actions, which a script's can't replace (GameHost.Scripting.cs)
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

        // Set "Read This" back to standard active
        FindMenuItem(MainMenu, "readthis")?.active = 1;
        MainItems.curpos = 0;

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
    internal static FadeStyle levelFadeStyle => gameInfoSettings.levelFadeStyle;
    internal static int levelFadeTics => gameInfoSettings.levelFadeTics;
    internal static int deathTurnSpeed => gameInfoSettings.deathTurnSpeed;
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

    // From the signon through the title loop to the games it starts (Ui/TitleLoop.cs)
    static TitleLoop titleLoop = null!;     // made in InitServices

    private static void SignonScreen() => titleLoop.SignonScreen();
    internal static void ClearMargins() => titleLoop.ClearMargins();
    private static void DrawTitle() => titleLoop.DrawTitle();
    private static void FinishSignon(List<string> startupLines) => titleLoop.FinishSignon(startupLines);
    private static void DemoLoop() => titleLoop.DemoLoop();
    static void ApplyPendingWarpStart() => titleLoop.ApplyPendingWarpStart();

    // Starting a new game, and the skills (Gameplay/NewGames.cs)
    static NewGames newGames = null!;       // made in InitServices

    internal static SkillInfo CurrentSkill => newGames.CurrentSkill;
    internal static short DemoSkill => newGames.DemoSkill;
    /// <summary>The class the next new game is played as (the `playerclass` command); null for the default</summary>
    internal static string? newGamePlayerClass { get => newGames.newGamePlayerClass; set => newGames.newGamePlayerClass = value; }
    internal static void NewGame(short difficulty, EpisodeInfo epInfo, MapInfo mapInfo, string? playerClass = null) =>
        newGames.NewGame(difficulty, epInfo, mapInfo, playerClass);
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

    // The view's size and the projection (Rendering/ViewSetup.cs)
    static ViewSetup viewSetup = null!;     // made in InitServices

    internal static void NewViewSize(int width) => viewSetup.NewViewSize(width);
    // Where the play area is, between the status bars (Ui/PlayArea.cs)
    static PlayArea playArea = null!;       // made in InitServices
    internal static int PlayAreaHeight => playArea.PlayAreaHeight;
    internal static int PlayAreaAndStatusLine => playArea.PlayAreaAndStatusLine;
    internal static int PlayAreaTop => playArea.PlayAreaTop;
    internal static void ShowViewSize(int width) => viewSetup.ShowViewSize(width);

    internal static void DoJukebox() => jukebox.DoJukebox();

    // The sine, cosine and fine tangent tables (Rendering/TrigTables.cs)
    internal static void BuildTables() => TrigTables.BuildTables();
}
