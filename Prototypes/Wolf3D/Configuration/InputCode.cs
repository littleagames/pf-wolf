using SDL2;

namespace Wolf3D.Configuration;

/// <summary>What an <see cref="InputCode"/>'s code refers to.</summary>
internal enum InputDevice : byte
{
    None,

    /// <summary>A key: the code is a <see cref="ScanCodes"/> value.</summary>
    Key,

    /// <summary>A mouse button, numbered as SDL does: 1 left, 2 middle, 3 right, 4 and 5 the side buttons.</summary>
    MouseButton,

    /// <summary>A joystick button by its raw index, from 0: for a joystick SDL has no controller layout for.</summary>
    JoyButton,

    /// <summary>A controller button: the code is an <see cref="SDL.SDL_GameControllerButton"/>.</summary>
    PadButton,

    /// <summary>
    /// A controller stick pushed one way, or a trigger pulled, used as a button: the code is the
    /// <see cref="SDL.SDL_GameControllerAxis"/> times two, plus one for the positive direction.
    /// </summary>
    PadAxis,
}

/// <summary>
/// One key, mouse button or controller button that a control can be bound to. Written in
/// controls.cfg and the console by name: SDL's key names ("Left Ctrl"), "Mouse 1" to "Mouse 5",
/// "Joy 1" up (joystick buttons counted from 1), and "Pad A", "Pad RT", "Pad LStick Up" and so on
/// for a controller.
/// </summary>
internal readonly record struct InputCode(InputDevice Device, int Code)
{
    public const int MouseButtonCount = 5;
    public const int JoyButtonCount = 32;

    public static readonly InputCode None = default;

    public bool IsNone => Device == InputDevice.None;

    /// <summary>Whether this goes in a binding's controller slot rather than a keyboard and mouse one.</summary>
    public bool IsController => Device is InputDevice.JoyButton or InputDevice.PadButton or InputDevice.PadAxis;

    // By SDL_GameControllerButton
    private static readonly string[] PadButtonNames =
    [
        "Pad A", "Pad B", "Pad X", "Pad Y", "Pad Back", "Pad Guide", "Pad Start", "Pad LS", "Pad RS",
        "Pad LB", "Pad RB", "Pad Up", "Pad Down", "Pad Left", "Pad Right",
        "Pad Misc", "Pad P1", "Pad P2", "Pad P3", "Pad P4", "Pad Touchpad",
    ];

    // By PadAxis code: each SDL_GameControllerAxis negative then positive. A trigger only goes one way.
    private static readonly string?[] PadAxisNames =
    [
        "Pad LStick Left", "Pad LStick Right", "Pad LStick Up", "Pad LStick Down",
        "Pad RStick Left", "Pad RStick Right", "Pad RStick Up", "Pad RStick Down",
        null, "Pad LT", null, "Pad RT",
    ];

    public static InputCode FromPadButton(SDL.SDL_GameControllerButton button) =>
        button >= 0 && (int)button < PadButtonNames.Length ? new(InputDevice.PadButton, (int)button) : None;

    public static InputCode FromPadAxis(SDL.SDL_GameControllerAxis axis, bool positive)
    {
        int code = (int)axis * 2 + (positive ? 1 : 0);
        return code >= 0 && code < PadAxisNames.Length && PadAxisNames[code] != null ? new(InputDevice.PadAxis, code) : None;
    }

    /// <summary>For a <see cref="InputDevice.PadAxis"/>: the axis, and whether it's the positive direction.</summary>
    public SDL.SDL_GameControllerAxis Axis => (SDL.SDL_GameControllerAxis)(Code / 2);
    public bool IsPositive => Code % 2 == 1;

    public ScanCodes Key => Device == InputDevice.Key ? (ScanCodes)Code : ScanCodes.sc_None;

    public static InputCode FromKey(ScanCodes key) =>
        key > ScanCodes.sc_None && key < ScanCodes.sc_Last ? new(InputDevice.Key, (int)key) : None;

    public static InputCode FromMouseButton(int button) =>
        button >= 1 && button <= MouseButtonCount ? new(InputDevice.MouseButton, button) : None;

    public static InputCode FromJoyButton(int index) =>
        index >= 0 && index < JoyButtonCount ? new(InputDevice.JoyButton, index) : None;

    public override string ToString() => Device switch
    {
        InputDevice.Key => KeyName((ScanCodes)Code),
        InputDevice.MouseButton => $"Mouse {Code}",
        InputDevice.JoyButton => $"Joy {Code + 1}",
        InputDevice.PadButton => PadButtonNames[Code],
        InputDevice.PadAxis => PadAxisNames[Code]!,
        _ => "none",
    };

    /// <summary>A key's name as SDL gives it, which is what <see cref="TryParse"/> reads back.</summary>
    public static string KeyName(ScanCodes key)
    {
        var name = SDL.SDL_GetScancodeName((SDL.SDL_Scancode)key);
        return string.IsNullOrEmpty(name) ? $"#{(int)key}" : name;
    }

    /// <summary>
    /// Reads a name <see cref="ToString"/> writes, ignoring case. Keys come back as SDL reports
    /// them, before InputManager.MapKey folds right-hand modifiers into left-hand ones.
    /// </summary>
    public static bool TryParse(string text, out InputCode code)
    {
        code = None;
        text = text.Trim();

        int pad = Array.FindIndex(PadButtonNames, name => string.Equals(name, text, StringComparison.OrdinalIgnoreCase));
        int axis = Array.FindIndex(PadAxisNames, name => string.Equals(name, text, StringComparison.OrdinalIgnoreCase));

        if (pad >= 0)
            code = new(InputDevice.PadButton, pad);
        else if (axis >= 0)
            code = new(InputDevice.PadAxis, axis);
        else if (TryParseNumbered(text, "Mouse", out int button))
            code = FromMouseButton(button);
        else if (TryParseNumbered(text, "Joy", out int joy))
            code = FromJoyButton(joy - 1);
        else
        {
            var scancode = SDL.SDL_GetScancodeFromName(text);
            if (scancode == SDL.SDL_Scancode.SDL_SCANCODE_UNKNOWN && text.StartsWith('#')
                && int.TryParse(text[1..], out int raw))
                scancode = (SDL.SDL_Scancode)raw;
            code = FromKey((ScanCodes)scancode);
        }

        return !code.IsNone;
    }

    // "Mouse 3" or "Mouse3"
    private static bool TryParseNumbered(string text, string prefix, out int number)
    {
        number = 0;
        return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(text[prefix.Length..].Trim(), out number);
    }

    /// <summary>Every name <see cref="TryParse"/> accepts, for the console's tab completion.</summary>
    public static IEnumerable<string> AllNames() =>
        Enumerable.Range(1, (int)ScanCodes.sc_Last - 1)
            .Select(i => SDL.SDL_GetScancodeName((SDL.SDL_Scancode)i))
            .Where(name => !string.IsNullOrEmpty(name))
            .Concat(Enumerable.Range(1, MouseButtonCount).Select(i => $"Mouse {i}"))
            .Concat(Enumerable.Range(1, JoyButtonCount).Select(i => $"Joy {i}"))
            .Concat(PadButtonNames)
            .Concat(PadAxisNames.OfType<string>());
}
