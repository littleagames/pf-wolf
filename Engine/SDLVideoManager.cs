using PFWolf.Common;
using PFWolf.Common.Assets;
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
        void Draw(Graphic graphic, Vector2 position, Dimension dimension);
        void DrawFps(double fps);
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

        private bool _isInitialized = false;

        public int ScreenPitch { get; private set; }
        public int BufferPitch { get; private set; }
        public int ScreenHeight { get; private set; }
        public int ScreenWidth { get; private set; }

        private uint[] ylookup = [];

        public SDLVideoManager(int screenWidth, int screenHeight, Palette palette)
        {
            ScreenHeight = screenHeight;
            this.palette = palette;
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
            SDL.HideCursor();

            int bpp = -1;
            uint r = 0, g = 0, b = 0, a = 0;


            SDL.GetMasksForPixelFormat(SDL.PixelFormat.ARGB8888, ref bpp, ref r, ref g, ref b, ref a);
            screenPtr = SDL.CreateSurface(ScreenWidth, ScreenHeight, SDL.GetPixelFormatForMasks(bpp, r, g, b, a));
            screenBufferPtr = SDL.CreateSurface(ScreenWidth, ScreenHeight, SDL.GetPixelFormatForMasks(8, 0, 0, 0, 0));

            IntPtr palette = SDL.CreateSurfacePalette(screenBufferPtr);
            SDL.SetPaletteColors(palette, this.palette.ToSDLColors(), 0, 256);
            //SDL.SetPaletteColors(palette, GamePalette.BasePalette, 0, 256);

            texturePtr = SDL.CreateTexture(rendererPtr, SDL.PixelFormat.ARGB8888, SDL.TextureAccess.Streaming, ScreenWidth, ScreenHeight);

            SDL.Surface sdlScreen = Marshal.PtrToStructure<SDL.Surface>(screenPtr);
            ScreenPitch = sdlScreen.Pitch;

            SDL.Surface sdlScreenBuffer = Marshal.PtrToStructure<SDL.Surface>(screenBufferPtr);
            BufferPitch = sdlScreenBuffer.Pitch;

            ylookup = new uint[ScreenHeight];

            for (int i = 0; i < ScreenHeight; i++)
                ylookup[i] = (uint)(i * BufferPitch);

            _isInitialized = true;
            return true;
        }
        private double fps;
        private readonly Palette palette;

        public void DrawFps(double fps)
        {
            this.fps = fps;
        }

        public void Update()
        {
            if (!_isInitialized)
                throw new Exception("Video Manager is not initialized");

            UpdateScreen(screenBufferPtr);
        }

        private void UpdateScreen(IntPtr surface)
        {
            SDL.BlitSurface(surface, IntPtr.Zero, screenPtr, IntPtr.Zero);

            var sdlScreen = Marshal.PtrToStructure<SDL.Surface>(screenPtr);

            SDL.UpdateTexture(texturePtr, IntPtr.Zero, sdlScreen.Pixels, ScreenPitch);
            SDL.RenderTexture(rendererPtr, texturePtr, IntPtr.Zero, IntPtr.Zero);

            SDL.SetRenderDrawColor(rendererPtr, 255, 255, 255, 255);
            SDL.RenderDebugText(rendererPtr, 10, 10, $"FPS: {fps:N0}");

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

        public void Draw(Graphic graphic, Vector2 position, Dimension size)
        {
            float scaleX = (float)graphic.Dimensions.Width / size.Width;
            float scaleY = (float)graphic.Dimensions.Height / size.Height;

            IntPtr dest = LockSurface(screenBufferPtr);
            if (dest == IntPtr.Zero) return;
            unsafe
            {
                byte* pixels = (byte*)dest;

                var startingX = Math.Max(position.X, 0);
                var startingY = Math.Max(position.Y, 0);
                var endingX = Math.Min(size.Width, ScreenWidth);
                var endingY = Math.Min(size.Height, ScreenHeight);

                for (int y = startingY; y < endingY; y++)
                {
                    int srcY = (int)(y * scaleY);
                    for (int x = startingX; x < endingX; x++)
                    {
                        int srcX = (int)(x * scaleX);
                        byte col = graphic.Data[srcY* graphic.Dimensions.Width + srcX];
                        pixels[ylookup[y] + x] = col;
                    }
                }
            }

            UnlockSurface(screenBufferPtr);
        }

        private static IntPtr LockSurface(IntPtr surface)
        {
            var sdlSurface = Marshal.PtrToStructure<SDL.Surface>(surface);
            if (SDL.MustLock(sdlSurface))
            {
                if (!SDL.LockSurface(surface))
                    return IntPtr.Zero;
            }
            return sdlSurface.Pixels;
        }

        private static void UnlockSurface(IntPtr surface)
        {
            var sdlSurface = Marshal.PtrToStructure<SDL.Surface>(surface);
            if (SDL.MustLock(sdlSurface))
            {
                SDL.UnlockSurface(surface);
            }
        }
    }
}
