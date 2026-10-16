using PFWolf.Assets;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>
/// A map's starting loadout (game-info map `loadout:`, Midnight Assault's SetupMapSpecialFeatures):
/// as the level starts afresh, each player's weapons and ammo can be taken (what's loaded in a
/// magazine stays, as the original's clips did), items given, and health set or capped. A saved
/// game loads as it was.
/// </summary>
internal sealed class MapLoadouts(InventoryManager inventory, PlayerWeapons weapons, GameSession session)
{
    /// <summary>Gives every player the loadout (none: nothing changes)</summary>
    internal void Apply(MapLoadout? loadout)
    {
        if (loadout == null)
            return;
        foreach (var p in session.players)
        {
            using var _ = session.ActAs(p);
            ApplyToActing(loadout);
        }
    }

    void ApplyToActing(MapLoadout loadout)
    {
        var playerstate = session.playerstate;
        bool armed = false;

        if (loadout.TakeWeapons)
        {
            // The ammo some weapon fires (not a magazine, nor an Ammo item that's carried for
            // something else: Midnight Assault's medkits)
            var weaponClasses = inventory.GetClassesDerivedFrom("Weapon").ToList();
            var magazines = weaponClasses
                .Select(w => inventory.GetStringProperty(w, "weapon.magazine"))
                .OfType<string>().Where(m => m.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var fired = weaponClasses
                .SelectMany(w => new[] { inventory.GetStringProperty(w, "weapon.ammotype1"), inventory.GetStringProperty(w, "weapon.ammotype2") })
                .OfType<string>().Where(a => a.Length > 0 && !magazines.Contains(a))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var (item, count) in playerstate.Items.ToList())
            {
                bool weapon = inventory.FindClass(item, "Weapon") != null;
                bool ammo = !weapon && fired.Contains(item);
                if ((weapon || ammo) && count > 0)
                    inventory.Take(item, count);
            }
            armed = true;
        }

        foreach (var (item, amount) in loadout.Give)
        {
            if (inventory.FindClass(item, "Inventory") == null)
            {
                Console.WriteLine($"Map loadout: no item class {item}.");
                continue;
            }
            inventory.Give(item, amount);
            armed |= inventory.FindClass(item, "Weapon") != null;
        }

        if (armed)
        {
            playerstate.weapon = playerstate.chosenweapon = weapons.BestWeapon(canFire: true) ?? weapons.BestWeapon();
            playerstate.WeaponSprite = null;        // the weapon in hand starts on its Ready state
            playerstate.WeaponPower = 0;
        }

        if (loadout.Health is { } health)
            playerstate.health = (short)Math.Max(health, 1);
        if (loadout.HealthCap is { } cap && playerstate.health > cap)
            playerstate.health = (short)Math.Max(cap, 1);
    }
}
