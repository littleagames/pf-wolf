namespace PFWolf.Configuration;

/// <summary>
/// The mouse look and controller settings (the Control and Controller Settings menus, and m_*
/// and joy_* in controls.cfg)
/// </summary>
internal sealed class ControlSettings
{
    internal const int JOYTURNSPEEDS = 10;

    // Mouse look: moving the mouse up and down looks up and down rather than walking, the other
    // way round if inverted
    internal bool mouselook, mouseinvert;

    // How much of a stick's travel around the middle is ignored, in percent; how fast a stick
    // turns, as a step of JoyTurnSpeed; and whether the left stick turns, with the right
    // strafing (classic), rather than the right stick turning (modern).
    internal int joydeadzone = 20;
    internal int joyturnspeed = 4;
    internal bool joyclassicsticks;

    internal string JoySticksName => joyclassicsticks ? "classic" : "modern";

    /// <summary>The settings as console commands, for saving to controls.cfg.</summary>
    internal IEnumerable<string> SettingCommands() =>
    [
        $"m_look {(mouselook ? 1 : 0)}",
        $"m_invert {(mouseinvert ? 1 : 0)}",
        $"joy_deadzone {joydeadzone}",
        $"joy_turnspeed {joyturnspeed}",
        $"joy_sticks {JoySticksName}",
    ];
}
