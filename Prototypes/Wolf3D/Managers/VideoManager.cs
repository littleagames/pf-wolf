using SDL2;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Wolf3D.Assets;
using Wolf3D.Entities;
using Wolf3D.Extensions;
using static SDL2.SDL;

namespace Wolf3D.Managers;

internal class VideoManager
{
    internal bool fullscreen = false; // TODO: Set these from config
    internal short screenWidth = 640; // TODO: Set these from config
    internal short screenHeight = 400; // TODO: Set these from config
    internal int screenBits = -1; // use "best" color depth according to libSDL

    internal IntPtr screen = IntPtr.Zero;
    internal uint screenPitch;

    internal IntPtr screenBuffer = IntPtr.Zero;
    internal uint bufferPitch;

    internal IntPtr window;
    internal IntPtr renderer;
    internal IntPtr texture;

    internal int scaleFactor; // TODO: Separate X and Y scale factors in case of non-square pixels

    internal uint[] ylookup;

    public bool screenfaded { get; private set; }

    internal SDL.SDL_Color[] palette1 = new SDL.SDL_Color[256];
    internal SDL.SDL_Color[] palette2 = new SDL.SDL_Color[256];
    internal SDL.SDL_Color[] curpal = new SDL.SDL_Color[256];

    [Obsolete("Will be 32-bit in the future, and this will only be used to map legacy data to 32bit. This will move to a graphic/asset manager")]
    private SDL.SDL_Color[] gamepal { get; set; }
    /// <summary>
    /// Palette index for each color name seen so far: the theme's names, filled at Init,
    /// plus any #RRGGBB literals as they are first drawn. Drawing calls resolve colors
    /// every frame, so the palette search runs once per color instead.
    /// </summary>
    private readonly Dictionary<string, byte> _colorIndexCache = [];
    private readonly HashSet<string> _unknownColors = [];

    /// <summary>
    /// Resolves a color to a palette byte index. Named colors (e.g. "White") come from
    /// the game pack's theme and #RRGGBB values are matched to the closest palette entry.
    /// Anything else is parsed as a raw numeric palette index, which is how in-game
    /// layout text (^C&lt;hex&gt;&lt;hex&gt;) selects a color directly.
    /// </summary>
    private byte ResolveColorByte(string color)
    {
        if (_colorIndexCache.TryGetValue(color, out byte col))
            return col;

        if (color.StartsWith('#'))
        {
            col = ParseColor(color);
            _colorIndexCache[color] = col;
            return col;
        }

        if (byte.TryParse(color, out col))
            return col;

        if (_unknownColors.Add(color))
            Console.WriteLine($"Unknown color '{color}', drawing palette index 0 instead");
        return 0;
    }

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
    private readonly Lazy<AssetManager> _assetManager;
    static uint rndbits_y;
    static uint rndmask;

    public VideoManager(Lazy<AssetManager> assetManager)
    {
        _assetManager = assetManager;
        InputManager.MouseGrabbed += SetWindowGrab;
    }

    public void Init(ColorThemeAsset? theme)
    {
        if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
        {
            throw new PfWolfVideoException("Could not initialize SDL: {error}", SDL.SDL_GetError());
        }

        var paletteName = _assetManager.Value.GetGamePaletteName();
        var pal = _assetManager.Value.Find<Palette>(paletteName);
        if (pal == null)
            throw new PfWolfVideoException($"Could not find '{paletteName}' palette in asset manager.");

        gamepal = pal.ToSDLColors();
        _darkenTable = null;

        _colorIndexCache.Clear();
        foreach (var (name, color) in theme?.Colors ?? [])
            _colorIndexCache[name] = ParseColor(color);

        InitializeSDLVideo();


        int rndbits_x = log2_ceil((UInt32)screenWidth);
        rndbits_y = (uint)log2_ceil((UInt32)screenHeight);

        int rndbits = rndbits_x + (int)rndbits_y;
        if (rndbits < 17)
            rndbits = 17;       // no problem, just a bit slower
        else if (rndbits > 25)
            rndbits = 25;       // fizzle fade will not fill whole screen

        rndmask = rndmasks[rndbits - 17];
    }

    public void Shutdown()
    {
        if (texture != IntPtr.Zero) SDL.SDL_DestroyTexture(texture);
        if (screenBuffer != IntPtr.Zero) SDL.SDL_FreeSurface(screenBuffer);
        if (screen != IntPtr.Zero) SDL.SDL_FreeSurface(screen);
        if (renderer != IntPtr.Zero) SDL.SDL_DestroyRenderer(renderer);
        if (window != IntPtr.Zero) SDL.SDL_DestroyWindow(window);
    }

    public void MemToScreen(byte[] source, int width, int height, int x, int y)
        => MemToScreenScaledCoord(source, width, height, scaleFactor * x, scaleFactor * y);

    public void MemToScreenScaledCoord(byte[] source, int width, int height, int destx, int desty)
    {
        int i, j, sci, scj;
        int m, n;

        IntPtr destPtr = LockSurface(screenBuffer);
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

        UnlockSurface(screenBuffer);
    }

    public void MemToScreenScaledCoord2(byte[] source, int origwidth, int srcx, int srcy,
                                int destx, int desty, int width, int height)
    {
        int i, j, sci, scj;
        int m, n;

        IntPtr destPtr = LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero) return;

        unsafe
        {
            byte* dest = (byte*)destPtr;

            for (j = 0, scj = 0; j < height; j++, scj += scaleFactor)
            {
                for (i = 0, sci = 0; i < width; i++, sci += scaleFactor)
                {
                    byte col = source[((j + srcy) * origwidth) + (i + srcx)];
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

        UnlockSurface(screenBuffer);
    }

    public void HorizontalLine(int x1, int x2, int y, string color)
    {
        if (scaleFactor == 1)
            HorizontalLine(x1, y, x2 - x1 + 1, color);
        else
            Bar(x1, y, x2 - x1 + 1, 1, color);
    }

    public void HorizontalLine(Vector2 position, int width, string color)
    {
        byte col = ResolveColorByte(color);
        Debug.Assert(position.X >= 0 && position.X + width <= screenWidth
            && position.Y >= 0 && position.Y < screenHeight,
            "VL_Hlin: Destination rectangle out of bounds!");
        IntPtr destPtr = LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero) return;
        unsafe
        {
            byte* dest = (byte*)destPtr;
            dest += ylookup[(int)position.Y] + (int)position.X;
            for (int i = 0; i < width; i++)
            {
                dest[i] = col;
            }
        }
        UnlockSurface(screenBuffer);
    }

    public void VerticalLine(int y1, int y2, int x, string color)
    {
        if (scaleFactor == 1)
            VerticalLine(x, y1, y2 - y1 + 1, color);
        else
            Bar(x, y1, 1, y2 - y1 + 1, color);
    }

    public void VerticalLine(Vector2 position, int height, int color)
    {
        Debug.Assert(position.X >= 0 && position.X < screenWidth
            && position.Y >= 0 && position.Y + height <= screenHeight,
            "VL_Vlin: Destination rectangle out of bounds!");
        IntPtr destPtr = LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero) return;
        unsafe
        {
            byte* dest = (byte*)destPtr;
            dest += ylookup[(int)position.Y] + (int)position.X;
            while (height-- > 0)
            {
                dest[0] = (byte)color;
                dest += bufferPitch;

            }
        }

        UnlockSurface(screenBuffer);
    }

    public void Bar(int x, int y, int width, int height, string color)
        => BarScaledCoord(scaleFactor * x, scaleFactor * y, scaleFactor * width, scaleFactor * height, color);

    public void BarScaledCoord(int scx, int scy, int scwidth, int scheight, string color)
    {
        Debug.Assert(scx >= 0 && scx + scwidth <= screenWidth
            && scy >= 0 && scy + scheight <= screenHeight,
            "VL_BarScaledCoord: Destination rectangle out of bounds!");

        byte col = ResolveColorByte(color);
        IntPtr destPtr = LockSurface(screenBuffer);
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
                    dest[i] = col;
                }

                dest += bufferPitch;
            }
        }

        UnlockSurface(screenBuffer);
    }


    // Last table from GetDarkenTable, and the brightness it was built for
    private byte[]? _darkenTable;
    private float _darkenBrightness;

    /// <summary>
    /// A palette remap that dims every color to <paramref name="brightness"/> (0..1) of itself,
    /// each matched to the closest palette entry. Built on first use and kept until the palette
    /// or brightness changes.
    /// </summary>
    internal byte[] GetDarkenTable(float brightness)
    {
        if (_darkenTable != null && _darkenBrightness == brightness)
            return _darkenTable;

        var table = new byte[256];
        for (int i = 0; i < table.Length; i++)
        {
            var c = gamepal[i];
            table[i] = FindClosestPaletteIndex((byte)(c.r * brightness), (byte)(c.g * brightness), (byte)(c.b * brightness));
        }

        _darkenTable = table;
        _darkenBrightness = brightness;
        return table;
    }

    /// <summary>Replaces every pixel in a rectangle (screen pixels) with its entry in <paramref name="table"/>.</summary>
    public void ShadeRegionScaledCoord(int scx, int scy, int scwidth, int scheight, byte[] table)
    {
        Debug.Assert(scx >= 0 && scx + scwidth <= screenWidth
            && scy >= 0 && scy + scheight <= screenHeight,
            "ShadeRegionScaledCoord: Destination rectangle out of bounds!");

        IntPtr destPtr = LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero) return;

        unsafe
        {
            byte* dest = (byte*)destPtr + ylookup[scy] + scx;

            while (scheight-- > 0)
            {
                for (int i = 0; i < scwidth; i++)
                    dest[i] = table[dest[i]];

                dest += bufferPitch;
            }
        }

        UnlockSurface(screenBuffer);
    }

    /// <summary>Whether a color name is defined by the game pack's theme.</summary>
    internal bool IsThemeColor(string name) => !name.StartsWith('#') && _colorIndexCache.ContainsKey(name);

    /// <summary>
    /// Draws a line in screen pixels with a square pen <paramref name="thickness"/> pixels wide
    /// (its top-left corner follows the line), clipped so that no pixel leaves the clip rectangle.
    /// </summary>
    public void DrawLineScaledCoord(int x0, int y0, int x1, int y1, string color, int thickness,
        int clipX, int clipY, int clipWidth, int clipHeight)
    {
        // Clip the pen's top-left corner, so the whole pen stays inside the rectangle.
        double left = clipX, top = clipY;
        double right = clipX + clipWidth - thickness, bottom = clipY + clipHeight - thickness;
        if (right < left || bottom < top)
            return;

        double ax = x0, ay = y0, bx = x1, by = y1;
        if (!ClipLine(ref ax, ref ay, ref bx, ref by, left, top, right, bottom))
            return;

        int sx = (int)Math.Round(ax), sy = (int)Math.Round(ay);
        int ex = (int)Math.Round(bx), ey = (int)Math.Round(by);

        byte col = ResolveColorByte(color);
        IntPtr destPtr = LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero) return;

        unsafe
        {
            byte* dest = (byte*)destPtr;

            // Bresenham
            int dx = Math.Abs(ex - sx), stepx = sx < ex ? 1 : -1;
            int dy = -Math.Abs(ey - sy), stepy = sy < ey ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                for (int py = 0; py < thickness; py++)
                {
                    byte* row = dest + ylookup[sy + py] + sx;
                    for (int px = 0; px < thickness; px++)
                        row[px] = col;
                }

                if (sx == ex && sy == ey)
                    break;

                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; sx += stepx; }
                if (e2 <= dx) { err += dx; sy += stepy; }
            }
        }

        UnlockSurface(screenBuffer);
    }

    /// <summary>Cohen–Sutherland: trims a line to a rectangle, or returns false if none of it is inside.</summary>
    private static bool ClipLine(ref double x0, ref double y0, ref double x1, ref double y1,
        double left, double top, double right, double bottom)
    {
        const int Inside = 0, Left = 1, Right = 2, Top = 4, Bottom = 8;

        int Code(double x, double y) =>
            (x < left ? Left : x > right ? Right : Inside) | (y < top ? Top : y > bottom ? Bottom : Inside);

        int code0 = Code(x0, y0), code1 = Code(x1, y1);

        while (true)
        {
            if ((code0 | code1) == 0)
                return true;
            if ((code0 & code1) != 0)
                return false;

            int outside = code0 != 0 ? code0 : code1;
            double x, y;

            if ((outside & Bottom) != 0)
            {
                x = x0 + (x1 - x0) * (bottom - y0) / (y1 - y0);
                y = bottom;
            }
            else if ((outside & Top) != 0)
            {
                x = x0 + (x1 - x0) * (top - y0) / (y1 - y0);
                y = top;
            }
            else if ((outside & Right) != 0)
            {
                y = y0 + (y1 - y0) * (right - x0) / (x1 - x0);
                x = right;
            }
            else
            {
                y = y0 + (y1 - y0) * (left - x0) / (x1 - x0);
                x = left;
            }

            if (outside == code0)
            {
                x0 = x; y0 = y;
                code0 = Code(x0, y0);
            }
            else
            {
                x1 = x; y1 = y;
                code1 = Code(x1, y1);
            }
        }
    }

    internal void FadeIn() => FadeIn(30);
    internal void FadeIn(int steps) => FadeIn(0, 255, new GamePalette { Colors = gamepal }, steps);
    internal void FadeIn(int start, int end, GamePalette gamePalette, int steps)
    {
        int i, j, delta;
        var palette = gamePalette.Colors;

        GameEngineManager.WaitVBL(1);
        GetPalette(palette1);
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

            GameEngineManager.WaitVBL(1);
            SetPalette(palette2, true);
        }

        //
        // final color
        //
        SetPalette(palette, true);
        screenfaded = false;
    }

    internal void FadeOut() => FadeOut(0, 255, 0, 0, 0, 30);

    /// <param name="red">6-bit VGA value (0-63), as are green and blue</param>
    internal void FadeOut(int start, int end, int red, int green, int blue, int steps)
        => FadeOut(start, end, new Color
        {
            Red = (byte)(red * 255 / 63),
            Green = (byte)(green * 255 / 63),
            Blue = (byte)(blue * 255 / 63),
            Alpha = 255
        }, steps);

    internal void FadeOut(int start, int end, Color color, int steps)
    {
        int i, j;
        int red = color.Red, green = color.Green, blue = color.Blue;

        GameEngineManager.WaitVBL(1);
        GetPalette(palette1);
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

            GameEngineManager.WaitVBL(1);
            SetPalette(palette2, true);
        }

        //
        // final color
        //
        FillPalette(red, green, blue);

        screenfaded = true;
    }


    internal void DrawPropString(int px, int py, string text, string fontcolor, FontAsset font)
    {
        int width, step, height;
        byte[] source;

        int i;
        int sx, sy;

        IntPtr destPtr = LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero)
            return;

        height = font.Height;

        byte col = ResolveColorByte(fontcolor);

        unsafe
        {
            byte* dest = (byte*)destPtr;
            dest += scaleFactor * (ylookup[py] + px); // starting point on the screenbuffer

            foreach (char ch in text.ToCharArray())
            {
                width = step = font.Width[ch];
                int locIndex = font.Location[ch];

                while (width-- != 0)
                {
                    for (i = 0; i < height; i++)
                    {
                        if (font.RawData[locIndex + (i * step)] != 0)
                        {
                            for (sy = 0; sy < scaleFactor; sy++)
                                for (sx = 0; sx < scaleFactor; sx++)
                                    dest[ylookup[scaleFactor * i + sy] + sx] = col;
                        }
                    }

                    locIndex++;
                    px++;
                    dest += scaleFactor;
                }
            }

            UnlockSurface(screenBuffer);
        }
    }

    /// <summary>Writes the screen to a BMP file; false (see SDL_GetError) if it couldn't.</summary>
    internal bool SaveScreenShot(string filename) => SDL.SDL_SaveBMP(screenBuffer, filename) == 0;

    internal bool FizzleFade(int x1, int y1, uint width, uint height, uint frames, bool abortable)
        => FizzleFade(screenBuffer, x1, y1, width, height, frames, abortable);

    private bool FizzleFade(IntPtr source, int x1, int y1, uint width, uint height, uint frames, bool abortable)
    {
        uint x = 0, y = 0, p, frame, pixperframe;
        int rndval;

        rndval = 1;
        pixperframe = width * height / frames;

        //inputManager.StartAck();

        frame = GameEngineManager.GetTimeCount();
        IntPtr srcptr = LockSurface(source);
        if (srcptr == IntPtr.Zero) return false;

        while (true)
        {
            //inputManager.ProcessEvents();

            //if (abortable && inputManager.CheckAck())
            //{
            //    UnlockSurface(source);
            //    Update(source);
            //    return true;
            //}

            IntPtr destptr = LockSurface(screen);

            if (destptr == IntPtr.Zero)
                throw new PfWolfVideoException("Unable to lock dest surface: {0}", SDL.SDL_GetError());

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
                    UnlockSurface(screenBuffer);
                    UnlockSurface(screen);
                    Update(screenBuffer);

                    return false;
                }
            }

            UnlockSurface(screen);

            SDL.SDL_UpdateTexture(texture, IntPtr.Zero, scrn_surface.pixels, (int)screenPitch);
            SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
            SDL.SDL_RenderPresent(renderer);

            frame++;
            GameEngineManager.DelayTics((int)(frame - GameEngineManager.GetTimeCount()));        // don't go too fast
        }

        return false;
    }


    internal byte GetPixel(int x, int y)
    {
        byte col;

        IntPtr destPtr = LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero)
            return 0;

        unsafe
        {
            byte* dest = (byte*)destPtr;
            col = dest[ylookup[y] + x];
        }

        UnlockSurface(screenBuffer);
        return col;
    }

    public void ClearScreen(byte color)
    {
        // TODO: Will the "color" become a full 32-bit ARGB value in the future?
        SDL.SDL_FillRect(screenBuffer, IntPtr.Zero, color);
    }

    public void Update()
    {
        Update(screenBuffer);
    }

    private void Update(IntPtr surface)
    {
        SDL.SDL_BlitSurface(surface, IntPtr.Zero, screen, IntPtr.Zero);

        var screenPixels = GetSurface(screen).pixels;
        SDL.SDL_UpdateTexture(texture, IntPtr.Zero, screenPixels, (int)screenPitch);
        SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
        SDL.SDL_RenderPresent(renderer);
    }

    public IntPtr LockSurface() => LockSurface(screenBuffer);
    public void UnlockSurface() => UnlockSurface(screenBuffer);

    public void CenterMouse()
    {
        SDL.SDL_WarpMouseInWindow(window, screenWidth / 2, screenHeight / 2);
    }

    internal void SetWindowGrab(object? sender, bool grabInput)
    {

        if (SDL.SDL_ShowCursor(!grabInput ? 1 : 0) < 0)
            throw new PfWolfVideoException("Unable to {0} cursor: {1}", (grabInput ? "show" : "hide"), SDL.SDL_GetError());

        SDL.SDL_SetWindowGrab(window, grabInput ? SDL.SDL_bool.SDL_TRUE : SDL.SDL_bool.SDL_FALSE);

        if (SDL.SDL_SetRelativeMouseMode(grabInput ? SDL.SDL_bool.SDL_TRUE : SDL.SDL_bool.SDL_FALSE) > 0)
            throw new PfWolfVideoException("Unable to set relative mode for mouse: {0}", SDL.SDL_GetError());
    }

    private const int NUMREDSHIFTS = 6;
    private const int REDSTEPS = 8;

    private const int NUMWHITESHIFTS = 3;
    private const int WHITESTEPS = 20;
    private const int WHITETICS = 6;

    private SDL_Color[,] redshifts = new SDL_Color[NUMREDSHIFTS, 256];
    private SDL_Color[,] whiteshifts = new SDL_Color[NUMWHITESHIFTS, 256];

    int damagecount, bonuscount;
    bool palshifted;

    private static byte ClampToByte(int v) => (byte)(v < 0 ? 0 : (v > 255 ? 255 : v));

    internal void InitRedShifts()
    {
        // Fade through intermediate red shift frames
        for (int i = 1; i <= NUMREDSHIFTS; i++)
        {
            int ri = i - 1;
            for (int j = 0; j < 256; j++)
            {
                var basec = gamepal[j];

                int delta = 256 - basec.r;
                int newR = basec.r + delta * i / REDSTEPS;

                delta = -basec.g;
                int newG = basec.g + delta * i / REDSTEPS;

                delta = -basec.b;
                int newB = basec.b + delta * i / REDSTEPS;

                redshifts[ri, j] = new SDL_Color
                {
                    r = ClampToByte(newR),
                    g = ClampToByte(newG),
                    b = ClampToByte(newB),
                    a = 255 //SDL_ALPHA_OPAQUE
                };
            }
        }

        // Prepare white shift palettes
        for (int i = 1; i <= NUMWHITESHIFTS; i++)
        {
            int wi = i - 1;
            for (int j = 0; j < 256; j++)
            {
                var basec = gamepal[j];

                int delta = 256 - basec.r;
                int newR = basec.r + delta * i / WHITESTEPS;

                delta = 248 - basec.g;
                int newG = basec.g + delta * i / WHITESTEPS;

                delta = 0 - basec.b;
                int newB = basec.b + delta * i / WHITESTEPS;

                whiteshifts[wi, j] = new SDL_Color
                {
                    r = ClampToByte(newR),
                    g = ClampToByte(newG),
                    b = ClampToByte(newB),
                    a = 255 //SDL_ALPHA_OPAQUE
                };
            }
        }
    }

    internal void UpdatePaletteShifts(uint tics)
    {
        int red, white;

        if (bonuscount != 0)
        {
            white = bonuscount / WHITETICS + 1;
            if (white > NUMWHITESHIFTS)
                white = NUMWHITESHIFTS;
            bonuscount -= (int)tics;
            if (bonuscount < 0)
                bonuscount = 0;
        }
        else
            white = 0;


        if (damagecount != 0)
        {
            red = damagecount / 10 + 1;
            if (red > NUMREDSHIFTS)
                red = NUMREDSHIFTS;

            damagecount -= (int)tics;
            if (damagecount < 0)
                damagecount = 0;
        }
        else
            red = 0;

        if (red != 0)
        {
            SDL_Color[] flat = new SDL_Color[256];
            for (int i = 0; i < 256; i++)
                flat[i] = redshifts[red - 1, i];
            SetPalette(flat, false);
            palshifted = true;
        }
        else if (white != 0)
        {
            SDL_Color[] flat = new SDL_Color[256];
            for (int i = 0; i < 256; i++)
                flat[i] = whiteshifts[white - 1, i];
            SetPalette(flat, false);
            palshifted = true;
        }
        else if (palshifted)
        {
            SetPalette(gamepal, false);        // back to normal
            palshifted = false;
        }
    }

    internal void FinishPaletteShifts()
    {
        if (palshifted)
        {
            palshifted = false;
            SetPalette(gamepal, true);
        }
    }

    internal void ClearPaletteShifts()
    {
        bonuscount = damagecount = 0;
        palshifted = false;
    }

    internal void StartBonusFlash()
    {
        bonuscount = NUMWHITESHIFTS * WHITETICS;    // white shift palette
    }

    internal void StartDamageFlash(int damage)
    {
        damagecount += damage;
    }

    private IntPtr LockSurface(IntPtr surface)
    {
        if (SDL.SDL_MUSTLOCK(surface))
        {
            if (SDL.SDL_LockSurface(surface) < 0)
                return IntPtr.Zero;
        }

        return GetSurface(surface).pixels;
    }

    private void UnlockSurface(IntPtr surface)
    {
        if (SDL.SDL_MUSTLOCK(surface))
        {
            SDL.SDL_UnlockSurface(surface);
        }
    }

    private void InitializeSDLVideo()
    {
        int i;
        UInt32 a, r, g, b;

        const string title = "Wolfenstein 3D"; // TODO: pull from PK3 in future

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
        //pixelangle = new short[screenWidth];
        //wallheight = new short[screenWidth];

        for (i = 0; i < screenHeight; i++)
            ylookup[i] = (uint)(i * bufferPitch);
    }

    private static SDL.SDL_Surface GetSurface(IntPtr surface)
    {
        return Marshal.PtrToStructure<SDL.SDL_Surface>(surface);
    }

    private static IntPtr GetSurfaceFormatPalette(IntPtr surface)
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

    private static SDL.SDL_PixelFormat GetSurfaceFormat(IntPtr surface)
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


    private void GetPalette(SDL.SDL_Color[] palette)
    {
        Array.Copy(curpal, palette, 256);
    }

    private void SetPalette(SDL.SDL_Color[] palette, bool forceupdate)
    {
        Array.Copy(palette, curpal, 256);

        if (screenBits == 0)
            SDL.SDL_SetPaletteColors(GetSurfaceFormatPalette(screen), palette, 0, 256);
        else
        {
            SDL.SDL_SetPaletteColors(GetSurfaceFormatPalette(screenBuffer), palette, 0, 256);
            if (forceupdate)
                Update(screenBuffer);
        }
    }

    internal static void ConvertPalette(byte[] srcpal, ref SDL.SDL_Color[] destpal, int numColors)
    {
        int i, s = 0;

        for (i = 0; i < numColors; i++)
        {
            destpal[i].r = (byte)(srcpal[s++] * 255 / 63);
            destpal[i].g = (byte)(srcpal[s++] * 255 / 63);
            destpal[i].b = (byte)(srcpal[s++] * 255 / 63);
            destpal[i].a = 255;// SDL.SDL_ALPHA_OPAQUE;
        }
    }
    private void FillPalette(int red, int green, int blue)
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

        SetPalette(pal, true);
    }

    private static int log2_ceil(UInt32 x)
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

    internal byte ParseColor(string colorValue)
    {
        if (string.IsNullOrEmpty(colorValue) || colorValue[0] != '#' || colorValue.Length != 7)
            throw new FormatException($"Expected a color in #RRGGBB format, got '{colorValue}'.");

        byte r = byte.Parse(colorValue.AsSpan(1, 2), NumberStyles.HexNumber);
        byte g = byte.Parse(colorValue.AsSpan(3, 2), NumberStyles.HexNumber);
        byte b = byte.Parse(colorValue.AsSpan(5, 2), NumberStyles.HexNumber);

        return FindClosestPaletteIndex(r, g, b);
    }

    internal byte ParseColor(Color color)
        => FindClosestPaletteIndex(color.Red, color.Green, color.Blue);

    private byte FindClosestPaletteIndex(byte r, byte g, byte b)
    {
        byte closestIndex = 0;
        int closestDistance = int.MaxValue;

        for (int i = 0; i < gamepal.Length; i++)
        {
            int dr = r - gamepal[i].r;
            int dg = g - gamepal[i].g;
            int db = b - gamepal[i].b;
            int distance = dr * dr + dg * dg + db * db;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = (byte)i;

                if (distance == 0)
                    break;
            }
        }

        return closestIndex;
    }
}
