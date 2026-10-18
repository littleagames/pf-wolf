using PFWolf.Configuration;
using static SDL2.SDL;

namespace PFWolf.Gameplay;

/// <summary>What polling the controls needs from the game</summary>
/// <param name="NetChatting">Typing something to say to the others: the controls are the line's</param>
/// <param name="NetLeavePrompt">Asking whether to leave the game with others</param>
/// <param name="DemoTesting">demotest: playing the demos through as fast as they go</param>
/// <param name="RouteMovementToAutomap">The automap's pan mode takes the movement (Ui/AutomapInput.cs)</param>
internal sealed record InputPollingHooks(
    Func<bool> NetChatting,
    Func<bool> NetLeavePrompt,
    Func<bool> DemoTesting,
    Action RouteMovementToAutomap);

/// <summary>
/// Each frame's controls: the tics it moves the game on by (CalcTics, or a fixed step for demos
/// and games with others), then the buttons, keys, mouse and controller read into LocalCmd, the
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
    InputPollingHooks hooks,
    GameOptions? gameOptions = null)
{
    // freelook and alwaysrun (Configuration/GameOptions.cs)
    private readonly GameOptions options = gameOptions ?? new();

    /// <summary>Whether Run is in effect: held, or not held with alwaysrun on</summary>
    internal bool Running => inputManager.IsButtonPressed(ButtonType.Run) != options.AlwaysRun;

    //
    // current user input: gathered here each frame by PollControls into LocalCmd, which is all
    // the game itself reads (through the player's PlayerInput)
    //
    internal TicCmd LocalCmd;
    internal int ControlX, ControlY;         // range from -100 to 100 per tic
    internal int ControlStrafe;              // a controller stick's sideways move, the same range (positive is right)
    internal int WheelNotchesThisFrame;               // the mouse wheel's turn this frame (positive is away from the user)
    internal double ControlPitch;            // how far to look up (down if negative) this frame, in degrees
    internal bool ControlCenterView;         // look straight ahead again this frame


    // Degrees a tic the look keys (and a stick pushed all the way) look up or down: level to
    // the steepest the view allows (about 19 degrees) in half a second, like Heretic's keys
    internal const double LookSpeed = 0.6;

    // Turning speed with a stick pushed all the way, per tic: 50 to 140, 90 by default (the
    // arrow keys turn at BaseMove, RunMove running)
    internal int JoyTurnSpeed => 50 + 10 * Math.Clamp(controlSettings.JoyTurnSpeed, 0, ControlSettings.JoyTurnSpeeds - 1);

    internal void PollControls()
    {
        int max, min;

        inputManager.ProcessEvents();

        //
        // get timing info for last frame
        //
        if (session.DemoPlayback || session.DemoRecord)   // demo recording and playback needs to be constant
            WaitFixedTics(GameConstants.DemoTics);
        else if (session.NetGame)                 // and so does playing with others (Networking/NetPlay.cs)
            WaitFixedTics(Networking.NetPlay.NetTics);
        else
            frame.CalcTics();

        LocalCmd = default;
        ControlX = 0;
        ControlY = 0;
        ControlStrafe = 0;
        ControlPitch = 0;
        ControlCenterView = false;
        WheelNotchesThisFrame = inputManager.TakeWheelDelta();    // taken every frame, so turns don't pile up
        inputManager.ProcessButtons();

        if (session.DemoPlayback)
        {
            //
            // read commands from demo buffer: a byte of buttons (the first eight ButtonType)
            // and a signed byte each of turning and walking a tic
            //
            LocalCmd.Buttons = demo.DemoData[demo.DemoPtr++];
            LocalCmd.ControlX = (sbyte)demo.DemoData[demo.DemoPtr++] * (int)session.Tics;
            LocalCmd.ControlY = (sbyte)demo.DemoData[demo.DemoPtr++] * (int)session.Tics;

            if (demo.DemoPtr + 3 > demo.LastDemoPtr)
                session.PlayState = PlayState.Completed;   // demo is done: no whole frame left

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

        if (controlSettings.MouseEnabled && inputManager.IsMouseInputGrabbed())
            PollMouseMove();

        if (controlSettings.JoystickEnabled)
            PollJoystickMove();

        //
        // bound movement to a maximum
        //
        max = (int)(100 * session.Tics);
        min = -max;
        if (ControlX > max)
            ControlX = max;
        else if (ControlX < min)
            ControlX = min;

        if (ControlY > max)
            ControlY = max;
        else if (ControlY < min)
            ControlY = min;

        ControlStrafe = Math.Clamp(ControlStrafe, min, max);

        hooks.RouteMovementToAutomap();       // pan mode: movement moves the map, not the player

        // With freelook off (a mod that wants the original's level view), nothing looks up or
        // down, and the view goes back to level if it was looking when it was turned off
        if (!options.Look)
        {
            ControlPitch = 0;
            ControlCenterView = true;
        }

        if (session.DemoRecord)
        {
            //
            // save info out to demo buffer
            //
            ControlX /= (int)session.Tics;
            ControlY /= (int)session.Tics;
            ControlStrafe = 0;      // a demo has no room for a stick's strafe, so it isn't played either

            // the demo format has room for the first eight buttons only
            demo.DemoData[demo.DemoPtr++] = (byte)LocalButtons();
            demo.DemoData[demo.DemoPtr++] = (byte)ControlX; // these might be wrong
            demo.DemoData[demo.DemoPtr++] = (byte)ControlY;// these might need 4 bytes

            if (demo.DemoPtr >= demo.LastDemoPtr - 8)
                session.PlayState = PlayState.Completed;
            else
            {
                ControlX *= (int)session.Tics;
                ControlY *= (int)session.Tics;
            }
        }

        LocalCmd = new TicCmd
        {
            Buttons = LocalButtons(),
            ControlX = ControlX,
            ControlY = ControlY,
            ControlStrafe = ControlStrafe,
            Pitch = ControlPitch,
            CenterView = ControlCenterView,
        };
    }

    /// <summary>The buttons pressed this frame, a bit each (bit 0 is Attack)</summary>
    internal uint LocalButtons()
    {
        uint buttons = 0;
        for (int i = 0; i < (int)ButtonType.Count; i++)
        {
            if (inputManager.IsButtonPressed((ButtonType)i))
                buttons |= 1u << i;
        }

        // alwaysrun turns Run over, so demos and the other players see it as held
        if (options.AlwaysRun)
            buttons ^= 1u << (int)ButtonType.Run;
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
        frame.LastTimeCount += (int)step;
        int timediff = (int)((frame.LastTimeCount * 100) / 7 - curtime);
        if (timediff > 0 && !hooks.DemoTesting())      // demotest doesn't wait
            GameEngineManager.DelayMs((uint)timediff);

        if (timediff < -2 * step)       // more than 2-times the step behind?
            frame.LastTimeCount = (int)((curtime * 7) / 100);    // yes, set to current timecount

        session.Tics = step;
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
        for (int i = 0; i < (int)ButtonType.Count; i++)
        {
            if (IsControlDown(ControlAction.Of((ButtonType)i)))
                inputManager.SetButtonPressed((ButtonType)i, true);
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
        InputDevice.MouseButton => controlSettings.MouseEnabled && inputManager.IsMouseInputGrabbed() && inputManager.IsInputDown(code),
        InputDevice.MouseWheel => controlSettings.MouseEnabled && WheelNotches(code) > 0,
        InputDevice.JoyButton or InputDevice.PadButton or InputDevice.PadAxis => controlSettings.JoystickEnabled && inputManager.IsInputDown(code),
        _ => inputManager.IsInputDown(code),
    };

    // Mouse and controller inputs that were down last frame, so a press is only taken once
    internal readonly HashSet<InputCode> HeldInputs = [];

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
                HeldInputs.Remove(input);
            else if (HeldInputs.Add(input) || input.Device == InputDevice.MouseWheel)   // every notch is a press
                fresh.Add(input);
        }
        return fresh;
    }

    /// <summary>How many notches the wheel turned this frame the way a wheel input stands for (0 for anything else).</summary>
    internal int WheelNotches(InputCode code) =>
        code == InputCode.WheelUp ? Math.Max(WheelNotchesThisFrame, 0)
        : code == InputCode.WheelDown ? Math.Max(-WheelNotchesThisFrame, 0)
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
        int delta = (int)(Running ? GameConstants.RunMove * session.Tics : GameConstants.BaseMove * session.Tics);

        if (inputManager.IsButtonPressed(ButtonType.MoveForward))
            ControlY -= delta;
        if (inputManager.IsButtonPressed(ButtonType.MoveBackward))
            ControlY += delta;
        if (inputManager.IsButtonPressed(ButtonType.TurnLeft))
            ControlX -= delta;
        if (inputManager.IsButtonPressed(ButtonType.TurnRight))
            ControlX += delta;

        if (inputManager.IsButtonPressed(ButtonType.LookUp))
            ControlPitch += LookSpeed * session.Tics;
        if (inputManager.IsButtonPressed(ButtonType.LookDown))
            ControlPitch -= LookSpeed * session.Tics;
        if (inputManager.IsButtonPressed(ButtonType.CenterView))
            ControlCenterView = true;
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

        ControlX += mousexmove * 10 / (13 - controlSettings.MouseAdjustment);

        // Looking goes as fast as turning: ControlX turns a degree every AngleScale. In the
        // automap's pan mode the mouse pans the map, so it walks (RouteMovementToAutomap takes it).
        if (controlSettings.MouseLook && options.Look && !(automap.IsOpen && !automap.Follow))
            ControlPitch += (controlSettings.MouseInvert ? mouseymove : -mouseymove) * 10.0 / (13 - controlSettings.MouseAdjustment) / GameConstants.AngleScale;
        else
            ControlY += mouseymove * 20 / (13 - controlSettings.MouseAdjustment);
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

        int speed = Running ? GameConstants.RunMove : GameConstants.BaseMove;

        // A controller: the left stick walks and strafes and the right stick turns (classic: the
        // left stick walks and turns and the right strafes), as fast as they're pushed. Turning
        // goes up with the square of the push, for fine aim near the middle.
        if (inputManager.HasGameController)
        {
            var turnAxis = controlSettings.JoyClassicSticks ? SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX : SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX;
            var strafeAxis = controlSettings.JoyClassicSticks ? SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX : SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX;
            float turn = StickValue(turnAxis);

            ControlY += (int)(StickValue(SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY) * speed * session.Tics);
            ControlStrafe += (int)(StickValue(strafeAxis) * speed * session.Tics);
            ControlX += (int)(turn * Math.Abs(turn) * JoyTurnSpeed * session.Tics);

            // the right stick's up and down looks, whichever way the sticks are set
            float look = StickValue(SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTY);
            ControlPitch -= look * Math.Abs(look) * LookSpeed * session.Tics;
            return;
        }

        // A plain joystick: its stick walks and turns at full speed once it's pushed halfway
        inputManager.GetJoyDelta(out joyx, out joyy);

        int delta = (int)(speed * session.Tics);

        // The movement buttons are in PollButtonMove, whatever they're bound to
        if (joyx > 64)
            ControlX += delta;
        else if (joyx < -64)
            ControlX -= delta;
        if (joyy > 64)
            ControlY += delta;
        else if (joyy < -64)
            ControlY -= delta;
    }

    /// <summary>A controller stick's axis from -1 to 1, with the dead zone taken out and the rest stretched to fill it.</summary>
    internal float StickValue(SDL_GameControllerAxis axis)
    {
        float value = inputManager.GetPadAxis(axis);
        float deadzone = Math.Clamp(controlSettings.JoyDeadzone, 0, 90) / 100f;

        if (Math.Abs(value) <= deadzone)
            return 0;

        return Math.Sign(value) * (Math.Abs(value) - deadzone) / (1 - deadzone);
    }
}
