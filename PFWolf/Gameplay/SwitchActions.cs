using PFWolf.Assets;
using PFWolf.Entities;

namespace PFWolf.Gameplay;

/// <summary>What the switch actions need from the player's code</summary>
/// <param name="CouldTakeInventory">Whether the player could take an item (it would do something)</param>
/// <param name="TryApplyInventory">Gives the player an item, so many of it: false if it did nothing</param>
internal sealed record SwitchHooks(Func<Entities.Actors.Inventory, bool> CouldTakeInventory, Func<Entities.Actors.Inventory, int, bool> TryApplyInventory);

internal sealed class SwitchActions(AudioManager audio, MapManager map, InventoryManager inventory, HudMessageManager messages,
    Doors doors, PushWalls pushWalls, GameSession session, SwitchHooks hooks)
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

    internal void Register()
    {
        // A_OpenDoor(["hold"]): opens the tagged doors; with hold they stay open until closed
        MapTriggerRegistry.Register("A_OpenDoor", (trigger, args) => ForTaggedDoors(trigger, door => SwitchOpenDoor(door, Hold(args))));
        // A_CloseDoor: closes the tagged doors (not on anything standing in the doorway)
        MapTriggerRegistry.Register("A_CloseDoor", (trigger, _) => ForTaggedDoors(trigger, doors.CloseDoor));
        // A_ToggleDoor(["hold"]): opens the tagged doors that are shut or shutting, closes the rest
        MapTriggerRegistry.Register("A_ToggleDoor", (trigger, args) => ForTaggedDoors(trigger, door =>
        {
            if (doors.DoorObjList[door].Action is DoorAction.Closed or DoorAction.Closing)
                SwitchOpenDoor(door, Hold(args));
            else
                doors.CloseDoor(door);
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
            foreach (var actor in map.TaggedActors(trigger.Tag).ToList())
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
            messages.Show(Managers.HudMessageKind.Lock, args[0], args.ElementAtOrDefault(1));
            return true;
        });
        Entities.Actors.ActorActionRegistry.Register("A_Message", (Entities.Actors.Actor _, string[] args) =>
        {
            if (args.Length > 0)
                messages.Show(Managers.HudMessageKind.Lock, args[0], args.ElementAtOrDefault(1));
        });

        // A_Vend("FoodToken", "FoodUnitMeal", "no-money message", "full message"[, "out-of-order
        // message", uses code]): a vending machine (Blake Stone's food units). The item would do
        // nothing (health full): the full message. No currency: the no-money message. With a uses
        // code (the high byte of the switch tile's object-plane value, whose low byte is the uses
        // left), none left or no such value: the out-of-order message. Each with the player's
        // use-fail sound. Otherwise one currency is taken and the item given, with its pickup
        // sound and message.
        MapTriggerRegistry.Register("A_Vend", VendAction);

        // A_GiveInventory("Item"[, amount]): gives the player who set it off an inventory item
        // (actordefs), as picking it up would, with its pickup sound and message; amount left out
        // is the item's own. Refused, with the use-fail sound, when it would do nothing (health
        // full, no room for the ammo), so a patch switch giving it stays as it is.
        MapTriggerRegistry.Register("A_GiveInventory", GiveInventoryTrigger);

        // A_SpawnItem("Class"): puts a thing (an item, or any actordefs class) on the floor in
        // front of the switch, on the side it was used from (from a script, where the player is)
        MapTriggerRegistry.Register("A_SpawnItem", SpawnItemTrigger);
    }

    private bool GiveInventoryTrigger(TriggerActivation trigger, string[] args)
    {
        if (args.Length == 0 || inventory.CreateActor(args[0]) is not Entities.Actors.Inventory item)
        {
            Console.WriteLine($"A_GiveInventory: '{args.ElementAtOrDefault(0)}' isn't an inventory item.");
            return false;
        }

        int amount = args.Length > 1 && int.TryParse(args[1], out var given) ? given
            : item.Properties.TryGetValue("inventory.amount", out var a) ? Convert.ToInt32(a) : 1;
        if (!hooks.CouldTakeInventory(item) || !hooks.TryApplyInventory(item, amount))
        {
            audio.Play("player/usefail");
            return false;
        }

        AnnouncePickup(item);
        return true;
    }

    private bool SpawnItemTrigger(TriggerActivation trigger, string[] args)
    {
        if (args.Length == 0)
            return false;

        // the floor the switch was used from: one tile back from it the way the player faced
        int x = trigger.Activator.TileX, y = trigger.Activator.TileY;
        if (trigger.TileX >= 0)
        {
            int dx = PushWalls.Dirs[(int)trigger.Dir][0], dy = PushWalls.Dirs[(int)trigger.Dir][1];
            (x, y) = (trigger.TileX - dx, trigger.TileY - dy);
        }

        if (map.SpawnThing(x, y, new MapActorTranslation { Class = args[0] }, countKill: false) == null)
        {
            Console.WriteLine($"A_SpawnItem: '{args[0]}' isn't an actor.");
            return false;
        }
        return true;
    }

    // An item's pickup sound and message, as the player takes it
    private void AnnouncePickup(Entities.Actors.Inventory item)
    {
        if (item.Properties.TryGetValue("inventory.pickupsound", out var sound) && sound?.ToString() is { Length: > 0 } soundName)
            audio.Play(soundName);
        if (item.Properties.TryGetValue("inventory.pickupmessage", out var message))
            messages.Show(Managers.HudMessageKind.Pickup, message?.ToString(),
                item.Properties.TryGetValue("inventory.pickupmessagestyle", out var style) ? style?.ToString() : null);
    }

    private bool VendAction(TriggerActivation trigger, string[] args)
    {
        if (args.Length < 2 || inventory.CreateActor(args[1]) is not Entities.Actors.Inventory item)
        {
            Console.WriteLine($"A_Vend: needs a currency and an inventory item to give ('{args.ElementAtOrDefault(1)}' isn't one).");
            return false;
        }

        bool Refuse(string? message)
        {
            audio.Play("player/usefail");
            if (!string.IsNullOrEmpty(message))
                messages.Show(Managers.HudMessageKind.Lock, message);
            return false;
        }

        // With a uses code, the machine's object-plane value (code << 8 | uses left) says how many
        // more times it works; any other value, or none left, and it's out of order
        int usesCode = int.TryParse(args.ElementAtOrDefault(5), out var code) ? code : -1;
        int spot = map.MapSpot(trigger.TileX, trigger.TileY, 1);
        int usesLeft = usesCode >= 0 && spot >> 8 == usesCode ? spot & 0xff : usesCode >= 0 ? 0 : int.MaxValue;
        if (usesLeft <= 0)
            return Refuse(args.ElementAtOrDefault(4));

        if (!hooks.CouldTakeInventory(item))
            return Refuse(args.ElementAtOrDefault(3));
        if (!inventory.Has(args[0]))
            return Refuse(args.ElementAtOrDefault(2));

        int amount = item.Properties.TryGetValue("inventory.amount", out var a) ? Convert.ToInt32(a) : 1;
        if (!hooks.TryApplyInventory(item, amount))
            return Refuse(args.ElementAtOrDefault(3));

        inventory.Take(args[0], 1);
        if (usesCode >= 0)
            map.SetMapSpot(trigger.TileX, trigger.TileY, 1, (ushort)(usesCode << 8 | (usesLeft - 1)));
        AnnouncePickup(item);
        return true;
    }

    private bool SetTaggedActorsState(TriggerActivation trigger, string stateName)
    {
        bool any = false;
        // Listed first: a state's action could spawn or remove actors
        foreach (var actor in map.TaggedActors(trigger.Tag).ToList())
        {
            if (actor.IsRemoved || !actor.ResolvedStates.TryGetValue(stateName, out var frame))
                continue;
            actor.JumpTo(frame);
            any = true;
        }
        return any;
    }

    internal static bool Hold(string[] args) => args.Any(a => a.Equals("hold", StringComparison.OrdinalIgnoreCase));

    // A switch opens a door whatever its lock: the lock is for opening it by hand (put a lock
    // on the switch to need a key for it)
    private void SwitchOpenDoor(int door, bool hold)
    {
        doors.OpenDoor(door);
        if (hold)
            doors.DoorObjList[door].Held = true;
    }

    /// <summary>Runs <paramref name="act"/> on each door on a tile with the trigger's tag; false if there are none.</summary>
    private bool ForTaggedDoors(TriggerActivation trigger, Action<int> act)
    {
        bool any = false;
        if (trigger.Tag == 0)
            return false;

        for (int door = 0; door < doors.LastDoorObj; door++)
        {
            if (map.GetTag(doors.DoorObjList[door].TileX, doors.DoorObjList[door].TileY) != trigger.Tag)
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
    private IEnumerable<(int X, int Y)> TaggedWalls(TriggerActivation trigger) =>
        map.TaggedTiles(trigger.Tag).Where(t =>
            (t.X, t.Y) != (trigger.TileX, trigger.TileY)
            && (map.TileMap[t.X, t.Y] & GameConstants.BitDoor) == 0
            && (map.TileMap[t.X, t.Y] & ~GameConstants.BitWall) != 0);

    /// <summary>A direction's name (north, east, south, west, any case), or null</summary>
    internal static ControlDir? ParseDirection(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "north" => ControlDir.North,
        "east" => ControlDir.East,
        "south" => ControlDir.South,
        "west" => ControlDir.West,
        _ => null,
    };

    /// <summary>
    /// A_MoveWall("direction", "moving sound", "blocked sound"[, object]): the walls with the
    /// switch's tag pushed that way (the way the player faces when left out). With an object-plane
    /// value, only the tagged walls with it on their tile move: a switch whose walls go different
    /// ways runs one A_MoveWall for each (Midnight Assault's 656-659 walls).
    /// </summary>
    private bool MoveWallAction(TriggerActivation trigger, string[] args)
    {
        var dirName = args.ElementAtOrDefault(0);
        ControlDir dir;
        if (string.IsNullOrWhiteSpace(dirName))
            dir = trigger.Dir;
        else if (ParseDirection(dirName) is { } named)
            dir = named;
        else
        {
            Console.WriteLine($"A_MoveWall: unknown direction '{dirName}' (north, east, south or west).");
            return false;
        }

        int? onlyObject = int.TryParse(args.ElementAtOrDefault(3), out var objectValue) ? objectValue : null;

        // every tagged wall moves together; one sound for the lot
        bool moved = false, any = false;
        foreach (var (x, y) in TaggedWalls(trigger).Where(t => onlyObject == null || map.MapSpot(t.X, t.Y, 1) == onlyObject).ToList())
        {
            any = true;
            moved |= pushWalls.PushWall(x, y, dir);
        }

        var sound = moved ? args.ElementAtOrDefault(1) : any ? args.ElementAtOrDefault(2) : null;
        if (!string.IsNullOrEmpty(sound))
            audio.Play(sound);
        return moved;
    }

    private bool SetWallAction(TriggerActivation trigger, string[] args)
    {
        if (!int.TryParse(args.ElementAtOrDefault(0), out var id) || id is <= 0 or >= GameConstants.BitWall
            || !map.GetMapData().Walls.ContainsKey(id))
        {
            Console.WriteLine($"A_SetWall: '{args.ElementAtOrDefault(0)}' isn't a walls.yaml wall id.");
            return false;
        }

        bool any = false;
        foreach (var (x, y) in TaggedWalls(trigger))
        {
            // keeping the door-side mark on a wall beside a door
            map.TileMap[x, y] = (ushort)(id | (map.TileMap[x, y] & GameConstants.BitWall));
            map.RestartAnimations(x, y, session.GameState.TimeCount);
            any = true;
        }
        return any;
    }

    private bool ChangeTextureAction(TriggerActivation trigger, string[] args)
    {
        var from = args.ElementAtOrDefault(0);
        var to = args.ElementAtOrDefault(1);
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            Console.WriteLine("A_ChangeTexture: needs the texture to change and the one to change it to.");
            return false;
        }

        // The switch's own tile (a script's game.trigger has none), then the others with its tag:
        // those showing FROM anywhere
        var tiles = new List<(int X, int Y)>();
        if (trigger.TileX >= 0 && trigger.TileY >= 0)
            tiles.Add((trigger.TileX, trigger.TileY));
        if (trigger.Tag != 0)
            tiles.AddRange(map.TaggedTiles(trigger.Tag).Where(t => t != (trigger.TileX, trigger.TileY)));

        bool any = false, flats = false;
        foreach (var (x, y) in tiles)
        {
            var (walls, floor, ceiling) = TileTextures(x, y);
            bool onFlat = Shows(floor, from) || Shows(ceiling, from);
            if (!onFlat && !walls.Any(name => Shows(name, from)))
                continue;

            map.ChangeTexture(x, y, from, to, session.GameState.TimeCount);
            any = true;
            flats |= onFlat;
        }

        if (flats)
            map.RebuildFlats();
        return any;
    }

    internal static bool Shows(string? name, string texture) => name != null && name.Equals(texture, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The textures a tile shows now (through any A_ChangeTexture on it): its wall's faces, or its
    /// door's (and its locked ones), and its floor and ceiling
    /// </summary>
    private (IEnumerable<string> Walls, string? Floor, string? Ceiling) TileTextures(int x, int y)
    {
        var tile = map.TileMap[x, y];
        MapTextureTranslation? faces = null;
        MapTextureTranslation? locked = null;
        if ((tile & GameConstants.BitDoor) != 0)
        {
            var door = doors.DoorObjList[tile & ~GameConstants.BitDoor];
            faces = door.Xlat;
            locked = door.Xlat?.Locked;
        }
        else if ((tile & ~GameConstants.BitWall) != 0)
            map.GetMapData().Walls.TryGetValue(tile & ~GameConstants.BitWall, out faces);

        var names = new[] { faces, locked }
            .Where(f => f != null)
            .SelectMany(f => new[] { f!.North, f.South, f.East, f.West })
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => map.ChangedTexture(x, y, name));
        if (map.GetDiagonal(x, y)?.Texture is { Length: > 0 } diagonal)
            names = names.Append(map.ChangedTexture(x, y, diagonal));

        return (names, map.FlatTextureName(x, y, ceiling: false), map.FlatTextureName(x, y, ceiling: true));
    }
}
