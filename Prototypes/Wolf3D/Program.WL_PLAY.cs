using static SDL2.SDL;

namespace Wolf3D;

internal partial class Program
{
    static bool madenoise; // true when shooting or screaming

    static byte playstate;
    //
    // control info
    //
    internal static byte /*int8_t boolean*/ mouseenabled, joystickenabled;
    internal static int[] dirscan = new int[4] { (int)ScanCodes.sc_UpArrow, (int)ScanCodes.sc_RightArrow, (int)ScanCodes.sc_DownArrow, (int)ScanCodes.sc_LeftArrow };
    internal static int[] buttonscan = new int[(int)buttontypes.NUMBUTTONS] { (int)ScanCodes.sc_Control, (int)ScanCodes.sc_Alt, (int)ScanCodes.sc_LShift, (int)ScanCodes.sc_Space, (int)ScanCodes.sc_1, (int)ScanCodes.sc_2, (int)ScanCodes.sc_3, (int)ScanCodes.sc_4, 0,0,0,0,0,0,0,0,0,0 };
    internal static int[] buttonmouse = new int[4] { (int)buttontypes.bt_attack, (int)buttontypes.bt_strafe, (int)buttontypes.bt_use, (int)buttontypes.bt_nobutton };
    internal static int[] buttonjoy = new int[32] {
        (int)buttontypes.bt_attack, (int)buttontypes.bt_strafe, (int)buttontypes.bt_use, (int)buttontypes.bt_run, (int)buttontypes.bt_strafeleft, (int)buttontypes.bt_straferight, (int)buttontypes.bt_esc, (int)buttontypes.bt_pause,
        (int)buttontypes.bt_prevweapon, (int)buttontypes.bt_nextweapon, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton,
        (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton,
        (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton, (int)buttontypes.bt_nobutton
    };

    static int viewsize;
    static bool demorecord, demoplayback;
    static int demoptr, lastdemoptr;


    //
    // current user input
    //
    static int controlx, controly;         // range from -100 to 100 per tic
    static bool[] buttonstate = new bool[(int)buttontypes.NUMBUTTONS];

    static int lastgamemusicoffset = 0;

    private const int NUMREDSHIFTS = 6;
    private const int REDSTEPS = 8;

    private const int NUMWHITESHIFTS = 3;
    private const int WHITESTEPS = 20;
    private const int WHITETICS = 6;

    private static SDL_Color[,] redshifts = new SDL_Color[NUMREDSHIFTS, 256];
    private static SDL_Color[,] whiteshifts = new SDL_Color[NUMWHITESHIFTS, 256];

    static int damagecount, bonuscount;
    static bool palshifted; // boolean

    private static byte ClampToByte(int v) => (byte)(v < 0 ? 0 : (v > 255 ? 255 : v));
    internal static void InitRedShifts()
    {
        // Fade through intermediate red shift frames
        for (int i = 1; i <= NUMREDSHIFTS; i++)
        {
            int ri = i - 1;
            for (int j = 0; j < 256; j++)
            {
                var basec = gamepal[j];

                int delta = 256 - basec.r;
                int newR = basec.r + delta * i / REDSTEPS;

                delta = -basec.g;
                int newG = basec.g + delta * i / REDSTEPS;

                delta = -basec.b;
                int newB = basec.b + delta * i / REDSTEPS;

                redshifts[ri, j] = new SDL_Color
                {
                    r = ClampToByte(newR),
                    g = ClampToByte(newG),
                    b = ClampToByte(newB),
                    a = 255 //SDL_ALPHA_OPAQUE
                };
            }
        }

        // Prepare white shift palettes
        for (int i = 1; i <= NUMWHITESHIFTS; i++)
        {
            int wi = i - 1;
            for (int j = 0; j < 256; j++)
            {
                var basec = gamepal[j];

                int delta = 256 - basec.r;
                int newR = basec.r + delta * i / WHITESTEPS;

                delta = 248 - basec.g;
                int newG = basec.g + delta * i / WHITESTEPS;

                delta = 0 - basec.b;
                int newB = basec.b + delta * i / WHITESTEPS;

                whiteshifts[wi, j] = new SDL_Color
                {
                    r = ClampToByte(newR),
                    g = ClampToByte(newG),
                    b = ClampToByte(newB),
                    a = 255 //SDL_ALPHA_OPAQUE
                };
            }
        }
    }

    internal static void StartMusic()
    {
        // TODO:
    }

    internal static void ContinueMusic(int offs)
    {
        // TODO:
    }

    internal static int StopMusic()
    {
        // TODO:
        return 0;
    }

    static int funnyticount;

    internal static void PlayLoop()
    {
        objstruct obj;
        playstate = (byte)playstatetypes.ex_stillplaying;
        lasttimecount = (int)GetTimeCount();
        frameon = 0;
        anglefrac = 0;
        facecount = 0;
        funnyticount = 0;
        buttonstate = new bool[(int)buttontypes.NUMBUTTONS];
        ClearPaletteShifts();

        IN_CenterMouse();

        if (demoplayback)
            IN_StartAck();

        //do
        //{
        //    PollControls();

        //    //
        //    // actor thinking
        //    //
        //    madenoise = false;
        //    MoveDoors();
        //    MovePWalls();
        //}
        //while (playstate == 0 && !startgame);

        //if (playstate != (byte)playstatetypes.ex_died)
        //    FinishPaletteShifts();
    }

    internal static void ClearPaletteShifts()
    {
        bonuscount = damagecount = 0;
        palshifted = false;
    }
}
