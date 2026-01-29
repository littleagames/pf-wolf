using System;
using System.Data;

namespace Wolf3D;

internal partial class Program
{
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
        doorobj.action = (byte)dooractiontypes.dr_closed;
        doorobjlist[lastdoorobj] = doorobj;

        // TODO: "door actor"
        //actorat[tilex,tiley] = (objtype*)(uintptr_t)(doornum | BIT_DOOR);   // consider it a solid wall

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
            doorobjlist[door].action = (byte)dooractiontypes.dr_opening;  // start it opening
    }

    internal static void CloseDoor(int door)
    {
        int tilex, tiley, area;
        objstruct? check = null;

        //
        // don't close on anything solid
        //
        tilex = doorobjlist[door].tilex;
        tiley = doorobjlist[door].tiley;

        if (actorat[tilex, tiley] != null)
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
            check = actorat[tilex - 1, tiley];
            if (ISPOINTER(check) && ((check.x + MINDIST) >> TILESHIFT) == tilex)
                return;
            check = actorat[tilex + 1, tiley];
            if (ISPOINTER(check) && ((check.x - MINDIST) >> TILESHIFT) == tilex)
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
            check = actorat[tilex, tiley - 1];
            if (ISPOINTER(check) && ((check.y + MINDIST) >> TILESHIFT) == tiley)
                return;
            check = actorat[tilex, tiley + 1];
            if (ISPOINTER(check) && ((check.y - MINDIST) >> TILESHIFT) == tiley)
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

        doorobjlist[door].action = (byte)dooractiontypes.dr_closing;
        //
        // make the door space solid
        //
        //actorat[tilex][tiley] = (objtype*)(uintptr_t)(door | BIT_DOOR);
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

        switch ((dooractiontypes)doorobjlist[door].action)
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
            actorat[doorobjlist[door].tilex, doorobjlist[door].tiley] = null;
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

        if (/*((int)(uintptr_t)actorat[tilex][tiley] != (door | BIT_DOOR))
            ||*/ (player.tilex == tilex && player.tiley == tiley))
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

            doorobjlist[door].action = (byte)dooractiontypes.dr_closed;

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
            switch ((dooractiontypes)doorobjlist[door].action)
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

        if (actorat[checkx + dx, checky + dy] != null)
        {
            SD_PlaySound((int)soundnames.NOWAYSND);
            return;
        }
        // TODO:
        //actorat[checkx + dx, checky + dy] = (objtype*)(uintptr_t)(tilemap[checkx + dx][checky + dy] = oldtile);

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
            actorat[pwallx, pwally] = null;
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

                if (actorat[pwallx + dx, pwally + dy] != null
                    || (xl <= pwallx + dx && pwallx + dx <= xh && yl <= pwally + dy && pwally + dy <= yh))
                {
                    pwallstate = 0;
                    tilemap[pwallx, pwally] = (byte)oldtile;
                    return;
                }
                // TODO: actorat is used as the collision detection layer too, need a better solution
                //actorat[pwallx + dx, pwally + dy] = (objtype*)(uintptr_t)(tilemap[pwallx + dx][pwally + dy] = oldtile);
                tilemap[pwallx + dx, pwally + dy] = BIT_WALL;
            }
        }

        pwallpos = (ushort)((pwallstate / 2) & 63);
    }
}
