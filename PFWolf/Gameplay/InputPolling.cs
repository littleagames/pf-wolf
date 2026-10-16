using PFWolf.Configuration;
using static SDL2.SDL;

namespace PFWolf.Gameplay;

/// <summary>What polling the controls needs from the game</summary>
/// <param name="SetPlayState">Ends the level (a demo played or recorded to its end)</param>
/// <param name="NetGame">Whether this is a game with others (a fixed step of tics a frame)</param>
/// <param name="NetChatting">Typing something to say to the others: the controls are the line's</param>
/// <param name="NetLeavePrompt">Asking whether to leave the game with others</param>
/// <param name="DemoPlayback">Whether a demo is playing (its controls come from the demo)</param>
/// <param name="DemoRecord">Whether a demo is recording (the controls go into it)</param>
/// <param name="DemoTesting">demotest: playing the demos through as fast as they go</param>
/// <param name="RouteMovementToAutomap">The automap's pan mode takes the movement (Program.Automap.cs)</param>
internal sealed record InputPollingHooks(
    Action<playstatetypes> SetPlayState,
    Func<bool> NetGame,
    Func<bool> NetChatting,
    Func<bool> NetLeavePrompt,
    Func<bool> DemoPlayback,
    Func<bool> DemoRecord,
    Func<bool> DemoTesting,
    Action RouteMovementToAutomap);

/// <summary>
/// Each frame's controls: the tics it moves the game on by (CalcTics, or a fixed step for demos
/// and games with others), then the buttons, keys, mouse and controller read into localcmd, the
/// one TicCmd the game reads (PollControls). Also what's held down for each control
/// (IsControlDown, IsInputActive) and the fresh presses console binds run on.
/// </summary>
internal sealed class InputPolling(
    InputManager inputManager,
    ConsoleManager console,
    AutomapManager automap,
    ControlBindings controls,
    ControlSettings controlSettings,
    FrameRefresh frame,
    DemoBuffer demo,
    GameSession session,
    InputPollingHooks hooks)
{
    //
    // current user input: gathered here each frame by PollControls into localcmd, which is all
    // the game itself reads (through the player's PlayerInput)
    //
    internal TicCmd localcmd;
    internal int controlx, controly;         // range from -100 to 100 per tic
    internal int controlstrafe;              // a controller stick's sideways move, the same range (positive is right)
    internal int wheelnotches;               // the mouse wheel's turn this frame (positive is away from the user)
    internal double controlpitch;            // how far to look up (down if negative) this frame, in degrees
    internal bool controlcenterview;         // look straight ahead again this frame


    // Degrees a tic the look keys (and a stick pushed all the way) look up or down: level to
    // the steepest the view allows (about 19 degrees) in half a second, like Heretic's keys
    internal const double LOOKSPEED = 0.6;

    // Turning speed with a stick pushed all the way, per tic: 50 to 140, 90 by default (the
    // arrow keys turn at BASEMOVE, RUNMOVE running)
    internal int JoyTurnSpeed => 50 + 10 * Math.Clamp(controlSettings.joyturnspeed, 0, ControlSettings.JOYTURNSPEEDS - 1);

    internal void PollControls()
    {
        int max, min;

        inputManager.ProcessEvents();

        //
        // get timing info for last frame
        //
        if (hooks.DemoPlayback() || hooks.DemoRecord())   // demo recording and playback needs to be constant
            WaitFixedTics(GameConstants.DEMOTICS);
        else if (hooks.NetGame())                 // and so does playing with others (Program.NetPlay.cs)
            WaitFixedTics(Networking.NetPlay.NETTICS);
        else
            frame.CalcTics();

        localcmd = default;
        controlx = 0;
        controly = 0;
        controlstrafe = 0;
        controlpitch = 0;
        controlcenterview = false;
        wheelnotches = inputManager.TakeWheelDelta();    // taken every frame, so turns don't pile up
        inputManager.ProcessButtons();

        if (hooks.DemoPlayback())
        {
            //
            // read commands from demo buffer: a byte of buttons (the first eight buttontypes)
            // and a signed byte each of turning and walking a tic
            //
            localcmd.Buttons = demo.demoData[demo.demoptr++];
            localcmd.ControlX = (sbyte)demo.demoData[demo.demoptr++] * (int)session.tics;
            localcmd.ControlY = (sbyte)demo.demoData[demo.demoptr++] * (int)session.tics;

            if (demo.demoptr + 3 > demo.lastdemoptr)
                hooks.SetPlayState(playstatetypes.ex_completed);   // demo is done: no whole frame left

            return;
        }

        // Keyboard input already bypasses the game while the console is open; the mouse and
        // joystick are polled directly, so skip them too or the player keeps moving and firing.
        if (console.IsOpen || hooks.NetChatting() || hooks.NetLeavePrompt())
            return;

        //
        // get button states
        //
        PollButtons();

        //
        // get movements
        //
        PollButtonMove();

        if (controlSettings.mouseenabled && inputManager.IsMouseInputGrabbed())
            PollMouseMove();

        if (controlSettings.joystickenabled)
            PollJoystickMove();

        //
        // bound movement to a maximum
        //
        max = (int)(100 * session.tics);
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

        hooks.RouteMovementToAutomap();       // pan mode: movement moves the map, not the player

        if (hooks.DemoRecord())
        {
            //
            // save info out to demo buffer
            //
            controlx /= (int)session.tics;
            controly /= (int)session.tics;
            controlstrafe = 0;      // a demo has no room for a stick's strafe, so it isn't played either

            // the demo format has room for the first eight buttons only
            demo.demoData[demo.demoptr++] = (byte)LocalButtons();
            demo.demoData[demo.demoptr++] = (byte)controlx; // these might be wrong
            demo.demoData[demo.demoptr++] = (byte)controly;// these might need 4 bytes

            if (demo.demoptr >= demo.lastdemoptr - 8)
                hooks.SetPlayState(playstatetypes.ex_completed);
            else
            {
                controlx *= (int)session.tics;
                controly *= (int)session.tics;
            }
        }

        localcmd = new TicCmd
        {
            Buttons = LocalButtons(),
            ControlX = controlx,
            ControlY = controly,
            ControlStrafe = controlstrafe,
            Pitch = controlpitch,
            CenterView = controlcenterview,
        };
    }

    /// <summary>The buttons pressed this frame, a bit each (bit 0 is bt_attack)</summary>
    internal uint LocalButtons()
    {
        uint buttons = 0;
        for (int i = 0; i < (int)buttontypes.NUMBUTTONS; i++)
        {
            if (inputManager.IsButtonPressed((buttontypes)i))
                buttons |= 1u << i;
        }
        return buttons;
    }

    /// <summary>
    /// Runs the game a fixed step of tics a frame, as demos need: waits until that much time has
    /// passed since the last frame (demotest doesn't), and if the game has fallen more than two
    /// steps behind, gives up on catching up.
    /// </summary>
    internal void WaitFixedTics(uint step)
    {
        uint curtime = SDL_GetTicks();
        frame.lasttimecount += (int)step;
        int timediff = (int)((frame.lasttimecount * 100) / 7 - curtime);
        if (timediff > 0 && !hooks.DemoTesting())      // demotest doesn't wait
            GameEngineManager.DelayMs((uint)timediff);

        if (timediff < -2 * step)       // more than 2-times the step behind?
            frame.lasttimecount = (int)((curtime * 7) / 100);    // yes, set to current timecount

        session.tics = step;
    }

    /*
    ===================
    =
    = PollButtons
    =
    ===================
    */

    /// <summary>Presses every button with something bound to it held down.</summary>
    internal void PollButtons()
    {
        for (int i = 0; i < (int)buttontypes.NUMBUTTONS; i++)
        {
            if (IsControlDown(ControlAction.Of((buttontypes)i)))
                inputManager.SetButtonPressed((buttontypes)i, true);
        }
    }

    /// <summary>
    /// Whether anything bound to an action is held down. While the automap is open, an input
    /// bound to one of its keys belongs to it, so it doesn't press a play button as well (the
    /// wheel zooms the map then, rather than changing weapons).
    /// </summary>
    internal bool IsControlDown(ControlAction action)
    {
        bool mapFirst = action.IsButton && automap.IsOpen;

        foreach (var code in controls.Get(action))
        {
            if (mapFirst && IsAutomapInput(code))
                continue;

            if (IsInputActive(code))
                return true;
        }
        return false;
    }

    internal bool IsAutomapInput(InputCode code) =>
        ControlAction.All.Any(action => action.IsAutomapKey && controls.IsBound(action, code));

    /// <summary>
    /// Whether an input is down this frame. Mouse buttons only count while the mouse is enabled
    /// and grabbed (so the click that brings the window forward doesn't fire), the wheel while it's
    /// enabled, and a controller or joystick while it's enabled. The wheel is down for the frame it turns in.
    /// </summary>
    internal bool IsInputActive(InputCode code) => code.Device switch
    {
        InputDevice.MouseButton => controlSettings.mouseenabled && inputManager.IsMouseInputGrabbed() && inputManager.IsInputDown(code),
        InputDevice.MouseWheel => controlSettings.mouseenabled && WheelNotches(code) > 0,
        InputDevice.JoyButton or InputDevice.PadButton or InputDevice.PadAxis => controlSettings.joystickenabled && inputManager.IsInputDown(code),
        _ => inputManager.IsInputDown(code),
    };

    // Mouse and controller inputs that were down last frame, so a press is only taken once
    internal readonly HashSet<InputCode> heldinputs = [];

    /// <summary>
    /// The mouse buttons, wheel turns and controller buttons with a console bind or an automap
    /// key on them that went down since the last frame. Keys come from InputManager's own queue.
    /// </summary>
    internal List<InputCode> TakeFreshPresses()
    {
        var watched = console.Binds.Keys
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
    internal int WheelNotches(InputCode code) =>
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
    internal void PollButtonMove()
    {
        int delta = (int)(inputManager.IsButtonPressed(buttontypes.bt_run) ? GameConstants.RUNMOVE * session.tics : GameConstants.BASEMOVE * session.tics);

        if (inputManager.IsButtonPressed(buttontypes.bt_moveforward))
            controly -= delta;
        if (inputManager.IsButtonPressed(buttontypes.bt_movebackward))
            controly += delta;
        if (inputManager.IsButtonPressed(buttontypes.bt_turnleft))
            controlx -= delta;
        if (inputManager.IsButtonPressed(buttontypes.bt_turnright))
            controlx += delta;

        if (inputManager.IsButtonPressed(buttontypes.bt_lookup))
            controlpitch += LOOKSPEED * session.tics;
        if (inputManager.IsButtonPressed(buttontypes.bt_lookdown))
            controlpitch -= LOOKSPEED * session.tics;
        if (inputManager.IsButtonPressed(buttontypes.bt_centerview))
            controlcenterview = true;
    }


    /*
    ===================
    =
    = PollMouseMove
    =
    ===================
    */

    internal void PollMouseMove()
    {
        int mousexmove, mouseymove;

        SDL_GetRelativeMouseState(out mousexmove, out mouseymove);

        controlx += mousexmove * 10 / (13 - controlSettings.mouseadjustment);

        // Looking goes as fast as turning: controlx turns a degree every ANGLESCALE. In the
        // automap's pan mode the mouse pans the map, so it walks (RouteMovementToAutomap takes it).
        if (controlSettings.mouselook && !(automap.IsOpen && !automap.Follow))
            controlpitch += (controlSettings.mouseinvert ? mouseymove : -mouseymove) * 10.0 / (13 - controlSettings.mouseadjustment) / Program.ANGLESCALE;
        else
            controly += mouseymove * 20 / (13 - controlSettings.mouseadjustment);
    }


    /*
    ===================
    =
    = PollJoystickMove
    =
    ===================
    */

    internal void PollJoystickMove()
    {
        int joyx, joyy;

        int speed = inputManager.IsButtonPressed(buttontypes.bt_run) ? GameConstants.RUNMOVE : GameConstants.BASEMOVE;

        // A controller: the left stick walks and strafes and the right stick turns (classic: the
        // left stick walks and turns and the right strafes), as fast as they're pushed. Turning
        // goes up with the square of the push, for fine aim near the middle.
        if (inputManager.HasGameController)
        {
            var turnAxis = controlSettings.joyclassicsticks ? SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX : SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX;
            var strafeAxis = controlSettings.joyclassicsticks ? SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX : SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX;
            float turn = StickValue(turnAxis);

            controly += (int)(StickValue(SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY) * speed * session.tics);
            controlstrafe += (int)(StickValue(strafeAxis) * speed * session.tics);
            controlx += (int)(turn * Math.Abs(turn) * JoyTurnSpeed * session.tics);

            // the right stick's up and down looks, whichever way the sticks are set
            float look = StickValue(SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY);
            controlpitch -= look * Math.Abs(look) * LOOKSPEED * session.tics;
            return;
        }

        // A plain joystick: its stick walks and turns at full speed once it's pushed halfway
        inputManager.GetJoyDelta(out joyx, out joyy);

        int delta = (int)(speed * session.tics);

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
    internal float StickValue(SDL_GameControllerAxis axis)
    {
        float value = inputManager.GetPadAxis(axis);
        float deadzone = Math.Clamp(controlSettings.joydeadzone, 0, 90) / 100f;

        if (Math.Abs(value) <= deadzone)
            return 0;

        return Math.Sign(value) * (Math.Abs(value) - deadzone) / (1 - deadzone);
    }
}
