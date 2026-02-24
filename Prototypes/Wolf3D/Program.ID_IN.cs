using SDL2;
using System.Runtime.InteropServices;

namespace Wolf3D;

internal enum Direction
{
    dir_North, dir_NorthEast,
    dir_East, dir_SouthEast,
    dir_South, dir_SouthWest,
    dir_West, dir_NorthWest,
    dir_None
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct ControlInfo
{
    public byte button0, button1, button2, button3; // bool
    public short x, y;
    public short xaxis, yaxis;
    public byte dir;
}

internal partial class Program
{
    internal const int TEXTINPUTSIZE = SDL.SDL_TEXTINPUTEVENT_TEXT_SIZE;

    internal static void IN_ClearKey(int code)
    {
        Keyboard[code] = false;
        if (code == LastScan) LastScan = (int)ScanCodes.sc_None;
    }

    internal enum ScanCodes
    {
        sc_None = SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN,
        sc_Bad = 0x7fffffff,
        sc_Last = SDL.SDL_Scancode.SDL_NUM_SCANCODES,
        sc_Return = SDL.SDL_Scancode.SDL_SCANCODE_RETURN,

        sc_Escape = SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE,
        sc_Space = SDL.SDL_Scancode.SDL_SCANCODE_SPACE,
        sc_BackSpace = SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE,
        sc_Tab = SDL.SDL_Scancode.SDL_SCANCODE_TAB,
        sc_RAlt = SDL.SDL_Scancode.SDL_SCANCODE_RALT,
        sc_LAlt = SDL.SDL_Scancode.SDL_SCANCODE_LALT,
        sc_RControl = SDL.SDL_Scancode.SDL_SCANCODE_RCTRL,
        sc_LControl = SDL.SDL_Scancode.SDL_SCANCODE_LCTRL,
        sc_CapsLock = SDL.SDL_Scancode.SDL_SCANCODE_CAPSLOCK,
        sc_LShift = SDL.SDL_Scancode.SDL_SCANCODE_LSHIFT,
        sc_RShift = SDL.SDL_Scancode.SDL_SCANCODE_RSHIFT,
        sc_UpArrow = SDL.SDL_Scancode.SDL_SCANCODE_UP,
        sc_DownArrow = SDL.SDL_Scancode.SDL_SCANCODE_DOWN,
        sc_LeftArrow = SDL.SDL_Scancode.SDL_SCANCODE_LEFT,
        sc_RightArrow = SDL.SDL_Scancode.SDL_SCANCODE_RIGHT,
        sc_Insert = SDL.SDL_Scancode.SDL_SCANCODE_INSERT,
        sc_Delete = SDL.SDL_Scancode.SDL_SCANCODE_DELETE,
        sc_Home = SDL.SDL_Scancode.SDL_SCANCODE_HOME,
        sc_End = SDL.SDL_Scancode.SDL_SCANCODE_END,
        sc_PgUp = SDL.SDL_Scancode.SDL_SCANCODE_PAGEUP,
        sc_PgDn = SDL.SDL_Scancode.SDL_SCANCODE_PAGEDOWN,
        sc_KeyPad2 = SDL.SDL_Scancode.SDL_SCANCODE_KP_2,
        sc_KeyPad4 = SDL.SDL_Scancode.SDL_SCANCODE_KP_4,
        sc_KeyPad5 = SDL.SDL_Scancode.SDL_SCANCODE_KP_5,
        sc_KeyPad6 = SDL.SDL_Scancode.SDL_SCANCODE_KP_6,
        sc_KeyPad8 = SDL.SDL_Scancode.SDL_SCANCODE_KP_8,
        sc_KeyPadEnter = SDL.SDL_Scancode.SDL_SCANCODE_KP_ENTER,
        sc_F1 = SDL.SDL_Scancode.SDL_SCANCODE_F1,
        sc_F2 = SDL.SDL_Scancode.SDL_SCANCODE_F2,
        sc_F3 = SDL.SDL_Scancode.SDL_SCANCODE_F3,
        sc_F4 = SDL.SDL_Scancode.SDL_SCANCODE_F4,
        sc_F5 = SDL.SDL_Scancode.SDL_SCANCODE_F5,
        sc_F6 = SDL.SDL_Scancode.SDL_SCANCODE_F6,
        sc_F7 = SDL.SDL_Scancode.SDL_SCANCODE_F7,
        sc_F8 = SDL.SDL_Scancode.SDL_SCANCODE_F8,
        sc_F9 = SDL.SDL_Scancode.SDL_SCANCODE_F9,
        sc_F10 = SDL.SDL_Scancode.SDL_SCANCODE_F10,
        sc_F11 = SDL.SDL_Scancode.SDL_SCANCODE_F11,
        sc_F12 = SDL.SDL_Scancode.SDL_SCANCODE_F12,
        sc_ScrollLock = SDL.SDL_Scancode.SDL_SCANCODE_SCROLLLOCK,
        sc_PrintScreen = SDL.SDL_Scancode.SDL_SCANCODE_PAUSE,
        sc_Pause = SDL.SDL_Scancode.SDL_SCANCODE_PAUSE,
        sc_1 = SDL.SDL_Scancode.SDL_SCANCODE_1,
        sc_2 = SDL.SDL_Scancode.SDL_SCANCODE_2,
        sc_3 = SDL.SDL_Scancode.SDL_SCANCODE_3,
        sc_4 = SDL.SDL_Scancode.SDL_SCANCODE_4,
        sc_5 = SDL.SDL_Scancode.SDL_SCANCODE_5,
        sc_6 = SDL.SDL_Scancode.SDL_SCANCODE_6,
        sc_7 = SDL.SDL_Scancode.SDL_SCANCODE_7,
        sc_8 = SDL.SDL_Scancode.SDL_SCANCODE_8,
        sc_9 = SDL.SDL_Scancode.SDL_SCANCODE_9,
        sc_0 = SDL.SDL_Scancode.SDL_SCANCODE_0,
        sc_Equal = SDL.SDL_Scancode.SDL_SCANCODE_EQUALS,
        sc_Minus = SDL.SDL_Scancode.SDL_SCANCODE_MINUS,
        sc_A = SDL.SDL_Scancode.SDL_SCANCODE_A,
        sc_B = SDL.SDL_Scancode.SDL_SCANCODE_B,
        sc_C = SDL.SDL_Scancode.SDL_SCANCODE_C,
        sc_D = SDL.SDL_Scancode.SDL_SCANCODE_D,
        sc_E = SDL.SDL_Scancode.SDL_SCANCODE_E,
        sc_F = SDL.SDL_Scancode.SDL_SCANCODE_F,
        sc_G = SDL.SDL_Scancode.SDL_SCANCODE_G,
        sc_H = SDL.SDL_Scancode.SDL_SCANCODE_H,
        sc_I = SDL.SDL_Scancode.SDL_SCANCODE_I,
        sc_J = SDL.SDL_Scancode.SDL_SCANCODE_J,
        sc_K = SDL.SDL_Scancode.SDL_SCANCODE_K,
        sc_L = SDL.SDL_Scancode.SDL_SCANCODE_L,
        sc_M = SDL.SDL_Scancode.SDL_SCANCODE_M,
        sc_N = SDL.SDL_Scancode.SDL_SCANCODE_N,
        sc_O = SDL.SDL_Scancode.SDL_SCANCODE_O,
        sc_P = SDL.SDL_Scancode.SDL_SCANCODE_P,
        sc_Q = SDL.SDL_Scancode.SDL_SCANCODE_Q,
        sc_R = SDL.SDL_Scancode.SDL_SCANCODE_R,
        sc_S = SDL.SDL_Scancode.SDL_SCANCODE_S,
        sc_T = SDL.SDL_Scancode.SDL_SCANCODE_T,
        sc_U = SDL.SDL_Scancode.SDL_SCANCODE_U,
        sc_V = SDL.SDL_Scancode.SDL_SCANCODE_V,
        sc_W = SDL.SDL_Scancode.SDL_SCANCODE_W,
        sc_X = SDL.SDL_Scancode.SDL_SCANCODE_X,
        sc_Y = SDL.SDL_Scancode.SDL_SCANCODE_Y,
        sc_Z = SDL.SDL_Scancode.SDL_SCANCODE_Z,
        sc_Alt = sc_LAlt,
        sc_Control = sc_LControl,
        sc_Enter = sc_Return,
        key_None = sc_None
    }

/*
 =============================================================================

                     GLOBAL VARIABLES

 =============================================================================
 */
    //
    // configuration variables
    //
    static bool MousePresent;
    static bool forcegrabmouse;

    internal static bool[] Keyboard = new bool[(int)ScanCodes.sc_Last];
    internal static char[] textinput = new char[TEXTINPUTSIZE];
    internal static bool Paused;
    internal static int LastScan;

    static IntPtr Joystick;
    static int JoyNumButtons;
    static int JoyNumHats;

    static bool GrabInput = false;

    /*
    =============================================================================

                        LOCAL VARIABLES

    =============================================================================
    */

    static bool IN_Started;

    static byte[] DirTable =        // Quick lookup for total direction
    {
        (byte)Direction.dir_NorthWest,  (byte)Direction.dir_North,  (byte)Direction.dir_NorthEast,
        (byte)Direction.dir_West,       (byte)Direction.dir_None,   (byte)Direction.dir_East,
        (byte)Direction.dir_SouthWest,  (byte)Direction.dir_South,  (byte)Direction.dir_SouthEast
    };


    internal static void IN_Startup()
    {
        if (IN_Started)
            return;

        IN_ClearKeysDown();

        if (param_joystickindex >= 0 && param_joystickindex < SDL.SDL_NumJoysticks())
        {
            Joystick = SDL.SDL_JoystickOpen(param_joystickindex);

            if (Joystick != IntPtr.Zero)
            {
                JoyNumButtons = SDL.SDL_JoystickNumButtons(Joystick);

                if (JoyNumButtons > 32)
                    JoyNumButtons = 32; // only up to 32 buttons are supported

                JoyNumHats = SDL.SDL_JoystickNumHats(Joystick);

                if (param_joystickhat < -1 || param_joystickhat >= JoyNumHats)
                    Quit($"The joystickhit param must be between 0 and {JoyNumHats - 1}!");
            }
        }

        SDL.SDL_EventState(SDL.SDL_EventType.SDL_MOUSEMOTION, SDL.SDL_IGNORE);

        if (fullscreen || forcegrabmouse)
        {
            GrabInput = true;

            IN_SetWindowGrab(window);
        }

        // I didn't find a wayto ask libSDL whether a mouse is present, yet...
        MousePresent = true;

        IN_Started = true;
    }

    internal static void IN_Shutdown()
    {
        if (!IN_Started)
            return;

        if (Joystick != IntPtr.Zero)
            SDL.SDL_JoystickClose(Joystick);

        IN_Started = false;
    }

    internal static void IN_ClearKeysDown()
    {
        LastScan = (int)ScanCodes.sc_None;

        Array.Fill(Keyboard, false);
    }

    internal static void IN_ClearTextInput()
    {
        Array.Fill(textinput, (char)0);
    }

    internal static void IN_ReadControl(out ControlInfo info)
    {
        ushort buttons;
        int dx, dy;
        int mx, my;

        dx = dy = 0;
        mx = my = 0;
        buttons = 0;

        IN_ProcessEvents();

        if (Keyboard[(int)ScanCodes.sc_Home])
        {
            mx = -1;
            my = -1;
        }
        else if (Keyboard[(int)ScanCodes.sc_PgUp])
        {
            mx = 1;
            my = -1;
        }
        else if (Keyboard[(int)ScanCodes.sc_End])
        {
            mx = -1;
            my = 1;
        }
        else if (Keyboard[(int)ScanCodes.sc_PgDn])
        {
            mx = 1;
            my = 1;
        }

        if (Keyboard[(int)ScanCodes.sc_UpArrow])
            my = -1;
        else if (Keyboard[(int)ScanCodes.sc_DownArrow])
            my = 1;

        if (Keyboard[(int)ScanCodes.sc_LeftArrow])
            mx = -1;
        else if (Keyboard[(int)ScanCodes.sc_RightArrow])
            mx = 1;

        dx = mx * 127;
        dy = my * 127;

        info.x = (short)dx;
        info.xaxis = (short)mx;
        info.y = (short)dy;
        info.yaxis = (short)my;
        info.button0 = (byte)((buttons & 1) != 0 ? 1 : 0);
        info.button1 = (byte)((buttons & (1 << 1)) != 0 ? 1 : 0);
        info.button2 = (byte)((buttons & (1 << 2)) != 0 ? 1 : 0);
        info.button3 = (byte)((buttons & (1 << 3)) != 0 ? 1 : 0);
        info.dir = DirTable[((my + 1) * 3) + (mx + 1)];
    }

    internal static bool IN_JoyPresent()
    {
        return Joystick != IntPtr.Zero;
    }

    internal static void IN_GetJoyDelta(out int dx, out int dy)
    {
        if (Joystick == IntPtr.Zero)
        {
            dx = dy = 0;
            return;
        }

        SDL.SDL_JoystickUpdate();
        int x = SDL.SDL_JoystickGetAxis(Joystick, 0) >> 8;
        int y = SDL.SDL_JoystickGetAxis(Joystick, 1) >> 8;

        if (param_joystickhat != -1)
        {
            byte hatState = SDL.SDL_JoystickGetHat(Joystick, param_joystickhat);

            if ((hatState & SDL.SDL_HAT_RIGHT) != 0)
                x += 127;
            else if ((hatState & SDL.SDL_HAT_LEFT) != 0)
                x -= 127;

            if ((hatState & SDL.SDL_HAT_DOWN) != 0)
                y += 127;
            else if ((hatState & SDL.SDL_HAT_UP) != 0)
                y -= 127;

            x = int.Max(-128, int.Min(x, 127));
            y = int.Max(-128, int.Min(y, 127));
        }

        dx = x;
        dy = y;
    }

    internal static int IN_JoyButtons()
    {
        int i;

        if (Joystick == IntPtr.Zero)
            return 0;

        SDL.SDL_JoystickUpdate();

        int res = 0;

        for (i = 0; i < JoyNumButtons && i < 32; i++)
            res |= SDL.SDL_JoystickGetButton(Joystick, i) << i;

        return res;
    }

    internal static void IN_SetWindowGrab(IntPtr window)
    {

        if (SDL.SDL_ShowCursor((!GrabInput ? 1 : 0)) < 0)
            Quit($"Unable to {(GrabInput ? "show" : "hide")} cursor: {SDL.SDL_GetError()}");

        SDL.SDL_SetWindowGrab(window, GrabInput ? SDL.SDL_bool.SDL_TRUE : SDL.SDL_bool.SDL_FALSE);

        if (SDL.SDL_SetRelativeMouseMode(GrabInput ? SDL.SDL_bool.SDL_TRUE : SDL.SDL_bool.SDL_FALSE) > 0)
            Quit($"Unable to set relative mode for mouse: {SDL.SDL_GetError()}");
    }

    internal static void IN_ProcessEvents()
    {
        while (SDL.SDL_PollEvent(out SDL.SDL_Event e) == 1)
        {
            IN_HandleEvent(e);
        }
    }

    internal static void IN_HandleEvent(SDL.SDL_Event e)
    {
        int key;

        key = (int)e.key.keysym.scancode;

        switch (e.type)
        {
            case SDL.SDL_EventType.SDL_QUIT:
                Quit(string.Empty);
                break;

            case SDL.SDL_EventType.SDL_KEYDOWN:
                if (key == (int)ScanCodes.sc_ScrollLock || key == (int)ScanCodes.sc_F12)
                {
                    GrabInput = !GrabInput;

                    IN_SetWindowGrab(window);

                    return;
                }

                LastScan = IN_MapKey(key);

                if (Keyboard[(int)ScanCodes.sc_Alt])
                {
                    if (LastScan == (int)ScanCodes.sc_F4)
                        Quit(string.Empty);
                }

                if (LastScan < (int)ScanCodes.sc_Last)
                    Keyboard[(int)LastScan] = true;

                if (LastScan == (int)ScanCodes.sc_Pause)
                    Paused = true;
                break;

            case SDL.SDL_EventType.SDL_KEYUP:
                key = (int)IN_MapKey(key);

                if (key < (int)ScanCodes.sc_Last)
                    Keyboard[key] = false;
                break;

            case SDL.SDL_EventType.SDL_TEXTINPUT:
                // Clear managed text buffer
                Array.Fill(textinput, (char)0);

                // e.text.text is a fixed-size buffer (byte* / fixed byte[]). Access it in unsafe context and copy bytes.
                for (int i = 0; i < TEXTINPUTSIZE; i++)
                {
                    unsafe
                    {
                        byte b = e.text.text[i]; // allowed inside unsafe
                        if (b == 0)
                            break;
                        textinput[i] = (char)b;
                    }
                }
                break;
        }
    }

    internal static int IN_MapKey(int key)
    {
        int scan = key;

        switch ((ScanCodes)key)
        {
            case ScanCodes.sc_KeyPadEnter: scan = (int)ScanCodes.sc_Enter; break;
            case ScanCodes.sc_RShift: scan = (int)ScanCodes.sc_LShift; break;
            case ScanCodes.sc_RAlt: scan = (int)ScanCodes.sc_LAlt; break;
            case ScanCodes.sc_RControl: scan = (int)ScanCodes.sc_LControl; break;

            case ScanCodes.sc_KeyPad2:
            case ScanCodes.sc_KeyPad4:
            case ScanCodes.sc_KeyPad6:
            case ScanCodes.sc_KeyPad8:
                if (((int)SDL.SDL_GetModState() & (int)SDL.SDL_Keymod.KMOD_NUM) == 0)
                {
                    switch ((ScanCodes)key)
                    {
                        case ScanCodes.sc_KeyPad2: scan = (int)ScanCodes.sc_DownArrow; break;
                        case ScanCodes.sc_KeyPad4: scan = (int)ScanCodes.sc_LeftArrow; break;
                        case ScanCodes.sc_KeyPad6: scan = (int)ScanCodes.sc_RightArrow; break;
                        case ScanCodes.sc_KeyPad8: scan = (int)ScanCodes.sc_UpArrow; break;
                    }
                }
                break;
        }

        return scan;
    }

    internal static void IN_Ack()
    {
        IN_StartAck();

        do
        {
            IN_WaitAndProcessEvents();
        } while (!IN_CheckAck());
    }

    internal static bool IN_UserInput(uint delay)
    {
        uint lasttime = GetTimeCount();
        IN_StartAck();

        do
        {
            IN_ProcessEvents();

            if (IN_CheckAck())
                return true;

            SDL.SDL_Delay(5);
        } while (GetTimeCount() - lasttime < delay);

        return false;
    }

    static bool[] btnstate = new bool[(int)buttontypes.NUMBUTTONS];
    internal static void IN_StartAck()
    {
        int i;

        IN_ProcessEvents();

        //
        // get initial state of everything
        //
        IN_ClearKeysDown();
        Array.Fill(btnstate, false);

        int buttons = IN_JoyButtons() << 4;

        if (MousePresent)
            buttons |= IN_MouseButtons();

        for (i = 0; i < (int)buttontypes.NUMBUTTONS; i++, buttons >>= 1)
        {
            if ((buttons & 1) != 0)
                btnstate[i] = true;
        }
    }

    static int IN_MouseButtons()
    {
        if (MousePresent)
            return INL_GetMouseButtons();
        else
            return 0;
    }

    static int INL_GetMouseButtons()
    {
        int buttons = (int)SDL.SDL_GetMouseState(out _, out _);
        int middlePressed = (int)(buttons & SDL.SDL_BUTTON(SDL.SDL_BUTTON_MIDDLE));
        int rightPressed = (int)(buttons & SDL.SDL_BUTTON(SDL.SDL_BUTTON_RIGHT));

        buttons &= (int)~(SDL.SDL_BUTTON(SDL.SDL_BUTTON_MIDDLE) | SDL.SDL_BUTTON(SDL.SDL_BUTTON_RIGHT));

        if (middlePressed != 0)
            buttons |= 1 << 2;
        if (rightPressed != 0)
            buttons |= 1 << 1;

        return buttons;
    }

    static void IN_WaitAndProcessEvents()
    {
        IN_WaitEvent();
        IN_ProcessEvents();
    }

    static void IN_WaitEvent()
    {
        // BUG: This eats any KEYDOWN/UP events

        //if (SDL.SDL_WaitEvent(out var e) == 0)
        //    Quit($"Error waiting for event: {SDL.SDL_GetError()}\n");
    }

    static bool IN_CheckAck()
    {
        int i;

        IN_ProcessEvents();

        //
        // see if something has been pressed
        //
        if (LastScan != 0)
            return true;

        int buttons = IN_JoyButtons() << 4;

        if (MousePresent)
            buttons |= IN_MouseButtons();

        for (i = 0; i < (int)buttontypes.NUMBUTTONS; i++, buttons >>= 1)
        {
            if ((buttons & 1) != 0)
            {
                if (!btnstate[i])
                {
                    // Wait until button has been released
                    do
                    {
                        IN_WaitAndProcessEvents();

                        buttons = IN_JoyButtons() << 4;

                        if (MousePresent)
                            buttons |= IN_MouseButtons();

                    } while ((buttons & (1 << i)) != 0);

                    return true;
                }
            }
            else
                btnstate[i] = false;
        }

        return false;
    }

    internal static void IN_CenterMouse()
    {
        if (MousePresent && GrabInput)
            SDL.SDL_WarpMouseInWindow(window, screenWidth / 2, screenHeight / 2);
    }
}