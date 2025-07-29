//using System.Drawing;
//using System.Runtime.InteropServices;

//namespace Engine;

//internal static class Program
//{
//    //public static bool param_debugmode = false;
//    //public static int param_difficulty = 1;
//    //public static bool param_nowait = false;
//    //public static int param_tedlevel = -1;
//    //public static int param_joystickindex = 0;
//    //public static int param_audiobuffer = 2048; // DEFAULT_AUDIO_BUFFER_SIZE
//    //public static int param_joystickhat = -1;
//    //public static int param_samplerate = 44100;
//    //public static int param_mission = 0;
//    //public static bool param_goodtimes = false;
//    //public static bool param_ignorenumchunks = false;

//    public static IntPtr _windowPtr, _screenPtr, _screenBufferPtr, _texturePtr, _rendererPtr;
//    private static readonly uint[] _yLookup = new uint[ScreenHeight];
//    private static int _screenPitch, _bufferPitch;
//    private static int _scaleFactor;

//    private const int ScreenWidth = 320;
//    private const int ScreenHeight = 200;

//    private static FpsCounter _fpsCounter;

//    [STAThread]
//    private static void Main(string[] args)
//    {
//        try
//        {
//            //CheckParameters(args);
//            // CheckForEpisodes();
//            InitGame();
//            // DemoLoop();
//            //Quit("Demo loop exited???");
//        }
//        catch (Exception e)
//        {
//            Console.WriteLine(e.Message);
//        }

//        var loop = true;
//        var startCounter = SDL.GetPerformanceCounter();
//        var frequency = SDL.GetPerformanceFrequency();
//        var fpsCounter = new FpsCounter();

//        while (loop)
//        {
//            while (SDL.PollEvent(out var e))
//            {
//                if (e.Type == (uint)SDL.EventType.Quit || e is { Type: (uint)SDL.EventType.KeyDown, Key.Key: SDL.Keycode.Escape })
//                {
//                    loop = false;
//                }
//            }
//        }
//    }

//    public static void InitGame()
//    {
//        // TODO: Move to VideoManager, AudioManager, InputManager, can move all of the other initflags to SDL.InitSubsystem
//        if (!SDL.Init(SDL.InitFlags.Video))
//        {
//            SDL.LogError(SDL.LogCategory.System, $"SDL could not initialize: {SDL.GetError()}");
//            SDL.Quit();
//            Environment.Exit((int)ExitCode.UnknownError);
//        }

//        SDL.CreateWindowAndRenderer("PF Wolf", ScreenWidth, ScreenHeight, SDL.WindowFlags.OpenGL, out _windowPtr, out _rendererPtr);
//        //_windowPtr = SDL.CreateWindow("PF-Wolf", ScreenWidth, ScreenHeight, SDL.WindowFlags.OpenGL);
//        if (_windowPtr == IntPtr.Zero)
//        {
//            SDL.LogError(SDL.LogCategory.Render, $"Error creating window and rendering: {SDL.GetError()}");
//            SDL.Quit();
//            Environment.Exit((int)ExitCode.UnknownError);
//        }

//        int bpp = -1;
//        uint rMask = 0, gMask = 0, bMask = 0, aMask = 0;
//        SDL.GetMasksForPixelFormat(SDL.PixelFormat.ARGB8888, ref bpp, ref rMask, ref gMask, ref bMask, ref aMask);

//        _screenPtr = SDL.CreateSurface(ScreenWidth, ScreenHeight, SDL.GetPixelFormatForMasks(bpp, rMask, gMask, bMask, aMask));
//        if (_screenPtr == IntPtr.Zero)
//        {
//            SDL.LogError(SDL.LogCategory.Render, $"Error creating screen: {SDL.GetError()}");
//            SDL.Quit();
//            Environment.Exit((int)ExitCode.UnknownError);
//        }

//        //_rendererPtr = SDL.CreateRenderer(_windowPtr, null);
//        //if (_rendererPtr == IntPtr.Zero)
//        //{
//        //    SDL.LogError(SDL.LogCategory.Render, $"Error creating window and rendering: {SDL.GetError()}");
//        //    SDL.Quit();
//        //    Environment.Exit((int)ExitCode.UnknownError);
//        //}

//        SDL.SetRenderVSync(_rendererPtr, 1);
//        SDL.SetRenderDrawBlendMode(_rendererPtr, SDL.BlendMode.Blend);
//        SDL.HideCursor();

//        _screenBufferPtr = SDL.CreateSurface(ScreenWidth, ScreenHeight, SDL.PixelFormat.ARGB8888);// GetPixelFormatForMasks(8, 0, 0, 0, 0));
//        if (_screenBufferPtr == IntPtr.Zero)
//        {
//            SDL.LogError(SDL.LogCategory.Render, $"Error creating screen buffer: {SDL.GetError()}");
//            SDL.Quit();
//            Environment.Exit((int)ExitCode.UnknownError);
//        }

//        //IntPtr screenBufferPalettePtr = SDL.CreateSurfacePalette(_screenBufferPtr);
//        //if (screenBufferPalettePtr == IntPtr.Zero)
//        //{
//        //    SDL.LogError(SDL.LogCategory.Render, $"Error creating color palette for buffer surface: {SDL.GetError()}");
//        //    SDL.Quit();
//        //    Environment.Exit((int)ExitCode.UnknownError);
//        //}

//        IntPtr palettePtr = SDL.CreatePalette(256);
//        if (palettePtr == IntPtr.Zero)
//        {
//            SDL.LogError(SDL.LogCategory.Render, $"Error creating color palette: {SDL.GetError()}");
//            SDL.Quit();
//            Environment.Exit((int)ExitCode.UnknownError);
//        }

//        SDL.SetPaletteColors(palettePtr, GamePalette.BasePalette, 0, 256);

//        // SDL.SetPaletteColors(screenBufferPalettePtr, GamePalette.BasePalette, 0, 256);
//        SDL.SetSurfacePalette(_screenBufferPtr, palettePtr);


//        _texturePtr = SDL.CreateTexture(_rendererPtr,
//            SDL.PixelFormat.ARGB8888,
//            SDL.TextureAccess.Streaming,
//            ScreenWidth,
//            ScreenHeight);

//        if (_texturePtr == IntPtr.Zero)
//        {
//            SDL.LogError(SDL.LogCategory.Render, $"Error creating texture: {SDL.GetError()}");
//            SDL.Quit();
//            Environment.Exit((int)ExitCode.UnknownError);
//        }

//        SDL.Surface screen = Marshal.PtrToStructure<SDL.Surface>(_screenPtr)!;
//        _screenPitch = screen.Pitch;

//        SDL.Surface screenBuffer = Marshal.PtrToStructure<SDL.Surface>(_screenBufferPtr)!;
//        _bufferPitch = screenBuffer.Pitch;

//        _scaleFactor = ScreenWidth / 320;
//        if (ScreenHeight / 200 < _scaleFactor) _scaleFactor = ScreenHeight / 200;

//        for (var i = 0; i < ScreenHeight; i++)
//            _yLookup[i] = (uint)(i * _bufferPitch);

//        _fpsCounter = new FpsCounter();
//        var loop = true;
//        while (loop)
//        {
//            while (SDL.PollEvent(out var e))
//            {
//                if (e.Type == (uint)SDL.EventType.Quit || e is { Type: (uint)SDL.EventType.KeyDown, Key.Key: SDL.Keycode.Escape })
//                {
//                    loop = false;
//                }
//            }

//            //DrawRectangle(0, 0, 320, 200, 0x40);
//            //UpdateScreen();
//            MemToScreen(Signon.SignOn, 0, 0, 320, 200); // this 320x200 is the gfx size
//            UpdateScreen();
//        }

//        SDL.DestroyTexture(_texturePtr);
//        SDL.DestroySurface(_screenPtr);
//        SDL.DestroySurface(_screenBufferPtr);
//        SDL.DestroyRenderer(_rendererPtr);
//        SDL.DestroyWindow(_windowPtr);

//    }

//    private static void UpdateScreen()
//    {

//        // Calculate elapsed time

//        var elapsed = 10000;
//        // Calculate color components based on sine wave functions
//        var r = (byte)(Math.Sin(elapsed) * 127 + 128);
//        var g = (byte)(Math.Sin(elapsed + Math.PI / 2) * 127 + 128);
//        var b = (byte)(Math.Sin(elapsed + Math.PI) * 127 + 128);

//        _fpsCounter.Update();

//        //SDL.SetRenderDrawColor(_rendererPtr, r, g, b, 255);
//        //SDL.RenderClear(_rendererPtr);
//        SDL.Surface screenBuffer = Marshal.PtrToStructure<SDL.Surface>(_screenBufferPtr);
//        // Blit the image surface to the window surface
//        SDL.BlitSurface(_screenBufferPtr, IntPtr.Zero, _screenPtr, IntPtr.Zero);
//        //SDL.BlitSurfaceScaled(surface, IntPtr.Zero, _screenPtr, IntPtr.Zero, SDL.ScaleMode.Nearest);

//        SDL.Surface screen = Marshal.PtrToStructure<SDL.Surface>(_screenPtr);
//        //SDL.UnlockTexture(_texturePtr);
//        SDL.UpdateTexture(_texturePtr, IntPtr.Zero, screen.Pixels, _screenPitch);
//        //SDL.RenderClear(_rendererPtr);
//        SDL.RenderTexture(_rendererPtr, _texturePtr, IntPtr.Zero, IntPtr.Zero);

//        // Debug FPS
//        //SDL.SetRenderDrawColor(_rendererPtr, 255,0,0, 255);
//        //SDL.RenderDebugText(_rendererPtr, 10, 10, $"FPS: {_fpsCounter.FPS:N0}");

//        SDL.RenderPresent(_rendererPtr);

//    }

//    public static void Quit(string? errorStr)
//    {
//        bool hasError = false;
//        if (!string.IsNullOrEmpty(errorStr))
//        {
//            hasError = true;
//            // Gets all errors from the application
//            Console.WriteLine(errorStr);
//        }
//        else
//            errorStr = string.Empty;

//        Environment.Exit((int)(hasError ? ExitCode.UnknownError : ExitCode.Success));
//    }
//    private static void MemToScreen(byte[] source, int x, int y, int width, int height)
//    {
//        MemToScreenScaledCoord(source, x * _scaleFactor, y * _scaleFactor, width, height);
//    }
//    private static void MemToScreenScaledCoord(byte[] source, int destX, int destY, int width, int height)
//    {
//        int i, j, sci, scj;
//        int m, n;

//        var pixelsPtr = LockSurface(_screenBufferPtr);
//        if (pixelsPtr == IntPtr.Zero)
//        {
//            return;
//        }

//        unsafe
//        {
//            byte* pixels = (byte*)pixelsPtr;


//            for (j = 0, scj = 0; j < height; j++, scj += _scaleFactor)
//            {
//                for (i = 0, sci = 0; i < width; i++, sci += _scaleFactor)
//                {
//                    //const SDL_Color* colors = surf->format->palette->colors;
//                    byte col = source[(j * width) + i];
//                    for (m = 0; m < _scaleFactor; m++)
//                    {
//                        for (n = 0; n < _scaleFactor; n++)
//                        {
//                            //*dest++ = (colors[*src].r<<16)|(colors[*src].g<<8)|(colors[*src].b);
//                            var pos = _yLookup[scj + m + destY] + sci + n + destX;
//                            pixels[pos] = col;
//                        }
//                    }
//                }
//            }
//        }

//        UnlockSurface(_screenBufferPtr);
//    }

//    private static void DrawRectangle(int x, int y, int w, int h, byte color)
//    {
//        var surfacePixels = LockSurface(_screenBufferPtr);

//        int width = ScreenWidth;
//        int height = ScreenHeight;

//        unsafe
//        {
//            byte* pixels = (byte*)surfacePixels;
//            for (var i = 0; i < (width * height); i++)
//            {
//                pixels[i] = (byte)color;
//                //if (i > height * 20)
//                //{
//                //    pixels[i] = 0;
//                //    pixels[i + 1] = 0xff;
//                //    pixels[i + 2] = 0;
//                //    pixels[i + 3] = 255;
//                //}
//                //else
//                //{
//                //    pixels[i] = (byte)255;
//                //    pixels[i + 1] = 0;
//                //    pixels[i + 2] = 0;
//                //    pixels[i + 3] = 255;
//                //}
//            }
//            // Set each pixel to a red color (ARGB format)
//            //for (int y1 = y; y1 < height * 2 && y1 < (y+h)*2; y1+=2)
//            //{
//            //    for (int x1 = x; x1 < width*2 && x1 < (x + w)*2; x1+=2)
//            //    {
//            //        var arrayIndex = (y1 * width + x1);
//            //        pixels[arrayIndex] = 255; // b
//            //        pixels[arrayIndex+1] = 127; //g
//            //        pixels[arrayIndex+2] = 0; // r
//            //        pixels[arrayIndex+ 3] = 184; // a
//            //    }
//            //}

//        }

//        UnlockSurface(_screenBufferPtr);
//    }


//    private static IntPtr LockSurface(IntPtr surfacePtr)
//    {
//        SDL.Surface surface = Marshal.PtrToStructure<SDL.Surface>(surfacePtr)!;
//        if (SDL.MustLock(surface))
//        {
//            if (!SDL.LockSurface(surfacePtr))
//                return IntPtr.Zero;
//        }

//        return surface.Pixels;
//    }

//    private static void UnlockSurface(IntPtr surfacePtr)
//    {
//        SDL.Surface surface = Marshal.PtrToStructure<SDL.Surface>(surfacePtr)!;
//        if (SDL.MustLock(surface))
//        {
//            SDL.UnlockSurface(surfacePtr);
//        }
//    }

//    enum ExitCode : int
//    {
//        Success = 0,
//        UnknownError = 1
//    }
//}