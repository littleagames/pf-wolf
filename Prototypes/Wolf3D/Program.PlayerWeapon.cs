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

        var old = weaponSprite;
        weaponSprite = _inventoryManager.CreateActor(gamestate.weapon);
        if (weaponSprite == null)
            return null;

        // A weapon picked up mid-attack takes over the attack where it had got to, as vanilla's
        // T_Attack carried on through the new weapon's attackinfo; otherwise it starts Ready
        if (old != null && AttackStep(old) is { } step && AttackFrame(weaponSprite, step) is { } frame)
        {
            weaponSprite.CurrentState = frame;
            weaponSprite.TicCount = old.TicCount;
        }
        else if (weaponSprite.ResolvedStates.TryGetValue(WeaponReadyState, out var ready))
            weaponSprite.ArmState(ready);
        return weaponSprite;
    }

    /// <summary>How many timed frames into its attack (from Fire's first) a weapon is, or null when it isn't attacking</summary>
    static int? AttackStep(Entities.Actors.Actor weapon)
    {
        if (weapon.CurrentState is not { } current || current.StateName == WeaponReadyState
            || !weapon.ResolvedStates.TryGetValue("Fire", out var frame))
            return null;

        for (int step = 0, guard = 0; frame != null && guard < 32; frame = frame.Next, guard++)
        {
            if (ReferenceEquals(frame, current))
                return step;
            if (frame.TicTime != 0)
                step++;
            if (frame.StateName == WeaponReadyState)
                break;
        }
        return null;
    }

    /// <summary>The timed frame that many into a weapon's attack (AttackStep), if it gets that far</summary>
    static Entities.Actors.ActorStateFrame? AttackFrame(Entities.Actors.Actor weapon, int step)
    {
        if (!weapon.ResolvedStates.TryGetValue("Fire", out var frame))
            return null;

        for (int guard = 0; frame != null && guard < 32 && frame.StateName != WeaponReadyState; frame = frame.Next, guard++)
        {
            if (frame.TicTime == 0)
                continue;
            if (step-- == 0)
                return frame;
        }
        return null;
    }

    /// <summary>Whether the weapon is idle on its Ready state (not mid-attack).</summary>
    static bool IsWeaponReady() =>
        SyncWeaponSprite()?.CurrentState?.StateName is null or WeaponReadyState;

    /// <summary>Runs the weapon's states for this tic's worth of time.</summary>
    static void TickWeapon()
    {
        TickWeaponCharge();
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
        RegisterMissileActions();
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
            gamestate.weapon = inHand;
            DrawWeapon();

            // A press now fires the weapon taken up (as vanilla, where the knife came out as the
            // last attack ended), so carry on with its sprite rather than this one
            if (SyncWeaponSprite() is not { } taken)
                return;
            weapon = taken;
        }

        if (_inputManager.IsButtonPressed(buttontypes.bt_attack) && !_inputManager.IsButtonHeld(buttontypes.bt_attack))
        {
            _inputManager.SetButtonHeld(buttontypes.bt_attack, true);

            // As Cmd_Fire: the attack starts from its first frame's full tics, counted down from
            // the next tic on, not from what's left of this one (or a machine gun's check for
            // fire still being held comes a tic early)
            weapon.TicCount = 0;
            JumpWeaponState(weapon, "Fire");
        }
    }

    /// <summary>
    /// A_GunAttack([near, mid, far]): one hitscan shot from the weapon in hand, if there's ammo
    /// (and, for a weapon.chargetics weapon, charge) for it. A hit deals a random 0-255 divided by
    /// near within 2 tiles, mid within 4 and far beyond (Wolf3D's 4, 6, 6 when left out).
    /// </summary>
    static void A_GunAttack(Entities.Actors.Actor weapon, string[] args)
    {
        if (!CanFire(gamestate.weapon) || !TakeCharge())
            return;
        int Divisor(int i, int fallback) => args.Length > i && int.TryParse(args[i], out var d) && d > 0 ? d : fallback;
        GunAttack(player, Divisor(0, 4), Divisor(1, 6), Divisor(2, 6));
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
