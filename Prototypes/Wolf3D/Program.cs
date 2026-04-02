using CommandLine;
using SDL2;
using Wolf3D.Configuration;
using Wolf3D.Managers;
using Wolf3D.Mappers;
using Microsoft.Extensions.DependencyInjection;
using System;
using Wolf3D.Extensions;

namespace Wolf3D;

internal partial class Program
{
    private static VideoManager _videoManager;
    private static InputManager _inputManager;
    private static GameEngineManager _gameEngineManager;
    private static GraphicManager _graphicManager;
    private static MapManager _mapManager;
    private static AssetManager _assetManager;

    public Program()
    {
        var services = new ServiceCollection();
        services.AddSingleton<GameEngineManager>();
        services.AddSingleton<VideoManager>();
        services.AddSingleton<InputManager>();
        services.AddSingleton<GraphicManager>();
        services.AddSingleton<MapManager>();
        services.AddSingleton<AssetManager>();

        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();

        _gameEngineManager = serviceProvider.GetRequiredService<GameEngineManager>();
        _videoManager = serviceProvider.GetRequiredService<VideoManager>();
        _inputManager = serviceProvider.GetRequiredService<InputManager>();
        _graphicManager = serviceProvider.GetRequiredService<GraphicManager>();
        _mapManager = serviceProvider.GetRequiredService<MapManager>();
        _assetManager = serviceProvider.GetRequiredService<AssetManager>();

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
    static bool loadedgame;
    internal static int mouseadjustment;

    //
    // Command line parameter variables
    //
    internal static bool param_nowait = false;
    
    internal static int param_audiobuffer = DEFAULT_AUDIO_BUFFER_SIZE;
    internal static int param_samplerate = 44100;
    internal static int param_mission = 0;
    internal static bool param_goodtimes = false;
    internal static bool param_ignorenumchunks = false;

    private static void Main(string[] args)
    {
        var gameParams = Parser.Default.ParseArguments<GameParams>(args); // Move into gamemanager, add unit tests
        // TODO: gameParams, handle errors?
        new Program();
        _gameEngineManager.Init(gameParams.Value);

        //CheckParameters(args); // Remove

        CheckForEpisodes();

        InitGame();

        DemoLoop();

        _gameEngineManager.Quit("Demo loop exited???");
    }

    private static void InitGame()
    {
        bool didjukebox = false;
        var theme = _assetManager.GetColors("wolf3d-theme");
        // var palette = _assetManager.GetPalette("wolfpal");
        _videoManager.Init(theme!);
        _inputManager.Init(_videoManager.fullscreen);
        
        pixelangle = new short[_videoManager.screenWidth];
        wallheight = new short[_videoManager.screenWidth];

        // TODO: Move to AudioManager
        if (SDL.SDL_InitSubSystem(SDL.SDL_INIT_AUDIO) < 0)
        {
            _gameEngineManager.Quit($"Could not initialize SDL: {SDL.SDL_GetError()}");
        }

        //AppDomain.CurrentDomain.ProcessExit += (s, e) => SDL.SDL_Quit();

        SignonScreen();

        _videoManager.Update();

        PM_Startup();
        SD_Startup();
        _graphicManager.Init(extension, param_ignorenumchunks);
        _mapManager.Init(extension);
        CA_Startup();
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
        _graphicManager.DrawPic("signon", 0, 0);
    }

    private static void FinishSignon()
    {
        // TODO: Spear of Destiny support
        //if (Game == "SPEAR")
        //{
        //    _videoManager.Update();

        //    if (!param_nowait)
        //        VW_WaitVBL(3 * 70);
        //}
        //else {}
        _videoManager.Bar(0, 189, 300, 11, "Maroon");
        WindowX = 0;
        WindowW = 320;
        PrintY = 190;

        SETFONTCOLOR("Bright Yellow", "Maroon");
        US_CPrint("Press a key"); // "Oprima una tecla"

        _videoManager.Update();

        if (!param_nowait)
            _inputManager.Ack();

        _videoManager.Bar(0, 189, 300, 11, "Maroon");

        PrintY = 190;
        SETFONTCOLOR("Lime", "Maroon");

        US_CPrint("Working..."); // "pensando..."

        _videoManager.Update();

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

                if (_inputManager.UserInput(TickBase * 15))
                    break;
                _videoManager.FadeOut();

                //
                // credits page
                //
                _graphicManager.DrawPic("credits", 0, 0);
                _videoManager.Update();
                _videoManager.FadeIn();
                if (_inputManager.UserInput(TickBase * 10))
                    break;
                _videoManager.FadeOut();

                //
                // high scores
                //
                DrawHighScores();
                _videoManager.Update();
                _videoManager.FadeIn();

                if (_inputManager.UserInput(TickBase * 10))
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
        gamestate.weapon = gamestate.bestweapon = gamestate.chosenweapon = weapontypes.wp_pistol;

        gamestate.health = 100;
        gamestate.ammo = STARTAMMO;
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
        for (int i = 0; i < wolfdigimap.Length; i++)
        {
            var map = wolfdigimap[i];
            var mapIndex = AudioMappings.SoundKeys.IndexOf(map.sound);
            
            // Prevent outside of array exceptions
            if (mapIndex >= 0 || mapIndex < DigiMap.Length)
                DigiMap[mapIndex] = map.index;

            DigiChannel[map.index] = map.channel;
            SD_PrepareSound(map.index);
        }
    }


    static digimap[] wolfdigimap =
    {
        new ("HALTSND",                0,  -1),
        new ("DOGBARKSND",             1,  -1),
        new ("CLOSEDOORSND",           2,  -1),
        new ("OPENDOORSND",            3,  -1),
        new ("ATKMACHINEGUNSND",       4,   0),
        new ("ATKPISTOLSND",           5,   0),
        new ("ATKGATLINGSND",          6,   0),
        new ("SCHUTZADSND",            7,  -1),
        new ("GUTENTAGSND",            8,  -1),
        new ("MUTTISND",               9,  -1),
        new ("BOSSFIRESND",            10,  1),
        new ("SSFIRESND",              11, -1),
        new ("DEATHSCREAM1SND",        12, -1),
        new ("DEATHSCREAM2SND",        13, -1),
        new ("DEATHSCREAM3SND",        13, -1),
        new ("TAKEDAMAGESND",          14, -1),
        new ("PUSHWALLSND",            15, -1),

        new ("LEBENSND",               20, -1),
        new ("NAZIFIRESND",            21, -1),
        new ("SLURPIESND",             22, -1),

        new ("YEAHSND",                32, -1),
        // Thall other episodes
        new ("DOGDEATHSND",            16, -1),
        new ("AHHHGSND",               17, -1),
        new ("DIESND",                 18, -1),
        new ("EVASND",                 19, -1),

        new ("TOT_HUNDSND",            23, -1),
        new ("MEINGOTTSND",            24, -1),
        new ("SCHABBSHASND",           25, -1),
        new ("HITLERHASND",            26, -1),
        new ("SPIONSND",               27, -1),
        new ("NEINSOVASSND",           28, -1),
        new ("DOGATTACKSND",           29, -1),
        new ("LEVELDONESND",           30, -1),
        new ("MECHSTEPSND",            31, -1),

        new ("SCHEISTSND",             33, -1),
        new ("DEATHSCREAM4SND",        34, -1),         // AIIEEE
        new ("DEATHSCREAM5SND",        35, -1),         // DEE-DEE
        new ("DONNERSND",              36, -1),         // EPISODE 4 BOSS DIE
        new ("EINESND",                37, -1),         // EPISODE 4 BOSS SIGHTING
        new ("ERLAUBENSND",            38, -1),         // EPISODE 6 BOSS SIGHTING
        new ("DEATHSCREAM6SND",        39, -1),         // FART
        new ("DEATHSCREAM7SND",        40, -1),         // GASP
        new ("DEATHSCREAM8SND",        41, -1),         // GUH-BOY!
        new ("DEATHSCREAM9SND",        42, -1),         // AH GEEZ!
        new ("KEINSND",                43, -1),         // EPISODE 5 BOSS SIGHTING
        new ("MEINSND",                44, -1),         // EPISODE 6 BOSS DIE
        new ("ROSESND",                45, -1),         // EPISODE 5 BOSS DIE
    };

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
        heightnumerator = (int)((TILEGLOBAL * scale) >> 6);

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

    internal static bool LoadTheGame(BinaryReader br, int x, int y)
    {
        var language = _assetManager.GetText("en-us");
        int i, j;
        ushort laststatobjnum;
        ushort tile;
        int checksum, oldchecksum;

        checksum = 0;

        DiskFlopAnim(x, y);
        gamestate.Read(br);
        checksum = DoChecksum(gamestate.AsBytes(), checksum);

        DiskFlopAnim(x, y);
        var levelRatioData = new List<byte>();
        foreach (var lr in LevelRatios)
        {
            lr.Read(br);
            levelRatioData.AddRange(lr.AsBytes());
        }

        checksum = DoChecksum(levelRatioData.ToArray(), checksum);

        DiskFlopAnim(x, y);
        SetupGameLevel();

        DiskFlopAnim(x, y);
        _mapManager.tilemap = br.ReadBytes(_mapManager.tilemap.Length).ToFixedArray(MapManager.MAPSIZE, MapManager.MAPSIZE);
        checksum = DoChecksum(_mapManager.tilemap, checksum);
        DiskFlopAnim(x, y);

        for (i = 0; i < _mapManager.mapwidth; i++)
            for (j = 0; j < _mapManager.mapheight; j++)
            {
                int objHashCode = br.ReadInt32();
                checksum = DoChecksum(objHashCode, checksum);
                _mapManager.actorat[i, j] = objlist2.FirstOrDefault(x => x.GetHashCode() == objHashCode);
            }

        areaconnect = br.ReadBytes(MapConstants.NUMAREAS* MapConstants.NUMAREAS).ToFixedArray(MapConstants.NUMAREAS, MapConstants.NUMAREAS);
        areabyplayer = br.ReadBytes(MapConstants.NUMAREAS);

        DiskFlopAnim(x, y);
        InitActorList();

        while (true)
        {
            objstruct nullobj = new();
            int stateOffset = nullobj.Read(br);
            if (nullobj.active == activetypes.ac_badobject)
                break;

            if (nullobj.obclass == classtypes.playerobj)
            {
                player.Copy(nullobj);
                player.state = PlayerStateList[stateOffset];
            }
            else
            {
                objstruct newobj;
                newobj = GetNewActor();
                newobj.state = EnemyStateList[stateOffset];
                newobj.Copy(nullobj);
            }
        }

        DiskFlopAnim(x, y);
        laststatobjnum = br.ReadUInt16();
        laststatobj = laststatobjnum;
        checksum = DoChecksum(laststatobjnum, checksum);

        DiskFlopAnim(x, y);

        for (var statptr = 0; statptr != laststatobj; statptr++)
        {
            statobj_t statobj = new();
            statobj.Read(br);
            statobjlist[statptr] = statobj;
            checksum = DoChecksum(statobj.AsBytes(), checksum);
        }
        DiskFlopAnim(x, y);
        for (int doorIndex = 0; doorIndex < lastdoorobj; doorIndex++)
        {
            doorobj_t door = new();
            door.Read(br);
            doorobjlist[doorIndex] = door;
            checksum = DoChecksum(door.AsBytes(), checksum);
        }

        DiskFlopAnim(x, y);
        pwallstate = br.ReadUInt16();
        checksum = DoChecksum(pwallstate, checksum);
        pwalltile = br.ReadByte();
        checksum = DoChecksum(pwalltile, checksum);
        pwallx = br.ReadUInt16();
        checksum = DoChecksum(pwallx, checksum);
        pwally = br.ReadUInt16();
        checksum = DoChecksum(pwally, checksum);
        pwalldir = (controldirs)br.ReadByte();
        checksum = DoChecksum((byte)pwalldir, checksum);
        pwallpos = br.ReadUInt16();
        checksum = DoChecksum(pwallpos, checksum);

        if (gamestate.secretcount != 0)
        {
            //
            // assign valid floorcodes under moved pushwalls
            //

            for (y = 0; y<_mapManager.mapheight; y++)
            {
                for (x = 0; x<_mapManager.mapwidth; x++)
                {
                    tile = _mapManager.GetTile(x, y, 0);

                    if (_mapManager.MAPSPOT(x, y,1) == MapConstants.PUSHABLETILE && _mapManager.tilemap[x, y] == 0 && !MapManager.VALIDAREA(tile))
                    {
                        if (MapManager.VALIDAREA(_mapManager.MAPSPOT(x + 1, y, 0)))
                            tile = (ushort)_mapManager.MAPSPOT(x + 1, y, 0);
                        if (MapManager.VALIDAREA(_mapManager.MAPSPOT(x, y - 1, 0)))
                            tile = (ushort)_mapManager.MAPSPOT(x, y - 1, 0);
                        if (MapManager.VALIDAREA(_mapManager.MAPSPOT(x, y + 1, 0)))
                            tile = (ushort)_mapManager.MAPSPOT(x, y + 1, 0);
                        if (MapManager.VALIDAREA(_mapManager.MAPSPOT(x - 1, y, 0)))
                            tile = (ushort)_mapManager.MAPSPOT(x - 1, y, 0);

                        _mapManager.SetMapSpot(x, y,1, 0);
                    }
                }
            }
        }

        Thrust(0, 0);    // set player->areanumber to the floortile you're standing on

        oldchecksum = br.ReadInt32();
        lastgamemusicoffset = br.ReadInt32();

        if (lastgamemusicoffset < 0)
            lastgamemusicoffset = 0;

        if (oldchecksum != checksum)
        {
            Message($"{"$STR_SAVECHT1".ToLanguageText(language)}\n{"$STR_SAVECHT2".ToLanguageText(language)}\n{"$STR_SAVECHT3".ToLanguageText(language)}\n{"$STR_SAVECHT4".ToLanguageText(language)}");

            _inputManager.ClearKeysDown();
            _inputManager.Ack();

            gamestate.oldscore = gamestate.score = 0;
            gamestate.lives = 1;
            gamestate.weapon =
            gamestate.chosenweapon =
            gamestate.bestweapon = weapontypes.wp_pistol;
            gamestate.ammo = 8;
        }

        return true;
    }

    internal static bool SaveTheGame(BinaryWriter bw, int x, int y)
    {
        int i, j;
        int checksum;
        ushort laststatobjnum;
        //objstruct ob;
        objstruct nullobj = new();

        checksum = 0;

        DiskFlopAnim(x, y);
        var gamestateData = gamestate.AsBytes();
        bw.Write(gamestateData);
        checksum = DoChecksum(gamestateData, checksum);

        DiskFlopAnim(x, y);
        var levelRatioData = new List<byte>();
        foreach (var lr in LevelRatios)
        {
            var lrData = lr.AsBytes();
            bw.Write(lrData);
            levelRatioData.AddRange(lrData);
        }
        checksum = DoChecksum(levelRatioData.ToArray(), checksum);

        DiskFlopAnim(x, y);
        bw.Write(_mapManager.tilemap);
        checksum = DoChecksum(_mapManager.tilemap, checksum);
        DiskFlopAnim(x, y);

        for (i = 0; i < _mapManager.mapwidth; i++)
        {
            for (j = 0; j < _mapManager.mapheight; j++)
            {
                var actor = _mapManager.actorat[i, j];
                if (actor is objstruct obj)
                {
                    LinkedListNode<objstruct>? objIndex = objlist2.Find(obj);
                }
                bw.Write((actor?.GetHashCode() ?? 0));
                checksum = DoChecksum((actor?.GetHashCode() ?? 0), checksum);
            }
        }

        bw.Write(areaconnect);
        bw.Write(areabyplayer);

        DiskFlopAnim(x, y);
        foreach (var ob in objlist2)
        //for (int? o = 0; o != null; o = ob.next)
        {
            //ob = objlist[o.Value];
            if (ob == null)
                continue;
            int stateOffset = 0;
            if (ob == player)
                stateOffset = PlayerStateList.IndexOf(ob.state);
            else
                stateOffset = EnemyStateList.IndexOf(ob.state);
            bw.Write(ob.AsBytes(stateOffset));
        }

        nullobj.active = activetypes.ac_badobject;          // end of file marker
        DiskFlopAnim(x, y);
        bw.Write(nullobj.AsBytes(stateOffset: 0));

        DiskFlopAnim(x, y);
        laststatobjnum = (ushort)(laststatobj);
        bw.Write(laststatobjnum);
        checksum = DoChecksum(laststatobjnum, checksum);

        DiskFlopAnim(x, y);
        for (var statptr = 0; statptr != laststatobj; statptr++)
        {
            statobj_t statptr_val = statobjlist[statptr];
            var statptrData = statptr_val.AsBytes();
            bw.Write(statptrData);
            checksum = DoChecksum(statptrData, checksum);
        }

        DiskFlopAnim(x, y);
        for (int doorIndex = 0; doorIndex < lastdoorobj; doorIndex++)
        {
            doorobj_t door = doorobjlist[doorIndex];
            var doorData = door.AsBytes();
            bw.Write(doorData);
            checksum = DoChecksum(doorData, checksum);
        }

        DiskFlopAnim(x, y);
        bw.Write(pwallstate);
        checksum = DoChecksum(pwallstate, checksum);
        bw.Write(pwalltile);
        checksum = DoChecksum(pwalltile, checksum);
        bw.Write(pwallx);
        checksum = DoChecksum(pwallx, checksum);
        bw.Write(pwally);
        checksum = DoChecksum(pwally, checksum);

        bw.Write((byte)pwalldir);
        checksum = DoChecksum((byte)pwalldir, checksum);
        bw.Write(pwallpos);
        checksum = DoChecksum(pwallpos, checksum);

        //
        // write out checksum
        //
        bw.Write(checksum);

        bw.Write(lastgamemusicoffset);

        return true;
    }

    static byte diskflopanim_which = 0;
    internal static void DiskFlopAnim(int x, int y)
    {
        if (x == 0 && y == 0)
            return;

        string[] diskpics = new[] { "c_diskloading1", "c_diskloading2" };

        _graphicManager.DrawPic(diskpics[diskflopanim_which], x, y);
        _videoManager.Update();
        diskflopanim_which ^= 1;
    }

    internal static int DoChecksum(int source, int checksum)
    {
        // Should return same value, as checksum requires 2 bytes
        return DoChecksum(BitConverter.GetBytes(source), checksum);
    }
    internal static int DoChecksum(uint source, int checksum)
    {
        // Should return same value, as checksum requires 2 bytes
        return DoChecksum(BitConverter.GetBytes(source), checksum);
    }

    internal static int DoChecksum(ushort source, int checksum)
    {
        // Should return same value, as checksum requires 2 bytes
        return DoChecksum(BitConverter.GetBytes(source), checksum);
    }

    internal static int DoChecksum(byte[,] source, int checksum)
    {
        return DoChecksum(source.Flatten(), checksum);
    }

    internal static int DoChecksum(byte source, int checksum)
    {
        // Should return same value, as checksum requires 2 bytes
        return DoChecksum([source], checksum);
    }

    internal static int DoChecksum(byte[] source, int checksum)
    {
        uint i;
        int size = source.Length;

        for (i = 0; i < size - 1; i++)
            checksum += source[i] ^ source[i + 1];

        return checksum;
    }

    internal static void DoJukebox()
    {
        int which, lastsong = -1;
        uint start;
        string[] songs =
        {
            "GetThem",
            "Searching",
            "PrisonerOfWar",
            "Suspense",
            "MarchToWar",
            "Corner",

            "NaziAnthem",
            "Lurking",
            "GoingAfterHitler",
            "PoundingHeadache",
            "Dungeons",
            "Ultimate",

            "KillTheSOB",
            "NaziRap",
            "TheTwelfthHour",
            "ZeroHour",
            "VictoryMarch",
            "PacMan"
        };

        _inputManager.ClearKeysDown();
        if (!AdLibPresent && !SoundBlasterPresent)
            return;

        MenuFadeOut();

        start = ((SDL.SDL_GetTicks() / 10) % 3) * 6;

        CA_LoadAllSounds();

        fontnumber = 1;
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
            finetangent[i] = (int)(tang * GLOBAL1);
            finetangent[FINEANGLES / 4 - 1 - i] = (int)((1 / tang) * GLOBAL1);
        }

        //
        // costable overlays sintable with a quarter phase shift
        // ANGLES is assumed to be divisable by four
        //

        float angle = 0;
        float anglestep = (float)(PI / 2 / ANGLEQUAD);
        for (i = 0; i < ANGLEQUAD; i++)
        {
            int value= (int)(GLOBAL1 * Math.Sin(angle));
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
