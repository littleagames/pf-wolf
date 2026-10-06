namespace PFWolf.Configuration;

/// <summary>How the screen buffer is smoothed when it's scaled up to the window.</summary>
internal enum ScaleFilter : byte
{
    Nearest,
    Linear,
}

/// <summary>
/// The video mode: the size the game draws at, and how that's shown in the window.
/// Applied with VideoManager.ApplyVideoSettings.
/// </summary>
internal sealed record VideoSettings
{
    internal const int BaseWidth = 320;
    internal const int BaseHeight = 200;

    // The largest render size: the fizzle fade's random sequence covers no more
    internal const int MaxRenderWidth = 8192;
    internal const int MaxRenderHeight = 4096;

    /// <summary>Borderless fullscreen on the desktop; the window size is kept for leaving it.</summary>
    public bool Fullscreen { get; init; }

    /// <summary>
    /// The screen buffer is 320x200 times this, unless <see cref="RenderSize"/> gives another
    /// size. Everything is drawn at this size, so it sets how sharp the 3D view and text are,
    /// not how big the window is.
    /// </summary>
    public int RenderScale { get; init; } = 2;

    /// <summary>
    /// A screen buffer of any size and shape (at least 320x200) in place of 320x200 times
    /// <see cref="RenderScale"/>. The 320x200 menus and status bar sit in the middle of it,
    /// drawn at <see cref="UiScale"/>.
    /// </summary>
    public (int Width, int Height)? RenderSize { get; init; }

    /// <summary>
    /// The render size follows what the picture is shown in: the window, or the desktop when
    /// fullscreen (less a sixth of its height when aspect correcting, which stretches it back).
    /// VideoManager works it out into <see cref="RenderSize"/> whenever the mode is set.
    /// </summary>
    public bool MatchWindow { get; init; }

    /// <summary>
    /// Screen pixels to each of the menus' and status bar's 320x200 pixels; 0 (auto) is the
    /// most whole number that fits. Smaller makes them smaller on the screen, not less sharp.
    /// It needn't be whole: at 2.5 their pixels are 2 and 3 screen pixels by turns.
    /// </summary>
    public double UiScale { get; init; }

    /// <summary>The window's size when it isn't fullscreen.</summary>
    public int WindowWidth { get; init; } = 640;
    public int WindowHeight { get; init; } = 400;

    public bool VSync { get; init; } = true;

    /// <summary>Shows the 320x200 picture at 4:3, the shape it had on a CRT, instead of with square pixels.</summary>
    public bool AspectCorrect { get; init; }

    public ScaleFilter Filter { get; init; } = ScaleFilter.Nearest;

    public int RenderWidth => RenderSize?.Width ?? BaseWidth * RenderScale;
    public int RenderHeight => RenderSize?.Height ?? BaseHeight * RenderScale;

    /// <summary>The largest UI scale the render size has room for, which may not be whole.</summary>
    public double MaxUiScale => Math.Max(1, Math.Min((double)RenderWidth / BaseWidth, (double)RenderHeight / BaseHeight));

    /// <summary>
    /// The UI scale in use: <see cref="UiScale"/>, or when that's auto the most whole number that
    /// fits (crisp, every layout pixel the same size), and never more than fits.
    /// </summary>
    public double EffectiveUiScale => UiScale <= 0 ? Math.Floor(MaxUiScale) : Math.Clamp(UiScale, 1, MaxUiScale);

    /// <summary>A UI scale for showing: up to two decimals, with no trailing zeros (2, 2.5, 3.38).</summary>
    public static string FormatScale(double scale) =>
        scale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The shape the picture is shown at: the render size, with its pixels stretched 6:5 tall
    /// when aspect correcting (320x200 becomes 4:3, as on a CRT).
    /// </summary>
    public int DisplayWidth => RenderWidth;
    public int DisplayHeight => AspectCorrect ? RenderHeight * 6 / 5 : RenderHeight;
}
