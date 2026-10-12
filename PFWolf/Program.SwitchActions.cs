using PFWolf.Assets;
using PFWolf.Entities;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

                                SWITCH ACTIONS

    Actions for mapdefs switches (walls.yaml `switch:`) and triggers that act on
    whatever shares the tag (map plane 4) of the tile that set them off. Tag 0 is
    no tag, so an untagged switch's actions find nothing. The switch's own tile is
    never a target, though it carries the same tag, except for A_ChangeTexture,
    which changes it, tagged or not.

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

        // A_MoveWall([direction], ["moving sound"], ["blocked sound"]): pushes every tagged
        // wall north, east, south or west, or left out (or ""), the way the player faces. The
        // blocked sound plays when none of them could move. Their tag goes with them, so they
        // can be moved again.
        MapTriggerRegistry.Register("A_MoveWall", MoveWallAction);

        // A_SetWall(id): turns the tagged walls into wall id (walls.yaml), e.g. lights on a
        // panel coming on
        MapTriggerRegistry.Register("A_SetWall", SetWallAction);

        // A_ChangeTexture("FROM", "TO"): the switch's own tile and the tagged ones show texture TO
        // wherever they showed FROM: wall and door faces, floors and ceilings. TO can be an animation
        // (animdefs); one that plays once starts as it's changed. Changing it back takes another
        // A_ChangeTexture("TO", "FROM").
        MapTriggerRegistry.Register("A_ChangeTexture", ChangeTextureAction);

        // A_Activate / A_Deactivate: the tagged actors go to their Active / Inactive state
        // (actordefs), e.g. a light coming on; actors without that state are left alone
        MapTriggerRegistry.Register("A_Activate", (trigger, _) => SetTaggedActorsState(trigger, "Active"));
        MapTriggerRegistry.Register("A_Deactivate", (trigger, _) => SetTaggedActorsState(trigger, "Inactive"));
        // A_ToggleActive: each tagged actor on its Inactive state (or one named Inactive...,
        // such as an InactiveIdle it starts off on) goes Active, the rest Inactive
        MapTriggerRegistry.Register("A_ToggleActive", (trigger, _) =>
        {
            bool any = false;
            foreach (var actor in _mapManager.TaggedActors(trigger.Tag).ToList())
            {
                bool inactive = actor.CurrentState?.StateName?.StartsWith("Inactive", StringComparison.Ordinal) == true;
                var stateName = inactive ? "Active" : "Inactive";
                if (actor.IsRemoved || !actor.ResolvedStates.TryGetValue(stateName, out var frame))
                    continue;
                actor.JumpTo(frame);
                any = true;
            }
            return any;
        });

        // A_Message("text"[, "style"]): shows a message (a $NAME language key or the text), in
        // the style for locked doors unless one is named
        MapTriggerRegistry.Register("A_Message", (_, args) =>
        {
            if (args.Length == 0)
                return false;
            _hudMessageManager.Show(Managers.HudMessageKind.Lock, args[0], args.ElementAtOrDefault(1));
            return true;
        });

        // A_Vend("FoodToken", "FoodUnitMeal", "no-money message", "full message"[, "out-of-order
        // message", uses code]): a vending machine (Blake Stone's food units). The item would do
        // nothing (health full): the full message. No currency: the no-money message. With a uses
        // code (the high byte of the switch tile's object-plane value, whose low byte is the uses
        // left), none left or no such value: the out-of-order message. Each with the player's
        // use-fail sound. Otherwise one currency is taken and the item given, with its pickup
        // sound and message.
        MapTriggerRegistry.Register("A_Vend", VendAction);
    }

    private static bool VendAction(TriggerActivation trigger, string[] args)
    {
        if (args.Length < 2 || _inventoryManager.CreateActor(args[1]) is not Entities.Actors.Inventory item)
        {
            Console.WriteLine($"A_Vend: needs a currency and an inventory item to give ('{args.ElementAtOrDefault(1)}' isn't one).");
            return false;
        }

        bool Refuse(string? message)
        {
            _audioManager.Play("player/usefail");
            if (!string.IsNullOrEmpty(message))
                _hudMessageManager.Show(Managers.HudMessageKind.Lock, message);
            return false;
        }

        // With a uses code, the machine's object-plane value (code << 8 | uses left) says how many
        // more times it works; any other value, or none left, and it's out of order
        int usesCode = int.TryParse(args.ElementAtOrDefault(5), out var code) ? code : -1;
        int spot = _mapManager.MAPSPOT(trigger.TileX, trigger.TileY, 1);
        int usesLeft = usesCode >= 0 && spot >> 8 == usesCode ? spot & 0xff : usesCode >= 0 ? 0 : int.MaxValue;
        if (usesLeft <= 0)
            return Refuse(args.ElementAtOrDefault(4));

        if (!CouldTakeInventory(item))
            return Refuse(args.ElementAtOrDefault(3));
        if (!_inventoryManager.Has(args[0]))
            return Refuse(args.ElementAtOrDefault(2));

        int amount = item.Properties.TryGetValue("inventory.amount", out var a) ? Convert.ToInt32(a) : 1;
        if (!TryApplyInventory(item, amount))
            return Refuse(args.ElementAtOrDefault(3));

        _inventoryManager.Take(args[0], 1);
        if (usesCode >= 0)
            _mapManager.SetMapSpot(trigger.TileX, trigger.TileY, 1, (ushort)(usesCode << 8 | (usesLeft - 1)));
        if (item.Properties.TryGetValue("inventory.pickupsound", out var sound) && sound?.ToString() is { Length: > 0 } soundName)
            _audioManager.Play(soundName);
        if (item.Properties.TryGetValue("inventory.pickupmessage", out var message))
            _hudMessageManager.Show(Managers.HudMessageKind.Pickup, message?.ToString(),
                item.Properties.TryGetValue("inventory.pickupmessagestyle", out var style) ? style?.ToString() : null);
        return true;
    }

    private static bool SetTaggedActorsState(TriggerActivation trigger, string stateName)
    {
        bool any = false;
        // Listed first: a state's action could spawn or remove actors
        foreach (var actor in _mapManager.TaggedActors(trigger.Tag).ToList())
        {
            if (actor.IsRemoved || !actor.ResolvedStates.TryGetValue(stateName, out var frame))
                continue;
            actor.JumpTo(frame);
            any = true;
        }
        return any;
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

        // every tagged wall moves together; one sound for the lot
        bool moved = false, any = false;
        foreach (var (x, y) in TaggedWalls(trigger).ToList())
        {
            any = true;
            moved |= PushWall(x, y, dir);
        }

        var sound = moved ? args.ElementAtOrDefault(1) : any ? args.ElementAtOrDefault(2) : null;
        if (!string.IsNullOrEmpty(sound))
            _audioManager.Play(sound);
        return moved;
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
            _mapManager.RestartAnimations(x, y, gamestate.TimeCount);
            any = true;
        }
        return any;
    }

    private static bool ChangeTextureAction(TriggerActivation trigger, string[] args)
    {
        var from = args.ElementAtOrDefault(0);
        var to = args.ElementAtOrDefault(1);
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            Console.WriteLine("A_ChangeTexture: needs the texture to change and the one to change it to.");
            return false;
        }

        // The switch's own tile, then the others with its tag: those showing FROM anywhere
        var tiles = new List<(int X, int Y)> { (trigger.TileX, trigger.TileY) };
        if (trigger.Tag != 0)
            tiles.AddRange(_mapManager.TaggedTiles(trigger.Tag).Where(t => t != (trigger.TileX, trigger.TileY)));

        bool any = false, flats = false;
        foreach (var (x, y) in tiles)
        {
            var (walls, floor, ceiling) = TileTextures(x, y);
            bool onFlat = Shows(floor, from) || Shows(ceiling, from);
            if (!onFlat && !walls.Any(name => Shows(name, from)))
                continue;

            _mapManager.ChangeTexture(x, y, from, to, gamestate.TimeCount);
            any = true;
            flats |= onFlat;
        }

        if (flats)
            _mapManager.RebuildFlats();
        return any;
    }

    private static bool Shows(string? name, string texture) => name != null && name.Equals(texture, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The textures a tile shows now (through any A_ChangeTexture on it): its wall's faces, or its
    /// door's (and its locked ones), and its floor and ceiling
    /// </summary>
    private static (IEnumerable<string> Walls, string? Floor, string? Ceiling) TileTextures(int x, int y)
    {
        var tile = _mapManager.tilemap[x, y];
        MapTextureTranslation? faces = null;
        MapTextureTranslation? locked = null;
        if ((tile & BIT_DOOR) != 0)
        {
            var door = doorobjlist[tile & ~BIT_DOOR];
            faces = door.xlat;
            locked = door.xlat?.Locked;
        }
        else if ((tile & ~BIT_WALL) != 0)
            _mapManager.GetMapData().Walls.TryGetValue(tile & ~BIT_WALL, out faces);

        var names = new[] { faces, locked }
            .Where(f => f != null)
            .SelectMany(f => new[] { f!.North, f.South, f.East, f.West })
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => _mapManager.ChangedTexture(x, y, name));
        if (_mapManager.GetDiagonal(x, y)?.Texture is { Length: > 0 } diagonal)
            names = names.Append(_mapManager.ChangedTexture(x, y, diagonal));

        return (names, _mapManager.FlatTextureName(x, y, ceiling: false), _mapManager.FlatTextureName(x, y, ceiling: true));
    }
}
