using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SDL2;
using System;
using Wolf3D.Assets;
using Wolf3D.Configuration;
using Wolf3D.Constants;
using Wolf3D.DependencyInjection;
using Wolf3D.Extensions;
using Wolf3D.Fonts;
using Wolf3D.Loaders;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    private static VideoManager _videoManager;
    private static AudioManager _audioManager;
    private static InputManager _inputManager;
    private static GameEngineManager _gameEngineManager;
    private static GraphicManager _graphicManager;
    private static FontManager _fontManager;
    private static MapManager _mapManager;
    private static AssetManager _assetManager;
    private static InventoryManager _inventoryManager;
    private static ConsoleManager _consoleManager;
    private static AutomapManager _automapManager;
    private static HudMessageManager _hudMessageManager;

    public Program()
    {
        var services = new ServiceCollection();
        services.AddTransient(typeof(Lazy<>), typeof(Lazier<>));
        services.AddSingleton<GameEngineManager>();
        services.AddSingleton<VideoManager>();
        services.AddSingleton<AudioManager>();
        services.AddSingleton<InputManager>();
        services.AddSingleton<GraphicManager>();
        services.AddSingleton<FontManager>();
        services.AddSingleton<MapManager>();
        services.AddSingleton<AssetManager>();
        services.AddSingleton<InventoryManager>();
        services.AddSingleton<ConsoleManager>();
        services.AddSingleton<AutomapManager>();
        services.AddSingleton<HudMessageManager>();

        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();

        //_audioManager = new AudioManager(
       //     new Lazy<AssetManager>(
       //         () => serviceProvider.GetRequiredService<AssetManager>()));

        _gameEngineManager = serviceProvider.GetRequiredService<GameEngineManager>();
        _videoManager = serviceProvider.GetRequiredService<VideoManager>();
        _audioManager = serviceProvider.GetRequiredService<AudioManager>();
        _inputManager = serviceProvider.GetRequiredService<InputManager>();
        _graphicManager = serviceProvider.GetRequiredService<GraphicManager>();
        _fontManager = serviceProvider.GetRequiredService<FontManager>();
        _mapManager = serviceProvider.GetRequiredService<MapManager>();
        _assetManager = serviceProvider.GetRequiredService<AssetManager>();
        _inventoryManager = serviceProvider.GetRequiredService<InventoryManager>();
        _inventoryManager.PlayerClass = () => gamestate.playerclass;
        _consoleManager = serviceProvider.GetRequiredService<ConsoleManager>();
        _automapManager = serviceProvider.GetRequiredService<AutomapManager>();
        _hudMessageManager = serviceProvider.GetRequiredService<HudMessageManager>();

        // TODO: Remove circular dependencies here
        //_videoManager = new();
        //_mapManager = new();
        //_gameEngineManager = new(_videoManager, _inputManager);
        //_inputManager = new();
        //_graphicManager = new(_videoManager);
    }

    /*
    =============================================================================

                                 LOCAL CONSTANTS

    =============================================================================
    */
    const long FOCALLENGTH = (0x5700L);               // in global coordinates
    const int VIEWGLOBAL = 0x10000;               // globals visable flush to wall

    const int VIEWWIDTH = 256;                   // size of view window
    const int VIEWHEIGHT = 144;

    /*
    =============================================================================

                                GLOBAL VARIABLES

    =============================================================================
    */

    static readonly int[] dirangle = {0,ANGLES/8,2*ANGLES/8,3*ANGLES/8,4*ANGLES/8,
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
    static int scale;
    static int heightnumerator;

    static bool startgame;
    internal static bool loadedgame;
    internal static int mouseadjustment;

    //
    // Command line parameter variables
    //
    internal static bool param_nowait = false;
    internal static int param_mission = 0;
    internal static bool param_goodtimes = false;

    private static void Main(string[] args)
    {
        // Bad arguments are reported by the parser and otherwise ignored: run with the defaults.
        // --file can be given more than once.
        using var parser = new Parser(settings =>
        {
            settings.HelpWriter = Console.Error;
            settings.AllowMultiInstance = true;
        });
        var gameParams = parser.ParseArguments<GameParams>(args).Value ?? new GameParams(); // Move into gamemanager, add unit tests

        // Relative mod paths mean the folder the game was started in, so pin them down before
        // UseGameFolder can move away from it
        var modPaths = gameParams.Files.Concat(gameParams.Paths).Select(ModSource.FullPathIfExists).ToList();
        UseGameFolder();

        new Program();
        _gameEngineManager.Init(gameParams);

        // The Mods menu's choices (mods.cfg, per game), then the command line's
        var configMods = ModsConfig.Read(_gameEngineManager.GetConfigFilePath(ModsConfig.FileName));
        _assetManager.Load(_gameEngineManager.GamePackId, _gameEngineManager.GameReleaseId, configMods.Concat(modPaths));
        RegisterActorActions();
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

        if (!string.IsNullOrWhiteSpace(gameParams.Exec))
            _consoleManager.Execute(gameParams.Exec);

        DemoLoop();

        _gameEngineManager.Quit("Demo loop exited???");
    }

    /// <summary>
    /// Files dropped on the exe start it in some other folder (often the dropped files' own), but
    /// pfwolf.pk3 and the game's data files are read from the working folder. When they aren't
    /// there and are beside the exe, work from there instead.
    /// </summary>
    private static void UseGameFolder()
    {
        const string basePk3 = "pfwolf.pk3";
        if (!File.Exists(basePk3) && File.Exists(Path.Combine(AppContext.BaseDirectory, basePk3)))
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
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

        if (!didjukebox)
            FinishSignon();
    }

    // A new render size needs per-column tables for the new width and the view placed again;
    // fullscreen takes the mouse, a window gives it back.
    private static void OnVideoModeChanged(object? sender, VideoSettings previous)
    {
        var current = _videoManager.Settings;

        if (current.RenderScale != previous.RenderScale)
        {
            pixelangle = new short[_videoManager.screenWidth];
            wallheight = new short[_videoManager.screenWidth];
            NewViewSize(viewsize);
        }

        if (current.Fullscreen != previous.Fullscreen)
            _inputManager.SetMouseGrab(current.Fullscreen);
    }

    // Fade styles and lengths (tics) from game-info; ordinary fades use _videoManager.FadeStyle
    // and FadeTics (screen-fade-style, screen-fade-tics)
    internal static FadeStyle menuFadeStyle, deathFadeStyle, levelFadeStyle;
    internal static int menuFadeTics, deathFadeTics, levelFadeTics;
    internal static string deathFadeColor = "Maroon";
    // The death screen's turn to face the killer (degrees a tic) and hold on the color (tics)
    internal static int deathTurnSpeed = 2, deathHoldTics = 100;

    private static void ReadFadeStyles()
    {
        var gameInfo = _gameEngineManager.GetGameInfo();

        _videoManager.FadeStyle = ParseFadeStyle(gameInfo.ScreenFadeStyle, "screen-fade-style", FadeStyle.Palette);
        _videoManager.FadeTics = ParseFadeTics(gameInfo.ScreenFadeTics, "screen-fade-tics");
        menuFadeStyle = ParseFadeStyle(gameInfo.MenuFadeStyle, "menu-fade-style", _videoManager.FadeStyle);
        menuFadeTics = ParseFadeTics(gameInfo.MenuFadeTics, "menu-fade-tics") ?? 20;
        deathFadeStyle = ParseFadeStyle(gameInfo.DeathFadeStyle, "death-fade-style", FadeStyle.Fizzle);
        deathFadeTics = ParseFadeTics(gameInfo.DeathFadeTics, "death-fade-tics") ?? 70;
        deathFadeColor = string.IsNullOrWhiteSpace(gameInfo.DeathFadeColor) ? "Maroon" : gameInfo.DeathFadeColor;
        deathTurnSpeed = Math.Max(gameInfo.DeathTurnSpeed ?? 2, 1);     // 0 would never finish turning
        deathHoldTics = Math.Max(gameInfo.DeathHoldTics ?? 100, 0);
        levelFadeStyle = ParseFadeStyle(gameInfo.LevelFadeStyle, "level-fade-style", FadeStyle.Fizzle);
        levelFadeTics = ParseFadeTics(gameInfo.LevelFadeTics, "level-fade-tics") ?? 20;
    }

    private static int? ParseFadeTics(int? value, string key)
    {
        if (value is null or > 0)
            return value;

        Console.WriteLine($"{key} must be more than 0 tics in game-info, not {value}; using the default");
        return null;
    }

    private static FadeStyle ParseFadeStyle(string? value, string key, FadeStyle fallback)
    {
        if (string.IsNullOrEmpty(value))
            return fallback;

        // By name only: Enum.TryParse would also take numbers
        if (Enum.GetNames<FadeStyle>().Contains(value, StringComparer.OrdinalIgnoreCase))
            return Enum.Parse<FadeStyle>(value, ignoreCase: true);

        Console.WriteLine($"Unknown {key} '{value}' in game-info, using {fallback.ToString().ToLowerInvariant()} instead");
        return fallback;
    }

    private static void SignonScreen()
    {
        _graphicManager.DrawPic(_gameEngineManager.GetGameInfo().Signon.Pic ?? "", 0, 0);
    }

    /// <summary>
    /// The game-info title-pics, each drawn below the one before
    /// </summary>
    private static void DrawTitle()
    {
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

    private static void FinishSignon()
    {
        if (!_gameEngineManager.GetGameInfo().Signon.PressAKey)
        {
            // TODO: In the future, the signon screen will not be different for SPEAR, and this conditional will not be required
            // The hope is that the Signon will show the stats of loading chunks, what settings are configured, etc
            // The graphic may change, but the logic and "Console" viewport should remain the same`
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
        int LastDemo = 0;

        //
        // main game cycle
        //
        if (!param_nowait && _gameEngineManager.GetGameInfo().NonSharewareNotice)
            NonShareware();

        StartCPMusic(INTROSONG);

        if (!param_nowait)
            PG13();

        while (true)
        {
            // recorddemo and playdemo, from the command line's --exec or a game they ended
            if (RecordPendingDemo())
            {
                RunStartedGame();       // New Game from the menu ends a recording and starts one
                continue;
            }
            PlayPendingDemo();

            while (!param_nowait)
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
                PlayDemo(LastDemo++ % 4);
                if (playstate == playstatetypes.ex_abort)
                    break;
                _videoManager.FadeOut();
                if (_videoManager.screenHeight % 200 != 0)
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
        gamestate.playerclass = playerClass ?? newGamePlayerClass ?? DefaultPlayerClass;
        GiveStartingInventory();

        gamestate.health = StartingHealth;
        gamestate.lives = StartingLives;
        gamestate.nextextra = ExtraLifeScore;
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
    internal static int PlayAreaHeight => _videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES + TOPLINES);

    /// <summary>Where the play area (view and border) starts down the screen, in screen pixels: below any top status bar</summary>
    internal static int PlayAreaTop => _videoManager.scaleFactor * TOPLINES;

    /// <summary>
    /// A bordered view's height at view size <paramref name="width"/>. The sizes are laid out for
    /// Wolf3D's 40 line status bar: with a taller one (Blake Stone's), the view stops short of it,
    /// leaving room for the border lines round it.
    /// </summary>
    private static int BorderedViewHeight(int width)
    {
        int height = (int)(width * 16 * HEIGHTRATIO * _videoManager.screenHeight / 200);
        int room = PlayAreaHeight - _videoManager.scaleFactor * 2;
        return Math.Min(height, room);
    }

    internal static bool SetViewSize(uint width, uint height)
    {
        viewwidth = (int)(width & ~15);                  // must be divisable by 16
        viewheight = (int)(height & ~1);                 // must be even
        centerx = (short)(viewwidth / 2 - 1);
        centery = basecentery = (short)(viewheight / 2);
        shootdelta = viewwidth / 10;
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
        // and sprite x calculations
        //
        scale = (int) (halfview * facedist / (VIEWGLOBAL / 2));

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
            tang = (int)i * VIEWGLOBAL / viewwidth / facedist;
            angle = (float)Math.Atan(tang);
            intang = (int)(angle * radtoint);
            pixelangle[halfview - 1 - i] = (short)intang;
            pixelangle[halfview + i] = (short)-intang;
        }
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
