using System;
using System.Data;
using Wolf3D;
using static Wolf3D.Program;

namespace Wolf3D;

internal partial class Program
{
    internal static statobj_t[] statobjlist = new statobj_t[MAXSTATS];
    internal static int laststatobj;

    internal struct statinfo_t
    {
        public short picnum;
        public short type;
        public uint specialFlags;    // they are ORed to the statobj_t flags

        public statinfo_t(short picnum)
        {
            this.picnum = picnum;
        }

        public statinfo_t(short picnum, short type)
        {
            this.picnum = picnum;
            this.type = type;
        }

        public statinfo_t(short picnum, short type, uint specialFlags)
        {
            this.picnum = picnum;
            this.type = type;
            this.specialFlags = specialFlags;
        }
    }

    internal static statinfo_t[] statinfo =
    {
        new statinfo_t((short)spritenums.SPR_STAT_0),                           // puddle          spr1v
        new statinfo_t((short)spritenums.SPR_STAT_1,(short)wl_stat_types.block),                     // Green Barrel    "
        new statinfo_t((short)spritenums.SPR_STAT_2,(short)wl_stat_types.block),                     // Table/chairs    "
        new statinfo_t((short)spritenums.SPR_STAT_3,(short)wl_stat_types.block,(uint)objflags.FL_FULLBRIGHT),       // Floor lamp      "
        new statinfo_t((short)spritenums.SPR_STAT_4,(short)wl_stat_types.none,(uint)objflags.FL_FULLBRIGHT),        // Chandelier      "
        new statinfo_t((short)spritenums.SPR_STAT_5,(short)wl_stat_types.block),                     // Hanged man      "
        new statinfo_t((short)spritenums.SPR_STAT_6,(short)wl_stat_types.bo_alpo),                   // Bad food        "
        new statinfo_t((short)spritenums.SPR_STAT_7,(short)wl_stat_types.block),                     // Red pillar      "
        //
        // NEW PAGE
        //
        new statinfo_t((short)spritenums.SPR_STAT_8,(short)wl_stat_types.block),                     // Tree            spr2v
        new statinfo_t((short)spritenums.SPR_STAT_9),                           // Skeleton flat   "
        new statinfo_t((short)spritenums.SPR_STAT_10,(short)wl_stat_types.block),                    // Sink            " (SOD:gibs)
        new statinfo_t((short)spritenums.SPR_STAT_11,(short)wl_stat_types.block),                    // Potted plant    "
        new statinfo_t((short)spritenums.SPR_STAT_12,(short)wl_stat_types.block),                    // Urn             "
        new statinfo_t((short)spritenums.SPR_STAT_13,(short)wl_stat_types.block),                    // Bare table      "
        new statinfo_t((short)spritenums.SPR_STAT_14,(short)wl_stat_types.none,(uint)objflags.FL_FULLBRIGHT),       // Ceiling light   "
        new statinfo_t((short)spritenums.SPR_STAT_15),                          // Kitchen stuff   "
        //
        // NEW PAGE
        //
        new statinfo_t((short)spritenums.SPR_STAT_16, (short) wl_stat_types.block),                    // suit of armor   spr3v
        new statinfo_t((short)spritenums.SPR_STAT_17, (short) wl_stat_types.block),                    // Hanging cage    "
        new statinfo_t((short)spritenums.SPR_STAT_18, (short)wl_stat_types.block),                    // SkeletoninCage  "
        new statinfo_t((short)spritenums.SPR_STAT_19),                          // Skeleton relax  "
        new statinfo_t((short)spritenums.SPR_STAT_20, (short)wl_stat_types.bo_key1),                  // Key 1           "
        new statinfo_t((short)spritenums.SPR_STAT_21, (short)wl_stat_types.bo_key2),                  // Key 2           "
        new statinfo_t((short)spritenums.SPR_STAT_22, (short)wl_stat_types.block),                    // stuff             (SOD:gibs)
        new statinfo_t((short)spritenums.SPR_STAT_23),                          // stuff
        //
        // NEW PAGE
        //
        new statinfo_t((short)spritenums.SPR_STAT_24,(short)wl_stat_types.bo_food),                  // Good food       spr4v
        new statinfo_t((short)spritenums.SPR_STAT_25,(short)wl_stat_types.bo_firstaid),              // First aid       "
        new statinfo_t((short)spritenums.SPR_STAT_26,(short)wl_stat_types.bo_clip),                  // Clip            "
        new statinfo_t((short)spritenums.SPR_STAT_27,(short)wl_stat_types.bo_machinegun),            // Machine gun     "
        new statinfo_t((short)spritenums.SPR_STAT_28,(short)wl_stat_types.bo_chaingun),              // Gatling gun     "
        new statinfo_t((short)spritenums.SPR_STAT_29,(short)wl_stat_types.bo_cross),                 // Cross           "
        new statinfo_t((short)spritenums.SPR_STAT_30,(short)wl_stat_types.bo_chalice),               // Chalice         "
        new statinfo_t((short)spritenums.SPR_STAT_31, (short)wl_stat_types.bo_bible),                 // Bible           "
        //
        // NEW PAGE
        //
        new statinfo_t((short)spritenums.SPR_STAT_32,(short)wl_stat_types.bo_crown),                 // crown           spr5v
        new statinfo_t((short)spritenums.SPR_STAT_33,(short)wl_stat_types.bo_fullheal,(uint)objflags.FL_FULLBRIGHT),// one up          "
        new statinfo_t((short)spritenums.SPR_STAT_34,(short)wl_stat_types.bo_gibs),                  // gibs            "
        new statinfo_t((short)spritenums.SPR_STAT_35,(short)wl_stat_types.block),                    // barrel          "
        new statinfo_t((short)spritenums.SPR_STAT_36,(short)wl_stat_types.block),                    // well            "
        new statinfo_t((short)spritenums.SPR_STAT_37,(short)wl_stat_types.block),                    // Empty well      "
        new statinfo_t((short)spritenums.SPR_STAT_38,(short)wl_stat_types.bo_gibs),                  // Gibs 2          "
        new statinfo_t((short)spritenums.SPR_STAT_39, (short)wl_stat_types.block),                    // flag            "
        //
        // NEW PAGE
        //
        new statinfo_t((short)spritenums.SPR_STAT_40, (short)wl_stat_types.block),                    // Call Apogee          spr7v
        //
        // NEW PAGE
        //
        new statinfo_t((short)spritenums.SPR_STAT_41),                          // junk            "
        new statinfo_t((short)spritenums.SPR_STAT_42),                          // junk            "
        new statinfo_t((short)spritenums.SPR_STAT_43),                          // junk            "
        new statinfo_t((short)spritenums.SPR_STAT_44),                          // pots            "
        new statinfo_t((short)spritenums.SPR_STAT_45,(short)wl_stat_types.block),                    // stove           " (SOD:gibs)
        new statinfo_t((short)spritenums.SPR_STAT_46, (short)wl_stat_types.block),                    // spears          " (SOD:gibs)
        new statinfo_t((short)spritenums.SPR_STAT_47),                          // vines           "
        //
        // NEW PAGE
        //
        new statinfo_t((short)spritenums.SPR_STAT_26, (short)wl_stat_types.bo_clip2),                 // Clip     
        new statinfo_t(-1)                                   // terminator
    };

    internal static void InitStaticList()
    {
        laststatobj = 0;
    }

    internal static void SpawnStatic(int tilex, int tiley, int type)
    {
        var newstatobj = new statobj_t();
        newstatobj.shapenum = statinfo[type].picnum;
        newstatobj.tilex = (byte)tilex;
        newstatobj.tiley = (byte)tiley;
        newstatobj.itemnumber = (byte)statinfo[type].type;

        switch ((wl_stat_types)statinfo[type].type)
        {
            case wl_stat_types.block:
                actorat[tilex, tiley] = BIT_WALL;          // consider it a blocking tile
                goto case wl_stat_types.none;
            case wl_stat_types.none:
                newstatobj.flags = 0;
                break;

            case wl_stat_types.bo_cross:
            case wl_stat_types.bo_chalice:
            case wl_stat_types.bo_bible:
            case wl_stat_types.bo_crown:
            case wl_stat_types.bo_fullheal:
                if (!loadedgame)
                    gamestate.treasuretotal++;
                goto case wl_stat_types.bo_firstaid;

            case wl_stat_types.bo_firstaid:
            case wl_stat_types.bo_key1:
            case wl_stat_types.bo_key2:
            case wl_stat_types.bo_key3:
            case wl_stat_types.bo_key4:
            case wl_stat_types.bo_clip:
            case wl_stat_types.bo_clip2:
            case wl_stat_types.bo_25clip:
            case wl_stat_types.bo_machinegun:
            case wl_stat_types.bo_chaingun:
            case wl_stat_types.bo_food:
            case wl_stat_types.bo_alpo:
            case wl_stat_types.bo_gibs:
            case wl_stat_types.bo_spear:
                newstatobj.flags = (uint)objflags.FL_BONUS;
                break;
        }

        newstatobj.flags |= statinfo[type].specialFlags;
        statobjlist[laststatobj] = newstatobj;

        laststatobj++;

        if (laststatobj == (MAXSTATS - 1))
            Quit("Too many static objects!\n");
    }

    internal static void PlaceItemType(int itemtype, int tilex, int tiley)
    {
        int type;
        statobj_t spot = null!;

        //
        // find the item number
        //
        for (type = 0; ; type++)
        {
            if (statinfo[type].picnum == -1)                    // end of list
                Quit("PlaceItemType: couldn't find type!");
            if (statinfo[type].type == itemtype)
                break;
        }

        //
        // find a spot in statobjlist to put it in
        //
        for (int i = 0; i < laststatobj; i++)
            //spot = statobjlist[0]; ; spot++)
        {
            spot = statobjlist[i];
            if (i == laststatobj)
            {
                if (spot != null && spot == statobjlist[MAXSTATS - 1])
                    return;                                     // no free spots
                spot = new statobj_t();
                laststatobj++;                                  // space at end
                break;
            }

            if (spot.shapenum == -1)                           // -1 is a free spot
                break;
        }

        //
        // place it
        //
        spot.shapenum = statinfo[type].picnum;
        spot.tilex = (byte)tilex;
        spot.tiley = (byte)tiley;
        spot.flags = (uint)objflags.FL_BONUS | statinfo[type].specialFlags;
        spot.itemnumber = (byte)statinfo[type].type;
        statobjlist[laststatobj] = spot;
    }

    internal const int DOORWIDTH = 0x7800;
    internal const int OPENTICS = 300;

    internal static doorobj_t[] doorobjlist = new doorobj_t[MAXDOORS];
    internal static int lastdoorobj; // index
    internal static short doornum;

    internal static byte[,] areaconnect = new byte[NUMAREAS, NUMAREAS];

    internal static byte[] areabyplayer = new byte[NUMAREAS];


    internal static void RecursiveConnect(int areanumber)
    {
        int i;

        for (i = 0; i < NUMAREAS; i++)
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
        if (player.areanumber < NUMAREAS)
            areabyplayer[player.areanumber] = 1; // true
    }

    internal static void InitDoorList()
    {
        Array.Fill(areabyplayer, (byte)0);

        for(int i = 0; i < NUMAREAS; i++)
            for (int j = 0; j < NUMAREAS; j++)
            {
                areaconnect[i, j] = 0;
            }

        lastdoorobj = 0;
        //lastdoorobj = &doorobjlist[0]; (will this just be a List<T>.Length?
        doornum = 0;
    }

    internal static void SpawnDoor(int tilex, int tiley, bool vertical, int locknum)
    {
        if (doornum == MAXDOORS)
            Quit("64+ doors on level!");

        var doorobj = new doorobj_t();
        doorobj.position = 0;              // doors start out fully closed
        doorobj.tilex = (byte)tilex;
        doorobj.tiley = (byte)tiley;
        doorobj.vertical = (byte)(vertical ? 1 : 0);
        doorobj.locknum = (byte)locknum;
        doorobj.action = dooractiontypes.dr_closed;
        doorobjlist[lastdoorobj] = doorobj;

        // TODO: "door actor"
        actorat[tilex,tiley] = (uint)(doornum | BIT_DOOR);   // consider it a solid wall

        //
        // make the door tile a special tile, and mark the adjacent tiles
        // for door sides
        //
        tilemap[tilex, tiley] = (byte)(doornum | BIT_DOOR);
        //map = &MAPSPOT(tilex, tiley, 0);
        if (vertical)
        {
            var map_areanum = MAPSPOT(tilex-1, tiley, 0);
            SetMapSpot(tilex, tiley, 0, (ushort)map_areanum); // set area number
            tilemap[tilex, tiley - 1] |= BIT_WALL;
            tilemap[tilex, tiley + 1] |= BIT_WALL;
        }
        else
        {
            var map_areanum = MAPSPOT(tilex, tiley-1, 0);
            SetMapSpot(tilex, tiley, 0, (ushort)map_areanum);
            tilemap[tilex - 1, tiley] |= BIT_WALL;
            tilemap[tilex + 1, tiley] |= BIT_WALL;
        }

        doornum++;
        lastdoorobj++;
    }

    internal static void OpenDoor(int door)
    {
        if (doorobjlist[door].action == (byte)dooractiontypes.dr_open)
            doorobjlist[door].ticcount = 0;         // reset open time
        else
            doorobjlist[door].action = dooractiontypes.dr_opening;  // start it opening
    }

    internal static void CloseDoor(int door)
    {
        int tilex, tiley, area;
        uint checkIndex;
        objstruct? check = null;

        //
        // don't close on anything solid
        //
        tilex = doorobjlist[door].tilex;
        tiley = doorobjlist[door].tiley;

        if (actorat[tilex, tiley] > 0)
            return;

        if (player.tilex == tilex && player.tiley == tiley)
            return;

        if (doorobjlist[door].vertical != 0)
        {
            if (player.tiley == tiley)
            {
                if (((player.x + MINDIST) >> TILESHIFT) == tilex)
                    return;
                if (((player.x - MINDIST) >> TILESHIFT) == tilex)
                    return;
            }
            checkIndex = actorat[tilex - 1, tiley];
            if (ISPOINTER(checkIndex, out check) && ((check.x + MINDIST) >> TILESHIFT) == tilex)
                return;
            checkIndex = actorat[tilex + 1, tiley];
            if (ISPOINTER(checkIndex, out check) && ((check.x - MINDIST) >> TILESHIFT) == tilex)
                return;
        }
        else
        {
            if (player.tilex == tilex)
            {
                if (((player.y + MINDIST) >> TILESHIFT) == tiley)
                    return;
                if (((player.y - MINDIST) >> TILESHIFT) == tiley)
                    return;
            }

            checkIndex = actorat[tilex, tiley - 1];
            if (ISPOINTER(checkIndex, out check) && ((check.y + MINDIST) >> TILESHIFT) == tiley)
                return;

            checkIndex = actorat[tilex, tiley + 1];
            if (ISPOINTER(checkIndex, out check) && ((check.y - MINDIST) >> TILESHIFT) == tiley)
                return;
        }


        //
        // play door sound if in a connected area
        //
        area = MAPSPOT(tilex, tiley, 0) - AREATILE;

        if (areabyplayer[area] != 0)
        {
            //PlaySoundLocTile(CLOSEDOORSND, doorobjlist[door].tilex, doorobjlist[door].tiley); // JAB
        }

        doorobjlist[door].action = dooractiontypes.dr_closing;
        //
        // make the door space solid
        //
        actorat[tilex, tiley] = (uint)(door | BIT_DOOR);
    }

    internal static void OperateDoor(int door)
    {
        int locknum;

        locknum = doorobjlist[door].locknum;
        if (locknum >= (int)doortypes.dr_lock1 && locknum <= (int)doortypes.dr_lock4)
        {
            if ((gamestate.keys & (1 << (locknum - (int)doortypes.dr_lock1) ) ) == 0)
        {
                if (doorobjlist[door].position == 0)
                    SD_PlaySound((int)soundnames.NOWAYSND);  // ADDEDFIX 9       // locked
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

    internal static void DoorOpen(int door)
    {
        if ((doorobjlist[door].ticcount += (short)tics) >= OPENTICS)
            CloseDoor(door);
    }

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

            if (doorobjlist[door].vertical != 0)
            {
                area1 = (uint)MAPSPOT(door_tilex + 1, door_tiley, 0);
                area2 = (uint)MAPSPOT(door_tilex - 1, door_tiley, 0);
            }
            else
            {
                area1 = (uint)MAPSPOT(door_tilex, door_tiley - 1, 0);
                area2 = (uint)MAPSPOT(door_tilex, door_tiley + 1, 0);
            }
            area1 -= AREATILE;
            area2 -= AREATILE;

            if (area1 < NUMAREAS && area2 < NUMAREAS)
            {
                areaconnect[area1, area2]++;
                areaconnect[area2, area1]++;

                if (player.areanumber < NUMAREAS)
                    ConnectAreas();

                if (areabyplayer[area1] != 0)
                {
                    //PlaySoundLocTile(OPENDOORSND, doorobjlist[door].tilex, doorobjlist[door].tiley);  // JAB
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
            actorat[doorobjlist[door].tilex, doorobjlist[door].tiley] = 0;
        }

        doorobjlist[door].position = (ushort)position;
    }

    internal static void DoorClosing(int door)
    {
        uint area1, area2;
        int position;
        int tilex, tiley;

        tilex = doorobjlist[door].tilex;
        tiley = doorobjlist[door].tiley;

        if ((actorat[tilex, tiley] != (door | BIT_DOOR))
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

            if (doorobjlist[door].vertical != 0)
            {
                area1 = (uint)MAPSPOT(door_tilex + 1, door_tiley, 0);
                area2 = (uint)MAPSPOT(door_tilex - 1, door_tiley, 0);
            }
            else
            {
                area1 = (uint)MAPSPOT(door_tilex, door_tiley - 1, 0);
                area2 = (uint)MAPSPOT(door_tilex, door_tiley + 1, 0);
            }

            area1 -= AREATILE;
            area2 -= AREATILE;

            if (area1 < NUMAREAS && area2 < NUMAREAS)
            {
                areaconnect[area1, area2]--;
                areaconnect[area2, area1]--;

                if (player.areanumber < NUMAREAS)
                    ConnectAreas();
            }
        }

        doorobjlist[door].position = (ushort)position;
    }

    internal static void MoveDoors()
    {
        int door;

        if (gamestate.victoryflag != 0)              // don't move door during victory sequence
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
    static byte pwalldir;
    static byte pwalltile;
    static int[][] dirs = [[0, -1], [1, 0], [0, 1], [-1, 0]];

    internal static void PushWall(int checkx, int checky, int dir)
    {
        int oldtile, dx, dy;

        if (pwallstate != 0)
            return;

        oldtile = tilemap[checkx, checky];
        if (oldtile == 0)
            return;

        dx = dirs[dir][0];
        dy = dirs[dir][1];

        if (actorat[checkx + dx, checky + dy] != 0)
        {
            SD_PlaySound((int)soundnames.NOWAYSND);
            return;
        }
        actorat[checkx + dx, checky + dy] = (tilemap[checkx + dx, checky + dy] = (byte)oldtile);

        gamestate.secretcount++;
        pwallx = (ushort)checkx;
        pwally = (ushort)checky;
        pwalldir = (byte)dir;
        pwallstate = 1;
        pwallpos = 0;
        pwalltile = tilemap[pwallx, pwally];
        tilemap[pwallx, pwally] = BIT_WALL;
        tilemap[pwallx + dx, pwally + dy] = BIT_WALL;
        SetMapSpot(pwallx, pwally, 1,  0);   // remove P tile info
        SetMapSpot(pwallx, pwally, 0, (ushort)MAPSPOT(player.tilex, player.tiley, 0)); // set correct floorcode (BrotherTank's fix) TODO: use a better method...

        SD_PlaySound((int)soundnames.PUSHWALLSND);
    }

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
            tilemap[pwallx, pwally] = 0;
            actorat[pwallx, pwally] = 0;
            SetMapSpot(pwallx, pwally, 0, (ushort)(player.areanumber + AREATILE));    // TODO: this is unnecessary, and makes a mess of mapsegs

            int dx = dirs[pwalldir][0], dy = dirs[pwalldir][1];
            //
            // see if it should be pushed farther
            //
            if (pwallstate >= 256)            // only move two tiles fix
            {
                //
                // the block has been pushed two tiles
                //
                pwallstate = 0;
                tilemap[pwallx + dx, pwally + dy] = (byte)oldtile;
                return;
            }
            else
            {
                int xl, yl, xh, yh;
                xl = (int)((player.x - PLAYERSIZE) >> TILESHIFT);
                yl = (int)((player.y - PLAYERSIZE) >> TILESHIFT);
                xh = (int)((player.x + PLAYERSIZE) >> TILESHIFT);
                yh = (int)((player.y + PLAYERSIZE) >> TILESHIFT);

                pwallx += (ushort)dx;
                pwally += (ushort)dy;

                if (actorat[pwallx + dx, pwally + dy] != 0
                    || (xl <= pwallx + dx && pwallx + dx <= xh && yl <= pwally + dy && pwally + dy <= yh))
                {
                    pwallstate = 0;
                    tilemap[pwallx, pwally] = (byte)oldtile;
                    return;
                }

                actorat[pwallx + dx, pwally + dy] = (tilemap[pwallx + dx, pwally + dy] = (byte)oldtile);
                tilemap[pwallx + dx, pwally + dy] = BIT_WALL;
            }
        }

        pwallpos = (ushort)((pwallstate / 2) & 63);
    }
}
