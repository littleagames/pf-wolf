using Wolf3D.Assets;
using Wolf3D.Constants;
using Wolf3D.Entities.Actors;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    internal static statobj_t[] statobjlist = new statobj_t[MAXSTATS];
    internal static int laststatobj;

    internal struct statinfo_t
    {
        public string picnum;
        public wl_stat_types type;
        public objflags specialFlags;    // they are ORed to the statobj_t flags

        public statinfo_t(string picnum)
        {
            this.picnum = picnum;
        }

        public statinfo_t(string picnum, wl_stat_types type)
        {
            this.picnum = picnum;
            this.type = type;
        }

        public statinfo_t(string picnum, wl_stat_types type, objflags specialFlags)
        {
            this.picnum = picnum;
            this.type = type;
            this.specialFlags = specialFlags;
        }
    }

    internal static void InitStaticList()
    {
        laststatobj = 0;
    }

    internal static void SpawnStatic(int tilex, int tiley, int type)
    {
        // Temporary way to spawn objects
        var data = _mapManager.GetMapData(); //_assetManager.Find<MapObjectTranslationAsset>("wolf3d/mapdefs"); // TODO: This is not the best way to do this.
        //if (data == null) return;

        var actors = _assetManager.GetActorMetadata();
        if (!data.Things.TryGetValue(type, out var actorSpawnData))
            return;

        if (!actors.Actors.TryGetValue(actorSpawnData.Class, out var actor))
            return;

        if (!actor.States.TryGetValue("Spawn", out var spawnState))
            return;
        var firstSpawnFrame = spawnState.First() as ActorStatesData;
        // To get what was "statinfo"

        var newstatobj = new statobj_t();
        newstatobj.shapenum = firstSpawnFrame.Sprite + firstSpawnFrame.Frames.First() + "0"; // e.g. DRUMA0
        newstatobj.tilex = (byte)tilex;
        newstatobj.tiley = (byte)tiley;
        //newstatobj.itemnumber = statinfo[type].type; // TODO: bonus items

        if (actor.Flags.Any(x => x == "SOLID"))
            _mapManager.actorat[tilex, tiley] = new BlockingActor();// BIT_WALL;          // consider it a blocking tile
        else
            newstatobj.flags = 0;

        //switch (statinfo[type].type)
        //{
        //    case wl_stat_types.block:
        //        _mapManager.actorat[tilex, tiley] = new BlockingActor();// BIT_WALL;          // consider it a blocking tile
        //        goto case wl_stat_types.none;
        //    case wl_stat_types.none:
        //        newstatobj.flags = 0;
        //        break;

        //    case wl_stat_types.bo_cross:
        //    case wl_stat_types.bo_chalice:
        //    case wl_stat_types.bo_bible:
        //    case wl_stat_types.bo_crown:
        //    case wl_stat_types.bo_fullheal:
        //        if (!loadedgame)
        //            gamestate.treasuretotal++;
        //        goto case wl_stat_types.bo_firstaid;

        //    case wl_stat_types.bo_firstaid:
        //    case wl_stat_types.bo_key1:
        //    case wl_stat_types.bo_key2:
        //    case wl_stat_types.bo_key3:
        //    case wl_stat_types.bo_key4:
        //    case wl_stat_types.bo_clip:
        //    case wl_stat_types.bo_clip2:
        //    case wl_stat_types.bo_25clip:
        //    case wl_stat_types.bo_machinegun:
        //    case wl_stat_types.bo_chaingun:
        //    case wl_stat_types.bo_food:
        //    case wl_stat_types.bo_alpo:
        //    case wl_stat_types.bo_gibs:
        //    case wl_stat_types.bo_spear:
        //        newstatobj.flags = objflags.FL_BONUS;
        //        break;
        //}

        //newstatobj.flags |= statinfo[type].specialFlags;
        statobjlist[laststatobj] = newstatobj;

        laststatobj++;

        if (laststatobj == (MAXSTATS - 1))
            _gameEngineManager.Quit("Too many static objects!\n");
    }

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
    internal static void PlaceItemType(wl_stat_types itemtype, int tilex, int tiley)
    {
        int type;
        statobj_t spot = null!;

        //
        // find the item number
        //
        //for (type = 0; ; type++)
        //{
        //    if (statinfo[type].picnum == "")                    // end of list
        //        _gameEngineManager.Quit("PlaceItemType: couldn't find type!");
        //    if (statinfo[type].type == itemtype)
        //        break;
        //}
        return;// throw new NotImplementedException("Not quite ready for this in the new system yet.");

        //
        // find a spot in statobjlist to put it in
        //
        for (int i = 0; ; i++)
            //spot = statobjlist[0]; ; spot++)
        {
            spot = statobjlist[i];
            if (i == laststatobj)
            {
                if (spot != null && spot == statobjlist[MAXSTATS - 1])
                    return;                                     // no free spots
                spot = new statobj_t();
                statobjlist[laststatobj] = spot;
                laststatobj++;                                  // space at end
                break;
            }

            if (spot.shapenum == "")                           // -1 is a free spot
                break;
        }

        //
        // place it
        //
        //spot.shapenum = statinfo[type].picnum;
        spot.tilex = (byte)tilex;
        spot.tiley = (byte)tiley;
        //spot.flags = objflags.FL_BONUS | statinfo[type].specialFlags;
        //spot.itemnumber = statinfo[type].type;
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

    internal static byte[,] areaconnect = new byte[MapDataConstants.NUMAREAS, MapDataConstants.NUMAREAS];

    internal static byte[] areabyplayer = new byte[MapDataConstants.NUMAREAS];



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

        for (i = 0; i < MapDataConstants.NUMAREAS; i++)
        {
            if (areaconnect[areanumber, i] != 0 && areabyplayer[i] == 0)
            {
                areabyplayer[i] = 1; // true
                RecursiveConnect(i);
            }
        }
    }

    internal static void ConnectAreas()
    {
        Array.Fill(areabyplayer, (byte)0);
        areabyplayer[player.areanumber] = 1; // true
        RecursiveConnect(player.areanumber);
    }

    internal static void InitAreas()
    {
        Array.Fill(areabyplayer, (byte)0);
        if (player.areanumber < MapDataConstants.NUMAREAS)
            areabyplayer[player.areanumber] = 1; // true
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
        Array.Fill(areabyplayer, (byte)0);

        for(int i = 0; i < MapDataConstants.NUMAREAS; i++)
            for (int j = 0; j < MapDataConstants.NUMAREAS; j++)
            {
                areaconnect[i, j] = 0;
            }

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
    internal static void SpawnDoor(int tilex, int tiley, bool vertical, int locknum, MapTextureTranslation doorXlat)
    {
        if (doornum == MAXDOORS)
            _gameEngineManager.Quit("64+ doors on level!");

        var doorobj = new doorobj_t();
        doorobj.position = 0;              // doors start out fully closed
        doorobj.tilex = (sbyte)tilex;
        doorobj.tiley = (sbyte)tiley;
        doorobj.vertical = vertical;
        doorobj.locknum = (sbyte)locknum;
        doorobj.action = dooractiontypes.dr_closed;
        doorobj.xlat = doorXlat;
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
        Actor? check = null;

        //
        // don't close on anything solid
        //
        tilex = doorobjlist[door].tilex;
        tiley = doorobjlist[door].tiley;

        if (_mapManager.actorat[tilex, tiley] is Actor)
            return;

        if (player.tilex == tilex && player.tiley == tiley)
            return;

        if (doorobjlist[door].vertical)
        {
            if (player.tiley == tiley)
            {
                if (((player.x + MINDIST) >> MapConstants.TILESHIFT) == tilex)
                    return;
                if (((player.x - MINDIST) >> MapConstants.TILESHIFT) == tilex)
                    return;
            }
            check = _mapManager.actorat[tilex - 1, tiley];
            if (MapManager.ISPOINTER(check) && ((check.x + MINDIST) >> MapConstants.TILESHIFT) == tilex)
                return;
            check = _mapManager.actorat[tilex + 1, tiley];
            if (MapManager.ISPOINTER(check) && ((check.x - MINDIST) >> MapConstants.TILESHIFT) == tilex)
                return;
        }
        else
        {
            if (player.tilex == tilex)
            {
                if (((player.y + MINDIST) >> MapConstants.TILESHIFT) == tiley)
                    return;
                if (((player.y - MINDIST) >> MapConstants.TILESHIFT) == tiley)
                    return;
            }

            check = _mapManager.actorat[tilex, tiley - 1];
            if (MapManager.ISPOINTER(check) && ((check.y + MINDIST) >> MapConstants.TILESHIFT) == tiley)
                return;

            check = _mapManager.actorat[tilex, tiley + 1];
            if (MapManager.ISPOINTER(check) && ((check.y - MINDIST) >> MapConstants.TILESHIFT) == tiley)
                return;
        }


        //
        // play door sound if in a connected area
        //
        area = _mapManager.MAPSPOT(tilex, tiley, 0) - MapDataConstants.AREATILE;

        if (areabyplayer[area] != 0)
        {
            PlaySoundLocTile("CLOSEDOOR", doorobjlist[door].tilex, doorobjlist[door].tiley); // JAB
        }

        doorobjlist[door].action = dooractiontypes.dr_closing;
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
        int locknum;

        locknum = doorobjlist[door].locknum;
        if (locknum >= (int)doortypes.dr_lock1 && locknum <= (int)doortypes.dr_lock4)
        {
            if ((gamestate.keys & (1 << (locknum - (int)doortypes.dr_lock1) ) ) == 0)
        {
                if (doorobjlist[door].position == 0)
                    _audioManager.Play("player/usefail");  // ADDEDFIX 9       // locked
                return;
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


    //===========================================================================

    /*
    ===============
    =
    = DoorOpen
    =
    = Close the door after three seconds
    =
    ===============
    */

    internal static void DoorOpen(int door)
    {
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
            area1 -= MapDataConstants.AREATILE;
            area2 -= MapDataConstants.AREATILE;

            if (area1 < MapDataConstants.NUMAREAS && area2 < MapDataConstants.NUMAREAS)
            {
                areaconnect[area1, area2]++;
                areaconnect[area2, area1]++;

                if (player.areanumber < MapDataConstants.NUMAREAS)
                    ConnectAreas();

                if (areabyplayer[area1] != 0)
                {
                    PlaySoundLocTile("OPENDOOR", doorobjlist[door].tilex, doorobjlist[door].tiley);  // JAB
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
            || (player.tilex == tilex && player.tiley == tiley))
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

            area1 -= MapDataConstants.AREATILE;
            area2 -= MapDataConstants.AREATILE;

            if (area1 < MapDataConstants.NUMAREAS && area2 < MapDataConstants.NUMAREAS)
            {
                areaconnect[area1, area2]--;
                areaconnect[area2, area1]--;

                if (player.areanumber < MapDataConstants.NUMAREAS)
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

    static ushort pwallstate;
    static ushort pwallpos;                  // amount a pushable wall has been moved (0-63)
    static ushort pwallx, pwally;
    static controldirs pwalldir;
    static byte pwalltile;
    static int[][] dirs = [[0, -1], [1, 0], [0, 1], [-1, 0]];

    /*
    ===============
    =
    = PushWall
    =
    ===============
    */
    internal static void PushWall(int checkx, int checky, controldirs dir)
    {
        int oldtile, dx, dy;

        if (pwallstate != 0)
            return;

        oldtile = _mapManager.tilemap[checkx, checky];
        if (oldtile == 0)
            return;

        dx = dirs[(int)dir][0];
        dy = dirs[(int)dir][1];

        if (_mapManager.actorat[checkx + dx, checky + dy] != null)
        {
            _audioManager.Play("player/usefail");
            return;
        }

        _mapManager.tilemap[checkx + dx, checky + dy] = (byte)oldtile;
        _mapManager.actorat[checkx + dx, checky + dy] = new Wall(oldtile);

        gamestate.secretcount++;
        pwallx = (ushort)checkx;
        pwally = (ushort)checky;
        pwalldir = dir;
        pwallstate = 1;
        pwallpos = 0;
        pwalltile = _mapManager.tilemap[pwallx, pwally];
        _mapManager.tilemap[pwallx, pwally] = BIT_WALL;
        _mapManager.tilemap[pwallx + dx, pwally + dy] = BIT_WALL;
        _mapManager.SetMapSpot(pwallx, pwally, 1,  0);   // remove P tile info
        _mapManager.SetMapSpot(pwallx, pwally, 0, (ushort)_mapManager.MAPSPOT(player.tilex, player.tiley, 0)); // set correct floorcode (BrotherTank's fix) TODO: use a better method...

        _audioManager.Play("world/pushwall");
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
        int oldblock, oldtile;

        if (pwallstate == 0)
            return;

        oldblock = pwallstate / 128;

        pwallstate += (ushort)tics;

        if (pwallstate / 128 != oldblock)
        {
            // block crossed into a new block
            oldtile = pwalltile;

            //
            // the tile can now be walked into
            //
            _mapManager.tilemap[pwallx, pwally] = 0;
            _mapManager.actorat[pwallx, pwally] = null;
            _mapManager.SetMapSpot(pwallx, pwally, 0, (ushort)(player.areanumber + MapDataConstants.AREATILE));    // TODO: this is unnecessary, and makes a mess of mapsegs

            int dx = dirs[(byte)pwalldir][0], dy = dirs[(byte)pwalldir][1];
            //
            // see if it should be pushed farther
            //
            if (pwallstate >= 256)            // only move two tiles fix
            {
                //
                // the block has been pushed two tiles
                //
                pwallstate = 0;
                _mapManager.tilemap[pwallx + dx, pwally + dy] = (byte)oldtile;
                return;
            }
            else
            {
                int xl, yl, xh, yh;
                xl = (int)((player.x - PLAYERSIZE) >> MapConstants.TILESHIFT);
                yl = (int)((player.y - PLAYERSIZE) >> MapConstants.TILESHIFT);
                xh = (int)((player.x + PLAYERSIZE) >> MapConstants.TILESHIFT);
                yh = (int)((player.y + PLAYERSIZE) >> MapConstants.TILESHIFT);

                pwallx += (ushort)dx;
                pwally += (ushort)dy;

                if (_mapManager.actorat[pwallx + dx, pwally + dy] != null
                    || (xl <= pwallx + dx && pwallx + dx <= xh && yl <= pwally + dy && pwally + dy <= yh))
                {
                    pwallstate = 0;
                    _mapManager.tilemap[pwallx, pwally] = (byte)oldtile;
                    return;
                }

                _mapManager.tilemap[pwallx + dx, pwally + dy] = (byte)oldtile;
                _mapManager.actorat[pwallx + dx, pwally + dy] = new Wall(oldtile); // the double-assign here is something of pointers, might not be useful here anymore
                _mapManager.tilemap[pwallx + dx, pwally + dy] = BIT_WALL;
            }
        }

        pwallpos = (ushort)((pwallstate / 2) & 63);
    }
}
