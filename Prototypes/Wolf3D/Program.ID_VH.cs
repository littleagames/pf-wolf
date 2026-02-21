using SDL2;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Wolf3D;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct pictabletype
{
    public short width;
    public short height;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct fontstruct
{
    public short height;
    public short[] location;
    public byte[] width;

    public fontstruct()
    {
        location = new short[256];
        width = new byte[256];
    }
}

internal partial class Program
{
    internal static int WHITE = 15;			// graphics mode independant colors
    internal static int BLACK		= 0;
    internal static int FIRSTCOLOR	= 1;
    internal static int SECONDCOLOR	= 12;
    internal static int F_WHITE		= 15;
    internal static int F_BLACK		= 0;
    internal static int F_FIRSTCOLOR= 1;
    internal static int F_SECONDCOLOR = 12;

    static pictabletype[] pictable;
    static int px, py;
    static byte fontcolor, backcolor;
    static int fontnumber;

    static readonly UInt32[] rndmasks = {
                    // n    XNOR from (starting at 1, not 0 as usual)
    0x00012000,     // 17   17,14
    0x00020400,     // 18   18,11
    0x00040023,     // 19   19,6,2,1
    0x00090000,     // 20   20,17
    0x00140000,     // 21   21,19
    0x00300000,     // 22   22,21
    0x00420000,     // 23   23,18
    0x00e10000,     // 24   24,23,22,17
    0x01200000,     // 25   25,22      (this is enough for 8191x4095)
};
    static uint rndbits_y;
    static uint rndmask;

    internal static void SETFONTCOLOR(byte f, byte b)
    {
        fontcolor = f;
        backcolor = b;
    }

    internal static void VWB_Bar(int x, int y, int width, int height, int color) => VW_Bar(x, y, width, height, color);
    internal static void VW_Bar(int x, int y, int width, int height, int color) => VL_Bar(x, y, width, height, color);
    internal static void VWB_BarScaledCoord(int scx, int scy, int scwidth, int scheight, int color) => VL_BarScaledCoord(scx, scy, scwidth, scheight, color);
    internal static void VL_BarScaledCoord(int scx, int scy, int scwidth, int scheight, int color)
    {
        Debug.Assert(scx >= 0 && scx + scwidth <= screenWidth
            && scy >= 0 && scy + scheight <= screenHeight,
            "VL_BarScaledCoord: Destination rectangle out of bounds!");

        IntPtr destPtr = VL_LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero) return;

        unsafe
        {
            byte* dest = (byte*)destPtr;

            dest += ylookup[scy] + scx;


            while (scheight-- > 0)
            {
                // memset equivalent: set scwidth bytes to (byte)color
                for (int i = 0; i < scwidth; i++)
                {
                    dest[i] = (byte)color;
                }

                dest += bufferPitch;
            }
        }

        VL_UnlockSurface(screenBuffer);
    }

    internal static void VWB_Hlin(int x1, int x2, int y, int color)
    {
        if (scaleFactor == 1)
            VW_Hlin(x1, x2, y, color);
        else
            VW_Bar(x1, y, x2 - x1 + 1, 1, color);
    }

    internal static void VWB_Vlin(int y1, int y2, int x, int color)
    {
        if (scaleFactor == 1)
            VW_Vlin(y1, y2, x, color);
        else
            VW_Bar(x, y1, 1, y2 - y1 + 1, color);
    }

    internal static void VW_Hlin(int x, int z, int y, int c) => VL_Hlin(x, y, (z) - (x) + 1, c);
    internal static void VW_Vlin(int y, int z, int x, int c) => VL_Vlin(x, y, (z) - (y) + 1, c);
    internal static void VWB_DrawTile8(int x, int y, int tile)
    {
        VL_MemToScreen(grsegs[STARTTILE8].Skip(tile * 64).ToArray(), 8, 8, x, y);
    }

    internal static void VWB_DrawPic(int x, int y, int chunknum)
    {
        int picnum = chunknum - STARTPICS;
        int width, height;

        x &= ~7;

        width = pictable[picnum].width;
        height = pictable[picnum].height;

        VL_MemToScreen(grsegs[chunknum], width, height, x, y);
    }

    internal static void VWB_DrawPicScaledCoord(int scx, int scy, int chunknum)
    {
        int picnum = chunknum - STARTPICS;
        short width, height;

        width = pictable[picnum].width;
        height = pictable[picnum].height;

        VL_MemToScreenScaledCoord(grsegs[chunknum], width, height, scx, scy);
    }

    internal static void VW_MeasurePropString (string text, out ushort width, out ushort height)
    {
        var data = grsegs[STARTFONT + fontnumber];

        int dataIndex = 0;
        fontstruct font = FontHelper.GetFont(data);
        // ignoring the rest of the data (we don't need it here)
        VWL_MeasureString(text, out width, out height, font);
    }

    internal static void VWL_MeasureString (string text, out ushort width, out ushort height, fontstruct font)
    {
        width = 0;
        int i;
        height = (ushort)font.height;
        for (i = 0; i < text.Length; i++)
        {
            width += font.width[text[i]]; // proportional width
        }
    }

    internal static void VWB_DrawPropString(string text)
    {
        fontstruct font;
        int width, step, height;
        byte[] source;

        int i;
        int sx, sy;

        IntPtr destPtr = VL_LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero)
            return;

        byte[] data = grsegs[STARTFONT + fontnumber];
        font = FontHelper.GetFont(data);

        height = font.height;

        unsafe
        {
            byte* dest = (byte*) destPtr;
            dest += scaleFactor * (ylookup[py] + px); // starting point on the screenbuffer

            foreach (char ch in text.ToCharArray())
            {
                width = step = font.width[ch];
                int locIndex = font.location[ch];

                while (width-- != 0)
                {
                    for (i = 0; i < height; i++)
                    {
                        if (data[locIndex+(i*step)] != 0)
                        {
                            for (sy = 0; sy < scaleFactor; sy++)
                                for (sx = 0; sx < scaleFactor; sx++)
                                    dest[ylookup[scaleFactor * i + sy] + sx] = fontcolor;
                        }
                    }

                    locIndex++;
                    px++;
                    dest += scaleFactor;
                }
            }

            VL_UnlockSurface(screenBuffer);
        }
    }

    internal static void VW_UpdateScreen() => VH_UpdateScreen(screenBuffer);

    internal static void VH_UpdateScreen(IntPtr surface)
    {
        SDL.SDL_BlitSurface(surface, IntPtr.Zero, screen, IntPtr.Zero);
        
        var screenPixels = GetSurface(screen).pixels;
        SDL.SDL_UpdateTexture(texture, IntPtr.Zero, screenPixels, (int)screenPitch);
        SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
        SDL.SDL_RenderPresent(renderer);
    }
    static int log2_ceil(UInt32 x)
    {
        int n = 0;
        UInt32 v = 1;
        while (v < x)
        {
            n++;
            v <<= 1;
        }
        return n;
    }
    internal static void VH_Startup()
    {
        int rndbits_x = log2_ceil((UInt32)screenWidth);
        rndbits_y = (uint)log2_ceil((UInt32)screenHeight);

        int rndbits = rndbits_x + (int)rndbits_y;
        if (rndbits < 17)
            rndbits = 17;       // no problem, just a bit slower
        else if (rndbits > 25)
            rndbits = 25;       // fizzle fade will not fill whole screen

        rndmask = rndmasks[rndbits - 17];
    }

    internal static void VW_FadeIn() => VL_FadeIn(0, 255, gamepal, 30);
    internal static void VW_FadeOut() => VL_FadeOut(0, 255, 0, 0, 0, 30);

    internal static bool FizzleFade(IntPtr source, int x1, int y1, uint width, uint height, uint frames, bool abortable)
    {
        uint x=0, y=0, p, frame, pixperframe;
        int rndval;

        rndval = 1;
        pixperframe = width * height / frames;

        IN_StartAck();

        frame = GetTimeCount();
        IntPtr srcptr = VL_LockSurface(source);
        if (srcptr == IntPtr.Zero) return false;

        while (true)
        {
            IN_ProcessEvents();

            if (abortable && IN_CheckAck())
            {
                VL_UnlockSurface(source);
                VH_UpdateScreen(source);
                return true;
            }

            IntPtr destptr = VL_LockSurface(screen);

            if (destptr == IntPtr.Zero)
                Quit($"Unable to lock dest surface: {SDL.SDL_GetError()}\n");

            var scrn_surface = GetSurface(screen);
            var src_surface = GetSurface(source);

            for (p = 0; p < pixperframe; p++)
            {
                //
                // seperate random value into x/y pair
                //
                x = (uint)(rndval >> (int)rndbits_y);
                y = (uint)(rndval & ((1 << (int)rndbits_y) - 1));

                //
                // advance to next random element
                //
                rndval = (int)((rndval >> 1) ^ ((rndval & 1) != 0 ? 0 : rndmask));

                if (x >= width || y >= height)
                    p--;                         // not into the view area; get a new pair
                else
                {
                    unsafe
                    {
                        byte* src = (byte*)srcptr;
                        byte* dest = (byte*)destptr;
                        //
                        // copy one pixel
                        //
                        if (screenBits == 8)
                        {
                            dest[((y1 + y) * scrn_surface.pitch + x1 + x)] =
                                src[((y1 + y) * src_surface.pitch + x1 + x)];
                            //*(destptr + (y1 + y) * scrn_surface.pitch + x1 + x)
                            //   = *(srcptr + (y1 + y) * src_surface.pitch + x1 + x);
                        }
                        else
                        {
                            var screen_format = GetSurfaceFormat(screen);
                            var scrnBpp = screen_format.BytesPerPixel;
                            byte col = src[(y1 + y) * src_surface.pitch + x1 + x];//*(srcptr + (y1 + y) * src_surface.pitch + x1 + x);
                            uint fullcol = SDL.SDL_MapRGBA(scrn_surface.format, curpal[col].r, curpal[col].g, curpal[col].b, 255);//SDL_ALPHA_OPAQUE);

                            // saving "fullcol" into a full bpp of the dest
                            //memcpy (dest, src, count)
                            var fullColBytes = BitConverter.GetBytes(fullcol);
                            for (var b = 0; b < scrnBpp; b++)
                            {
                                dest[(y1 + y) * scrn_surface.pitch + (x1 + x) * scrnBpp + b] = fullColBytes[b];
                            }
                            //memcpy(dest + (y1 + y) * scrn_surface.pitch + (x1 + x) * scrnBpp, fullcol, scrnBpp);
                        }
                    }
                }

                if (rndval == 1)
                {
                    //
                    // entire sequence has been completed
                    //
                    VL_UnlockSurface(screenBuffer);
                    VL_UnlockSurface(screen);
                    VH_UpdateScreen(screenBuffer);

                    return false;
                }
            }

            VL_UnlockSurface(screen);

            SDL.SDL_UpdateTexture(texture, IntPtr.Zero, scrn_surface.pixels, (int)screenPitch);
            SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
            SDL.SDL_RenderPresent(renderer);

            frame++;
            Delay((int)(frame - GetTimeCount()));        // don't go too fast
        }

        return false;
    }
}
