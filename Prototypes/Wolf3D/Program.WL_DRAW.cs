using SDL2;
using System;
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

    //static visobj_t[] vislist = new visobj_t[MAXVISABLE];

    static int lasttimecount;
    static int frameon;
    static bool fpscounter;

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

    }

    internal static void HitHorizDoor()
    {

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


    private static void WallRefresh()//double midAngle, short focaltx, short focalty)
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

                //tileFound = vertentry(ystep);

                //
                // check intersections with horizontal walls
                //
                if ((xtile - xtilestep) == xinttile && (ytile - ytilestep) == yinttile)
                    xinttile = xtile;

                if ((xtilestep == -1 && xinttile <= xtile) || (xtilestep == 1 && xinttile >= xtile))
                    tileFound = vertentry(ystep);
            }
        }
    }

    private static bool vertentry(int ystep)
    {
        int yinttemp;
        // #ifdef REVEALMAP
        //             mapseen[xtile][yinttile] = true;
        // #endif
       // tilehit2 = _map.TilePlane[yinttile][xtile];
       // tilehit = _map.PlaneIds[0][yinttile, xtile]; // tilemap[xtile][yinttile];
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
                // TODO:
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


    internal static void DrawScaleds()
    {
        // TODO:
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
