using SDL2;

namespace PFWolf.Ui;

/// <summary>
/// What the window shows while startup reads the assets, before the game's palette, fonts and
/// pictures are there to draw with: the PFWolf icon over a progress bar, drawn straight to the
/// window with its own SDL renderer. <see cref="Close"/> takes the renderer away before
/// VideoManager.Init makes the game's (a window has one at a time).
/// </summary>
internal sealed class LoadingScreen
{
    // Where each step starts, as a share of the whole startup, from timing Wolf3D's: reading
    // pfwolf.pk3 (its YAML and pictures) is most of it. The names are AssetManager.Load's
    // progress steps, plus Starting for what GameHost does after it; a step not here moves the
    // bar part of the way on.
    private static readonly Dictionary<string, double> StepStarts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Opening the mods"] = 0.02,
        ["Reading pfwolf.pk3"] = 0.05,
        ["Reading pfwolf.pk3 and the mods"] = 0.05,
        ["Reading the sounds and music"] = 0.65,
        ["Reading the levels"] = 0.70,
        ["Reading the pictures"] = 0.75,
        ["Reading the walls and sprites"] = 0.80,
        [Starting] = 0.90,
    };

    /// <summary>The last step, after the assets are read: scripts, checks and the game's setup</summary>
    internal const string Starting = "Starting";

    private static readonly SDL.SDL_Color Background = new() { r = 0, g = 0, b = 0, a = 255 };
    private static readonly SDL.SDL_Color Frame = new() { r = 96, g = 96, b = 96, a = 255 };
    private static readonly SDL.SDL_Color Fill = new() { r = 200, g = 24, b = 24, a = 255 };

    private IntPtr _renderer;
    private IntPtr _icon;
    private int _iconWidth, _iconHeight;
    private double _progress;

    /// <summary>Shows the empty bar in the window; does nothing on SDL's dummy driver (headless runs)</summary>
    internal void Open(IntPtr window)
    {
        if (window == IntPtr.Zero || Managers.VideoManager.Headless)
            return;

        _renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
        if (_renderer == IntPtr.Zero)
            _renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_SOFTWARE);
        if (_renderer == IntPtr.Zero)
            return;     // nothing to show it with, so startup goes on without it

        _icon = LoadIcon(_renderer, out _iconWidth, out _iconHeight);

        // The window opens hidden (VideoManager.OpenWindow), so it's shown with this in it rather
        // than blank; drawn again once shown, as a hidden window's first frame may not stay
        Draw();
        SDL.SDL_ShowWindow(window);
        Draw();
    }

    /// <summary>Moves the bar to where the named step starts</summary>
    internal void Step(string step)
    {
        _progress = StepStarts.TryGetValue(step, out var start)
            ? Math.Max(_progress, start)
            : _progress + (1.0 - _progress) / 4;
        Draw();
    }

    /// <summary>Takes the renderer off the window, leaving the last picture there until the game draws</summary>
    internal void Close()
    {
        if (_icon != IntPtr.Zero)
            SDL.SDL_DestroyTexture(_icon);
        if (_renderer != IntPtr.Zero)
            SDL.SDL_DestroyRenderer(_renderer);
        _icon = _renderer = IntPtr.Zero;
    }

    private void Draw()
    {
        if (_renderer == IntPtr.Zero)
            return;

        // Keeps Windows from calling the window "Not Responding"; what's pumped waits in SDL's
        // queue for the game to read
        SDL.SDL_PumpEvents();

        if (SDL.SDL_GetRendererOutputSize(_renderer, out int width, out int height) != 0 || width <= 0 || height <= 0)
            return;

        SetColor(Background);
        SDL.SDL_RenderClear(_renderer);

        // The icon a sixth of the window high, above the bar, the pair centered a little high
        int barWidth = width * 2 / 5;
        int barHeight = Math.Max(6, height / 50);
        int iconSize = Math.Max(32, height / 6);
        int gap = iconSize / 2;
        int top = (height - (iconSize + gap + barHeight)) * 9 / 20;

        if (_icon != IntPtr.Zero)
        {
            var iconRect = new SDL.SDL_Rect
            {
                x = (width - iconSize) / 2, y = top,
                w = iconSize, h = iconSize * _iconHeight / Math.Max(1, _iconWidth),
            };
            SDL.SDL_RenderCopy(_renderer, _icon, IntPtr.Zero, ref iconRect);
        }

        int border = Math.Max(1, barHeight / 6);
        var frame = new SDL.SDL_Rect { x = (width - barWidth) / 2, y = top + iconSize + gap, w = barWidth, h = barHeight };
        SetColor(Frame);
        SDL.SDL_RenderFillRect(_renderer, ref frame);

        var inside = new SDL.SDL_Rect { x = frame.x + border, y = frame.y + border, w = frame.w - border * 2, h = frame.h - border * 2 };
        SetColor(Background);
        SDL.SDL_RenderFillRect(_renderer, ref inside);

        var fill = inside with { w = (int)(inside.w * Math.Clamp(_progress, 0, 1)) };
        if (fill.w > 0)
        {
            SetColor(Fill);
            SDL.SDL_RenderFillRect(_renderer, ref fill);
        }

        SDL.SDL_RenderPresent(_renderer);
    }

    private void SetColor(SDL.SDL_Color color) => SDL.SDL_SetRenderDrawColor(_renderer, color.r, color.g, color.b, color.a);

    // The embedded icons/pfwolf-64.png (the window's icon too) as a texture, or nothing without it
    private static IntPtr LoadIcon(IntPtr renderer, out int width, out int height)
    {
        width = height = 0;
        using var stream = typeof(LoadingScreen).Assembly.GetManifestResourceStream("pfwolf-icon.png");
        if (stream == null)
            return IntPtr.Zero;
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        var image = PFWolf.Loaders.ImageDecoder.Decode(bytes.ToArray());
        width = image.Width;
        height = image.Height;

        // Scaled smoothly: it's drawn larger than its 64 pixels
        SDL.SDL_SetHint(SDL.SDL_HINT_RENDER_SCALE_QUALITY, "1");
        unsafe
        {
            fixed (byte* pixels = image.Pixels)
            {
                // ABGR8888 is R, G, B, A in memory on little-endian machines
                var surface = SDL.SDL_CreateRGBSurfaceWithFormatFrom((IntPtr)pixels, image.Width, image.Height,
                    32, image.Width * 4, SDL.SDL_PIXELFORMAT_ABGR8888);
                if (surface == IntPtr.Zero)
                    return IntPtr.Zero;
                var texture = SDL.SDL_CreateTextureFromSurface(renderer, surface);
                SDL.SDL_FreeSurface(surface);
                return texture;
            }
        }
    }
}
