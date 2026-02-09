using System;
using System.Data;
using static SDL2.SDL;

namespace Wolf3D;

internal partial class Program
{
    static bool madenoise; // true when shooting or screaming

    static byte playstate;

    static int lastmusicchunk = 0;

    internal static int DebugOk;

    internal static objstruct[] objlist = new objstruct[MAXACTORS];
    internal static objstruct player;
    internal static int? lastobj = null;
    internal static int objfreelist;

    internal static byte singlestep, godmode, noclip, ammocheat, mapreveal;
    internal static int extravbls;

    internal static byte[,] tilemap = new byte[MAPSIZE, MAPSIZE]; // wall values only
    internal static bool[,] spotvis = new bool[MAPSIZE ,MAPSIZE];
    internal static int?[,] actorat = new int?[MAPSIZE, MAPSIZE];

    internal static ushort mapwidth, mapheight;
    internal static uint tics;

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

        do
        {
            PollControls();

            //
            // actor thinking
            //
            madenoise = false;
            MoveDoors();
            MovePWalls();

            for (int? i = 0; i != null; i = obj.next)
            {
                obj = objlist[i.Value];
                DoActor(obj, i.Value);
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
                    playstate = (byte)playstatetypes.ex_abort;
                }
            }
        }
        while (playstate == 0 && !startgame);

        if (playstate != (byte)playstatetypes.ex_died)
            FinishPaletteShifts();
    }

    internal static int objcount;
    internal static void InitActorList()
    {
        int i;

        //
        // init the actor lists
        //
        for (i = 0; i < MAXACTORS - 1; i++)
        {
            objlist[i] = new objstruct
            {
                prev = i + 1,
                next = null
            };
        }

        objlist[MAXACTORS - 1] = new objstruct
        {
            prev = null,
            next = null
        };

        objfreelist = 0;
        lastobj = null;

        objcount = 0;

        //
        // give the player the first free spots
        //
        player = GetNewActor();
    }

    internal static objstruct GetNewActor()
    {
        objstruct? newobj = null;
        if (objfreelist >= MAXACTORS)
            Quit("GetNewActor: No free spots in objlist!");
        int newobjIndex = objfreelist;
        newobj = objlist[newobjIndex];
        objfreelist = newobj.prev ?? -1;

        // TODO: This prev/next isn't setting right
        if (lastobj != null && objlist[lastobj.Value] != null)
        {
            objlist[lastobj.Value].next = newobjIndex;     // newobj->next is allready NULL from memset
        }

        newobj.prev = lastobj;

        newobj.active = (byte)activetypes.ac_no;
        lastobj = newobjIndex;

        objcount++;
        return newobj;
    }

    internal static void DoActor(objstruct ob, int objlistIndex)
    {
        Action<objstruct>? think;
        if (ob.active == 0 && ob.areanumber < NUMAREAS && areabyplayer[ob.areanumber] == 0)
            return;
        
        if ((ob.flags & (int)(objflags.FL_NONMARK | objflags.FL_NEVERMARK)) == 0)
            actorat[ob.tilex,ob.tiley] = null;


        //
        // non transitional object
        //

        if (ob.ticcount == 0)
        {
            think = ob.state.think;
            if (think != null)
            {
                think(ob);
                if (ob.state == null)
                {
                    RemoveObj(ob, objlistIndex);
                    return;
                }
            }

            if ((ob.flags & (int)objflags.FL_NEVERMARK) != 0)
                return;

            if ((ob.flags & (int)objflags.FL_NONMARK) != 0 && actorat[ob.tilex, ob.tiley] != 0)
                return;

            actorat[ob.tilex, ob.tiley] = objlistIndex;
            return;
        }

        //
        // transitional object
        //
        ob.ticcount -= (short)tics;
        while (ob.ticcount <= 0)
        {
            think = ob.state.action;        // end of state action
            if (think != null)
            {
                think(ob);
                if (ob.state == null)
                {
                    RemoveObj(ob, objlistIndex);
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
                RemoveObj(ob, objlistIndex);
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
        think = ob.state.think;
        if (think != null)
        {
            think.Invoke(ob);
            if (ob.state == null)
            {
                RemoveObj(ob, objlistIndex);
                return;
            }
        }

        if ((ob.flags & (int)objflags.FL_NEVERMARK) != 0)
            return;

        if ((ob.flags & (int)objflags.FL_NONMARK) != 0 && actorat[ob.tilex, ob.tiley] != 0)
            return;

        actorat[ob.tilex, ob.tiley] = objlistIndex;
    }

    internal static void RemoveObj(objstruct gone, int objlistIndex)
    {
        if (gone == player)
            Quit("RemoveObj: Tried to remove the player!");

        gone.state = null;

        //
        // fix the next object's back link
        //
        if (objlistIndex == lastobj)
            lastobj = gone.prev ?? 0;
        else
            objlist[(int)gone.next!].prev = gone.prev;

        //
        // fix the previous object's forward link
        //
        objlist[(int)gone.prev!].next = gone.next;

        //
        // add it back in to the free list
        //
        gone.prev = objfreelist;
        objfreelist = objlistIndex;

        objcount--;
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
            GiveWeapon((int)weapontypes.wp_chaingun);
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
            VWB_DrawPic(16 * 8, 80 - 2 * 8, (int)graphicnums.PAUSEDPIC);
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
                playstate = (byte)playstatetypes.ex_abort;
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

            controlx = demoData[demoptr++];
            controly = demoData[demoptr++];

            if (demoptr == lastdemoptr)
                playstate = (byte)playstatetypes.ex_completed;   // demo is done

            controlx *= (int)tics;
            controly *= (int)tics;

            return;
        }


        //
        // get button states
        //
        PollKeyboardButtons();

        if (mouseenabled != 0 && GrabInput)
            PollMouseButtons();

        if (joystickenabled != 0)
            PollJoystickButtons();

        //
        // get movements
        //
        PollKeyboardMove();

        if (mouseenabled != 0 && GrabInput)
            PollMouseMove();

        if (joystickenabled != 0)
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
                playstate = (byte)playstatetypes.ex_completed;
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
