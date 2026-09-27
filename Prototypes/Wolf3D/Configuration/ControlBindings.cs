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
    /// ("attack", "readyknife"); the automap keys keep their am_ ("am_zoomin").
    /// </summary>
    public string Name => IsButton
        ? ((buttontypes)Index).ToString()["bt_".Length..]
        : ((Program.automapkeys)(Index - ButtonCount)).ToString();

    public static bool TryParse(string name, out ControlAction action)
    {
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
    /// Binds an input to an action in place of the action's own input from the same device (or
    /// in a free slot, or over its last slot of that kind), taking it off every other action first.
    /// </summary>
    public void Replace(ControlAction action, InputCode code)
    {
        if (code.IsNone)
            return;

        Unbind(code);

        var slots = SlotsFor(code).ToArray();
        int found = Array.FindIndex(slots, s => _slots[action.Index, s].Device == code.Device);
        if (found < 0)
            found = Array.FindIndex(slots, s => _slots[action.Index, s].IsNone);

        _slots[action.Index, found >= 0 ? slots[found] : slots[^1]] = code;
    }

    /// <summary>Takes an input off every action it's bound to.</summary>
    public void Unbind(InputCode code)
    {
        for (int action = 0; action < ControlAction.Count; action++)
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                if (_slots[action, slot] == code)
                    _slots[action, slot] = InputCode.None;
            }
        }
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
        static InputCode Joy(int index) => InputCode.FromJoyButton(index);

        Default(buttontypes.bt_attack, Key(ScanCodes.sc_Control), Mouse(1), Joy(0));
        Default(buttontypes.bt_strafe, Key(ScanCodes.sc_Alt), Mouse(3), Joy(1));
        Default(buttontypes.bt_run, Key(ScanCodes.sc_LShift), Joy(3));
        Default(buttontypes.bt_use, Key(ScanCodes.sc_Space), Mouse(2), Joy(2));
        Default(buttontypes.bt_readyknife, Key(ScanCodes.sc_1));
        Default(buttontypes.bt_readypistol, Key(ScanCodes.sc_2));
        Default(buttontypes.bt_readymachinegun, Key(ScanCodes.sc_3));
        Default(buttontypes.bt_readychaingun, Key(ScanCodes.sc_4));
        Default(buttontypes.bt_nextweapon, Joy(9));
        Default(buttontypes.bt_prevweapon, Joy(8));
        Default(buttontypes.bt_esc, Joy(6));
        Default(buttontypes.bt_pause, Joy(7));
        Default(buttontypes.bt_strafeleft, Joy(4));
        Default(buttontypes.bt_straferight, Joy(5));
        Default(buttontypes.bt_moveforward, Key(ScanCodes.sc_UpArrow));
        Default(buttontypes.bt_movebackward, Key(ScanCodes.sc_DownArrow));
        Default(buttontypes.bt_turnleft, Key(ScanCodes.sc_LeftArrow));
        Default(buttontypes.bt_turnright, Key(ScanCodes.sc_RightArrow));
        Default(buttontypes.bt_automap, Key(ScanCodes.sc_Tab));

        for (int i = 0; i < DefaultAutomapKeys.Length; i++)
            Add(ControlAction.Of((Program.automapkeys)i), Key(DefaultAutomapKeys[i]));
    }

    private static readonly ScanCodes[] DefaultAutomapKeys =
    [
        ScanCodes.sc_Equal, ScanCodes.sc_Minus,
        ScanCodes.sc_KeyPad8, ScanCodes.sc_KeyPad2, ScanCodes.sc_KeyPad4, ScanCodes.sc_KeyPad6,
        ScanCodes.sc_C, ScanCodes.sc_F, ScanCodes.sc_R, ScanCodes.sc_V, ScanCodes.sc_O, ScanCodes.sc_G,
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
        {
            Clear(action, InputDevice.MouseButton);
            Clear(action, InputDevice.JoyButton);
        }

        for (int i = 0; i < Math.Min(buttonMouse.Length, LegacyMouseButtons.Length); i++)
        {
            if (IsLegacyButton(buttonMouse[i]))
                Add(ControlAction.Of(buttonMouse[i]), InputCode.FromMouseButton(LegacyMouseButtons[i]));
        }

        for (int i = 0; i < buttonJoy.Length; i++)
        {
            if (IsLegacyButton(buttonJoy[i]))
                Add(ControlAction.Of(buttonJoy[i]), InputCode.FromJoyButton(i));
        }
    }

    private static bool IsLegacyButton(buttontypes button) => button >= 0 && button < buttontypes.NUMBUTTONS;
}
