using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>
/// The weapon in hand, animated from its actordefs states (weapons.yaml) the way ZDoom runs a
/// player sprite: Ready loops on A_WeaponReady until fire is pressed, Fire (and Hold) play the
/// attack, and the attack actions (A_GunAttack, A_CustomPunch, A_ReFire) run as their frames
/// end. It's an ordinary Entities.Actors.Actor of the weapon class, ticked by MapManager.DoActor
/// but never placed in the world. Switching weapons is instant, as in Wolf3D, so the Select and
/// Deselect states are never entered.
/// </summary>
internal sealed class WeaponInHand(
    InventoryManager inventory,
    MapManager map,
    GameSession session,
    PlayerWeapons weapons,
    PlayerAttacks attacks,
    Action registerMissileActions)
{
    internal const string WeaponReadyState = "Ready";

    // The weapon in hand's actor is the acting player's
    Entities.Actors.Actor? weaponSprite
    {
        get => session.playerstate.WeaponSprite;
        set => session.playerstate.WeaponSprite = value;
    }

    /// <summary>
    /// Makes the weapon sprite match <c>playerstate.weapon</c>: a new weapon in hand starts on its
    /// Ready state (dropping whatever the old one was doing); no weapon means no sprite.
    /// </summary>
    internal Entities.Actors.Actor? SyncWeaponSprite()
    {
        var playerstate = session.playerstate;
        if (playerstate.weapon == null)
            return weaponSprite = null;

        if (weaponSprite != null && string.Equals(weaponSprite.Name, playerstate.weapon, StringComparison.OrdinalIgnoreCase))
            return weaponSprite;

        var old = weaponSprite;
        weaponSprite = inventory.CreateActor(playerstate.weapon);
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
    internal static int? AttackStep(Entities.Actors.Actor weapon)
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
    internal static Entities.Actors.ActorStateFrame? AttackFrame(Entities.Actors.Actor weapon, int step)
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
    internal bool IsWeaponReady() =>
        SyncWeaponSprite()?.CurrentState?.StateName is null or WeaponReadyState;

    /// <summary>Runs the weapon's states for this tic's worth of time.</summary>
    internal void TickWeapon()
    {
        weapons.TickWeaponCharge();
        if (SyncWeaponSprite() is not { } sprite)
            return;

        // Fire cuts a reload short once a round is in (the original Midnight Assault's T_Reload),
        // so a shotgun loading shell by shell can shoot between them
        if (IsReloading(sprite) && weapons.IsLoaded(session.playerstate.weapon)
            && session.playerinput.IsFreshPress(ButtonType.bt_attack))
            StartState(sprite, "Fire", ButtonType.bt_attack);

        map.DoActor(sprite, session.tics);
    }

    /// <summary>
    /// Whether the weapon is reloading: on its Reload state, or one whose name starts with it
    /// (a ReloadLoop a shotgun goes round shell by shell)
    /// </summary>
    internal static bool IsReloading(Entities.Actors.Actor? weapon) =>
        weapon?.CurrentState?.StateName?.StartsWith(ReloadState, StringComparison.OrdinalIgnoreCase) == true;

    internal const string ReloadState = "Reload";

    // As Cmd_Fire: the state starts from its first frame's full tics, counted down from the next
    // tic on, not from what's left of this one (or a machine gun's check for fire still being held
    // comes a tic early); the button that started it is held until it's let go
    void StartState(Entities.Actors.Actor weapon, string state, ButtonType button)
    {
        session.playerinput.SetHeld(button, true);
        weapon.TicCount = 0;
        JumpWeaponState(weapon, state);
    }

    /// <summary>The shape to draw for the weapon in hand, or null for none.</summary>
    internal string? WeaponShapeName() =>
        SyncWeaponSprite()?.CurrentState is { Sprite.Length: > 0 } state ? $"{state.Sprite}{state.FrameLetter}0" : null;

    /// <summary>Puts the weapon on the named state, if it has one; returns whether it did.</summary>
    internal static bool JumpWeaponState(Entities.Actors.Actor weapon, string stateName)
    {
        if (!weapon.ResolvedStates.TryGetValue(stateName, out var frame))
            return false;
        weapon.JumpTo(frame);
        return true;
    }

    internal void Register()
    {
        Entities.Actors.ActorActionRegistry.Register("A_WeaponReady", A_WeaponReady);
        Entities.Actors.ActorActionRegistry.Register("A_GunAttack", A_GunAttack);
        Entities.Actors.ActorActionRegistry.Register("A_FireBullet", A_FireBullet);
        Entities.Actors.ActorActionRegistry.Register("A_HoldCharge", A_HoldCharge);
        Entities.Actors.ActorActionRegistry.Register("A_CustomPunch", A_CustomPunch);
        Entities.Actors.ActorActionRegistry.Register("A_ReFire", A_ReFire);
        Entities.Actors.ActorActionRegistry.Register("A_Reload", A_Reload);
        registerMissileActions();
    }

    /// <summary>
    /// Ready's every-tic action. Out of ammo for the picked weapon, it falls back to the best
    /// one that can still fire (the knife), and takes the picked one back up once there's ammo
    /// again. A weapon with a magazine goes to its Reload state once the magazine is empty, or on
    /// a press of reload while it isn't full, if there's ammo to load. Otherwise a fresh press of
    /// fire starts the Fire state.
    /// </summary>
    internal void A_WeaponReady(Entities.Actors.Actor weapon)
    {
        var playerstate = session.playerstate;
        var inHand = weapons.CanFire(playerstate.chosenweapon) ? playerstate.chosenweapon : weapons.BestWeapon(canFire: true);
        if (inHand != null && inHand != playerstate.weapon)
        {
            playerstate.weapon = inHand;
            weapons.DrawWeapon();

            // A press now fires the weapon taken up (as vanilla, where the knife came out as the
            // last attack ended), so carry on with its sprite rather than this one
            if (SyncWeaponSprite() is not { } taken)
                return;
            weapon = taken;
        }

        if (weapons.CanReload(playerstate.weapon) && weapon.ResolvedStates.ContainsKey(ReloadState)
            && (!weapons.IsLoaded(playerstate.weapon) || session.playerinput.IsFreshPress(ButtonType.bt_reload)))
        {
            StartState(weapon, ReloadState, ButtonType.bt_reload);
            return;
        }

        // Zoomed in through a scope, a weapon with a ZoomFire state fires that (Gameplay/WeaponZoom.cs)
        if (session.playerinput.IsFreshPress(ButtonType.bt_attack))
            StartState(weapon, playerstate.ZoomMode == ZoomMode.Zoomed && weapon.ResolvedStates.ContainsKey("ZoomFire") ? "ZoomFire" : "Fire",
                ButtonType.bt_attack);
    }

    /// <summary>
    /// A_Reload([rounds[, "state"]]): loads the weapon in hand's magazine from its ammo, all that
    /// fits or that many rounds. Given a state, it goes there while the magazine still has room
    /// and there's ammo left (a shotgun's shell-by-shell loop).
    /// </summary>
    internal void A_Reload(Entities.Actors.Actor weapon, string[] args)
    {
        int? rounds = args.Length > 0 && int.TryParse(args[0], out var n) && n > 0 ? n : null;
        weapons.Reload(rounds);
        if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) && weapons.CanReload(session.playerstate.weapon))
            JumpWeaponState(weapon, args[1]);
    }

    /// <summary>
    /// A_HoldCharge("State"[, max]): while fire is held, the throw cooks: the weapon's power
    /// (PlayerState.WeaponPower) goes up a tic's worth, to max (100), and the weapon goes back to
    /// the state, held there; let go, it carries on (Midnight Assault's grenade). A_FireMissile's
    /// "power" throws with it.
    /// </summary>
    internal void A_HoldCharge(Entities.Actors.Actor weapon, string[] args)
    {
        if (!session.playerinput.IsPressed(ButtonType.bt_attack) || args.Length == 0)
            return;
        int max = args.Length > 1 && int.TryParse(args[1], out var m) ? Math.Clamp(m, 1, 100) : 100;
        var playerstate = session.playerstate;
        playerstate.WeaponPower = Math.Min(playerstate.WeaponPower + (int)session.tics, max);
        JumpWeaponState(weapon, args[0]);
    }

    /// <summary>
    /// A_FireBullet(min[, max[, "silent"]]): one shot at the closest target in the weapon's
    /// sights, for min..max whatever the distance (Midnight Assault's FireBullet; its zoomed sniper
    /// shot); silent, it alerts no one. Ammo and charge as A_GunAttack.
    /// </summary>
    internal void A_FireBullet(Entities.Actors.Actor weapon, string[] args)
    {
        if (!weapons.IsLoaded(session.playerstate.weapon) || !weapons.TakeCharge())
            return;
        int min = args.Length > 0 && int.TryParse(args[0], out var a0) ? a0 : 0;
        int max = args.Length > 1 && int.TryParse(args[1], out var a1) ? a1 : min;
        bool silent = args.Skip(2).Any(a => a.Trim().Equals("silent", StringComparison.OrdinalIgnoreCase));
        attacks.FireBullet(min, max, silent);
        weapons.UseAmmo();
    }

    /// <summary>
    /// A_GunAttack([near, mid, far]): one hitscan shot from the weapon in hand, if there's ammo
    /// (and, for a weapon.chargetics weapon, charge) for it. A hit deals a random 0-255 divided by
    /// near within 2 tiles, mid within 4 and far beyond (Wolf3D's 4, 6, 6 when left out).
    /// </summary>
    internal void A_GunAttack(Entities.Actors.Actor weapon, string[] args)
    {
        if (!weapons.IsLoaded(session.playerstate.weapon) || !weapons.TakeCharge())
            return;
        int Divisor(int i, int fallback) => args.Length > i && int.TryParse(args[i], out var d) && d > 0 ? d : fallback;
        attacks.GunAttack(session.player, Divisor(0, 4), Divisor(1, 6), Divisor(2, 6));
        weapons.UseAmmo();
    }

    // A_CustomPunch(damage, ...): the arguments aren't read yet; KnifeAttack deals vanilla's
    // US_RndT() >> 4 (0-15), which is what weapons.yaml asks for.
    internal void A_CustomPunch(Entities.Actors.Actor weapon) => attacks.KnifeAttack(session.player);

    /// <summary>Fire still held and a round ready: back to Hold (or Fire, for a weapon without one).</summary>
    internal void A_ReFire(Entities.Actors.Actor weapon)
    {
        if (!session.playerinput.IsPressed(ButtonType.bt_attack) || !weapons.IsLoaded(session.playerstate.weapon))
            return;
        if (!JumpWeaponState(weapon, "Hold"))
            JumpWeaponState(weapon, "Fire");
    }
}
