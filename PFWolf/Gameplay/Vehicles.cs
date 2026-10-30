using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Managers;

namespace PFWolf.Gameplay;

/*
=============================================================================

                                  VEHICLES

Something the player rides (Midnight Assault's boat, wl_agent.c EnterBoat and ExitBoat; a car,
a tank). Using the thing runs its Use state's A_Board(["sound"][, "message"]): the thing goes,
and the player is on its tile facing the way they used it from, riding it. Riding, they turn as
usual but don't walk or strafe: forward raises its speed by `vehicle.accel` a tic, back lowers
it by `vehicle.brake`, up to `vehicle.maxspeed` (global units a tic), and it carries them the
way they face. With a `vehicle.reverse` top speed, back goes on into reverse once it's stopped
(and forward brakes out of it); `vehicle.drag` slows it with neither pressed (else it holds its
speed). Running into a wall stops it. The HUD's power meter shows the throttle.

It can't go onto a dock (an object-plane thing whose class has `docktile: true`); use beside one
gets them off onto it -- the one ahead first, then the sides, then behind -- with the thing put
back where they were, its `vehicle.exitmessage` and `vehicle.exitsound`. With
`vehicle.exitanywhere`, any open tile beside them will do as well (the sides first, then behind,
then ahead); with nowhere to get off, `vehicle.noexit` says so. Use facing a door works it
instead (Gameplay/PlayerUse.cs). Dying, or another level, ends the ride. `vehicle.exithint` is
the use hint while they can get off, and `vehicle.dockmessage` a message kept up then (for a
status bar with no use hint).

`vehicle.weapon` is a weapon in hand for as long as they ride (a tank's cannon): they can't
change it, and getting off gives back the one they had. `vehicle.damagetaken` is the share of a
hit the rider takes (Gameplay/PlayerStats.cs). `vehicle.crushdamage` hurts an enemy it runs into
at `vehicle.crushspeed` or faster (a quarter of its top speed by default), each one at most every
half second, with `vehicle.crushsound`.

=============================================================================
*/

/// <param name="PlaySound">A sound the player makes</param>
/// <param name="ShowMessage">A message (a $NAME language key or the text)</param>
/// <param name="Settle">Settles the player's tile and area where they've been put (PlayerMovement.Thrust(0, 0))</param>
/// <param name="Respawn">Puts the vehicle's class back on a tile (MapManager.SpawnThing)</param>
/// <param name="Crush">Hurts an enemy the vehicle runs into, by so much (Monster.Damage, from the player)</param>
internal sealed record VehiclesHooks(Action<string> PlaySound, Action<string> ShowMessage, Action Settle, Action<int, int, string> Respawn,
    Action<Entities.Actors.Monster, int>? Crush = null);

internal sealed class Vehicles(MapManager map, InventoryManager inventory, GameSession session, VehiclesHooks hooks,
    PlayerWeapons? weapons = null)
{
    // How long an enemy run into is spared another crush, in tics
    const int CrushWait = 35;

    // Enemies just run into, and the tics until they can be again
    readonly Dictionary<Entities.Actors.Monster, int> crushed = [];

    /// <summary>Whether the acting player is riding something</summary>
    internal bool IsRiding => session.PlayerState.Vehicle != null;

    int Property(string name, int fallback) =>
        session.PlayerState.Vehicle is { } vehicle ? inventory.GetIntProperty(vehicle, name, fallback) : fallback;

    string? Text(string name) =>
        session.PlayerState.Vehicle is { } vehicle && inventory.GetStringProperty(vehicle, name) is { Length: > 0 } text ? text : null;

    bool Flag(string name) =>
        session.PlayerState.Vehicle is { } vehicle && inventory.GetProperty(vehicle, name) is { } value
        && (value is true || bool.TryParse(value.ToString(), out var b) && b);

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
        TakeWeapon();
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            hooks.PlaySound(args[0].Trim());
        if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            hooks.ShowMessage(args[1].Trim());
    }

    // vehicle.weapon: in hand while they ride, the weapon they had kept to give back
    void TakeWeapon()
    {
        var playerstate = session.PlayerState;
        if (weapons == null || Text("vehicle.weapon") is not { } gun)
            return;
        if (inventory.FindClass(gun, "Weapon") == null)
        {
            Console.WriteLine($"vehicle.weapon: no weapon class {gun}.");
            return;
        }
        (playerstate.VehicleGun, playerstate.VehicleFrom) = (gun, playerstate.ChosenWeapon);
        inventory.Give(gun, 1);
        playerstate.Weapon = playerstate.ChosenWeapon = gun;
        playerstate.WeaponPower = 0;
        session.PlayerInput.SetHeld(ButtonType.Attack, true);   // no shot from a fire held as they got in
        weapons.DrawWeapon();
    }

    /// <summary>
    /// Riding, a tic's controls: forward raises the speed (or brakes out of reverse), back lowers
    /// it (and, with a reverse gear, goes on into reverse), neither lets drag slow it; returns how
    /// far it carries them this tic (global units), the way they face, or back for less than 0
    /// </summary>
    internal int Throttle(int controlY, uint tics)
    {
        var playerstate = session.PlayerState;
        int max = Math.Max(Property("vehicle.maxspeed", 20 * (int)GameConstants.MoveScale), 1);
        int reverse = Math.Max(Property("vehicle.reverse", 0), 0);
        int accel = Property("vehicle.accel", 20) * (int)tics, brake = Property("vehicle.brake", 40) * (int)tics;
        int drag = Math.Max(Property("vehicle.drag", 0), 0) * (int)tics;
        int speed = playerstate.VehicleSpeed;
        if (controlY < 0)
            speed = speed < 0 ? Math.Min(speed + brake, 0) : speed + accel;
        else if (controlY > 0)
            speed = speed > 0 ? Math.Max(speed - brake, 0) : speed - accel;
        else if (drag > 0)
            speed = speed > 0 ? Math.Max(speed - drag, 0) : Math.Min(speed + drag, 0);
        playerstate.VehicleSpeed = speed = Math.Clamp(speed, -reverse, max);
        playerstate.VehicleThrottle = Math.Abs(speed) * 100 / (speed < 0 ? Math.Max(reverse, 1) : max);
        Crush(speed * (int)tics, tics);
        return speed * (int)tics;
    }

    /// <summary>The vehicle ran into something and couldn't move at all this tic: it stops dead</summary>
    internal void Stalled()
    {
        if (IsRiding)
            session.PlayerState.VehicleSpeed = session.PlayerState.VehicleThrottle = 0;
    }

    // vehicle.crushdamage: enemies just ahead the way it's going, at speed, are hurt
    void Crush(int carry, uint tics)
    {
        foreach (var victim in crushed.Keys.ToList())
            if ((crushed[victim] -= (int)tics) <= 0)
                crushed.Remove(victim);

        int damage = Property("vehicle.crushdamage", 0);
        if (damage <= 0 || hooks.Crush == null || session.PlayerState.Pawn is not { } player)
            return;
        int max = Math.Max(Property("vehicle.maxspeed", 20 * (int)GameConstants.MoveScale), 1);
        if (Math.Abs(session.PlayerState.VehicleSpeed) < Math.Max(Property("vehicle.crushspeed", max / 4), 1))
            return;

        // The way it's going: the way they face, or back in reverse (y runs south, angles north)
        double angle = player.Angle * Math.PI / 180 + (carry < 0 ? Math.PI : 0);
        double ax = Math.Cos(angle), ay = -Math.Sin(angle);
        long reach = 0x10000 + Math.Abs(carry) + 0x800;
        foreach (var actor in map.GetActors())
        {
            if (actor is not Entities.Actors.Monster monster || monster.IsRemoved || !monster.RuntimeFlags.Has(ObjFlags.Shootable)
                || crushed.ContainsKey(monster))
                continue;
            long dx = monster.X - player.X, dy = monster.Y - player.Y;
            if (Math.Abs(dx) > reach || Math.Abs(dy) > reach || dx * ax + dy * ay <= 0)
                continue;
            crushed[monster] = CrushWait;
            if (Text("vehicle.crushsound") is { } sound)
                hooks.PlaySound(sound);
            hooks.Crush(monster, damage);
        }
    }

    /// <summary>
    /// Where the player would get off: the dock beside them -- ahead first, then the sides, then
    /// behind -- else, with vehicle.exitanywhere, the first open tile beside them -- the sides,
    /// behind, then ahead; null for nowhere
    /// </summary>
    internal (int X, int Y)? ExitBeside()
    {
        if (session.PlayerState.Pawn is not { } player)
            return null;
        var dir = PlayerMovement.FacingDir(player.Angle);
        var tiles = MountedWeapons.ExitTiles(player.TileX, player.TileY, dir).ToList();   // behind, sides, ahead
        foreach (var (x, y) in Enumerable.Reverse(tiles))
            if (map.IsDock(x, y))
                return (x, y);
        if (Flag("vehicle.exitanywhere"))
            foreach (var (x, y) in new[] { tiles[1], tiles[2], tiles[0], tiles[3] })
                if (MountedWeapons.IsOpen(map, x, y) && !map.BlocksPlayer(x, y))
                    return (x, y);
        return null;
    }

    /// <summary>
    /// Use while riding: off onto the dock (or open tile) beside them, the vehicle left where they
    /// were; true if they were riding (whether or not they could get off)
    /// </summary>
    internal bool TryLeave()
    {
        var playerstate = session.PlayerState;
        if (!IsRiding || playerstate.Pawn is not { } player)
            return false;
        if (ExitBeside() is not { } exit)
        {
            if (Text("vehicle.noexit") is { } noexit)
                hooks.ShowMessage(noexit);
            return true;
        }

        var (fromx, fromy) = (player.TileX, player.TileY);
        var dir = PlayerMovement.FacingDir(player.Angle);
        var vehicle = playerstate.Vehicle!;
        var (message, sound) = (Text("vehicle.exitmessage"), Text("vehicle.exitsound"));
        player.SetPosition(exit.X, exit.Y);
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

    /// <summary>The use hint while riding: the vehicle's exithint where they can get off, else none</summary>
    internal string? Hint() => IsRiding && ExitBeside() != null ? Text("vehicle.exithint") : null;

    /// <summary>
    /// The message kept up while riding where they can get off (the vehicle's dockmessage: "Press
    /// Use to step onto the dock"), for a status bar with no use hint; else none
    /// </summary>
    internal string? DockMessage() => IsRiding && ExitBeside() != null ? Text("vehicle.dockmessage") : null;

    /// <summary>Each think: a ride ends with the player's death or another level; its weapon stays in hand</summary>
    internal void Tick()
    {
        var playerstate = session.PlayerState;
        if (IsRiding && (playerstate.Health <= 0 || playerstate.VehicleMap != session.GameState.MapOn))
            Clear();
        else if (IsRiding && playerstate.VehicleGun is { } gun && playerstate.Weapon != gun && inventory.Has(gun))
        {
            playerstate.Weapon = playerstate.ChosenWeapon = gun;
            weapons?.DrawWeapon();
        }
    }

    void Clear()
    {
        var playerstate = session.PlayerState;
        if (playerstate.VehicleGun is { } gun)
        {
            inventory.Take(gun, 1);
            if (weapons != null)
            {
                playerstate.Weapon = playerstate.ChosenWeapon =
                    playerstate.VehicleFrom is { } before && inventory.Has(before) && weapons.CanFire(before) ? before : weapons.BestWeapon(canFire: true);
                session.PlayerInput.SetHeld(ButtonType.Attack, true);
                weapons.DrawWeapon();
            }
        }
        (playerstate.Vehicle, playerstate.VehicleMap, playerstate.VehicleSpeed, playerstate.VehicleThrottle) = (null, null, 0, 0);
        (playerstate.VehicleGun, playerstate.VehicleFrom) = (null, null);
        crushed.Clear();
    }
}
