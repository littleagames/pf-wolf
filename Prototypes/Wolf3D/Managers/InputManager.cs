using SDL2;

namespace Wolf3D.Managers;

public enum ScanCodes
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

internal class InputManager
{
    private string[] ScanNames =
    {
        "?","?","?","?","A","B","C","D",
        "E","F","G","H","I","J","K","L",
        "M","N","O","P","Q","R","S","T",
        "U","V","W","X","Y","Z","1","2",
        "3","4","5","6","7","8","9","0",
        "Return","Esc","BkSp","Tab","Space","-","=","[",
        "]","#","?",";","'","`",",",".",
        "/","CapsLk","F1","F2","F3","F4","F5","F6",
        "F7","F8","F9","F10","F11","F12","PrtSc",
        "ScrlLk","Pause","Ins","Home","PgUp","Delete","End","PgDn",
        "Right","Left","Down","Up","NumLk","KP /","KP *","KP -",
        "KP +","Enter","KP 1","KP 2","KP 3","KP 4","KP 5","KP 6",
        "KP 7","KP 8","KP 9","KP 0","KP .","\\","?","?",
        "?","F13","F14","F15","F16","F17","F18","F19",
        "F20","F21","F22","F23","F24","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","Return",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","Ctrl","Shift","Alt","?","RCtrl","RShft","RAlt",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?","?","?","?","?","?","?","?",
        "?"
    };

    public string GetScanName(ScanCodes scan)
    {
        return ScanNames[(int)scan];
    }

    public InputManager(
        GameEngineManager gameEngineManager,
        VideoManager videoManager)
    {
        this.gameEngineManager = gameEngineManager;
        this.videoManager = videoManager;
    }

    private int param_joystickindex = 0;
    private int param_joystickhat = -1;

    private const int TEXTINPUTSIZE = SDL.SDL_TEXTINPUTEVENT_TEXT_SIZE;
    private readonly GameEngineManager gameEngineManager;
    private readonly VideoManager videoManager;

    internal void ClearKey(ScanCodes code)
    {
        Keyboard[(int)code] = false;
        if (code == LastScan) 
            ClearLastKey();
    }

    internal void ClearLastKey()
    {
        LastScan = ScanCodes.sc_None;
    }

    /*
     =============================================================================

                         GLOBAL VARIABLES

     =============================================================================
     */
    //
    // configuration variables
    //
    private bool MousePresent;
    private bool forcegrabmouse;

    private bool[] buttonstate = new bool[(int)buttontypes.NUMBUTTONS];
    private bool[] buttonheld = new bool[(int)buttontypes.NUMBUTTONS];

    private bool[] Keyboard = new bool[(int)ScanCodes.sc_Last];
    internal char[] textinput = new char[TEXTINPUTSIZE];
    internal ScanCodes LastScan;

    private IntPtr Joystick;
    public int JoyNumButtons { get; private set; }
    private int JoyNumHats;

    private bool GrabInput = false;

    /*
    =============================================================================

                        LOCAL VARIABLES

    =============================================================================
    */

    private bool Started;

    private Direction[] DirTable =        // Quick lookup for total direction
    {
        Direction.dir_NorthWest,  Direction.dir_North,  Direction.dir_NorthEast,
        Direction.dir_West,       Direction.dir_None,   Direction.dir_East,
        Direction.dir_SouthWest,  Direction.dir_South,  Direction.dir_SouthEast
    };


    internal void Init()
    {
        if (Started)
            return;

        if (SDL.SDL_InitSubSystem(SDL.SDL_INIT_JOYSTICK) < 0)
        {
            throw new PfWolfInputException("Could not initialize SDL: {0}", SDL.SDL_GetError());
        }

        ClearKeysDown();

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
                    throw new PfWolfInputException($"The joystickhit param must be between 0 and {JoyNumHats - 1}!");
            }
        }

        SDL.SDL_EventState(SDL.SDL_EventType.SDL_MOUSEMOTION, SDL.SDL_IGNORE);

        if (videoManager.fullscreen || forcegrabmouse)
        {
            GrabInput = true;

            videoManager.SetWindowGrab(GrabInput);
        }

        int numJoysticks = SDL.SDL_NumJoysticks();
        if (param_joystickindex > 0 && (param_joystickindex < -1 || param_joystickindex > numJoysticks))
        {
            if (numJoysticks == 0)
                Console.WriteLine("No joysticks are available to SDL!");
            else
                Console.WriteLine($"The joystick index must be between -1 and {numJoysticks - 1}!");
            Environment.Exit(1);
        }

        // I didn't find a wayto ask libSDL whether a mouse is present, yet...
        MousePresent = true;

        Started = true;
    }

    public void InitButtonState()
    {
        buttonstate = new bool[(int)buttontypes.NUMBUTTONS];
    }

    internal void Shutdown()
    {
        if (!Started)
            return;

        if (Joystick != IntPtr.Zero)
            SDL.SDL_JoystickClose(Joystick);

        Started = false;
    }

    public bool IsMouseInputGrabbed()
    {
        return GrabInput;
    }

    public bool IsKeyDown(ScanCodes code)
    {
        return Keyboard[(int)code];
    }

    public bool IsButtonPressed(buttontypes code)
    {
        return buttonstate[(int)code];
    }

    public bool IsButtonHeld(buttontypes code)
    {
        return buttonheld[(int)code];
    }

    public void SetButtonPressed(buttontypes button, bool isPressed)
    {
        buttonstate[(int)button] = isPressed;
    }

    public void SetButtonHeld(buttontypes button, bool isHeld)
    {
        buttonheld[(int)button] = isHeld;
    }

    /// <summary>
    /// Sets the "held" state from the "pressed" state and clears the "pressed" state. This should be called once per frame after processing input.
    /// </summary>
    public void ProcessButtons()
    {
        Array.Copy(buttonstate, buttonheld, buttonstate.Length);
        Array.Fill(buttonstate, false);
    }

    public ScanCodes GetLastKeyPressed()
    {
        return LastScan;
    }

    internal void ClearKeysDown()
    {
        LastScan = ScanCodes.sc_None;

        Array.Fill(Keyboard, false);
    }

    internal char[] GetTextInput()
    {
        return textinput;
    }

    internal void ClearTextInput()
    {
        Array.Fill(textinput, (char)0);
    }

    internal void ReadControl(out ControlInfo info)
    {
        ushort buttons;
        int dx, dy;
        int mx, my;

        dx = dy = 0;
        mx = my = 0;
        buttons = 0;

        ProcessEvents();

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
        info.button0 = ((buttons & 1) != 0 );
        info.button1 = ((buttons & (1 << 1)) != 0);
        info.button2 = ((buttons & (1 << 2)) != 0);
        info.button3 = ((buttons & (1 << 3)) != 0);
        info.dir = DirTable[((my + 1) * 3) + (mx + 1)];
    }

    internal bool IsMousePresent() => MousePresent;

    internal bool JoyPresent()
    {
        return Joystick != IntPtr.Zero;
    }

    internal void GetJoyDelta(out int dx, out int dy)
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

    internal int JoyButtons()
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

    internal void ProcessEvents()
    {
        while (SDL.SDL_PollEvent(out SDL.SDL_Event e) == 1)
        {
            HandleEvent(e);
        }
    }

    internal void HandleEvent(SDL.SDL_Event e)
    {
        ScanCodes key;

        key = (ScanCodes)e.key.keysym.scancode;

        switch (e.type)
        {
            case SDL.SDL_EventType.SDL_QUIT:
                gameEngineManager.Quit("");
                break;

            case SDL.SDL_EventType.SDL_KEYDOWN:
                if (key == ScanCodes.sc_ScrollLock || key == ScanCodes.sc_F12)
                {
                    GrabInput = !GrabInput;

                    videoManager.SetWindowGrab(GrabInput);

                    return;
                }

                LastScan = MapKey(key);

                if (Keyboard[(int)ScanCodes.sc_Alt])
                {
                    if (LastScan == ScanCodes.sc_F4)
                        gameEngineManager.Quit(string.Empty);
                }

                if (LastScan < ScanCodes.sc_Last)
                    Keyboard[(int)LastScan] = true;

                if (LastScan == ScanCodes.sc_Pause)
                    gameEngineManager.SetPaused(true);
                break;

            case SDL.SDL_EventType.SDL_KEYUP:
                key = MapKey(key);

                if (key < ScanCodes.sc_Last)
                    Keyboard[(int)key] = false;
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

    internal ScanCodes MapKey(ScanCodes key)
    {
        ScanCodes scan = key;

        switch (key)
        {
            case ScanCodes.sc_KeyPadEnter: scan = ScanCodes.sc_Enter; break;
            case ScanCodes.sc_RShift: scan = ScanCodes.sc_LShift; break;
            case ScanCodes.sc_RAlt: scan = ScanCodes.sc_LAlt; break;
            case ScanCodes.sc_RControl: scan = ScanCodes.sc_LControl; break;

            case ScanCodes.sc_KeyPad2:
            case ScanCodes.sc_KeyPad4:
            case ScanCodes.sc_KeyPad6:
            case ScanCodes.sc_KeyPad8:
                if (((int)SDL.SDL_GetModState() & (int)SDL.SDL_Keymod.KMOD_NUM) == 0)
                {
                    switch ((ScanCodes)key)
                    {
                        case ScanCodes.sc_KeyPad2: scan = ScanCodes.sc_DownArrow; break;
                        case ScanCodes.sc_KeyPad4: scan = ScanCodes.sc_LeftArrow; break;
                        case ScanCodes.sc_KeyPad6: scan = ScanCodes.sc_RightArrow; break;
                        case ScanCodes.sc_KeyPad8: scan = ScanCodes.sc_UpArrow; break;
                    }
                }
                break;
        }

        return scan;
    }

    internal void Ack()
    {
        StartAck();

        do
        {
            WaitAndProcessEvents();
        } while (!CheckAck());
    }

    internal bool UserInput(uint delay)
    {
        uint lasttime = GameEngineManager.GetTimeCount();
        StartAck();

        do
        {
            ProcessEvents();

            if (CheckAck())
                return true;

            GameEngineManager.DelayMs(5);
        } while (GameEngineManager.GetTimeCount() - lasttime < delay);

        return false;
    }

    private bool[] btnstate = new bool[(int)buttontypes.NUMBUTTONS];
    public void StartAck()
    {
        int i;

        ProcessEvents();

        //
        // get initial state of everything
        //
        ClearKeysDown();
        Array.Fill(btnstate, false);

        int buttons = JoyButtons() << 4;

        if (MousePresent)
            buttons |= MouseButtons();

        for (i = 0; i < (int)buttontypes.NUMBUTTONS; i++, buttons >>= 1)
        {
            if ((buttons & 1) != 0)
                btnstate[i] = true;
        }
    }

    public int MouseButtons()
    {
        if (MousePresent)
            return INL_GetMouseButtons();
        else
            return 0;
    }

    public int INL_GetMouseButtons()
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

    public void WaitAndProcessEvents()
    {
        WaitEvent();
        ProcessEvents();
    }

    public void WaitEvent()
    {
        // BUG: This eats any KEYDOWN/UP events

        //if (SDL.SDL_WaitEvent(out var e) == 0)
        //    Quit($"Error waiting for event: {SDL.SDL_GetError()}\n");
    }

    public bool CheckAck()
    {
        int i;

        ProcessEvents();

        //
        // see if something has been pressed
        //
        if (LastScan != 0)
            return true;

        int buttons = JoyButtons() << 4;

        if (MousePresent)
            buttons |= MouseButtons();

        for (i = 0; i < (int)buttontypes.NUMBUTTONS; i++, buttons >>= 1)
        {
            if ((buttons & 1) != 0)
            {
                if (!btnstate[i])
                {
                    // Wait until button has been released
                    do
                    {
                        WaitAndProcessEvents();

                        buttons = JoyButtons() << 4;

                        if (MousePresent)
                            buttons |= MouseButtons();

                    } while ((buttons & (1 << i)) != 0);

                    return true;
                }
            }
            else
                btnstate[i] = false;
        }

        return false;
    }

    internal void CenterMouse()
    {
        if (MousePresent && GrabInput)
            videoManager.CenterMouse();
    }
}
