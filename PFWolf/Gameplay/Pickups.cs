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
        if ((session.playstate == playstatetypes.ex_died))
            return;

        //if (string.IsNullOrWhiteSpace(check.item_class))
        //    return;

        //var actors = _assetManager.GetActorMetadata();
        //if (!actors.Actors.TryGetValue(check.item_class, out var actor))
        //    return;
        //var builtActor = actors.BuildActor(check.item_class, actor); // TODO: Should this just create objects?

        if (builtActor.Properties.Count == 0)
            return;

        // An item for other player classes stays on the floor
        if (!classes.PlayerClassCanPickUp(builtActor.Name))
            return;

        // Deathmatch: a weapon stays where it is, so it's only for a player without it yet
        if (session.gamemode.KeepsItems() && builtActor is Weapon weaponPickup && hooks.HasWeaponFrom(weaponPickup))
            return;

        // An item the player can't take (health at 100, a key already held) stays on the
        // floor, unless it's flagged ALWAYSPICKUP.
        if (builtActor.Properties.TryGetValue("inventory.amount", out var amount)
            && !stats.TryApplyInventory(builtActor, Convert.ToInt32(amount))
            && !builtActor.Flags.Contains("INVENTORY.ALWAYSPICKUP", StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (builtActor.Flags.Contains("COUNTITEM", StringComparer.OrdinalIgnoreCase))
            session.gamestate.treasurecount++;

        builtActor.RunState("Pickup");

        if (builtActor.Properties.TryGetValue("inventory.pickupsound", out var pickupSound))
        {
            hooks.PlayPlayerSound(pickupSound?.ToString() ?? "");
            stats.GrinAtPickup(builtActor, pickupSound?.ToString());
        }

        // Shown over the view in the item's message style (hud-messages.yaml), or game-info's for pickups
        if (builtActor.Properties.TryGetValue("inventory.pickupmessage", out var pickupMessage))
        {
            builtActor.Properties.TryGetValue("inventory.pickupmessagestyle", out var pickupMessageStyle);
            if (session.ActingIsLocal)
                hudMessages.Show(HudMessageKind.Pickup, pickupMessage?.ToString(), pickupMessageStyle?.ToString());
        }

        if (session.ActingIsLocal)
            video.StartBonusFlash();

        hooks.Taken?.Invoke(builtActor);

        // Deathmatch (Gameplay/Deathmatch.cs): a weapon stays for everyone else, and anything
        // else comes back a while after it's taken
        if (session.gamemode.KeepsItems() && builtActor is Weapon)
            return;
        if (session.gamemode.KeepsItems() && hooks.ItemRespawn())
            hooks.QueueItemRespawn(builtActor);

        //check.shapenum = "";                   // remove from list
        map.RemoveActor(builtActor);
    }
}
