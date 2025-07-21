using System.Runtime.InteropServices;

namespace Engine;

internal static class Program
{
    //public static bool param_debugmode = false;
    //public static int param_difficulty = 1;
    //public static bool param_nowait = false;
    //public static int param_tedlevel = -1;
    //public static int param_joystickindex = 0;
    //public static int param_audiobuffer = 2048; // DEFAULT_AUDIO_BUFFER_SIZE
    //public static int param_joystickhat = -1;
    //public static int param_samplerate = 44100;
    //public static int param_mission = 0;
    //public static bool param_goodtimes = false;
    //public static bool param_ignorenumchunks = false;

    public static IntPtr _windowPtr, _screenPtr, _screenBufferPtr, _texturePtr, _rendererPtr;
    private static readonly uint[] _yLookup = new uint[ScreenWidth * ScreenHeight];
    private static int _bufferPitch;

    private const int ScreenWidth = 320;
    private const int ScreenHeight = 200;

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            //CheckParameters(args);
            // CheckForEpisodes();
            InitGame();
            // DemoLoop();
            //Quit("Demo loop exited???");
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }

    public static void InitGame()
    {
        // TODO: Move to VideoManager, AudioManager, InputManager, can move all of the other initflags to SDL.InitSubsystem
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Audio | SDL.InitFlags.Joystick | SDL.InitFlags.Gamepad))
        {
            SDL.LogError(SDL.LogCategory.System, $"SDL could not initialize: {SDL.GetError()}");
            SDL.Quit();
            Environment.Exit((int)ExitCode.UnknownError);
        }

        if (!SDL.CreateWindowAndRenderer("PF-Wolf", ScreenWidth, ScreenHeight, 0, out _windowPtr, out _rendererPtr))
        {
            SDL.LogError(SDL.LogCategory.Render, $"Error creating window and rendering: {SDL.GetError()}");
            SDL.Quit();
            Environment.Exit((int)ExitCode.UnknownError);
        }

        SDL.SetRenderVSync(_rendererPtr, 1);
        SDL.HideCursor();

        int screenBits = 0;
        uint r = 0,g = 0, b = 0, a = 0;
        SDL.GetMasksForPixelFormat(SDL.PixelFormat.ABGR8888, ref screenBits, ref r, ref g, ref b, ref a);

        _screenPtr = SDL.CreateSurface(ScreenWidth, ScreenHeight, SDL.PixelFormat.ABGR8888);
        if (_screenPtr == IntPtr.Zero)
        {
            SDL.LogError(SDL.LogCategory.Render, $"Error creating screen: {SDL.GetError()}");
            SDL.Quit();
            Environment.Exit((int)ExitCode.UnknownError);
        }

        IntPtr palettePtr = SDL.CreateSurfacePalette(_screenPtr);
        SDL.SetPaletteColors(palettePtr, GamePalette.BasePalette, 0, 256);


        _screenBufferPtr = SDL.CreateSurface(ScreenWidth, ScreenHeight, SDL.PixelFormat.ABGR8888);
        IntPtr screenBufferPalettePtr = SDL.CreateSurfacePalette(_screenPtr);
        SDL.SetPaletteColors(screenBufferPalettePtr, GamePalette.BasePalette, 0, 256);

        _texturePtr = SDL.CreateTexture(_rendererPtr, SDL.PixelFormat.ABGR8888, SDL.TextureAccess.Streaming, ScreenWidth, ScreenHeight);
        if (_texturePtr == IntPtr.Zero)
        {
            SDL.LogError(SDL.LogCategory.Render, $"Error creating texture: {SDL.GetError()}");
            SDL.Quit();
            Environment.Exit((int)ExitCode.UnknownError);
        }

        SDL.Surface screenBuffer = (SDL.Surface)Marshal.PtrToStructure(_screenBufferPtr, typeof(SDL.Surface))!;
        _bufferPitch = screenBuffer.Pitch;

        for (var i = 0; i < ScreenHeight; i++)
            _yLookup[i] = (uint)(i * _bufferPitch);

        MemToScreen(Signon.SignOn, 0, 0, ScreenWidth, ScreenHeight);

    }

    private static void UpdateScreen(IntPtr surface)
    {
        // Blit the image surface to the window surface
        SDL.BlitSurface(surface, IntPtr.Zero, _screenPtr, IntPtr.Zero);

        Present(_screenPtr);
    }
    private static void Present(IntPtr screen)
    {
        SDL.Surface sdlScreen = (SDL.Surface)Marshal.PtrToStructure(screen, typeof(SDL.Surface))!;
        SDL.UpdateTexture(_texturePtr, IntPtr.Zero, sdlScreen.Pixels, ScreenWidth * sizeof(uint));
        SDL.RenderClear(_rendererPtr);
        SDL.RenderTexture(_rendererPtr, _texturePtr, IntPtr.Zero, IntPtr.Zero);
        SDL.RenderPresent(_rendererPtr);
    }
    //public static void SignonScreen()
    //{
    //    // Pull this data from the pfwolf.pk3 file
    //    VL_SetVGAPlaneMode();
    //    // sets the window, etc

    //    VL_MemToScreen(signon, 320, 200, 0, 0);
    //    // blits array of data to screen
    //}

    public static void Quit(string? errorStr)
    {
        bool hasError = false;
        if (!string.IsNullOrEmpty(errorStr))
        {
            hasError = true;
            // Gets all errors from the application
            Console.WriteLine(errorStr);
        }
        else
            errorStr = string.Empty;

        Environment.Exit((int)(hasError ? ExitCode.UnknownError : ExitCode.Success));
    }

    private static void MemToScreen(byte[] source, int x, int y, int width, int height)
    {
        int i, j;

        var pixelsPtr = LockSurface(_screenBufferPtr);
        unsafe
        {
            byte* pixels = (byte*)pixelsPtr;

            for (j = 0; j < height; j++)
                for (i = 0; i < width; i++)
                {
                    Console.WriteLine($"{i},{j} pixel");
                    byte col = source[j * width + i];
                    if (col == 0xff) continue;

                    var xlength = i + x;
                    var ylength = j + y;
                    if (ylength > _yLookup.Length ||
                        (_yLookup[ylength] + xlength) > (ScreenWidth * ScreenHeight)) return;
                    var oldVal = pixels[_yLookup[ylength] + xlength];
                    pixels[_yLookup[ylength] + xlength] = col;
                }
        }

        UnlockSurface(_screenBufferPtr);
    }


    private static IntPtr LockSurface(IntPtr surfacePtr)
    {
        SDL.Surface surface = (SDL.Surface)Marshal.PtrToStructure(surfacePtr, typeof(SDL.Surface))!;
        if (SDL.MustLock(surface))
        {
            if (!SDL.LockSurface(surfacePtr))
                return IntPtr.Zero;
        }

        return surface.Pixels;
    }

    private static void UnlockSurface(IntPtr surfacePtr)
    {
        SDL.Surface surface = (SDL.Surface)Marshal.PtrToStructure(surfacePtr, typeof(SDL.Surface))!;
        if (!SDL.MustLock(surface))
        {
            SDL.UnlockSurface(surfacePtr);
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = 1
    }
}