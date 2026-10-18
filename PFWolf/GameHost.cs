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
    internal static AudioManager AudioManager;
    private static InputManager _inputManager;
    internal static GameEngineManager GameEngineManager;
    private static GraphicManager _graphicManager;
    private static FontManager _fontManager;
    internal static MapManager MapManager;
    internal static AssetManager AssetManager;
    internal static InventoryManager InventoryManager;
    private static ConsoleManager _consoleManager;
    private static AutomapManager _automapManager;
    internal static HudMessageManager HudMessageManager;
    internal static FactionManager FactionManager;
    internal static TranslationManager TranslationManager;

    // Who's playing, the acting player, tics, GameState and the camera (Gameplay/GameSession.cs)
    internal static readonly GameSession Session = new();

    // What the actors use of the game (Entities/Actors/ActorWorld.cs), built last in InitServices
    static Entities.Actors.ActorWorld actorWorld = null!;

    /*
    =============================================================================

                        TEXT, WINDOWS, LINE INPUT, RANDOM NUMBERS

    What ID_US gave the rest of the game, now in Ui/ (text and line input), Gameplay/RandomTable.cs
    and Gameplay/HighScore.cs; these forward to them.

    =============================================================================
    */

    static UiText ui = null!;               // made in InitServices
    static LineInput lineInput = null!;

    // The high score table: Wolf3D's, until InitHighScores puts in the game pack's
    // The settings and high scores config.cfg and controls.cfg keep (Configuration/GameSettings.cs)
    internal static readonly GameSettings Settings = new();

    internal static HighScore[] Scores { get => Settings.Scores; set => Settings.Scores = value; }
    internal static void InitHighScores() => Scores = HighScore.TableFor(GameEngineManager.GetGameInfo().HighScores, Scores);

    internal static void InitRandom(bool randomize) => Session.Random.InitRndT(randomize);
    internal static int NextRandom() => Session.Random.RndT();

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
    internal static int ShowTextScreen(string textName)
    {
        textPresenter.ShowTextPages(textName, fromMenu: true);
        StartCPMusic(MenuSong);
        return 0;
    }
    // The high scores, and the level-end and victory screens (Ui/HighScoresScreen.cs,
    // Ui/IntermissionScreen.cs, made in InitServices)
    static HighScoresScreen highScoresScreen = null!;
    static IntermissionScreen intermissionScreen = null!;
    internal static void DrawHighScores() => highScoresScreen.DrawHighScores();

    static IntermissionHooks MakeIntermissionHooks() => new(
        ViewColorBorder: () =>
        {
            if (BorderCol != "VIEWCOLOR")
                DrawStatusBorder("VIEWCOLOR");
        },
        GiveEveryone: bonus =>
        {
            // Everyone's bonus, with others: each machine gives each player theirs alike
            foreach (var p in PlayerStates)
            {
                using var _ = ActAs(p);
                GivePoints(bonus);
            }
        },
        DrawScore: DrawScore,
        DrawPlayBorder: DrawPlayBorder,
        GameWon: () => { FindMenuItem(MainMenu, "savegame")?.Active = 0; },
        EndText: EndText,
        MapDisplayName: GetMapDisplayName,
        NewUnlocks: () => Unlocks.TakeNews());

    // game-info's intro and title-loop screens, and Blake Stone's movies (Ui/TitleScreens.cs,
    // Ui/MoviePlayer.cs)
    static MoviePlayer moviePlayer = null!;     // made in InitServices
    static TitleScreens titleScreens = null!;

    static void DrawTitleText(TitleTextInfo text) => textPresenter.DrawTitleText(text);

    static bool randomStarted;

    internal static void InitRandomOnce()
    {
        if (randomStarted)
            return;

        InitRandom(true);

        randomStarted = true;
    }

    /// <summary>Builds the managers (Startup/GameServices.cs) and hooks them up</summary>
    private static void InitServices()
    {
        var serviceProvider = GameServices.Build(Session, Settings, () => actorWorld);

        GameEngineManager = serviceProvider.GetRequiredService<GameEngineManager>();
        _videoManager = serviceProvider.GetRequiredService<VideoManager>();
        AudioManager = serviceProvider.GetRequiredService<AudioManager>();
        _inputManager = serviceProvider.GetRequiredService<InputManager>();
        _graphicManager = serviceProvider.GetRequiredService<GraphicManager>();
        _fontManager = serviceProvider.GetRequiredService<FontManager>();
        MapManager = serviceProvider.GetRequiredService<MapManager>();
        AssetManager = serviceProvider.GetRequiredService<AssetManager>();
        InventoryManager = serviceProvider.GetRequiredService<InventoryManager>();
        InventoryManager.PlayerClass = () => PlayerState.PlayerClass;
        Session.ActingChanged = p => InventoryManager.SetHolder(p.Items);
        Session.GameModeChanged = mode => InventoryManager.ShareKeys = mode.IsCoopLike();
        SetActing(PlayerState);         // the inventory reads the acting player's items
        _consoleManager = serviceProvider.GetRequiredService<ConsoleManager>();
        _automapManager = serviceProvider.GetRequiredService<AutomapManager>();
        HudMessageManager = serviceProvider.GetRequiredService<HudMessageManager>();
        FactionManager = serviceProvider.GetRequiredService<FactionManager>();
        TranslationManager = serviceProvider.GetRequiredService<TranslationManager>();

        ui = new UiText(_graphicManager, _fontManager);

        positionalSounds = new PositionalSounds(AudioManager, Session);

        debugScreens = new DebugScreens(GameEngineManager, _videoManager, _inputManager, MapManager, Session);
        menuInput = new MenuInput(_inputManager, AudioManager, GameEngineManager, () => MouseEnabled, () => JoystickEnabled);
        menuDrawing = new MenuDrawing(_graphicManager, _videoManager, AssetManager, _fontManager, GameEngineManager, ui, () => gameInfoSettings);
        menuLoop = new MenuLoop(menuDrawing, menuInput, _graphicManager, _videoManager, _inputManager, AudioManager, AssetManager);
        lineInput = new LineInput(_graphicManager, _inputManager, _videoManager, ui, menuInput.ReadAnyControl);
        doors = new Doors(GameEngineManager, AudioManager, MapManager, InventoryManager, HudMessageManager, Session,
            new DoorHooks(PlaySoundLocTile, DrawKeys));
        pushWalls = new PushWalls(MapManager, AudioManager, Session, () => GameEngineManager.GetGameInfo().PushwallDistance);
        wallSpriteBlocking = new WallSpriteBlocking(MapManager);
        lineOfSight = new LineOfSight(MapManager, doors, Session, WallSpriteBlocksLine);
        LevelAI = new LevelAI(MapManager, lineOfSight, positionalSounds, Session);
        switchActions = MakeSwitchActions();
        smartSwitches = MakeSmartSwitches();
        hubs = new Hubs(GameEngineManager, MapManager, Session);
        projectileActions = MakeProjectileActions();
        lighting = MakeLighting();
        levelBodies = new LevelBodies(MapManager, doors, pushWalls, lighting, Session);
        consoleTargets = new ConsoleTargets(MapManager, lighting, Session);
        actorLights = MakeActorLights();
        weather = MakeWeather();
        spriteScaler = MakeSpriteScaler();
        wallSpriteDrawing = MakeWallSpriteDrawing();
        skyAndFlats = MakeSkyAndFlats();
        wallPosts = MakeWallPosts();
        wallHits = MakeWallHits();
        rayTrace = MakeRayTrace();
        spriteDrawing = MakeSpriteDrawing();
        playerBodies = new PlayerBodies(AssetManager, MapManager, InventoryManager, spriteDrawing, Session);
        playerView = MakePlayerView();
        levelEditCommands = new LevelEditCommands(_consoleManager, AssetManager, MapManager, lighting, actorLights, weather, skyAndFlats, Render,
            consoleTargets, Session);
        factionCommands = new FactionCommands(_consoleManager, AssetManager, MapManager, FactionManager, TranslationManager, consoleTargets, Session);
        frameRefresh = MakeFrameRefresh();
        inputPolling = MakeInputPolling();
        automapInput = MakeAutomapInput();
        statusBarDrawing = MakeStatusBarDrawing();
        playArea = new PlayArea(_videoManager, statusBarDrawing);
        deathmatch = MakeDeathmatch();
        statusBarDrawing.ScoreShown = deathmatch.ScoreShown;
        AddObjectives();
        radar = MakeRadar();
        automapDrawing = MakeAutomapDrawing();
        playerClassRules = new PlayerClassRules(GameEngineManager, InventoryManager, Session);
        playerWeapons = MakePlayerWeapons();
        playScreen = MakePlayScreen();
        viewSetup = new ViewSetup(_videoManager, playScreen, Render, playArea, Settings);
        consoleDrawing = new ConsoleDrawing(_videoManager, _graphicManager, _fontManager, _consoleManager, playScreen, statusBarDrawing, playArea);
        debugCommands = new DebugCommands(_consoleManager, GameEngineManager, _videoManager, MapManager, doors, frameRefresh, debugScreens,
            playScreen, Render, consoleTargets, Session,
            Settings);
        playerStats = MakePlayerStats();
        playerActions = MakePlayerActions();
        cheatCommands = new CheatCommands(_consoleManager, GameEngineManager, AssetManager, InventoryManager, MapManager, doors, statusBarDrawing,
            playerWeapons, playerClassRules, playerStats, frameRefresh, consoleTargets, Session,
            new CheatCommandsHooks(() => NewGamePlayerClass, playerClass => NewGamePlayerClass = playerClass, SetEyeHeight));
        playerUse = MakePlayerUse();
        playerMovement = MakePlayerMovement();
        playerAttacks = MakePlayerAttacks();
        projectiles = MakeProjectiles();
        actorSounds = new ActorSounds(MapManager, AudioManager, Session, PlaySoundLocActor);
        bjVictory = new BJVictory(MapManager, Session);
        missiles = MakeMissiles();
        weaponInHand = MakeWeaponInHand();
        pickups = MakePickups();
        playerThink = MakePlayerThink();
        playersManager = MakePlayers();
        newGames = new NewGames(GameEngineManager, InventoryManager, hubs, playersManager, playerClassRules, Session);
        hudMessageDrawing = MakeHudMessageDrawing();
        spectating = new Spectating(MapManager, _consoleManager, HudMessageManager, hudMessageDrawing, Session);
        playerCommands = new PlayerCommands(_consoleManager, MapManager, doors, playerClassRules, playersManager, playerThink, deathmatch, Session,
            Settings, new PlayerCommandsHooks(DrawPlayScreen, PlayerColor));
        scoreboard = MakeScoreboard();
        scoreboard.Match = deathmatch;
        netPlay = MakeNetPlay();
        netOverlay = MakeNetOverlay();
        netOverlay.Match = deathmatch;
        netOverlay.ViewTop = () => _videoManager.ToLayoutY(Render.ViewScreenY, roundUp: true);
        netOverlay.ViewRect = () => (Render.ViewScreenX, Render.ViewScreenY, Render.ViewWidth, Render.ViewHeight);
        hudMessageDrawing.TopReserved = netOverlay.MatchLineHeight;     // messages go under a match's line
        automapDrawing.TopReserved = netOverlay.MatchLineHeight;        // and the automap's stats
        Session.Camera.WantsChase = () => ControlSettings.ChaseCam && deathmatch.Objective?.ChaseCam(Session.LocalPlayer) == true;
        Session.Camera.IsOpenTile = (x, y) => x > 0 && y > 0 && x < MapManager.MapWidth - 1 && y < MapManager.MapHeight - 1
            && MapManager.TileMap[x, y] == 0;
        gameScripts = MakeGameScripts();
        levelMusic = new LevelMusic(GameEngineManager, AudioManager, Session);
        playKeys = MakePlayKeys();
        levelLoop = MakeLevelLoop();
        levelSetup = MakeLevelSetup();
        levelEntry = MakeLevelEntry();
        gameSaves = MakeGameSaves();
        saveCommands = new SaveCommands(_consoleManager, GameEngineManager, gameSaves, playScreen, Session);
        midGameJoin = MakeMidGameJoin();
        demos = MakeDemos();
        demoCommands = new DemoCommands(_consoleManager, MapManager, demos, DemoBuffer, Session);
        menuDefs = new MenuDefs(AssetManager, GameEngineManager, _graphicManager, AudioManager, ui);
        jukebox = new Jukebox(_inputManager, _videoManager, AudioManager, menuDrawing, menuLoop, menuDefs);
        RegisterMenuDefs();
        modsMenu = new ModsMenu(GameEngineManager, AssetManager, _inputManager, AudioManager, _videoManager,
            menuDrawing, menuLoop, menuInput, menuDefs, ui, Session);
        videoMenu = new VideoMenu(AssetManager, _inputManager, AudioManager, _videoManager, menuDrawing, menuLoop, menuInput, menuDefs);
        soundMenu = new SoundMenu(AudioManager, _videoManager, menuDrawing, menuLoop, menuInput, menuDefs);
        controlMenus = new ControlMenus(AssetManager, _inputManager, AudioManager, _videoManager, menuDrawing, menuLoop, menuInput, menuDefs, ui, ControlSettings);
        customizeScreen = new CustomizeControlsScreen(AssetManager, _inputManager, AudioManager, _videoManager,
            menuDrawing, menuLoop, menuInput, menuDefs, ui, Controls);
        textPresenter = new TextPresenter(AssetManager, _inputManager, AudioManager, _videoManager,
            _graphicManager, _fontManager, menuDrawing, menuInput);
        articleReader = new ArticleReader(GameEngineManager, AssetManager, _inputManager, AudioManager, _videoManager,
            _graphicManager, menuInput, Session, textPresenter);
        highScoresScreen = new HighScoresScreen(GameEngineManager, AssetManager, _inputManager, AudioManager, _videoManager,
            _graphicManager, menuDrawing, menuDefs, ui, lineInput, Session, () => Scores, FloorScore);
        intermissionScreen = new IntermissionScreen(GameEngineManager, AssetManager, _inputManager, AudioManager, _videoManager,
            _graphicManager, menuDrawing, ui, Session, playArea, MakeIntermissionHooks());
        loadSaveScreen = new LoadSaveScreen(GameEngineManager, AssetManager, _inputManager, AudioManager, _videoManager,
            _graphicManager, menuDrawing, menuLoop, menuInput, menuDefs, ui, lineInput, MakeLoadSaveHooks());
        multiplayerMenu = new MultiplayerMenu(GameEngineManager, AssetManager, _inputManager, AudioManager, _videoManager,
            InventoryManager, _consoleManager, menuDrawing, menuLoop, menuInput, menuDefs, ui, lineInput, Session, netStart, MakeMultiplayerHooks());
        netGames = MakeNetGames();
        netCommands = new NetCommands(_consoleManager, multiplayerMenu, scoreboard, netPlay, Session);
        elevatorScreens = new ElevatorScreens(GameEngineManager, AssetManager, _inputManager, AudioManager, _videoManager, _graphicManager,
            InventoryManager, MapManager, menuDrawing, textPresenter, hubs, Session, new ElevatorHooks(RadarTileColor, GetMapDisplayName));
        changeViewScreen = new ChangeViewScreen(AssetManager, _inputManager, AudioManager, _videoManager, menuDrawing, menuLoop, menuInput, ui,
            Settings, Session, new ChangeViewHooks(ShowViewSize, NewViewSize));
        optionsMenu = new OptionsMenu(_videoManager, HudMessageManager, _automapManager, menuDrawing, menuLoop, menuInput, menuDefs);
        playerDeath = MakePlayerDeath();
        newGameMenu = new NewGameMenu(GameEngineManager, AssetManager, _inputManager, AudioManager, _videoManager, _graphicManager,
            menuDrawing, menuLoop, menuInput, menuDefs, textPresenter, InventoryManager, Session, StartNewGame, PlayerClasses,
            episode => Unlocks.IsOpen(episode));
        controlPanel = new ControlPanel(GameEngineManager, AssetManager, _inputManager, AudioManager, _videoManager, menuDrawing, menuLoop,
            menuDefs, articleReader, highScoresScreen, soundMenu, controlMenus, changeViewScreen, () => gameInfoSettings, Session, MakeControlPanelHooks());
        moviePlayer = new MoviePlayer(AssetManager, _videoManager, _inputManager, AudioManager, menuInput);
        mainGameLoop = MakeMainGameLoop();
        quickKeys = new QuickKeys(GameEngineManager, AssetManager, _videoManager, AudioManager, _inputManager, menuDrawing, menuLoop,
            controlPanel, gameSaves, levelMusic, frameRefresh, playScreen, Session,
            Settings, new QuickKeysHooks(UiRandom));
        hubActions = MakeHubActions();
        titleScreens = new TitleScreens(_videoManager, AudioManager, _inputManager, _graphicManager, AssetManager, menuDrawing, ui, moviePlayer,
            new TitleScreenHooks(
                DemoExists,
                demonumber => { PlayDemo(demonumber); return PlayState == PlayState.Abort; },
                ClearMargins,
                DrawHighScores,
                DrawTitle,
                DrawTitleText));
        signonPanel = new SignonPanel(GameEngineManager, AssetManager, _videoManager, _inputManager, AudioManager,
            _graphicManager, _fontManager, ui, () => MouseEnabled, () => Scripts?.Loaded.Count ?? 0);

        titleLoop = new TitleLoop(GameEngineManager, AssetManager, _videoManager, _graphicManager, _inputManager, AudioManager, ui,
            signonPanel, titleScreens, highScoresScreen, demos, netGames, quickKeys, mainGameLoop, cheatCommands, warpRequest, Session,
            new TitleLoopHooks(() => NoWait, () => IntroSong,
                (skill, episode, map) => NewGame(skill, episode, map)));

        // What the actors use of the game (Entities/Actors/ActorWorld.cs); the map hands it to each
        // as it joins the level, taking it (lazily, through the services) once actors are made
        actorWorld = new Entities.Actors.ActorWorld(GameEngineManager, AssetManager, AudioManager, InventoryManager,
            HudMessageManager, MapManager, FactionManager, lineOfSight, doors, positionalSounds, actorSounds, hudMessageDrawing,
            playerWeapons, playerAttacks, playerStats, playersManager, playerView, smartSwitches, LevelAI, Stealth, Objectives, Session);

        // TODO: Remove circular dependencies here
    }

    /*
    =============================================================================

                                 LOCAL CONSTANTS

    =============================================================================
    */
    internal const long BaseFocalLength = GameConstants.FocalLength;

    const int ClassicViewWidth = 256;                   // size of view window
    const int ClassicViewHeight = 144;

    /*
    =============================================================================

                                GLOBAL VARIABLES

    =============================================================================
    */

    //
    // proejection variables
    //
    static int FocalLength { get => Render.FocalLength; set => Render.FocalLength = value; }
    static int ViewScreenX { get => Render.ViewScreenX; set => Render.ViewScreenX = value; }
    static int ViewScreenY { get => Render.ViewScreenY; set => Render.ViewScreenY = value; }
    static int ViewWidth { get => Render.ViewWidth; set => Render.ViewWidth = value; }
    static int ViewHeight { get => Render.ViewHeight; set => Render.ViewHeight = value; }
    static short CenterX { get => Render.CenterX; set => Render.CenterX = value; }
    static int ShootDelta { get => Render.ShootDelta; set => Render.ShootDelta = value; }   // pixels away from CenterX a target can be
    static int HeightNumerator { get => Render.HeightNumerator; set => Render.HeightNumerator = value; }

    static bool GameChosen { get => Session.GameChosen; set => Session.GameChosen = value; }

    //
    // Command line parameter variables
    //
    internal static bool NoWait = false;
    static readonly WarpRequest warpRequest = new();    // --warp, --skill, --start

    /// <summary>Runs the game: what Main does (GameHost.cs)</summary>
    internal static void Run(string[] args)
    {
        ConsoleWindow.OpenIfAsked(args);
        CrashReport.Install();

        // What startup prints is shown on the signon screen as warnings
        CaptureStartupOutput();

        // Relative mod paths are made full here, before GameFolder.Use can move away from the
        // folder they mean
        var (gameParams, modPaths) = StartupArgs.Parse(args, Console.Error);
        NoWait = gameParams.NoWait;
        GameFolder.Use();

        InitServices();
        GameEngineManager.Init(gameParams, modPaths);
        SetNetParams(gameParams);     // --host, --join, --port, --name (Networking/NetGames.cs)
        warpRequest.SetParams(gameParams);  // --warp, --skill, --start (Gameplay/WarpRequest.cs)

        // The Mods menu's choices (mods.cfg, per game), then the command line's
        var configMods = ModsConfig.Read(GameEngineManager.GetConfigFilePath(ModsConfig.FileName));
        try
        {
            AssetManager.Load(GameEngineManager.GamePackId, GameEngineManager.GameReleaseId, configMods.Concat(modPaths));
            // Animated pictures in menus and the status bar play in real time; the view's walls,
            // flats and sky go by the level's clock instead (WorldTexture)
            AssetManager.AnimationClock = () => (int)GameEngineManager.GetTimeCount();
        }
        catch (DataFilesException e)
        {
            // Nothing is set up yet to shut down, and there are no settings to keep
            Console.Error.WriteLine(e.Message);
            GameEngineManager.Error(e.Message);
            Environment.Exit(1);
        }
        RegisterActorActions();
        LoadScripts();      // after the built-in actions, which a script's can't replace (Scripting/GameScripts.cs)
        Entities.Actors.Monster.CheckClasses(AssetManager.GetActorMetadata());
        FactionManager.CheckClasses(AssetManager.GetActorMetadata());
        TranslationManager.CheckDefinitions(AssetManager.GetActorMetadata(), FactionManager.Table);
        RegisterConsoleCommands();

        //CheckParameters(args); // Remove

        CheckForEpisodes();

        InitGame();

        // After the config is read, so these can override its settings: the controls and binds
        // saved on the last exit, then the player's own autoexec.cfg, then any --exec commands.
        _consoleManager.ExecFile(GameEngineManager.GetConfigFilePath(GameEngineManager.ControlsFileName));
        _consoleManager.ExecFile(GameEngineManager.GetConfigFilePath(GameEngineManager.BindsFileName));
        GameEngineManager.SettingsLoaded = true;      // from here on, saving them keeps what was loaded
        _consoleManager.ExecFile(GameEngineManager.GetConfigFilePath(GameEngineManager.AutoexecFileName));

        // --cheats: the debug keys and cheat commands from the start, as Shift+Alt+Backspace opens them
        if (gameParams.Cheats)
            DebugOk = 1;

        if (!string.IsNullOrWhiteSpace(gameParams.Exec))
            _consoleManager.Execute(gameParams.Exec);

        DemoLoop();

        GameEngineManager.Quit("Demo loop exited???");
    }

    private static void InitGame()
    {
        bool didjukebox = false;
        var theme = AssetManager.FindInGamePack<ColorThemeAsset>("colors");
        _videoManager.Init(theme, GameEngineManager.ReadVideoConfig());
        _graphicManager.MenuBackdrop = GameEngineManager.GetGameInfo().MenuBackdrop;
        _graphicManager.MenuStripe = GameEngineManager.GetGameInfo().MenuStripe;
        ReadFadeStyles();
        _inputManager.Init(_videoManager.FullScreen);
        
        PixelAngle = new short[_videoManager.ScreenWidth];
        WallHeight = new short[_videoManager.ScreenWidth];
        _videoManager.VideoModeChanged += OnVideoModeChanged;

        //AppDomain.CurrentDomain.ProcessExit += (s, e) => SDL.SDL_Quit();

        SignonScreen();

        _videoManager.Update();

        InitRandomOnce();

        //
        // build some tables
        //
        InitDigiMap();

        InitHighScores();
        GameEngineManager.ReadConfig();

        // Set "Read This" back to standard active
        FindMenuItem(MainMenu, "readthis")?.Active = 1;
        MainItems.CurPos = 0;

        //
        // HOLDING DOWN 'M' KEY?
        //

        if (_inputManager.IsKeyDown(ScanCodes.M))
        {
            DoJukebox();
            didjukebox = true;
        }

        //
        // load in and lock down some basic chunks
        //
        BuildTables();          // trig tables

        NewViewSize(ViewSize);

        //
        // initialize variables
        //
        _videoManager.InitRedShifts();

        var startupLines = StopCapturingStartupOutput();
        if (!didjukebox)
            FinishSignon(startupLines);
    }

    // A new render size needs per-column tables for the new width and the view placed again;
    // FullScreen takes the mouse, a window gives it back.
    private static void OnVideoModeChanged(object? sender, VideoSettings previous)
    {
        var current = _videoManager.Settings;

        if (current.RenderWidth != previous.RenderWidth || current.RenderHeight != previous.RenderHeight
            || current.EffectiveUiScale != previous.EffectiveUiScale)
        {
            PixelAngle = new short[_videoManager.ScreenWidth];
            WallHeight = new short[_videoManager.ScreenWidth];
            NewViewSize(ViewSize);
        }

        if (current.Fullscreen != previous.Fullscreen)
            _inputManager.SetMouseGrab(current.Fullscreen);
    }

    // Fade styles and lengths, the death screen and the weapon in hand, from game-info
    // (Gameplay/GameInfoSettings.cs); ordinary fades use _videoManager.FadeStyle and FadeTics
    static GameInfoSettings gameInfoSettings = new();
    internal static FadeStyle LevelFadeStyle => gameInfoSettings.LevelFadeStyle;
    internal static int LevelFadeTics => gameInfoSettings.LevelFadeTics;
    internal static int DeathTurnSpeed => gameInfoSettings.DeathTurnSpeed;
    internal static int? DeathDropHeight => gameInfoSettings.DeathDropHeight;
    internal static int DeathDropSpeed => gameInfoSettings.DeathDropSpeed;
    internal static double WeaponScale => gameInfoSettings.WeaponScale;
    internal static readonly WeaponBob WeaponBob = new();

    private static void ReadFadeStyles()
    {
        gameInfoSettings = GameInfoSettings.Read(GameEngineManager.GetGameInfo());
        _videoManager.FadeStyle = gameInfoSettings.ScreenFadeStyle;
        _videoManager.FadeTics = gameInfoSettings.ScreenFadeTics;
        WeaponBob.Enabled = gameInfoSettings.WeaponBob;
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
    /// <summary>The class the next new game is played as (the `PlayerClass` command); null for the default</summary>
    internal static string? NewGamePlayerClass { get => newGames.NewGamePlayerClass; set => newGames.NewGamePlayerClass = value; }
    internal static void NewGame(short difficulty, EpisodeInfo epInfo, MapInfo mapInfo, string? playerClass = null) =>
        newGames.NewGame(difficulty, epInfo, mapInfo, playerClass);
    private record DigiMapEntry
    {
        public DigiMapEntry(string sound, int index, int channel)
        {
            this.Sound = sound;
            this.Index = index;
            this.Channel = channel;
        }

        public string Sound;
        public int Index;
        public int Channel;
    }

    static void InitDigiMap()
    {
      //  foreach (var sound in DigitizedSoundMappings.NameIndexMap)
      //  {
         //   AudioManager.SD_PrepareSound(sound);
     //   }
    }

    // The view's size and the projection (Rendering/ViewSetup.cs)
    static ViewSetup viewSetup = null!;     // made in InitServices

    internal static void NewViewSize(int width) => viewSetup.NewViewSize(width);
    // Where the play area is, between the status bars (Ui/PlayArea.cs)
    static PlayArea playArea = null!;       // made in InitServices
    internal static void ShowViewSize(int width) => viewSetup.ShowViewSize(width);

    internal static void DoJukebox() => jukebox.DoJukebox();

    // The sine, cosine and fine tangent tables (Rendering/TrigTables.cs)
    internal static void BuildTables() => TrigTables.BuildTables();
}
