namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                SMART SWITCHES

    Blake Stone's smart switches (bstone's OperateSmartSwitch): something that works whatever is
    on a tile it names. A door there is unlocked and opened; a wall switch is thrown (left be
    when told to turn things on or off and it's already that way, by its walls.yaml switch
    `state`); an enemy there is woken, or, with `switched: kill` in its actordefs properties
    (crates, pod eggs, morphing posts), killed; a thing with `switched: remove` is taken away.

    What sets one off: a floors trigger running A_SmartSwitch (the tile's object-plane value is
    the tile it works, x high byte and y low byte), and an enemy dying that a floors actor-code
    linked to a tile (Planet Strike's: its death throws the switch for its barriers).

    =============================================================================
    */

    internal enum SmartSwitchOp { Off, On, Toggle }

    private static void RegisterSmartSwitchActions()
    {
        // A_SmartSwitch(["off" | "on" | "toggle"][, "remove"]): works the tile the trigger's
        // object-plane value names (toggle by default); with "remove", the trigger goes once
        // it has found something to work that isn't a door or a wall (bstone's door trigger)
        Entities.MapTriggerRegistry.Register("A_SmartSwitch", (trigger, args) =>
        {
            var op = args.ElementAtOrDefault(0)?.ToLowerInvariant() switch
            {
                "off" => SmartSwitchOp.Off,
                "on" => SmartSwitchOp.On,
                _ => SmartSwitchOp.Toggle,
            };
            int target = _mapManager.MAPSPOT(trigger.TileX, trigger.TileY, 1);
            if (target == 0)
                return false;
            bool done = OperateSmartSwitch(target >> 8, target & 0xff, op);
            if (done && args.Any(a => a.Equals("remove", StringComparison.OrdinalIgnoreCase)))
            {
                _mapManager.ClearFloorCode(trigger.TileX, trigger.TileY);
                _mapManager.SetMapSpot(trigger.TileX, trigger.TileY, 1, 0);
            }
            return true;
        });
    }

    /// <summary>Works whatever is on tile (x, y). True when it found an actor or thing there, or nothing at all.</summary>
    internal static bool OperateSmartSwitch(int x, int y, SmartSwitchOp op)
    {
        if (x < 0 || y < 0 || x >= MapManager.MAPSIZE || y >= MapManager.MAPSIZE)
            return true;

        int tile = _mapManager.tilemap[x, y];

        // A door: unlocked and opened
        if ((tile & BIT_DOOR) != 0 && (tile & BIT_WALL) == 0)
        {
            int door = tile & ~BIT_DOOR;
            if (door < lastdoorobj)
            {
                doorobjlist[door].unlocked = true;
                OpenDoor(door);
            }
            return false;
        }

        // An actor or thing standing there
        var thing = _mapManager.GetActors().FirstOrDefault(a => !a.IsRemoved && a != player && a.TileX == x && a.TileY == y
            && (a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE) || a.Properties.ContainsKey("switched")));
        if (thing != null)
        {
            var switched = thing.Properties.TryGetValue("switched", out var how) ? how?.ToString() : null;
            if (switched?.Equals("remove", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (_mapManager.actorat[x, y] is BlockingActor)
                    _mapManager.actorat[x, y] = null;
                _mapManager.MarkForRemoval(thing);
            }
            else if (thing is Entities.Actors.Monster monster && monster.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE))
            {
                if (switched?.Equals("kill", StringComparison.OrdinalIgnoreCase) == true)
                {
                    monster.Hitpoints = 0;
                    monster.Kill();
                }
                else
                {
                    if (monster.Active == activetypes.ac_no)
                        monster.Active = activetypes.ac_yes;
                    if (!monster.RuntimeFlags.HasFlag(objflags.FL_ATTACKMODE))
                        monster.FirstSighting();
                }
            }
            return true;
        }

        // A wall switch: thrown, unless it's already the way it's told to turn
        if (tile != 0)
        {
            int id = tile & ~BIT_WALL;
            if (_mapManager.GetMapData().Walls.TryGetValue(id, out var wall) && wall.Switch is { } wallSwitch)
            {
                bool isOn = wallSwitch.State.Equals("on", StringComparison.OrdinalIgnoreCase);
                bool isOff = wallSwitch.State.Equals("off", StringComparison.OrdinalIgnoreCase);
                if (!(op == SmartSwitchOp.Off && isOff) && !(op == SmartSwitchOp.On && isOn))
                    UseSwitch(wallSwitch, x, y, FacingDir(player.Angle));
            }
            return false;
        }

        // Nothing there (what it was for has moved off)
        return true;
    }

    /// <summary>A dying actor's floors actor-code work: what it carried, and the tile its death switches</summary>
    internal static void CarriedDeathWork(Entities.Actors.Actor ob, int tilex, int tiley)
    {
        foreach (var carried in ob.CarriedDrops)
            PlaceItemType(carried, tilex, tiley);
        ob.CarriedDrops.Clear();

        if (ob.DeathLink != 0)
        {
            int link = ob.DeathLink;
            ob.DeathLink = 0;
            OperateSmartSwitch(link >> 8, link & 0xff, SmartSwitchOp.Off);
        }
    }
}
