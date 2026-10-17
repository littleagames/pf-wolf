using SDL2;

namespace PFWolf.Ui;

/// <summary>
/// The menus' input: the keyboard, mouse and controller read as one (ReadAnyControl), waiting
/// for buttons to be let go, and the pause key
/// </summary>
/// <param name="mouseEnabled">Whether the mouse is on (the Control menu's setting)</param>
/// <param name="joystickEnabled">Whether the controller is on</param>
internal sealed class MenuInput(InputManager input, AudioManager audio, GameEngineManager engine, Func<bool> mouseEnabled, Func<bool> joystickEnabled)
{
    internal int Sensitive = 60;

    internal int SoundStatus = 1;

    internal void CheckPause()
    {
        if (engine.IsPaused())
        {
            switch (SoundStatus)
            {
                case 0:
                    audio.SetPaused(false);
                    break;
                case 1:
                    audio.SetPaused(true);
                    break;
            }

            SoundStatus ^= 1;
            GameEngineManager.WaitVBL(3);
            input.ClearKeysDown();
            engine.SetPaused(false);
        }
    }

    internal void TicDelay(int count)
    {
        ControlInfo ci;

        int startTime = (int)GameEngineManager.GetTimeCount();

        do
        {
            GameEngineManager.DelayMs(5);
            ReadAnyControl(out ci);
        }
        while ((int)GameEngineManager.GetTimeCount() - startTime < count && ci.dir != Direction.None);
    }

    int totalMousex = 0, totalMousey = 0;
    internal void ReadAnyControl(out ControlInfo ci)
    {
        int mouseactive = 0;
        input.ReadControl(out ci);
        if (mouseEnabled() && input.IsMouseInputGrabbed())
        {
            int mousex, mousey, buttons;

            buttons = (int)SDL.SDL_GetRelativeMouseState(out mousex, out mousey);


            int middlePressed = (int)(buttons & SDL.SDL_BUTTON(SDL.SDL_BUTTON_MIDDLE));
            int rightPressed = (int)(buttons & SDL.SDL_BUTTON(SDL.SDL_BUTTON_RIGHT));
            buttons &= (int)~(SDL.SDL_BUTTON(SDL.SDL_BUTTON_MIDDLE) | SDL.SDL_BUTTON(SDL.SDL_BUTTON_RIGHT));
            if (middlePressed != 0) buttons |= 1 << 2;
            if (rightPressed != 0) buttons |= 1 << 1;

            totalMousex += mousex;
            totalMousey += mousey;

            if (totalMousey < -Sensitive)
            {
                ci.dir = Direction.North;
                mouseactive = 1;
            }
            else if (totalMousey > Sensitive)
            {
                ci.dir = Direction.South;
                mouseactive = 1;
            }

            if (totalMousex < -Sensitive)
            {
                ci.dir = Direction.West;
                mouseactive = 1;
            }
            else if (totalMousex > Sensitive)
            {
                ci.dir = Direction.East;
                mouseactive = 1;
            }

            if (mouseactive != 0)
            {
                totalMousex = 0;
                totalMousey = 0;
            }

            if (buttons != 0)
            {
                ci.button0 = (buttons & 1) != 0;
                ci.button1 = (buttons & 2) != 0;
                ci.button2 = (buttons & 4) != 0;
                ci.button3 = false;
                mouseactive = 1;
            }
        }

        if (joystickEnabled() && mouseactive == 0)
        {
            int jx, jy, jb;

            input.GetJoyDelta(out jx, out jy);
            if (jy < -Sensitive)
                ci.dir = Direction.North;
            else if (jy > Sensitive)
                ci.dir = Direction.South;

            if (jx < -Sensitive)
                ci.dir = Direction.West;
            else if (jx > Sensitive)
                ci.dir = Direction.East;

            jb = input.JoyButtons();
            if (jb != 0)
            {
                ci.button0 = (jb & 1) != 0;
                ci.button1 = (jb & 2) != 0;
                ci.button2 = (jb & 4) != 0;
                ci.button3 = (jb & 8) != 0;
            }
        }
    }

    /// <summary>
    /// Waits for the select and back buttons, Space, Enter and Escape to be let go, so one press
    /// isn't taken again by the next screen. Matters most for a controller, whose buttons are read
    /// as they are rather than as presses that ClearKeysDown can throw away.
    /// </summary>
    internal void WaitKeyUp()
    {
        ControlInfo ci;
        while (true)
        {
            ReadAnyControl(out ci);
            bool keyPressed =
               ci.button0 ||
               ci.button1 ||
               ci.button2 ||
               ci.button3 ||
               input.IsKeyDown(ScanCodes.Space) ||
               input.IsKeyDown(ScanCodes.Enter) ||
               input.IsKeyDown(ScanCodes.Escape);

            if (!keyPressed)
                break;

            GameEngineManager.DelayMs(5);
        }
    }
}