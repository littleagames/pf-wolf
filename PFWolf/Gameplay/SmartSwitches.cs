using PFWolf.Assets;

namespace PFWolf.Gameplay;

internal enum SmartSwitchOp { Off, On, Toggle }

/// <param name="useSwitch">Throws a wall switch on a tile, as the player using it from where they face</param>
internal sealed class SmartSwitches(MapManager map, Doors doors, GameSession session, Action<MapSwitchTranslation, int, int> useSwitch)
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

    internal void Register()
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
            int target = map.MapSpot(trigger.TileX, trigger.TileY, 1);
            if (target == 0)
                return false;
            bool done = OperateSmartSwitch(target >> 8, target & 0xff, op);
            if (done && args.Any(a => a.Equals("remove", StringComparison.OrdinalIgnoreCase)))
            {
                map.ClearFloorCode(trigger.TileX, trigger.TileY);
                map.SetMapSpot(trigger.TileX, trigger.TileY, 1, 0);
            }
            return true;
        });
    }

    /// <summary>Works whatever is on tile (x, y). True when it found an actor or thing there, or nothing at all.</summary>
    internal bool OperateSmartSwitch(int x, int y, SmartSwitchOp op)
    {
        if (x < 0 || y < 0 || x >= MapManager.MapSize || y >= MapManager.MapSize)
            return true;

        int tile = map.TileMap[x, y];

        // A door: unlocked and opened
        if ((tile & GameConstants.BitDoor) != 0)
        {
            int door = tile & ~GameConstants.BitDoor;
            if (door < doors.LastDoorObj)
            {
                doors.DoorObjList[door].Unlocked = true;
                doors.OpenDoor(door);
            }
            return false;
        }

        // An actor or thing standing there
        var thing = map.GetActors().FirstOrDefault(a => !a.IsRemoved && a != session.Player && a.TileX == x && a.TileY == y
            && (a.RuntimeFlags.Has(ObjFlags.Shootable) || a.Properties.ContainsKey("switched")));
        if (thing != null)
        {
            var switched = thing.Properties.TryGetValue("switched", out var how) ? how?.ToString() : null;
            if (switched?.Equals("remove", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (map.ActorAt[x, y] is BlockingActor)
                    map.ActorAt[x, y] = null;
                map.MarkForRemoval(thing);
            }
            else if (thing is Entities.Actors.Monster monster && monster.RuntimeFlags.Has(ObjFlags.Shootable))
            {
                if (switched?.Equals("kill", StringComparison.OrdinalIgnoreCase) == true)
                {
                    monster.Hitpoints = 0;
                    monster.Kill();
                }
                else
                {
                    if (monster.Active == ActiveType.No)
                        monster.Active = ActiveType.Yes;
                    if (!monster.RuntimeFlags.Has(ObjFlags.AttackMode))
                        monster.FirstSighting();
                }
            }
            return true;
        }

        // A wall switch: thrown, unless it's already the way it's told to turn
        if (tile != 0)
        {
            int id = tile & ~GameConstants.BitWall;
            if (map.GetMapData().Walls.TryGetValue(id, out var wall) && wall.Switch is { } wallSwitch)
            {
                bool isOn = wallSwitch.State.Equals("on", StringComparison.OrdinalIgnoreCase);
                bool isOff = wallSwitch.State.Equals("off", StringComparison.OrdinalIgnoreCase);
                if (!(op == SmartSwitchOp.Off && isOff) && !(op == SmartSwitchOp.On && isOn))
                    useSwitch(wallSwitch, x, y);
            }
            return false;
        }

        // Nothing there (what it was for has moved off)
        return true;
    }

    /// <summary>A dying actor's floors actor-code work: what it carried, and the tile its death switches</summary>
    internal void CarriedDeathWork(Entities.Actors.Actor ob, int tilex, int tiley)
    {
        foreach (var carried in ob.CarriedDrops)
            map.SpawnThing(tilex, tiley, carried);     // PlaceItemType
        ob.CarriedDrops.Clear();

        if (ob.DeathLink != 0)
        {
            int link = ob.DeathLink;
            ob.DeathLink = 0;
            OperateSmartSwitch(link >> 8, link & 0xff, SmartSwitchOp.Off);
        }
    }
}
