using static SDL2.SDL;

namespace Wolf3D;

internal partial class Program
{
    static bool madenoise; // true when shooting or screaming

    static playstatetypes playstate;

    static musicnames lastmusicchunk = 0;

    internal static int DebugOk;

    internal static LinkedList<objstruct> objlist2 = new LinkedList<objstruct>();
    internal static objstruct player;

    internal static byte singlestep, godmode, noclip, ammocheat, mapreveal;
    internal static int extravbls;

    internal static byte[,] tilemap = new byte[MAPSIZE, MAPSIZE]; // wall values only
    internal static bool[,] spotvis = new bool[MAPSIZE ,MAPSIZE];
    internal static Actor?[,] actorat = new Actor?[MAPSIZE, MAPSIZE];

    internal static ushort mapwidth, mapheight;
    internal static uint tics;

    //
    // control info
    //
    internal static bool mouseenabled, joystickenabled;
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
    static bool[] buttonheld = new bool[(int)buttontypes.NUMBUTTONS];

    static bool demorecord, demoplayback;
    static byte[] demoData;
    static int demoptr, lastdemoptr;


    //
    // current user input
    //
    static int controlx, controly;         // range from -100 to 100 per tic
    static bool[] buttonstate = new bool[(int)buttontypes.NUMBUTTONS];

    static int lastgamemusicoffset = 0;

    /*
    =============================================================================

                                                     LOCAL VARIABLES

    =============================================================================
    */

    //
    // LIST OF SONGS FOR EACH VERSION
    //
    internal static musicnames[] songs = new[] {
        
    //
    // Episode One
    //
    musicnames.GETTHEM_MUS,
    musicnames.SEARCHN_MUS,
    musicnames.POW_MUS,
    musicnames.SUSPENSE_MUS,
    musicnames.GETTHEM_MUS,
    musicnames.SEARCHN_MUS,
    musicnames.POW_MUS,
    musicnames.SUSPENSE_MUS,

    musicnames.WARMARCH_MUS,               // Boss level
    musicnames.CORNER_MUS,                 // Secret level

    //
    // Episode Two
    //
    musicnames.NAZI_OMI_MUS,
    musicnames.PREGNANT_MUS,
    musicnames.GOINGAFT_MUS,
    musicnames.HEADACHE_MUS,
    musicnames.NAZI_OMI_MUS,
    musicnames.PREGNANT_MUS,
    musicnames.HEADACHE_MUS,
    musicnames.GOINGAFT_MUS,

    musicnames.WARMARCH_MUS,               // Boss level
    musicnames.DUNGEON_MUS,                // Secret level

    //
    // Episode Three
    //
    musicnames.INTROCW3_MUS,
    musicnames.NAZI_RAP_MUS,
    musicnames.TWELFTH_MUS,
    musicnames.ZEROHOUR_MUS,
    musicnames.INTROCW3_MUS,
    musicnames.NAZI_RAP_MUS,
    musicnames.TWELFTH_MUS,
    musicnames.ZEROHOUR_MUS,

    musicnames.ULTIMATE_MUS,               // Boss level
    musicnames.PACMAN_MUS,                 // Secret level

    //
    // Episode Four
    //
    musicnames.GETTHEM_MUS,
    musicnames.SEARCHN_MUS,
    musicnames.POW_MUS,
    musicnames.SUSPENSE_MUS,
    musicnames.GETTHEM_MUS,
    musicnames.SEARCHN_MUS,
    musicnames.POW_MUS,
    musicnames.SUSPENSE_MUS,

    musicnames.WARMARCH_MUS,               // Boss level
    musicnames.CORNER_MUS,                 // Secret level

    //
    // Episode Five
    //
    musicnames.NAZI_OMI_MUS,
    musicnames.PREGNANT_MUS,
    musicnames.GOINGAFT_MUS,
    musicnames.HEADACHE_MUS,
    musicnames.NAZI_OMI_MUS,
    musicnames.PREGNANT_MUS,
    musicnames.HEADACHE_MUS,
    musicnames.GOINGAFT_MUS,

    musicnames.WARMARCH_MUS,               // Boss level
    musicnames.DUNGEON_MUS,                // Secret level

    //
    // Episode Six
    //
    musicnames.INTROCW3_MUS,
    musicnames.NAZI_RAP_MUS,
    musicnames.TWELFTH_MUS,
    musicnames.ZEROHOUR_MUS,
    musicnames.INTROCW3_MUS,
    musicnames.NAZI_RAP_MUS,
    musicnames.TWELFTH_MUS,
    musicnames.ZEROHOUR_MUS,

    musicnames.ULTIMATE_MUS,               // Boss level
    musicnames.FUNKYOU_MUS                 // Secret level
};

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
        SD_MusicOff();
        lastmusicchunk = songs[gamestate.mapon + gamestate.episode * 10];
        SD_StartMusic(STARTMUSIC + lastmusicchunk);
    }

    internal static void ContinueMusic(int offs)
    {
        SD_MusicOff();
        lastmusicchunk = songs[gamestate.mapon + gamestate.episode * 10];
        SD_ContinueMusic((int)(STARTMUSIC + lastmusicchunk), offs);
    }

    internal static int StopMusic()
    {
        int lastoffs = SD_MusicOff();

        UNCACHEAUDIOCHUNK((int)(STARTMUSIC + lastmusicchunk));

        return lastoffs;
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

        do
        {
            PollControls();

            //
            // actor thinking
            //
            madenoise = false;
            MoveDoors();
            MovePWalls();

            for (LinkedListNode<objstruct>? actor = objlist2.First; actor != null; actor = actor.Next)
            {
                DoActor(actor.Value);
            }

            UpdatePaletteShifts();

            ThreeDRefresh();

            gamestate.TimeCount += (int)tics;

            UpdateSoundLoc();      // JAB
            if (screenfaded)
                VW_FadeIn();

            CheckKeys();

            //
            // debug aids
            //
            if (singlestep != 0)
            {
                VW_WaitVBL(singlestep);
                lasttimecount = (int)GetTimeCount();
            }
            if (extravbls != 0)
                VW_WaitVBL((uint)extravbls);

            if (demoplayback)
            {
                if (IN_CheckAck())
                {
                    IN_ClearKeysDown();
                    playstate = playstatetypes.ex_abort;
                }
            }
        }
        while (playstate == 0 && !startgame);

        if (playstate != playstatetypes.ex_died)
            FinishPaletteShifts();
    }

    internal static void InitActorList()
    {
        objlist2 = new LinkedList<objstruct>();

        //
        // give the player the first free spots
        //
        player = GetNewActor();
    }

    internal static objstruct GetNewActor()
    {
        objstruct? newobj = new();
        objlist2.AddLast(newobj);

        newobj.active = (byte)activetypes.ac_no;
        return newobj;
    }

    internal static void DoActor(objstruct ob)
    {
        if (ob.active == 0 && ob.areanumber < NUMAREAS && areabyplayer[ob.areanumber] == 0)
            return;
        
        if (!ob.flags.HasFlag(objflags.FL_NONMARK) && !ob.flags.HasFlag(objflags.FL_NEVERMARK))
            actorat[ob.tilex,ob.tiley] = null;


        //
        // non transitional object
        //

        if (ob.ticcount == 0)
        {
            var think2 = ob.state?.think;
            if (think2 != null)
            {
                think2.Invoke(ob);
                if (ob.state == null)
                {
                    RemoveObj(ob);
                    return;
                }
            }

            if (ob.flags.HasFlag(objflags.FL_NEVERMARK))
                return;

            if (ob.flags.HasFlag(objflags.FL_NONMARK) && actorat[ob.tilex, ob.tiley] != null)
                return;

            actorat[ob.tilex, ob.tiley] = ob;
            return;
        }

        //
        // transitional object
        //
        ob.ticcount -= (short)tics;
        while (ob.ticcount <= 0)
        {
            var action = ob.state?.action;        // end of state action
            if (action != null)
            {
                action.Invoke(ob);
                if (ob.state == null)
                {
                    RemoveObj(ob);
                    return;
                }
            }
            if (ob.state != null
                && !string.IsNullOrEmpty(ob.state?.next)
                && enemy_states.TryGetValue(ob.state.next, out var newState))
            {
                ob.state = newState;
            }

            if (ob.state == null)
            {
                RemoveObj(ob);
                return;
            }

            if (ob.state.tictime == 0)
            {
                ob.ticcount = 0;
                break;
            }

            ob.ticcount += ob.state.tictime;
        }

        //
        // think
        //
        var think = ob.state?.think;
        if (think != null)
        {
            think.Invoke(ob);
            if (ob.state == null)
            {
                RemoveObj(ob);
                return;
            }
        }

        if (ob.flags.HasFlag(objflags.FL_NEVERMARK))
            return;

        if (ob.flags.HasFlag(objflags.FL_NONMARK) && actorat[ob.tilex, ob.tiley] != null)
            return;

        actorat[ob.tilex, ob.tiley] = ob;// (uint)((objlistIndex + 0xffff));
    }

    internal static void RemoveObj(objstruct gone)
    {
        if (gone.obclass == classtypes.playerobj)
            Quit("RemoveObj: Tried to remove the player!");

        gone.state = null;

        //
        // fix the next object's back link
        //
        objlist2.Remove(gone);
    }

    internal static void CheckKeys()
    {
        int scan;

        if (screenfaded || demoplayback)    // don't do anything with a faded screen
            return;

        scan = LastScan;


        //
        // SECRET CHEAT CODE: 'MLI'
        //
        if (Keyboard[(int)ScanCodes.sc_M] && Keyboard[(int)ScanCodes.sc_L] && Keyboard[(int)ScanCodes.sc_I])
        {
            gamestate.health = 100;
            gamestate.ammo = 99;
            gamestate.keys = 3;
            gamestate.score = 0;
            gamestate.TimeCount += (int)42000L;
            GiveWeapon(weapontypes.wp_chaingun);
            DrawWeapon();
            DrawHealth();
            DrawKeys();
            DrawAmmo();
            DrawScore();

            ClearMemory();
            ClearSplitVWB();

            Message(STR_CHEATER1 + "\n" +
                     STR_CHEATER2 + "\n\n" +
                     STR_CHEATER3 + "\n" +
                     STR_CHEATER4 + "\n" +
                     STR_CHEATER5);

            IN_ClearKeysDown();
            IN_Ack();

            if (viewsize < 17)
                DrawPlayBorder();
        }

        //
        // OPEN UP DEBUG KEYS
        //
        if (Keyboard[(int)ScanCodes.sc_BackSpace] && Keyboard[(int)ScanCodes.sc_LShift] && Keyboard[(int)ScanCodes.sc_Alt] && param_debugmode)
        {
            ClearMemory();
            ClearSplitVWB();

            Message("Debugging keys are\nnow available!");
            IN_ClearKeysDown();
            IN_Ack();

            DrawPlayBorderSides();
            DebugOk = 1;
        }

        //
        // TRYING THE KEEN CHEAT CODE!
        //
        if (Keyboard[(int)ScanCodes.sc_B] && Keyboard[(int)ScanCodes.sc_A] && Keyboard[(int)ScanCodes.sc_T])
        {
            ClearMemory();
            ClearSplitVWB();

            Message("Commander Keen is also\n" +
                        "available from Apogee, but\n" +
                        "then, you already know\n" +
                        "that - right, Cheatmeister?!");

            IN_ClearKeysDown();
            IN_Ack();

            if (viewsize < 18)
                DrawPlayBorder();
        }

        //
        // pause key weirdness can't be checked as a scan code
        //
        if (buttonstate[(int)buttontypes.bt_pause]) Paused = true;
        if (Paused)
        {
            int lastoffs = StopMusic();
            VWB_DrawPic(16 * 8, 80 - 2 * 8, graphicnums.PAUSEDPIC);
            VW_UpdateScreen();
            IN_Ack();
            Paused = false;
            ContinueMusic(lastoffs);
            IN_CenterMouse();
            lasttimecount = (int)GetTimeCount();
            return;
        }
        if (scan == (int)ScanCodes.sc_F10 ||
            scan == (int)ScanCodes.sc_F9 || scan == (int)ScanCodes.sc_F7 || scan == (int)ScanCodes.sc_F8)     // pop up quit dialog
        {
            ClearMemory();
            ClearSplitVWB();
            US_ControlPanel(scan);

            DrawPlayBorderSides();

            SETFONTCOLOR(0, 15);
            IN_ClearKeysDown();
            return;
        }

        if ((scan >= (int)ScanCodes.sc_F1 && scan <= (int)ScanCodes.sc_F9) || scan == (int)ScanCodes.sc_Escape || buttonstate[(int)buttontypes.bt_esc])
        {
            int lastoffs = StopMusic();
            ClearMemory();
            VW_FadeOut();

            US_ControlPanel(buttonstate[(int)buttontypes.bt_esc] ? (int)ScanCodes.sc_Escape : scan);

            SETFONTCOLOR(0, 15);
            IN_ClearKeysDown();
            VW_FadeOut();
            if (viewsize != 21)
                DrawPlayScreen();
            if (!startgame && !loadedgame)
                ContinueMusic(lastoffs);
            if (loadedgame)
                playstate = playstatetypes.ex_abort;
            lasttimecount = (int)GetTimeCount();
            IN_CenterMouse();
            return;
        }

        //
        // TAB-? debug keys
        //
        if (Keyboard[(int)ScanCodes.sc_Tab] && DebugOk != 0)
        {
            fontnumber = 0;
            SETFONTCOLOR(0, 15);
            if (DebugKeys() != 0 && viewsize < 20)
            {
                DrawPlayBorder();       // dont let the blue borders flash
                IN_CenterMouse();

                lasttimecount = (int)GetTimeCount();
            }
            return;
        }
    }

    internal static void PollControls()
    {
        int max, min, i;
        byte buttonbits;

        IN_ProcessEvents();

        //
        // get timing info for last frame
        //
        if (demoplayback || demorecord)   // demo recording and playback needs to be constant
        {
            // wait up to DEMOTICS Wolf tics
            uint curtime = SDL_GetTicks();
            lasttimecount += DEMOTICS;
            int timediff = (int)((lasttimecount * 100) / 7 - curtime);
            if (timediff > 0)
                SDL_Delay((uint)timediff);

            if (timediff < -2 * DEMOTICS)       // more than 2-times DEMOTICS behind?
                lasttimecount = (int)((curtime * 7) / 100);    // yes, set to current timecount

            tics = DEMOTICS;
        }
        else
            CalcTics();

        controlx = 0;
        controly = 0;
        Array.Copy(buttonstate, buttonheld, buttonstate.Length);
        Array.Fill(buttonstate, false);

        if (demoplayback)
        {
            //
            // read commands from demo buffer
            //
            buttonbits = demoData[demoptr++];
            for (i = 0; i < (int)buttontypes.NUMBUTTONS; i++)
            {
                buttonstate[i] = (buttonbits & 1) != 0;
                buttonbits >>= 1;
            }

            controlx = (sbyte)demoData[demoptr++];
            controly = (sbyte)demoData[demoptr++];

            if (demoptr == lastdemoptr)
                playstate = playstatetypes.ex_completed;   // demo is done

            controlx *= (int)tics;
            controly *= (int)tics;

            return;
        }


        //
        // get button states
        //
        PollKeyboardButtons();

        if (mouseenabled && GrabInput)
            PollMouseButtons();

        if (joystickenabled)
            PollJoystickButtons();

        //
        // get movements
        //
        PollKeyboardMove();

        if (mouseenabled && GrabInput)
            PollMouseMove();

        if (joystickenabled)
            PollJoystickMove();

        //
        // bound movement to a maximum
        //
        max = (int)(100 * tics);
        min = -max;
        if (controlx > max)
            controlx = max;
        else if (controlx < min)
            controlx = min;

        if (controly > max)
            controly = max;
        else if (controly < min)
            controly = min;

        if (demorecord)
        {
            //
            // save info out to demo buffer
            //
            controlx /= (int)tics;
            controly /= (int)tics;

            buttonbits = 0;

            // TODO: Support 32-bit buttonbits
            for (i = (int)buttontypes.NUMBUTTONS - 1; i >= 0; i--)
            {
                buttonbits <<= 1;
                if (buttonstate[i])
                    buttonbits |= 1;
            }

            demoData[demoptr++] = buttonbits;
            demoData[demoptr++] = (byte)controlx; // these might be wrong
            demoData[demoptr++] = (byte)controly;// these might need 4 bytes

            if (demoptr >= lastdemoptr - 8)
                playstate = playstatetypes.ex_completed;
            else
            {
                controlx *= (int)tics;
                controly *= (int)tics;
            }
        }
    }

    internal const int MAXX = 320;
    internal const int MAXY = 160;

    internal static void CenterWindow(ushort w, ushort h)
    {
        US_DrawWindow((ushort)(((MAXX / 8) - w) / 2), (ushort)(((MAXY / 8) - h) / 2), w, h);
    }

    internal static void UpdatePaletteShifts()
    {
        int red, white;

        if (bonuscount != 0)
        {
            white = bonuscount / WHITETICS + 1;
            if (white > NUMWHITESHIFTS)
                white = NUMWHITESHIFTS;
            bonuscount -= (int)tics;
            if (bonuscount < 0)
                bonuscount = 0;
        }
        else
            white = 0;


        if (damagecount != 0)
        {
            red = damagecount / 10 + 1;
            if (red > NUMREDSHIFTS)
                red = NUMREDSHIFTS;

            damagecount -= (int)tics;
            if (damagecount < 0)
                damagecount = 0;
        }
        else
            red = 0;

        if (red != 0)
        {
            SDL_Color[] flat = new SDL_Color[256];
            for (int i = 0; i < 256; i++)
                flat[i] = redshifts[red - 1, i];
            VL_SetPalette(flat, false);
            palshifted = true;
        }
        else if (white != 0)
        {
            SDL_Color[] flat = new SDL_Color[256];
            for (int i = 0; i < 256; i++)
                flat[i] = whiteshifts[white - 1, i];
            VL_SetPalette(flat, false);
            palshifted = true;
        }
        else if (palshifted)
        {
            VL_SetPalette(gamepal, false);        // back to normal
            palshifted = false;
        }
    }

    internal static void FinishPaletteShifts()
    {
        if (palshifted)
        {
            palshifted = false;
            VL_SetPalette(gamepal, true);
        }
    }
    internal static void ClearPaletteShifts()
    {
        bonuscount = damagecount = 0;
        palshifted = false;
    }

    internal static void StartBonusFlash()
    {
        bonuscount = NUMWHITESHIFTS * WHITETICS;    // white shift palette
    }

    internal static void StartDamageFlash(int damage)
    {
        damagecount += damage;
    }

    /*
=============================================================================

                               USER CONTROL

=============================================================================
*/

    /*
    ===================
    =
    = PollKeyboardButtons
    =
    ===================
    */

    internal static void PollKeyboardButtons()
    {
        int i;

        for (i = 0; i < (int)buttontypes.NUMBUTTONS; i++)
            if (Keyboard[buttonscan[i]])
                buttonstate[i] = true;
    }

    /*
    ===================
    =
    = PollMouseButtons
    =
    ===================
    */

    internal static void PollMouseButtons()
    {
        int buttons = IN_MouseButtons();

        if ((buttons & 1) != 0)
            buttonstate[buttonmouse[0]] = true;
        if ((buttons & 2) != 0)
            buttonstate[buttonmouse[1]] = true;
        if ((buttons & 4) != 0)
            buttonstate[buttonmouse[2]] = true;
    }


    /*
    ===================
    =
    = PollJoystickButtons
    =
    ===================
    */

    internal static void PollJoystickButtons()
    {
        int i, val, buttons = IN_JoyButtons();

        for (i = 0, val = 1; i < JoyNumButtons; i++, val <<= 1)
        {
            if ((buttons & val) != 0)
                buttonstate[buttonjoy[i]] = true;
        }
    }

    /*
    ===================
    =
    = PollKeyboardMove
    =
    ===================
    */

    internal static void PollKeyboardMove()
    {
        int delta = (int)(buttonstate[(int)buttontypes.bt_run] ? RUNMOVE * tics : BASEMOVE * tics);

        if (Keyboard[dirscan[(int)controldirs.di_north]])
            controly -= delta;
        if (Keyboard[dirscan[(int)controldirs.di_south]])
            controly += delta;
        if (Keyboard[dirscan[(int)controldirs.di_west]])
            controlx -= delta;
        if (Keyboard[dirscan[(int)controldirs.di_east]])
            controlx += delta;
    }


    /*
    ===================
    =
    = PollMouseMove
    =
    ===================
    */

    internal static void PollMouseMove()
    {
        int mousexmove, mouseymove;

        SDL_GetRelativeMouseState(out mousexmove, out mouseymove);

        controlx += mousexmove * 10 / (13 - mouseadjustment);
        controly += mouseymove * 20 / (13 - mouseadjustment);
    }


    /*
    ===================
    =
    = PollJoystickMove
    =
    ===================
    */

    internal static void PollJoystickMove()
    {
        int joyx, joyy;

        IN_GetJoyDelta(out joyx, out joyy);

        int delta = (int)(buttonstate[(int)buttontypes.bt_run] ? RUNMOVE * tics : BASEMOVE * tics);

        if (joyx > 64 || buttonstate[(int)buttontypes.bt_turnright])
            controlx += delta;
        else if (joyx < -64 || buttonstate[(int)buttontypes.bt_turnleft])
            controlx -= delta;
        if (joyy > 64 || buttonstate[(int)buttontypes.bt_movebackward])
            controly += delta;
        else if (joyy < -64 || buttonstate[(int)buttontypes.bt_moveforward])
            controly -= delta;
    }
}
