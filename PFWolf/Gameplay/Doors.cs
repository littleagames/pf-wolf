using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Gameplay;

/// <summary>What the doors need from the rest of the game</summary>
/// <param name="PlaySoundAt">Plays a sound from a tile, heard by how far it is from the player</param>
/// <param name="DrawKeys">Draws the keys on the status bar again (after a takes-key door uses one)</param>
/// <param name="MayMove">Whether the scripts let a door (its tile) open (true) or close, set going by what (game.ondoor)</param>
internal sealed record DoorHooks(Action<string, int, int> PlaySoundAt, Action DrawKeys,
    Func<int, int, bool, Entities.Actors.Actor?, bool>? MayMove = null);

/*
=============================================================================

                                  DOORS

DoorObjList[] holds most of the information for the doors

door->position holds the amount the door is open, ranging from 0 to 0xffff
        this is directly accessed by AsmRefresh during rendering

The number of doors is limited to 64 because a spot in TileMap holds the
        door number in the low 6 bits, with the high bit meaning a door center
        and bit 6 meaning a door side tile

Open doors conect two areas, so sounds will travel between them and sight
        will be checked when the player is in a connected area.

Areaconnect is incremented/decremented by each door. If >0 they connect

Every time a door opens or closes the session.areas.AreaByPlayer matrix gets recalculated.
        An area is true if it connects with the player's current spor.

=============================================================================
*/

/// <summary>The level's doors: spawned from the map, opened and closed, locked, and moved each tic</summary>
internal sealed class Doors(GameEngineManager engine, AudioManager audio, MapManager map, InventoryManager inventory,
    HudMessageManager messages, GameSession session, DoorHooks hooks)
{
    internal const int DoorWidth = 0x7800;
    internal const int OpenTics = 300;

    internal DoorObject[] DoorObjList = new DoorObject[GameConstants.MaxDoors];
    internal int LastDoorObj; // index
    internal short DoorNum;

    /*
    ===============
    =
    = InitDoorList
    =
    ===============
    */
    /// <summary>Works out again which areas are connected to the players' (with several players, to any of theirs)</summary>
    internal void ConnectAreas() => session.Areas.ConnectAreas(map.Players.Select(pawn => (int)pawn.AreaNumber), map.Floors.NumAreas);

    internal void InitDoorList()
    {
        session.Areas.Reset(map.Floors.NumAreas);

        LastDoorObj = 0;
        DoorNum = 0;
    }

    /*
    ===============
    =
    = SpawnDoor
    =
    ===============
    */
    internal void SpawnDoor(int tilex, int tiley, bool vertical, MapTextureTranslation doorXlat)
    {
        if (DoorNum == GameConstants.MaxDoors)
            engine.Quit($"{GameConstants.MaxDoors}+ doors on level!");

        var doorobj = new DoorObject();
        doorobj.Position = 0;              // doors start out fully closed
        doorobj.TileX = (byte)tilex;
        doorobj.TileY = (byte)tiley;
        doorobj.Vertical = vertical;
        doorobj.Action = DoorAction.Closed;
        doorobj.Xlat = doorXlat;
        // A mapdefs door-locks object on the door's tile locks it in place of its own lock(s)
        if (map.GetMapData().DoorLocks.TryGetValue(map.MapSpot(tilex, tiley, 1), out var tileLock))
            doorobj.MapLock = tileLock;
        else
        {
            doorobj.MapLock = doorXlat.Lock;
            doorobj.MapLocks = doorXlat.Locks;
            doorobj.LocksNeeded = doorXlat.LocksNeeded;
        }

        // A linked door's object-plane value that isn't its card is the next door's tile (bstone's CheckLinkedDoors)
        int value = map.MapSpot(tilex, tiley, 1);
        if (doorXlat.Link.Equals("object-plane", StringComparison.OrdinalIgnoreCase) && value != 0
            && !map.GetMapData().DoorLocks.ContainsKey(value))
            (doorobj.LinkX, doorobj.LinkY) = (value >> 8, value & 0xff);
        DoorObjList[LastDoorObj] = doorobj;

        map.ActorAt[tilex, tiley] = new Door(DoorNum);// (uint)(DoorNum | GameConstants.BitDoor);   // consider it a solid wall

        //
        // make the door tile a special tile, and mark the adjacent tiles
        // for door sides
        //
        map.TileMap[tilex, tiley] = (ushort)(DoorNum | GameConstants.BitDoor);
        //map = &MapSpot(TileX, TileY, 0);
        // The door's tile takes the area west (or north) of it: a real area, as bstone's
        // GetAreaNumber finds one, not a floor code such as a trigger beside it
        var (sideX, sideY) = vertical ? (tilex - 1, tiley) : (tilex, tiley - 1);
        map.SetMapSpot(tilex, tiley, 0, (ushort)(map.Floors.AreaTile + Math.Max(AreaNumberAt(sideX, sideY), 0)));
        if (vertical)
        {
            MarkDoorSide(tilex, tiley - 1);
            MarkDoorSide(tilex, tiley + 1);
        }
        else
        {
            MarkDoorSide(tilex - 1, tiley);
            MarkDoorSide(tilex + 1, tiley);
        }

        DoorNum++;
        LastDoorObj++;
    }

    // Where bstone's GetAreaNumber looks when a tile isn't an area itself: beside it, then corners
    static readonly (int X, int Y)[] AreaSearch = [(0, -1), (0, 1), (-1, 0), (1, 0), (-1, -1), (1, 1), (-1, 1), (1, -1)];

    /// <summary>
    /// The area a tile beside a door is in (bstone's GetAreaNumber): its own area code, else the
    /// first of the tiles round it that has one, else area 0.
    /// -1 for a wall or a tile off the map. Floor codes that aren't areas (Blake Stone's door
    /// triggers, an ambush tile) count as none, so a door beside one still finds its area.
    /// </summary>
    internal int AreaNumberAt(int x, int y)
    {
        if (!map.InMap(x, y) || map.TileMap[x, y] is > 0 and < GameConstants.BitDoor)
            return -1;
        if (AreaCodeAt(x, y) is { } area)
            return area;
        foreach (var (dx, dy) in AreaSearch)
            if (map.InMap(x + dx, y + dy) && AreaCodeAt(x + dx, y + dy) is { } near)
                return near;
        return 0;
    }

    // A tile's area from its floor code, or null when the code isn't an area's
    int? AreaCodeAt(int x, int y) =>
        map.MapSpot(x, y, 0) is var code && map.ValidArea(code) ? code - map.Floors.AreaTile : null;

    /// <summary>
    /// Marks a wall beside a door (BitWall) so it's drawn with the door-side texture. Only a wall
    /// is marked: on open floor the mark would read as a moving pushwall, and on a door spawned
    /// before this one it would change that door's number.
    /// </summary>
    private void MarkDoorSide(int x, int y)
    {
        if (!map.InMap(x, y))
            return;
        if (map.TileMap[x, y] is > 0 and < GameConstants.BitWall)
            map.TileMap[x, y] |= GameConstants.BitWall;
    }

    //===========================================================================

    /*
    =====================
    =
    = OpenDoor
    =
    =====================
    */
    /// <param name="by">What set it going: the player using it, an enemy walking through; null for a switch or a linked door</param>
    internal void OpenDoor(int door, Entities.Actors.Actor? by = null)
    {
        if (DoorObjList[door].Jammed)
            return;
        if (DoorObjList[door].Action == (byte)DoorAction.Open)
            DoorObjList[door].TicCount = 0;         // reset open time
        else if (DoorObjList[door].Action != DoorAction.Opening)
        {
            if (hooks?.MayMove?.Invoke(DoorObjList[door].TileX, DoorObjList[door].TileY, true, by) == false)
                return;                             // a script keeps it shut (game.ondoor)
            DoorObjList[door].Action = DoorAction.Opening;  // start it opening
        }
        WorkLinkedDoor(door, open: true);
    }

    // The doors a chain of linked doors has worked so far, so a chain that loops stops; null outside one
    private HashSet<int>? _linkedWorked;

    /// <summary>
    /// Opens or closes the next door of a linked door's chain (doors.yaml link: object-plane),
    /// unlocking it, which works the one after it in turn, as bstone's CheckLinkedDoors: each
    /// door once, however the chain loops
    /// </summary>
    private void WorkLinkedDoor(int door, bool open)
    {
        var from = DoorObjList[door];
        if (from.LinkX <= 0 || from.LinkY <= 0 || !map.InMap(from.LinkX, from.LinkY))
            return;
        int tile = map.TileMap[from.LinkX, from.LinkY];
        if ((tile & GameConstants.BitDoor) == 0 || (tile & ~GameConstants.BitDoor) is var next && next >= LastDoorObj)
            return;

        bool first = _linkedWorked == null;
        _linkedWorked ??= [door];
        try
        {
            if (!_linkedWorked.Add(next))
                return;
            DoorObjList[next].Unlocked = true;
            if (open)
                OpenDoor(next);
            else
                CloseDoor(next);
        }
        finally
        {
            if (first)
                _linkedWorked = null;
        }
    }

    /*
    =====================
    =
    = CloseDoor
    =
    =====================
    */

    /// <param name="by">What set it going: the player using it; null for its own closing, a switch or a linked door</param>
    internal void CloseDoor(int door, Entities.Actors.Actor? by = null)
    {
        int tilex, tiley, area;

        //
        // don't close on anything solid
        //
        tilex = DoorObjList[door].TileX;
        tiley = DoorObjList[door].TileY;

        if (DoorObjList[door].Jammed)               // blown open for good
            return;
        if (map.ActorAt[tilex, tiley] is Actor)     // an enemy (or its corpse) in the doorway
            return;

        // any player in the doorway, or close enough to reach into it
        foreach (var pawn in map.Players)
        {
            if (pawn.TileX == tilex && pawn.TileY == tiley)
                return;

            if (DoorObjList[door].Vertical)
            {
                if (pawn.TileY == tiley && (((pawn.X + GameConstants.MinDist) >> MapConstants.TileShift) == tilex
                    || ((pawn.X - GameConstants.MinDist) >> MapConstants.TileShift) == tilex))
                    return;
            }
            else if (pawn.TileX == tilex && (((pawn.Y + GameConstants.MinDist) >> MapConstants.TileShift) == tiley
                || ((pawn.Y - GameConstants.MinDist) >> MapConstants.TileShift) == tiley))
                return;
        }

        if (DoorObjList[door].Vertical)
        {
            // an enemy marked on a neighbouring tile, close enough to reach into the doorway
            if (map.ActorMarkAt(tilex - 1, tiley) is { } west && ((west.X + GameConstants.MinDist) >> MapConstants.TileShift) == tilex)
                return;
            if (map.ActorMarkAt(tilex + 1, tiley) is { } east && ((east.X - GameConstants.MinDist) >> MapConstants.TileShift) == tilex)
                return;
        }
        else
        {
            if (map.ActorMarkAt(tilex, tiley - 1) is { } north && ((north.Y + GameConstants.MinDist) >> MapConstants.TileShift) == tiley)
                return;
            if (map.ActorMarkAt(tilex, tiley + 1) is { } south && ((south.Y - GameConstants.MinDist) >> MapConstants.TileShift) == tiley)
                return;
        }


        // A script may keep it open (game.ondoor)
        if (DoorObjList[door].Action is DoorAction.Open or DoorAction.Opening
            && hooks?.MayMove?.Invoke(tilex, tiley, false, by) == false)
            return;

        //
        // play door sound if in a connected area
        //
        area = AreaCodeAt(tilex, tiley) ?? -1;

        if (area >= 0 && session.Areas.AreaByPlayer[area] != 0 && DoorObjList[door].Xlat.CloseSound is { Length: > 0 } closeSound)
        {
            hooks.PlaySoundAt(closeSound, DoorObjList[door].TileX, DoorObjList[door].TileY); // JAB
        }

        DoorObjList[door].Action = DoorAction.Closing;
        DoorObjList[door].Held = false;     // closing lets go of a switch's hold
        //
        // make the door space solid
        //
        map.ActorAt[tilex, tiley] = new Door(door);// (uint)(door | GameConstants.BitDoor);
        WorkLinkedDoor(door, open: false);
    }

    /*
    =====================
    =
    = OperateDoor
    =
    = The player wants to change the door's direction
    =
    =====================
    */

    internal void OperateDoor(int door)
    {
        var xlat = DoorObjList[door].Xlat;

        // A one-way door only opens from its opens-from side
        if (!OnOpeningSide(DoorObjList[door]))
        {
            if (DoorObjList[door].Action == DoorAction.Closed)
            {
                if (xlat.WrongSideSound is { Length: > 0 } sound)
                    audio.Play(sound);
                if (xlat.WrongSideMessage is { Length: > 0 } message)
                    messages.Show(HudMessageKind.Lock, message, xlat.LockMessageStyle is { Length: > 0 } style ? style : null);
            }
            return;
        }

        // The required key comes from the map: a door-locks object on the door's tile, else the
        // door's mapdef entry (doors.yaml `lock:`, and `locks:` for several)
        if (DoorObjList[door].IsLocked)
        {
            var needed = ItemsToOpen(DoorObjList[door], out var missing);
            if (needed == null)
            {
                // Short of a door that takes only some of its locks, no one key's message fits
                var d = DoorObjList[door];
                var message = xlat.LockMessage.Length == 0 && missing != d.Lock && d.LocksRequired < d.MapLocks.Count
                    ? "$LOCKEDDOOR" : xlat.LockMessage;
                if (d.Position == 0)
                    RefuseLocked(missing, xlat.LockedSound, message, xlat.LockMessageStyle);
                return;
            }

            // A takes-key door uses the items up and stays unlocked
            if (xlat.TakesKey)
            {
                foreach (var item in needed)
                    inventory.Take(item, 1);
                DoorObjList[door].Unlocked = true;
                hooks.DrawKeys();
            }
        }

        switch (DoorObjList[door].Action)
        {
            case DoorAction.Closed:
            case DoorAction.Closing:
                OpenDoor(door, session.Player);
                break;
            case DoorAction.Open:
            case DoorAction.Opening:
                CloseDoor(door, session.Player);
                break;
        }
    }


    /// <summary>
    /// The items a locked door opens with from what the player carries: its lock, and the first
    /// <see cref="DoorObject.LocksRequired"/> of its locks they have. Null when they're short;
    /// <paramref name="missing"/> is then the first item they lack (its lock, else one of its
    /// locks), whose locked message and sound are used.
    /// </summary>
    internal List<string>? ItemsToOpen(DoorObject door, out string missing)
    {
        missing = "";
        var items = new List<string>();
        if (door.Lock.Length > 0)
        {
            if (!inventory.Has(door.Lock))
            {
                missing = door.Lock;
                return null;
            }
            items.Add(door.Lock);
        }

        int required = door.LocksRequired, carried = 0;
        foreach (var item in door.MapLocks)
        {
            if (carried == required)
                break;
            if (inventory.Has(item))
            {
                items.Add(item);
                carried++;
            }
            else if (missing.Length == 0)
                missing = item;
        }

        return carried < required ? null : items;
    }

    /// <summary>
    /// Whether the player is on a one-way door's opens-from side (or in line with it); always
    /// true for an ordinary door
    /// </summary>
    bool OnOpeningSide(DoorObject door) => door.Xlat.OpensFrom.ToLowerInvariant() switch
    {
        "east" => session.Player.TileX >= door.TileX,
        "west" => session.Player.TileX <= door.TileX,
        "south" => session.Player.TileY >= door.TileY,
        "north" => session.Player.TileY <= door.TileY,
        _ => true,
    };

    /// <summary>
    /// The texture on one face of a door (north, south, east or west): its locked face while
    /// it's locked, else its own; a face it doesn't give falls back to the one opposite.
    /// </summary>
    internal static string DoorFace(DoorObject door, string face)
    {
        var xlat = door.Xlat;
        if (door.IsLocked && xlat.Locked != null)
        {
            var locked = xlat.Locked.Side(face);
            if (locked.Length > 0)
                return locked;
        }

        return xlat.WallFace(face);
    }

    /// <summary>
    /// Refuses a locked door or switch the player lacks the lock item for. The sound is its own
    /// locked-sound (doors.yaml or the switch), else its lock item's `key.lockedsound`. The
    /// message is its own lock-message, else the lock item's `key.lockedmessage` (for a switch,
    /// `key.switchlockedmessage`), else that it's locked; the style is found the same way, then
    /// comes from game-info's for locked doors.
    /// </summary>
    internal void RefuseLocked(string lockItem, string lockedSound, string lockMessage, string lockMessageStyle, bool isSwitch = false)
    {
        var sound = !string.IsNullOrWhiteSpace(lockedSound) ? lockedSound
            : inventory.GetStringProperty(lockItem, "key.lockedsound");
        if (!string.IsNullOrEmpty(sound))
            audio.Play(sound);

        var message = !string.IsNullOrWhiteSpace(lockMessage) ? lockMessage
            : isSwitch ? inventory.GetStringProperty(lockItem, "key.switchlockedmessage") ?? "$LOCKEDSWITCH"
            : inventory.GetStringProperty(lockItem, "key.lockedmessage") ?? "$LOCKEDDOOR";
        var style = !string.IsNullOrWhiteSpace(lockMessageStyle) ? lockMessageStyle
            : inventory.GetStringProperty(lockItem, "key.lockedmessagestyle");
        messages.Show(HudMessageKind.Lock, message, style);
    }

    //===========================================================================

    /*
    ===============
    =
    = DoorOpen
    =
    = Close the door after three seconds, unless a switch holds it open
    =
    ===============
    */

    internal void DoorOpen(int door)
    {
        if (DoorObjList[door].Held || DoorObjList[door].Jammed)
            return;

        if ((DoorObjList[door].TicCount += (short)session.Tics) >= OpenTics)
            CloseDoor(door);
    }

    /*
    ===============
    =
    = DoorOpening
    =
    ===============
    */

    internal void DoorOpening(int door)
    {
        int position;

        position = DoorObjList[door].Position;
        if (position == 0)
        {
            //
            // door is just starting to open, so connect the areas
            //
            if (ConnectDoorAreas(door) is { } area1
                && session.Areas.AreaByPlayer[area1] != 0 && DoorObjList[door].Xlat.OpenSound is { Length: > 0 } openSound)
            {
                hooks.PlaySoundAt(openSound, DoorObjList[door].TileX, DoorObjList[door].TileY);  // JAB
            }
        }

        //
        // slide the door by an adaptive amount
        //
        position += (int)(session.Tics << 10);
        if (position >= 0xffff)
        {
            //
            // door is all the way open
            //
            position = 0xffff;
            DoorObjList[door].TicCount = 0;
            DoorObjList[door].Action = (byte)DoorAction.Open;
            map.ActorAt[DoorObjList[door].TileX, DoorObjList[door].TileY] = null;
        }

        DoorObjList[door].Position = (ushort)position;
    }

    /// <summary>
    /// Joins the areas either side of a door that's starting to open: the first of them, or null
    /// when either side isn't an area
    /// </summary>
    uint? ConnectDoorAreas(int door)
    {
        if (DoorAreas(door) is not var (area1, area2))
            return null;

        session.Areas.AreaConnect[area1, area2]++;
        session.Areas.AreaConnect[area2, area1]++;

        if (map.Players.Any(pawn => pawn.AreaNumber < map.Floors.NumAreas))
            ConnectAreas();
        return area1;
    }

    /// <summary>
    /// The areas either side of a door, east then west or north then south (bstone's
    /// doorobj areanumber pair, found as GetAreaNumber finds them); null when either side has none
    /// </summary>
    (uint, uint)? DoorAreas(int door)
    {
        int x = DoorObjList[door].TileX, y = DoorObjList[door].TileY;
        var (area1, area2) = DoorObjList[door].Vertical
            ? (AreaNumberAt(x + 1, y), AreaNumberAt(x - 1, y))
            : (AreaNumberAt(x, y - 1), AreaNumberAt(x, y + 1));
        return area1 < 0 || area2 < 0 ? null : ((uint)area1, (uint)area2);
    }

    /// <summary>
    /// Blows a door open for good, if it can be (bstone's TryBlastDoor): one that's at most half
    /// open, isn't locked and isn't one-way. It's open at once, unlocked, and never closes again.
    /// True when it was.
    /// </summary>
    internal bool BlastDoor(int door)
    {
        var d = DoorObjList[door];
        if (d.Jammed || d.IsLocked || d.Xlat.OpensFrom.Length > 0 || d.Position >= 0x8000)
            return false;

        // A door still shut has its areas joined now, as one starting to open does
        if (d.Position == 0)
            ConnectDoorAreas(door);

        d.Position = 0xffff;
        d.Action = DoorAction.Open;
        d.TicCount = 0;
        d.Held = false;
        d.Unlocked = true;
        d.Jammed = true;
        map.ActorAt[d.TileX, d.TileY] = null;
        return true;
    }

    /*
    ===============
    =
    = DoorClosing
    =
    ===============
    */

    internal void DoorClosing(int door)
    {
        int position;
        int tilex, tiley;

        tilex = DoorObjList[door].TileX;
        tiley = DoorObjList[door].TileY;

        if ((map.ActorAt[tilex, tiley] is not Door)//!= (door | GameConstants.BitDoor))
            || map.Players.Any(pawn => pawn.TileX == tilex && pawn.TileY == tiley))
        {                       // something got inside the door
            OpenDoor(door);
            return;
        }

        position = DoorObjList[door].Position;

        //
        // slide the door by an adaptive amount
        //
        position -= (int)(session.Tics << 10);
        if (position <= 0)
        {
            //
            // door is closed all the way, so disconnect the areas
            //
            position = 0;

            DoorObjList[door].Action = DoorAction.Closed;

            if (DoorAreas(door) is var (area1, area2))
            {
                session.Areas.AreaConnect[area1, area2]--;
                session.Areas.AreaConnect[area2, area1]--;

                if (map.Players.Any(pawn => pawn.AreaNumber < map.Floors.NumAreas))
                    ConnectAreas();
            }
        }

        DoorObjList[door].Position = (ushort)position;
    }


    /*
    =====================
    =
    = MoveDoors
    =
    = Called from PlayLoop
    =
    =====================
    */
    internal void MoveDoors()
    {
        int door;

        if (session.GameState.VictoryFlag)              // don't move door during victory sequence
            return;

        for (door = 0; door < DoorNum; door++)
        {
            switch (DoorObjList[door].Action)
            {
                case DoorAction.Open:
                    DoorOpen(door);
                    break;

                case DoorAction.Opening:
                    DoorOpening(door);
                    break;

                case DoorAction.Closing:
                    DoorClosing(door);
                    break;
            }
        }
    }
}
