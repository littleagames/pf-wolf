using SDL2;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static SDL2.SDL;

namespace Wolf3D;

internal partial class Program
{
    // id_vl globals
    internal static bool fullscreen = true;
    internal static short screenWidth = 640;
    internal static short screenHeight = 400;
    internal static int screenBits = -1; // use "best" color depth according to libSDL

    internal static IntPtr screen = IntPtr.Zero;
    internal static uint screenPitch;

    internal static IntPtr screenBuffer = IntPtr.Zero;
    internal static uint bufferPitch;

    internal static IntPtr window;
    internal static IntPtr renderer;
    internal static IntPtr texture;

    internal static int scaleFactor;
    internal static bool screenfaded;
    internal static uint bordercolor;

    internal static uint[] ylookup;

    internal static SDL.SDL_Color[] palette1 = new SDL.SDL_Color[256];
    internal static SDL.SDL_Color[] palette2 = new SDL.SDL_Color[256];
    internal static SDL.SDL_Color[] curpal = new SDL.SDL_Color[256];

    internal static SDL.SDL_Color[] gamepal = GamePal.wolfpal;

    internal static void VW_WaitVBL(uint a) => VL_WaitVBL(a);
    internal static void VL_WaitVBL(uint a) => SDL.SDL_Delay((a) * 8);

    internal static void VL_ClearScreen(byte c) => SDL.SDL_FillRect(screenBuffer, IntPtr.Zero, c);

    internal static void VL_SetVGAPlaneMode()
    {
        int i;
        UInt32 a, r, g, b;

        const string title = "Wolfenstein 3D";

        window = SDL.SDL_CreateWindow(
            title,
            SDL.SDL_WINDOWPOS_UNDEFINED,
            SDL.SDL_WINDOWPOS_UNDEFINED,
            screenWidth,
            screenHeight,
            (fullscreen ? SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN : 0) | SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL);

        SDL.SDL_PixelFormatEnumToMasks(SDL.SDL_PIXELFORMAT_ARGB8888, out screenBits, out r, out g, out b, out a);

        screen = SDL.SDL_CreateRGBSurface(0, screenWidth, screenHeight, screenBits, r, g, b, a);

        if (screen == IntPtr.Zero)
        {
            Console.WriteLine($"Unable to set {screenWidth}x{screenHeight}x{screenBits} video mode: {SDL.SDL_GetError()}");
            Environment.Exit(1);
        }

        renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
        SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
        SDL.SDL_SetHint(SDL.SDL_HINT_RENDER_SCALE_QUALITY, "0");

        SDL.SDL_ShowCursor(SDL.SDL_DISABLE);
       
        SDL.SDL_SetPaletteColors(GetSurfaceFormatPalette(screen), gamepal, 0, 256);
        Array.Copy(gamepal, curpal, 256);

        screenBuffer = SDL.SDL_CreateRGBSurface(0, screenWidth, screenHeight, 8, 0, 0, 0, 0);

        if (screenBuffer == IntPtr.Zero)
        {
            Console.WriteLine($"Unable to create screen buffer surface: {SDL.SDL_GetError()}");
            Environment.Exit(1);
        }

        SDL.SDL_SetPaletteColors(GetSurfaceFormatPalette(screenBuffer), gamepal, 0, 256);

        texture = SDL.SDL_CreateTexture(renderer,
            SDL.SDL_PIXELFORMAT_ARGB8888,
            (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
            screenWidth,
            screenHeight);

        screenPitch = (uint)GetSurface(screen).pitch;
        bufferPitch = (uint)GetSurface(screenBuffer).pitch;

        scaleFactor = screenWidth / 320;
        if (screenHeight / 200 < scaleFactor) scaleFactor = screenHeight / 200;

        ylookup = new uint[screenHeight];
        pixelangle = new short[screenWidth];
        wallheight = new short[screenWidth];

        for (i = 0; i < screenHeight; i++)
            ylookup[i] = (uint)(i * bufferPitch);
    }

    internal static void VL_MemToScreen(byte[] source, int width, int height, int x, int y)
        => VL_MemToScreenScaledCoord(source, width, height, scaleFactor * x, scaleFactor * y);

    internal static void VL_MemToScreenScaledCoord(byte[] source, int width, int height, int destx, int desty)
    {
        int i, j, sci, scj;
        int m, n;

        IntPtr destPtr = VL_LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero) return;

        unsafe
        {
            byte* dest = (byte*)destPtr;

            for (j = 0, scj = 0; j < height; j++, scj += scaleFactor)
            {
                for (i = 0, sci = 0; i < width; i++, sci += scaleFactor)
                {
                    byte col = source[(j * width) + i];
                    for (m = 0; m < scaleFactor; m++)
                    {
                        for (n = 0; n < scaleFactor; n++)
                        {
                            dest[ylookup[scj + m + desty] + sci + n + destx] = col;
                        }
                    }
                }
            }
        }

        VL_UnlockSurface(screenBuffer);
    }

    internal static void VL_Hlin(int x, int y, int width, int color)
    {
        Debug.Assert(x >= 0 && x + width <= screenWidth
            && y >= 0 && y < screenHeight,
            "VL_Hlin: Destination rectangle out of bounds!");
        IntPtr destPtr = VL_LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero) return;
        unsafe
        {
            byte* dest = (byte*)destPtr;
            dest += ylookup[y] + x;
            for (int i = 0; i < width; i++)
            {
                dest[i] = (byte)color;
            }
        }
        VL_UnlockSurface(screenBuffer);
    }

    internal static void VL_Vlin(int x, int y, int height, int color)
    {
        Debug.Assert(x >= 0 && x < screenWidth
            && y >= 0 && y + height <= screenHeight,
            "VL_Vlin: Destination rectangle out of bounds!");
        IntPtr destPtr = VL_LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero) return;
        unsafe
        {
            byte* dest = (byte*)destPtr;
            dest += ylookup[y] + x;
            while (height-- > 0)
            {
                dest[0] = (byte)color;
                dest += bufferPitch;

            }
        }

        VL_UnlockSurface(screenBuffer);
    }

    internal static IntPtr VL_LockSurface(IntPtr surface)
    {
        if (SDL.SDL_MUSTLOCK(surface))
        {
            if (SDL.SDL_LockSurface(surface) < 0)
                return IntPtr.Zero;
        }

        return GetSurface(surface).pixels;
    }

    internal static void VL_UnlockSurface(IntPtr surface)
    {
        if (SDL.SDL_MUSTLOCK(surface))
        {
            SDL.SDL_UnlockSurface(surface);
        }
    }

    internal static SDL.SDL_Surface GetSurface(IntPtr surface)
    {
        return Marshal.PtrToStructure<SDL.SDL_Surface>(surface);
    }

    internal static SDL.SDL_PixelFormat GetSurfaceFormat(IntPtr surface)
    {
        if (surface == IntPtr.Zero)
            return new SDL_PixelFormat();

        // Marshal the native SDL_Surface to read its 'format' pointer
        SDL.SDL_Surface surf = Marshal.PtrToStructure<SDL.SDL_Surface>(surface);
        IntPtr formatPtr = surf.format;
        if (formatPtr == IntPtr.Zero)
            return new SDL_PixelFormat();

        // Marshal the native SDL_PixelFormat to read its 'palette' pointer
        return Marshal.PtrToStructure<SDL.SDL_PixelFormat>(formatPtr);
    }

    internal static IntPtr GetSurfaceFormatPalette(IntPtr surface)
    {
        if (surface == IntPtr.Zero)
            return IntPtr.Zero;

        // Marshal the native SDL_Surface to read its 'format' pointer
        SDL.SDL_Surface surf = Marshal.PtrToStructure<SDL.SDL_Surface>(surface);
        IntPtr formatPtr = surf.format;
        if (formatPtr == IntPtr.Zero)
            return IntPtr.Zero;

        // Marshal the native SDL_PixelFormat to read its 'palette' pointer
        SDL.SDL_PixelFormat pixfmt = Marshal.PtrToStructure<SDL.SDL_PixelFormat>(formatPtr);
        return pixfmt.palette;
    }

    internal static void VL_GetPalette(SDL.SDL_Color[] palette)
    {
        Array.Copy(curpal, palette, 256);
    }

    internal static void VL_SetPalette(SDL.SDL_Color[] palette, bool forceupdate)
    {
        Array.Copy(palette, curpal, 256);

        if (screenBits == 0)
            SDL.SDL_SetPaletteColors(GetSurfaceFormatPalette(screen), palette, 0, 256);
        else
        {
            SDL.SDL_SetPaletteColors(GetSurfaceFormatPalette(screenBuffer), palette, 0, 256);
            if (forceupdate)
                VH_UpdateScreen(screenBuffer);
        }
    }

    internal static void VL_FillPalette(int red, int green, int blue)
    {
        int i;
        SDL_Color[] pal = new SDL_Color[256];

        for (i = 0; i < 256; i++)
        {
            pal[i].r = (byte)red;
            pal[i].g = (byte)green;
            pal[i].b = (byte)blue;
            pal[i].a = 255;//(byte)SDL_ALPHA_OPAQUE;
        }

        VL_SetPalette(pal, true);
    }

    internal static void VL_FadeIn(int start, int end, SDL.SDL_Color[] palette, int steps)
    {
        int i, j, delta;

        VL_WaitVBL(1);
        VL_GetPalette(palette1);
        Array.Copy(palette1, palette2, 256);

        //
        // fade through intermediate frames
        //
        for (i = 0; i < steps; i++)
        {
            for (j = start; j <= end; j++)
            {
                delta = palette[j].r - palette1[j].r;
                palette2[j].r = (byte)(palette1[j].r + delta * i / steps);
                delta = palette[j].g - palette1[j].g;
                palette2[j].g = (byte)(palette1[j].g + delta * i / steps);
                delta = palette[j].b - palette1[j].b;
                palette2[j].b = (byte)(palette1[j].b + delta * i / steps);
                palette2[j].a = 255;// SDL_ALPHA_OPAQUE;
            }

            VL_WaitVBL(1);
            VL_SetPalette(palette2, true);
        }

        //
        // final color
        //
        VL_SetPalette(palette, true);
        screenfaded = false;
    }

    internal static void VL_FadeOut(int start, int end, int red, int green, int blue, int steps)
    {
        int i, j, orig, delta;
        SDL.SDL_Color[] origPtr, newPtr;

        red = red * 255 / 63;
        green = green * 255 / 63;
        blue = blue * 255 / 63;

        VL_WaitVBL(1);
        VL_GetPalette(palette1);
        Array.Copy(palette1, palette2, 256);

        //
        // fade through intermediate frames
        //
        for (i = 0; i < steps; i++)
        {
            for (j = start; j <= end; j++)
            {
                int origR = palette1[j].r;
                int deltaR = red - origR;
                int newR = origR + deltaR * i / steps;

                int origG = palette1[j].g;
                int deltaG = green - origG;
                int newG = origG + deltaG * i / steps;

                int origB = palette1[j].b;
                int deltaB = blue - origB;
                int newB = origB + deltaB * i / steps;

                palette2[j].r = (byte)Math.Clamp(newR, 0, 255);
                palette2[j].g = (byte)Math.Clamp(newG, 0, 255);
                palette2[j].b = (byte)Math.Clamp(newB, 0, 255);
                palette2[j].a = 255;
            }

            VL_WaitVBL(1);
            VL_SetPalette(palette2, true);
        }

        //
        // final color
        //
        VL_FillPalette(red, green, blue);

        screenfaded = true;
    }

    internal static void VW_Shutdown() => VL_Shutdown();

    internal static void VL_Shutdown()
    {
        SDL.SDL_FreeSurface(screenBuffer);

        SDL.SDL_DestroyRenderer(renderer);
        SDL.SDL_DestroyWindow(window);
        SDL.SDL_DestroyTexture(texture);
    }

    internal static byte VL_GetPixel(int x, int y)
    {
        byte col;

        IntPtr destPtr = VL_LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero)
            return 0;

        unsafe
        {
            byte* dest = (byte*)destPtr;
            col = dest[ylookup[y] + x];
        }

        VL_UnlockSurface(screenBuffer);
        return col;
    }



    internal static void VL_DePlaneVGA(byte[] source, int sourceIndex, int width, int height)
    {
        int x, y, plane;
        ushort size, pwidth;

        size = (ushort)(width * height);

        if ((width & 3) != 0)
        {
            Quit("DePlaneVGA: width not divisible by 4!");
            return;
        }

        var temp = new byte[size];

        //
        // munge pic into the temp buffer
        //

        var srcline = 0;
        pwidth = (ushort)(width >> 2);

        for(plane = 0; plane < 4; plane++)
        {
            var destIndex = 0;
            for (y = 0; y < height; y++)
            {
                for (x = 0; x < pwidth; x++)
                    temp[destIndex + ((x << 2) + plane)] = source[srcline++];

                destIndex += width;
            }
        }

        //
        // copy the temp buffer back into the original source
        //
        Array.Copy(temp, source, size);
    }
}
