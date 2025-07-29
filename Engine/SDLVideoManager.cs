using System.Runtime.InteropServices;

namespace Engine
{
    internal interface IVideoManager
    {
        public int ScreenPitch { get; }
        public int BufferPitch { get; }
        public int ScreenHeight { get; }
        public int ScreenWidth { get; }
        bool Initialize();
        void Draw(byte[] graphic, int x, int y, int width, int height, float scale);
        void Update();
        void ShutDown();
    }

    internal class SDLVideoManager : IVideoManager
    {
        private static IntPtr windowPtr;
        private static IntPtr rendererPtr;
        private static IntPtr screenPtr;
        private static IntPtr screenBufferPtr;
        private static IntPtr texturePtr;

        public int ScreenPitch { get; private set; }
        public int BufferPitch { get; private set; }
        public int ScreenHeight { get; private set; }
        public int ScreenWidth { get; private set; }

        private static uint[] ylookup;

        public SDLVideoManager(int screenWidth, int screenHeight)
        {
            ScreenHeight = screenHeight;
            ScreenWidth = screenWidth;
        }

        public bool Initialize()
        {
            if (!SDL.InitSubSystem(SDL.InitFlags.Video))
            {
                SDL.LogError(SDL.LogCategory.Video, "Unable to initialize video system");
                return false;
            }

            if (!SDL.CreateWindowAndRenderer("PF Wolf", ScreenWidth, ScreenHeight, SDL.WindowFlags.OpenGL, out windowPtr, out rendererPtr))
            {
                SDL.LogError(SDL.LogCategory.Video, "Unable to initialize window and/or renderer");
                return false;
            }

            SDL.SetRenderDrawBlendMode(rendererPtr, SDL.BlendMode.Blend);
            SDL.SetRenderVSync(rendererPtr, 1);

            int bpp = -1;
            uint r = 0, g = 0, b = 0, a = 0;


            SDL.GetMasksForPixelFormat(SDL.PixelFormat.ARGB8888, ref bpp, ref r, ref g, ref b, ref a);
            screenPtr = SDL.CreateSurface(ScreenWidth, ScreenHeight, SDL.GetPixelFormatForMasks(bpp, r, g, b, a));
            screenBufferPtr = SDL.CreateSurface(ScreenWidth, ScreenHeight, SDL.GetPixelFormatForMasks(8, 0, 0, 0, 0));

            IntPtr palette = SDL.CreateSurfacePalette(screenBufferPtr);
            SDL.SetPaletteColors(palette, GamePalette.BasePalette, 0, 256);

            texturePtr = SDL.CreateTexture(rendererPtr, SDL.PixelFormat.ARGB8888, SDL.TextureAccess.Streaming, ScreenWidth, ScreenHeight);

            SDL.Surface sdlScreen = Marshal.PtrToStructure<SDL.Surface>(screenPtr);
            ScreenPitch = sdlScreen.Pitch;

            SDL.Surface sdlScreenBuffer = Marshal.PtrToStructure<SDL.Surface>(screenBufferPtr);
            BufferPitch = sdlScreenBuffer.Pitch;

            ylookup = new uint[ScreenHeight];

            for (int i = 0; i < ScreenHeight; i++)
                ylookup[i] = (uint)(i * BufferPitch);

            return true;
        }

        public void Update()
        {
            UpdateScreen(screenBufferPtr);
        }

        private void UpdateScreen(IntPtr surface)
        {
            SDL.BlitSurface(surface, IntPtr.Zero, screenPtr, IntPtr.Zero);

            var sdlScreen = Marshal.PtrToStructure<SDL.Surface>(screenPtr);

            SDL.UpdateTexture(texturePtr, IntPtr.Zero, sdlScreen.Pixels, ScreenPitch);
            SDL.RenderTexture(rendererPtr, texturePtr, IntPtr.Zero, IntPtr.Zero);
            SDL.RenderPresent(rendererPtr);
        }

        public void ShutDown()
        {
            SDL.DestroySurface(screenPtr);
            SDL.DestroySurface(screenBufferPtr);
            SDL.DestroyTexture(texturePtr);
            SDL.DestroyRenderer(rendererPtr);
            SDL.DestroyWindow(windowPtr);
        }

        public void Draw(byte[] graphic, int x, int y, int width, int height, float scale)
        {
            int scaleFactor = (int)scale;
            int i, j, sci, scj;
            int m, n;

            IntPtr dest = LockSurface(screenBufferPtr);
            if (dest == IntPtr.Zero) return;
            unsafe
            {
                byte* pixels = (byte*)dest;

                for (j = 0, scj = 0; j < height; j++, scj += scaleFactor)
                {
                    for (i = 0, sci = 0; i < width; i++, sci += scaleFactor)
                    {
                        byte col = graphic[(j * width) + i];
                        for (m = 0; m < scaleFactor; m++)
                        {
                            for (n = 0; n < scaleFactor; n++)
                            {
                                pixels[ylookup[scj + m + y] + sci + n + x] = col;
                            }
                        }
                    }
                }
            }
            UnlockSurface(screenBufferPtr);
        }

        private IntPtr LockSurface(IntPtr surface)
        {
            var sdlSurface = Marshal.PtrToStructure<SDL.Surface>(surface);
            if (SDL.MustLock(sdlSurface))
            {
                if (!SDL.LockSurface(surface))
                    return IntPtr.Zero;
            }
            return sdlSurface.Pixels;
        }

        private void UnlockSurface(IntPtr surface)
        {
            var sdlSurface = Marshal.PtrToStructure<SDL.Surface>(surface);
            if (SDL.MustLock(sdlSurface))
            {
                SDL.UnlockSurface(surface);
            }
        }
    }
}
