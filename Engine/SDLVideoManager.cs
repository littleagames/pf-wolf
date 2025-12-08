using PFWolf.Common;
using PFWolf.Common.Assets;
using System.Runtime.InteropServices;

namespace Engine
{
    //internal interface IVideoManager
    //{
    //    public int ScreenPitch { get; }
    //    public int BufferPitch { get; }
    //    public int ScreenHeight { get; }
    //    public int ScreenWidth { get; }
    //    bool Initialize();
    //    void Draw(Graphic graphic, Vector2 position, Dimension dimension);
    //    void DrawFps(double fps);
    //    void Update();
    //    void ShutDown();
    //}

    internal class SDLVideoManager //: IVideoManager
    {
        public IntPtr windowPtr;
        private static IntPtr rendererPtr;
        private static IntPtr screenPtr;
        private static IntPtr screenBufferPtr;
        private static IntPtr texturePtr;

        private bool _isInitialized = false;
        private bool _hasChanged = false;
        private double fps;
        private readonly AssetManager assetManager;
        private readonly GameConfigurationData config;

        public int ScreenPitch { get; private set; }
        public int BufferPitch { get; private set; }
        public int ScreenHeight { get; private set; }
        public int ScreenWidth { get; private set; }

        private uint[] ylookup = [];

        public SDLVideoManager(
            AssetManager assetManager,
            GameConfigurationData config)
        {
            ScreenHeight = config.ScreenResolution.Height;
            ScreenWidth = config.ScreenResolution.Width;
            this.assetManager = assetManager;
            this.config = config;
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
            SDL.SetPaletteColors(palette, this.config.DefaultPalette.ToSDLColors(), 0, 256);
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

        // TBD: Font positioning in the component vs methods
        public void Draw(Font font, Position position, TextAlignment alignment, string text, byte fontColor, byte backingColor)
        {
            var scaleFactorX = (ScreenWidth / 320.0f);
            var scaleFactorY = (ScreenHeight / 200.0f);

            var printX = CalcPrintX(font, text, alignment, position.Origin.X);
            var printY = (int)(position.Origin.Y * scaleFactorY);

            foreach (char textChar in text)
            {
                var asciiIndex = (int)textChar;
                var fontChar = font.FontCharacters[asciiIndex];

                if (fontChar.Data.Length > 0)
                {
                    var modifiedFontData = new byte[fontChar.Data.Length];
                    for (var i = 0; i < fontChar.Data.Length; i++)
                    {
                        var fontFlag = fontChar.Data[i] > 0;
                        modifiedFontData[i] = fontFlag ? fontColor : (byte)0xff;
                    }

                    // TODO: Map textalign to position alignment
                    var charPosition = new Position(new Vector2(printX, printY), AnchorPosition.TopLeft, ScaleType.Relative);
                    DrawData(modifiedFontData, charPosition, new Dimension(fontChar.Width, fontChar.Height), new Dimension((int)(fontChar.Width*scaleFactorX), (int)(fontChar.Height*scaleFactorY)));
                }

                if (textChar == '\n')
                {
                    printX = position.Origin.X;
                    printY = printY + fontChar.Height;
                    continue;
                }

                printX += (int)(fontChar.Width*scaleFactorX);
            }

            // TODO: Loop through each character in "text"
            // Or I can build the text in byte array size
            //      can send that once to MemToScreen

            //MemToScreen(colorizedFont)

            // get each character and print it to the byte[] pixels

        }

        private int CalcPrintX(Font font, string text, TextAlignment alignment, int x)
        {
            var scaleFactorX = (ScreenWidth / 320.0f);
            var scaleFactorY = (ScreenHeight / 200.0f);

            switch (alignment)
            {
                case TextAlignment.Left:
                    return x;
                case TextAlignment.Center:
                    {
                        // Calculate total width
                        int totalWidth = 0;
                        foreach (char textChar in text)
                        {
                            var asciiIndex = (int)textChar;
                            var fontChar = font.FontCharacters[asciiIndex];
                            totalWidth += (int)(fontChar.Width * scaleFactorY);
                        }
                        int startX = (ScreenWidth - totalWidth) / 2;
                        return startX;
                    }
                case TextAlignment.Right:
                    {
                        // Calculate total width
                        int totalWidth = 0;
                        foreach (char textChar in text)
                        {
                            var asciiIndex = (int)textChar;
                            var fontChar = font.FontCharacters[asciiIndex];
                            totalWidth += (int)(fontChar.Width * scaleFactorY);
                        }
                        int startX = (x - totalWidth);
                        return startX;
                    }
            }

            return x;
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

        private void DrawData(byte[] data, Position position, Dimension dimensions, Dimension size)
        {
            if (data.Length == 0)
                return;

            float scaleX = (float)dimensions.Width / size.Width;
            float scaleY = (float)dimensions.Height / size.Height;

            IntPtr dest = LockSurface(screenBufferPtr);
            if (dest == IntPtr.Zero) return;
            unsafe
            {
                byte* pixels = (byte*)dest;

                int rawX = position.Origin.X - position.Offset.X;
                int rawY = position.Origin.Y - position.Offset.Y;

                int startingX = Math.Max(rawX, 0);
                int startingY = Math.Max(rawY, 0);
                int endingX = Math.Min(rawX + size.Width, ScreenWidth);
                int endingY = Math.Min(rawY + size.Height, ScreenHeight);

                for (int y = startingY; y < endingY; y++)
                {
                    int srcY = (int)((y - rawY) * scaleY);
                    if (srcY < 0 || srcY >= dimensions.Height) continue;

                    for (int x = startingX; x < endingX; x++)
                    {
                        int srcX = (int)((x - rawX) * scaleX);
                        if (srcX < 0 || srcX >= dimensions.Width) continue;

                        byte col = data[srcY * dimensions.Width + srcX];
                        if (col == 0xff)
                            continue; // transparent
                        pixels[ylookup[y] + x] = col;
                    }
                }
            }

            UnlockSurface(screenBufferPtr);
        }

        /// <summary>
        /// Takes an existing transform and updates values to accomodate the video layout.
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        public Transform CalculateTransform(Transform transform)
        {
            return new TransformCalculator(ScreenWidth, ScreenHeight).CalculateTransform(transform);
        }

        internal void DrawComponent(RenderComponent component)
        {
            if (component.Children.Count > 0)
            {
                foreach (var child in component.Children)
                {
                    DrawComponent(child);
                }
            }

            //if (component.Transform.HasChanged)
            //{
            //    CalculateTransform(component.Transform);
            //    //component.Transform.Update(updatedTransform);
            //}

            if (component is PFWolf.Common.Components.Background background)
            {
                var data = new byte[ScreenWidth * ScreenHeight];
                Array.Fill(data, background.Color);
                DrawData(data, Position.Zero, new Dimension(ScreenWidth, ScreenHeight), new Dimension(ScreenWidth, ScreenHeight));
            }
            else if (component is PFWolf.Common.Components.Rectangle rectangle)
            {
                var transform = rectangle.Transform;

                int srcW = rectangle.OriginalSize.Width;
                int srcH = rectangle.OriginalSize.Height;

                // Guard against invalid source dimensions
                if (srcW > 0 && srcH > 0)
                {
                    // TODO: Handle PositionalAlignment
                    // TODO: Handle Rotation
                    // TODO: Avoid calculating size every frame if not changed
                   // transform.Update(CalculateBoundingBoxSize(rectangle.Size, transform.Size, transform.BoundingBoxType));
                }

                var data = new byte[transform.Size.Width * transform.Size.Height];
                Array.Fill(data, rectangle.Color);
                // TODO: Transform calculations here
                // Maybe a place they are done one time?
                DrawData(data, rectangle.Transform.Position, transform.Size, transform.Size);
            }
            else if (component is PFWolf.Common.Components.Graphic graphic)
            {
                var asset = assetManager.Load<PFWolf.Common.Assets.Graphic>(graphic.AssetName);
                var transform = graphic.Transform;

                int srcW = asset.Size.Width;
                int srcH = asset.Size.Height;

                // Guard against invalid source dimensions
                if (srcW > 0 && srcH > 0)
                {
                    // TODO: Handle PositionalAlignment
                    // TODO: Handle Rotation
                    // TODO: Avoid calculating size every frame if not changed
                   // transform.Update(CalculateBoundingBoxSize(asset.Dimensions, transform.Size, transform.BoundingBoxType));
                }

                if (!graphic.Hidden)
                {
                    DrawData(asset.Data, transform.Position, asset.Size, transform.Size);
                }
                // _loadedAssets.Add(graphic.AssetName, asset);
                // Store in graphic list (some can be reused, e.g. toggled buttons)
            }
            else if (component is PFWolf.Common.Components.Text text)
            {
                if (!string.IsNullOrEmpty(text.TempGraphicAssetName))
                {
                    var fontGraphic = assetManager.Load<PFWolf.Common.Assets.Graphic>(text.TempGraphicAssetName);
                    var transform = text.Transform;

                    if (!text.Hidden)
                    {
                        DrawData(fontGraphic.Data, transform.Position, fontGraphic.Size, transform.Size);
                    }
                }
            }
        }

        private Dimension CalculateBoundingBoxSize(Dimension src, Dimension currentSize, BoundingBoxType type)
        {
            int srcW = src.Width;
            int srcH = src.Height;

            // Guard against invalid source dimensions (caller already checks, but keep safe)
            if (srcW <= 0 || srcH <= 0)
                return currentSize;

            switch (type)
            {
                case BoundingBoxType.Scale:
                    {
                        float scaleX = ScreenWidth / (float)320;
                        float scaleY = ScreenHeight / (float)200;
                        int newW = Math.Max(1, (int)Math.Round(srcW * scaleX));
                        int newH = Math.Max(1, (int)Math.Round(srcH * scaleY));
                        return new Dimension(newW, newH);
                    }
                case BoundingBoxType.ScaleToScreen:
                    {
                        float scaleX = ScreenWidth / (float)srcW;
                        float scaleY = ScreenHeight / (float)srcH;
                        float scale = Math.Min(scaleX, scaleY);
                        int newW = Math.Max(1, (int)Math.Round(srcW * scale));
                        int newH = Math.Max(1, (int)Math.Round(srcH * scale));
                        return new Dimension(newW, newH);
                    }
                case BoundingBoxType.StretchToScreen:
                    return new Dimension(ScreenWidth, ScreenHeight);
                case BoundingBoxType.ScaleWidthToScreen:
                    {
                        float scale = ScreenWidth / (float)srcW;
                        int newW = ScreenWidth;
                        int newH = Math.Max(1, (int)Math.Round(srcH * scale));
                        return new Dimension(newW, newH);
                    }
                case BoundingBoxType.ScaleHeightToScreen:
                    {
                        float scale = ScreenHeight / (float)srcH;
                        int newH = ScreenHeight;
                        int newW = Math.Max(1, (int)Math.Round(srcW * scale));
                        return new Dimension(newW, newH);
                    }
                default:
                    // leave transform.Size unchanged for unknown types
                    return currentSize;
            }
        }
    }
}
