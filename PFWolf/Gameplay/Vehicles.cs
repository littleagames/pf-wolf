using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/*
=============================================================================

                                  VEHICLES

Something the player rides (Midnight Assault's boat, wl_agent.c EnterBoat and ExitBoat). Using
the thing runs its Use state's A_Board(["sound"][, "message"]): the thing goes, and the player is
on its tile facing the way they used it from, riding it. Riding, they turn as usual but don't
walk or strafe: forward raises its speed by `vehicle.accel` a tic, back lowers it by
`vehicle.brake`, up to `vehicle.maxspeed` (global units a tic), and it carries them the way they
face. The HUD's power meter shows the throttle. It can't go onto a dock (an object-plane thing
whose class has `docktile: true`); use beside one gets them off onto it -- the one ahead first,
then the sides, then behind -- with the thing put back where they were, its
`vehicle.exitmessage` and `vehicle.exitsound`; with no dock beside them, `vehicle.noexit` says
so. Dying, or another level, ends the ride. `vehicle.exithint` is the use hint while a dock is
beside them.

=============================================================================
*/

/// <param name="PlaySound">A sound the player makes</param>
/// <param name="ShowMessage">A message (a $NAME language key or the text)</param>
/// <param name="Settle">Settles the player's tile and area where they've been put (PlayerMovement.Thrust(0, 0))</param>
/// <param name="Respawn">Puts the vehicle's class back on a tile (MapManager.SpawnThing)</param>
internal sealed record VehiclesHooks(Action<string> PlaySound, Action<string> ShowMessage, Action Settle, Action<int, int, string> Respawn);

internal sealed class Vehicles(MapManager map, InventoryManager inventory, GameSession session, VehiclesHooks hooks)
{
    /// <summary>Whether the acting player is riding something</summary>
    internal bool IsRiding => session.PlayerState.Vehicle != null;

    int Property(string name, int fallback) =>
        session.PlayerState.Vehicle is { } vehicle ? inventory.GetIntProperty(vehicle, name, fallback) : fallback;

    string? Text(string name) =>
        session.PlayerState.Vehicle is { } vehicle && inventory.GetStringProperty(vehicle, name) is { Length: > 0 } text ? text : null;

    /// <summary>A_Board(["sound"][, "message"]): the player who used it rides it</summary>
    internal void Board(Entities.Actors.Actor thing, string[] args)
    {
        var playerstate = session.PlayerState;
        if (IsRiding || playerstate.Mount != null || playerstate.Pawn is not { } player)
            return;

        (playerstate.Vehicle, playerstate.VehicleMap, playerstate.VehicleSpeed, playerstate.VehicleThrottle) =
            (thing.Name, session.GameState.MapOn, 0, 0);
        int facing = MountedWeapons.FacingAngle(PlayerMovement.FacingDir(player.Angle));
        int x = thing.TileX, y = thing.TileY;
        map.MarkForRemoval(thing);
        if (map.ActorAt[x, y] is BlockingActor)
            map.ActorAt[x, y] = null;
        player.SetPosition(x, y);
        player.Angle = (short)facing;
        hooks.Settle();
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            hooks.PlaySound(args[0].Trim());
        if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            hooks.ShowMessage(args[1].Trim());
    }

    /// <summary>
    /// Riding, a tic's controls: forward raises the speed, back lowers it, within its top speed;
    /// returns how far it carries them this tic (global units), the way they face
    /// </summary>
    internal int Throttle(int controlY, uint tics)
    {
        var playerstate = session.PlayerState;
        int max = Math.Max(Property("vehicle.maxspeed", 20 * (int)GameConstants.MoveScale), 1);
        if (controlY < 0)
            playerstate.VehicleSpeed += Property("vehicle.accel", 20) * (int)tics;
        else if (controlY > 0)
            playerstate.VehicleSpeed -= Property("vehicle.brake", 40) * (int)tics;
        playerstate.VehicleSpeed = Math.Clamp(playerstate.VehicleSpeed, 0, max);
        playerstate.VehicleThrottle = playerstate.VehicleSpeed * 100 / max;
        return playerstate.VehicleSpeed * (int)tics;
    }

    /// <summary>The dock beside the player they'd get off onto: ahead first, then the sides, then behind; null for none</summary>
    internal (int X, int Y)? DockBeside()
    {
        if (session.PlayerState.Pawn is not { } player)
            return null;
        var dir = PlayerMovement.FacingDir(player.Angle);
        foreach (var (x, y) in MountedWeapons.ExitTiles(player.TileX, player.TileY, dir).Reverse())
            if (map.IsDock(x, y))
                return (x, y);
        return null;
    }

    /// <summary>
    /// Use while riding: off onto the dock beside them, the vehicle left where they were; true
    /// if they were riding (whether or not they could get off)
    /// </summary>
    internal bool TryLeave()
    {
        var playerstate = session.PlayerState;
        if (!IsRiding || playerstate.Pawn is not { } player)
            return false;
        if (DockBeside() is not { } dock)
        {
            if (Text("vehicle.noexit") is { } noexit)
                hooks.ShowMessage(noexit);
            return true;
        }

        var (fromx, fromy) = (player.TileX, player.TileY);
        var dir = PlayerMovement.FacingDir(player.Angle);
        var vehicle = playerstate.Vehicle!;
        var (message, sound) = (Text("vehicle.exitmessage"), Text("vehicle.exitsound"));
        player.SetPosition(dock.X, dock.Y);
        player.Angle = (short)MountedWeapons.FacingAngle(dir);
        hooks.Settle();
        Clear();
        hooks.Respawn(fromx, fromy, vehicle);
        if (sound != null)
            hooks.PlaySound(sound);
        if (message != null)
            hooks.ShowMessage(message);
        return true;
    }

    /// <summary>The use hint while riding: the vehicle's exithint with a dock beside them, else none</summary>
    internal string? Hint() => IsRiding && DockBeside() != null ? Text("vehicle.exithint") : null;

    /// <summary>Each think: a ride ends with the player's death or another level</summary>
    internal void Tick()
    {
        var playerstate = session.PlayerState;
        if (IsRiding && (playerstate.Health <= 0 || playerstate.VehicleMap != session.GameState.MapOn))
            Clear();
    }

    void Clear()
    {
        var playerstate = session.PlayerState;
        (playerstate.Vehicle, playerstate.VehicleMap, playerstate.VehicleSpeed, playerstate.VehicleThrottle) = (null, null, 0, 0);
    }
}
