namespace Wolf3D;

// The weapon in hand, animated from its actordefs states (weapons.yaml) the way ZDoom runs a
// player sprite: Ready loops on A_WeaponReady until fire is pressed, Fire (and Hold) play the
// attack, and the attack actions (A_GunAttack, A_CustomPunch, A_ReFire) run as their frames
// end. It's an ordinary Entities.Actors.Actor of the weapon class, ticked by MapManager.DoActor
// but never placed in the world. Switching weapons is instant, as in Wolf3D, so the Select and
// Deselect states are never entered.
internal partial class Program
{
    internal const string WeaponReadyState = "Ready";

    static Entities.Actors.Actor? weaponSprite;

    /// <summary>
    /// Makes the weapon sprite match <c>gamestate.weapon</c>: a new weapon in hand starts on its
    /// Ready state (dropping whatever the old one was doing); no weapon means no sprite.
    /// </summary>
    static Entities.Actors.Actor? SyncWeaponSprite()
    {
        if (gamestate.weapon == null)
            return weaponSprite = null;

        if (weaponSprite != null && string.Equals(weaponSprite.Name, gamestate.weapon, StringComparison.OrdinalIgnoreCase))
            return weaponSprite;

        weaponSprite = _inventoryManager.CreateActor(gamestate.weapon);
        if (weaponSprite != null && weaponSprite.ResolvedStates.TryGetValue(WeaponReadyState, out var ready))
            weaponSprite.ArmState(ready);
        return weaponSprite;
    }

    /// <summary>Whether the weapon is idle on its Ready state (not mid-attack).</summary>
    static bool IsWeaponReady() =>
        SyncWeaponSprite()?.CurrentState?.StateName is null or WeaponReadyState;

    /// <summary>Runs the weapon's states for this tic's worth of time.</summary>
    static void TickWeapon()
    {
        if (SyncWeaponSprite() is { } sprite)
            _mapManager.DoActor(sprite, tics);
    }

    /// <summary>The shape to draw for the weapon in hand, or null for none.</summary>
    static string? WeaponShapeName() =>
        SyncWeaponSprite()?.CurrentState is { Sprite.Length: > 0 } state ? $"{state.Sprite}{state.FrameLetter}0" : null;

    /// <summary>Puts the weapon on the named state, if it has one; returns whether it did.</summary>
    static bool JumpWeaponState(Entities.Actors.Actor weapon, string stateName)
    {
        if (!weapon.ResolvedStates.TryGetValue(stateName, out var frame))
            return false;
        weapon.JumpTo(frame);
        return true;
    }

    static void RegisterWeaponActions()
    {
        Entities.Actors.ActorActionRegistry.Register("A_WeaponReady", A_WeaponReady);
        Entities.Actors.ActorActionRegistry.Register("A_GunAttack", A_GunAttack);
        Entities.Actors.ActorActionRegistry.Register("A_CustomPunch", A_CustomPunch);
        Entities.Actors.ActorActionRegistry.Register("A_ReFire", A_ReFire);
    }

    /// <summary>
    /// Ready's every-tic action. Out of ammo for the picked weapon, it falls back to the best
    /// one that can still fire (the knife), and takes the picked one back up once there's ammo
    /// again. Otherwise a fresh press of fire starts the Fire state.
    /// </summary>
    static void A_WeaponReady(Entities.Actors.Actor weapon)
    {
        var inHand = CanFire(gamestate.chosenweapon) ? gamestate.chosenweapon : BestWeapon(canFire: true);
        if (inHand != null && inHand != gamestate.weapon)
        {
            gamestate.weapon = inHand;      // SyncWeaponSprite swaps the sprite next time
            DrawWeapon();
            return;
        }

        if (_inputManager.IsButtonPressed(buttontypes.bt_attack) && !_inputManager.IsButtonHeld(buttontypes.bt_attack))
        {
            _inputManager.SetButtonHeld(buttontypes.bt_attack, true);
            JumpWeaponState(weapon, "Fire");
        }
    }

    /// <summary>One hitscan shot from the weapon in hand, if there's ammo for it.</summary>
    static void A_GunAttack(Entities.Actors.Actor weapon)
    {
        if (!CanFire(gamestate.weapon))
            return;
        GunAttack(player);
        UseAmmo();
    }

    // A_CustomPunch(damage, ...): the arguments aren't read yet; KnifeAttack deals vanilla's
    // US_RndT() >> 4 (0-15), which is what weapons.yaml asks for.
    static void A_CustomPunch(Entities.Actors.Actor weapon) => KnifeAttack(player);

    /// <summary>Fire still held and ammo left: back to Hold (or Fire, for a weapon without one).</summary>
    static void A_ReFire(Entities.Actors.Actor weapon)
    {
        if (!_inputManager.IsButtonPressed(buttontypes.bt_attack) || !CanFire(gamestate.weapon))
            return;
        if (!JumpWeaponState(weapon, "Hold"))
            JumpWeaponState(weapon, "Fire");
    }
}
