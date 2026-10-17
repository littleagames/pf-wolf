using SDL2;
using PFWolf.Configuration;

namespace PFWolf.Managers;

internal struct ControlInfo
{
    public bool button0, button1, button2, button3;
    public short x, y;
    public short xaxis, yaxis;
    public Direction dir;
}
public enum ScanCodes
{
    None = SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN,
    Bad = 0x7fffffff,
    Last = SDL.SDL_Scancode.SDL_NUM_SCANCODES,
    Return = SDL.SDL_Scancode.SDL_SCANCODE_RETURN,

    Escape = SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE,
    Space = SDL.SDL_Scancode.SDL_SCANCODE_SPACE,
    BackSpace = SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE,
    Tab = SDL.SDL_Scancode.SDL_SCANCODE_TAB,
    RAlt = SDL.SDL_Scancode.SDL_SCANCODE_RALT,
    LAlt = SDL.SDL_Scancode.SDL_SCANCODE_LALT,
    RControl = SDL.SDL_Scancode.SDL_SCANCODE_RCTRL,
    LControl = SDL.SDL_Scancode.SDL_SCANCODE_LCTRL,
    CapsLock = SDL.SDL_Scancode.SDL_SCANCODE_CAPSLOCK,
    LShift = SDL.SDL_Scancode.SDL_SCANCODE_LSHIFT,
    RShift = SDL.SDL_Scancode.SDL_SCANCODE_RSHIFT,
    UpArrow = SDL.SDL_Scancode.SDL_SCANCODE_UP,
    DownArrow = SDL.SDL_Scancode.SDL_SCANCODE_DOWN,
    LeftArrow = SDL.SDL_Scancode.SDL_SCANCODE_LEFT,
    RightArrow = SDL.SDL_Scancode.SDL_SCANCODE_RIGHT,
    Insert = SDL.SDL_Scancode.SDL_SCANCODE_INSERT,
    Delete = SDL.SDL_Scancode.SDL_SCANCODE_DELETE,
    Home = SDL.SDL_Scancode.SDL_SCANCODE_HOME,
    End = SDL.SDL_Scancode.SDL_SCANCODE_END,
    PgUp = SDL.SDL_Scancode.SDL_SCANCODE_PAGEUP,
    PgDn = SDL.SDL_Scancode.SDL_SCANCODE_PAGEDOWN,
    KeyPad2 = SDL.SDL_Scancode.SDL_SCANCODE_KP_2,
    KeyPad4 = SDL.SDL_Scancode.SDL_SCANCODE_KP_4,
    KeyPad5 = SDL.SDL_Scancode.SDL_SCANCODE_KP_5,
    KeyPad6 = SDL.SDL_Scancode.SDL_SCANCODE_KP_6,
    KeyPad8 = SDL.SDL_Scancode.SDL_SCANCODE_KP_8,
    KeyPadEnter = SDL.SDL_Scancode.SDL_SCANCODE_KP_ENTER,
    KeyPadPlus = SDL.SDL_Scancode.SDL_SCANCODE_KP_PLUS,
    KeyPadMinus = SDL.SDL_Scancode.SDL_SCANCODE_KP_MINUS,
    F1 = SDL.SDL_Scancode.SDL_SCANCODE_F1,
    F2 = SDL.SDL_Scancode.SDL_SCANCODE_F2,
    F3 = SDL.SDL_Scancode.SDL_SCANCODE_F3,
    F4 = SDL.SDL_Scancode.SDL_SCANCODE_F4,
    F5 = SDL.SDL_Scancode.SDL_SCANCODE_F5,
    F6 = SDL.SDL_Scancode.SDL_SCANCODE_F6,
    F7 = SDL.SDL_Scancode.SDL_SCANCODE_F7,
    F8 = SDL.SDL_Scancode.SDL_SCANCODE_F8,
    F9 = SDL.SDL_Scancode.SDL_SCANCODE_F9,
    F10 = SDL.SDL_Scancode.SDL_SCANCODE_F10,
    F11 = SDL.SDL_Scancode.SDL_SCANCODE_F11,
    F12 = SDL.SDL_Scancode.SDL_SCANCODE_F12,
    ScrollLock = SDL.SDL_Scancode.SDL_SCANCODE_SCROLLLOCK,
    PrintScreen = SDL.SDL_Scancode.SDL_SCANCODE_PAUSE,
    Pause = SDL.SDL_Scancode.SDL_SCANCODE_PAUSE,
    D1 = SDL.SDL_Scancode.SDL_SCANCODE_1,
    D2 = SDL.SDL_Scancode.SDL_SCANCODE_2,
    D3 = SDL.SDL_Scancode.SDL_SCANCODE_3,
    D4 = SDL.SDL_Scancode.SDL_SCANCODE_4,
    D5 = SDL.SDL_Scancode.SDL_SCANCODE_5,
    D6 = SDL.SDL_Scancode.SDL_SCANCODE_6,
    D7 = SDL.SDL_Scancode.SDL_SCANCODE_7,
    D8 = SDL.SDL_Scancode.SDL_SCANCODE_8,
    D9 = SDL.SDL_Scancode.SDL_SCANCODE_9,
    D0 = SDL.SDL_Scancode.SDL_SCANCODE_0,
    Equal = SDL.SDL_Scancode.SDL_SCANCODE_EQUALS,
    Minus = SDL.SDL_Scancode.SDL_SCANCODE_MINUS,
    Grave = SDL.SDL_Scancode.SDL_SCANCODE_GRAVE,
    A = SDL.SDL_Scancode.SDL_SCANCODE_A,
    B = SDL.SDL_Scancode.SDL_SCANCODE_B,
    C = SDL.SDL_Scancode.SDL_SCANCODE_C,
    D = SDL.SDL_Scancode.SDL_SCANCODE_D,
    E = SDL.SDL_Scancode.SDL_SCANCODE_E,
    F = SDL.SDL_Scancode.SDL_SCANCODE_F,
    G = SDL.SDL_Scancode.SDL_SCANCODE_G,
    H = SDL.SDL_Scancode.SDL_SCANCODE_H,
    I = SDL.SDL_Scancode.SDL_SCANCODE_I,
    J = SDL.SDL_Scancode.SDL_SCANCODE_J,
    K = SDL.SDL_Scancode.SDL_SCANCODE_K,
    L = SDL.SDL_Scancode.SDL_SCANCODE_L,
    M = SDL.SDL_Scancode.SDL_SCANCODE_M,
    N = SDL.SDL_Scancode.SDL_SCANCODE_N,
    O = SDL.SDL_Scancode.SDL_SCANCODE_O,
    P = SDL.SDL_Scancode.SDL_SCANCODE_P,
    Q = SDL.SDL_Scancode.SDL_SCANCODE_Q,
    R = SDL.SDL_Scancode.SDL_SCANCODE_R,
    S = SDL.SDL_Scancode.SDL_SCANCODE_S,
    T = SDL.SDL_Scancode.SDL_SCANCODE_T,
    U = SDL.SDL_Scancode.SDL_SCANCODE_U,
    V = SDL.SDL_Scancode.SDL_SCANCODE_V,
    W = SDL.SDL_Scancode.SDL_SCANCODE_W,
    X = SDL.SDL_Scancode.SDL_SCANCODE_X,
    Y = SDL.SDL_Scancode.SDL_SCANCODE_Y,
    Z = SDL.SDL_Scancode.SDL_SCANCODE_Z,
    Alt = LAlt,
    Control = LControl,
    Enter = Return,
    KeyNone = None
}

internal class InputManager
{
    internal static event EventHandler<bool>? MouseGrabbed;
    internal static event EventHandler? Quit;
    internal static event EventHandler<bool>? Pause;

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

    private readonly Lazy<ConsoleManager> _consoleManager;

    public InputManager(Lazy<ConsoleManager> consoleManager)
    {
        _consoleManager = consoleManager;
    }

    private int joystickIndex = 0;
    private int joystickHat = -1;

    private const int TextInputSize = SDL.SDL_TEXTINPUTEVENT_TEXT_SIZE;

    internal void ClearKey(ScanCodes code)
    {
        Keyboard[(int)code] = false;
        if (code == LastScan) 
            ClearLastKey();
    }

    internal void ClearLastKey()
    {
        LastScan = ScanCodes.None;
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

    private bool[] buttonstate = new bool[(int)ButtonType.Count];
    private bool[] buttonheld = new bool[(int)ButtonType.Count];

    private bool[] Keyboard = new bool[(int)ScanCodes.Last];

    // Fresh (non-repeat) key presses in order, for console binds: Keyboard[] only says what's
    // held and LastScan only keeps the latest. Drained by the play loop; cleared with the keys.
    private const int MaxPressedKeys = 32;
    private readonly Queue<ScanCodes> pressedKeys = new();
    internal char[] textinput = new char[TextInputSize];
    internal ScanCodes LastScan;

    // Mouse wheel notches since the last TakeWheelDelta (positive = away from the user).
    private int wheelDelta;

    /// <summary>The mouse wheel notches turned since the last call (positive = away from the user).</summary>
    internal int TakeWheelDelta()
    {
        int delta = wheelDelta;
        wheelDelta = 0;
        return delta;
    }

    // The joystick in use, and when SDL knows its layout, the controller it's opened as (the
    // joystick is then the controller's own). Swapped for another when it's unplugged.
    private IntPtr Joystick;
    private IntPtr GameController;
    private int JoystickInstance = -1;
    public int JoyNumButtons { get; private set; }
    private int JoyNumHats;

    /// <summary>The name of the controller or joystick in use, or null with none plugged in.</summary>
    internal string? ControllerName { get; private set; }

    /// <summary>Whether the joystick in use is a controller SDL knows the layout of.</summary>
    internal bool HasGameController => GameController != IntPtr.Zero;

    // How far a stick has to be pushed, or a trigger pulled, to count as pressed when bound as a button
    private const short AxisPressThreshold = 16384;

    private bool GrabInput = false;

    /*
    =============================================================================

                        LOCAL VARIABLES

    =============================================================================
    */

    private bool Started;

    private Direction[] DirTable =        // Quick lookup for total direction
    {
        Direction.NorthWest,  Direction.North,  Direction.NorthEast,
        Direction.West,       Direction.None,   Direction.East,
        Direction.SouthWest,  Direction.South,  Direction.SouthEast
    };


    /// <summary>Grabs or frees the mouse, as going in or out of fullscreen does (F12 toggles it by hand).</summary>
    internal void SetMouseGrab(bool grab)
    {
        if (GrabInput == grab)
            return;

        GrabInput = grab;
        MouseGrabbed?.Invoke(this, GrabInput);
    }

    internal void Init(bool fullscreen)
    {
        if (Started)
            return;

        if (SDL.SDL_InitSubSystem(SDL.SDL_INIT_JOYSTICK | SDL.SDL_INIT_GAMECONTROLLER) < 0)
        {
            throw new PfWolfInputException("Could not initialize SDL: {0}", SDL.SDL_GetError());
        }

        ClearKeysDown();

        // A controller plugged in later is opened by HandleEvent
        if (joystickIndex >= 0 && joystickIndex < SDL.SDL_NumJoysticks())
            OpenJoystick(joystickIndex);

        SDL.SDL_EventState(SDL.SDL_EventType.SDL_MOUSEMOTION, SDL.SDL_IGNORE);

        if (fullscreen || forcegrabmouse)
        {
            GrabInput = true;

            MouseGrabbed?.Invoke(this, GrabInput);
            //SetWindowGrab(GrabInput);
        }

        int numJoysticks = SDL.SDL_NumJoysticks();
        if (joystickIndex > 0 && (joystickIndex < -1 || joystickIndex > numJoysticks))
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
        buttonstate = new bool[(int)ButtonType.Count];
    }

    internal void Shutdown()
    {
        if (!Started)
            return;

        CloseJoystick();

        Started = false;
    }

    /// <summary>
    /// Opens a joystick: as a controller if SDL knows its layout (so its buttons have names and
    /// its sticks and triggers are told apart), otherwise as a plain joystick. Only one is used
    /// at a time; does nothing if one is already open.
    /// </summary>
    private void OpenJoystick(int deviceIndex)
    {
        if (Joystick != IntPtr.Zero)
            return;

        if (SDL.SDL_IsGameController(deviceIndex) == SDL.SDL_bool.SDL_TRUE)
        {
            GameController = SDL.SDL_GameControllerOpen(deviceIndex);
            if (GameController != IntPtr.Zero)
                Joystick = SDL.SDL_GameControllerGetJoystick(GameController);
        }

        if (Joystick == IntPtr.Zero)
            Joystick = SDL.SDL_JoystickOpen(deviceIndex);

        if (Joystick == IntPtr.Zero)
        {
            Console.WriteLine($"Couldn't open joystick {deviceIndex}: {SDL.SDL_GetError()}");
            return;
        }

        JoystickInstance = SDL.SDL_JoystickInstanceID(Joystick);
        JoyNumButtons = Math.Min(SDL.SDL_JoystickNumButtons(Joystick), 32);    // only up to 32 buttons are supported
        JoyNumHats = SDL.SDL_JoystickNumHats(Joystick);
        ControllerName = GameController != IntPtr.Zero
            ? SDL.SDL_GameControllerName(GameController)
            : SDL.SDL_JoystickName(Joystick);

        Console.WriteLine(GameController != IntPtr.Zero
            ? $"Controller connected: {ControllerName}"
            : $"Joystick connected: {ControllerName} (no controller layout; bind its buttons as Joy 1 and up)");
    }

    private void CloseJoystick()
    {
        // A controller's joystick belongs to it, so it's closed with it
        if (GameController != IntPtr.Zero)
            SDL.SDL_GameControllerClose(GameController);
        else if (Joystick != IntPtr.Zero)
            SDL.SDL_JoystickClose(Joystick);

        GameController = Joystick = IntPtr.Zero;
        JoystickInstance = -1;
        JoyNumButtons = JoyNumHats = 0;
        ControllerName = null;
    }

    public bool IsMouseInputGrabbed()
    {
        return GrabInput;
    }

    public bool IsKeyDown(ScanCodes code)
    {
        return Keyboard[(int)code];
    }

    /// <summary>Whether a key, mouse button or joystick button is held down right now.</summary>
    internal bool IsInputDown(InputCode code) => code.Device switch
    {
        InputDevice.Key => code.Code < (int)ScanCodes.Last && Keyboard[code.Code],
        InputDevice.MouseButton => MousePresent
            && (SDL.SDL_GetMouseState(out _, out _) & SDL.SDL_BUTTON((uint)code.Code)) != 0,
        InputDevice.JoyButton => Joystick != IntPtr.Zero && code.Code < JoyNumButtons
            && SDL.SDL_JoystickGetButton(Joystick, code.Code) != 0,
        InputDevice.PadButton => GameController != IntPtr.Zero
            && SDL.SDL_GameControllerGetButton(GameController, (SDL.SDL_GameControllerButton)code.Code) != 0,
        InputDevice.PadAxis => GameController != IntPtr.Zero
            && (code.IsPositive ? RawPadAxis(code.Axis) >= AxisPressThreshold : RawPadAxis(code.Axis) <= -AxisPressThreshold),
        _ => false,
    };

    private short RawPadAxis(SDL.SDL_GameControllerAxis axis) => SDL.SDL_GameControllerGetAxis(GameController, axis);

    /// <summary>
    /// A controller axis from -1 to 1 (a trigger from 0 to 1), with nothing ignored around the
    /// middle; 0 without a controller. Down is positive on the sticks.
    /// </summary>
    internal float GetPadAxis(SDL.SDL_GameControllerAxis axis) =>
        GameController == IntPtr.Zero ? 0 : Math.Max(RawPadAxis(axis) / 32767f, -1f);

    public bool IsButtonPressed(ButtonType code)
    {
        return buttonstate[(int)code];
    }

    public bool IsButtonHeld(ButtonType code)
    {
        return buttonheld[(int)code];
    }

    public void SetButtonPressed(ButtonType button, bool isPressed)
    {
        buttonstate[(int)button] = isPressed;
    }

    public void SetButtonHeld(ButtonType button, bool isHeld)
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
        LastScan = ScanCodes.None;

        Array.Fill(Keyboard, false);
        pressedKeys.Clear();
        wheelDelta = 0;     // or turns made in a menu change weapons once the game carries on
    }

    /// <summary>Takes the oldest key pressed since the last call (or since keys were cleared).</summary>
    internal bool TryTakePressedKey(out ScanCodes key) => pressedKeys.TryDequeue(out key);

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

        if (Keyboard[(int)ScanCodes.Home])
        {
            mx = -1;
            my = -1;
        }
        else if (Keyboard[(int)ScanCodes.PgUp])
        {
            mx = 1;
            my = -1;
        }
        else if (Keyboard[(int)ScanCodes.End])
        {
            mx = -1;
            my = 1;
        }
        else if (Keyboard[(int)ScanCodes.PgDn])
        {
            mx = 1;
            my = 1;
        }

        if (Keyboard[(int)ScanCodes.UpArrow])
            my = -1;
        else if (Keyboard[(int)ScanCodes.DownArrow])
            my = 1;

        if (Keyboard[(int)ScanCodes.LeftArrow])
            mx = -1;
        else if (Keyboard[(int)ScanCodes.RightArrow])
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

        // A controller: its left stick, with the d-pad pushing it all the way
        if (GameController != IntPtr.Zero)
        {
            static bool Held(IntPtr pad, SDL.SDL_GameControllerButton button) => SDL.SDL_GameControllerGetButton(pad, button) != 0;

            int padx = RawPadAxis(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTX) >> 8;
            int pady = RawPadAxis(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY) >> 8;

            if (Held(GameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT))
                padx = -127;
            else if (Held(GameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT))
                padx = 127;
            if (Held(GameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP))
                pady = -127;
            else if (Held(GameController, SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN))
                pady = 127;

            dx = padx;
            dy = pady;
            return;
        }

        int x = SDL.SDL_JoystickGetAxis(Joystick, 0) >> 8;
        int y = SDL.SDL_JoystickGetAxis(Joystick, 1) >> 8;

        if (joystickHat != -1 && joystickHat < JoyNumHats)
        {
            byte hatState = SDL.SDL_JoystickGetHat(Joystick, joystickHat);

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

        // A controller's buttons in its own order, so A and B come first: the menus take the
        // first as select and the second as back
        if (GameController != IntPtr.Zero)
        {
            for (i = 0; i < (int)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_MAX; i++)
                res |= SDL.SDL_GameControllerGetButton(GameController, (SDL.SDL_GameControllerButton)i) << i;
            return res;
        }

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
                Quit?.Invoke(this, EventArgs.Empty);
                break;

            // Controllers come as joysticks too, so these cover both. SDL also sends an added
            // event at startup for each one already plugged in; OpenJoystick skips it if in use.
            case SDL.SDL_EventType.SDL_JOYDEVICEADDED:     // which: the device index
                OpenJoystick(e.jdevice.which);
                break;

            case SDL.SDL_EventType.SDL_JOYDEVICEREMOVED:   // which: the instance ID
                if (e.jdevice.which == JoystickInstance)
                {
                    Console.WriteLine($"Controller disconnected: {ControllerName}");
                    CloseJoystick();

                    // Carry on with another, if there is one
                    for (int i = 0; i < SDL.SDL_NumJoysticks() && Joystick == IntPtr.Zero; i++)
                        OpenJoystick(i);
                }
                break;

            case SDL.SDL_EventType.SDL_KEYDOWN:
                if (key == ScanCodes.ScrollLock || key == ScanCodes.F12)
                {
                    GrabInput = !GrabInput;

                    MouseGrabbed?.Invoke(this, GrabInput);

                    return;
                }

                // While the console is open it takes every key, so the game never sees them
                // (no walking, firing or menus while typing). The console is opened from the
                // play loop (CheckKeys), but closes here on ` or Escape.
                if (_consoleManager.Value.IsOpen)
                {
                    // Holding ` would otherwise auto-repeat and close the console right after opening it.
                    if (e.key.repeat != 0 && key == ScanCodes.Grave)
                        return;

                    _consoleManager.Value.HandleKey(MapKey(key));
                    return;
                }

                LastScan = MapKey(key);

                if (LastScan < ScanCodes.Last)
                    Keyboard[(int)LastScan] = true;

                if (e.key.repeat == 0 && pressedKeys.Count < MaxPressedKeys)
                    pressedKeys.Enqueue(LastScan);

                if (LastScan == ScanCodes.Pause)
                    Pause?.Invoke(this, true);
                break;

            case SDL.SDL_EventType.SDL_KEYUP:
                key = MapKey(key);

                if (key < ScanCodes.Last)
                    Keyboard[(int)key] = false;
                break;

            case SDL.SDL_EventType.SDL_MOUSEWHEEL:
                wheelDelta += e.wheel.direction == (uint)SDL.SDL_MouseWheelDirection.SDL_MOUSEWHEEL_FLIPPED
                    ? -e.wheel.y : e.wheel.y;
                break;

            case SDL.SDL_EventType.SDL_TEXTINPUT:
                string text;
                unsafe
                {
                    // e.text.text is a fixed-size, null-terminated UTF-8 buffer.
                    int length = 0;
                    while (length < TextInputSize && e.text.text[length] != 0)
                        length++;
                    text = System.Text.Encoding.UTF8.GetString(e.text.text, length);
                }

                if (_consoleManager.Value.IsOpen)
                {
                    _consoleManager.Value.HandleText(text);
                    break;
                }

                // Append rather than overwrite: several text events can arrive in one poll when
                // typing quickly, and readers clear the buffer once they've consumed it.
                int end = Array.IndexOf(textinput, (char)0);
                if (end < 0)
                    break;
                foreach (char c in text)
                {
                    if (end >= textinput.Length - 1)
                        break;
                    textinput[end++] = c;
                }
                break;
        }
    }

    /// <summary>
    /// The folding <see cref="MapKey"/> always does: right-hand modifiers into left-hand ones and
    /// keypad Enter into Enter. For reading a key's name from a .cfg file or the console, where
    /// MapKey's Num Lock-dependent keypad arrows would bind a different key depending on the
    /// Num Lock light at the time.
    /// </summary>
    internal static ScanCodes FoldKey(ScanCodes key) => key switch
    {
        ScanCodes.KeyPadEnter => ScanCodes.Enter,
        ScanCodes.RShift => ScanCodes.LShift,
        ScanCodes.RAlt => ScanCodes.LAlt,
        ScanCodes.RControl => ScanCodes.LControl,
        _ => key,
    };

    internal ScanCodes MapKey(ScanCodes key)
    {
        ScanCodes scan = FoldKey(key);

        switch (key)
        {
            case ScanCodes.KeyPad2:
            case ScanCodes.KeyPad4:
            case ScanCodes.KeyPad6:
            case ScanCodes.KeyPad8:
                if (((int)SDL.SDL_GetModState() & (int)SDL.SDL_Keymod.KMOD_NUM) == 0)
                {
                    switch ((ScanCodes)key)
                    {
                        case ScanCodes.KeyPad2: scan = ScanCodes.DownArrow; break;
                        case ScanCodes.KeyPad4: scan = ScanCodes.LeftArrow; break;
                        case ScanCodes.KeyPad6: scan = ScanCodes.RightArrow; break;
                        case ScanCodes.KeyPad8: scan = ScanCodes.UpArrow; break;
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

    private bool[] btnstate = new bool[(int)ButtonType.Count];
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

        for (i = 0; i < (int)ButtonType.Count; i++, buttons >>= 1)
        {
            if ((buttons & 1) != 0)
                btnstate[i] = true;
        }
    }

    public int MouseButtons()
    {
        if (MousePresent)
            return GetMouseButtons();
        else
            return 0;
    }

    public int GetMouseButtons()
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

        for (i = 0; i < (int)ButtonType.Count; i++, buttons >>= 1)
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
            Console.WriteLine("Center mouse");
          //  SDL.SDL_WarpMouseInWindow(window, screenWidth / 2, screenHeight / 2);
    }
}
