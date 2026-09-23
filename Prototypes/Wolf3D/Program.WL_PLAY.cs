using Wolf3D.Entities.Actors;
using Wolf3D.Extensions;
using Wolf3D.Managers;
using static SDL2.SDL;

namespace Wolf3D;

internal partial class Program
{
    static bool madenoise; // true when shooting or screaming

    static playstatetypes playstate;

    static string lastmusicchunk = "";

    internal static int DebugOk;

    // The player lives in MapManager._actors like every other actor; this is just a shortcut to it.
    internal static Entities.Actors.PlayerPawn player =>
        _mapManager.Player ?? throw new InvalidOperationException("The player has not been spawned for this level yet.");

    internal static byte singlestep, godmode, noclip, ammocheat, mapreveal;
    internal static int extravbls;
    internal static uint tics;

    //
    // control info
    //
    internal static bool mouseenabled, joystickenabled;
    internal static ScanCodes[] dirscan = new ScanCodes[4] { ScanCodes.sc_UpArrow, ScanCodes.sc_RightArrow, ScanCodes.sc_DownArrow, ScanCodes.sc_LeftArrow };
    internal static ScanCodes[] buttonscan = new ScanCodes[(int)buttontypes.NUMBUTTONS] { ScanCodes.sc_Control, ScanCodes.sc_Alt, ScanCodes.sc_LShift, ScanCodes.sc_Space, ScanCodes.sc_1, ScanCodes.sc_2, ScanCodes.sc_3, ScanCodes.sc_4, 0,0,0,0,0,0,0,0,0,0 };
    internal static buttontypes[] buttonmouse = new buttontypes[4] { buttontypes.bt_attack, buttontypes.bt_strafe, buttontypes.bt_use, buttontypes.bt_nobutton };
    internal static buttontypes[] buttonjoy = new buttontypes[32] {
        buttontypes.bt_attack, buttontypes.bt_strafe, buttontypes.bt_use, buttontypes.bt_run, buttontypes.bt_strafeleft, buttontypes.bt_straferight, buttontypes.bt_esc, buttontypes.bt_pause,
        buttontypes.bt_prevweapon, buttontypes.bt_nextweapon, buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton,
        buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton,
        buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton, buttontypes.bt_nobutton
    };

    internal static int viewsize;

    static bool demorecord, demoplayback;
    static byte[] demoData;
    static int demoptr, lastdemoptr;


    //
    // current user input
    //
    static int controlx, controly;         // range from -100 to 100 per tic

    static int lastgamemusicoffset = 0;

    /*
    =============================================================================

                                                     LOCAL VARIABLES

    =============================================================================
    */

    internal static void StartMusic()
    {
        //_audioManager.SetPaused(true);
        var gameInfo = _gameEngineManager.GetGameInfo();
        var song = gameInfo.Maps[gamestate.mapon].Music;
        lastmusicchunk = song;
        _audioManager.PlayMusic(lastmusicchunk);
    }

    internal static void ContinueMusic(int offs)
    {
        //_audioManager.SetPaused(true);
        var gameInfo = _gameEngineManager.GetGameInfo();
        var song = gameInfo.Maps[gamestate.mapon].Music;
        lastmusicchunk = song;
        //_audioManager.SetPaused(false);// lastmusicchunk, offs);
    }

    internal static int StopMusic()
    {
        _audioManager.SetPaused(true);
        return 0;
    }

    static int funnyticount;

    internal static void PlayLoop()
    {
        playstate = playstatetypes.ex_stillplaying;
        lasttimecount = (int)GameEngineManager.GetTimeCount();
        frameon = 0;
        anglefrac = 0;
        facecount = 0;
        funnyticount = 0;
        _inputManager.InitButtonState();
        _videoManager.ClearPaletteShifts();

        _inputManager.CenterMouse();

        if (demoplayback)
            _inputManager.StartAck();

        UpdateSoundListener();

        do
        {
            PollControls();

            // With con_pause on, an open console freezes the world but keeps drawing it.
            // CalcTics still advances lasttimecount each frame, so no time builds up to be
            // spent all at once when the console closes.
            bool worldPaused = _consoleManager.IsPausingGame;

            if (!worldPaused)
            {
                //
                // actor thinking
                //
                madenoise = false;
                MoveDoors();
                MovePWalls();

                // Every actor lives in _mapManager._actors. The player is at its head, so it still
                // thinks before every enemy, projectile and the BJ-victory actor.
                _mapManager.DoActors(tics);

                _videoManager.UpdatePaletteShifts(tics);
            }

            ThreeDRefresh();

            if (!worldPaused)
                gamestate.TimeCount += (int)tics;

            UpdateSoundListener();      // JAB
            if (_videoManager.screenfaded)
                _videoManager.FadeIn();

            CheckKeys();

            _consoleManager.RunDeferred();      // console commands that need to run between frames

            //
            // debug aids
            //
            if (singlestep != 0)
            {
                GameEngineManager.WaitVBL(singlestep);
                lasttimecount = (int)GameEngineManager.GetTimeCount();
            }
            if (extravbls != 0)
                GameEngineManager.WaitVBL((uint)extravbls);

            if (demoplayback)
            {
                if (_inputManager.CheckAck())
                {
                    _inputManager.ClearKeysDown();
                    playstate = playstatetypes.ex_abort;
                }
            }
        }
        while (playstate == 0 && !startgame);

        // Intermission, death and menu screens don't draw the console, so don't leave it
        // capturing keys behind them (e.g. after a "map" command ends the level).
        _consoleManager.Close();

        if (playstate != playstatetypes.ex_died)
            _videoManager.FinishPaletteShifts();
    }

    internal static void InitActorList()
    {
        //
        // the player is created first, so it sits at the head of _actors and thinks first
        //
        _mapManager.CreatePlayer();
    }

    internal static void CheckKeys()
    {
        var language = _assetManager.GetText("en-us");
        ScanCodes scan;

        if (_videoManager.screenfaded || demoplayback)    // don't do anything with a faded screen
        {
            while (_inputManager.TryTakePressedKey(out _)) { }     // nor fire binds for these keys later
            return;
        }

        //
        // command console: it only opens from here, so it's never up during a blocking menu or
        // screen with nothing drawing it. Once open, InputManager routes all keys to it.
        //
        if (_consoleManager.IsOpen)
            return;

        if (_inputManager.IsKeyDown(ScanCodes.sc_Grave) && !demorecord)
        {
            _inputManager.ClearKeysDown();
            _inputManager.ClearTextInput();
            _consoleManager.Open();
            return;
        }

        //
        // console binds: run the command bound to each key pressed since the last frame
        // (never while recording, or the demo wouldn't match what was played)
        //
        while (_inputManager.TryTakePressedKey(out var pressed))
        {
            if (!demorecord && _consoleManager.Binds.TryGetValue(pressed, out var boundCommand))
                _consoleManager.Execute(boundCommand);
        }

        scan = _inputManager.GetLastKeyPressed();


        //
        // SECRET CHEAT CODE: 'MLI'
        //
        if (_inputManager.IsKeyDown(ScanCodes.sc_M) && _inputManager.IsKeyDown(ScanCodes.sc_L) && _inputManager.IsKeyDown(ScanCodes.sc_I))
        {
            gamestate.health = 100;
            _inventoryManager.Give(AmmoType, 99);
            _inventoryManager.Give("GoldKey", 1);
            _inventoryManager.Give("SilverKey", 1);
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

            Message("$STR_CHEATER1".ToLanguageText(language) + "\n" +
                    "$STR_CHEATER2".ToLanguageText(language) + "\n\n" +
                    "$STR_CHEATER3".ToLanguageText(language) + "\n" +
                    "$STR_CHEATER4".ToLanguageText(language) + "\n" +
                    "$STR_CHEATER5".ToLanguageText(language));

            _inputManager.ClearKeysDown();
            _inputManager.Ack();

            if (viewsize < 17)
                DrawPlayBorder();
        }

        //
        // OPEN UP DEBUG KEYS
        //
        if (_inputManager.IsKeyDown(ScanCodes.sc_BackSpace) && _inputManager.IsKeyDown(ScanCodes.sc_LShift) && _inputManager.IsKeyDown(ScanCodes.sc_Alt))
        {
            ClearMemory();
            ClearSplitVWB();

            Message("Cheat commands are\nnow available!\nPress ` for the console.");
            _inputManager.ClearKeysDown();
            _inputManager.Ack();

            DrawPlayBorderSides();
            DebugOk = 1;
        }

        //
        // TRYING THE KEEN CHEAT CODE!
        //
        if (_inputManager.IsKeyDown(ScanCodes.sc_B) && _inputManager.IsKeyDown(ScanCodes.sc_A) && _inputManager.IsKeyDown(ScanCodes.sc_T))
        {
            ClearMemory();
            ClearSplitVWB();

            Message("Commander Keen is also\n" +
                        "available from Apogee, but\n" +
                        "then, you already know\n" +
                        "that - right, Cheatmeister?!");

            _inputManager.ClearKeysDown();
            _inputManager.Ack();

            if (viewsize < 18)
                DrawPlayBorder();
        }

        //
        // pause key weirdness can't be checked as a scan code
        //
        if (_inputManager.IsButtonPressed(buttontypes.bt_pause))
            _gameEngineManager.SetPaused(true);
        if (_gameEngineManager.IsPaused())
        {
            int lastoffs = StopMusic();
            _graphicManager.DrawPic("paused", 16 * 8, 80 - 2 * 8);
            _videoManager.Update();
            _inputManager.Ack();
            _gameEngineManager.SetPaused(false);
            ContinueMusic(lastoffs);
            _inputManager.CenterMouse();
            lasttimecount = (int)GameEngineManager.GetTimeCount();
            return;
        }
        if (scan == ScanCodes.sc_F10 ||
            scan == ScanCodes.sc_F9 || scan == ScanCodes.sc_F7 || scan == ScanCodes.sc_F8)     // pop up quit dialog
        {
            ClearMemory();
            ClearSplitVWB();
            US_ControlPanel(scan);

            DrawPlayBorderSides();

            SETFONTCOLOR("Black", "White");
            _inputManager.ClearKeysDown();
            return;
        }

        if ((scan >= ScanCodes.sc_F1 && scan <= ScanCodes.sc_F9) || scan == ScanCodes.sc_Escape || _inputManager.IsButtonPressed(buttontypes.bt_esc))
        {
            int lastoffs = StopMusic();
            ClearMemory();
            _videoManager.FadeOut();

            US_ControlPanel(_inputManager.IsButtonPressed(buttontypes.bt_esc) ? ScanCodes.sc_Escape : scan);

            SETFONTCOLOR("Black", "White");
            _inputManager.ClearKeysDown();
            _videoManager.FadeOut();
            if (viewsize != 21)
                DrawPlayScreen();
            if (!startgame && !loadedgame)
                ContinueMusic(lastoffs);
            if (loadedgame)
                playstate = playstatetypes.ex_abort;
            lasttimecount = (int)GameEngineManager.GetTimeCount();
            _inputManager.CenterMouse();
            return;
        }
    }

    internal static void PollControls()
    {
        int max, min, i;
        byte buttonbits;

        _inputManager.ProcessEvents();

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
                GameEngineManager.DelayMs((uint)timediff);

            if (timediff < -2 * DEMOTICS)       // more than 2-times DEMOTICS behind?
                lasttimecount = (int)((curtime * 7) / 100);    // yes, set to current timecount

            tics = DEMOTICS;
        }
        else
            CalcTics();

        controlx = 0;
        controly = 0;
        _inputManager.ProcessButtons();

        if (demoplayback)
        {
            //
            // read commands from demo buffer
            //
            buttonbits = demoData[demoptr++];
            for (i = 0; i < (int)buttontypes.NUMBUTTONS; i++)
            {
                _inputManager.SetButtonPressed((buttontypes)i, (buttonbits & 1) != 0);
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

        // Keyboard input already bypasses the game while the console is open; the mouse and
        // joystick are polled directly, so skip them too or the player keeps moving and firing.
        if (_consoleManager.IsOpen)
            return;

        //
        // get button states
        //
        PollKeyboardButtons();

        if (mouseenabled && _inputManager.IsMouseInputGrabbed())
            PollMouseButtons();

        if (joystickenabled)
            PollJoystickButtons();

        //
        // get movements
        //
        PollKeyboardMove();

        if (mouseenabled && _inputManager.IsMouseInputGrabbed())
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
                if (_inputManager.IsButtonPressed((buttontypes)i))
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
            if (_inputManager.IsKeyDown((ScanCodes)buttonscan[i]))
                _inputManager.SetButtonPressed((buttontypes)i, true);
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
        int buttons = _inputManager.MouseButtons();

        if ((buttons & 1) != 0)
            _inputManager.SetButtonPressed((buttontypes)buttonmouse[0], true);
        if ((buttons & 2) != 0)
            _inputManager.SetButtonPressed((buttontypes)buttonmouse[1], true);
        if ((buttons & 4) != 0)
            _inputManager.SetButtonPressed((buttontypes)buttonmouse[2], true);
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
        int i, val, buttons = _inputManager.JoyButtons();

        for (i = 0, val = 1; i < _inputManager.JoyNumButtons; i++, val <<= 1)
        {
            if ((buttons & val) != 0)
                _inputManager.SetButtonPressed((buttontypes)buttonjoy[i], true);
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
        int delta = (int)(_inputManager.IsButtonPressed(buttontypes.bt_run) ? RUNMOVE * tics : BASEMOVE * tics);

        if (_inputManager.IsKeyDown(dirscan[(int)controldirs.di_north]))
            controly -= delta;
        if (_inputManager.IsKeyDown(dirscan[(int)controldirs.di_south]))
            controly += delta;
        if (_inputManager.IsKeyDown(dirscan[(int)controldirs.di_west]))
            controlx -= delta;
        if (_inputManager.IsKeyDown(dirscan[(int)controldirs.di_east]))
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

        _inputManager.GetJoyDelta(out joyx, out joyy);

        int delta = (int)(_inputManager.IsButtonPressed(buttontypes.bt_run) ? RUNMOVE * tics : BASEMOVE * tics);

        if (joyx > 64 || _inputManager.IsButtonPressed(buttontypes.bt_turnright))
            controlx += delta;
        else if (joyx < -64 || _inputManager.IsButtonPressed(buttontypes.bt_turnleft))
            controlx -= delta;
        if (joyy > 64 || _inputManager.IsButtonPressed(buttontypes.bt_movebackward))
            controly += delta;
        else if (joyy < -64 || _inputManager.IsButtonPressed(buttontypes.bt_moveforward))
            controly -= delta;
    }
}
