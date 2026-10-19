using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/*
=============================================================================

                               MOUNTED WEAPONS

A gun the player mans where it stands (Midnight Assault's MG nest, wl_agent.c EnterMountedWeapon
and ExitMountedWeapon), a weapon with `weapon.mounted: true`. Using the thing it's on runs its
Use state's A_Mount("Gun"[, "sound"]): the player
steps onto its tile facing the way they used it from, with the gun in hand (given for as long as
they're on it). There they can turn but not move or change weapons, and the gun's
`weapon.mountdamage` is the share of a hit they take (the original's 50%). Use again (not while
firing) steps them off: behind, then to the sides, then in front, to the first tile with nothing
in it, facing the same way, with the weapon they had before back in hand; with nowhere to go,
the gun's `weapon.mountnoroom` says so.

=============================================================================
*/

/// <param name="PlaySound">A sound the player makes (getting on)</param>
/// <param name="ShowMessage">A message (no room to get off)</param>
/// <param name="Settle">Settles the player's tile and area where they've been put (PlayerMovement.Thrust(0, 0))</param>
internal sealed record MountedWeaponsHooks(Action<string> PlaySound, Action<string> ShowMessage, Action Settle);

internal sealed class MountedWeapons(MapManager map, InventoryManager inventory, PlayerWeapons weapons, GameSession session,
    MountedWeaponsHooks hooks)
{
    /// <summary>Whether the acting player is manning a mounted gun</summary>
    internal bool IsMounted => session.PlayerState.Mount != null;

    /// <summary>A_Mount("Gun"[, "sound"]): the player who used it mans it, with that gun</summary>
    internal void Mount(Entities.Actors.Actor mount, string[] args)
    {
        var playerstate = session.PlayerState;
        if (IsMounted || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]) || playerstate.Pawn is not { } player)
            return;
        var gun = args[0].Trim();
        if (inventory.FindClass(gun, "Weapon") == null)
        {
            Console.WriteLine($"A_Mount: no weapon class {gun}.");
            return;
        }

        // the way they face as they use it, squared up
        int facing = FacingAngle(PlayerMovement.FacingDir(player.Angle));
        (playerstate.Mount, playerstate.MountGun, playerstate.MountFrom, playerstate.MountMap) =
            (mount, gun, playerstate.ChosenWeapon, session.GameState.MapOn);
        inventory.Give(gun, 1);
        playerstate.Weapon = playerstate.ChosenWeapon = gun;
        playerstate.WeaponPower = 0;
        session.PlayerInput.SetHeld(ButtonType.Attack, true);   // no shot from a fire held as they got on

        player.SetPosition(mount.TileX, mount.TileY);
        player.Angle = (short)facing;
        hooks.Settle();
        weapons.DrawWeapon();
        if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            hooks.PlaySound(args[1].Trim());
    }

    /// <summary>
    /// Steps the player off what they're manning, if they're on one and there's a tile to step
    /// onto: true if they were on one (whether or not they could get off)
    /// </summary>
    internal bool TryDismount()
    {
        var playerstate = session.PlayerState;
        if (!IsMounted || playerstate.Pawn is not { } player)
            return false;

        var dir = PlayerMovement.FacingDir(player.Angle);
        foreach (var (x, y) in ExitTiles(player.TileX, player.TileY, dir))
        {
            if (!IsOpen(x, y))
                continue;
            player.SetPosition(x, y);
            player.Angle = (short)FacingAngle(dir);
            hooks.Settle();
            Leave();
            return true;
        }

        if (playerstate.MountGun != null && inventory.GetStringProperty(playerstate.MountGun, "weapon.mountnoroom") is { Length: > 0 } noRoom)
            hooks.ShowMessage(noRoom);
        return true;
    }

    /// <summary>
    /// Each think: one who's no longer on it (dying, a saved game loaded, another level) is let
    /// off where they are, and a mounted gun no one's manning goes
    /// </summary>
    internal void Tick()
    {
        var playerstate = session.PlayerState;
        if (IsMounted && (playerstate.Weapon != playerstate.MountGun || playerstate.MountMap != session.GameState.MapOn
            || playerstate.Mount!.IsRemoved))
            Leave();
        else if (!IsMounted && playerstate.Weapon is { } weapon && IsMountedGun(weapon))
        {
            inventory.Take(weapon, 1);
            playerstate.Weapon = playerstate.ChosenWeapon = weapons.BestWeapon(canFire: true);
            weapons.DrawWeapon();
        }
    }

    /// <summary>Whether a weapon is one that's only manned where it stands (weapon.mounted)</summary>
    internal bool IsMountedGun(string weapon) =>
        inventory.GetProperty(weapon, "weapon.mounted") is { } mounted && (mounted is true || bool.TryParse(mounted.ToString(), out var b) && b);

    // Off the gun: it's taken away, and the weapon they had before is back if they can still fire it
    void Leave()
    {
        var playerstate = session.PlayerState;
        if (playerstate.MountGun is { } gun && playerstate.Weapon == gun)
        {
            inventory.Take(gun, 1);
            playerstate.Weapon = playerstate.ChosenWeapon =
                playerstate.MountFrom is { } before && inventory.Has(before) && weapons.CanFire(before) ? before : weapons.BestWeapon(canFire: true);
            session.PlayerInput.SetHeld(ButtonType.Attack, true);
        }
        else if (playerstate.MountGun is { } kept)
            inventory.Take(kept, 1);
        (playerstate.Mount, playerstate.MountGun, playerstate.MountFrom, playerstate.MountMap) = (null, null, null, null);
        weapons.DrawWeapon();
    }

    // Nothing in the way: no wall or door, and nothing solid or shootable standing there
    bool IsOpen(int x, int y) =>
        x > 0 && x < map.MapWidth - 1 && y > 0 && y < map.MapHeight - 1
        && map.TileMap[x, y] == 0 && map.ActorAt[x, y] == null
        && !map.GetActors().Any(a => !a.IsRemoved && a.TileX == x && a.TileY == y && a is not Entities.Actors.PlayerPawn
            && (a.RuntimeFlags.Has(ObjFlags.Shootable) || a.Flags.Contains("SOLID")));

    /// <summary>The tiles to step off onto, in turn: behind, to either side, then in front (ExitMountedWeapon)</summary>
    internal static IEnumerable<(int X, int Y)> ExitTiles(int x, int y, ControlDir facing) => facing switch
    {
        ControlDir.East => [(x - 1, y), (x, y - 1), (x, y + 1), (x + 1, y)],
        ControlDir.North => [(x, y + 1), (x + 1, y), (x - 1, y), (x, y - 1)],
        ControlDir.West => [(x + 1, y), (x, y - 1), (x, y + 1), (x - 1, y)],
        _ => [(x, y - 1), (x + 1, y), (x - 1, y), (x, y + 1)],
    };

    internal static int FacingAngle(ControlDir dir) => dir switch
    {
        ControlDir.East => 0,
        ControlDir.North => 90,
        ControlDir.West => 180,
        _ => 270,
    };
}
