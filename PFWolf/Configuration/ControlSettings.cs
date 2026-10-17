namespace PFWolf.Configuration;

/// <summary>
/// The mouse look and controller settings (the Control and Controller Settings menus, and m_*
/// and joy_* in controls.cfg)
/// </summary>
internal sealed class ControlSettings
{
    internal const int JoyTurnSpeeds = 10;

    // Whether the mouse and the controller are used at all (the Control menu, config.cfg), and
    // the mouse's sensitivity, 0 to 9 (the mouse sensitivity screen)
    internal bool MouseEnabled, JoystickEnabled;
    internal int MouseAdjustment;

    // Mouse look: moving the mouse up and down looks up and down rather than walking, the other
    // way round if inverted
    internal bool MouseLook, MouseInvert;

    // How much of a stick's travel around the middle is ignored, in percent; how fast a stick
    // turns, as a step of JoyTurnSpeed; and whether the left stick turns, with the right
    // strafing (classic), rather than the right stick turning (modern).
    internal int JoyDeadzone = 20;
    internal int JoyTurnSpeed = 4;
    internal bool JoyClassicSticks;

    internal string JoySticksName => JoyClassicSticks ? "classic" : "modern";

    /// <summary>The settings as console commands, for saving to controls.cfg.</summary>
    internal IEnumerable<string> SettingCommands() =>
    [
        $"m_look {(MouseLook ? 1 : 0)}",
        $"m_invert {(MouseInvert ? 1 : 0)}",
        $"joy_deadzone {JoyDeadzone}",
        $"joy_turnspeed {JoyTurnSpeed}",
        $"joy_sticks {JoySticksName}",
    ];
}
