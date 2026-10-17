using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>Where a scope is in its zoom (Midnight Assault's zoomState)</summary>
internal enum ZoomMode
{
    Ready,      // not zoomed
    ZoomIn,     // zoom held: zooming in
    Zoomed,     // let go once in: held there
    ZoomOut,    // zoom held again: zooming out
}

/*
=============================================================================

                                 WEAPON ZOOM

A weapon with a scope (Midnight Assault's sniper rifle, wl_agent.c CheckZoom): `weapon.zoom` is
how many times it magnifies at its most. Holding the zoom key zooms in (2 a tic, of 100); let go,
it stays there; held again it zooms out (5 a tic), and let go then it's back to none. Each press
plays `weapon.zoomsound`. While zooming, `weapon.zoompic` covers the view in place of the weapon
(Rendering: the scope), and once zoomed in the player turns `weapon.zoomturn` times slower and the
weapon fires its ZoomFire state, if it has one, in place of Fire. Changing weapons or reloading
drops the zoom.

=============================================================================
*/

internal sealed class WeaponZoom(InventoryManager inventory, AudioManager audio, GameSession session)
{
    internal const int MaxLevel = 100;
    internal const int InPerTic = 2;
    internal const int OutPerTic = 5;

    /// <summary>How many times the weapon magnifies at its most (weapon.zoom); 0 (or 1) for none</summary>
    internal double MaxZoom(string? weapon) =>
        weapon != null && inventory.GetProperty(weapon, "weapon.zoom") is { } zoom
            && double.TryParse(zoom.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z)
            ? z : 0;

    /// <summary>Moves the acting player's zoom on by a tic's worth, from the zoom key</summary>
    internal void Tick(bool reloading)
    {
        var playerstate = session.PlayerState;
        var input = session.PlayerInput;
        var weapon = playerstate.Weapon;
        if (MaxZoom(weapon) <= 1 || reloading)
        {
            Reset();
            return;
        }

        bool pressed = input.IsPressed(ButtonType.Zoom), held = input.IsHeld(ButtonType.Zoom);
        if (pressed && !held && playerstate.ZoomMode is ZoomMode.Ready or ZoomMode.Zoomed
            && inventory.GetStringProperty(weapon!, "weapon.zoomsound") is { Length: > 0 } sound)
            audio.Play(sound);

        int tics = (int)session.Tics;
        (playerstate.ZoomMode, playerstate.ZoomLevel) = Step(playerstate.ZoomMode, playerstate.ZoomLevel, pressed, held, tics);
        playerstate.TurnSlowdown = playerstate.ZoomMode == ZoomMode.Zoomed
            ? Math.Max(1, inventory.GetIntProperty(weapon!, "weapon.zoomturn", 1))
            : 1;
    }

    /// <summary>
    /// The zoom's next mode and level (CheckZoom): held from before, zooming in from ready (or on
    /// in), out from zoomed (or on out); let go, a zoom in holds there and a zoom out ends
    /// </summary>
    internal static (ZoomMode Mode, int Level) Step(ZoomMode mode, int level, bool pressed, bool held, int tics)
    {
        if (mode is ZoomMode.Ready or ZoomMode.ZoomIn && held)
            return (ZoomMode.ZoomIn, Math.Min(level + InPerTic * tics, MaxLevel));
        if (mode == ZoomMode.ZoomIn && !pressed && !held)
            return (ZoomMode.Zoomed, level);
        if (mode is ZoomMode.Zoomed or ZoomMode.ZoomOut && held)
            return (ZoomMode.ZoomOut, Math.Max(level - OutPerTic * tics, 0));
        if (mode == ZoomMode.ZoomOut && !pressed && !held)
            return (ZoomMode.Ready, 0);
        return (mode, level);
    }

    /// <summary>The acting player's scope down: no zoom</summary>
    internal void Reset()
    {
        var playerstate = session.PlayerState;
        (playerstate.ZoomMode, playerstate.ZoomLevel, playerstate.TurnSlowdown) = (ZoomMode.Ready, 0, 1);
    }

    /// <summary>How many times the view's magnified now: 1 for not at all</summary>
    internal double Magnification()
    {
        var playerstate = session.PlayerState;
        return 1 + (MaxZoom(playerstate.Weapon) - 1) * playerstate.ZoomLevel / MaxLevel;
    }

    /// <summary>Whether the acting player is looking through the scope: zooming in, zoomed or zooming out</summary>
    internal bool Scoped => session.PlayerState.ZoomMode != ZoomMode.Ready;

    /// <summary>Whether the acting player is zoomed all the way in (and let go): ZoomFire's turn</summary>
    internal bool Zoomed => session.PlayerState.ZoomMode == ZoomMode.Zoomed;

    /// <summary>The picture over the view while scoped (weapon.zoompic), or null</summary>
    internal string? ScopePicture() =>
        Scoped && session.PlayerState.Weapon is { } weapon && inventory.GetStringProperty(weapon, "weapon.zoompic") is { Length: > 0 } pic
            ? pic : null;
}
