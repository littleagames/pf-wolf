using System.Numerics;

namespace Wolf3D.Entities;

internal class MenuMetadata
{
    public Vector2 Position { get; set; } = Vector2.Zero;

    /// <summary>
    /// The type of prefab menu this instance can build from
    /// </summary>
    public string? Type { get; set; } = null;

    /// <summary>
    /// Music track asset name to play when entering this menu.
    /// (If the track name is the same as a previous menu transition,
    /// it will continue to play, unless you use the "MusicForceRestart"
    /// </summary>
    public string? Music { get; set; } = null;

    /// <summary>
    /// List of drawable items on the menu, list them in order of
    /// drawing bottom to top
    /// </summary>
    public List<MenuComponent> Components { get; set; } = new();

    public List<MenuItem> MenuItems { get; set; } = new();
    public int Indent { get; internal set; }
}

internal abstract record MenuComponent
{

}

internal record Background : MenuComponent
{
    public byte Color { get; set; }

    public Background(byte color)
    {
        Color = color;    
    }
}

internal record Window : MenuComponent
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public Window(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public Window (int x, int y, int width, int height, string theme)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}

internal record Graphic : MenuComponent
{
    public string Asset { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public HorizontalOrientation OrientationX { get; set; } = HorizontalOrientation.Left;
    public VerticalOrientation OrientationY { get; set; } = VerticalOrientation.Top;

    public Graphic(string asset, int x, int y)
    {
        Asset = asset;
        X = x;
        Y = y;
    }

    public Graphic(string asset, int x, VerticalOrientation y)
    {
        Asset = asset;
        X = x;
        OrientationY = y;
    }

    public Graphic(string asset, HorizontalOrientation x, int y)
    {
        Asset = asset;
        OrientationX = x;
        Y = y;
    }

    public Graphic(string asset, HorizontalOrientation x, VerticalOrientation y)
    {
        Asset = asset;
        OrientationX = x;
        OrientationY = y;
    }
}

internal record Stripe : MenuComponent
{
    public int Y { get; set; }
    public byte BackingColor { get; set; }
    public byte LineColor { get; set; }
    public Stripe(int y)
    {
        Y = y;
        BackingColor = 0x00;
        LineColor = 0x2c;
    }

    public Stripe(int y, byte backingColor, byte lineColor)
    {
        Y = y;
        BackingColor = backingColor;
        LineColor = lineColor;
    }
}

internal enum HorizontalOrientation
{
    Left,
    Center,
    Right
}

internal enum VerticalOrientation
{
    Top,
    Center,
    Bottom
}

internal abstract record MenuItem
{
    public string Text { get; set; } = null!;
    public bool IsEnabled { get; set; }
}

internal record MenuSwitcher : MenuItem
{
    public MenuSwitcher(string text, bool isEnabled, string action)
    {
        Text = text;
        IsEnabled = isEnabled;
        Action = action;
    }

    public string? Action { get; init; } = null;
}

internal record ToggleMenuItem : MenuItem
{
    public bool State { get; set; } = false;
    public ToggleMenuItem(string text, bool isEnabled, bool defaultState)
    {
        Text = text;
        IsEnabled = isEnabled;
        State = defaultState;
    }
}

internal record BlankMenuItem : MenuItem
{
    public BlankMenuItem()
    {
        Text = "";
        IsEnabled = false;
    }
}