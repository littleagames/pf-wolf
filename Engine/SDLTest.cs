// SDLTest.cpp : This file contains the 'main' function. Program execution begins and ends there.
//
using SDL3;
using System.Runtime.InteropServices;

namespace Engine;

public class Program
{

    private static IntPtr window;
    private static IntPtr renderer;
    private static IntPtr screen;
    private static IntPtr screenBuffer;
    private static IntPtr texture;
    private static int screenPitch, bufferPitch, scaleFactor;
    private static uint[] ylookup;

    static void UpdateScreen(IntPtr surface)
    {
        SDL.BlitSurface(surface, IntPtr.Zero, screen, IntPtr.Zero);

        var sdlScreen = Marshal.PtrToStructure<SDL.Surface>(screen);

        SDL.UpdateTexture(texture, IntPtr.Zero, sdlScreen.Pixels, screenPitch);
        SDL.RenderTexture(renderer, texture, IntPtr.Zero, IntPtr.Zero);
        SDL.RenderPresent(renderer);
    }

    static IntPtr LockSurface(IntPtr surface)
    {
        var sdlSurface = Marshal.PtrToStructure<SDL.Surface>(surface);
        if (SDL.MustLock(sdlSurface))
        {
            if (!SDL.LockSurface(surface))
                return IntPtr.Zero;
        }
        return sdlSurface.Pixels;
    }

    static void UnlockSurface(IntPtr surface)
    {
        var sdlSurface = Marshal.PtrToStructure<SDL.Surface>(surface);
        if (SDL.MustLock(sdlSurface))
        {
            SDL.UnlockSurface(surface);
        }
    }

    static void MemToScreenScaledCoord(byte[] source, int width, int height, int destx, int desty)
    {
        int i, j, sci, scj;
        int m, n;

        IntPtr dest = LockSurface(screenBuffer);
        if (dest == IntPtr.Zero) return;
        unsafe
        {
            byte* pixels = (byte*)dest;

            for (j = 0, scj = 0; j < height; j++, scj += scaleFactor)
            {
                for (i = 0, sci = 0; i < width; i++, sci += scaleFactor)
                {
                    byte col = source[(j * width) + i];
                    for (m = 0; m < scaleFactor; m++)
                    {
                        for (n = 0; n < scaleFactor; n++)
                        {
                            pixels[ylookup[scj + m + desty] + sci + n + destx] = col;
                        }
                    }
                }
            }
        }
        UnlockSurface(screenBuffer);
    }

    static void MemToScreen(byte[] source, int width, int height, int x, int y)
    {
        MemToScreenScaledCoord(source, width, height, scaleFactor * x, scaleFactor * y);
    }

    static int Main(string[] args)
    {
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            SDL.Quit();
            return 0;
        }


        if (!SDL.CreateWindowAndRenderer("PF Wolf", 320, 200, SDL.WindowFlags.OpenGL, out window, out renderer))
        {
            SDL.Quit();
            return 0;
        }

        SDL.SetRenderDrawBlendMode(renderer, SDL.BlendMode.Blend);
        SDL.SetRenderVSync(renderer, 1);

        int bpp = -1;
        uint r = 0, g = 0, b = 0, a = 0;


        SDL.GetMasksForPixelFormat(SDL.PixelFormat.ARGB8888, ref bpp, ref r, ref g, ref b, ref a);
        screen = SDL.CreateSurface(320, 200, SDL.GetPixelFormatForMasks(bpp, r, g, b, a));
        screenBuffer = SDL.CreateSurface(320, 200, SDL.GetPixelFormatForMasks(8, 0, 0, 0, 0));

        IntPtr palette = SDL.CreateSurfacePalette(screenBuffer);
        SDL.SetPaletteColors(palette, GamePalette.BasePalette, 0, 256);

        texture = SDL.CreateTexture(renderer, SDL.PixelFormat.ARGB8888, SDL.TextureAccess.Streaming, 320, 200);

        SDL.Surface sdlScreen = Marshal.PtrToStructure<SDL.Surface>(screen);
        screenPitch = sdlScreen.Pitch;

        SDL.Surface sdlScreenBuffer = Marshal.PtrToStructure<SDL.Surface>(screenBuffer);
        bufferPitch = sdlScreenBuffer.Pitch;

        scaleFactor = 1;
        ylookup = new uint[200];

        for (int i = 0; i < 200; i++)
            ylookup[i] = (uint)(i * bufferPitch);


        bool quit = false;
        SDL.Event e;

        while (!quit)
        {
            while (SDL.PollEvent(out e))
            {
                if (e.Type == (uint)SDL.EventType.Quit || e is { Type: (uint)SDL.EventType.KeyDown, Key.Key: SDL.Keycode.Escape })
                {
                    quit = true;
                }

            }

            // Render something here
            MemToScreen(Signon.SignOn, 320, 200, 0, 0);
            UpdateScreen(screenBuffer);
        }

        SDL.DestroySurface(screen);
        SDL.DestroySurface(screenBuffer);
        SDL.DestroyTexture(texture);
        SDL.DestroyRenderer(renderer);
        SDL.DestroyWindow(window);
        SDL.Quit();
        return 0;
    }
}
// Run program: Ctrl + F5 or Debug > Start Without Debugging menu
// Debug program: F5 or Debug > Start Debugging menu

// Tips for Getting Started: 
//   1. Use the Solution Explorer window to add/manage files
//   2. Use the Team Explorer window to connect to source control
//   3. Use the Output window to see build output and other messages
//   4. Use the Error List window to view errors
//   5. Go to Project > Add New Item to create new code files, or Project > Add Existing Item to add existing code files to the project
//   6. In the future, to open this project again, go to File > Open > Project and select the .sln file
