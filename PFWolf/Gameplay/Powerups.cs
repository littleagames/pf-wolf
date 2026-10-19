using PFWolf.Managers;

namespace PFWolf.Gameplay;

/*
=============================================================================

                                POWER-UPS

A Powerup item (actordefs native.yaml: End of Destiny's god mode and Knife Fury) is held as the
tics it has left: picking one up adds its powerup.duration, so another of the same before it's
over adds to what's left, and each tic takes the time off (PlayerStats.TickPowerups). Its count
is kept in a saved game like any item's, so nothing more needs saving; a level change
(inventory.interhubamount 0) or a death ends it.

While it runs:
  powerup.invulnerable  nothing hurts the player, and the screen doesn't flash red
  powerup.weapon        that weapon (if they have it) goes into their hand as they pick it up, and
                        the one they had comes back when it's over, if they're still holding it;
                        a better weapon picked up meanwhile isn't switched to
  powerup.weaponkills   every stab of that weapon kills (its blow powerup.damagetype's death)
  powerup.attacksound   played for that weapon's attacks in place of its own
  powerup.face          the status bar's face: a pic, or a list the face looks around through
  powerup.timerstyle    the time left, m:ss, in that hud-messages style (a message kept up to
                        date in its place, so timers sharing a position stack in the order they
                        started, below a script's clock shown the same way: game.message's key)
  powerup.flash         the screen is kept lit, as a pickup's flash
  powerup.endsound      played as it runs out

=============================================================================
*/

/// <summary>What the acting player's running power-ups do (see the notes above)</summary>
internal static class Powerups
{
    internal const string BaseClass = "Powerup";

    /// <summary>The acting player's power-ups that are running</summary>
    internal static IEnumerable<string> Running(InventoryManager inventory) =>
        inventory.GetClassesDerivedFrom(BaseClass).Where(inventory.Has);

    /// <summary>Whether a power-up's property is a true flag</summary>
    internal static bool Flag(InventoryManager inventory, string powerup, string property) =>
        inventory.GetProperty(powerup, property) is { } value && (value is true || bool.TryParse(value.ToString(), out var b) && b);

    /// <summary>Whether one running makes the player unhurt</summary>
    internal static bool Invulnerable(InventoryManager inventory) =>
        Running(inventory).Any(p => Flag(inventory, p, "powerup.invulnerable"));

    /// <summary>The running power-up that put this weapon in hand, if one did</summary>
    internal static string? ForWeapon(InventoryManager inventory, string? weapon) =>
        weapon == null ? null : Running(inventory).FirstOrDefault(p =>
            string.Equals(inventory.GetStringProperty(p, "powerup.weapon"), weapon, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether one running holds a weapon in the player's hand</summary>
    internal static bool HoldsAWeapon(InventoryManager inventory) =>
        Running(inventory).Any(p => inventory.GetStringProperty(p, "powerup.weapon") is { Length: > 0 });

    /// <summary>The status bar face one running shows, by the face's look-around frame; null for the usual one</summary>
    internal static string? Face(InventoryManager inventory, int faceFrame)
    {
        foreach (var powerup in Running(inventory))
        {
            switch (inventory.GetProperty(powerup, "powerup.face"))
            {
                case string { Length: > 0 } pic:
                    return pic;
                case IList<object> pics when pics.Count > 0:
                    return pics[Math.Abs(faceFrame) % pics.Count]?.ToString();
            }
        }
        return null;
    }

    /// <summary>Tics as the clock shows them: m:ss, the seconds begun counting down from (2100 is 0:30)</summary>
    internal static string TimerText(int tics)
    {
        int seconds = Math.Max(tics, 0) / 70;
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    /// <summary>The key a power-up's timer message goes by (HudMessageManager.ShowKeyed)</summary>
    internal static string TimerKey(string powerup) => "powerup:" + powerup;
}
