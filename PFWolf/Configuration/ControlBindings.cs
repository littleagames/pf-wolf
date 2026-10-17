using SDL2;

namespace PFWolf.Configuration;

/// <summary>
/// Something a control can be bound to: one of the play buttons (<see cref="ButtonType"/>),
/// then the keys that work while the automap is open (<see cref="AutomapAction"/>).
/// </summary>
internal readonly record struct ControlAction(int Index)
{
    private const int ButtonCount = (int)ButtonType.Count;

    public const int Count = ButtonCount + (int)AutomapAction.Count;

    public static ControlAction Of(ButtonType button) => new((int)button);
    public static ControlAction Of(AutomapAction key) => new(ButtonCount + (int)key);

    public static IEnumerable<ControlAction> All => Enumerable.Range(0, Count).Select(i => new ControlAction(i));

    public bool IsButton => Index < ButtonCount;
    public ButtonType Button => IsButton ? (ButtonType)Index : ButtonType.NoButton;
    public bool IsAutomapKey => Index >= ButtonCount;

    /// <summary>
    /// The name controls.cfg and the console use (a file format, so it stays as it was): the
    /// enum name in lower case for the buttons ("attack", "slot1"), and with am_ before it for the
    /// automap keys ("am_zoomin").
    /// </summary>
    public string Name => IsButton
        ? ((ButtonType)Index).ToString().ToLowerInvariant()
        : "am_" + ((AutomapAction)(Index - ButtonCount)).ToString().ToLowerInvariant();

    // Names the weapon slot keys had before they were generic, still found in older
    // controls.cfg files and menus.
    private static readonly Dictionary<string, ButtonType> OldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["readyknife"] = ButtonType.Slot1,
        ["readypistol"] = ButtonType.Slot2,
        ["readymachinegun"] = ButtonType.Slot3,
        ["readychaingun"] = ButtonType.Slot4,
    };

    public static bool TryParse(string name, out ControlAction action)
    {
        if (OldNames.TryGetValue(name, out var renamed))
        {
            action = Of(renamed);
            return true;
        }

        foreach (var candidate in All)
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                action = candidate;
                return true;
            }
        }

        action = default;
        return false;
    }

    public override string ToString() => Name;
}

/// <summary>
/// What each control is bound to. Every action has <see cref="KeySlots"/> slots for keys and mouse
/// buttons and one for a controller button; an empty slot is <see cref="InputCode.None"/>. The same
/// input may be bound to more than one action.
/// </summary>
internal sealed class ControlBindings
{
    public const int KeySlots = 2;
    public const int ControllerSlot = KeySlots;
    public const int SlotCount = KeySlots + 1;

    private readonly InputCode[,] _slots = new InputCode[ControlAction.Count, SlotCount];

    public ControlBindings() => SetDefaults();

    public InputCode this[ControlAction action, int slot]
    {
        get => _slots[action.Index, slot];
        set
        {
            if (!value.IsNone && value.IsController != (slot == ControllerSlot))
                throw new ArgumentException($"{value} can't go in slot {slot}");
            _slots[action.Index, slot] = value;
        }
    }

    /// <summary>The inputs bound to an action, skipping empty slots.</summary>
    public IEnumerable<InputCode> Get(ControlAction action)
    {
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (!_slots[action.Index, slot].IsNone)
                yield return _slots[action.Index, slot];
        }
    }

    public IEnumerable<InputCode> Get(ButtonType button) => Get(ControlAction.Of(button));

    public bool IsBound(ControlAction action, InputCode code)
    {
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (_slots[action.Index, slot] == code)
                return true;
        }
        return false;
    }

    /// <summary>Puts an input in the action's first free slot of its kind. False if they're all taken.</summary>
    public bool Add(ControlAction action, InputCode code)
    {
        if (code.IsNone || IsBound(action, code))
            return !code.IsNone;

        foreach (int slot in SlotsFor(code))
        {
            if (_slots[action.Index, slot].IsNone)
            {
                _slots[action.Index, slot] = code;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Puts an input in one of an action's slots, and takes it off the action's other slots and
    /// off every other action in the same group: the play buttons, or the automap's keys, which
    /// only work while the map is open. Returns the actions it was taken off.
    /// </summary>
    public List<ControlAction> Set(ControlAction action, int slot, InputCode code)
    {
        var takenFrom = new List<ControlAction>();

        if (!code.IsNone)
        {
            foreach (var other in ControlAction.All)
            {
                if (other.IsAutomapKey != action.IsAutomapKey || !IsBound(other, code))
                    continue;

                for (int s = 0; s < SlotCount; s++)
                {
                    if (_slots[other.Index, s] == code)
                        _slots[other.Index, s] = InputCode.None;
                }

                if (other != action)
                    takenFrom.Add(other);
            }
        }

        this[action, slot] = code;
        return takenFrom;
    }

    /// <summary>Empties an action's slots, or only those holding the given device's inputs.</summary>
    public void Clear(ControlAction action, InputDevice? device = null)
    {
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (device == null || _slots[action.Index, slot].Device == device)
                _slots[action.Index, slot] = InputCode.None;
        }
    }

    public void ClearAll() => Array.Clear(_slots);

    private static IEnumerable<int> SlotsFor(InputCode code) =>
        code.IsController ? [ControllerSlot] : Enumerable.Range(0, KeySlots);

    /// <summary>A `bindaction` command for every action, for saving to controls.cfg.</summary>
    public IEnumerable<string> GetCommands() =>
        ControlAction.All.Select(action => $"bindaction {action.Name} {FormatInputs(action)}");

    /// <summary>An action's inputs as `bindaction` takes them: each quoted, or "none".</summary>
    public string FormatInputs(ControlAction action)
    {
        var inputs = Get(action).Select(code => $"\"{code}\"").ToList();
        return inputs.Count > 0 ? string.Join(' ', inputs) : "none";
    }

    /*
    =============================================================================

                                    DEFAULTS

    =============================================================================
    */

    public void SetDefaults()
    {
        ClearAll();

        void Default(ButtonType button, params InputCode[] codes)
        {
            foreach (var code in codes)
                Add(ControlAction.Of(button), code);
        }

        static InputCode Key(ScanCodes key) => InputCode.FromKey(key);
        static InputCode Mouse(int button) => InputCode.FromMouseButton(button);
        static InputCode Pad(SDL.SDL_GameControllerButton button) => InputCode.FromPadButton(button);
        static InputCode Trigger(SDL.SDL_GameControllerAxis axis) => InputCode.FromPadAxis(axis, true);

        // A controller walks, strafes and turns with its sticks (PollJoystickMove); the d-pad
        // walks and turns like the arrow keys.
        Default(ButtonType.Attack, Key(ScanCodes.Control), Mouse(1), Trigger(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT));
        Default(ButtonType.Strafe, Key(ScanCodes.Alt), Mouse(3), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_X));
        Default(ButtonType.Run, Key(ScanCodes.LShift), Trigger(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT));
        Default(ButtonType.Use, Key(ScanCodes.Space), Mouse(2), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A));
        Default(ButtonType.Slot1, Key(ScanCodes.D1));
        Default(ButtonType.Slot2, Key(ScanCodes.D2));
        Default(ButtonType.Slot3, Key(ScanCodes.D3));
        Default(ButtonType.Slot4, Key(ScanCodes.D4));
        Default(ButtonType.Slot5, Key(ScanCodes.D5));
        Default(ButtonType.Slot6, Key(ScanCodes.D6));
        Default(ButtonType.Slot7, Key(ScanCodes.D7));
        Default(ButtonType.Slot8, Key(ScanCodes.D8));
        Default(ButtonType.Slot9, Key(ScanCodes.D9));
        Default(ButtonType.Slot0, Key(ScanCodes.D0));
        Default(ButtonType.NextWeapon, InputCode.WheelDown, Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER));
        Default(ButtonType.PrevWeapon, InputCode.WheelUp, Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER));
        Default(ButtonType.Esc, Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START));
        Default(ButtonType.MoveForward, Key(ScanCodes.UpArrow), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP));
        Default(ButtonType.MoveBackward, Key(ScanCodes.DownArrow), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN));
        Default(ButtonType.TurnLeft, Key(ScanCodes.LeftArrow), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT));
        Default(ButtonType.TurnRight, Key(ScanCodes.RightArrow), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT));
        Default(ButtonType.Automap, Key(ScanCodes.Tab), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_BACK));

        // A controller looks up and down with its right stick (PollJoystickMove)
        Default(ButtonType.LookUp, Key(ScanCodes.PgUp));
        Default(ButtonType.LookDown, Key(ScanCodes.PgDn));
        Default(ButtonType.CenterView, Key(ScanCodes.End), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSTICK));

        // Only does anything with a status bar radar (Planet Strike's); the automap's zoom keys
        // are its own while the map is open
        Default(ButtonType.RadarZoomIn, Key(ScanCodes.Equal));
        Default(ButtonType.RadarZoomOut, Key(ScanCodes.Minus));

        // Only does anything with a weapon that has a magazine (weapon.magazine)
        Default(ButtonType.Reload, Key(ScanCodes.R), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_Y));
        // Only does anything with a weapon that has a scope (weapon.zoom)
        Default(ButtonType.Zoom, Key(ScanCodes.Z), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSTICK));

        for (int i = 0; i < DefaultAutomapKeys.Length; i++)
            Add(ControlAction.Of((AutomapAction)i), Key(DefaultAutomapKeys[i]));

        // While the map is open the wheel zooms it rather than changing weapons (see InputPolling.IsControlDown)
        Add(ControlAction.Of(AutomapAction.ZoomIn), InputCode.WheelUp);
        Add(ControlAction.Of(AutomapAction.ZoomOut), InputCode.WheelDown);
    }

    private static readonly ScanCodes[] DefaultAutomapKeys =
    [
        ScanCodes.Equal, ScanCodes.Minus,
        ScanCodes.KeyPad8, ScanCodes.KeyPad2, ScanCodes.KeyPad4, ScanCodes.KeyPad6,
        ScanCodes.C, ScanCodes.F, ScanCodes.R, ScanCodes.V, ScanCodes.O, ScanCodes.G,
        ScanCodes.S,
    ];

    /*
    =============================================================================

                                OLD CONFIG.CFG LAYOUT

    Before controls.cfg, config.cfg held one key per button, the four movement
    keys, and which button each mouse and joystick button pressed. Read once,
    for a config.cfg written before controls.cfg existed.

    =============================================================================
    */

    // The old mouse slots were the left, right and middle buttons, in that order
    private static readonly int[] LegacyMouseButtons = [1, 3, 2];

    /// <summary>
    /// Takes the old layout's bindings over the defaults. It only ever held keys in the first
    /// slot, mouse buttons and joystick buttons, so those replace the defaults and the rest stays.
    /// </summary>
    /// <param name="dirScan">Forward, right, back, left.</param>
    /// <param name="buttonScan">A key per button, up to (not including) the automap's.</param>
    /// <param name="automapKey">The automap's key, if the config had one.</param>
    /// <param name="automapKeys">The automap's own keys, if the config had them; may be short.</param>
    public void ImportLegacy(ScanCodes[] dirScan, ScanCodes[] buttonScan, ButtonType[] buttonMouse,
        ButtonType[] buttonJoy, ScanCodes? automapKey, ScanCodes[]? automapKeys)
    {
        void SetKey(ControlAction action, ScanCodes key)
        {
            var code = InputCode.FromKey(key);
            if (code.IsNone)
                return;

            // The old layout only had the first key: it replaces that, any second key stays
            if (this[action, 0].Device is InputDevice.Key or InputDevice.None)
                this[action, 0] = code;
            else
                Add(action, code);
        }

        ButtonType[] movement = [ButtonType.MoveForward, ButtonType.TurnRight, ButtonType.MoveBackward, ButtonType.TurnLeft];
        for (int i = 0; i < Math.Min(dirScan.Length, movement.Length); i++)
            SetKey(ControlAction.Of(movement[i]), dirScan[i]);

        // Its keys for the movement buttons were always blank; the movement keys above stand for them
        for (int i = 0; i < buttonScan.Length && i < (int)ButtonType.Automap; i++)
        {
            if (Array.IndexOf(movement, (ButtonType)i) < 0)
                SetKey(ControlAction.Of((ButtonType)i), buttonScan[i]);
        }

        if (automapKey is ScanCodes key)
            SetKey(ControlAction.Of(ButtonType.Automap), key);

        for (int i = 0; i < Math.Min(automapKeys?.Length ?? 0, DefaultAutomapKeys.Length); i++)
            SetKey(ControlAction.Of((AutomapAction)i), automapKeys![i]);

        foreach (var action in ControlAction.All)
            Clear(action, InputDevice.MouseButton);

        for (int i = 0; i < Math.Min(buttonMouse.Length, LegacyMouseButtons.Length); i++)
        {
            if (IsLegacyButton(buttonMouse[i]))
                Add(ControlAction.Of(buttonMouse[i]), InputCode.FromMouseButton(LegacyMouseButtons[i]));
        }

        // Raw joystick buttons only if they were changed from the old layout's: the stock one
        // gives way to the named controller buttons the defaults use now.
        if (!buttonJoy.SequenceEqual(LegacyJoyDefaults))
        {
            foreach (var action in ControlAction.All)
                this[action, ControllerSlot] = InputCode.None;

            for (int i = 0; i < buttonJoy.Length; i++)
            {
                if (IsLegacyButton(buttonJoy[i]))
                    Add(ControlAction.Of(buttonJoy[i]), InputCode.FromJoyButton(i));
            }
        }
    }

    // The old layout's 32 joystick buttons: these ten, then the rest unbound
    private static readonly ButtonType[] LegacyJoyDefaults =
    [
        ButtonType.Attack, ButtonType.Strafe, ButtonType.Use, ButtonType.Run,
        ButtonType.StrafeLeft, ButtonType.StrafeRight, ButtonType.Esc, ButtonType.Pause,
        ButtonType.PrevWeapon, ButtonType.NextWeapon,
        .. Enumerable.Repeat(ButtonType.NoButton, 22),
    ];

    private static bool IsLegacyButton(ButtonType button) => button >= 0 && button < ButtonType.Count;
}
