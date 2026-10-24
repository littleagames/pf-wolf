using PFWolf.Entities.Actors;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>What picking items up needs from the game</summary>
/// <param name="PlayPlayerSound">A sound the acting player makes (the item's pickup sound)</param>
/// <param name="HasWeaponFrom">Deathmatch: whether the player already has the weapon a pickup gives</param>
/// <param name="ItemRespawn">Deathmatch: whether items taken come back (the game's rules)</param>
/// <param name="QueueItemRespawn">Deathmatch: brings an item taken back a while later</param>
/// <param name="Taken">An item's been taken, by the acting player (Heist's loot: Gameplay/MatchModes)</param>
internal sealed record PickupHooks(
    Action<string> PlayPlayerSound,
    Func<Weapon, bool> HasWeaponFrom,
    Func<bool> ItemRespawn,
    Action<Inventory> QueueItemRespawn,
    Action<Inventory>? Taken = null);

/// <summary>
/// The acting player picking up an item they walk over: unless their class may not, or they
/// can't take it (health at 100, a key already held), it gives what it gives, plays its sound,
/// shows its message and flash, counts as treasure, and goes -- or in deathmatch stays (a
/// weapon) or comes back later.
/// </summary>
internal sealed class Pickups(
    MapManager map,
    VideoManager video,
    HudMessageManager hudMessages,
    PlayerClassRules classes,
    PlayerStats stats,
    GameSession session,
    PickupHooks hooks)
{
    internal void GetBonus(Inventory builtActor)
    {
        if ((session.PlayState == PlayState.Died))
            return;

        if (builtActor.Properties.Count == 0)
            return;

        // An item for other player classes stays on the floor
        if (!classes.PlayerClassCanPickUp(builtActor.Name))
            return;

        // Deathmatch: a weapon stays where it is, so it's only for a player without it yet
        if (session.GameMode.KeepsItems() && builtActor is Weapon weaponPickup && hooks.HasWeaponFrom(weaponPickup))
            return;

        // An item the player can't take (health at 100, a key already held) stays on the
        // floor, unless it's flagged ALWAYSPICKUP.
        int? given = null;
        if (builtActor.Properties.TryGetValue("inventory.amount", out var amount))
        {
            given = PickupAmount(builtActor, Convert.ToInt32(amount));
            if (!stats.TryApplyInventory(builtActor, given.Value) && !builtActor.HasFlag("INVENTORY.ALWAYSPICKUP"))
                return;
        }

        if (builtActor.HasFlag("COUNTITEM"))
            session.GameState.TreasureCount++;

        builtActor.RunState("Pickup");

        if (builtActor.Properties.TryGetValue("inventory.pickupsound", out var pickupSound))
        {
            hooks.PlayPlayerSound(pickupSound?.ToString() ?? "");
            stats.GrinAtPickup(builtActor, pickupSound?.ToString());
        }

        // Shown over the view in the item's message style (hud-messages.yaml), or game-info's for
        // pickups; %a in it is how much it gave
        if (builtActor.Properties.TryGetValue("inventory.pickupmessage", out var pickupMessage))
        {
            builtActor.Properties.TryGetValue("inventory.pickupmessagestyle", out var pickupMessageStyle);
            if (session.ActingIsLocal)
                hudMessages.Show(HudMessageKind.Pickup, pickupMessage?.ToString(), pickupMessageStyle?.ToString(),
                    given is { } count ? new Dictionary<char, string> { ['a'] = count.ToString() } : null);
        }

        if (session.ActingIsLocal)
            video.StartBonusFlash();

        hooks.Taken?.Invoke(builtActor);

        // Deathmatch (Gameplay/Deathmatch.cs): a weapon stays for everyone else, and anything
        // else comes back a while after it's taken
        if (session.GameMode.KeepsItems() && builtActor is Weapon)
            return;
        if (session.GameMode.KeepsItems() && hooks.ItemRespawn())
            hooks.QueueItemRespawn(builtActor);

        //check.ShapeNum = "";                   // remove from list
        map.RemoveActor(builtActor);
    }

    /// <summary>
    /// How much an item gives: its `inventory.randomamount` [min, max] when it has one (Blake
    /// Stone's charge pack, 1 to 8 units), from the game's random table; else its amount
    /// </summary>
    internal int PickupAmount(Inventory item, int amount)
    {
        var bounds = item.PropertyInts("inventory.randomamount");
        if (bounds.Count != 2 || bounds[1] < bounds[0])
            return amount;
        // One roll spread across the range, as bstone's 1 + (US_RndT() & 7) for 1 to 8
        int span = bounds[1] - bounds[0] + 1;
        return bounds[0] + session.Random.RndT() * span / 256;
    }
}
