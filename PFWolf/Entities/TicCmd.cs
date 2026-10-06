namespace PFWolf.Entities;

/// <summary>
/// One frame of a player's controls: everything the game reads from them while it thinks. Built
/// from the local keys, mouse and controller (PollControls), read from a demo, or (later) received
/// from another player, and handed to that player's <see cref="PlayerInput"/> before the actors think.
/// </summary>
internal struct TicCmd
{
    /// <summary>A bit for each <see cref="buttontypes"/> held down (bit 0 is bt_attack)</summary>
    public uint Buttons;

    /// <summary>
    /// Turning (or sideways with bt_strafe), forward/back (negative is forwards) and a controller
    /// stick's strafe (positive is right): up to 100 a tic, already multiplied by the frame's tics
    /// </summary>
    public int ControlX, ControlY, ControlStrafe;

    /// <summary>How far to look up (down if negative) this frame, in degrees: the view only</summary>
    public double Pitch;

    /// <summary>Look straight ahead again this frame</summary>
    public bool CenterView;

    public readonly bool IsDown(buttontypes button) => (Buttons & Bit(button)) != 0;

    public void SetDown(buttontypes button, bool down)
    {
        if (down)
            Buttons |= Bit(button);
        else
            Buttons &= ~Bit(button);
    }

    static uint Bit(buttontypes button) => 1u << (int)button;
}
