namespace Wolf3D.Configuration;

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

    /// <summary>Borderless fullscreen on the desktop; the window size is kept for leaving it.</summary>
    public bool Fullscreen { get; init; }

    /// <summary>
    /// The screen buffer is 320x200 times this. Everything is drawn at this size, so it sets
    /// how sharp the 3D view and text are, not how big the window is.
    /// </summary>
    public int RenderScale { get; init; } = 2;

    /// <summary>The window's size when it isn't fullscreen.</summary>
    public int WindowWidth { get; init; } = 640;
    public int WindowHeight { get; init; } = 400;

    public bool VSync { get; init; } = true;

    /// <summary>Shows the 320x200 picture at 4:3, the shape it had on a CRT, instead of with square pixels.</summary>
    public bool AspectCorrect { get; init; }

    public ScaleFilter Filter { get; init; } = ScaleFilter.Nearest;

    public int RenderWidth => BaseWidth * RenderScale;
    public int RenderHeight => BaseHeight * RenderScale;

    /// <summary>The shape the picture is shown at: the render size, stretched to 4:3 when aspect correcting.</summary>
    public int DisplayWidth => RenderWidth;
    public int DisplayHeight => AspectCorrect ? RenderWidth * 3 / 4 : RenderHeight;
}
