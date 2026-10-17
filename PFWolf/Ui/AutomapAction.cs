namespace PFWolf;

/// <summary>The keys that work while the automap is open; bound in <see cref="Configuration.ControlBindings"/>.</summary>
internal enum AutomapAction
{
    ZoomIn,
    ZoomOut,
    PanUp,
    PanDown,
    PanLeft,
    PanRight,
    Center,
    Follow,
    Rotate,
    Style,
    Overlay,
    Grid,
    Stats,

    Count
}
