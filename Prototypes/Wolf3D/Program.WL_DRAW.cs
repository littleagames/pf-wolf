using SDL2;
using System;
using System.Data;
using System.Runtime.InteropServices;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                   LOCAL CONSTANTS

    =============================================================================
    */

    internal const int ACTORSIZE = 0x4000;

    /*
    =============================================================================

                                  GLOBAL VARIABLES

    =============================================================================
    */

    static int vbuf; // pointer index
    static IntPtr vbufPtr = IntPtr.Zero;

    static visobj_t[] vislist = new visobj_t[MAXVISABLE];

    static int lasttimecount;
    static int frameon;
    static bool fpscounter = true;

    static int fps_frames = 0, fps_time = 0, fps = 0;

    internal static short[] wallheight;

    //
    // math tables
    //
    internal static short[] pixelangle;
    internal static int[] finetangent = new int[FINEANGLES / 4];
    internal static int[] sintable = new int[ANGLES + ANGLES / 4];
    internal static int[] costable => sintable[(ANGLES/4) ..]; // same as sintable, just offset by ANGLES/4

    //
    // refresh variables
    internal static int viewx, viewy;
    internal static short viewangle;
    internal static int viewsin, viewcos;

    internal static int postx;
    internal static byte[] postsource;

    //
    // ray tracing variables
    //
    internal static short focaltx, focalty;
    internal static uint xpartialup, xpartialdown, ypartialup, ypartialdown;

    internal static short midangle;

    internal static ushort tilehit;
    internal static int pixx;

    internal static short xtile, ytile;
    internal static short xtilestep, ytilestep;
    internal static int xintercept, yintercept;
    internal static int xinttile, yinttile;
    internal static ushort texdelta;

    internal static ushort[] horizwall = new ushort[MAXWALLTILES];
    internal static ushort[] vertwall = new ushort[MAXWALLTILES];

    /*
=====================
=
= CalcTics
=
=====================
*/

    internal static void CalcTics()
    {
        //
        // calculate tics since last refresh for adaptive timing
        //
        if (lasttimecount > (int)GetTimeCount())
            lasttimecount = (int)GetTimeCount();    // if the game was paused a LONG time

        uint curtime = SDL.SDL_GetTicks();
        tics = (uint)((curtime * 7) / 100 - lasttimecount);
        if (tics == 0)
        {
            // wait until end of current tic
            SDL.SDL_Delay((uint)(((lasttimecount + 1) * 100) / 7 - curtime));
            tics = 1;
        }

        lasttimecount += (int)tics;

        if (tics > MAXTICS)
            tics = MAXTICS;
    }

    internal static short CalcHeight()
    {
        short height;
        int gx, gy, gxt, gyt, nx;


        //
        // translate point to view centered coordinates
        //
        gx = xintercept - viewx;
        gy = yintercept - viewy;

        //
        // calculate nx
        //
        gxt = FixedMul(gx, viewcos);
        gyt = FixedMul(gy, viewsin);
        nx = gxt - gyt;

        //
        // calculate perspective ratio
        //
        if (nx < MINDIST)
            nx = (int)MINDIST;             // don't let divide overflow

        height = (short)(heightnumerator / (nx >> 8));
        return height;
    }

    internal static void Setup3DView()
    {
        viewangle = player.angle;
        midangle = (short)(viewangle * (FINEANGLES / ANGLES));

        viewsin = sintable[viewangle];
        viewcos = costable[viewangle];

        viewx = player.x - FixedMul(focallength, viewcos);
        viewy = player.y + FixedMul(focallength, viewsin);

        focaltx = (short)(viewx >> (int)TILESHIFT);
        focalty = (short)(viewy >> (int)TILESHIFT);

        xpartialdown = (uint)(viewx & (TILEGLOBAL - 1));
        xpartialup = (uint)(xpartialdown ^ (TILEGLOBAL - 1));
        ypartialdown = (uint)(viewy & (TILEGLOBAL - 1));
        ypartialup = (uint)(ypartialdown ^ (TILEGLOBAL - 1));
    }

    //==========================================================================

    /*
    ===================
    =
    = ScalePost
    =
    ===================
    */
    internal static void ScalePost()
    {
        int ywcount, yoffs, yw, yd, yendoffs;
        byte col;

        ywcount = yd = wallheight[postx] >> 3;
        if (yd <= 0) yd = 100;

        yoffs = (int)((centery - ywcount) * bufferPitch);
        if (yoffs < 0) yoffs = 0;
        yoffs += postx;

        yendoffs = centery + ywcount - 1;
        yw = TEXTURESIZE - 1;

        while (yendoffs >= viewheight)
        {
            ywcount -= TEXTURESIZE / 2;
            while (ywcount <= 0)
            {
                ywcount += yd;
                yw--;
            }
            yendoffs--;
        }
        if (yw < 0) return;

        col = postsource[yw];
        yendoffs = (int)(yendoffs * bufferPitch + postx);
        while (yoffs <= yendoffs)
        {
            unsafe
            {
                byte* dest = (byte*)vbufPtr + screenofs;
                dest[yendoffs] = col; // get the surface pixels
            }
            ywcount -= TEXTURESIZE / 2;
            if (ywcount <= 0)
            {
                do
                {
                    ywcount += yd;
                    yw--;
                }
                while (ywcount <= 0);
                if (yw < 0) break;
                col = postsource[yw];
            }
            yendoffs -= (int)bufferPitch;
        }
    }


    /*
    ====================
    =
    = HitVertWall
    =
    = tilehit bit 7 is 0, because it's not a door tile
    = if bit 6 is 1 and the adjacent tile is a door tile, use door side pic
    =
    ====================
    */

    internal static void HitVertWall()
    {
        int wallpic;
        int texture;

        texture = ((yintercept - texdelta) >> FIXED2TEXSHIFT) & TEXTUREMASK;

        if (xtilestep == -1)
        {
            texture = TEXTUREMASK - texture;
            xintercept += (int)TILEGLOBAL;
        }
        wallheight[pixx] = CalcHeight();
        postx = pixx;

        if ((tilehit & BIT_WALL) != 0)
        {
            //
            // check for adjacent doors
            //
            if ((tilemap[xtile - xtilestep, yinttile] & BIT_DOOR) != 0)
                wallpic = DOORWALL + 3;
            else
                wallpic = vertwall[tilehit & ~BIT_WALL];
        }
        else
            wallpic = vertwall[tilehit];

        postsource = PM_GetPage(wallpic).Skip(texture).ToArray();
        ScalePost();
    }

    /*
    ====================
    =
    = HitHorizWall
    =
    = tilehit bit 7 is 0, because it's not a door tile
    = if bit 6 is 1 and the adjacent tile is a door tile, use door side pic
    =
    ====================
    */

    internal static void HitHorizWall()
    {
        int wallpic;
        int texture;

        texture = ((xintercept - texdelta) >> FIXED2TEXSHIFT) & TEXTUREMASK;

        if (ytilestep == -1)
            yintercept += (int)TILEGLOBAL;
        else
            texture = TEXTUREMASK - texture;

        wallheight[pixx] = CalcHeight();
        postx = pixx;

        if ((tilehit & BIT_WALL) != 0)
        {
            //
            // check for adjacent doors
            //
            if ((tilemap[xinttile, ytile - ytilestep] & BIT_DOOR) != 0)
                wallpic = DOORWALL + 2;
            else
                wallpic = horizwall[tilehit & ~BIT_WALL];
        }
        else
            wallpic = horizwall[tilehit];

        postsource = PM_GetPage(wallpic).Skip(texture).ToArray();
        ScalePost();
    }

    internal static void HitVertDoor()
    {
        int doorpage = 0;
        int doornum;
        int texture;

        doornum = tilehit & ~BIT_DOOR;
        texture = ((yintercept - doorobjlist[doornum].position) >> FIXED2TEXSHIFT) & TEXTUREMASK;

        wallheight[pixx] = CalcHeight();
        postx = pixx;

        switch ((doortypes)doorobjlist[doornum].locknum)
        {
            case doortypes.dr_normal:
                doorpage = DOORWALL + 1;
                break;

            case doortypes.dr_lock1:
            case doortypes.dr_lock2:
            case doortypes.dr_lock3:
            case doortypes.dr_lock4:
                doorpage = DOORWALL + 7;
                break;

            case doortypes.dr_elevator:
                doorpage = DOORWALL + 5;
                break;
        }

        postsource = PM_GetPage(doorpage).Skip(texture).ToArray();// + texture;

        ScalePost();
    }

    internal static void HitHorizDoor()
    {
        int doorpage = 0;
        int doornum;
        int texture;

        doornum = tilehit & ~BIT_DOOR;
        texture = ((xintercept - doorobjlist[doornum].position) >> FIXED2TEXSHIFT) & TEXTUREMASK;

        wallheight[pixx] = CalcHeight();
        postx = pixx;

        switch ((doortypes)doorobjlist[doornum].locknum)
        {
            case doortypes.dr_normal:
                doorpage = DOORWALL;
                break;

            case doortypes.dr_lock1:
            case doortypes.dr_lock2:
            case doortypes.dr_lock3:
            case doortypes.dr_lock4:
                doorpage = DOORWALL + 6;
                break;

            case doortypes.dr_elevator:
                doorpage = DOORWALL + 4;
                break;
            }

        postsource = PM_GetPage(doorpage).Skip(texture).ToArray();// + texture;

        ScalePost();
    }

    internal static byte[] vgaCeiling =
    {
        0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0x1d,0xbf,
        0x4e,0x4e,0x4e,0x1d,0x8d,0x4e,0x1d,0x2d,0x1d,0x8d,
        0x1d,0x1d,0x1d,0x1d,0x1d,0x2d,0xdd,0x1d,0x1d,0x98,

        0x1d,0x9d,0x2d,0xdd,0xdd,0x9d,0x2d,0x4d,0x1d,0xdd,
        0x7d,0x1d,0x2d,0x2d,0xdd,0xd7,0x1d,0x1d,0x1d,0x2d,
        0x1d,0x1d,0x1d,0x1d,0xdd,0xdd,0x7d,0xdd,0xdd,0xdd
    };

    internal static void VGAClearScreen()
    {
        byte ceiling = vgaCeiling[(gamestate.episode * 10) + gamestate.mapon];
        var destIndex = vbuf;
        int y;
        unsafe
        {
            byte* dest = (byte*)vbufPtr;// + destIndex;
            for (y = 0; y < centery; y++, destIndex += (int)bufferPitch)
                for(var v = 0; v < viewwidth; v++)
                    dest[destIndex+v] = ceiling;
                    //Array.Fill(dest, ceiling, destIndex, viewwidth);

            for (; y < viewheight; y++, destIndex += (int)bufferPitch)
                for (var v = 0; v < viewwidth; v++)
                    dest[destIndex + v] = 0x19;
                    //Array.Fill(dest, 0x19, destIndex, viewwidth);
        }
    }


    private static void WallRefresh()
    {
        short angle;
        int xstep = 0, ystep = 0;
        uint xpartial = 0, ypartial = 0;

        for (pixx = 0; pixx < viewwidth; pixx++)
        {
            angle = (short)(midangle + pixelangle[pixx]);

            if (angle < 0) // -90 - -1 degree arc
                angle += ANG360; // -90 is the same as 270
            if (angle >= ANG360) // 360-449 degree arc
                angle -= ANG360; // -449 is the same as 89

            //
            // setup xstep/ystep based on angle
            //
            if (angle < ANG90) // 0-89 degree arc
            {
                xtilestep = 1;
                ytilestep = -1;
                xstep = finetangent[ANG90 - 1 - angle];
                ystep = -finetangent[angle];
                xpartial = (uint)xpartialup;
                ypartial = (uint)ypartialdown;
            }
            else if (angle < ANG180) // 90-179 degree arc
            {
                xtilestep = -1;
                ytilestep = -1;
                xstep = -finetangent[angle - ANG90];
                ystep = -finetangent[ANG180 - 1 - angle];
                xpartial = (uint)xpartialdown;
                ypartial = (uint)ypartialdown;
            }
            else if (angle < ANG270) // 180-269 degree arc
            {
                xtilestep = -1;
                ytilestep = 1;
                xstep = -finetangent[ANG270 - 1 - angle];
                ystep = finetangent[angle - ANG180];
                xpartial = (uint)xpartialdown;
                ypartial = (uint)ypartialup;
            }
            else if (angle < ANG360) // 270-359 degree arc
            {
                xtilestep = 1;
                ytilestep = 1;
                xstep = finetangent[angle - ANG270];
                ystep = finetangent[ANG360 - 1 - angle];
                xpartial = (uint)xpartialup;
                ypartial = (uint)ypartialup;
            }

            //
            // initialise variables for intersection testing
            //
            yintercept = FixedMul(ystep, (int)xpartial) + viewy;
            yinttile = yintercept >> (int)TILESHIFT;
            xtile = (short)(focaltx + xtilestep);

            xintercept = FixedMul(xstep, (int)ypartial) + viewx;
            xinttile = xintercept >> (int)TILESHIFT;
            ytile = (short)(focalty + ytilestep);

            texdelta = 0;

            //
            // trace along this angle until we hit a wall
            //
            // CORE LOOP!
            //
            var tileFound = false;
            while (!tileFound)
            {
                //
                // check intersections with vertical walls
                //
                if ((xtile - xtilestep) == xinttile && (ytile - ytilestep) == yinttile)
                    yinttile = ytile;

                if ((ytilestep == -1 && yinttile <= ytile) || (ytilestep == 1 && yinttile >= ytile))
                    tileFound = horizentry(xstep);

                if (tileFound) break;

                //tileFound = vertentry(ystep, xstep);

                //
                // check intersections with horizontal walls
                //
                if ((xtile - xtilestep) == xinttile && (ytile - ytilestep) == yinttile)
                    xinttile = xtile;

                if ((xtilestep == -1 && xinttile <= xtile) || (xtilestep == 1 && xinttile >= xtile))
                    tileFound = vertentry(ystep, xstep);
            }
        }
    }

    private static bool vertentry(int ystep, int xstep)
    {
        int pwallposnorm, pwallposinv, pwallposi;           // holds modified pwallpos

        int yinttemp;
        // #ifdef REVEALMAP
        //             mapseen[xtile][yinttile] = true;
        // #endif
        tilehit = tilemap[xtile, yinttile];

        if (tilehit != 0)
        {
            if ((tilehit & BIT_DOOR) != 0)
            {
                //
                // hit a vertical door, so find which coordinate the door would be
                // intersected at, and check to see if the door is open past that point
                //
                var door = doorobjlist[tilehit & ~BIT_DOOR];

                if (door.action == (byte)dooractiontypes.dr_open)
                {
                    passvert(ystep); // door is open, continue tracing
                    return false;
                }

                yinttemp = yintercept + (ystep >> 1);    // add halfstep to current intercept position

                //
                // midpoint is outside tile, so it hit the side of the wall before a door
                //
                if (yinttemp >> TILESHIFT != yinttile)
                {
                    passvert(ystep);
                    return false;
                }

                if (door.action != (byte)dooractiontypes.dr_closed)
                {
                    //
                    // the trace hit the door plane at pixel position yintercept, see if the door is
                    // closed that much
                    //
                    if ((ushort)yinttemp < door.position)
                    {
                        passvert(ystep);
                        return false;
                    }
                }

                yintercept = yinttemp;
                xintercept = (int)((xtile << (int)TILESHIFT) + (TILEGLOBAL / 2));

                HitVertDoor();
            }
            else if (tilehit == BIT_WALL)
            {
                //
                // hit a sliding vertical wall
                //
                if (pwalldir == (byte)controldirs.di_west || pwalldir == (byte)controldirs.di_east)
                {
                    if (pwalldir == (byte)controldirs.di_west)
                    {
                        pwallposnorm = 64 - pwallpos;
                        pwallposinv = pwallpos;
                    }
                    else
                    {
                        pwallposnorm = pwallpos;
                        pwallposinv = 64 - pwallpos;
                    }

                    if ((pwalldir == (byte)controldirs.di_east && xtile == pwallx && yinttile == pwally)
                     || (pwalldir == (byte)controldirs.di_west && !(xtile == pwallx && yinttile == pwally)))
                    {
                        yinttemp = yintercept + ((ystep * pwallposnorm) >> 6);

                        if (yinttemp >> TILESHIFT != yinttile)
                        {
                            passvert(ystep);
                            return false;
                        }

                        yintercept = yinttemp;
                        xintercept = (int)(((xtile << TILESHIFT) + TILEGLOBAL) - (pwallposinv << 10));
                        yinttile = yintercept >> TILESHIFT;
                        tilehit = pwalltile;

                        HitVertWall();
                    }
                    else
                    {
                        yinttemp = yintercept + ((ystep * pwallposinv) >> 6);

                        if (yinttemp >> TILESHIFT != yinttile)
                        {
                            passvert(ystep);
                            return false;
                        }

                        yintercept = yinttemp;
                        xintercept = (xtile << TILESHIFT) -(pwallposinv << 10);
                        yinttile = yintercept >> TILESHIFT;
                        tilehit = pwalltile;

                        HitVertWall();
                    }
                }
                else
                {
                    if (pwalldir == (byte)controldirs.di_north)
                        pwallposi = 64 - pwallpos;
                    else
                        pwallposi = pwallpos;

                    if ((pwalldir == (byte)controldirs.di_south && (ushort)yintercept < (pwallposi << 10))
                     || (pwalldir == (byte)controldirs.di_north && (ushort)yintercept > (pwallposi << 10)))
                    {
                        if (xtile == pwallx && yinttile == pwally)
                        {
                            if ((pwalldir == (byte)controldirs.di_south && (int)((ushort)yintercept) + ystep < (pwallposi << 10))
                             || (pwalldir == (byte)controldirs.di_north && (int)((ushort)yintercept) + ystep > (pwallposi << 10)))
                            {
                                //goto passvert;
                                passvert(ystep);
                                return false;
                            }

                            //
                            // set up a horizontal intercept position
                            //
                            if (pwalldir == (byte)controldirs.di_south)
                                yintercept = (yinttile << TILESHIFT) + (pwallposi << 10);
                            else
                                yintercept = (int)(((yinttile << TILESHIFT) - TILEGLOBAL) + (pwallposi << 10));

                            xintercept -= (xstep * (64 - pwallpos)) >> 6;
                            xinttile = xintercept >> TILESHIFT;
                            tilehit = pwalltile;

                            HitHorizWall();
                        }
                        else
                        {
                            texdelta = (ushort)(pwallposi << 10);
                            xintercept = xtile << TILESHIFT;
                            tilehit = pwalltile;

                            HitVertWall();
                        }
                    }
                    else
                    {
                        if (xtile == pwallx && yinttile == pwally)
                        {
                            texdelta = (ushort)(pwallposi << 10);
                            xintercept = xtile << TILESHIFT;
                            tilehit = pwalltile;

                            HitVertWall();
                        }
                        else
                        {
                            if ((pwalldir == (byte)controldirs.di_south && (int)((ushort)yintercept) + ystep > (pwallposi << 10))
                             || (pwalldir == (byte)controldirs.di_north && (int)((ushort)yintercept) + ystep < (pwallposi << 10)))
                            {
                                //goto passvert;
                                passvert(ystep);
                                return false;
                            }

                            //
                            // set up a horizontal intercept position
                            //
                            if (pwalldir == (byte)controldirs.di_south)
                                yintercept = (yinttile << TILESHIFT) - ((64 - pwallpos) << 10);
                            else
                                yintercept = (yinttile << TILESHIFT) + ((64 - pwallpos) << 10);

                            xintercept -= (xstep * pwallpos) >> 6;
                            xinttile = xintercept >> TILESHIFT;
                            tilehit = pwalltile;

                            HitHorizWall();
                        }
                    }
                }
            }
            else
            {
                xintercept = xtile << (int)TILESHIFT;

                HitVertWall();
            }

            return true;
        }

        //
        // mark the tile as visible and setup for next step
        //
        spotvis[xtile, yinttile] = true;
        passvert(ystep);
        return false;
    }

    private static void passvert(int ystep)
    {
        spotvis[xtile, yinttile] = true;
        xtile += xtilestep;
        yintercept += ystep;
        yinttile = yintercept >> (int)TILESHIFT;
    }

    private static bool horizentry(int xstep)
    {
        int xinttemp;
        // #ifdef REVEALMAP
        //             mapseen[xinttile][ytile] = true;
        // #endif
        // TODO: Turn tilehit into a Wall vs Door MapComponent check
        //tilehit2 = _map.TilePlane[ytile][xinttile];
        //tilehit = _map.PlaneIds[0][ytile, xinttile];//tilemap[xinttile][ytile];
        tilehit = tilemap[xinttile, ytile];

        if (tilehit != 0)
        {
            if ((tilehit & BIT_DOOR) != 0)
            {
                //
                // hit a horizontal door, so find which coordinate the door would be
                // intersected at, and check to see if the door is open past that point
                //
                var door = doorobjlist[tilehit & ~BIT_DOOR];

                if (door.action == (byte)dooractiontypes.dr_open)
                {
                    passhoriz(xstep); // door is open, continue tracing
                    return false;
                }

                xinttemp = xintercept + (xstep >> 1);    // add half step to current intercept position

                //
                // midpoint is outside tile, so it hit the side of the wall before a door
                //
                if ((xinttemp >> TILESHIFT) != xinttile)
                {
                    passhoriz(xstep);
                    return false;
                }

                if (door.action != (byte)dooractiontypes.dr_closed)
                {
                    //
                    // the trace hit the door plane at pixel position xintercept, see if the door is
                    // closed that much
                    //
                    if ((ushort)xinttemp < door.position)
                    {
                        passhoriz(xstep);
                        return false;
                    }
                }

                xintercept = xinttemp;
                yintercept = (int)((ytile << (int)TILESHIFT) + (TILEGLOBAL / 2));

                HitHorizDoor();
            }
            else if (tilehit == BIT_WALL)
            {
                // TODO: Moving pushwalls
            }
            else
            {
                yintercept = ytile << (int)TILESHIFT;

                HitHorizWall();
            }

            return true;
        }

        //
        // mark the tile as visible and setup for next step
        //
        spotvis[xinttile, ytile] = true;
        passhoriz(xstep);
        return false;
    }

    private static void passhoriz(int xstep)
    {
        spotvis[xinttile, ytile] = true;
        ytile += ytilestep;
        xintercept += xstep;
        xinttile = xintercept >> (int)TILESHIFT;
    }
    /*
========================
=
= TransformTile
=
= Takes paramaters:
=   tx,ty               : tile the object is centered in
=
= globals:
=   viewx,viewy         : point of view
=   viewcos,viewsin     : sin/cos of viewangle
=   scale               : conversion from global value to screen value
=
= sets:
=   screenx,transx,transy,screenheight: projected edge location and size
=
= Returns true if the tile is withing getting distance
=
========================
*/

    internal static bool TransformTile(int tx, int ty, ref short dispx, ref short dispheight)
    {
        int gx, gy, gxt, gyt, nx, ny;

        //
        // translate point to view centered coordinates
        //
        gx = ((int)tx << TILESHIFT) + 0x8000 - viewx;
        gy = ((int)ty << TILESHIFT) + 0x8000 - viewy;

        //
        // calculate newx
        //
        gxt = FixedMul(gx, viewcos);
        gyt = FixedMul(gy, viewsin);
        nx = gxt - gyt - 0x2000;            // 0x2000 is size of object

        //
        // calculate newy
        //
        gxt = FixedMul(gx, viewsin);
        gyt = FixedMul(gy, viewcos);
        ny = gyt + gxt;


        //
        // calculate height / perspective ratio
        //
        if (nx < MINDIST)                 // too close, don't overflow the divide
            dispheight = 0;
        else
        {
            dispx = (short)(centerx + ny * scale / nx);
            dispheight = (short)(heightnumerator / (nx >> 8));
        }

        //
        // see if it should be grabbed
        //
        if (nx < TILEGLOBAL && ny > -TILEGLOBAL / 2 && ny < TILEGLOBAL / 2)
            return true;
        else
            return false;
    }

    internal static void DrawScaleds()
    {
        int i, least, numvisable, height;
        byte[] visspot;
        int statptr;
        objstruct obj;
        int farthest = -1;
        int visptr, visstep;

        visptr = 0;

        //
        // place static objects
        //
        for (statptr = 0; statptr != laststatobj; statptr++)
        {
            visobj_t visptr_val = new visobj_t();
            statobj_t statptr_val = statobjlist[statptr];
            if ((visptr_val.shapenum = statptr_val.shapenum) == -1)
                continue;                                               // object has been deleted

            if (!spotvis[statptr_val.tilex, statptr_val.tiley])
                continue;                                               // not visable

            if (TransformTile(statptr_val.tilex, statptr_val.tiley,
                ref visptr_val.viewx, ref visptr_val.viewheight) && (statptr_val.flags & (int)objflags.FL_BONUS) != 0)
            {
                GetBonus(statptr_val);
                if (statptr_val.shapenum == -1)
                    continue;                                           // object has been taken
            }

            if (visptr_val.viewheight == 0)
                continue;                                               // to close to the object

            if (visptr < (MAXVISABLE - 1))    // don't let it overflow
            {
                visptr_val.tilex = statptr_val.tilex;
                visptr_val.tiley = statptr_val.tiley;
                visptr_val.flags = statptr_val.flags;
                vislist[visptr] = visptr_val;
                visptr++;
            }
        }

        ////
        //// place active objects
        ////
        //for (obj = player.next; obj; obj = obj.next)
        //{
        //    if ((visptr_val.shapenum = obj.state.shapenum) == 0)
        //        continue;                                               // no shape

        //    visspot = (byte*)&spotvis[obj.tilex, obj.tiley];

        //    //
        //    // could be in any of the nine surrounding tiles
        //    //
        //    if (*visspot
        //        || (*(visspot - 1))
        //        || (*(visspot + 1))
        //        || (*(visspot - (MAPSIZE + 1)))
        //        || (*(visspot - (MAPSIZE)))
        //        || (*(visspot - (MAPSIZE - 1)))
        //        || (*(visspot + (MAPSIZE + 1)))
        //        || (*(visspot + (MAPSIZE)))
        //        || (*(visspot + (MAPSIZE - 1))))
        //    {
        //        obj->active = ac_yes;
        //        TransformActor(obj);
        //        if (!obj->viewheight)
        //            continue;                                               // too close or far away

        //        visptr->viewx = obj->viewx;
        //        visptr->viewheight = obj->viewheight;
        //        if (visptr->shapenum == -1)
        //            visptr->shapenum = obj->temp1;  // special shape

        //        if (obj->state->rotate)
        //            visptr->shapenum += CalcRotate(obj);

        //        if (visptr < &vislist[MAXVISABLE - 1])    // don't let it overflow
        //        {
        //            visptr->tilex = obj->x >> TILESHIFT;
        //            visptr->tiley = obj->y >> TILESHIFT;
        //            visptr->flags = obj->flags;
        //            visptr++;
        //        }
        //        obj->flags |= FL_VISABLE;
        //    }
        //    else
        //        obj->flags &= ~FL_VISABLE;
        //}

        //
        // draw from back to front
        //
        numvisable = (int)(visptr);

        if (numvisable == 0)
            return;                                                                 // no visable objects

        for (i = 0; i < numvisable; i++)
        {
            least = 32000;
            for (visstep = 0; visstep < visptr; visstep++)
            {
                visobj_t visstep_val = vislist[visstep];
                height = visstep_val.viewheight;
                if (height < least)
                {
                    least = height;
                    farthest = visstep;
                }
            }
            //
            // draw farthest
            //
            if (farthest != -1)
            {
                visobj_t farthest_obj = vislist[farthest];
                ScaleShape(farthest_obj);

                farthest_obj.viewheight = 32000;
            }
        }
    }

    internal static void DrawPlayerWeapon()
    {
        // TODO:
    }

    internal static void ThreeDRefresh()
    {
        // Create a Span<T> from the multidimensional array's data reference
        Span<bool> data = MemoryMarshal.CreateSpan(
            ref spotvis[0, 0], // Reference to the first element
            spotvis.Length     // Total number of elements
        );

        // Fill the span with the value
        data.Fill(false);


        if (tilemap[player.tilex, player.tiley] == 0 ||
             (tilemap[player.tilex, player.tiley] & BIT_DOOR) != 0)
            spotvis[player.tilex, player.tiley] = true;       // Detect all sprites over player fix


        vbuf = 0;
        vbufPtr = VL_LockSurface(screenBuffer);
        if (vbufPtr == IntPtr.Zero) return;

        vbuf += (int)screenofs;

        Setup3DView();

        //
        // follow the walls from there to the right, drawing as we go
        //
        VGAClearScreen();

        WallRefresh();

        //
        // draw all the scaled images
        //
        DrawScaleds();                  // draw scaled stuff

        DrawPlayerWeapon();    // draw player's hands

        //if (Keyboard[(int)ScanCodes.sc_Tab] && viewsize == 21 && gamestate.weapon != -1)
        //    ShowActStatus();

        VL_UnlockSurface(screenBuffer);
        vbuf = 0;

        //
        // show screen and time last cycle
        //

        if (fizzlein)
        {
            FizzleFade(screenBuffer, 0, 0, (uint)screenWidth, (uint)screenHeight, 20, false);
            fizzlein = false;

            lasttimecount = (int)GetTimeCount();          // don't make a big tic count
        }
        else
        {
            if (fpscounter)
            {
                fontnumber = 0;
                SETFONTCOLOR(7, 127);
                PrintX = 4; PrintY = 1;
                VWB_Bar(0, 0, 50, 10, bordercol);
                US_PrintSigned(fps);
                US_Print(" fps");
            }
            VW_UpdateScreen();
        }

        if (fpscounter)
        {
            fps_frames++;
            fps_time += (int)tics;

            if (fps_time > 35)
            {
                fps_time -= 35;
                fps = fps_frames << 1;
                fps_frames = 0;
            }
        }
    }
}
