using SDL2;
using System.Globalization;
using System.Runtime.InteropServices;
using Wolf3D.Assets;
using Wolf3D.Configuration;
using Wolf3D.Entities;
using Wolf3D.Extensions;
using static SDL2.SDL;

namespace Wolf3D.Managers;

/// <summary>Which edge of the screen the 320x200 layout lines up with (it's always centered across).</summary>
internal enum UiAnchor
{
    Center,
    Top,
    Bottom,
}

internal class VideoManager
{
    /// <summary>The video mode in use; change it with ApplyVideoSettings.</summary>
    internal VideoSettings Settings { get; private set; } = new();

    /// <summary>
    /// Raised after ApplyVideoSettings changes the mode, with the settings it replaced. The screen
    /// buffer may be new and blank by then, so whatever was on screen needs drawing again.
    /// </summary>
    internal event EventHandler<VideoSettings>? VideoModeChanged;

    internal bool fullscreen => Settings.Fullscreen;
    // The screen buffer's size: Settings' render size
    internal short screenWidth;
    internal short screenHeight;
    internal int screenBits = -1; // use "best" color depth according to libSDL

    internal IntPtr screen = IntPtr.Zero;
    internal uint screenPitch;

    internal IntPtr screenBuffer = IntPtr.Zero;
    internal uint bufferPitch;

    internal IntPtr window;
    internal IntPtr renderer;
    internal IntPtr texture;

    /// <summary>
    /// The UI scale: screen pixels to each of the 320x200 pixels that menus, text, pictures and
    /// the status bar are laid out in. The 3D view fills the screen buffer whatever this is.
    /// </summary>
    internal int scaleFactor;

    /// <summary>
    /// Where the 320x200 layout's top left corner is on the screen (screen pixels), for the
    /// origin in use (<see cref="UseUiOrigin"/>). Both are 0 when the screen is exactly
    /// 320x200 times the UI scale; on a bigger or other-shaped screen it sits in the middle.
    /// </summary>
    internal int uiX, uiY;

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

    public void Init(ColorThemeAsset? theme, VideoSettings? settings = null)
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
        _lightRows.Clear();
        _lightMatches.Clear();
        _paletteLab = null;

        _colorIndexCache.Clear();
        foreach (var (name, color) in theme?.Colors ?? [])
            _colorIndexCache[name] = ParseColor(color);

        InitializeSDLVideo(settings ?? new VideoSettings());
    }

    public void Shutdown()
    {
        DestroyRenderer();
        DestroySurfaces();
        if (window != IntPtr.Zero) SDL.SDL_DestroyWindow(window);
        window = IntPtr.Zero;
    }

    /// <summary>
    /// Switches to another video mode while the game runs, rebuilding only what the change
    /// touches: a new render scale means a new, blank screen buffer, and VSync a new renderer.
    /// If SDL can't make the switch, the previous mode is put back and this returns false.
    /// Either way the caller should draw the screen again.
    /// </summary>
    internal bool ApplyVideoSettings(VideoSettings next)
    {
        var previous = Settings;
        next = ResolveRenderSize(next);
        if (next == previous)
            return true;

        if (TrySetVideoMode(previous, next, rebuildAll: false))
        {
            Settings = next;
            VideoModeChanged?.Invoke(this, previous);
            return true;
        }

        Console.WriteLine($"Couldn't switch to {next.RenderWidth}x{next.RenderHeight} " +
            $"({(next.Fullscreen ? "fullscreen" : $"{next.WindowWidth}x{next.WindowHeight} window")}): {SDL.SDL_GetError()}");

        // Part of the new mode may be in place, so rebuild all of the old one
        if (!TrySetVideoMode(next, previous, rebuildAll: true))
            throw new PfWolfVideoException("Unable to restore the previous video mode: {0}", SDL.SDL_GetError());

        return false;
    }

    /// <summary>
    /// For settings whose render size matches the window (<see cref="VideoSettings.MatchWindow"/>),
    /// that size worked out: the window's, or the display's when fullscreen, made shorter by
    /// a sixth when aspect correcting so the 6:5 stretch fills it. Other settings come back as they are.
    /// </summary>
    internal VideoSettings ResolveRenderSize(VideoSettings settings)
    {
        if (!settings.MatchWindow)
            return settings;

        var (width, height) = settings.Fullscreen && window != IntPtr.Zero
            ? GetDisplaySize()
            : (settings.WindowWidth, settings.WindowHeight);
        if (settings.AspectCorrect)
            height = height * 5 / 6;

        width = Math.Clamp(width, VideoSettings.BaseWidth, VideoSettings.MaxRenderWidth);
        height = Math.Clamp(height, VideoSettings.BaseHeight, VideoSettings.MaxRenderHeight);
        return settings with { RenderSize = (width, height) };
    }

    // Room left for the window's title bar and borders when sizing it to the desktop
    private const int WindowFrameAllowance = 40;

    /// <summary>The size of the display the window is on.</summary>
    internal (int Width, int Height) GetDisplaySize()
    {
        int display = Math.Max(0, SDL.SDL_GetWindowDisplayIndex(window));
        return SDL.SDL_GetDisplayBounds(display, out var bounds) == 0
            ? (bounds.w, bounds.h)
            : (screenWidth, screenHeight);
    }

    /// <summary>The largest window that fits on that display, clear of taskbars and with its frame showing.</summary>
    internal (int Width, int Height) GetLargestWindowSize()
    {
        int display = Math.Max(0, SDL.SDL_GetWindowDisplayIndex(window));
        return SDL.SDL_GetDisplayUsableBounds(display, out var bounds) == 0
            ? (bounds.w, bounds.h - WindowFrameAllowance)
            : (Settings.WindowWidth, Settings.WindowHeight);
    }

    private bool TrySetVideoMode(VideoSettings from, VideoSettings to, bool rebuildAll)
    {
        bool newWindowMode = rebuildAll || to.Fullscreen != from.Fullscreen
            || (!to.Fullscreen && (to.WindowWidth != from.WindowWidth || to.WindowHeight != from.WindowHeight));
        bool newRenderer = rebuildAll || to.VSync != from.VSync;
        // A new UI scale keeps the buffer's size, but everything on it needs drawing again too
        bool newSurfaces = rebuildAll || to.RenderWidth != from.RenderWidth || to.RenderHeight != from.RenderHeight
            || to.EffectiveUiScale != from.EffectiveUiScale;
        bool newTexture = newRenderer || newSurfaces || to.Filter != from.Filter;

        if (newWindowMode && !SetWindowMode(to))
            return false;

        if (newTexture)
            DestroyTexture();

        if (newRenderer)
        {
            DestroyRenderer();
            if (!CreateRenderer(to))
                return false;
        }
        else if (!SetLogicalSize(to))
            return false;

        if (newSurfaces)
        {
            DestroySurfaces();
            if (!CreateSurfaces(to))
                return false;
        }

        return !newTexture || CreateTexture(to);
    }

    private bool SetWindowMode(VideoSettings settings)
    {
        if (settings.Fullscreen)
            return SDL.SDL_SetWindowFullscreen(window, (uint)SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP) == 0;

        if (SDL.SDL_SetWindowFullscreen(window, 0) != 0)
            return false;

        SDL.SDL_SetWindowSize(window, settings.WindowWidth, settings.WindowHeight);
        int centered = SDL.SDL_WINDOWPOS_CENTERED_DISPLAY(SDL.SDL_GetWindowDisplayIndex(window));
        SDL.SDL_SetWindowPosition(window, centered, centered);
        return true;
    }

    private bool CreateRenderer(VideoSettings settings)
    {
        var flags = SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED;
        if (settings.VSync)
            flags |= SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC;

        renderer = SDL.SDL_CreateRenderer(window, -1, flags);
        if (renderer == IntPtr.Zero)
            return false;

        SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
        return SetLogicalSize(settings);
    }

    // The picture keeps its shape in any window, with black bars filling the rest
    private bool SetLogicalSize(VideoSettings settings)
        => SDL.SDL_RenderSetLogicalSize(renderer, settings.DisplayWidth, settings.DisplayHeight) == 0;

    /// <summary>The screen buffer and the 32-bit surface it's converted to, at the render size.</summary>
    private bool CreateSurfaces(VideoSettings settings)
    {
        int width = settings.RenderWidth, height = settings.RenderHeight;

        SDL.SDL_PixelFormatEnumToMasks(SDL.SDL_PIXELFORMAT_ARGB8888, out screenBits, out uint r, out uint g, out uint b, out uint a);

        screen = SDL.SDL_CreateRGBSurface(0, width, height, screenBits, r, g, b, a);
        if (screen == IntPtr.Zero)
            return false;
        SDL.SDL_SetPaletteColors(GetSurfaceFormatPalette(screen), curpal, 0, 256);

        screenBuffer = SDL.SDL_CreateRGBSurface(0, width, height, 8, 0, 0, 0, 0);
        if (screenBuffer == IntPtr.Zero)
            return false;
        SDL.SDL_SetPaletteColors(GetSurfaceFormatPalette(screenBuffer), curpal, 0, 256);

        screenWidth = (short)width;
        screenHeight = (short)height;
        screenPitch = (uint)GetSurface(screen).pitch;
        bufferPitch = (uint)GetSurface(screenBuffer).pitch;
        scaleFactor = settings.EffectiveUiScale;
        (uiX, uiY) = UiOriginFor(UiAnchor.Center);

        ylookup = new uint[screenHeight];
        for (int i = 0; i < screenHeight; i++)
            ylookup[i] = (uint)(i * bufferPitch);

        int rndbits_x = log2_ceil((UInt32)screenWidth);
        rndbits_y = (uint)log2_ceil((UInt32)screenHeight);

        int rndbits = rndbits_x + (int)rndbits_y;
        if (rndbits < 17)
            rndbits = 17;       // no problem, just a bit slower
        else if (rndbits > 25)
            rndbits = 25;       // fizzle fade will not fill whole screen

        rndmask = rndmasks[rndbits - 17];
        return true;
    }

    private bool CreateTexture(VideoSettings settings)
    {
        // Read when a texture is created
        SDL.SDL_SetHint(SDL.SDL_HINT_RENDER_SCALE_QUALITY, settings.Filter == ScaleFilter.Linear ? "1" : "0");

        texture = SDL.SDL_CreateTexture(renderer,
            SDL.SDL_PIXELFORMAT_ARGB8888,
            (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
            screenWidth,
            screenHeight);
        return texture != IntPtr.Zero;
    }

    private void DestroyTexture()
    {
        if (texture != IntPtr.Zero) SDL.SDL_DestroyTexture(texture);
        texture = IntPtr.Zero;
    }

    // Its texture goes with it
    private void DestroyRenderer()
    {
        DestroyTexture();
        if (renderer != IntPtr.Zero) SDL.SDL_DestroyRenderer(renderer);
        renderer = IntPtr.Zero;
    }

    private void DestroySurfaces()
    {
        if (screenBuffer != IntPtr.Zero) SDL.SDL_FreeSurface(screenBuffer);
        if (screen != IntPtr.Zero) SDL.SDL_FreeSurface(screen);
        screenBuffer = screen = IntPtr.Zero;
    }

    /// <summary>Whether the screen has room beyond the 320x200 layout, which nothing laid out in it covers.</summary>
    internal bool HasMargins => screenWidth != VideoSettings.BaseWidth * scaleFactor
        || screenHeight != VideoSettings.BaseHeight * scaleFactor;

    /// <summary>The 320x200 layout's screen position for an anchor: always centered across, and up or down as anchored.</summary>
    private (int X, int Y) UiOriginFor(UiAnchor anchor)
    {
        int x = (screenWidth - VideoSettings.BaseWidth * scaleFactor) / 2;
        int room = screenHeight - VideoSettings.BaseHeight * scaleFactor;
        return (x, anchor switch
        {
            UiAnchor.Top => 0,
            UiAnchor.Bottom => room,
            _ => room / 2,
        });
    }

    /// <summary>
    /// Moves the 320x200 layout to <paramref name="anchor"/> until the result is disposed, so
    /// what's drawn in it lines up with that edge of the screen: the status bar with the
    /// bottom, say. <c>using var _ = _videoManager.UseUiOrigin(UiAnchor.Bottom);</c>
    /// </summary>
    internal UiOriginScope UseUiOrigin(UiAnchor anchor)
    {
        var scope = new UiOriginScope(this, uiX, uiY);
        (uiX, uiY) = UiOriginFor(anchor);
        return scope;
    }

    internal readonly struct UiOriginScope(VideoManager video, int x, int y) : IDisposable
    {
        public void Dispose() => (video.uiX, video.uiY) = (x, y);
    }

    /// <summary>A screen x (pixels) as an x in the 320x200 layout, rounded down.</summary>
    internal int ToLayoutX(int screenX) => FloorDiv(screenX - uiX, scaleFactor);

    /// <summary>A screen y (pixels) as a y in the 320x200 layout, rounded down.</summary>
    internal int ToLayoutY(int screenY) => FloorDiv(screenY - uiY, scaleFactor);

    private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

    /// <summary>Fills the whole screen with a color: the 320x200 layout and any room round it.</summary>
    public void FillScreen(string color) => BarScaledCoord(0, 0, screenWidth, screenHeight, color);

    public void MemToScreen(byte[] source, int width, int height, int x, int y)
        => MemToScreenScaledCoord(source, width, height, uiX + scaleFactor * x, uiY + scaleFactor * y);

    public void MemToScreenScaledCoord(byte[] source, int width, int height, int destx, int desty)
    {
        int i, j, sci, scj;
        int m, n;

        IntPtr destPtr = LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero) return;

        unsafe
        {
            byte* dest = (byte*)destPtr;

            // A picture reaching past the screen's edges (a game pack's bigger pictures in a
            // menu laid out for smaller ones, say) is cut off there
            for (j = 0, scj = 0; j < height; j++, scj += scaleFactor)
            {
                for (i = 0, sci = 0; i < width; i++, sci += scaleFactor)
                {
                    byte col = source[(j * width) + i];
                    for (m = 0; m < scaleFactor; m++)
                    {
                        int y = scj + m + desty;
                        if (y < 0 || y >= screenHeight)
                            continue;
                        for (n = 0; n < scaleFactor; n++)
                        {
                            int x = sci + n + destx;
                            if (x >= 0 && x < screenWidth)
                                dest[ylookup[y] + x] = col;
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

    public void HorizontalLine(int x1, int x2, int y, string color) => Bar(x1, y, x2 - x1 + 1, 1, color);

    public void VerticalLine(int y1, int y2, int x, string color) => Bar(x, y1, 1, y2 - y1 + 1, color);

    /// <summary>Fills a rectangle in the 320x200 layout.</summary>
    public void Bar(int x, int y, int width, int height, string color)
        => BarScaledCoord(uiX + scaleFactor * x, uiY + scaleFactor * y, scaleFactor * width, scaleFactor * height, color);

    /// <summary>Fills a rectangle in screen pixels, clipped to the screen.</summary>
    public void BarScaledCoord(int scx, int scy, int scwidth, int scheight, string color)
    {
        if (!ClipToScreen(ref scx, ref scy, ref scwidth, ref scheight))
            return;

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

    /// <summary>Cuts a rectangle (screen pixels) down to the part on the screen; false if none of it is.</summary>
    private bool ClipToScreen(ref int x, ref int y, ref int width, ref int height)
    {
        if (x < 0) { width += x; x = 0; }
        if (y < 0) { height += y; y = 0; }
        width = Math.Min(width, screenWidth - x);
        height = Math.Min(height, screenHeight - y);
        return width > 0 && height > 0;
    }

    // Last table from GetDarkenTable, and the brightness it was built for
    private byte[]? _darkenTable;
    private float _darkenBrightness;

    /// <summary>
    /// A palette remap that dims every color to <paramref name="brightness"/> (0..1) of itself,
    /// each matched to the closest palette entry. Built on first use and kept until the palette
    /// or brightness changes.
    /// </summary>
    private readonly Dictionary<(string Color, int Rows, int Top, int Bottom), byte[]> _gradients = [];

    /// <summary>
    /// A palette index for each of <paramref name="rows"/> rows of text, shading
    /// <paramref name="color"/> from <paramref name="top"/> percent at the first row to
    /// <paramref name="bottom"/> percent at the last: positive toward white, negative toward black.
    /// Each row is matched to the closest palette entry. Built once per color and shape.
    /// </summary>
    internal byte[] GetGradient(string color, int rows, int top, int bottom)
    {
        rows = Math.Max(rows, 1);
        var key = (color, rows, top, bottom);
        if (_gradients.TryGetValue(key, out var table))
            return table;

        var c = gamepal[ResolveColorByte(color)];
        static byte Shade(byte value, float amount)
            => (byte)Math.Clamp(amount >= 0 ? value + (255 - value) * amount : value * (1 + amount), 0, 255);

        table = new byte[rows];
        for (int i = 0; i < rows; i++)
        {
            float t = rows > 1 ? i / (float)(rows - 1) : 0.5f;
            float amount = (top + (bottom - top) * t) / 100f;
            table[i] = FindClosestPaletteIndex(Shade(c.r, amount), Shade(c.g, amount), Shade(c.b, amount));
        }

        _gradients[key] = table;
        return table;
    }

    private readonly Dictionary<(string Glow, string Background, int Radius, int Strength), string[]> _glowColors = [];

    /// <summary>
    /// The color of each of <paramref name="radius"/> glow rings, nearest the text first: the
    /// glow color mixed into the background at <paramref name="strength"/> percent for the
    /// first, fading evenly for the rest. Each is a palette index, as a color string.
    /// </summary>
    internal string[] GetGlowColors(string glow, string background, int radius, int strength)
    {
        radius = Math.Max(radius, 1);
        var key = (glow, background, radius, strength);
        if (_glowColors.TryGetValue(key, out var colors))
            return colors;

        var g = gamepal[ResolveColorByte(glow)];
        var b = gamepal[ResolveColorByte(background)];
        static byte Mix(byte from, byte to, float amount) => (byte)Math.Clamp(from + (to - from) * amount, 0, 255);

        colors = new string[radius];
        for (int ring = 0; ring < radius; ring++)
        {
            float amount = strength / 100f * (1 - ring / (float)radius);
            colors[ring] = FindClosestPaletteIndex(Mix(b.r, g.r, amount), Mix(b.g, g.g, amount), Mix(b.b, g.b, amount)).ToString();
        }

        _glowColors[key] = colors;
        return colors;
    }

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

    private readonly Dictionary<(int Level, int Levels, int FadeSteps, int Fade, int Tint), byte[]> _lightRows = [];

    // The closest palette entry to each #RRGGBB any light row has needed, shared by them all:
    // rows built mid-level (for changing lights) mostly find their colors here
    private readonly Dictionary<int, byte> _lightMatches = [];

    /// <summary>
    /// The shading tables for one light level: <paramref name="fadeSteps"/> palette remaps of 256
    /// entries each, one after another. Every color is lit: dimmed toward black to
    /// <paramref name="level"/>/(<paramref name="levels"/> - 1) of itself and multiplied by the
    /// tint (#FFFFFF for none). Then it's mixed toward the fade color by
    /// step/(<paramref name="fadeSteps"/> - 1) for the step'th remap, and matched to the closest
    /// palette entry. Built on first use and kept until the palette changes.
    /// </summary>
    internal byte[] GetLightRow(int level, int levels, int fadeSteps, byte fadeR, byte fadeG, byte fadeB,
        byte tintR = 255, byte tintG = 255, byte tintB = 255)
    {
        int tint = tintR << 16 | tintG << 8 | tintB;
        var key = (level, levels, fadeSteps, fadeR << 16 | fadeG << 8 | fadeB, tint);
        if (_lightRows.TryGetValue(key, out var row))
            return row;

        float light = level / (float)(levels - 1);
        float lightR = light * tintR / 255f, lightG = light * tintG / 255f, lightB = light * tintB / 255f;
        bool untouched = level == levels - 1 && tint == 0xFFFFFF;
        static byte Mix(float from, byte to, float amount) => (byte)Math.Clamp(from + (to - from) * amount + 0.5f, 0, 255);

        row = new byte[fadeSteps * 256];
        var matches = _lightMatches;
        for (int step = 0; step < fadeSteps; step++)
        {
            float fade = step / (float)(fadeSteps - 1);
            for (int i = 0; i < 256; i++)
            {
                if (untouched && step == 0)
                {
                    row[i] = (byte)i;           // keep the index, not just its color
                    continue;
                }

                var c = gamepal[i];
                byte r = Mix(c.r * lightR, fadeR, fade), g = Mix(c.g * lightG, fadeG, fade), b = Mix(c.b * lightB, fadeB, fade);
                int rgb = r << 16 | g << 8 | b;
                if (!matches.TryGetValue(rgb, out var index))
                    matches[rgb] = index = FindClosestPaletteIndexByLab(r, g, b);
                row[step * 256 + i] = index;
            }
        }

        _lightRows[key] = row;
        return row;
    }

    private (double L, double A, double B)[]? _paletteLab;

    /// <summary>
    /// The palette entry closest to a color as the eye sees it (in CIELAB): a faded or dimmed
    /// color keeps its hue where the palette allows, instead of jumping to one that's merely
    /// near in RGB (a greyed blue to a purple).
    /// </summary>
    private byte FindClosestPaletteIndexByLab(byte r, byte g, byte b)
    {
        _paletteLab ??= gamepal.Select(c => ToLab(c.r, c.g, c.b)).ToArray();

        var (l, a, bb) = ToLab(r, g, b);
        byte closestIndex = 0;
        double closestDistance = double.MaxValue;

        for (int i = 0; i < _paletteLab.Length; i++)
        {
            var p = _paletteLab[i];
            double dl = l - p.L, da = a - p.A, db = bb - p.B;
            double distance = dl * dl + da * da + db * db;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = (byte)i;
            }
        }

        return closestIndex;
    }

    static (double L, double A, double B) ToLab(byte r, byte g, byte b)
    {
        static double Linear(byte c)
        {
            double v = c / 255.0;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        static double F(double t) => t > 216.0 / 24389 ? Math.Cbrt(t) : (24389.0 / 27 * t + 16) / 116;

        double lr = Linear(r), lg = Linear(g), lb = Linear(b);
        double x = (0.4124 * lr + 0.3576 * lg + 0.1805 * lb) / 0.95047;
        double y = 0.2126 * lr + 0.7152 * lg + 0.0722 * lb;
        double z = (0.0193 * lr + 0.1192 * lg + 0.9505 * lb) / 1.08883;
        double fx = F(x), fy = F(y), fz = F(z);
        return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    /// <summary>Replaces every pixel in a rectangle (screen pixels) with its entry in <paramref name="table"/>.</summary>
    public void ShadeRegionScaledCoord(int scx, int scy, int scwidth, int scheight, byte[] table)
    {
        if (!ClipToScreen(ref scx, ref scy, ref scwidth, ref scheight))
            return;

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

    /// <summary>The style of fades that aren't given one (game-info screen-fade-style)</summary>
    internal FadeStyle FadeStyle { get; set; } = FadeStyle.Palette;

    /// <summary>
    /// How long fades that aren't given a style last, in tics (game-info screen-fade-tics);
    /// null leaves each fade its own length
    /// </summary>
    internal int? FadeTics { get; set; }

    /// <summary>
    /// Tics a styled fade runs for each palette fade step, which takes about a WaitVBL and a
    /// vsynced present, so a fade lasts about as long whatever its style.
    /// </summary>
    private const int TicsPerFadeStep = 2;

    internal void FadeIn() => FadeIn(30);
    internal void FadeIn(int steps) => FadeIn(FadeStyle, new GamePalette { Colors = gamepal }, steps, FadeTics);
    internal void FadeIn(FadeStyle style, int steps, int? tics = null) => FadeIn(style, new GamePalette { Colors = gamepal }, steps, tics);
    internal void FadeIn(GamePalette gamePalette, int steps) => FadeIn(FadeStyle, gamePalette, steps, FadeTics);

    private void FadeIn(int start, int end, GamePalette gamePalette, int steps)
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

    internal void FadeOut() => FadeOut(new Color { Alpha = 255 }, 30);
    internal void FadeOut(Color color, int steps) => FadeOut(FadeStyle, color, steps, FadeTics);

    private void FadeOut(int start, int end, Color color, int steps)
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


    /// <summary>A color name, #RRGGBB or palette index, as the palette index it draws with</summary>
    internal byte ResolveColor(string color) => ResolveColorByte(color);

    /// <summary>
    /// Draws part of a paletted image (width by height from srcX, srcY) at (x, y) in 320x200
    /// coordinates, leaving out pixels the opacity mask clears and pixels of the key color. With
    /// a color, every pixel drawn is that color instead of its own, or with
    /// <paramref name="rowColors"/> too, each row that row's color. Clipped to the screen.
    /// </summary>
    internal void DrawImageRegion(byte[] pixels, byte[]? opacityMask, int stride, int srcX, int srcY, int width, int height,
        int x, int y, byte? key = null, string? color = null, byte[]? rowColors = null)
    {
        IntPtr destPtr = LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero)
            return;

        byte? col = color == null ? null : ResolveColorByte(color);

        unsafe
        {
            byte* dest = (byte*)destPtr;
            for (int j = 0; j < height; j++)
            {
                int dy = y + j;
                if (!LayoutYOnScreen(dy))
                    continue;

                byte? rowCol = col != null && rowColors != null ? rowColors[Math.Min(j, rowColors.Length - 1)] : col;

                for (int i = 0; i < width; i++)
                {
                    int dx = x + i;
                    if (!LayoutXOnScreen(dx))
                        continue;

                    int src = (srcY + j) * stride + srcX + i;
                    byte pixel = pixels[src];
                    if ((opacityMask != null && opacityMask[src] == 0) || pixel == key)
                        continue;

                    PutLayoutPixel(dest, dx, dy, rowCol ?? pixel);
                }
            }
        }

        UnlockSurface(screenBuffer);
    }

    // Whether a whole 320x200 layout pixel's screen square is on the screen. A margin narrower
    // than the UI scale leaves a partial square at the edge, which is left out.
    private bool LayoutXOnScreen(int x)
    {
        int sx = uiX + x * scaleFactor;
        return sx >= 0 && sx + scaleFactor <= screenWidth;
    }

    private bool LayoutYOnScreen(int y)
    {
        int sy = uiY + y * scaleFactor;
        return sy >= 0 && sy + scaleFactor <= screenHeight;
    }

    // Fills a 320x200 layout pixel's square of screen pixels; it must be on screen
    private unsafe void PutLayoutPixel(byte* dest, int x, int y, byte color)
    {
        int sx = uiX + x * scaleFactor, sy = uiY + y * scaleFactor;
        for (int m = 0; m < scaleFactor; m++)
        {
            byte* row = dest + ylookup[sy + m] + sx;
            for (int n = 0; n < scaleFactor; n++)
                row[n] = color;
        }
    }

    /// <summary>
    /// Draws a line of text in a Wolf3D font at (px, py) in 320x200 coordinates. Clipped to the
    /// screen, so text (or a shadow, outline or glow drawn with it) can reach past the edges.
    /// </summary>
    /// <param name="rowColors">A palette index for each row of the glyphs, in place of <paramref name="fontcolor"/></param>
    internal void DrawPropString(int px, int py, string text, string fontcolor, FontAsset font, byte[]? rowColors = null)
    {
        IntPtr destPtr = LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero)
            return;

        int height = font.Height;
        byte col = ResolveColorByte(fontcolor);

        unsafe
        {
            byte* dest = (byte*)destPtr;

            foreach (char ch in text)
            {
                // Past the font's 256 glyphs: leave a space's worth of room (see VgaFont.Advance)
                if (ch >= font.Width.Length)
                {
                    px += font.Width[' '];
                    continue;
                }

                int step = font.Width[ch];
                int locIndex = font.Location[ch];

                for (int column = 0; column < step; column++, px++)
                {
                    if (!LayoutXOnScreen(px))
                        continue;

                    for (int i = 0; i < height; i++)
                    {
                        int y = py + i;
                        if (!LayoutYOnScreen(y) || font.RawData[locIndex + column + i * step] == 0)
                            continue;

                        PutLayoutPixel(dest, px, y, rowColors != null ? rowColors[Math.Min(i, rowColors.Length - 1)] : col);
                    }
                }
            }

            UnlockSurface(screenBuffer);
        }
    }

    /// <summary>Writes the screen to a BMP file; false (see SDL_GetError) if it couldn't.</summary>
    internal bool SaveScreenShot(string filename) => SDL.SDL_SaveBMP(screenBuffer, filename) == 0;

    // The 3D view as last drawn, so a save gets a picture of the game even though a menu,
    // message or the console has been drawn over the screen since
    private byte[] _viewCopy = [];
    private int _viewCopyWidth, _viewCopyHeight;

    /// <summary>
    /// Keeps a copy of the 3D view (screen pixels) for <see cref="MakeThumbnail"/>. Called
    /// every frame once the view is drawn, before the automap, console or anything else goes over it.
    /// </summary>
    internal void KeepViewCopy(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        if (_viewCopy.Length != width * height)
            _viewCopy = new byte[width * height];
        _viewCopyWidth = width;
        _viewCopyHeight = height;

        IntPtr srcPtr = LockSurface(screenBuffer);
        if (srcPtr == IntPtr.Zero) return;

        unsafe
        {
            byte* src = (byte*)srcPtr;
            for (int j = 0; j < height; j++)
                new ReadOnlySpan<byte>(src + ylookup[y + j] + x, width).CopyTo(_viewCopy.AsSpan(j * width, width));
        }

        UnlockSurface(screenBuffer);
    }

    /// <summary>
    /// The last 3D view shrunk to at most <paramref name="maxWidth"/> wide, keeping its shape,
    /// as game palette indices; null before a view has been drawn. Each pixel is the average
    /// of the ones it covers, matched back to the palette, so it doesn't sparkle like
    /// skipping pixels would.
    /// </summary>
    internal SaveThumbnail? MakeThumbnail(int maxWidth)
    {
        int sw = _viewCopyWidth, sh = _viewCopyHeight;
        if (sw == 0 || sh == 0)
            return null;

        int dw = Math.Min(maxWidth, sw);
        int dh = Math.Max(1, (int)((long)sh * dw / sw));
        var pixels = new byte[dw * dh];
        var matches = new Dictionary<int, byte>();

        for (int dy = 0; dy < dh; dy++)
        {
            int y0 = dy * sh / dh, y1 = Math.Max(y0 + 1, (dy + 1) * sh / dh);
            for (int dx = 0; dx < dw; dx++)
            {
                int x0 = dx * sw / dw, x1 = Math.Max(x0 + 1, (dx + 1) * sw / dw);
                int r = 0, g = 0, b = 0, count = (x1 - x0) * (y1 - y0);
                for (int sy = y0; sy < y1; sy++)
                {
                    for (int sx = x0; sx < x1; sx++)
                    {
                        var c = gamepal[_viewCopy[sy * sw + sx]];
                        r += c.r; g += c.g; b += c.b;
                    }
                }

                r /= count; g /= count; b /= count;
                int key = (r << 16) | (g << 8) | b;
                if (!matches.TryGetValue(key, out var index))
                    matches[key] = index = FindClosestPaletteIndex((byte)r, (byte)g, (byte)b);
                pixels[dy * dw + dx] = index;
            }
        }

        return new SaveThumbnail(dw, dh, pixels);
    }

    /// <summary>
    /// Draws a thumbnail stretched over a rectangle given in 320x200 units, sampled at screen
    /// resolution so it's as sharp as the thumbnail allows.
    /// </summary>
    internal void DrawThumbnail(SaveThumbnail thumbnail, int x, int y, int width, int height)
    {
        int destx = uiX + x * scaleFactor, desty = uiY + y * scaleFactor;
        int dw = width * scaleFactor, dh = height * scaleFactor;
        // Clipped to the screen, but sampled over the whole rectangle
        int clipw = Math.Min(dw, screenWidth - destx), cliph = Math.Min(dh, screenHeight - desty);
        if (destx < 0 || desty < 0 || clipw <= 0 || cliph <= 0 || thumbnail.Width <= 0 || thumbnail.Height <= 0)
            return;

        IntPtr destPtr = LockSurface(screenBuffer);
        if (destPtr == IntPtr.Zero) return;

        unsafe
        {
            byte* dest = (byte*)destPtr;
            for (int j = 0; j < cliph; j++)
            {
                int row = (j * thumbnail.Height / dh) * thumbnail.Width;
                byte* line = dest + ylookup[desty + j] + destx;
                for (int i = 0; i < clipw; i++)
                    line[i] = thumbnail.Pixels[row + i * thumbnail.Width / dw];
            }
        }

        UnlockSurface(screenBuffer);
    }

    /// <summary>
    /// Changes what's on screen over to what's been drawn in the screen buffer since, within a
    /// rectangle (screen pixels), over <paramref name="tics"/> tics. The palette style has nothing
    /// to step between here, so it just shows the new frame.
    /// </summary>
    internal void Transition(FadeStyle style, int x1, int y1, int width, int height, uint tics)
    {
        if (style != FadeStyle.Palette)
        {
            var from = CaptureScreen();
            var canvas = new TransitionCanvas
            {
                From = from,
                To = RenderScreenBuffer(curpal),
                Output = (uint[])from.Clone(),
                Stride = screenWidth,
                X = x1,
                Y = y1,
                Width = width,
                Height = height,
                ScaleFactor = scaleFactor,
            };
            RunTransition(style, canvas, tics);
        }

        Update(screenBuffer);
    }

    /// <summary>
    /// Fades what's on screen out to a solid color in the given style, taking <paramref name="tics"/>
    /// tics when given, or else as long as a palette fade of <paramref name="steps"/> steps.
    /// </summary>
    internal void FadeOut(FadeStyle style, Color color, int steps, int? tics = null)
    {
        if (style == FadeStyle.Palette)
        {
            FadeOut(0, 255, color, PaletteFadeSteps(steps, tics));
            return;
        }

        var from = CaptureScreen();
        var to = new uint[from.Length];
        Array.Fill(to, SDL.SDL_MapRGBA(GetSurface(screen).format, color.Red, color.Green, color.Blue, 255));

        RunTransition(style, FullScreenCanvas(from, to, fromIsSolid: false, toIsSolid: true), StyledFadeTics(steps, tics));

        // Leave things as the palette fade does, so anything drawn before the fade in stays hidden
        FillPalette(color.Red, color.Green, color.Blue);
        screenfaded = true;
    }

    /// <summary>
    /// Fades in the screen buffer, drawn in <paramref name="gamePalette"/>, from whatever's on
    /// screen (normally the color it faded out to) in the given style, taking <paramref name="tics"/>
    /// tics when given, or else as long as a palette fade of <paramref name="steps"/> steps.
    /// </summary>
    internal void FadeIn(FadeStyle style, GamePalette gamePalette, int steps, int? tics = null)
    {
        if (style == FadeStyle.Palette)
        {
            FadeIn(0, 255, gamePalette, PaletteFadeSteps(steps, tics));
            return;
        }

        var from = CaptureScreen();
        var to = RenderScreenBuffer(gamePalette.Colors);

        RunTransition(style, FullScreenCanvas(from, to, fromIsSolid: screenfaded, toIsSolid: false), StyledFadeTics(steps, tics));

        SetPalette(gamePalette.Colors, true);
        screenfaded = false;
    }

    private static int PaletteFadeSteps(int steps, int? tics) => tics is { } t ? Math.Max(1, t / TicsPerFadeStep) : steps;
    private static uint StyledFadeTics(int steps, int? tics) => (uint)(tics ?? steps * TicsPerFadeStep);

    private TransitionCanvas FullScreenCanvas(uint[] from, uint[] to, bool fromIsSolid, bool toIsSolid) => new()
    {
        From = from,
        To = to,
        Output = (uint[])from.Clone(),
        Stride = screenWidth,
        X = 0,
        Y = 0,
        Width = screenWidth,
        Height = screenHeight,
        ScaleFactor = scaleFactor,
        FromIsSolid = fromIsSolid,
        ToIsSolid = toIsSolid,
    };

    /// <summary>Runs a transition to the end, putting each step on screen once a tic.</summary>
    private void RunTransition(FadeStyle style, TransitionCanvas canvas, uint tics)
    {
        ScreenTransition transition = style switch
        {
            FadeStyle.Fizzle => new FizzleTransition(rndmask, (int)rndbits_y),
            FadeStyle.Melt => new MeltTransition(canvas),
            FadeStyle.Mosaic => new MosaicTransition(),
            _ => throw new ArgumentOutOfRangeException(nameof(style), style, "Not a transition style"),
        };

        uint start = GameEngineManager.GetTimeCount();
        uint frame = start;

        while (true)
        {
            double progress = tics == 0 ? 1 : (double)(GameEngineManager.GetTimeCount() - start) / tics;
            bool done = transition.Draw(canvas, Math.Min(progress, 1));

            PresentPixels(canvas.Output);

            if (done)
                return;

            frame++;
            GameEngineManager.DelayTics((int)(frame - GameEngineManager.GetTimeCount()));        // don't go too fast
        }
    }

    /// <summary>The frame last put on screen, as 32-bit pixels.</summary>
    private uint[] CaptureScreen()
    {
        var pixels = new uint[screenWidth * screenHeight];

        IntPtr src = LockSurface(screen);
        if (src == IntPtr.Zero)
            throw new PfWolfVideoException("Unable to lock screen surface: {0}", SDL.SDL_GetError());

        unsafe
        {
            for (int y = 0; y < screenHeight; y++)
                new ReadOnlySpan<uint>((byte*)src + y * screenPitch, screenWidth)
                    .CopyTo(pixels.AsSpan(y * screenWidth, screenWidth));
        }

        UnlockSurface(screen);
        return pixels;
    }

    /// <summary>The screen buffer as it would look drawn in <paramref name="palette"/>, as 32-bit pixels.</summary>
    private uint[] RenderScreenBuffer(SDL.SDL_Color[] palette)
    {
        var format = GetSurface(screen).format;
        var colors = new uint[256];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = SDL.SDL_MapRGBA(format, palette[i].r, palette[i].g, palette[i].b, 255);

        var pixels = new uint[screenWidth * screenHeight];

        IntPtr src = LockSurface(screenBuffer);
        if (src == IntPtr.Zero)
            throw new PfWolfVideoException("Unable to lock screen buffer: {0}", SDL.SDL_GetError());

        unsafe
        {
            byte* buffer = (byte*)src;
            for (int y = 0; y < screenHeight; y++)
            {
                byte* row = buffer + ylookup[y];
                for (int x = 0; x < screenWidth; x++)
                    pixels[y * screenWidth + x] = colors[row[x]];
            }
        }

        UnlockSurface(screenBuffer);
        return pixels;
    }

    /// <summary>Copies full-screen 32-bit pixels to the screen surface and shows them.</summary>
    private void PresentPixels(uint[] pixels)
    {
        IntPtr dest = LockSurface(screen);
        if (dest == IntPtr.Zero)
            throw new PfWolfVideoException("Unable to lock screen surface: {0}", SDL.SDL_GetError());

        unsafe
        {
            for (int y = 0; y < screenHeight; y++)
                pixels.AsSpan(y * screenWidth, screenWidth)
                    .CopyTo(new Span<uint>((byte*)dest + y * screenPitch, screenWidth));
        }

        UnlockSurface(screen);

        SDL.SDL_UpdateTexture(texture, IntPtr.Zero, GetSurface(screen).pixels, (int)screenPitch);
        Present();
    }

    // Shows the texture; the clear blacks out any letterbox bars around it
    private void Present()
    {
        SDL.SDL_RenderClear(renderer);
        SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
        SDL.SDL_RenderPresent(renderer);
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
        Present();
    }

    public IntPtr LockSurface() => LockSurface(screenBuffer);
    public void UnlockSurface() => UnlockSurface(screenBuffer);

    public void CenterMouse()
    {
        SDL.SDL_GetWindowSize(window, out int width, out int height);
        SDL.SDL_WarpMouseInWindow(window, width / 2, height / 2);
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

    private void InitializeSDLVideo(VideoSettings settings)
    {
        var title = _assetManager.Value.GetGameTitle();
        if (string.IsNullOrWhiteSpace(title))
            title = "PFWolf";

        var flags = SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL;
        if (settings.Fullscreen)
            flags |= SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN_DESKTOP;

        window = SDL.SDL_CreateWindow(title, SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED,
            settings.WindowWidth, settings.WindowHeight, flags);
        if (window == IntPtr.Zero)
            throw new PfWolfVideoException("Unable to create the window: {0}", SDL.SDL_GetError());

        SDL.SDL_ShowCursor(SDL.SDL_DISABLE);

        Array.Copy(gamepal, curpal, 256);
        settings = ResolveRenderSize(settings);

        // A saved mode this machine can't do (another display, another graphics card) falls
        // back to the default one rather than stopping the game
        var defaults = new VideoSettings();
        if (!CreateVideoMode(settings) && settings != defaults)
        {
            Console.WriteLine($"Couldn't start in {settings.RenderWidth}x{settings.RenderHeight} ({SDL.SDL_GetError()}); using {defaults.RenderWidth}x{defaults.RenderHeight} in a window.");
            DestroyRenderer();
            DestroySurfaces();
            settings = defaults;
            SetWindowMode(settings);
            if (!CreateVideoMode(settings))
                throw new PfWolfVideoException($"Unable to set the {settings.RenderWidth}x{settings.RenderHeight} video mode: {{0}}",
                    SDL.SDL_GetError());
        }

        Settings = settings;
    }

    private bool CreateVideoMode(VideoSettings settings)
        => CreateRenderer(settings) && CreateSurfaces(settings) && CreateTexture(settings);

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
