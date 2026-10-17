using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/*
=============================================================================

                                  LATCHING

Something that latches onto the player and has to be struck off (Midnight Assault's dog,
wp_dogattack: T_Bite's LatchOntoPlayer, UpdateDogAttack and BashAttack).

A monster's A_LatchBite("Struggle"[, latch[, hit]]) bites as T_Bite does (hit times in 256 it
lands, 240 when left out), and a bite that lands latches on latch times in 256 (128) instead of
hurting: the monster goes to its Latched state, where it isn't seen or shot and doesn't block the
way, and the player has the struggle weapon (`weapon.latch: true`) in hand. Theirs is gone until
it lets go; they can't change weapons or use anything, and move `weapon.slowmove` times slower.

The monster's Latched state hurts them (A_LatchHurt, 0-15 a time); the struggle weapon's fire
strikes at it (A_ShakeOff), killing it on the strike it's lucky with, or the last one;
A_JumpIfLatched carries on struggling while it holds on, and A_LatchRelease gives them their weapon
back. One manning a mounted gun, or already latched onto, is only bitten.

=============================================================================
*/

/// <param name="HurtPlayer">Hurts a player, as that player (Players.TakeDamage)</param>
/// <param name="PlayAttackSound">The weapon in hand's attacksound (a strike)</param>
/// <param name="WeaponReady">Whether the weapon in hand is idle on its Ready state</param>
/// <param name="HurtMonster">The player's strike lands on what's latched on (Monster.Damage)</param>
internal sealed record LatchingHooks(
    Action<Entities.Actors.PlayerPawn, int, Entities.Actors.Actor> HurtPlayer,
    Action PlayAttackSound,
    Func<bool> WeaponReady,
    Action<Entities.Actors.Monster, int, Entities.Actors.Actor> HurtMonster);

internal sealed class Latching(MapManager map, InventoryManager inventory, PlayerWeapons weapons, GameSession session, LatchingHooks hooks)
{
    internal const string LatchedState = "Latched";

    /// <summary>Whether something is latched onto the acting player</summary>
    internal bool IsLatched => session.PlayerState.Latch != null;

    /// <summary>Whether what latched onto the acting player still holds on (alive, on this level)</summary>
    internal bool StillHeld
    {
        get
        {
            var playerstate = session.PlayerState;
            return playerstate.Latch is { IsRemoved: false, Hitpoints: > 0 } && playerstate.LatchMap == session.GameState.MapOn;
        }
    }

    /// <summary>A_LatchBite("Struggle"[, latch[, hit]]): a bite that may latch on (see above)</summary>
    internal void LatchBite(Entities.Actors.Monster biter, string[] args)
    {
        int Chance(int i, int fallback) => args.Length > i && int.TryParse(args[i], out var c) ? Math.Clamp(c, 0, 256) : fallback;
        if (!biter.BiteLands(Chance(2, 240)))
            return;

        var gun = args.Length > 0 ? args[0].Trim() : "";
        if (biter.Target is Entities.Actors.PlayerPawn pawn && biter.ResolvedStates.ContainsKey(LatchedState)
            && gun.Length > 0 && inventory.FindClass(gun, "Weapon") != null)
        {
            using var _ = session.ActAs(pawn.State);
            var playerstate = session.PlayerState;
            if (playerstate.Latch == null && playerstate.Mount == null && playerstate.Health > 0
                && session.Random.RndT() < Chance(1, 128))
            {
                LatchOn(biter, gun);
                return;
            }
        }
        biter.BiteTarget();
    }

    internal void LatchOn(Entities.Actors.Monster biter, string gun)
    {
        var playerstate = session.PlayerState;
        (playerstate.Latch, playerstate.LatchGun, playerstate.LatchFrom, playerstate.LatchMap, playerstate.LatchStrikes) =
            (biter, gun, playerstate.ChosenWeapon, session.GameState.MapOn, 0);
        playerstate.MoveSlowdown = Math.Max(inventory.GetIntProperty(gun, "weapon.slowmove", 1), 1);

        inventory.Give(gun, 1);
        playerstate.Weapon = playerstate.ChosenWeapon = gun;
        playerstate.WeaponSprite = null;            // the struggle starts from its Ready, whatever they were doing
        playerstate.WeaponPower = 0;
        session.PlayerInput.SetHeld(ButtonType.Attack, true);   // a strike needs a fresh press
        weapons.DrawWeapon();

        // Out of sight and out of the way
        biter.RuntimeFlags = (biter.RuntimeFlags & ~ObjFlags.Shootable) | ObjFlags.NonMark;
        map.UnmarkActorTile(biter.TileX, biter.TileY);
        GoTo(biter, LatchedState);
    }

    /// <summary>
    /// A_LatchHurt: the one it's latched onto is hurt, 0-15. Latched onto no one (a saved game
    /// loaded, they're off the level), it lets go and goes after them again.
    /// </summary>
    internal void LatchHurt(Entities.Actors.Actor biter)
    {
        var victim = session.Players.FirstOrDefault(p => ReferenceEquals(p.Latch, biter));
        if (victim == null)
        {
            if (biter is Entities.Actors.Monster monster)
                LetGo(monster);
            return;
        }
        if (victim.Pawn is { } pawn && victim.Health > 0)
            hooks.HurtPlayer(pawn, session.Random.RndT() >> 4, biter);
    }

    /// <summary>
    /// A_ShakeOff([tries[, chance[, damage]]]): the struggle weapon strikes at what's latched on
    /// (its attacksound), which ends up where the player is; chance times in 256 (128), or on the
    /// tries'th strike (5), it takes the damage (1, a dog's every hitpoint)
    /// </summary>
    internal void ShakeOff(Entities.Actors.Actor weapon, string[] args)
    {
        var playerstate = session.PlayerState;
        if (!StillHeld || playerstate.Latch is not Entities.Actors.Monster biter || playerstate.Pawn is not { } player)
            return;
        int Arg(int i, int fallback) => args.Length > i && int.TryParse(args[i], out var n) ? n : fallback;

        (biter.X, biter.Y, biter.TileX, biter.TileY) = (player.X, player.Y, player.TileX, player.TileY);
        biter.SyncPosition();
        hooks.PlayAttackSound();

        if (++playerstate.LatchStrikes >= Math.Max(Arg(0, 5), 1) || session.Random.RndT() < Math.Clamp(Arg(1, 128), 0, 256))
        {
            playerstate.LatchStrikes = 0;
            hooks.HurtMonster(biter, Math.Max(Arg(2, 1), 0), player);
            if (biter.Hitpoints > 0 && !biter.IsRemoved && biter.CurrentState?.StateName != LatchedState)
                GoTo(biter, LatchedState);      // hurt, not off: it holds on
        }
    }

    /// <summary>A_JumpIfLatched("State"): the weapon goes there while what's latched on holds on</summary>
    internal void JumpIfLatched(Entities.Actors.Actor weapon, string[] args)
    {
        if (StillHeld && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            WeaponInHand.JumpWeaponState(weapon, args[0].Trim());
    }

    /// <summary>A_LatchRelease: the struggle's over, and the weapon they had is back in hand</summary>
    internal void LatchRelease(Entities.Actors.Actor weapon) => Release();

    /// <summary>
    /// Each think: one whose struggle has ended some other way (dying, a load, another level, the
    /// thing gone, or killed while they weren't striking) gets their weapon back, and a struggle
    /// weapon held with nothing latched on goes
    /// </summary>
    internal void Tick()
    {
        var playerstate = session.PlayerState;
        if (IsLatched)
        {
            if (playerstate.Weapon != playerstate.LatchGun || playerstate.LatchMap != session.GameState.MapOn
                || playerstate.Latch!.IsRemoved || !StillHeld && hooks.WeaponReady())
                Release();
        }
        else if (playerstate.Weapon is { } weapon && IsLatchGun(weapon))
        {
            inventory.Take(weapon, 1);
            playerstate.Weapon = playerstate.ChosenWeapon = weapons.BestWeapon(canFire: true);
            weapons.DrawWeapon();
        }
    }

    /// <summary>Whether a weapon is a struggle with something latched on (weapon.latch)</summary>
    internal bool IsLatchGun(string weapon) =>
        inventory.GetProperty(weapon, "weapon.latch") is { } latch && (latch is true || bool.TryParse(latch.ToString(), out var b) && b);

    void Release()
    {
        var playerstate = session.PlayerState;
        if (playerstate.LatchGun is { } gun)
        {
            if (playerstate.Weapon == gun)
            {
                playerstate.Weapon = playerstate.ChosenWeapon =
                    playerstate.LatchFrom is { } before && inventory.Has(before) && weapons.CanFire(before) ? before : weapons.BestWeapon(canFire: true);
                session.PlayerInput.SetHeld(ButtonType.Attack, true);
            }
            inventory.Take(gun, 1);
        }

        // Still alive on this level (they died, say): it lets go
        if (playerstate.Latch is Entities.Actors.Monster biter && StillHeld)
            LetGo(biter);

        (playerstate.Latch, playerstate.LatchGun, playerstate.LatchFrom, playerstate.LatchMap, playerstate.LatchStrikes) = (null, null, null, null, 0);
        playerstate.MoveSlowdown = 1;
        weapons.DrawWeapon();
    }

    // Seen, shot and in the way again, and after them
    static void LetGo(Entities.Actors.Monster biter)
    {
        biter.RuntimeFlags = (biter.RuntimeFlags & ~ObjFlags.NonMark) | ObjFlags.Shootable;
        GoTo(biter, biter.ResolvedStates.ContainsKey("Chase") ? "Chase" : "Spawn");
    }

    // To a state, from inside its own action too (where SetState would carry on past it)
    static void GoTo(Entities.Actors.Actor actor, string state)
    {
        if (actor.ResolvedStates.TryGetValue(state, out var frame))
            actor.JumpTo(frame);
    }
}
