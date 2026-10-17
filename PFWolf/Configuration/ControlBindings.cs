using SDL2;

namespace PFWolf.Configuration;

/// <summary>
/// Something a control can be bound to: one of the play buttons (<see cref="ButtonType"/>),
/// then the keys that work while the automap is open (<see cref="AutomapAction"/>).
/// </summary>
internal readonly record struct ControlAction(int Index)
{
    private const int ButtonCount = (int)ButtonType.NUMBUTTONS;

    public const int Count = ButtonCount + (int)AutomapAction.NUMAUTOMAPKEYS;

    public static ControlAction Of(ButtonType button) => new((int)button);
    public static ControlAction Of(AutomapAction key) => new(ButtonCount + (int)key);

    public static IEnumerable<ControlAction> All => Enumerable.Range(0, Count).Select(i => new ControlAction(i));

    public bool IsButton => Index < ButtonCount;
    public ButtonType Button => IsButton ? (ButtonType)Index : ButtonType.bt_nobutton;
    public bool IsAutomapKey => Index >= ButtonCount;

    /// <summary>
    /// The name controls.cfg and the console use: the enum name, less bt_ for the buttons
    /// ("attack", "slot1"); the automap keys keep their am_ ("am_zoomin").
    /// </summary>
    public string Name => IsButton
        ? ((ButtonType)Index).ToString()["bt_".Length..]
        : ((AutomapAction)(Index - ButtonCount)).ToString();

    // Names the weapon slot keys had before they were generic, still found in older
    // controls.cfg files and menus.
    private static readonly Dictionary<string, ButtonType> OldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["readyknife"] = ButtonType.bt_slot1,
        ["readypistol"] = ButtonType.bt_slot2,
        ["readymachinegun"] = ButtonType.bt_slot3,
        ["readychaingun"] = ButtonType.bt_slot4,
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
        Default(ButtonType.bt_attack, Key(ScanCodes.sc_Control), Mouse(1), Trigger(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT));
        Default(ButtonType.bt_strafe, Key(ScanCodes.sc_Alt), Mouse(3), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_X));
        Default(ButtonType.bt_run, Key(ScanCodes.sc_LShift), Trigger(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT));
        Default(ButtonType.bt_use, Key(ScanCodes.sc_Space), Mouse(2), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A));
        Default(ButtonType.bt_slot1, Key(ScanCodes.sc_1));
        Default(ButtonType.bt_slot2, Key(ScanCodes.sc_2));
        Default(ButtonType.bt_slot3, Key(ScanCodes.sc_3));
        Default(ButtonType.bt_slot4, Key(ScanCodes.sc_4));
        Default(ButtonType.bt_slot5, Key(ScanCodes.sc_5));
        Default(ButtonType.bt_slot6, Key(ScanCodes.sc_6));
        Default(ButtonType.bt_slot7, Key(ScanCodes.sc_7));
        Default(ButtonType.bt_slot8, Key(ScanCodes.sc_8));
        Default(ButtonType.bt_slot9, Key(ScanCodes.sc_9));
        Default(ButtonType.bt_slot0, Key(ScanCodes.sc_0));
        Default(ButtonType.bt_nextweapon, InputCode.WheelDown, Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER));
        Default(ButtonType.bt_prevweapon, InputCode.WheelUp, Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER));
        Default(ButtonType.bt_esc, Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START));
        Default(ButtonType.bt_moveforward, Key(ScanCodes.sc_UpArrow), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP));
        Default(ButtonType.bt_movebackward, Key(ScanCodes.sc_DownArrow), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN));
        Default(ButtonType.bt_turnleft, Key(ScanCodes.sc_LeftArrow), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT));
        Default(ButtonType.bt_turnright, Key(ScanCodes.sc_RightArrow), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT));
        Default(ButtonType.bt_automap, Key(ScanCodes.sc_Tab), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_BACK));

        // A controller looks up and down with its right stick (PollJoystickMove)
        Default(ButtonType.bt_lookup, Key(ScanCodes.sc_PgUp));
        Default(ButtonType.bt_lookdown, Key(ScanCodes.sc_PgDn));
        Default(ButtonType.bt_centerview, Key(ScanCodes.sc_End), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSTICK));

        // Only does anything with a status bar radar (Planet Strike's); the automap's zoom keys
        // are its own while the map is open
        Default(ButtonType.bt_radarzoomin, Key(ScanCodes.sc_Equal));
        Default(ButtonType.bt_radarzoomout, Key(ScanCodes.sc_Minus));

        // Only does anything with a weapon that has a magazine (weapon.magazine)
        Default(ButtonType.bt_reload, Key(ScanCodes.sc_R), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_Y));
        // Only does anything with a weapon that has a scope (weapon.zoom)
        Default(ButtonType.bt_zoom, Key(ScanCodes.sc_Z), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSTICK));

        for (int i = 0; i < DefaultAutomapKeys.Length; i++)
            Add(ControlAction.Of((AutomapAction)i), Key(DefaultAutomapKeys[i]));

        // While the map is open the wheel zooms it rather than changing weapons (see InputPolling.IsControlDown)
        Add(ControlAction.Of(AutomapAction.am_zoomin), InputCode.WheelUp);
        Add(ControlAction.Of(AutomapAction.am_zoomout), InputCode.WheelDown);
    }

    private static readonly ScanCodes[] DefaultAutomapKeys =
    [
        ScanCodes.sc_Equal, ScanCodes.sc_Minus,
        ScanCodes.sc_KeyPad8, ScanCodes.sc_KeyPad2, ScanCodes.sc_KeyPad4, ScanCodes.sc_KeyPad6,
        ScanCodes.sc_C, ScanCodes.sc_F, ScanCodes.sc_R, ScanCodes.sc_V, ScanCodes.sc_O, ScanCodes.sc_G,
        ScanCodes.sc_S,
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

        ButtonType[] movement = [ButtonType.bt_moveforward, ButtonType.bt_turnright, ButtonType.bt_movebackward, ButtonType.bt_turnleft];
        for (int i = 0; i < Math.Min(dirScan.Length, movement.Length); i++)
            SetKey(ControlAction.Of(movement[i]), dirScan[i]);

        // Its keys for the movement buttons were always blank; the movement keys above stand for them
        for (int i = 0; i < buttonScan.Length && i < (int)ButtonType.bt_automap; i++)
        {
            if (Array.IndexOf(movement, (ButtonType)i) < 0)
                SetKey(ControlAction.Of((ButtonType)i), buttonScan[i]);
        }

        if (automapKey is ScanCodes key)
            SetKey(ControlAction.Of(ButtonType.bt_automap), key);

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
        ButtonType.bt_attack, ButtonType.bt_strafe, ButtonType.bt_use, ButtonType.bt_run,
        ButtonType.bt_strafeleft, ButtonType.bt_straferight, ButtonType.bt_esc, ButtonType.bt_pause,
        ButtonType.bt_prevweapon, ButtonType.bt_nextweapon,
        .. Enumerable.Repeat(ButtonType.bt_nobutton, 22),
    ];

    private static bool IsLegacyButton(ButtonType button) => button >= 0 && button < ButtonType.NUMBUTTONS;
}
