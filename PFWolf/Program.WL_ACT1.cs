using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    /*
    ===============
    =
    = PlaceItemType
    =
    = Called during game play to drop actors' items.  It finds the proper
    = item number based on the item type (bo_???).  If there are no free item
    = spots, nothing is done.
    =
    ===============
    */
    internal static void PlaceItemType(string item_class, int tilex, int tiley)
    {
        _mapManager.SpawnThing(tilex, tiley, item_class);
    }

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

    Every time a door opens or closes the areabyplayer matrix gets recalculated.
            An area is true if it connects with the player's current spor.

    =============================================================================
    */

    internal const int DOORWIDTH = 0x7800;
    internal const int OPENTICS = 300;

    internal static doorobj_t[] doorobjlist = new doorobj_t[MAXDOORS];
    internal static int lastdoorobj; // index
    internal static short doornum;

    // Sized to the mapdefs area count by InitDoorList
    internal static byte[,] areaconnect = new byte[0, 0];

    internal static byte[] areabyplayer = [];



    /*
    ==============
    =
    = ConnectAreas
    =
    = Scans outward from playerarea, marking all connected areas
    =
    ==============
    */
    internal static void RecursiveConnect(int areanumber)
    {
        int i;

        for (i = 0; i < _mapManager.Floors.NumAreas; i++)
        {
            if (areaconnect[areanumber, i] != 0 && areabyplayer[i] == 0)
            {
                areabyplayer[i] = 1; // true
                RecursiveConnect(i);
            }
        }
    }

    // With several players, an area is "by the player" when it's connected to any of theirs
    internal static void ConnectAreas()
    {
        Array.Fill(areabyplayer, (byte)0);
        foreach (var pawn in _mapManager.Players)
        {
            if (pawn.AreaNumber >= _mapManager.Floors.NumAreas)
                continue;
            areabyplayer[pawn.AreaNumber] = 1; // true
            RecursiveConnect(pawn.AreaNumber);
        }
    }

    internal static void InitAreas()
    {
        Array.Fill(areabyplayer, (byte)0);
        foreach (var pawn in _mapManager.Players)
        {
            if (pawn.AreaNumber < _mapManager.Floors.NumAreas)
                areabyplayer[pawn.AreaNumber] = 1; // true
        }
    }

    /*
    ===============
    =
    = InitDoorList
    =
    ===============
    */
    internal static void InitDoorList()
    {
        var numareas = _mapManager.Floors.NumAreas;
        areabyplayer = new byte[numareas];
        areaconnect = new byte[numareas, numareas];

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
    internal static void SpawnDoor(int tilex, int tiley, bool vertical, MapTextureTranslation doorXlat)
    {
        if (doornum == MAXDOORS)
            _gameEngineManager.Quit("64+ doors on level!");

        var doorobj = new doorobj_t();
        doorobj.position = 0;              // doors start out fully closed
        doorobj.tilex = (sbyte)tilex;
        doorobj.tiley = (sbyte)tiley;
        doorobj.vertical = vertical;
        doorobj.action = dooractiontypes.dr_closed;
        doorobj.xlat = doorXlat;
        // A mapdefs door-locks object on the door's tile locks it in place of its own lock
        doorobj.maplock = _mapManager.GetMapData().DoorLocks.TryGetValue(_mapManager.MAPSPOT(tilex, tiley, 1), out var tileLock)
            ? tileLock
            : doorXlat.Lock;
        doorobjlist[lastdoorobj] = doorobj;

        _mapManager.actorat[tilex, tiley] = new Door(doornum);// (uint)(doornum | BIT_DOOR);   // consider it a solid wall

        //
        // make the door tile a special tile, and mark the adjacent tiles
        // for door sides
        //
        _mapManager.tilemap[tilex, tiley] = (byte)(doornum | BIT_DOOR);
        //map = &MAPSPOT(tilex, tiley, 0);
        if (vertical)
        {
            var map_areanum = _mapManager.MAPSPOT(tilex-1, tiley, 0);
            _mapManager.SetMapSpot(tilex, tiley, 0, (ushort)map_areanum); // set area number
            _mapManager.tilemap[tilex, tiley - 1] |= BIT_WALL;
            _mapManager.tilemap[tilex, tiley + 1] |= BIT_WALL;
        }
        else
        {
            var map_areanum = _mapManager.MAPSPOT(tilex, tiley-1, 0);
            _mapManager.SetMapSpot(tilex, tiley, 0, (ushort)map_areanum);
            _mapManager.tilemap[tilex - 1, tiley] |= BIT_WALL;
            _mapManager.tilemap[tilex + 1, tiley] |= BIT_WALL;
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
    internal static void OpenDoor(int door)
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

    internal static void CloseDoor(int door)
    {
        int tilex, tiley, area;

        //
        // don't close on anything solid
        //
        tilex = doorobjlist[door].tilex;
        tiley = doorobjlist[door].tiley;

        if (_mapManager.actorat[tilex, tiley] is Actor)     // an enemy (or its corpse) in the doorway
            return;

        // any player in the doorway, or close enough to reach into it
        foreach (var pawn in _mapManager.Players)
        {
            if (pawn.TileX == tilex && pawn.TileY == tiley)
                return;

            if (doorobjlist[door].vertical)
            {
                if (pawn.TileY == tiley && (((pawn.X + MINDIST) >> MapConstants.TILESHIFT) == tilex
                    || ((pawn.X - MINDIST) >> MapConstants.TILESHIFT) == tilex))
                    return;
            }
            else if (pawn.TileX == tilex && (((pawn.Y + MINDIST) >> MapConstants.TILESHIFT) == tiley
                || ((pawn.Y - MINDIST) >> MapConstants.TILESHIFT) == tiley))
                return;
        }

        if (doorobjlist[door].vertical)
        {
            // an enemy marked on a neighbouring tile, close enough to reach into the doorway
            if (_mapManager.ActorMarkAt(tilex - 1, tiley) is { } west && ((west.X + MINDIST) >> MapConstants.TILESHIFT) == tilex)
                return;
            if (_mapManager.ActorMarkAt(tilex + 1, tiley) is { } east && ((east.X - MINDIST) >> MapConstants.TILESHIFT) == tilex)
                return;
        }
        else
        {
            if (_mapManager.ActorMarkAt(tilex, tiley - 1) is { } north && ((north.Y + MINDIST) >> MapConstants.TILESHIFT) == tiley)
                return;
            if (_mapManager.ActorMarkAt(tilex, tiley + 1) is { } south && ((south.Y - MINDIST) >> MapConstants.TILESHIFT) == tiley)
                return;
        }


        //
        // play door sound if in a connected area
        //
        area = _mapManager.MAPSPOT(tilex, tiley, 0) - _mapManager.Floors.AreaTile;

        if (areabyplayer[area] != 0 && doorobjlist[door].xlat.CloseSound is { Length: > 0 } closeSound)
        {
            PlaySoundLocTile(closeSound, doorobjlist[door].tilex, doorobjlist[door].tiley); // JAB
        }

        doorobjlist[door].action = dooractiontypes.dr_closing;
        doorobjlist[door].held = false;     // closing lets go of a switch's hold
        //
        // make the door space solid
        //
        _mapManager.actorat[tilex, tiley] = new Door(door);// (uint)(door | BIT_DOOR);
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

    internal static void OperateDoor(int door)
    {
        var xlat = doorobjlist[door].xlat;

        // A one-way door only opens from its opens-from side
        if (!OnOpeningSide(doorobjlist[door]))
        {
            if (doorobjlist[door].action == dooractiontypes.dr_closed)
            {
                if (xlat.WrongSideSound is { Length: > 0 } sound)
                    _audioManager.Play(sound);
                if (xlat.WrongSideMessage is { Length: > 0 } message)
                    _hudMessageManager.Show(HudMessageKind.Lock, message, xlat.LockMessageStyle is { Length: > 0 } style ? style : null);
            }
            return;
        }

        // The required key comes from the map: a door-locks object on the door's tile, else the
        // door's mapdef entry (doors.yaml `lock:`)
        var lockItem = doorobjlist[door].Lock;
        if (!string.IsNullOrEmpty(lockItem))
        {
            if (!_inventoryManager.Has(lockItem))
            {
                if (doorobjlist[door].position == 0)
                    RefuseLocked(lockItem, xlat.LockedSound, xlat.LockMessage, xlat.LockMessageStyle);
                return;
            }

            // A takes-key door uses the item up and stays unlocked
            if (xlat.TakesKey)
            {
                _inventoryManager.Take(lockItem, 1);
                doorobjlist[door].unlocked = true;
                DrawKeys();
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
    static bool OnOpeningSide(doorobj_t door) => door.xlat.OpensFrom.ToLowerInvariant() switch
    {
        "east" => player.TileX >= door.tilex,
        "west" => player.TileX <= door.tilex,
        "south" => player.TileY >= door.tiley,
        "north" => player.TileY <= door.tiley,
        _ => true,
    };

    /// <summary>
    /// The texture on one face of a door (north, south, east or west): its locked face while
    /// it's locked, else its own; a face it doesn't give falls back to the one opposite.
    /// </summary>
    internal static string DoorFace(doorobj_t door, string face)
    {
        static string Face(MapTextureTranslation? xlat, string face) => xlat == null ? "" : face switch
        {
            "north" => xlat.North,
            "south" => xlat.South,
            "east" => xlat.East,
            _ => xlat.West,
        };
        static string Opposite(string face) => face switch { "north" => "south", "south" => "north", "east" => "west", _ => "east" };

        var xlat = door.xlat;
        if (!string.IsNullOrEmpty(door.Lock) && xlat.Locked != null)
        {
            var locked = Face(xlat.Locked, face);
            if (locked.Length > 0)
                return locked;
        }

        var own = Face(xlat, face);
        return own.Length > 0 ? own : Face(xlat, Opposite(face));
    }

    /// <summary>
    /// Refuses a locked door or switch the player lacks the lock item for. The sound is its own
    /// locked-sound (doors.yaml or the switch), else its lock item's `key.lockedsound`. The
    /// message is its own lock-message, else the lock item's `key.lockedmessage` (for a switch,
    /// `key.switchlockedmessage`), else that it's locked; the style is found the same way, then
    /// comes from game-info's for locked doors.
    /// </summary>
    static void RefuseLocked(string lockItem, string lockedSound, string lockMessage, string lockMessageStyle, bool isSwitch = false)
    {
        var sound = !string.IsNullOrWhiteSpace(lockedSound) ? lockedSound
            : _inventoryManager.GetStringProperty(lockItem, "key.lockedsound");
        if (!string.IsNullOrEmpty(sound))
            _audioManager.Play(sound);

        var message = !string.IsNullOrWhiteSpace(lockMessage) ? lockMessage
            : isSwitch ? _inventoryManager.GetStringProperty(lockItem, "key.switchlockedmessage") ?? "$LOCKEDSWITCH"
            : _inventoryManager.GetStringProperty(lockItem, "key.lockedmessage") ?? "$LOCKEDDOOR";
        var style = !string.IsNullOrWhiteSpace(lockMessageStyle) ? lockMessageStyle
            : _inventoryManager.GetStringProperty(lockItem, "key.lockedmessagestyle");
        _hudMessageManager.Show(HudMessageKind.Lock, message, style);
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

    internal static void DoorOpen(int door)
    {
        if (doorobjlist[door].held)
            return;

        if ((doorobjlist[door].ticcount += (short)tics) >= OPENTICS)
            CloseDoor(door);
    }

    /*
    ===============
    =
    = DoorOpening
    =
    ===============
    */

    internal static void DoorOpening(int door)
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
                area1 = (uint)_mapManager.MAPSPOT(door_tilex + 1, door_tiley, 0);
                area2 = (uint)_mapManager.MAPSPOT(door_tilex - 1, door_tiley, 0);
            }
            else
            {
                area1 = (uint)_mapManager.MAPSPOT(door_tilex, door_tiley - 1, 0);
                area2 = (uint)_mapManager.MAPSPOT(door_tilex, door_tiley + 1, 0);
            }
            area1 -= (uint)_mapManager.Floors.AreaTile;
            area2 -= (uint)_mapManager.Floors.AreaTile;

            if (area1 < _mapManager.Floors.NumAreas && area2 < _mapManager.Floors.NumAreas)
            {
                areaconnect[area1, area2]++;
                areaconnect[area2, area1]++;

                if (_mapManager.Players.Any(pawn => pawn.AreaNumber < _mapManager.Floors.NumAreas))
                    ConnectAreas();

                if (areabyplayer[area1] != 0 && doorobjlist[door].xlat.OpenSound is { Length: > 0 } openSound)
                {
                    PlaySoundLocTile(openSound, doorobjlist[door].tilex, doorobjlist[door].tiley);  // JAB
                }
            }
        }

        //
        // slide the door by an adaptive amount
        //
        position += (int)(tics << 10);
        if (position >= 0xffff)
        {
            //
            // door is all the way open
            //
            position = 0xffff;
            doorobjlist[door].ticcount = 0;
            doorobjlist[door].action = (byte)dooractiontypes.dr_open;
            _mapManager.actorat[doorobjlist[door].tilex, doorobjlist[door].tiley] = null;
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

    internal static void DoorClosing(int door)
    {
        uint area1, area2;
        int position;
        int tilex, tiley;

        tilex = doorobjlist[door].tilex;
        tiley = doorobjlist[door].tiley;

        if ((_mapManager.actorat[tilex, tiley] is not Door)//!= (door | BIT_DOOR))
            || _mapManager.Players.Any(pawn => pawn.TileX == tilex && pawn.TileY == tiley))
        {                       // something got inside the door
            OpenDoor(door);
            return;
        }

        position = doorobjlist[door].position;

        //
        // slide the door by an adaptive amount
        //
        position -= (int)(tics << 10);
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
                area1 = (uint)_mapManager.MAPSPOT(door_tilex + 1, door_tiley, 0);
                area2 = (uint)_mapManager.MAPSPOT(door_tilex - 1, door_tiley, 0);
            }
            else
            {
                area1 = (uint)_mapManager.MAPSPOT(door_tilex, door_tiley - 1, 0);
                area2 = (uint)_mapManager.MAPSPOT(door_tilex, door_tiley + 1, 0);
            }

            area1 -= (uint)_mapManager.Floors.AreaTile;
            area2 -= (uint)_mapManager.Floors.AreaTile;

            if (area1 < _mapManager.Floors.NumAreas && area2 < _mapManager.Floors.NumAreas)
            {
                areaconnect[area1, area2]--;
                areaconnect[area2, area1]--;

                if (_mapManager.Players.Any(pawn => pawn.AreaNumber < _mapManager.Floors.NumAreas))
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
    internal static void MoveDoors()
    {
        int door;

        if (gamestate.victoryflag)              // don't move door during victory sequence
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


    /*
    =============================================================================

                                    PUSHABLE WALLS

    =============================================================================
    */

    /// <summary>
    /// A pushwall on the move. Any number can move at once, each with its own progress; a
    /// moving wall's two tiles (the one it's leaving and the one ahead) are bare BIT_WALL.
    /// </summary>
    internal sealed class MovingPushWall
    {
        public ushort State;                // tics since it started: a tile per 128
        public ushort Pos;                  // amount it has moved into the tile ahead (0-63)
        public ushort X, Y;                 // the tile it's leaving
        public controldirs Dir;
        public byte Tile;                   // its wall id

        // Who pushed it: the tiles it leaves take their area (not saved: a loaded game's
        // walls go by the first player, the only one a save has)
        public Entities.Actors.PlayerPawn? Pusher;

        public void Write(BinaryWriter bw)
        {
            bw.Write(State);
            bw.Write(Pos);
            bw.Write(X);
            bw.Write(Y);
            bw.Write((byte)Dir);
            bw.Write(Tile);
        }

        public static MovingPushWall Read(BinaryReader br)
        {
            var wall = new MovingPushWall
            {
                State = br.ReadUInt16(),
                Pos = br.ReadUInt16(),
                X = br.ReadUInt16(),
                Y = br.ReadUInt16(),
                Dir = (controldirs)br.ReadByte(),
                Tile = br.ReadByte(),
            };
            if (wall.X >= MapManager.MAPSIZE || wall.Y >= MapManager.MAPSIZE || (int)wall.Dir > (int)controldirs.di_west)
                throw new InvalidDataException($"Bad moving pushwall at {wall.X},{wall.Y}.");
            return wall;
        }
    }

    // The pushwalls on the move, in the order they were pushed
    static readonly List<MovingPushWall> pushwalls = [];

    // The moving pushwall the renderer is tracing, picked by SelectPushWall as a trace enters
    // one of its tiles
    static MovingPushWall pwall = new();
    static ushort pwallpos => pwall.Pos;
    static ushort pwallx => pwall.X;
    static ushort pwally => pwall.Y;
    static controldirs pwalldir => pwall.Dir;
    static byte pwalltile => pwall.Tile;

    static int[][] dirs = [[0, -1], [1, 0], [0, 1], [-1, 0]];

    /// <summary>The moving pushwall on a tile, the one it's leaving or the one ahead, or null.</summary>
    static MovingPushWall? PushWallAt(int x, int y)
    {
        foreach (var wall in pushwalls)
        {
            if ((wall.X == x && wall.Y == y)
                || (wall.X + dirs[(int)wall.Dir][0] == x && wall.Y + dirs[(int)wall.Dir][1] == y))
                return wall;
        }
        return null;
    }

    /// <summary>Points pwall at the moving pushwall on a tile; keeps the last one if there's none.</summary>
    static void SelectPushWall(int x, int y)
    {
        if (PushWallAt(x, y) is { } wall)
            pwall = wall;
    }

    /// <summary>Stops every pushwall where it is (a new level, or the player starting over).</summary>
    internal static void ClearPushWalls() => pushwalls.Clear();

    static Entities.Actors.PlayerPawn PusherOf(MovingPushWall wall) =>
        wall.Pusher is { } p && ReferenceEquals(p.State.Pawn, p) ? p : _mapManager.Players.First();

    // Whether a player's box (PLAYERSIZE each way) reaches into a tile
    static bool PlayerBoxCovers(Entities.Actors.PlayerPawn pawn, int tilex, int tiley) =>
        (int)((pawn.X - PLAYERSIZE) >> MapConstants.TILESHIFT) <= tilex && tilex <= (int)((pawn.X + PLAYERSIZE) >> MapConstants.TILESHIFT)
        && (int)((pawn.Y - PLAYERSIZE) >> MapConstants.TILESHIFT) <= tiley && tiley <= (int)((pawn.Y + PLAYERSIZE) >> MapConstants.TILESHIFT);

    /*
    ===============
    =
    = PushWall
    =
    ===============
    */
    // A_PushWall, the mapdefs trigger action (registered in RegisterActorActions): true if the
    // wall started moving, which is when a secret trigger counts as found. moveSound plays as it
    // starts, blockedSound when something's in the way (either null for none).
    internal static bool PushWall(int checkx, int checky, controldirs dir, string? moveSound = null, string? blockedSound = null)
    {
        int oldtile, dx, dy;

        // demos keep the original game's one wall at a time, so they play out the same
        if (pushwalls.Count > 0 && (demoplayback || demorecord))
            return false;

        oldtile = _mapManager.tilemap[checkx, checky];
        if (oldtile == 0 || oldtile == BIT_WALL)    // nothing there, or a wall already on the move
            return false;

        dx = dirs[(int)dir][0];
        dy = dirs[(int)dir][1];

        if (_mapManager.actorat[checkx + dx, checky + dy] != null
            || _mapManager.EnemiesAt(checkx + dx, checky + dy).Any())
        {
            if (!string.IsNullOrEmpty(blockedSound))
                _audioManager.Play(blockedSound);
            return false;
        }

        _mapManager.tilemap[checkx + dx, checky + dy] = (byte)oldtile;
        _mapManager.actorat[checkx + dx, checky + dy] = new Wall(oldtile);

        pushwalls.Add(new MovingPushWall
        {
            X = (ushort)checkx,
            Y = (ushort)checky,
            Dir = dir,
            State = 1,
            Pos = 0,
            Tile = (byte)oldtile,
            Pusher = player,
        });
        _mapManager.tilemap[checkx, checky] = BIT_WALL;
        _mapManager.tilemap[checkx + dx, checky + dy] = BIT_WALL;
        _mapManager.SetMapSpot(checkx, checky, 1,  0);   // remove P tile info
        _mapManager.SetMapSpot(checkx, checky, 0, (ushort)_mapManager.MAPSPOT(player.TileX, player.TileY, 0)); // set correct floorcode (BrotherTank's fix) TODO: use a better method...

        if (!string.IsNullOrEmpty(moveSound))
            _audioManager.Play(moveSound);
        return true;
    }

    /*
    =================
    =
    = MovePWalls
    =
    =================
    */

    internal static void MovePWalls()
    {
        // in the order they were pushed, so every machine in a net game moves them the same
        for (int i = 0; i < pushwalls.Count;)
        {
            if (MovePWall(pushwalls[i]))
                i++;
            else
                pushwalls.RemoveAt(i);
        }
    }

    // Moves one pushwall on by this frame's tics: false once it has stopped
    static bool MovePWall(MovingPushWall wall)
    {
        int oldblock, oldtile;

        oldblock = wall.State / 128;

        wall.State += (ushort)tics;

        if (wall.State / 128 != oldblock)
        {
            // block crossed into a new block
            oldtile = wall.Tile;

            //
            // the tile can now be walked into
            //
            _mapManager.tilemap[wall.X, wall.Y] = 0;
            _mapManager.actorat[wall.X, wall.Y] = null;
            _mapManager.SetMapSpot(wall.X, wall.Y, 0, (ushort)(PusherOf(wall).AreaNumber + _mapManager.Floors.AreaTile));    // TODO: this is unnecessary, and makes a mess of mapsegs

            int dx = dirs[(byte)wall.Dir][0], dy = dirs[(byte)wall.Dir][1];
            //
            // see if it should be pushed farther
            //
            if (wall.State >= 256)            // only move two tiles fix
            {
                //
                // the block has been pushed two tiles
                //
                _mapManager.tilemap[wall.X + dx, wall.Y + dy] = (byte)oldtile;
                _mapManager.MoveWallStories(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                _mapManager.MoveTag(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                return false;
            }
            else
            {
                // the wall's height and tag go with it, on the tile wall.X/wall.Y name
                _mapManager.MoveWallStories(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                _mapManager.MoveTag(wall.X, wall.Y, wall.X + dx, wall.Y + dy);
                wall.X += (ushort)dx;
                wall.Y += (ushort)dy;
                int aheadx = wall.X + dx, aheady = wall.Y + dy;

                // stopped by anything in the way, a player included (any of them), or another
                // moving wall's tile
                if (_mapManager.actorat[aheadx, aheady] != null
                    || _mapManager.tilemap[aheadx, aheady] == BIT_WALL
                    || _mapManager.EnemiesAt(aheadx, aheady).Any()
                    || _mapManager.Players.Any(pawn => PlayerBoxCovers(pawn, aheadx, aheady)))
                {
                    _mapManager.tilemap[wall.X, wall.Y] = (byte)oldtile;
                    return false;
                }

                _mapManager.actorat[aheadx, aheady] = new Wall(oldtile);
                _mapManager.tilemap[aheadx, aheady] = BIT_WALL;
            }
        }

        wall.Pos = (ushort)((wall.State / 2) & 63);
        return true;
    }
}
