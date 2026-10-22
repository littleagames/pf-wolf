using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/// <summary>
/// The weapon in hand, animated from its actordefs states (weapons.yaml) the way ZDoom runs a
/// player sprite: Ready loops on A_WeaponReady until fire is pressed, Fire (and Hold) play the
/// attack, and the attack actions (A_GunAttack, A_CustomPunch, A_ReFire) run as their frames
/// end. It's an ordinary Entities.Actors.Actor of the weapon class, ticked by MapManager.DoActor
/// but never placed in the world.
///
/// Switching weapons is instant, as in Wolf3D, unless the weapons say otherwise: one with a
/// Deselect state plays it first (A_Lower takes it down off the view), and one with a Select
/// state comes in on it (A_Raise brings it up, then on to Ready). While a weapon goes down or
/// comes up, fire does nothing and another switch waits, as in ZDoom.
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
    internal const string SelectState = "Select", DeselectState = "Deselect";

    /// <summary>
    /// How far down a weapon goes to be put away, in ZDoom's units (WEAPONBOTTOM less WEAPONTOP:
    /// 96, drawn as the weapon's full height): A_Lower and A_Raise move it 6 a tic unless told
    /// otherwise, so a weapon goes down or comes up in 16 tics
    /// </summary>
    internal const int LoweredFully = 96;

    /// <summary>A_Lower's and A_Raise's step when they're given none</summary>
    internal const int DefaultSwitchSpeed = 6;

    // The weapon in hand's actor is the acting player's
    Entities.Actors.Actor? WeaponSprite
    {
        get => session.PlayerState.WeaponSprite;
        set => session.PlayerState.WeaponSprite = value;
    }

    /// <summary>
    /// Makes the weapon sprite match <c>PlayerState.weapon</c>; no weapon means no sprite. A new
    /// weapon starts on its Ready state, unless the old one has a Deselect state, which it plays
    /// first, and then the new one comes in on its Select state if it has one. A weapon coming up
    /// gets all the way before a change is looked at. A first weapon (a new level, a loaded game)
    /// is simply up.
    /// </summary>
    internal Entities.Actors.Actor? SyncWeaponSprite()
    {
        var playerstate = session.PlayerState;
        if (playerstate.Weapon == null)
        {
            playerstate.WeaponLowered = 0;
            return WeaponSprite = null;
        }

        var old = WeaponSprite;
        if (old != null)
        {
            bool same = string.Equals(old.Name, playerstate.Weapon, StringComparison.OrdinalIgnoreCase);
            var state = old.CurrentState?.StateName;
            if (state == SelectState)
                return old;                     // coming up: finishes first
            if (state == DeselectState)
            {
                if (playerstate.WeaponLowered < LoweredFully)
                    return old;                 // still going down
                if (same)
                {
                    BringUp(old, armed: false); // taken back as it went: it comes up again
                    return old;
                }
            }
            else if (same)
                return old;
            else if (Switches(old, DeselectState, "A_Lower") && JumpWeaponState(old, DeselectState))
                return old;                     // goes on from where it is: 0, up, or as far as a Deselect it left got it
        }

        var sprite = WeaponSprite = inventory.CreateActor(playerstate.Weapon);
        if (sprite == null)
            return null;
        playerstate.WeaponLowered = 0;

        // A weapon picked up mid-attack takes over the attack where it had got to, as vanilla's
        // T_Attack carried on through the new weapon's attackinfo; otherwise it starts Ready
        if (old != null && Switches(sprite, SelectState, "A_Raise"))
            BringUp(sprite, armed: true);
        else if (old != null && old.CurrentState?.StateName != DeselectState
                 && AttackStep(old) is { } step && AttackFrame(sprite, step) is { } frame)
        {
            sprite.CurrentState = frame;
            sprite.TicCount = old.TicCount;
        }
        else if (sprite.ResolvedStates.TryGetValue(WeaponReadyState, out var ready))
            sprite.ArmState(ready);
        return sprite;
    }

    // A weapon starting up from out of sight: on its Select state, or Ready without a working one
    void BringUp(Entities.Actors.Actor weapon, bool armed)
    {
        var playerstate = session.PlayerState;
        bool select = Switches(weapon, SelectState, "A_Raise");
        var state = weapon.ResolvedStates.GetValueOrDefault(select ? SelectState : WeaponReadyState);
        playerstate.WeaponLowered = select ? LoweredFully : 0;
        if (state == null)
            return;
        if (armed)
            weapon.ArmState(state);
        else
            weapon.JumpTo(state);
    }

    /// <summary>
    /// Whether the weapon has a Deselect (or Select) state that runs A_Lower (A_Raise), so it goes
    /// down (comes up) and gets there. One without the action would hold the weapon there for good,
    /// so it's left out, the change made at once, with a warning (once a class and state).
    /// </summary>
    static bool Switches(Entities.Actors.Actor weapon, string state, string action)
    {
        if (!weapon.ResolvedStates.TryGetValue(state, out var frame))
            return false;
        for (int guard = 0; frame != null && frame.StateName == state && guard < 64; frame = frame.Next, guard++)
        {
            if (frame.Action is { Length: > 0 } call && Entities.Actors.ActorActionRegistry.Parse(call).Name == action)
                return true;
        }
        if (warnedSwitches.Add($"{weapon.Name}:{state}"))
            WarningLog.Write($"Weapon {weapon.Name}: its {state} state never runs {action}, so it's left out and the weapon changes at once");
        return false;
    }

    static readonly HashSet<string> warnedSwitches = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the weapon is being put away or brought up (its Deselect or Select state)</summary>
    internal static bool IsSwitching(Entities.Actors.Actor? weapon) =>
        weapon?.CurrentState?.StateName is DeselectState or SelectState;

    // Whether the sprite is of the weapon the player has: one that isn't is on its way out (its
    // Deselect may be a jump that hasn't landed yet, from inside one of its own actions)
    bool IsInHand(Entities.Actors.Actor weapon) =>
        string.Equals(weapon.Name, session.PlayerState.Weapon, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A_Lower([speed]): takes the weapon in hand that much further down (6 when left out), until
    /// it's off the view; then the weapon being changed to comes in. A Deselect state's action,
    /// on a frame of a tic or so that loops.
    /// </summary>
    internal void Lower(Entities.Actors.Actor weapon, string[] args)
    {
        var playerstate = session.PlayerState;
        playerstate.WeaponLowered = Math.Min(playerstate.WeaponLowered + SwitchSpeed(args), LoweredFully);
    }

    /// <summary>
    /// A_Raise([speed]): brings the weapon in hand that much further up (6 when left out); once
    /// it's all the way up it goes to its Ready state. A Select state's action, on a looping frame.
    /// </summary>
    internal void Raise(Entities.Actors.Actor weapon, string[] args)
    {
        var playerstate = session.PlayerState;
        playerstate.WeaponLowered = Math.Max(playerstate.WeaponLowered - SwitchSpeed(args), 0);
        if (playerstate.WeaponLowered == 0)
            JumpWeaponState(weapon, WeaponReadyState);
    }

    static int SwitchSpeed(string[] args) =>
        args.Length > 0 && int.TryParse(args[0], out var speed) && speed > 0 ? speed : DefaultSwitchSpeed;

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
        if (IsReloading(sprite) && weapons.IsLoaded(session.PlayerState.Weapon)
            && session.PlayerInput.IsFreshPress(ButtonType.Attack))
            StartState(sprite, "Fire", ButtonType.Attack);

        map.DoActor(sprite, session.Tics);
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
        session.PlayerInput.SetHeld(button, true);
        weapon.TicCount = 0;
        JumpWeaponState(weapon, state);
    }

    /// <summary>
    /// The shape to draw for the weapon in hand, or null for none. Drawn as the tics left it: the
    /// view is drawn more often than the game ticks, and not on every machine, so drawing it
    /// mustn't move it on (SyncWeaponSprite starts a weapon going down or coming up).
    /// </summary>
    internal string? WeaponShapeName() =>
        session.PlayerState.Weapon != null
        && WeaponSprite?.CurrentState is { Sprite.Length: > 0 } state && state.Sprite != "TNT1"     // TNT1: nothing in hand to see
            ? $"{state.Sprite}{state.FrameLetter}0" : null;

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
        Entities.Actors.ActorActionRegistry.Register("A_WeaponReady", WeaponReady);
        Entities.Actors.ActorActionRegistry.Register("A_GunAttack", GunAttack);
        Entities.Actors.ActorActionRegistry.Register("A_FireBullet", FireBullet);
        Entities.Actors.ActorActionRegistry.Register("A_HoldCharge", HoldCharge);
        Entities.Actors.ActorActionRegistry.Register("A_CustomPunch", CustomPunch);
        Entities.Actors.ActorActionRegistry.Register("A_ReFire", ReFire);
        Entities.Actors.ActorActionRegistry.Register("A_Reload", Reload);
        Entities.Actors.ActorActionRegistry.Register("A_Lower", Lower);
        Entities.Actors.ActorActionRegistry.Register("A_Raise", Raise);
        registerMissileActions();
    }

    /// <summary>
    /// Ready's every-tic action. Out of ammo for the picked weapon, it falls back to the best
    /// one that can still fire (the knife), and takes the picked one back up once there's ammo
    /// again. A weapon with a magazine goes to its Reload state once the magazine is empty, or on
    /// a press of reload while it isn't full, if there's ammo to load. Otherwise a fresh press of
    /// fire starts the Fire state.
    /// </summary>
    internal void WeaponReady(Entities.Actors.Actor weapon)
    {
        // A weapon going down or coming up (a Deselect or Select state that runs it), or on its
        // way out, does nothing
        if (IsSwitching(weapon) || !IsInHand(weapon))
            return;

        var playerstate = session.PlayerState;
        var inHand = weapons.CanFire(playerstate.ChosenWeapon) ? playerstate.ChosenWeapon : weapons.BestWeapon(canFire: true);
        if (inHand != null && inHand != playerstate.Weapon)
        {
            playerstate.Weapon = inHand;
            weapons.DrawWeapon();

            // A press now fires the weapon taken up (as vanilla, where the knife came out as the
            // last attack ended), so carry on with its sprite rather than this one. A weapon
            // being put away fires nothing: here, inside its own action, its Deselect is a jump
            // that lands once the action's done, so it's still this sprite, of another weapon.
            if (SyncWeaponSprite() is not { } taken || IsSwitching(taken) || !IsInHand(taken))
                return;
            weapon = taken;
        }

        if (weapons.CanReload(playerstate.Weapon) && weapon.ResolvedStates.ContainsKey(ReloadState)
            && (!weapons.IsLoaded(playerstate.Weapon) || session.PlayerInput.IsFreshPress(ButtonType.Reload)))
        {
            StartState(weapon, ReloadState, ButtonType.Reload);
            return;
        }

        // Zoomed in through a scope, a weapon with a ZoomFire state fires that (Gameplay/WeaponZoom.cs);
        // a weapon with a Backstab state swings that at an unaware enemy's back (Midnight Assault's
        // knife: Cmd_Fire's stealth kill frames)
        if (session.PlayerInput.IsFreshPress(ButtonType.Attack))
            StartState(weapon,
                playerstate.ZoomMode == ZoomMode.Zoomed && weapon.ResolvedStates.ContainsKey("ZoomFire") ? "ZoomFire"
                : weapon.ResolvedStates.ContainsKey("Backstab") && attacks?.BackstabTarget() != null ? "Backstab"
                : "Fire",
                ButtonType.Attack);
    }

    /// <summary>
    /// A_Reload([rounds[, "state"]]): loads the weapon in hand's magazine from its ammo, all that
    /// fits or that many rounds. Given a state, it goes there while the magazine still has room
    /// and there's ammo left (a shotgun's shell-by-shell loop).
    /// </summary>
    internal void Reload(Entities.Actors.Actor weapon, string[] args)
    {
        int? rounds = args.Length > 0 && int.TryParse(args[0], out var n) && n > 0 ? n : null;
        weapons.Reload(rounds);
        if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) && weapons.CanReload(session.PlayerState.Weapon))
            JumpWeaponState(weapon, args[1]);
    }

    /// <summary>
    /// A_HoldCharge("State"[, max]): while fire is held, the throw cooks: the weapon's power
    /// (PlayerState.WeaponPower) goes up a tic's worth, to max (100), and the weapon goes back to
    /// the state, held there; let go, it carries on (Midnight Assault's grenade). A_FireMissile's
    /// "power" throws with it.
    /// </summary>
    internal void HoldCharge(Entities.Actors.Actor weapon, string[] args)
    {
        if (!session.PlayerInput.IsPressed(ButtonType.Attack) || args.Length == 0)
            return;
        int max = args.Length > 1 && int.TryParse(args[1], out var m) ? Math.Clamp(m, 1, 100) : 100;
        var playerstate = session.PlayerState;
        playerstate.WeaponPower = Math.Min(playerstate.WeaponPower + (int)session.Tics, max);
        JumpWeaponState(weapon, args[0]);
    }

    /// <summary>
    /// A_FireBullet(min[, max[, "silent"]]): one shot at the closest target in the weapon's
    /// sights, for min..max whatever the distance (Midnight Assault's FireBullet; its zoomed sniper
    /// shot); silent, it alerts no one. Ammo and charge as A_GunAttack.
    /// </summary>
    internal void FireBullet(Entities.Actors.Actor weapon, string[] args)
    {
        if (!weapons.IsLoaded(session.PlayerState.Weapon) || !weapons.TakeCharge())
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
    internal void GunAttack(Entities.Actors.Actor weapon, string[] args)
    {
        if (!weapons.IsLoaded(session.PlayerState.Weapon) || !weapons.TakeCharge())
            return;
        int Divisor(int i, int fallback) => args.Length > i && int.TryParse(args[i], out var d) && d > 0 ? d : fallback;
        attacks.GunAttack(session.Player, Divisor(0, 4), Divisor(1, 6), Divisor(2, 6));
        weapons.UseAmmo();
    }

    // A_CustomPunch(damage, ...): the arguments aren't read yet; KnifeAttack deals vanilla's
    // US_RndT() >> 4 (0-15), which is what weapons.yaml asks for.
    internal void CustomPunch(Entities.Actors.Actor weapon) => attacks.KnifeAttack(session.Player);

    /// <summary>Fire still held and a round ready: back to Hold (or Fire, for a weapon without one).</summary>
    internal void ReFire(Entities.Actors.Actor weapon)
    {
        if (!session.PlayerInput.IsPressed(ButtonType.Attack) || !weapons.IsLoaded(session.PlayerState.Weapon))
            return;
        if (!JumpWeaponState(weapon, "Hold"))
            JumpWeaponState(weapon, "Fire");
    }
}
