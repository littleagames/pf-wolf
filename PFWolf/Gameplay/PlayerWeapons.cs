using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>What the player's weapons need from the game</summary>
/// <param name="AmmoCheat">Whether shots cost no ammo (the ammo cheat)</param>
/// <param name="UseSlotItem">An empty slot's key: uses an item held for that slot (Planet Strike's fission detonator)</param>
internal sealed record PlayerWeaponsHooks(Func<bool> AmmoCheat, Action<int> UseSlotItem);

/// <summary>
/// The acting player's weapons as inventory: picking one by slot or next/previous, what a shot
/// costs, the best one held, giving weapons and ammo, a weapon's charge, and the status bar's
/// weapon, ammo and charge.
///
/// Everything about a weapon comes from its actordefs class (weapons.yaml): `weapon.slot`
/// and `weapon.selectionorder` (lower is better) order and rank them, `weapon.ammotype1`/
/// `weapon.ammouse1` say what a shot costs, `attacksound` and `inventory.icon` are its
/// sound and status bar pic, and its Ready state's sprite is drawn in the view.
/// </summary>
internal sealed class PlayerWeapons(
    InventoryManager inventory,
    GraphicManager graphics,
    StatusBarDrawing statusBar,
    GameSession session,
    PlayerWeaponsHooks hooks)
{
    // The weapon slot keys, indexed by slot number (`weapon.slot` 0-9, 1-4 for Wolf3D's four).
    private static readonly buttontypes[] SlotButtons =
    [
        buttontypes.bt_slot0, buttontypes.bt_slot1, buttontypes.bt_slot2, buttontypes.bt_slot3, buttontypes.bt_slot4,
        buttontypes.bt_slot5, buttontypes.bt_slot6, buttontypes.bt_slot7, buttontypes.bt_slot8, buttontypes.bt_slot9,
    ];

    // Only weapons with ammo can be switched to. Out of ammo for the picked weapon, the player
    // keeps the fallback it forced (the knife) and the pick stands, so the weapon comes back
    // when ammo turns up -- unless they switch to another weapon that still has ammo.
    internal void CheckWeaponChange()
    {
        var usable = OwnedWeapons().Where(CanFire).ToList();
        if (!CanFire(session.playerstate.chosenweapon) && session.playerstate.weapon != null)
            usable.Remove(session.playerstate.weapon);
        if (usable.Count == 0)
            return;

        string? newWeapon = null;
        var current = session.playerstate.weapon != null ? usable.IndexOf(session.playerstate.weapon) : -1;

        if (session.playerinput.IsFreshPress(buttontypes.bt_nextweapon))
        {
            newWeapon = usable[(current + 1) % usable.Count];
        }
        else if (session.playerinput.IsFreshPress(buttontypes.bt_prevweapon))
        {
            newWeapon = usable[current <= 0 ? usable.Count - 1 : current - 1];
        }
        else
        {
            for (var slot = 0; slot < SlotButtons.Length; slot++)
            {
                if (!session.playerinput.IsFreshPress(SlotButtons[slot]))
                    continue;

                // The best weapon with ammo in that slot; pressed again with one of the slot's
                // weapons already in hand, the next one in the slot.
                var inSlot = usable.Where(w => WeaponSlot(w) == slot).ToList();
                if (inSlot.Count == 0)
                {
                    hooks.UseSlotItem(slot);      // Planet Strike's fission detonator (GameHost.Teleporter.cs)
                    break;
                }
                var inHand = session.playerstate.weapon != null ? inSlot.IndexOf(session.playerstate.weapon) : -1;
                newWeapon = inHand >= 0
                    ? inSlot[(inHand + 1) % inSlot.Count]
                    : inSlot.OrderBy(WeaponSelectionOrder).First();
                if (newWeapon == session.playerstate.weapon)
                    newWeapon = null;
                break;
            }
        }

        if (newWeapon != null)
        {
            session.playerstate.weapon = session.playerstate.chosenweapon = newWeapon;
            DrawWeapon();
        }
    }

    // The slot key (1-9, 0) that picks a weapon; -1 for one with no `weapon.slot`, which only
    // next/previous weapon reach.
    internal int WeaponSlot(string weapon) => inventory.GetIntProperty(weapon, "weapon.slot", -1);

    // Slot order for cycling, as on the keyboard: 1-9, then 0, then weapons with no slot.
    int WeaponSlotOrder(string weapon) => WeaponSlot(weapon) switch { 0 => 10, < 0 => 11, var slot => slot };

    internal int WeaponSelectionOrder(string weapon) =>
        inventory.GetIntProperty(weapon, "weapon.selectionorder", int.MaxValue);

    /// <summary>The ammo a weapon shoots, or null for one that needs none (the knife).</summary>
    internal string? WeaponAmmoType(string? weapon)
    {
        if (weapon == null || inventory.GetIntProperty(weapon, "weapon.ammouse1", 0) <= 0)
            return null;
        var type = inventory.GetStringProperty(weapon, "weapon.ammotype1");
        return string.IsNullOrEmpty(type) || type.Equals("None", StringComparison.OrdinalIgnoreCase) ? null : type;
    }

    /// <summary>Whether the player has the ammo for one shot of <paramref name="weapon"/>.</summary>
    internal bool CanFire(string? weapon)
    {
        if (weapon == null)
            return false;
        var ammoType = WeaponAmmoType(weapon);
        return ammoType == null
            || inventory.GetCount(ammoType) >= inventory.GetIntProperty(weapon, "weapon.ammouse1", 0);
    }

    /// <summary>Takes one shot's ammo for the weapon in hand (unless the ammo cheat is on).</summary>
    internal void UseAmmo()
    {
        var ammoType = WeaponAmmoType(session.playerstate.weapon);
        if (ammoType != null && !hooks.AmmoCheat())
            inventory.Take(ammoType, inventory.GetIntProperty(session.playerstate.weapon!, "weapon.ammouse1", 0));
        DrawAmmo();
    }

    /// <summary>The held weapons, in slot order and worst to best within a slot.</summary>
    internal List<string> OwnedWeapons() =>
        inventory.Items.Keys
            .Where(item => inventory.FindClass(item, "Weapon") != null
                && inventory.FindClass(item, "WeaponGiver") == null)
            .OrderBy(WeaponSlotOrder)
            .ThenByDescending(WeaponSelectionOrder)
            .ToList();

    /// <summary>The best held weapon (lowest selectionorder), optionally only among those with ammo.</summary>
    internal string? BestWeapon(bool canFire = false) =>
        OwnedWeapons()
            .Where(w => !canFire || CanFire(w))
            .OrderBy(WeaponSelectionOrder)
            .FirstOrDefault();

    // The status bar shows the ammo of the weapon the player picked, even while out of ammo
    // forces a fallback; with an ammo-less weapon picked, the ammo of the best one that has some.
    string? DisplayAmmoType() =>
        WeaponAmmoType(session.playerstate.chosenweapon)
        ?? OwnedWeapons().OrderBy(WeaponSelectionOrder).Select(WeaponAmmoType).FirstOrDefault(t => t != null);

    internal int GetAmmo() => DisplayAmmoType() is { } type ? inventory.GetCount(type) : 0;

    internal void DrawAmmo()
    {
        if (statusBar.StatusBarHidden) return;
        // only-with-ammo: nothing while the weapon in hand needs none (Blake Stone's auto charge pistol)
        bool hide = statusBar.StatusBar.Get("ammo")?.OnlyWithAmmo == true && WeaponAmmoType(session.playerstate.weapon) == null;
        // item: what to count while the weapon picked uses none (Planet Strike's charge units)
        int ammo = WeaponAmmoType(session.playerstate.chosenweapon) == null && statusBar.StatusBar.Get("ammo")?.Item is { Length: > 0 } item
            ? inventory.GetCount(item)
            : GetAmmo();
        statusBar.LatchNumber("ammo", ammo, hide);
        DrawAmmoGauge();
    }

    /// <summary>Gives <paramref name="amount"/> of every ammo type; returns how much was taken in all.</summary>
    internal int GiveAllAmmo(int amount) =>
        inventory.GetClassesDerivedFrom("Ammo")
            .Select(inventory.GetItemType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Sum(type => GiveAmmo(type, amount));

/*
===============
=
= GiveAmmo
=
= Returns how much was actually taken (0 when the stack was already full)
=
===============
*/

    internal int GiveAmmo(string ammoType, int ammo)
    {
        // Out of ammo had forced a fallback (the knife): A_WeaponReady takes the picked weapon
        // back up once the fallback is idle again.
        var added = inventory.Give(ammoType, ammo);
        DrawAmmo();
        return added;
    }

    // The weapon in hand's picture, and what goes with it: its ammo (drawn over it, for a layout
    // that puts it there) and its charge
    internal void DrawWeapon()
    {
        if (statusBar.StatusBarHidden) return;
        statusBar.DrawWeaponPic();
        if (statusBar.StatusBar.Get("ammo")?.Behind?.Equals("weapon", StringComparison.OrdinalIgnoreCase) == true
            || statusBar.StatusBar.Get("ammo")?.OnlyWithAmmo == true || statusBar.StatusBar.Get("ammo-gauge") != null)
            DrawAmmo();
        DrawCharge();
    }

    /// <summary>
    /// Every weapon in actordefs that can be held, as held item types: a Blue reskin counts as
    /// its base weapon, and base classes with no Ready state (WolfWeapon) and WeaponGivers are
    /// left out.
    /// </summary>
    internal List<string> AllWeapons() =>
        inventory.GetClassesDerivedFrom("Weapon")
            .Where(w => inventory.FindClass(w, "WeaponGiver") == null
                && inventory.CreateActor(w)?.ResolvedStates.ContainsKey(WeaponInHand.WeaponReadyState) == true)
            .Select(inventory.GetItemType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Gives a weapon by class name with a pickup's worth (6) of its ammo.</summary>
    internal void GiveWeapon(string weapon) =>
        TryGiveWeapon(weapon, 6);

    internal bool TryGiveWeapon(Entities.Actors.Weapon pickup)
    {
        // A WeaponGiver (e.g. the map's gatling upgrade) names the weapon it hands out.
        var weaponName = pickup.Properties.TryGetValue("weapongiver.weapon", out var given)
            ? given.ToString() ?? pickup.Name
            : pickup.Name;
        var ammoGive = pickup.Properties.TryGetValue("weapon.ammogive1", out var ammo)
            ? Convert.ToInt32(ammo)
            : 0;

        return TryGiveWeapon(weaponName, ammoGive);
    }

    // Weapon pickups always come with ammo (vanilla GiveWeapon gave 6) of the weapon's own
    // type, and a weapon that's better than anything held becomes the selected one. Returns
    // false only when the player already owned the weapon and had no room for its ammo.
    internal bool TryGiveWeapon(string weaponName, int ammoGive)
    {
        var oldBest = BestWeapon();

        var ammoType = WeaponAmmoType(weaponName);
        var gotAmmo = ammoGive > 0 && ammoType != null && GiveAmmo(ammoType, ammoGive) > 0;
        var gotWeapon = inventory.Give(weaponName, 1) > 0;

        var newBest = BestWeapon();
        if (newBest != null && newBest != oldBest)
            session.playerstate.weapon = session.playerstate.chosenweapon = newBest;

        DrawWeapon();
        DrawAmmo();
        return gotWeapon || gotAmmo;
    }

    // The charge (PlayerState.WeaponCharge): tics until the acting player's weapon is charged again; 0 when it's ready
    // (weapon.chargetics)

    /// <summary>The weapon in hand's `weapon.chargetics`: tics to charge between shots, 0 for none</summary>
    internal int WeaponChargeTics(string? weapon) =>
        weapon == null ? 0 : inventory.GetIntProperty(weapon, "weapon.chargetics", 0);

    /// <summary>
    /// Whether the weapon in hand can shoot now as far as its charge goes; if it can and it
    /// charges, starts the next charge (the shot empties it)
    /// </summary>
    internal bool TakeCharge()
    {
        int charge = WeaponChargeTics(session.playerstate.weapon);
        if (charge <= 0)
            return true;
        if (session.playerstate.WeaponCharge > 0)
            return false;
        session.playerstate.WeaponCharge = charge;
        DrawCharge();
        return true;
    }

    /// <summary>Counts the weapon's charge down, redrawing the status bar's charge light once it's ready</summary>
    internal void TickWeaponCharge()
    {
        if (session.playerstate.WeaponCharge <= 0)
            return;
        session.playerstate.WeaponCharge = Math.Max(session.playerstate.WeaponCharge - (int)session.tics, 0);
        if (session.playerstate.WeaponCharge == 0)
            DrawCharge();
    }

    /// <summary>
    /// The charge light (statusbar.yaml charge): for a weapon that charges (weapon.chargetics),
    /// its ready picture once charged, else its wait picture
    /// </summary>
    internal void DrawCharge()
    {
        if (statusBar.StatusBarHidden) return;
        if (statusBar.StatusBar.Get("charge") is not { } element || WeaponChargeTics(session.playerstate.weapon) <= 0)
            return;
        var pic = session.playerstate.WeaponCharge > 0 ? element.Wait : element.Ready;
        using var _ = statusBar.StatusBarOrigin(element);
        if (!string.IsNullOrEmpty(pic))
            graphics.DrawPic(pic, element.X, statusBar.StatusBarTop(element) + element.Y);
    }

    /// <summary>
    /// The ammo gauge (statusbar.yaml ammo-gauge): a strip of segments, lit from the bottom for
    /// how full the weapon in hand's ammo is (any ammo at all lights one), colored by its levels
    /// for how many are unlit. Not drawn for a weapon that uses no ammo.
    /// </summary>
    internal void DrawAmmoGauge()
    {
        if (statusBar.StatusBarHidden) return;
        if (statusBar.StatusBar.Get("ammo-gauge") is not { } gauge || WeaponAmmoType(session.playerstate.weapon) is not { } ammoType)
            return;
        statusBar.DrawGauge(gauge, ammoType);
    }
}
