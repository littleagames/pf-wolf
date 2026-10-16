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

doorobjlist[] holds most of the information for the doors

door->position holds the amount the door is open, ranging from 0 to 0xffff
        this is directly accessed by AsmRefresh during rendering

The number of doors is limited to 64 because a spot in tilemap holds the
        door number in the low 6 bits, with the high bit meaning a door center
        and bit 6 meaning a door side tile

Open doors conect two areas, so sounds will travel between them and sight
        will be checked when the player is in a connected area.

Areaconnect is incremented/decremented by each door. If >0 they connect

Every time a door opens or closes the session.areas.areabyplayer matrix gets recalculated.
        An area is true if it connects with the player's current spor.

=============================================================================
*/

/// <summary>The level's doors: spawned from the map, opened and closed, locked, and moved each tic</summary>
internal sealed class Doors(GameEngineManager engine, AudioManager audio, MapManager map, InventoryManager inventory,
    HudMessageManager messages, GameSession session, DoorHooks hooks)
{
    internal const int DOORWIDTH = 0x7800;
    internal const int OPENTICS = 300;

    internal doorobj_t[] doorobjlist = new doorobj_t[GameConstants.MAXDOORS];
    internal int lastdoorobj; // index
    internal short doornum;

    /*
    ===============
    =
    = InitDoorList
    =
    ===============
    */
    /// <summary>Works out again which areas are connected to the players' (with several players, to any of theirs)</summary>
    internal void ConnectAreas() => session.areas.ConnectAreas(map.Players.Select(pawn => (int)pawn.AreaNumber), map.Floors.NumAreas);

    internal void InitDoorList()
    {
        session.areas.Reset(map.Floors.NumAreas);

        lastdoorobj = 0;
        doornum = 0;
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
        if (doornum == GameConstants.MAXDOORS)
            engine.Quit("64+ doors on level!");

        var doorobj = new doorobj_t();
        doorobj.position = 0;              // doors start out fully closed
        doorobj.tilex = (sbyte)tilex;
        doorobj.tiley = (sbyte)tiley;
        doorobj.vertical = vertical;
        doorobj.action = dooractiontypes.dr_closed;
        doorobj.xlat = doorXlat;
        // A mapdefs door-locks object on the door's tile locks it in place of its own lock
        doorobj.maplock = map.GetMapData().DoorLocks.TryGetValue(map.MAPSPOT(tilex, tiley, 1), out var tileLock)
            ? tileLock
            : doorXlat.Lock;
        doorobjlist[lastdoorobj] = doorobj;

        map.actorat[tilex, tiley] = new Door(doornum);// (uint)(doornum | GameConstants.BIT_DOOR);   // consider it a solid wall

        //
        // make the door tile a special tile, and mark the adjacent tiles
        // for door sides
        //
        map.tilemap[tilex, tiley] = (byte)(doornum | GameConstants.BIT_DOOR);
        //map = &MAPSPOT(tilex, tiley, 0);
        if (vertical)
        {
            var map_areanum = map.MAPSPOT(tilex-1, tiley, 0);
            map.SetMapSpot(tilex, tiley, 0, (ushort)map_areanum); // set area number
            map.tilemap[tilex, tiley - 1] |= GameConstants.BIT_WALL;
            map.tilemap[tilex, tiley + 1] |= GameConstants.BIT_WALL;
        }
        else
        {
            var map_areanum = map.MAPSPOT(tilex, tiley-1, 0);
            map.SetMapSpot(tilex, tiley, 0, (ushort)map_areanum);
            map.tilemap[tilex - 1, tiley] |= GameConstants.BIT_WALL;
            map.tilemap[tilex + 1, tiley] |= GameConstants.BIT_WALL;
        }

        doornum++;
        lastdoorobj++;
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
        if (doorobjlist[door].action == (byte)dooractiontypes.dr_open)
            doorobjlist[door].ticcount = 0;         // reset open time
        else
            doorobjlist[door].action = dooractiontypes.dr_opening;  // start it opening
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
        tilex = doorobjlist[door].tilex;
        tiley = doorobjlist[door].tiley;

        if (map.actorat[tilex, tiley] is Actor)     // an enemy (or its corpse) in the doorway
            return;

        // any player in the doorway, or close enough to reach into it
        foreach (var pawn in map.Players)
        {
            if (pawn.TileX == tilex && pawn.TileY == tiley)
                return;

            if (doorobjlist[door].vertical)
            {
                if (pawn.TileY == tiley && (((pawn.X + GameConstants.MINDIST) >> MapConstants.TILESHIFT) == tilex
                    || ((pawn.X - GameConstants.MINDIST) >> MapConstants.TILESHIFT) == tilex))
                    return;
            }
            else if (pawn.TileX == tilex && (((pawn.Y + GameConstants.MINDIST) >> MapConstants.TILESHIFT) == tiley
                || ((pawn.Y - GameConstants.MINDIST) >> MapConstants.TILESHIFT) == tiley))
                return;
        }

        if (doorobjlist[door].vertical)
        {
            // an enemy marked on a neighbouring tile, close enough to reach into the doorway
            if (map.ActorMarkAt(tilex - 1, tiley) is { } west && ((west.X + GameConstants.MINDIST) >> MapConstants.TILESHIFT) == tilex)
                return;
            if (map.ActorMarkAt(tilex + 1, tiley) is { } east && ((east.X - GameConstants.MINDIST) >> MapConstants.TILESHIFT) == tilex)
                return;
        }
        else
        {
            if (map.ActorMarkAt(tilex, tiley - 1) is { } north && ((north.Y + GameConstants.MINDIST) >> MapConstants.TILESHIFT) == tiley)
                return;
            if (map.ActorMarkAt(tilex, tiley + 1) is { } south && ((south.Y - GameConstants.MINDIST) >> MapConstants.TILESHIFT) == tiley)
                return;
        }


        //
        // play door sound if in a connected area
        //
        area = map.MAPSPOT(tilex, tiley, 0) - map.Floors.AreaTile;

        if (session.areas.areabyplayer[area] != 0 && doorobjlist[door].xlat.CloseSound is { Length: > 0 } closeSound)
        {
            hooks.PlaySoundAt(closeSound, doorobjlist[door].tilex, doorobjlist[door].tiley); // JAB
        }

        doorobjlist[door].action = dooractiontypes.dr_closing;
        doorobjlist[door].held = false;     // closing lets go of a switch's hold
        //
        // make the door space solid
        //
        map.actorat[tilex, tiley] = new Door(door);// (uint)(door | GameConstants.BIT_DOOR);
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
        var xlat = doorobjlist[door].xlat;

        // A one-way door only opens from its opens-from side
        if (!OnOpeningSide(doorobjlist[door]))
        {
            if (doorobjlist[door].action == dooractiontypes.dr_closed)
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
        var lockItem = doorobjlist[door].Lock;
        if (!string.IsNullOrEmpty(lockItem))
        {
            if (!inventory.Has(lockItem))
            {
                if (doorobjlist[door].position == 0)
                    RefuseLocked(lockItem, xlat.LockedSound, xlat.LockMessage, xlat.LockMessageStyle);
                return;
            }

            // A takes-key door uses the item up and stays unlocked
            if (xlat.TakesKey)
            {
                inventory.Take(lockItem, 1);
                doorobjlist[door].unlocked = true;
                hooks.DrawKeys();
            }
        }

        switch (doorobjlist[door].action)
        {
            case dooractiontypes.dr_closed:
            case dooractiontypes.dr_closing:
                OpenDoor(door);
                break;
            case dooractiontypes.dr_open:
            case dooractiontypes.dr_opening:
                CloseDoor(door);
                break;
        }
    }


    /// <summary>
    /// Whether the player is on a one-way door's opens-from side (or in line with it); always
    /// true for an ordinary door
    /// </summary>
    bool OnOpeningSide(doorobj_t door) => door.xlat.OpensFrom.ToLowerInvariant() switch
    {
        "east" => session.player.TileX >= door.tilex,
        "west" => session.player.TileX <= door.tilex,
        "south" => session.player.TileY >= door.tiley,
        "north" => session.player.TileY <= door.tiley,
        _ => true,
    };

    /// <summary>
    /// The texture on one face of a door (north, south, east or west): its locked face while
    /// it's locked, else its own; a face it doesn't give falls back to the one opposite.
    /// </summary>
    internal static string DoorFace(doorobj_t door, string face)
    {
        var xlat = door.xlat;
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
        if (doorobjlist[door].held)
            return;

        if ((doorobjlist[door].ticcount += (short)session.tics) >= OPENTICS)
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

        position = doorobjlist[door].position;
        if (position == 0)
        {
            //
            // door is just starting to open, so connect the areas
            //
            var door_tilex = doorobjlist[door].tilex;
            var door_tiley = doorobjlist[door].tiley;

            if (doorobjlist[door].vertical)
            {
                area1 = (uint)map.MAPSPOT(door_tilex + 1, door_tiley, 0);
                area2 = (uint)map.MAPSPOT(door_tilex - 1, door_tiley, 0);
            }
            else
            {
                area1 = (uint)map.MAPSPOT(door_tilex, door_tiley - 1, 0);
                area2 = (uint)map.MAPSPOT(door_tilex, door_tiley + 1, 0);
            }
            area1 -= (uint)map.Floors.AreaTile;
            area2 -= (uint)map.Floors.AreaTile;

            if (area1 < map.Floors.NumAreas && area2 < map.Floors.NumAreas)
            {
                session.areas.areaconnect[area1, area2]++;
                session.areas.areaconnect[area2, area1]++;

                if (map.Players.Any(pawn => pawn.AreaNumber < map.Floors.NumAreas))
                    ConnectAreas();

                if (session.areas.areabyplayer[area1] != 0 && doorobjlist[door].xlat.OpenSound is { Length: > 0 } openSound)
                {
                    hooks.PlaySoundAt(openSound, doorobjlist[door].tilex, doorobjlist[door].tiley);  // JAB
                }
            }
        }

        //
        // slide the door by an adaptive amount
        //
        position += (int)(session.tics << 10);
        if (position >= 0xffff)
        {
            //
            // door is all the way open
            //
            position = 0xffff;
            doorobjlist[door].ticcount = 0;
            doorobjlist[door].action = (byte)dooractiontypes.dr_open;
            map.actorat[doorobjlist[door].tilex, doorobjlist[door].tiley] = null;
        }

        doorobjlist[door].position = (ushort)position;
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

        tilex = doorobjlist[door].tilex;
        tiley = doorobjlist[door].tiley;

        if ((map.actorat[tilex, tiley] is not Door)//!= (door | GameConstants.BIT_DOOR))
            || map.Players.Any(pawn => pawn.TileX == tilex && pawn.TileY == tiley))
        {                       // something got inside the door
            OpenDoor(door);
            return;
        }

        position = doorobjlist[door].position;

        //
        // slide the door by an adaptive amount
        //
        position -= (int)(session.tics << 10);
        if (position <= 0)
        {
            //
            // door is closed all the way, so disconnect the areas
            //
            position = 0;

            doorobjlist[door].action = dooractiontypes.dr_closed;

            var door_tilex = doorobjlist[door].tilex;
            var door_tiley = doorobjlist[door].tiley;

            if (doorobjlist[door].vertical)
            {
                area1 = (uint)map.MAPSPOT(door_tilex + 1, door_tiley, 0);
                area2 = (uint)map.MAPSPOT(door_tilex - 1, door_tiley, 0);
            }
            else
            {
                area1 = (uint)map.MAPSPOT(door_tilex, door_tiley - 1, 0);
                area2 = (uint)map.MAPSPOT(door_tilex, door_tiley + 1, 0);
            }

            area1 -= (uint)map.Floors.AreaTile;
            area2 -= (uint)map.Floors.AreaTile;

            if (area1 < map.Floors.NumAreas && area2 < map.Floors.NumAreas)
            {
                session.areas.areaconnect[area1, area2]--;
                session.areas.areaconnect[area2, area1]--;

                if (map.Players.Any(pawn => pawn.AreaNumber < map.Floors.NumAreas))
                    ConnectAreas();
            }
        }

        doorobjlist[door].position = (ushort)position;
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

        if (session.gamestate.victoryflag)              // don't move door during victory sequence
            return;

        for (door = 0; door < doornum; door++)
        {
            switch (doorobjlist[door].action)
            {
                case dooractiontypes.dr_open:
                    DoorOpen(door);
                    break;

                case dooractiontypes.dr_opening:
                    DoorOpening(door);
                    break;

                case dooractiontypes.dr_closing:
                    DoorClosing(door);
                    break;
            }
        }
    }
}
