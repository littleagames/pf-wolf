using SDL2;
using System.Text;

namespace Wolf3D;

internal partial class Program
{
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
    static int mouseadjustment;

    internal const int CONFIG_DIR_SIZE = 256;
    internal static string configdir = string.Empty;
    internal static string configname = "config.";

    //
    // Command line parameter variables
    //
    internal static bool param_debugmode = false;
    internal static bool param_nowait = false;
    internal static difficultytypes param_difficulty = difficultytypes.gd_easy; // default is "normal"
    internal static int param_tedlevel = -1;            // default is not to start a level
    internal static int param_joystickindex = 0;
    internal static int param_audiobuffer = DEFAULT_AUDIO_BUFFER_SIZE;
    internal static int param_joystickhat = -1;
    internal static int param_samplerate = 7042;
    internal static int param_mission = 0;
    internal static bool param_goodtimes = false;
    internal static bool param_ignorenumchunks = false;

    private static void Main(string[] args)
    {
        CheckParameters(args);

        CheckForEpisodes();

        InitGame();

        DemoLoop();

        Quit("Demo loop exited???");
    }

    private static bool IfArg(string arg, string match)
    {
        return string.Equals(arg, match, StringComparison.OrdinalIgnoreCase);
    }

    private static void CheckParameters(string[] args)
    {
        var header =
@"Wolf4CSharp v0.1
C# Port by Little A Games (TreeSapThief)
Original Port by Chaos-Software, additions by the community
Original Wolfenstein 3D by id Software
Usage: Wolf4SDL [options]
See Options.txt for help";

        var error = string.Empty;

        for(int i = 0; i < args.Length; i++)
        {
            if (IfArg(args[i], "--goobers"))
                param_debugmode = true;
            else if (IfArg(args[i], "--baby"))
                param_difficulty = difficultytypes.gd_baby;
            else if (IfArg(args[i], "--easy"))
                param_difficulty = difficultytypes.gd_easy;
            else if (IfArg(args[i], "--medium"))
                param_difficulty = difficultytypes.gd_medium;
            else if (IfArg(args[i], "--hard"))
                param_difficulty = difficultytypes.gd_hard;
            else if (IfArg(args[i], "--nowait"))
                param_nowait = true;
            else if (IfArg(args[i], "--tedlevel"))
            {
                if (++i >= args.Length)
                {
                    error = "The tedlevel option is missing the level argument!";
                }
                else if (int.TryParse(args[i], out int level))
                {
                    param_tedlevel = level;
                }
            }
            else if (IfArg(args[i], "--windowed"))
            {
                fullscreen = false;
            }
            else if (IfArg(args[i], "--windowed-mouse"))
            {
                fullscreen = false;
                forcegrabmouse = true;
            }
            else if (IfArg(args[i], "--res"))
            {
                if (i + 2 >= args.Length)
                {
                    error = "The res option needs the width and/or the height argument!";
                }
                else
                {
                    screenWidth = short.Parse(args[++i]);
                    screenHeight = short.Parse(args[++i]);
                    int factor = screenWidth / 320;
                    if ((screenWidth % 320) != 0 || (screenHeight != 200 * factor && screenHeight != 240 * factor))
                        error = "Screen size must be a multiple of 320x200 or 320x240!";
                }
            }
            else if (IfArg(args[i], "--resf"))
            {
                if (i + 2 >= args.Length)
                {
                    error = "The res option needs the width and/or the height argument!";
                }
                else
                {
                    screenWidth = short.Parse(args[++i]);
                    screenHeight = short.Parse(args[++i]);
                    if (screenWidth < 320)
                        error = "Screen width must be at least 320!";
                    if (screenHeight < 200)
                        error = "Screen height must be at least 200!";
                }
            }
            else if (IfArg(args[i], "--bits"))
            {
                if (++i >= args.Length)
                {
                    error = "The bits option is missing the color depth argument!";
                }
                else
                {
                    int.TryParse(args[i], out screenBits);
                    if (screenBits > 32 || (screenBits & 7) != 0)
                        error = "Screen color depth must be 8, 16, 24, or 32!";
                }
            }
            else if (IfArg(args[i], "--extravbls"))
            {
                if (++i >= args.Length)
                {
                    error = "The extravbls option is missing the vbls argument!";
                }
                else
                {
                    int.TryParse(args[i], out extravbls);
                    if (extravbls < 0)
                        error = "The extravbls option must be 0 or higher!";
                }
            }
            else if (IfArg(args[i], "--joystick"))
            {
                if (++i >= args.Length)
                {
                    error = "The joystick option is missing the index argument!";
                }
                else
                {
                    int.TryParse(args[i], out param_joystickindex); // index is checked in InitGame
                }
            }
            else if (IfArg(args[i], "--joystickhat"))
            {
                if (++i >= args.Length)
                {
                    error = "The joystickhat option is missing the index argument!";
                }
                else
                {
                    int.TryParse(args[i], out param_joystickhat);
                }
            }
            else if (IfArg(args[i], "--samplerate"))
            {
                if (++i >= args.Length)
                {
                    error = "The samplerate option is missing the rate argument!";
                }
                else
                {
                    int.TryParse(args[i], out param_samplerate);
                    if (param_samplerate < 7042 || param_samplerate > 44100)
                        error = "The samplerate must be between 7042 and 44100!";
                }
            }
            else if (IfArg(args[i], "--audiobuffer"))
            {
                if (++i >= args.Length)
                {
                    error = "The audiobuffer option is missing the size argument!";
                }
                else
                {
                    int.TryParse(args[i], out param_audiobuffer);
                }
            }
            else if (IfArg(args[i], "--mission"))
            {
                if (++i >= args.Length)
                {
                    error = "The audiobuffer option is missing the size argument!";
                }
                else
                {
                    int.TryParse(args[i], out param_mission);
                    if (param_mission < 0 || param_mission > 3)
                        error = "The mission option must be between 0 and 3!";
                }
            }
            else if (IfArg(args[i], "--configdir"))
            {
                if (++i >= args.Length)
                {
                    error = "The configdir option is missing the dir argument!";
                }
                else
                {
                    var len = args[i].Length;
                    if (len + 2 > CONFIG_DIR_SIZE)
                        error = "The config directory is too long!";
                    else
                    {
                        if (args[i][len] != '/' && args[i][len] != '\\')
                            configdir = args[i] + "/";
                        else
                            configdir = args[i];
                    }
                }
            }
            else if (IfArg(args[i], "--goodtimes"))
                param_goodtimes = true;
            else if (IfArg(args[i], "--ignorenumchunks"))
                param_ignorenumchunks = true;
            else
            {
                error = "Invalid parameter(s).";
            }
        }

        if (error.Length > 0)
        {
            StringBuilder helpStr = new StringBuilder();
            helpStr.Append(header);
            helpStr.Append(error);

            Error(helpStr.ToString());
            Environment.Exit(1);
        }
    }

    private static void InitGame()
    {
        bool didjukebox = false;
        if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_AUDIO | SDL.SDL_INIT_JOYSTICK) < 0)
        {
            Quit($"Could not initialize SDL: {SDL.SDL_GetError()}");
        }

        AppDomain.CurrentDomain.ProcessExit += (s, e) => SDL.SDL_Quit();

        int numJoysticks = SDL.SDL_NumJoysticks();
        if (param_joystickindex > 0 && (param_joystickindex < -1 || param_joystickindex > numJoysticks))
        {
            if (numJoysticks == 0)
                Console.WriteLine("No joysticks are available to SDL!");
            else
                Console.WriteLine($"The joystick index must be between -1 and {numJoysticks - 1}!");
            Environment.Exit(1);
        }

        SignonScreen();

        VW_UpdateScreen();

        VH_Startup();
        IN_Startup();
        PM_Startup();
        SD_Startup();
        CA_Startup();
        US_Startup();

        //
        // build some tables
        //
        InitDigiMap();

        ReadConfig();

        SetupSaveGames();

        //
        // HOLDING DOWN 'M' KEY?
        //

        if (Keyboard[(int)ScanCodes.sc_M])
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
        InitRedShifts();

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
        VL_SetVGAPlaneMode();
        VL_MemToScreen(Signon.signon, 320, 200, 0, 0);
    }

    private static void FinishSignon()
    {
        // TODO: Spear of Destiny support
        //if (Game == "SPEAR")
        //{
        //    VW_UpdateScreen();

        //    if (!param_nowait)
        //        VW_WaitVBL(3 * 70);
        //}
        //else {}
        VW_Bar(0, 189, 300, 11, VL_GetPixel(0, 0));
        WindowX = 0;
        WindowW = 320;
        PrintY = 190;

        SETFONTCOLOR(14, 4);
        US_CPrint("Press a key"); // "Oprima una tecla"

        VW_UpdateScreen();

        if (!param_nowait)
            IN_Ack();

        VW_Bar(0, 189, 300, 11, VL_GetPixel(0, 0));

        PrintY = 190;
        SETFONTCOLOR(10, 4);

        US_CPrint("Working..."); // "pensando..."

        VW_UpdateScreen();

        SETFONTCOLOR(0, 15);
    }

    private static void DemoLoop()
    {
        int LastDemo = 0;

        //
        // check for launch from ted
        //
        if (param_tedlevel != -1)
        {
            param_nowait = true;
            EnableEndGameMenuItem();
            NewGame(param_difficulty, 0);

            gamestate.episode = (short)(param_tedlevel / 10);
            gamestate.mapon = (short)(param_tedlevel % 10);

            GameLoop();
            Quit("");
            return;
        }

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
                VWB_DrawPic(0, 0, graphicnums.TITLEPIC);
                VW_UpdateScreen();
                VW_FadeIn();

                if (IN_UserInput(TickBase * 15))
                    break;
                VW_FadeOut();

                //
                // credits page
                //
                VWB_DrawPic(0, 0, graphicnums.CREDITSPIC);
                VW_UpdateScreen();
                VW_FadeIn();
                if (IN_UserInput(TickBase * 10))
                    break;
                VW_FadeOut();

                //
                // high scores
                //
                DrawHighScores();
                VW_UpdateScreen();
                VW_FadeIn();

                if (IN_UserInput(TickBase * 10))
                    break;

                //
                // demo
                //
                PlayDemo(LastDemo++ % 4);
                if (playstate == playstatetypes.ex_abort)
                    break;
                VW_FadeOut();
                if (screenHeight % 200 != 0)
                    VL_ClearScreen(0);
                StartCPMusic(INTROSONG);
            }

            VW_FadeOut();

            if (Keyboard[(int)ScanCodes.sc_Tab] && param_debugmode)
                RecordDemo();
            else
                US_ControlPanel(0);

            if (startgame || loadedgame)
            {
                GameLoop();
                if (!param_nowait)
                {
                    VW_FadeOut();
                    StartCPMusic(INTROSONG);
                }
            }
        }
    }

    internal static void NewGame(difficultytypes difficulty, int episode)
    {
        gamestate = new gametype();
        gamestate.difficulty = difficulty;
        gamestate.weapon = gamestate.bestweapon = gamestate.chosenweapon = weapontypes.wp_pistol;

        gamestate.health = 100;
        gamestate.ammo = STARTAMMO;
        gamestate.lives = 3;
        gamestate.nextextra = EXTRAPOINTS;
        gamestate.episode = (short)episode;

        startgame = true;
    }

    static void InitDigiMap()
    {
        for (int i = 0; i != (int)soundnames.LASTSOUND; i += 3)
        {
            var map = wolfdigimap[i];
            DigiMap[wolfdigimap[i]] = wolfdigimap[i + 1];
            DigiChannel[wolfdigimap[i + 1]] = wolfdigimap[i + 2];
            SD_PrepareSound(wolfdigimap[i + 1]);
        }
    }


    static int[] wolfdigimap =
    {
        (int)soundnames.HALTSND,                0,  -1,
        (int)soundnames.DOGBARKSND,             1,  -1,
        (int)soundnames.CLOSEDOORSND,           2,  -1,
        (int)soundnames.OPENDOORSND,            3,  -1,
        (int)soundnames.ATKMACHINEGUNSND,       4,   0,
        (int)soundnames.ATKPISTOLSND,           5,   0,
        (int)soundnames.ATKGATLINGSND,          6,   0,
        (int)soundnames.SCHUTZADSND,            7,  -1,
        (int)soundnames.GUTENTAGSND,            8,  -1,
        (int)soundnames.MUTTISND,               9,  -1,
        (int)soundnames.BOSSFIRESND,            10,  1,
        (int)soundnames.SSFIRESND,              11, -1,
        (int)soundnames.DEATHSCREAM1SND,        12, -1,
        (int)soundnames.DEATHSCREAM2SND,        13, -1,
        (int)soundnames.DEATHSCREAM3SND,        13, -1,
        (int)soundnames.TAKEDAMAGESND,          14, -1,
        (int)soundnames.PUSHWALLSND,            15, -1,

        (int)soundnames.LEBENSND,               20, -1,
        (int)soundnames.NAZIFIRESND,            21, -1,
        (int)soundnames.SLURPIESND,             22, -1,

        (int)soundnames.YEAHSND,                32, -1,
        // These are in all other episodes
        (int)soundnames.DOGDEATHSND,            16, -1,
        (int)soundnames.AHHHGSND,               17, -1,
        (int)soundnames.DIESND,                 18, -1,
        (int)soundnames.EVASND,                 19, -1,

        (int)soundnames.TOT_HUNDSND,            23, -1,
        (int)soundnames.MEINGOTTSND,            24, -1,
        (int)soundnames.SCHABBSHASND,           25, -1,
        (int)soundnames.HITLERHASND,            26, -1,
        (int)soundnames.SPIONSND,               27, -1,
        (int)soundnames.NEINSOVASSND,           28, -1,
        (int)soundnames.DOGATTACKSND,           29, -1,
        (int)soundnames.LEVELDONESND,           30, -1,
        (int)soundnames.MECHSTEPSND,            31, -1,

        (int)soundnames.SCHEISTSND,             33, -1,
        (int)soundnames.DEATHSCREAM4SND,        34, -1,         // AIIEEE
        (int)soundnames.DEATHSCREAM5SND,        35, -1,         // DEE-DEE
        (int)soundnames.DONNERSND,              36, -1,         // EPISODE 4 BOSS DIE
        (int)soundnames.EINESND,                37, -1,         // EPISODE 4 BOSS SIGHTING
        (int)soundnames.ERLAUBENSND,            38, -1,         // EPISODE 6 BOSS SIGHTING
        (int)soundnames.DEATHSCREAM6SND,        39, -1,         // FART
        (int)soundnames.DEATHSCREAM7SND,        40, -1,         // GASP
        (int)soundnames.DEATHSCREAM8SND,        41, -1,         // GUH-BOY!
        (int)soundnames.DEATHSCREAM9SND,        42, -1,         // AH GEEZ!
        (int)soundnames.KEINSND,                43, -1,         // EPISODE 5 BOSS SIGHTING
        (int)soundnames.MEINSND,                44, -1,         // EPISODE 6 BOSS DIE
        (int)soundnames.ROSESND,                45, -1,         // EPISODE 5 BOSS DIE
        (int)soundnames.LASTSOUND
    };

    internal static void NewViewSize(int width)
    {
        viewsize = width;
        if (viewsize == 21)
            SetViewSize((uint)screenWidth, (uint)screenHeight);
        else if (viewsize == 20)
            SetViewSize((uint)screenWidth, (uint)(screenHeight - scaleFactor * STATUSLINES));
        else
            SetViewSize((uint)(width * 16 * screenWidth / 320), (uint)(width * 16 * HEIGHTRATIO * screenHeight / 200));
    }

    internal static bool SetViewSize(uint width, uint height)
    {
        viewwidth = (int)(width & ~15);                  // must be divisable by 16
        viewheight = (int)(height & ~1);                 // must be even
        centerx = (short)(viewwidth / 2 - 1);
        centery = (short)(viewheight / 2);
        shootdelta = viewwidth / 10;
        if (viewheight == screenHeight)
            viewscreenx = viewscreeny = (int)(screenofs = 0);
        else
        {
            viewscreenx = (screenWidth - viewwidth) / 2;
            viewscreeny = (screenHeight - scaleFactor * STATUSLINES - viewheight) / 2;
            screenofs = (uint)(viewscreeny * screenWidth + viewscreenx);
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

    private static void Quit(string errorStr)
    {
        var returnCode = errorStr.Length > 0 ? 1 : 0;

        if (returnCode == 0)
            WriteConfig();

        ShutdownId();

        if (returnCode != 0)
            Error(errorStr);

        Environment.Exit(returnCode);
    }

    internal static void ReadConfig()
    {
        SDMode sd;
        SMMode sm;
        SDSMode sds;
        string configpath;

        if (!string.IsNullOrEmpty(configdir))
            configpath = $"{configdir}/{configname}";
        else
            configpath = configname;

        if (!File.Exists(configpath))
        {
            SetDefaultConfig();
            return;
        }
        try
        {
            using (FileStream fs = File.OpenRead(configpath))
            using (BinaryReader br = new BinaryReader(fs))
            {
                //
                // valid config file
                //
                ushort tmp = br.ReadUInt16();
                if (tmp != 0xfefa)
                {
                    SetDefaultConfig();
                    return;
                }

                foreach (var s in Scores)
                    s.Read(br);

                sd = (SDMode)br.ReadByte();
                sm = (SMMode)br.ReadByte();
                sds = (SDSMode)br.ReadByte();

                mouseenabled = br.ReadByte();
                joystickenabled = br.ReadByte();
                _ = br.ReadByte(); // joypad enabled placeholder
                _ = br.ReadByte(); // joystick progressive placeholder
                _ = br.ReadInt32(); // joystick port placeholder

                for (int i = 0; i < dirscan.Length; i++)
                    dirscan[i] = br.ReadInt32();
                for (int i = 0; i < buttonscan.Length; i++)
                    buttonscan[i] = br.ReadInt32();
                for (int i = 0; i < buttonmouse.Length; i++)
                    buttonmouse[i] = br.ReadInt32();
                for (int i = 0; i < buttonjoy.Length; i++)
                    buttonjoy[i] = br.ReadInt32();

                viewsize = br.ReadInt32();
                mouseadjustment = br.ReadInt32();

                if ((sd == SDMode.AdLib || sm == SMMode.AdLib) && !AdLibPresent
                    && !SoundBlasterPresent)
                {
                    sd = SDMode.PC;
                    sm = SMMode.Off;
                }

                if ((sds == SDSMode.SoundBlaster) && !SoundBlasterPresent)
                    sds = SDSMode.Off;

                // make sure values are correct
                if (mouseenabled > 0) mouseenabled = 1; // true
                if (joystickenabled > 0) joystickenabled = 1; // true

                if (!MousePresent)
                    mouseenabled = 0; // false
                if (!IN_JoyPresent())
                    joystickenabled = 0; // false

                if (mouseadjustment < 0) mouseadjustment = 0;
                else if (mouseadjustment > 9) mouseadjustment = 9;

                if (viewsize < 4) viewsize = 4;
                else if (viewsize > 21) viewsize = 21;

                // Set "Read This" back to standard active
                MainMenu[6].active = 1;
                MainItems.curpos = 0;


                SD_SetMusicMode(sm);
                SD_SetSoundMode(sd);
                SD_SetDigiDevice(sds);
            }
        }
        catch (Exception e)
        {
            File.Delete(configpath);
            SetDefaultConfig();
        }
    }

    private static void SetDefaultConfig()
    {
        SDMode sd;
        SMMode sm;
        SDSMode sds;
        if (SoundBlasterPresent || AdLibPresent)
        {
            sd = SDMode.AdLib;
            sm = SMMode.AdLib;
        }
        else
        {
            sd = SDMode.PC;
            sm = SMMode.Off;
        }

        if (SoundBlasterPresent)
            sds = SDSMode.SoundBlaster;
        else
            sds = SDSMode.Off;

        if (MousePresent)
            mouseenabled = 1;// true;

        if (IN_JoyPresent())
            joystickenabled = 1;// true;

        viewsize = 19;
        mouseadjustment = 5;

        SD_SetMusicMode(sm);
        SD_SetSoundMode(sd);
        SD_SetDigiDevice(sds);
    }

    internal static void WriteConfig()
    {
        string configpath = string.Empty;

        if (configdir != string.Empty)
            configpath = $"{configdir}/{configname}";
        else
            configpath = configname;

        using (FileStream fs = File.OpenWrite(configpath))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            ushort tmp = 0xfefa;
            bw.Write(tmp);
            foreach (var s in Scores)
                s.Write(bw);

            bw.Write((byte)SoundMode);
            bw.Write((byte)MusicMode);
            bw.Write((byte)DigiMode);

            bw.Write(mouseenabled);
            bw.Write(joystickenabled);
            bw.Write((byte)0); // joypad placeholder
            bw.Write((byte)0); // joystick-progressive placeholder
            bw.Write((int)0); // joystick port placeholder

            for(int i = 0; i < dirscan.Length; i++)
                bw.Write(dirscan[i]);

            for (int i = 0; i < buttonscan.Length; i++)
                bw.Write(buttonscan[i]);

            for (int i = 0; i < buttonmouse.Length; i++)
                bw.Write(buttonmouse[i]);

            for (int i = 0; i < buttonjoy.Length; i++)
                bw.Write(buttonjoy[i]);

            bw.Write(viewsize);
            bw.Write(mouseadjustment);
        }
    }

    internal static bool LoadTheGame(BinaryReader br, int x, int y)
    {
        int i, j;
        uint actnum = 0;
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
        tilemap = br.ReadBytes(tilemap.Length).ToFixedArray(MAPSIZE, MAPSIZE);
        checksum = DoChecksum(tilemap, checksum);
        DiskFlopAnim(x, y);

        for (i = 0; i < mapwidth; i++)
            for (j = 0; j < mapheight; j++)
            {
                actnum = br.ReadUInt32();
                checksum = DoChecksum(actnum, checksum);
                throw new NotImplementedException("actorat needs revisiting");
                //actorat[i, j] = null;// actnum; // TODO: Get object again
            }

        areaconnect = br.ReadBytes(NUMAREAS*NUMAREAS).ToFixedArray(NUMAREAS, NUMAREAS);
        areabyplayer = br.ReadBytes(NUMAREAS);

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

            for (y = 0; y<mapheight; y++)
            {
                for (x = 0; x<mapwidth; x++)
                {
                    tile = mapsegs[0][y*mapwidth + x];

                    if (MAPSPOT(x, y,1) == PUSHABLETILE && tilemap[x, y] == 0 && !VALIDAREA(tile))
                    {
                        if (VALIDAREA(MAPSPOT(x + 1, y, 0)))
                            tile = (ushort)MAPSPOT(x + 1, y, 0);
                        if (VALIDAREA(MAPSPOT(x, y - 1, 0)))
                            tile = (ushort)MAPSPOT(x, y - 1, 0);
                        if (VALIDAREA(MAPSPOT(x, y + 1, 0)))
                            tile = (ushort)MAPSPOT(x, y + 1, 0);
                        if (VALIDAREA(MAPSPOT(x - 1, y, 0)))
                            tile = (ushort)MAPSPOT(x - 1, y, 0);

                        SetMapSpot(x, y,1, 0);
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
            Message($"{STR_SAVECHT1}\n{STR_SAVECHT2}\n{STR_SAVECHT3}\n{STR_SAVECHT4}");

            IN_ClearKeysDown();
            IN_Ack();

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
        bw.Write(tilemap);
        checksum = DoChecksum(tilemap, checksum);
        DiskFlopAnim(x, y);

        for (i = 0; i < mapwidth; i++)
        {
            for (j = 0; j < mapheight; j++)
            {
                var obIndex = actorat[i, j];
                bw.Write((int)(obIndex?.GetHashCode() ?? 0));
                checksum = DoChecksum((int)(obIndex?.GetHashCode() ?? 0), checksum);
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

        VWB_DrawPic(x, y, graphicnums.C_DISKLOADING1PIC + diskflopanim_which);
        VW_UpdateScreen();
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
        uint[] songs =
        {
            (uint)musicnames.GETTHEM_MUS,
            (uint)musicnames.SEARCHN_MUS,
            (uint)musicnames.POW_MUS,
            (uint)musicnames.SUSPENSE_MUS,
            (uint)musicnames.WARMARCH_MUS,
            (uint)musicnames.CORNER_MUS,

            (uint)musicnames.NAZI_OMI_MUS,
            (uint)musicnames.PREGNANT_MUS,
            (uint)musicnames.GOINGAFT_MUS,
            (uint)musicnames.HEADACHE_MUS,
            (uint)musicnames.DUNGEON_MUS,
            (uint)musicnames.ULTIMATE_MUS,

            (uint)musicnames.INTROCW3_MUS,
            (uint)musicnames.NAZI_RAP_MUS,
            (uint)musicnames.TWELFTH_MUS,
            (uint)musicnames.ZEROHOUR_MUS,
            (uint)musicnames.ULTIMATE_MUS,
            (uint)musicnames.PACMAN_MUS
        };

        IN_ClearKeysDown();
        if (!AdLibPresent && !SoundBlasterPresent)
            return;

        MenuFadeOut();

        start = ((SDL.SDL_GetTicks() / 10) % 3) * 6;

        CA_LoadAllSounds();

        fontnumber = 1;
        ClearMScreen();
        VWB_DrawPic(112, 184, graphicnums.C_MOUSELBACKPIC);
        DrawStripes(10);
        SETFONTCOLOR(TEXTCOLOR, BKGDCOLOR);

        DrawWindow(CTL_X - 2, CTL_Y - 6, 280, 13 * 7, BKGDCOLOR);

        DrawMenu(MusicItems, MusicMenu/*[start]*/);

        SETFONTCOLOR(READHCOLOR, BKGDCOLOR);
        PrintY = 15;
        WindowX = 0;
        WindowY = 320;
        US_CPrint("Robert's Jukebox");

        SETFONTCOLOR(TEXTCOLOR, BKGDCOLOR);
        VW_UpdateScreen();
        MenuFadeIn();

        do
        {
            which = HandleMenu(MusicItems, MusicMenu/*[start]*/, null);
            if (which >= 0)
            {
                if (lastsong >= 0)
                    MusicMenu[start + lastsong].active = 1;

                StartCPMusic((musicnames)songs[start + which]);
                MusicMenu[start + which].active = 2;
                DrawMenu(MusicItems, MusicMenu/*[start]*/);
                VW_UpdateScreen();
                lastsong = which;
            }
        } while (which >= 0);

        MenuFadeOut();
        IN_ClearKeysDown();
    }

    internal static void ShutdownId()
    {
        US_Shutdown(); // This line is completely useless...
        SD_Shutdown();
        PM_Shutdown();
        IN_Shutdown();
        VL_Shutdown();
        CA_Shutdown();
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
            viewwidth = screenWidth;
            viewheight = screenHeight;
            VWB_BarScaledCoord(0, 0, screenWidth, screenHeight, 0);
        }
        else if (width == 20)
        {
            viewwidth = screenWidth;
            viewheight = screenHeight - scaleFactor * STATUSLINES;
            DrawPlayBorder();
        }
        else
        {
            viewwidth = width * 16 * screenWidth / 320;
            viewheight = (int)(width * 16 * HEIGHTRATIO * screenHeight / 200);
            DrawPlayBorder();
        }

        viewwidth = oldwidth;
        viewheight = oldheight;
    }
}
