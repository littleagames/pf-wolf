using SDL2;

namespace Wolf3D.Configuration;

/// <summary>
/// Something a control can be bound to: one of the play buttons (<see cref="buttontypes"/>),
/// then the keys that work while the automap is open (<see cref="Program.automapkeys"/>).
/// </summary>
internal readonly record struct ControlAction(int Index)
{
    private const int ButtonCount = (int)buttontypes.NUMBUTTONS;

    public const int Count = ButtonCount + (int)Program.automapkeys.NUMAUTOMAPKEYS;

    public static ControlAction Of(buttontypes button) => new((int)button);
    public static ControlAction Of(Program.automapkeys key) => new(ButtonCount + (int)key);

    public static IEnumerable<ControlAction> All => Enumerable.Range(0, Count).Select(i => new ControlAction(i));

    public bool IsButton => Index < ButtonCount;
    public buttontypes Button => IsButton ? (buttontypes)Index : buttontypes.bt_nobutton;
    public bool IsAutomapKey => Index >= ButtonCount;

    /// <summary>
    /// The name controls.cfg and the console use: the enum name, less bt_ for the buttons
    /// ("attack", "slot1"); the automap keys keep their am_ ("am_zoomin").
    /// </summary>
    public string Name => IsButton
        ? ((buttontypes)Index).ToString()["bt_".Length..]
        : ((Program.automapkeys)(Index - ButtonCount)).ToString();

    // Names the weapon slot keys had before they were generic, still found in older
    // controls.cfg files and menus.
    private static readonly Dictionary<string, buttontypes> OldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["readyknife"] = buttontypes.bt_slot1,
        ["readypistol"] = buttontypes.bt_slot2,
        ["readymachinegun"] = buttontypes.bt_slot3,
        ["readychaingun"] = buttontypes.bt_slot4,
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

    public IEnumerable<InputCode> Get(buttontypes button) => Get(ControlAction.Of(button));

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

        void Default(buttontypes button, params InputCode[] codes)
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
        Default(buttontypes.bt_attack, Key(ScanCodes.sc_Control), Mouse(1), Trigger(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT));
        Default(buttontypes.bt_strafe, Key(ScanCodes.sc_Alt), Mouse(3), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_X));
        Default(buttontypes.bt_run, Key(ScanCodes.sc_LShift), Trigger(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT));
        Default(buttontypes.bt_use, Key(ScanCodes.sc_Space), Mouse(2), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A));
        Default(buttontypes.bt_slot1, Key(ScanCodes.sc_1));
        Default(buttontypes.bt_slot2, Key(ScanCodes.sc_2));
        Default(buttontypes.bt_slot3, Key(ScanCodes.sc_3));
        Default(buttontypes.bt_slot4, Key(ScanCodes.sc_4));
        Default(buttontypes.bt_slot5, Key(ScanCodes.sc_5));
        Default(buttontypes.bt_slot6, Key(ScanCodes.sc_6));
        Default(buttontypes.bt_slot7, Key(ScanCodes.sc_7));
        Default(buttontypes.bt_slot8, Key(ScanCodes.sc_8));
        Default(buttontypes.bt_slot9, Key(ScanCodes.sc_9));
        Default(buttontypes.bt_slot0, Key(ScanCodes.sc_0));
        Default(buttontypes.bt_nextweapon, InputCode.WheelDown, Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER));
        Default(buttontypes.bt_prevweapon, InputCode.WheelUp, Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSHOULDER));
        Default(buttontypes.bt_esc, Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START));
        Default(buttontypes.bt_moveforward, Key(ScanCodes.sc_UpArrow), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP));
        Default(buttontypes.bt_movebackward, Key(ScanCodes.sc_DownArrow), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN));
        Default(buttontypes.bt_turnleft, Key(ScanCodes.sc_LeftArrow), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT));
        Default(buttontypes.bt_turnright, Key(ScanCodes.sc_RightArrow), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT));
        Default(buttontypes.bt_automap, Key(ScanCodes.sc_Tab), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_BACK));

        // A controller looks up and down with its right stick (PollJoystickMove)
        Default(buttontypes.bt_lookup, Key(ScanCodes.sc_PgUp));
        Default(buttontypes.bt_lookdown, Key(ScanCodes.sc_PgDn));
        Default(buttontypes.bt_centerview, Key(ScanCodes.sc_End), Pad(SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSTICK));

        // Only does anything with a status bar radar (Planet Strike's); the automap's zoom keys
        // are its own while the map is open
        Default(buttontypes.bt_radarzoomin, Key(ScanCodes.sc_Equal));
        Default(buttontypes.bt_radarzoomout, Key(ScanCodes.sc_Minus));

        for (int i = 0; i < DefaultAutomapKeys.Length; i++)
            Add(ControlAction.Of((Program.automapkeys)i), Key(DefaultAutomapKeys[i]));

        // While the map is open the wheel zooms it rather than changing weapons (see Program.IsControlDown)
        Add(ControlAction.Of(Program.automapkeys.am_zoomin), InputCode.WheelUp);
        Add(ControlAction.Of(Program.automapkeys.am_zoomout), InputCode.WheelDown);
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
    public void ImportLegacy(ScanCodes[] dirScan, ScanCodes[] buttonScan, buttontypes[] buttonMouse,
        buttontypes[] buttonJoy, ScanCodes? automapKey, ScanCodes[]? automapKeys)
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

        buttontypes[] movement = [buttontypes.bt_moveforward, buttontypes.bt_turnright, buttontypes.bt_movebackward, buttontypes.bt_turnleft];
        for (int i = 0; i < Math.Min(dirScan.Length, movement.Length); i++)
            SetKey(ControlAction.Of(movement[i]), dirScan[i]);

        // Its keys for the movement buttons were always blank; the movement keys above stand for them
        for (int i = 0; i < buttonScan.Length && i < (int)buttontypes.bt_automap; i++)
        {
            if (Array.IndexOf(movement, (buttontypes)i) < 0)
                SetKey(ControlAction.Of((buttontypes)i), buttonScan[i]);
        }

        if (automapKey is ScanCodes key)
            SetKey(ControlAction.Of(buttontypes.bt_automap), key);

        for (int i = 0; i < Math.Min(automapKeys?.Length ?? 0, DefaultAutomapKeys.Length); i++)
            SetKey(ControlAction.Of((Program.automapkeys)i), automapKeys![i]);

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
    private static readonly buttontypes[] LegacyJoyDefaults =
    [
        buttontypes.bt_attack, buttontypes.bt_strafe, buttontypes.bt_use, buttontypes.bt_run,
        buttontypes.bt_strafeleft, buttontypes.bt_straferight, buttontypes.bt_esc, buttontypes.bt_pause,
        buttontypes.bt_prevweapon, buttontypes.bt_nextweapon,
        .. Enumerable.Repeat(buttontypes.bt_nobutton, 22),
    ];

    private static bool IsLegacyButton(buttontypes button) => button >= 0 && button < buttontypes.NUMBUTTONS;
}
