namespace Wolf3D.Entities;

/// <summary>
/// A player's controls as the game sees them while it thinks: this frame's <see cref="TicCmd"/>,
/// and which buttons were already down the frame before, so a fresh press can be told from one
/// held down. The game reads only this, never the keyboard, mouse or controller, so any player's
/// commands (local, from a demo or from the network) drive it the same way.
/// </summary>
/// <remarks>
/// Works as InputManager's button state always did for the game: <see cref="Begin"/> makes this
/// frame's buttons out of the command and last frame's (as the game left them) "held", and the
/// game may change either during the frame (a use or fire press dropped mid-attack, a switch
/// marking use as held).
/// </remarks>
internal sealed class PlayerInput
{
    TicCmd cmd;
    uint held;

    public int ControlX => cmd.ControlX;
    public int ControlY => cmd.ControlY;
    public int ControlStrafe => cmd.ControlStrafe;
    public double Pitch => cmd.Pitch;
    public bool CenterView => cmd.CenterView;

    /// <summary>Takes this frame's command: the buttons down last frame become the held ones</summary>
    public void Begin(in TicCmd next)
    {
        held = cmd.Buttons;
        cmd = next;
    }

    /// <summary>Nothing is pressed (as a level starts): the next frame sees every press as fresh</summary>
    public void Reset() => cmd = default;

    /// <summary>This frame's buttons as the game has left them (next frame's held ones), to send a player joining mid-game</summary>
    public uint Buttons => cmd.Buttons;

    /// <summary>Puts back the buttons <see cref="Buttons"/> gave, as a player joining mid-game takes the game up</summary>
    public void RestoreButtons(uint buttons) => cmd = new TicCmd { Buttons = buttons };

    public bool IsPressed(buttontypes button) => cmd.IsDown(button);

    public bool IsHeld(buttontypes button) => (held & (1u << (int)button)) != 0;

    /// <summary>Down this frame but not the last: a press, not a button held down</summary>
    public bool IsFreshPress(buttontypes button) => IsPressed(button) && !IsHeld(button);

    public void SetPressed(buttontypes button, bool pressed) => cmd.SetDown(button, pressed);

    public void SetHeld(buttontypes button, bool isHeld)
    {
        if (isHeld)
            held |= 1u << (int)button;
        else
            held &= ~(1u << (int)button);
    }
}
