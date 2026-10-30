using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>What the player's weapons need from the game</summary>
/// <param name="UseSlotItem">An empty slot's key: uses an item held for that slot (Planet Strike's fission detonator)</param>
internal sealed record PlayerWeaponsHooks(
    Action<int> UseSlotItem);

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
    private static readonly ButtonType[] SlotButtons =
    [
        ButtonType.Slot0, ButtonType.Slot1, ButtonType.Slot2, ButtonType.Slot3, ButtonType.Slot4,
        ButtonType.Slot5, ButtonType.Slot6, ButtonType.Slot7, ButtonType.Slot8, ButtonType.Slot9,
    ];

    // Only weapons with ammo can be switched to. Out of ammo for the picked weapon, the player
    // keeps the fallback it forced (the knife) and the pick stands, so the weapon comes back
    // when ammo turns up -- unless they switch to another weapon that still has ammo.
    internal void CheckWeaponChange()
    {
        // Not while reloading, manning a mounted gun or with something latched on, as the original
        // Midnight Assault's CheckWeaponChange, nor riding a vehicle with a weapon of its own
        if (WeaponInHand.IsReloading(session.PlayerState.WeaponSprite) || session.PlayerState.Mount != null
            || session.PlayerState.Latch != null || session.PlayerState.VehicleGun != null)
            return;

        var usable = OwnedWeapons().Where(CanFire).ToList();
        if (!CanFire(session.PlayerState.ChosenWeapon) && session.PlayerState.Weapon != null)
            usable.Remove(session.PlayerState.Weapon);
        if (usable.Count == 0)
            return;

        string? newWeapon = null;
        var current = session.PlayerState.Weapon != null ? usable.IndexOf(session.PlayerState.Weapon) : -1;

        if (session.PlayerInput.IsFreshPress(ButtonType.NextWeapon))
        {
            newWeapon = usable[(current + 1) % usable.Count];
        }
        else if (session.PlayerInput.IsFreshPress(ButtonType.PrevWeapon))
        {
            newWeapon = usable[current <= 0 ? usable.Count - 1 : current - 1];
        }
        else
        {
            for (var slot = 0; slot < SlotButtons.Length; slot++)
            {
                if (!session.PlayerInput.IsFreshPress(SlotButtons[slot]))
                    continue;

                // The best weapon with ammo in that slot; pressed again with one of the slot's
                // weapons already in hand, the next one in the slot.
                var inSlot = usable.Where(w => WeaponSlot(w) == slot).ToList();
                if (inSlot.Count == 0)
                {
                    hooks.UseSlotItem(slot);      // Planet Strike's fission detonator (Gameplay/PlayerUse.cs)
                    break;
                }
                var inHand = session.PlayerState.Weapon != null ? inSlot.IndexOf(session.PlayerState.Weapon) : -1;
                newWeapon = inHand >= 0
                    ? inSlot[(inHand + 1) % inSlot.Count]
                    : inSlot.OrderBy(WeaponSelectionOrder).First();
                if (newWeapon == session.PlayerState.Weapon)
                    newWeapon = null;
                break;
            }
        }

        if (newWeapon != null)
        {
            session.PlayerState.Weapon = session.PlayerState.ChosenWeapon = newWeapon;
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

    /// <summary>
    /// The item a weapon's loaded rounds are held as (`weapon.magazine`), or null for one that
    /// shoots straight from its ammo. The magazine's `inventory.maxamount` is the clip size; A_Reload
    /// fills it from the weapon's ammo (`weapon.ammotype1`), and shots come out of it.
    /// </summary>
    internal string? WeaponMagazine(string? weapon) =>
        WeaponAmmoType(weapon) != null && inventory.GetStringProperty(weapon!, "weapon.magazine") is { Length: > 0 } magazine
            ? magazine
            : null;

    int ShotCost(string weapon) => inventory.GetIntProperty(weapon, "weapon.ammouse1", 0);

    /// <summary>
    /// Whether the player has the ammo for one shot of <paramref name="weapon"/>: for one with a
    /// magazine, loaded or still to load (it can be taken up, and reloads before it shoots).
    /// </summary>
    internal bool CanFire(string? weapon)
    {
        if (weapon == null)
            return false;
        var ammoType = WeaponAmmoType(weapon);
        return ammoType == null
            || inventory.GetCount(ammoType) >= ShotCost(weapon)
            || (WeaponMagazine(weapon) is { } magazine && inventory.GetCount(magazine) >= ShotCost(weapon));
    }

    /// <summary>Whether a shot of <paramref name="weapon"/> is ready to go: loaded in its magazine, for one with a magazine</summary>
    internal bool IsLoaded(string? weapon) =>
        WeaponMagazine(weapon) is { } magazine ? inventory.GetCount(magazine) >= ShotCost(weapon!) : CanFire(weapon);

    /// <summary>Whether <paramref name="weapon"/>'s magazine has room and there's ammo to put in it</summary>
    internal bool CanReload(string? weapon) =>
        WeaponMagazine(weapon) is { } magazine && !inventory.IsFull(magazine) && inventory.GetCount(WeaponAmmoType(weapon)!) > 0;

    /// <summary>
    /// Moves up to <paramref name="rounds"/> (all that fit when null) from the weapon in hand's
    /// ammo into its magazine; returns how many went in.
    /// </summary>
    internal int Reload(int? rounds = null)
    {
        var weapon = session.PlayerState.Weapon;
        if (WeaponMagazine(weapon) is not { } magazine)
            return 0;
        var ammoType = WeaponAmmoType(weapon)!;
        int room = inventory.GetMaxAmount(magazine) - inventory.GetCount(magazine);
        int moved = inventory.Give(magazine, Math.Min(Math.Min(rounds ?? room, room), inventory.GetCount(ammoType)));
        inventory.Take(ammoType, moved);
        DrawAmmo();
        return moved;
    }

    /// <summary>Takes one shot's ammo for the weapon in hand, from its magazine if it has one (unless the ammo cheat is on).</summary>
    internal void UseAmmo()
    {
        var weapon = session.PlayerState.Weapon;
        var ammoType = WeaponAmmoType(weapon);
        if (ammoType != null && !(session.AmmoCheat != 0))
            inventory.Take(WeaponMagazine(weapon) ?? ammoType, ShotCost(weapon!));
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
        WeaponAmmoType(session.PlayerState.ChosenWeapon)
        ?? OwnedWeapons().OrderBy(WeaponSelectionOrder).Select(WeaponAmmoType).FirstOrDefault(t => t != null);

    internal int GetAmmo() => DisplayAmmoType() is { } type ? inventory.GetCount(type) : 0;

    internal void DrawAmmo()
    {
        if (statusBar.StatusBarHidden) return;
        // only-with-ammo: nothing while the weapon in hand needs none (Blake Stone's auto charge pistol)
        bool hide = statusBar.StatusBar.Get("ammo")?.OnlyWithAmmo == true && WeaponAmmoType(session.PlayerState.Weapon) == null;
        // item: what to count while the weapon picked uses none (Planet Strike's charge units)
        int ammo = WeaponAmmoType(session.PlayerState.ChosenWeapon) == null && statusBar.StatusBar.Get("ammo")?.Item is { Length: > 0 } item
            ? inventory.GetCount(item)
            : GetAmmo();
        statusBar.LatchNumber("ammo", ammo, hide);
        // clip: the rounds loaded in the weapon in hand's magazine; nothing for one without
        statusBar.LatchNumber("clip", WeaponMagazine(session.PlayerState.Weapon) is { } magazine ? inventory.GetCount(magazine) : 0,
            hide: WeaponMagazine(session.PlayerState.Weapon) == null);
        statusBar.DrawCounts();         // a carried item's count (medkits) changes with ammo's
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
        statusBar.DrawWeaponName();
        DrawWeaponSlots();
        if (statusBar.StatusBar.Get("ammo")?.Behind?.Equals("weapon", StringComparison.OrdinalIgnoreCase) == true
            || statusBar.StatusBar.Get("ammo")?.OnlyWithAmmo == true || statusBar.StatusBar.Get("ammo-gauge") != null
            || statusBar.StatusBar.Get("clip") != null)
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
    // false only when the player already owned the weapon and had no room for its ammo. A
    // weapon with a magazine comes loaded: an empty magazine takes what it gives first (the
    // original Midnight Assault's GiveWeapon), the rest going to its ammo.
    internal bool TryGiveWeapon(string weaponName, int ammoGive)
    {
        var oldBest = BestWeapon();

        var ammoType = WeaponAmmoType(weaponName);
        int loaded = ammoGive > 0 && WeaponMagazine(weaponName) is { } magazine && !inventory.Has(magazine)
            ? inventory.Give(magazine, ammoGive)
            : 0;
        int rest = ammoGive - loaded;
        var gotAmmo = loaded > 0 | (rest > 0 && ammoType != null && GiveAmmo(ammoType, rest) > 0);
        var gotWeapon = inventory.Give(weaponName, 1) > 0;

        var newBest = BestWeapon();
        // (not while a power-up holds a weapon in hand: End of Destiny's Knife Fury)
        if (newBest != null && newBest != oldBest && !Powerups.HoldsAWeapon(inventory))
            session.PlayerState.Weapon = session.PlayerState.ChosenWeapon = newBest;

        DrawWeapon();
        DrawAmmo();
        return gotWeapon || gotAmmo;
    }

    /// <summary>
    /// How the weapon in hand bobs: weapon.bobstyle (null when it has none, or names no style,
    /// which is warned of once), and weapon.bobrangex, bobrangey and bobspeed, 1 when left out
    /// </summary>
    internal WeaponBobShape BobShape(string? weapon)
    {
        if (weapon == null)
            return WeaponBobShape.Default;

        var name = inventory.GetStringProperty(weapon, "weapon.bobstyle");
        var style = WeaponBobShape.ParseStyle(name);
        if (style == null && name != null && warnedBobStyles.Add(weapon))
            WarningLog.Write($"Actor {weapon}: weapon.bobstyle {name} isn't a bob style (spring, normal, inverse, alpha, inversealpha, smooth, inversesmooth or none)");

        return new(style, BobNumber(weapon, "weapon.bobrangex"), BobNumber(weapon, "weapon.bobrangey"), BobNumber(weapon, "weapon.bobspeed"));
    }

    private readonly HashSet<string> warnedBobStyles = new(StringComparer.OrdinalIgnoreCase);

    private double BobNumber(string weapon, string key) =>
        double.TryParse(inventory.GetStringProperty(weapon, key), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 1;

    // The charge (PlayerState.WeaponCharge): tics until the acting player's weapon is charged again; 0 when it's ready
    // (weapon.chargetics)

    /// <summary>The weapon in hand's `weapon.chargetics`: tics to charge between shots, 0 for none</summary>
    internal int WeaponChargeTics(string? weapon) =>
        weapon == null ? 0 : inventory.GetIntProperty(weapon, "weapon.chargetics", 0);

    /// <summary>Whether the weapon in hand charges between shots and isn't charged yet (its WAIT light)</summary>
    internal bool IsCharging() =>
        session.PlayerState.WeaponCharge > 0 && WeaponChargeTics(session.PlayerState.Weapon) > 0;

    /// <summary>
    /// Whether the weapon in hand can shoot now as far as its charge goes; if it can and it
    /// charges, starts the next charge (the shot empties it)
    /// </summary>
    internal bool TakeCharge()
    {
        int charge = WeaponChargeTics(session.PlayerState.Weapon);
        if (charge <= 0)
            return true;
        if (session.PlayerState.WeaponCharge > 0)
            return false;
        session.PlayerState.WeaponCharge = charge;
        DrawCharge();
        return true;
    }

    /// <summary>Counts the weapon's charge down, redrawing the status bar's charge light once it's ready</summary>
    internal void TickWeaponCharge()
    {
        if (session.PlayerState.WeaponCharge <= 0)
            return;
        session.PlayerState.WeaponCharge = Math.Max(session.PlayerState.WeaponCharge - (int)session.Tics, 0);
        if (session.PlayerState.WeaponCharge == 0)
            DrawCharge();
    }

    /// <summary>
    /// The charge light (statusbar.yaml charge): for a weapon that charges (weapon.chargetics),
    /// its ready picture once charged, else its wait picture
    /// </summary>
    internal void DrawCharge()
    {
        if (statusBar.StatusBarHidden) return;
        if (statusBar.StatusBar.Get("charge") is not { } element || WeaponChargeTics(session.PlayerState.Weapon) <= 0)
            return;
        var pic = session.PlayerState.WeaponCharge > 0 ? element.Wait : element.Ready;
        using var _ = statusBar.StatusBarOrigin(element);
        if (!string.IsNullOrEmpty(pic))
            graphics.DrawPic(pic, element.X, statusBar.StatusBarTop(element) + element.Y);
    }

    /// <summary>
    /// The weapon slot row (statusbar.yaml weapon-slots, Midnight Assault's): the slot numbers 1
    /// up to its slots, each followed by a space, in its colors: current for the weapon in hand's
    /// slot, held for a slot with a weapon held, else empty
    /// </summary>
    internal void DrawWeaponSlots()
    {
        if (statusBar.StatusBarHidden) return;
        if (statusBar.StatusBar.Get("weapon-slots") is not { } element)
            return;
        var font = element.Font ?? statusBar.StatusBar.Get("numbers")?.Font;
        if (string.IsNullOrEmpty(font))
            return;

        statusBar.ClearBox(element);
        var held = OwnedWeapons().Select(WeaponSlot).ToHashSet();
        int current = session.PlayerState.Weapon is { } weapon ? WeaponSlot(weapon) : -1;
        var x = element.X;
        for (int slot = 1; slot <= element.Slots; slot++)
        {
            string color = slot == current ? element.Colors.GetValueOrDefault("current", "White")
                : held.Contains(slot) ? element.Colors.GetValueOrDefault("held", "Grey")
                : element.Colors.GetValueOrDefault("empty", "DarkGrey");
            string text = $"{slot} ";
            using (statusBar.StatusBarOrigin(element))
                graphics.DrawText(x, statusBar.StatusBarTop(element) + element.Y, text, new Fonts.TextStyle(font, color));
            x += statusBar.TextWidth(text, font);
        }
    }

    /// <summary>
    /// The ammo gauge (statusbar.yaml ammo-gauge): a strip of segments, lit from the bottom for
    /// how full the weapon in hand's ammo is (any ammo at all lights one), colored by its levels
    /// for how many are unlit. Not drawn for a weapon that uses no ammo.
    /// </summary>
    internal void DrawAmmoGauge()
    {
        if (statusBar.StatusBarHidden) return;
        if (statusBar.StatusBar.Get("ammo-gauge") is not { } gauge || WeaponAmmoType(session.PlayerState.Weapon) is not { } ammoType)
            return;
        statusBar.DrawGauge(gauge, ammoType);
    }
}
