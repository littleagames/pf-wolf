using Wolf3D.Entities;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                SWITCH ACTIONS

    Actions for mapdefs switches (walls.yaml `switch:`) and triggers that act on
    whatever shares the tag (map plane 4) of the tile that set them off. Tag 0 is
    no tag, so an untagged switch's actions find nothing. The switch's own tile is
    never a target, though it carries the same tag.

    =============================================================================
    */

    private static void RegisterSwitchActions()
    {
        // A_OpenDoor(["hold"]): opens the tagged doors; with hold they stay open until closed
        MapTriggerRegistry.Register("A_OpenDoor", (trigger, args) => ForTaggedDoors(trigger, door => SwitchOpenDoor(door, Hold(args))));
        // A_CloseDoor: closes the tagged doors (not on anything standing in the doorway)
        MapTriggerRegistry.Register("A_CloseDoor", (trigger, _) => ForTaggedDoors(trigger, CloseDoor));
        // A_ToggleDoor(["hold"]): opens the tagged doors that are shut or shutting, closes the rest
        MapTriggerRegistry.Register("A_ToggleDoor", (trigger, args) => ForTaggedDoors(trigger, door =>
        {
            if (doorobjlist[door].action is dooractiontypes.dr_closed or dooractiontypes.dr_closing)
                SwitchOpenDoor(door, Hold(args));
            else
                CloseDoor(door);
        }));

        // A_MoveWall([direction], ["moving sound"], ["blocked sound"]): pushes the first tagged
        // wall north, east, south or west, or left out (or ""), the way the player faces. Only
        // one wall moves at a time, as with pushwalls. Its tag goes with it, so it can be
        // moved again.
        MapTriggerRegistry.Register("A_MoveWall", MoveWallAction);

        // A_SetWall(id): turns the tagged walls into wall id (walls.yaml), e.g. lights on a
        // panel coming on
        MapTriggerRegistry.Register("A_SetWall", SetWallAction);
    }

    private static bool Hold(string[] args) => args.Any(a => a.Equals("hold", StringComparison.OrdinalIgnoreCase));

    // A switch opens a door whatever its lock: the lock is for opening it by hand (put a lock
    // on the switch to need a key for it)
    private static void SwitchOpenDoor(int door, bool hold)
    {
        OpenDoor(door);
        if (hold)
            doorobjlist[door].held = true;
    }

    /// <summary>Runs <paramref name="act"/> on each door on a tile with the trigger's tag; false if there are none.</summary>
    private static bool ForTaggedDoors(TriggerActivation trigger, Action<int> act)
    {
        bool any = false;
        if (trigger.Tag == 0)
            return false;

        for (int door = 0; door < lastdoorobj; door++)
        {
            if (_mapManager.GetTag(doorobjlist[door].tilex, doorobjlist[door].tiley) != trigger.Tag)
                continue;
            act(door);
            any = true;
        }
        return any;
    }

    /// <summary>
    /// The wall tiles with the trigger's tag, besides its own: solid walls (a wall beside a door
    /// too), not doors or a moving pushwall's tiles.
    /// </summary>
    private static IEnumerable<(int X, int Y)> TaggedWalls(TriggerActivation trigger) =>
        _mapManager.TaggedTiles(trigger.Tag).Where(t =>
            (t.X, t.Y) != (trigger.TileX, trigger.TileY)
            && (_mapManager.tilemap[t.X, t.Y] & BIT_DOOR) == 0
            && (_mapManager.tilemap[t.X, t.Y] & ~BIT_WALL) != 0);

    private static bool MoveWallAction(TriggerActivation trigger, string[] args)
    {
        var dirName = args.ElementAtOrDefault(0);
        controldirs dir;
        switch (dirName?.ToLowerInvariant())
        {
            case null or "": dir = trigger.Dir; break;
            case "north": dir = controldirs.di_north; break;
            case "east": dir = controldirs.di_east; break;
            case "south": dir = controldirs.di_south; break;
            case "west": dir = controldirs.di_west; break;
            default:
                Console.WriteLine($"A_MoveWall: unknown direction '{dirName}' (north, east, south or west).");
                return false;
        }

        foreach (var (x, y) in TaggedWalls(trigger))
            return PushWall(x, y, dir, args.ElementAtOrDefault(1), args.ElementAtOrDefault(2));
        return false;
    }

    private static bool SetWallAction(TriggerActivation trigger, string[] args)
    {
        if (!int.TryParse(args.ElementAtOrDefault(0), out var id) || id is <= 0 or >= BIT_WALL
            || !_mapManager.GetMapData().Walls.ContainsKey(id))
        {
            Console.WriteLine($"A_SetWall: '{args.ElementAtOrDefault(0)}' isn't a walls.yaml wall id.");
            return false;
        }

        bool any = false;
        foreach (var (x, y) in TaggedWalls(trigger))
        {
            // keeping the door-side mark on a wall beside a door
            _mapManager.tilemap[x, y] = (byte)(id | (_mapManager.tilemap[x, y] & BIT_WALL));
            any = true;
        }
        return any;
    }
}
