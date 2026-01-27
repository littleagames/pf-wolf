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
    internal static int[] costable = sintable[(ANGLES/4) ..]; // same as sintable, just offset by ANGLES/4

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

    internal static void WallRefresh()
    {
        // TODO:
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
