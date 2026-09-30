using Wolf3D.Configuration;
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

    /// <summary>The keys, mouse buttons and joystick buttons bound to each button and automap key (saved to controls.cfg).</summary>
    internal static readonly ControlBindings controls = new();

    internal static int viewsize;

    static bool demorecord, demoplayback;
    static byte[] demoData;
    static int demoptr, lastdemoptr;


    //
    // current user input
    //
    static int controlx, controly;         // range from -100 to 100 per tic
    static int controlstrafe;              // a controller stick's sideways move, the same range (positive is right)
    static int wheelnotches;               // the mouse wheel's turn this frame (positive is away from the user)
    static double controlpitch;            // how far to look up (down if negative) this frame, in degrees
    static bool controlcenterview;         // look straight ahead again this frame

    // Mouse look (m_look, m_invert in controls.cfg): moving the mouse up and down looks up and
    // down rather than walking, the other way round if inverted
    internal static bool mouselook, mouseinvert;

    // Degrees a tic the look keys (and a stick pushed all the way) look up or down: level to
    // the steepest the view allows (about 19 degrees) in half a second, like Heretic's keys
    const double LOOKSPEED = 0.6;

    // The controller settings (the Controller Settings menu, and joy_* in controls.cfg):
    // how much of a stick's travel around the middle is ignored, in percent; how fast a stick
    // turns, as a step of JoyTurnSpeed; and whether the left stick turns, with the right
    // strafing (classic), rather than the right stick turning (modern).
    internal static int joydeadzone = 20;
    internal static int joyturnspeed = 4;
    internal static bool joyclassicsticks;

    internal const int JOYTURNSPEEDS = 10;

    // Turning speed with a stick pushed all the way, per tic: 50 to 140, 90 by default (the
    // arrow keys turn at BASEMOVE, RUNMOVE running)
    static int JoyTurnSpeed => 50 + 10 * Math.Clamp(joyturnspeed, 0, JOYTURNSPEEDS - 1);

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

    // Music offsets aren't tracked, so this resumes the level's song if it's the one paused, and
    // otherwise (after the menu or a loaded game replaced it) starts it over.
    internal static void ContinueMusic(int offs)
    {
        var gameInfo = _gameEngineManager.GetGameInfo();
        var song = gameInfo.Maps[gamestate.mapon].Music;
        lastmusicchunk = song;
        if (string.Equals(_audioManager.CurrentMusicTrack, song, StringComparison.OrdinalIgnoreCase))
            _audioManager.SetPaused(false);
        else
            _audioManager.PlayMusic(song);
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

        _hudMessageManager.Clear();     // nothing left over from the last level, life or saved game

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
                _hudMessageManager.Tick((int)tics);
            }

            UpdateAutomap();

            ThreeDRefresh();

            if (autosavePending)
                AutoSaveGame();         // now there's a picture of the level for it

            if (!worldPaused)
            {
                gamestate.TimeCount += (int)tics;
                gamestate.PlayTime += (int)tics;
            }

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
        _automapManager.Close();

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
            _automapManager.Close();
            _consoleManager.Open();
            return;
        }

        //
        // automap
        //
        if (_inputManager.IsButtonPressed(buttontypes.bt_automap) && !_inputManager.IsButtonHeld(buttontypes.bt_automap))
            ToggleAutomap();

        //
        // console binds: run the command bound to each key or button pressed since the last
        // frame (never while recording, or the demo wouldn't match what was played)
        //
        var pressedInputs = new List<InputCode>();
        while (_inputManager.TryTakePressedKey(out var pressed))
            pressedInputs.Add(InputCode.FromKey(pressed));
        pressedInputs.AddRange(TakeFreshPresses());

        foreach (var input in pressedInputs)
        {
            if (HandleAutomapKey(input))        // the automap's own keys, while it's open
                continue;

            if (!demorecord && _consoleManager.Binds.TryGetValue(input, out var boundCommand))
                _consoleManager.Execute(boundCommand);
        }

        scan = _inputManager.GetLastKeyPressed();


        //
        // SECRET CHEAT CODE: 'MLI'
        //
        if (_inputManager.IsKeyDown(ScanCodes.sc_M) && _inputManager.IsKeyDown(ScanCodes.sc_L) && _inputManager.IsKeyDown(ScanCodes.sc_I))
        {
            gamestate.health = 100;
            _inventoryManager.Give("GoldKey", 1);
            _inventoryManager.Give("SilverKey", 1);
            gamestate.score = 0;
            gamestate.TimeCount += (int)42000L;
            // the best weapon there is (the gatling gun)
            if (AllWeapons().OrderBy(WeaponSelectionOrder).FirstOrDefault() is { } bestWeapon)
                GiveWeapon(bestWeapon);
            GiveAllAmmo(99);
            DrawWeapon();
            DrawHealth();
            DrawKeys();
            DrawAmmo();
            DrawScore();

            ClearMemory();

            Message("$STR_CHEATER1".ToLanguageText(language) + "\n" +
                    "$STR_CHEATER2".ToLanguageText(language) + "\n\n" +
                    "$STR_CHEATER3".ToLanguageText(language) + "\n" +
                    "$STR_CHEATER4".ToLanguageText(language) + "\n" +
                    "$STR_CHEATER5".ToLanguageText(language), MAXY);

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

            Message("Cheat commands are\nnow available!\nPress ` for the console.", MAXY);
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

            Message("Commander Keen is also\n" +
                        "available from Apogee, but\n" +
                        "then, you already know\n" +
                        "that - right, Cheatmeister?!", MAXY);

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
            _automapManager.Close();
            ClearMemory();
            US_ControlPanel(scan);

            DrawPlayBorderSides();

            _inputManager.ClearKeysDown();
            return;
        }

        if ((scan >= ScanCodes.sc_F1 && scan <= ScanCodes.sc_F9) || scan == ScanCodes.sc_Escape || _inputManager.IsButtonPressed(buttontypes.bt_esc))
        {
            int lastoffs = StopMusic();
            _automapManager.Close();
            ClearMemory();
            _videoManager.FadeOut();

            US_ControlPanel(_inputManager.IsButtonPressed(buttontypes.bt_esc) ? ScanCodes.sc_Escape : scan);

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
        controlstrafe = 0;
        controlpitch = 0;
        controlcenterview = false;
        wheelnotches = _inputManager.TakeWheelDelta();    // taken every frame, so turns don't pile up
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

            if (demoptr + 3 > lastdemoptr)
                playstate = playstatetypes.ex_completed;   // demo is done: no whole frame left

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
        PollButtons();

        //
        // get movements
        //
        PollButtonMove();

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

        controlstrafe = Math.Clamp(controlstrafe, min, max);

        RouteMovementToAutomap();       // pan mode: movement moves the map, not the player

        if (demorecord)
        {
            //
            // save info out to demo buffer
            //
            controlx /= (int)tics;
            controly /= (int)tics;
            controlstrafe = 0;      // a demo has no room for a stick's strafe, so it isn't played either

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

    /// <summary>Black text on the white of a <see cref="CenterWindow"/></summary>
    internal static readonly Fonts.TextStyle PromptStyle = new(SMALL_FONT, "Black", "White");

    /// <summary>A framed window of w by h tiles, centered in the play view</summary>
    internal static Fonts.TextWindow CenterWindow(int w, int h, Fonts.TextStyle style)
        => US_DrawWindow(((MAXX / 8) - w) / 2, ((MAXY / 8) - h) / 2, w, h, style);

/*
=============================================================================

                               USER CONTROL

=============================================================================
*/

    /*
    ===================
    =
    = PollButtons
    =
    ===================
    */

    /// <summary>Presses every button with something bound to it held down.</summary>
    internal static void PollButtons()
    {
        for (int i = 0; i < (int)buttontypes.NUMBUTTONS; i++)
        {
            if (IsControlDown(ControlAction.Of((buttontypes)i)))
                _inputManager.SetButtonPressed((buttontypes)i, true);
        }
    }

    /// <summary>
    /// Whether anything bound to an action is held down. While the automap is open, an input
    /// bound to one of its keys belongs to it, so it doesn't press a play button as well (the
    /// wheel zooms the map then, rather than changing weapons).
    /// </summary>
    internal static bool IsControlDown(ControlAction action)
    {
        bool mapFirst = action.IsButton && _automapManager.IsOpen;

        foreach (var code in controls.Get(action))
        {
            if (mapFirst && IsAutomapInput(code))
                continue;

            if (IsInputActive(code))
                return true;
        }
        return false;
    }

    static bool IsAutomapInput(InputCode code) =>
        ControlAction.All.Any(action => action.IsAutomapKey && controls.IsBound(action, code));

    /// <summary>
    /// Whether an input is down this frame. Mouse buttons only count while the mouse is enabled
    /// and grabbed (so the click that brings the window forward doesn't fire), the wheel while it's
    /// enabled, and a controller or joystick while it's enabled. The wheel is down for the frame it turns in.
    /// </summary>
    internal static bool IsInputActive(InputCode code) => code.Device switch
    {
        InputDevice.MouseButton => mouseenabled && _inputManager.IsMouseInputGrabbed() && _inputManager.IsInputDown(code),
        InputDevice.MouseWheel => mouseenabled && WheelNotches(code) > 0,
        InputDevice.JoyButton or InputDevice.PadButton or InputDevice.PadAxis => joystickenabled && _inputManager.IsInputDown(code),
        _ => _inputManager.IsInputDown(code),
    };

    // Mouse and controller inputs that were down last frame, so a press is only taken once
    static readonly HashSet<InputCode> heldinputs = [];

    /// <summary>
    /// The mouse buttons, wheel turns and controller buttons with a console bind or an automap
    /// key on them that went down since the last frame. Keys come from InputManager's own queue.
    /// </summary>
    static List<InputCode> TakeFreshPresses()
    {
        var watched = _consoleManager.Binds.Keys
            .Concat(ControlAction.All.Where(action => action.IsAutomapKey).SelectMany(controls.Get))
            .Where(input => input.Device != InputDevice.Key)
            .Distinct();

        var fresh = new List<InputCode>();
        foreach (var input in watched)
        {
            if (!IsInputActive(input))
                heldinputs.Remove(input);
            else if (heldinputs.Add(input) || input.Device == InputDevice.MouseWheel)   // every notch is a press
                fresh.Add(input);
        }
        return fresh;
    }

    /// <summary>How many notches the wheel turned this frame the way a wheel input stands for (0 for anything else).</summary>
    static int WheelNotches(InputCode code) =>
        code == InputCode.WheelUp ? Math.Max(wheelnotches, 0)
        : code == InputCode.WheelDown ? Math.Max(-wheelnotches, 0)
        : 0;

    /*
    ===================
    =
    = PollButtonMove
    =
    ===================
    */

    /// <summary>Walking and turning from the movement buttons, whatever they're bound to.</summary>
    internal static void PollButtonMove()
    {
        int delta = (int)(_inputManager.IsButtonPressed(buttontypes.bt_run) ? RUNMOVE * tics : BASEMOVE * tics);

        if (_inputManager.IsButtonPressed(buttontypes.bt_moveforward))
            controly -= delta;
        if (_inputManager.IsButtonPressed(buttontypes.bt_movebackward))
            controly += delta;
        if (_inputManager.IsButtonPressed(buttontypes.bt_turnleft))
            controlx -= delta;
        if (_inputManager.IsButtonPressed(buttontypes.bt_turnright))
            controlx += delta;

        if (_inputManager.IsButtonPressed(buttontypes.bt_lookup))
            controlpitch += LOOKSPEED * tics;
        if (_inputManager.IsButtonPressed(buttontypes.bt_lookdown))
            controlpitch -= LOOKSPEED * tics;
        if (_inputManager.IsButtonPressed(buttontypes.bt_centerview))
            controlcenterview = true;
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

        // Looking goes as fast as turning: controlx turns a degree every ANGLESCALE. In the
        // automap's pan mode the mouse pans the map, so it walks (RouteMovementToAutomap takes it).
        if (mouselook && !(_automapManager.IsOpen && !_automapManager.Follow))
            controlpitch += (mouseinvert ? mouseymove : -mouseymove) * 10.0 / (13 - mouseadjustment) / ANGLESCALE;
        else
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

        int speed = _inputManager.IsButtonPressed(buttontypes.bt_run) ? RUNMOVE : BASEMOVE;

        // A controller: the left stick walks and strafes and the right stick turns (classic: the
        // left stick walks and turns and the right strafes), as fast as they're pushed. Turning
        // goes up with the square of the push, for fine aim near the middle.
        if (_inputManager.HasGameController)
        {
            var turnAxis = joyclassicsticks ? SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX : SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX;
            var strafeAxis = joyclassicsticks ? SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX : SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX;
            float turn = StickValue(turnAxis);

            controly += (int)(StickValue(SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY) * speed * tics);
            controlstrafe += (int)(StickValue(strafeAxis) * speed * tics);
            controlx += (int)(turn * Math.Abs(turn) * JoyTurnSpeed * tics);

            // the right stick's up and down looks, whichever way the sticks are set
            float look = StickValue(SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY);
            controlpitch -= look * Math.Abs(look) * LOOKSPEED * tics;
            return;
        }

        // A plain joystick: its stick walks and turns at full speed once it's pushed halfway
        _inputManager.GetJoyDelta(out joyx, out joyy);

        int delta = (int)(speed * tics);

        // The movement buttons are in PollButtonMove, whatever they're bound to
        if (joyx > 64)
            controlx += delta;
        else if (joyx < -64)
            controlx -= delta;
        if (joyy > 64)
            controly += delta;
        else if (joyy < -64)
            controly -= delta;
    }

    /// <summary>A controller stick's axis from -1 to 1, with the dead zone taken out and the rest stretched to fill it.</summary>
    static float StickValue(SDL_GameControllerAxis axis)
    {
        float value = _inputManager.GetPadAxis(axis);
        float deadzone = Math.Clamp(joydeadzone, 0, 90) / 100f;

        if (Math.Abs(value) <= deadzone)
            return 0;

        return Math.Sign(value) * (Math.Abs(value) - deadzone) / (1 - deadzone);
    }
}
