using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Gameplay;

/// <summary>What the doors need from the rest of the game</summary>
/// <param name="PlaySoundAt">Plays a sound from a tile, heard by how far it is from the player</param>
/// <param name="DrawKeys">Draws the keys on the status bar again (after a takes-key door uses one)</param>
internal sealed record DoorHooks(Action<string, int, int> PlaySoundAt, Action DrawKeys);

/*
=============================================================================

                                  DOORS

DoorObjList[] holds most of the information for the doors

door->position holds the amount the door is open, ranging from 0 to 0xffff
        this is directly accessed by AsmRefresh during rendering

The number of doors is limited to 64 because a spot in tilemap holds the
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
        doorobj.TileX = (sbyte)tilex;
        doorobj.TileY = (sbyte)tiley;
        doorobj.Vertical = vertical;
        doorobj.Action = DoorAction.Closed;
        doorobj.Xlat = doorXlat;
        // A mapdefs door-locks object on the door's tile locks it in place of its own lock
        doorobj.MapLock = map.GetMapData().DoorLocks.TryGetValue(map.MapSpot(tilex, tiley, 1), out var tileLock)
            ? tileLock
            : doorXlat.Lock;
        DoorObjList[LastDoorObj] = doorobj;

        map.actorat[tilex, tiley] = new Door(DoorNum);// (uint)(DoorNum | GameConstants.BitDoor);   // consider it a solid wall

        //
        // make the door tile a special tile, and mark the adjacent tiles
        // for door sides
        //
        map.tilemap[tilex, tiley] = (ushort)(DoorNum | GameConstants.BitDoor);
        //map = &MapSpot(TileX, TileY, 0);
        if (vertical)
        {
            var map_areanum = map.MapSpot(tilex-1, tiley, 0);
            map.SetMapSpot(tilex, tiley, 0, (ushort)map_areanum); // set area number
            MarkDoorSide(tilex, tiley - 1);
            MarkDoorSide(tilex, tiley + 1);
        }
        else
        {
            var map_areanum = map.MapSpot(tilex, tiley-1, 0);
            map.SetMapSpot(tilex, tiley, 0, (ushort)map_areanum);
            MarkDoorSide(tilex - 1, tiley);
            MarkDoorSide(tilex + 1, tiley);
        }

        DoorNum++;
        LastDoorObj++;
    }

    /// <summary>
    /// Marks a wall beside a door (BitWall) so it's drawn with the door-side texture. Only a wall
    /// is marked: on open floor the mark would read as a moving pushwall, and on a door spawned
    /// before this one it would change that door's number.
    /// </summary>
    private void MarkDoorSide(int x, int y)
    {
        if (x < 0 || y < 0 || x >= MapManager.MapSize || y >= MapManager.MapSize)
            return;
        if (map.tilemap[x, y] is > 0 and < GameConstants.BitWall)
            map.tilemap[x, y] |= GameConstants.BitWall;
    }

    //===========================================================================

    /*
    =====================
    =
    = OpenDoor
    =
    =====================
    */
    internal void OpenDoor(int door)
    {
        if (DoorObjList[door].Action == (byte)DoorAction.Open)
            DoorObjList[door].TicCount = 0;         // reset open time
        else
            DoorObjList[door].Action = DoorAction.Opening;  // start it opening
    }

    /*
    =====================
    =
    = CloseDoor
    =
    =====================
    */

    internal void CloseDoor(int door)
    {
        int tilex, tiley, area;

        //
        // don't close on anything solid
        //
        tilex = DoorObjList[door].TileX;
        tiley = DoorObjList[door].TileY;

        if (map.actorat[tilex, tiley] is Actor)     // an enemy (or its corpse) in the doorway
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


        //
        // play door sound if in a connected area
        //
        area = map.MapSpot(tilex, tiley, 0) - map.Floors.AreaTile;

        if (session.Areas.AreaByPlayer[area] != 0 && DoorObjList[door].Xlat.CloseSound is { Length: > 0 } closeSound)
        {
            hooks.PlaySoundAt(closeSound, DoorObjList[door].TileX, DoorObjList[door].TileY); // JAB
        }

        DoorObjList[door].Action = DoorAction.Closing;
        DoorObjList[door].Held = false;     // closing lets go of a switch's hold
        //
        // make the door space solid
        //
        map.actorat[tilex, tiley] = new Door(door);// (uint)(door | GameConstants.BitDoor);
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
        // door's mapdef entry (doors.yaml `lock:`)
        var lockItem = DoorObjList[door].Lock;
        if (!string.IsNullOrEmpty(lockItem))
        {
            if (!inventory.Has(lockItem))
            {
                if (DoorObjList[door].Position == 0)
                    RefuseLocked(lockItem, xlat.LockedSound, xlat.LockMessage, xlat.LockMessageStyle);
                return;
            }

            // A takes-key door uses the item up and stays unlocked
            if (xlat.TakesKey)
            {
                inventory.Take(lockItem, 1);
                DoorObjList[door].Unlocked = true;
                hooks.DrawKeys();
            }
        }

        switch (DoorObjList[door].Action)
        {
            case DoorAction.Closed:
            case DoorAction.Closing:
                OpenDoor(door);
                break;
            case DoorAction.Open:
            case DoorAction.Opening:
                CloseDoor(door);
                break;
        }
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
        if (!string.IsNullOrEmpty(door.Lock) && xlat.Locked != null)
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
        if (DoorObjList[door].Held)
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
        uint area1, area2;
        //word* map;
        int position;

        position = DoorObjList[door].Position;
        if (position == 0)
        {
            //
            // door is just starting to open, so connect the areas
            //
            var door_tilex = DoorObjList[door].TileX;
            var door_tiley = DoorObjList[door].TileY;

            if (DoorObjList[door].Vertical)
            {
                area1 = (uint)map.MapSpot(door_tilex + 1, door_tiley, 0);
                area2 = (uint)map.MapSpot(door_tilex - 1, door_tiley, 0);
            }
            else
            {
                area1 = (uint)map.MapSpot(door_tilex, door_tiley - 1, 0);
                area2 = (uint)map.MapSpot(door_tilex, door_tiley + 1, 0);
            }
            area1 -= (uint)map.Floors.AreaTile;
            area2 -= (uint)map.Floors.AreaTile;

            if (area1 < map.Floors.NumAreas && area2 < map.Floors.NumAreas)
            {
                session.Areas.AreaConnect[area1, area2]++;
                session.Areas.AreaConnect[area2, area1]++;

                if (map.Players.Any(pawn => pawn.AreaNumber < map.Floors.NumAreas))
                    ConnectAreas();

                if (session.Areas.AreaByPlayer[area1] != 0 && DoorObjList[door].Xlat.OpenSound is { Length: > 0 } openSound)
                {
                    hooks.PlaySoundAt(openSound, DoorObjList[door].TileX, DoorObjList[door].TileY);  // JAB
                }
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
            map.actorat[DoorObjList[door].TileX, DoorObjList[door].TileY] = null;
        }

        DoorObjList[door].Position = (ushort)position;
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
        uint area1, area2;
        int position;
        int tilex, tiley;

        tilex = DoorObjList[door].TileX;
        tiley = DoorObjList[door].TileY;

        if ((map.actorat[tilex, tiley] is not Door)//!= (door | GameConstants.BitDoor))
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

            var door_tilex = DoorObjList[door].TileX;
            var door_tiley = DoorObjList[door].TileY;

            if (DoorObjList[door].Vertical)
            {
                area1 = (uint)map.MapSpot(door_tilex + 1, door_tiley, 0);
                area2 = (uint)map.MapSpot(door_tilex - 1, door_tiley, 0);
            }
            else
            {
                area1 = (uint)map.MapSpot(door_tilex, door_tiley - 1, 0);
                area2 = (uint)map.MapSpot(door_tilex, door_tiley + 1, 0);
            }

            area1 -= (uint)map.Floors.AreaTile;
            area2 -= (uint)map.Floors.AreaTile;

            if (area1 < map.Floors.NumAreas && area2 < map.Floors.NumAreas)
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
