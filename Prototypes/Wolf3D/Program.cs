using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using SDL2;
using System;
using Wolf3D.Assets;
using Wolf3D.Configuration;
using Wolf3D.Constants;
using Wolf3D.DependencyInjection;
using Wolf3D.Extensions;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    private static VideoManager _videoManager;
    private static AudioManager _audioManager;
    private static InputManager _inputManager;
    private static GameEngineManager _gameEngineManager;
    private static GraphicManager _graphicManager;
    private static MapManager _mapManager;
    private static AssetManager _assetManager;
    private static InventoryManager _inventoryManager;
    private static ConsoleManager _consoleManager;

    public Program()
    {
        var services = new ServiceCollection();
        services.AddTransient(typeof(Lazy<>), typeof(Lazier<>));
        services.AddSingleton<GameEngineManager>();
        services.AddSingleton<VideoManager>();
        services.AddSingleton<AudioManager>();
        services.AddSingleton<InputManager>();
        services.AddSingleton<GraphicManager>();
        services.AddSingleton<MapManager>();
        services.AddSingleton<AssetManager>();
        services.AddSingleton<InventoryManager>();
        services.AddSingleton<ConsoleManager>();

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
        _mapManager = serviceProvider.GetRequiredService<MapManager>();
        _assetManager = serviceProvider.GetRequiredService<AssetManager>();
        _inventoryManager = serviceProvider.GetRequiredService<InventoryManager>();
        _consoleManager = serviceProvider.GetRequiredService<ConsoleManager>();

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
    static short centerx, centery;
    static int shootdelta;           // pixels away from centerx a target can be
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
        var gameParams = Parser.Default.ParseArguments<GameParams>(args); // Move into gamemanager, add unit tests
        // TODO: gameParams, handle errors?
        new Program();
        _gameEngineManager.Init(gameParams.Value);
        _assetManager.Load();
        RegisterActorActions();
        RegisterConsoleCommands();

        //CheckParameters(args); // Remove

        CheckForEpisodes();

        InitGame();

        // After the config is read, so these can override its settings: the binds saved on the
        // last exit, then the player's own autoexec.cfg, then any --exec commands.
        _consoleManager.ExecFile(_gameEngineManager.GetConfigFilePath(GameEngineManager.BindsFileName));
        _consoleManager.ExecFile(_gameEngineManager.GetConfigFilePath(GameEngineManager.AutoexecFileName));

        if (!string.IsNullOrWhiteSpace(gameParams.Value.Exec))
            _consoleManager.Execute(gameParams.Value.Exec);

        DemoLoop();

        _gameEngineManager.Quit("Demo loop exited???");
    }

    private static void InitGame()
    {
        bool didjukebox = false;
        var theme = _assetManager.GetColors("wolf3d-theme");
        _videoManager.Init(theme!);
        _inputManager.Init(_videoManager.fullscreen);
        
        pixelangle = new short[_videoManager.screenWidth];
        wallheight = new short[_videoManager.screenWidth];

        //AppDomain.CurrentDomain.ProcessExit += (s, e) => SDL.SDL_Quit();

        SignonScreen();

        _videoManager.Update();

        US_Startup();

        //
        // build some tables
        //
        InitDigiMap();

        _gameEngineManager.ReadConfig();

        SetupSaveGames();

        //
        // HOLDING DOWN 'M' KEY?
        //

        if (_inputManager.IsKeyDown(ScanCodes.sc_M))
        {
            DoJukebox();
            didjukebox = true;
        }
        else
            //
            // draw intro screen stuff
            //
            IntroScreen();

        //
        // load in and lock down some basic chunks
        //
        BuildTables();          // trig tables
        SetupWalls();

        NewViewSize(viewsize);

        //
        // initialize variables
        //
        _videoManager.InitRedShifts();

        if (!didjukebox)
            FinishSignon();
    }

    internal static void SetupWalls()
    {
        int i;

        horizwall[0] = 0;
        vertwall[0] = 0;

        for (i = 1; i < MAXWALLTILES; i++)
        {
            horizwall[i] = (ushort)((i - 1) * 2);
            vertwall[i] = (ushort)((i - 1) * 2 + 1);
        }
    }

    private static void SignonScreen()
    {
        _graphicManager.DrawPic("wolf3d-signon", 0, 0); // TODO: Pull this value from a gamepack configuration
    }

    private static void FinishSignon()
    {
        if (_gameEngineManager.GameType == GameType.SpearOfDestiny)
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
            WindowX = 0;
            WindowW = 320;
            PrintY = 190;

            SETFONTCOLOR("Bright Yellow", "Maroon");
            fontnumber = "SmallFont";
            US_CPrint("Press a key"); // "Oprima una tecla"

            _videoManager.Update();

            if (!param_nowait)
                _inputManager.Ack();

            _videoManager.Bar(0, 189, 300, 11, "Maroon");

            PrintY = 190;
            SETFONTCOLOR("Lime", "Maroon");

            US_CPrint("Working..."); // "pensando..."

            _videoManager.Update();
        }
        SETFONTCOLOR("Black", "White");
    }

    private static void DemoLoop()
    {
        int LastDemo = 0;

        //
        // main game cycle
        //
        if (!param_nowait)
            NonShareware();

        StartCPMusic(INTROSONG);

        if (!param_nowait)
            PG13();

        while (true)
        {
            while (!param_nowait)
            {
                //
                // title page
                //
                _graphicManager.DrawPic("title", 0, 0);
                _videoManager.Update();
                _videoManager.FadeIn();

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

            if (startgame || loadedgame)
            {
                GameLoop();
                if (!param_nowait)
                {
                    _videoManager.FadeOut();
                    StartCPMusic(INTROSONG);
                }
            }
        }
    }

    internal static void NewGame(difficultytypes difficulty, EpisodeInfo epInfo, MapInfo mapInfo)
    {
        gamestate = new gametype();
        gamestate.difficulty = difficulty;
        GiveStartingInventory();

        gamestate.health = 100;
        gamestate.lives = 3;
        gamestate.nextextra = EXTRAPOINTS;
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
            SetViewSize((uint)_videoManager.screenWidth, (uint)(_videoManager.screenHeight - _videoManager.scaleFactor * STATUSLINES));
        else
            SetViewSize((uint)(width * 16 * _videoManager.screenWidth / 320), (uint)(width * 16 * HEIGHTRATIO * _videoManager.screenHeight / 200));
    }

    internal static bool SetViewSize(uint width, uint height)
    {
        viewwidth = (int)(width & ~15);                  // must be divisable by 16
        viewheight = (int)(height & ~1);                 // must be even
        centerx = (short)(viewwidth / 2 - 1);
        centery = (short)(viewheight / 2);
        shootdelta = viewwidth / 10;
        if (viewheight == _videoManager.screenHeight)
            viewscreenx = viewscreeny = (int)(screenofs = 0);
        else
        {
            viewscreenx = (_videoManager.screenWidth - viewwidth) / 2;
            viewscreeny = (_videoManager.screenHeight - _videoManager.scaleFactor * STATUSLINES - viewheight) / 2;
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
        uint start;
        string[] songs =
        {
            "GETTHEM",
            "SEARCHN",
            "POW",
            "SUSPENSE",
            "WARMARCH",
            "CORNER",

            "NAZI_OMI",
            "PREGNANT",
            "GOINGAFT",
            "HEADACHE",
            "DUNGEON",
            "ULTIMATE",

            "INTROCW3",
            "NAZI_RAP",
            "TWELFTH",
            "ZEROHOUR",
            "VICMARCH",
            "PACMAN"
        };

        _inputManager.ClearKeysDown();
        //if (!AdLibPresent && !SoundBlasterPresent)
        //    return;

        MenuFadeOut();

        start = ((SDL.SDL_GetTicks() / 10) % 3) * 6;

        //CA_LoadAllSounds();

        fontnumber = "LargeFont";
        ClearMScreen();
        _graphicManager.DrawPic("c_mouselback", 112, 184);
        DrawStripes(10);
        SETFONTCOLOR("TEXTCOLOR", "BKGDCOLOR");

        DrawWindow(CTL_X - 2, CTL_Y - 6, 280, 13 * 7, "BKGDCOLOR");

        DrawMenu(MusicItems, MusicMenu/*[start]*/);

        SETFONTCOLOR("READHCOLOR", "BKGDCOLOR");
        PrintY = 15;
        WindowX = 0;
        WindowY = 320;
        US_CPrint("Robert's Jukebox");

        SETFONTCOLOR("TEXTCOLOR", "BKGDCOLOR");
        _videoManager.Update();
        MenuFadeIn();

        do
        {
            which = HandleMenu(MusicItems, MusicMenu/*[start]*/, null);
            if (which >= 0)
            {
                if (lastsong >= 0)
                    MusicMenu[start + lastsong].active = 1;

                StartCPMusic(songs[start + which]);
                MusicMenu[start + which].active = 2;
                DrawMenu(MusicItems, MusicMenu/*[start]*/);
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
            viewheight = _videoManager.screenHeight - _videoManager.scaleFactor * STATUSLINES;
            DrawPlayBorder();
        }
        else
        {
            viewwidth = width * 16 * _videoManager.screenWidth / 320;
            viewheight = (int)(width * 16 * HEIGHTRATIO * _videoManager.screenHeight / 200);
            DrawPlayBorder();
        }

        viewwidth = oldwidth;
        viewheight = oldheight;
    }
}
