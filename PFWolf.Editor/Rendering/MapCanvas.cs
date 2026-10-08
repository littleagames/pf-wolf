using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PFWolf.Constants;
using PFWolf.Editor.Data;
using PFWolf.Enums;

namespace PFWolf.Editor.Rendering;

// Inside the namespace, so it beats the game's PFWolf.Point
using Point = Avalonia.Point;

/// <summary>A map tile the pointer is over, or none (-1, -1)</summary>
public sealed class TileEventArgs(int x, int y) : EventArgs
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public bool IsNone => X < 0;
}

/// <summary>
/// A level from above: walls and doors in their textures, things as their sprites, and the
/// other planes (flats, heights, tags, light zones) as numbered overlays. The wheel zooms
/// around the pointer; dragging pans.
/// </summary>
public sealed class MapCanvas : Control
{
    public static readonly StyledProperty<MapTiles?> TilesProperty = AvaloniaProperty.Register<MapCanvas, MapTiles?>(nameof(Tiles));
    public static readonly StyledProperty<ArtCache?> ArtProperty = AvaloniaProperty.Register<MapCanvas, ArtCache?>(nameof(Art));
    public static readonly StyledProperty<bool> ShowWallsProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowWalls), true);
    public static readonly StyledProperty<bool> ShowThingsProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowThings), true);
    public static readonly StyledProperty<bool> ShowFlatsProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowFlats));
    public static readonly StyledProperty<bool> ShowHeightsProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowHeights));
    public static readonly StyledProperty<bool> ShowTagsProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowTags));
    public static readonly StyledProperty<bool> ShowZonesProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowZones));
    public static readonly StyledProperty<bool> ShowGridProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowGrid), true);

    static MapCanvas()
    {
        AffectsRender<MapCanvas>(TilesProperty, ArtProperty, ShowWallsProperty, ShowThingsProperty, ShowFlatsProperty,
            ShowHeightsProperty, ShowTagsProperty, ShowZonesProperty, ShowGridProperty);
        FocusableProperty.OverrideDefaultValue<MapCanvas>(true);
        ClipToBoundsProperty.OverrideDefaultValue<MapCanvas>(true);
    }

    public MapTiles? Tiles { get => GetValue(TilesProperty); set => SetValue(TilesProperty, value); }
    public ArtCache? Art { get => GetValue(ArtProperty); set => SetValue(ArtProperty, value); }
    public bool ShowWalls { get => GetValue(ShowWallsProperty); set => SetValue(ShowWallsProperty, value); }
    public bool ShowThings { get => GetValue(ShowThingsProperty); set => SetValue(ShowThingsProperty, value); }
    public bool ShowFlats { get => GetValue(ShowFlatsProperty); set => SetValue(ShowFlatsProperty, value); }
    public bool ShowHeights { get => GetValue(ShowHeightsProperty); set => SetValue(ShowHeightsProperty, value); }
    public bool ShowTags { get => GetValue(ShowTagsProperty); set => SetValue(ShowTagsProperty, value); }
    public bool ShowZones { get => GetValue(ShowZonesProperty); set => SetValue(ShowZonesProperty, value); }
    public bool ShowGrid { get => GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }

    /// <summary>The pointer moved onto another tile, or off the map</summary>
    public event EventHandler<TileEventArgs>? TileHovered;

    private const double MinZoom = 2, MaxZoom = 128;

    // Screen pixels per tile, and where the map's top left corner is on screen
    private double _zoom = 12;
    private Point _origin;
    private bool _fitPending = true;

    private Point? _dragStart;
    private Point _dragOrigin;
    private (int X, int Y) _hover = (-1, -1);

    private static readonly IBrush Backdrop = new SolidColorBrush(Color.FromRgb(24, 24, 28));
    private static readonly IBrush FloorBrush = new SolidColorBrush(Color.FromRgb(56, 56, 60));
    private static readonly IBrush BuriedBrush = new SolidColorBrush(Color.FromArgb(170, 16, 16, 20));
    private static readonly IBrush UnknownBrush = new SolidColorBrush(Color.FromRgb(220, 40, 40));
    private static readonly IBrush MissingTextureBrush = new SolidColorBrush(Color.FromRgb(120, 120, 128));
    private static readonly IBrush ThingDotBrush = new SolidColorBrush(Color.FromRgb(240, 200, 64));
    private static readonly IBrush StartBrush = new SolidColorBrush(Color.FromRgb(64, 220, 96));
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)), 1);
    private static readonly IPen HoverPen = new Pen(Brushes.White, 2);
    private static readonly IPen TriggerPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 160, 32)), 2, new DashStyle([2, 2], 0));
    private static readonly IPen MapEdgePen = new Pen(new SolidColorBrush(Color.FromArgb(96, 255, 255, 255)), 1);
    private static readonly Typeface LabelFont = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TilesProperty)
        {
            _fitPending = true;
            SetHover(-1, -1);
        }
    }

    /// <summary>Zooms so the whole level fits, centred</summary>
    public void FitToView()
    {
        Fit();
        InvalidateVisual();
    }

    private void Fit()
    {
        if (Tiles is not { } tiles || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        _zoom = Math.Clamp(Math.Min(Bounds.Width / tiles.Width, Bounds.Height / tiles.Height) * 0.95, MinZoom, MaxZoom);
        _origin = new Point((Bounds.Width - tiles.Width * _zoom) / 2, (Bounds.Height - tiles.Height * _zoom) / 2);
        _fitPending = false;
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Backdrop, new Rect(Bounds.Size));
        if (Tiles is not { } tiles)
            return;
        if (_fitPending)
            Fit();

        // Only the tiles on screen
        int x0 = Math.Max(0, (int)Math.Floor(-_origin.X / _zoom)), y0 = Math.Max(0, (int)Math.Floor(-_origin.Y / _zoom));
        int x1 = Math.Min(tiles.Width - 1, (int)Math.Floor((Bounds.Width - _origin.X) / _zoom));
        int y1 = Math.Min(tiles.Height - 1, (int)Math.Floor((Bounds.Height - _origin.Y) / _zoom));

        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    DrawTile(context, tiles, x, y);
        }

        if (ShowFlats) DrawPlaneValues(context, tiles, MapConstants.FLATPLANE, x0, y0, x1, y1);
        if (ShowHeights) DrawPlaneValues(context, tiles, MapConstants.HEIGHTPLANE, x0, y0, x1, y1);
        if (ShowTags) DrawPlaneValues(context, tiles, MapConstants.TAGPLANE, x0, y0, x1, y1);
        if (ShowZones) DrawPlaneValues(context, tiles, MapConstants.ZONEPLANE, x0, y0, x1, y1);

        if (ShowGrid && _zoom >= 8)
        {
            for (int x = x0; x <= x1 + 1; x++)
                context.DrawLine(GridPen, new Point(_origin.X + x * _zoom, _origin.Y + y0 * _zoom), new Point(_origin.X + x * _zoom, _origin.Y + (y1 + 1) * _zoom));
            for (int y = y0; y <= y1 + 1; y++)
                context.DrawLine(GridPen, new Point(_origin.X + x0 * _zoom, _origin.Y + y * _zoom), new Point(_origin.X + (x1 + 1) * _zoom, _origin.Y + y * _zoom));
        }

        context.DrawRectangle(null, MapEdgePen, new Rect(_origin, new Size(tiles.Width * _zoom, tiles.Height * _zoom)));
        if (_hover.X >= 0)
            context.DrawRectangle(null, HoverPen, TileRect(_hover.X, _hover.Y));
    }

    private Rect TileRect(int x, int y) => new(_origin.X + x * _zoom, _origin.Y + y * _zoom, _zoom, _zoom);

    private void DrawTile(DrawingContext context, MapTiles tiles, int x, int y)
    {
        var rect = TileRect(x, y);
        var kind = tiles.KindAt(x, y);

        if (!ShowWalls || kind == TileKind.Floor)
            context.FillRectangle(FloorBrush, rect);

        if (ShowWalls)
        {
            switch (kind)
            {
                case TileKind.Wall:
                    var wall = tiles.Wall(x, y)!;
                    if (tiles.Diagonal(x, y) is { } diagonal)
                    {
                        // The solid half in the wall's texture, the open half as floor
                        context.FillRectangle(FloorBrush, rect);
                        using (context.PushGeometryClip(SolidHalf(rect, diagonal.Shape)))
                            DrawTexture(context, wall.North, rect);
                    }
                    else
                    {
                        DrawTexture(context, wall.North, rect);
                        if (Buried(tiles, x, y))
                            context.FillRectangle(BuriedBrush, rect);
                    }
                    break;

                case TileKind.Door:
                    // A strip through the middle of its tile, along the way it slides
                    context.FillRectangle(FloorBrush, rect);
                    var door = tiles.Door(x, y)!;
                    var strip = door.Vertical
                        ? new Rect(rect.X + rect.Width * 0.3, rect.Y, rect.Width * 0.4, rect.Height)
                        : new Rect(rect.X, rect.Y + rect.Height * 0.3, rect.Width, rect.Height * 0.4);
                    DrawTexture(context, door.Vertical ? door.East : door.North, strip);
                    break;

                case TileKind.Unknown:
                    context.FillRectangle(UnknownBrush, rect);
                    break;
            }
        }

        if (!ShowThings)
            return;

        if (tiles.Thing(x, y) is { } thing)
        {
            if (Art?.ThingSprite(thing.Class) is { } sprite)
            {
                if (_zoom >= 6)
                    context.DrawImage(sprite, new Rect(sprite.Size), rect);
                else
                    context.DrawEllipse(ThingDotBrush, null, rect.Center, rect.Width * 0.2, rect.Height * 0.2);
            }
            else
            {
                // Nothing to see in the game (a patrol point): the way it points
                context.DrawGeometry(ThingDotBrush, null, Arrow(rect.Deflate(rect.Width * 0.2), thing.Angles));
            }
        }
        else if (tiles.PlayerStart(x, y) is { } start)
            context.DrawGeometry(StartBrush, null, Arrow(rect, start.Angles));
        else if (tiles.Trigger(x, y) is not null)
            context.DrawRectangle(null, TriggerPen, rect.Deflate(2));
    }

    /// <summary>
    /// A wall with walls (or the map's edge) all round, which no one in the level can see: drawn
    /// dimmed, so the level's shape shows through the solid mass around it
    /// </summary>
    private static bool Buried(MapTiles tiles, int x, int y)
    {
        bool Solid(int tx, int ty) => tx < 0 || ty < 0 || tx >= tiles.Width || ty >= tiles.Height || tiles.KindAt(tx, ty) == TileKind.Wall;
        return Solid(x - 1, y) && Solid(x + 1, y) && Solid(x, y - 1) && Solid(x, y + 1);
    }

    private void DrawTexture(DrawingContext context, string name, Rect rect)
    {
        if (Art?.Texture(name) is { } texture)
            context.DrawImage(texture, new Rect(texture.Size), rect);
        else
            context.FillRectangle(MissingTextureBrush, rect);
    }

    /// <summary>The wall's solid triangle: its solid corner and the two corners beside it</summary>
    private static StreamGeometry SolidHalf(Rect rect, WallShape shape)
    {
        var corners = shape switch
        {
            WallShape.SolidNW => new[] { rect.TopLeft, rect.TopRight, rect.BottomLeft },
            WallShape.SolidNE => new[] { rect.TopRight, rect.BottomRight, rect.TopLeft },
            WallShape.SolidSW => new[] { rect.BottomLeft, rect.TopLeft, rect.BottomRight },
            WallShape.SolidSE => new[] { rect.BottomRight, rect.BottomLeft, rect.TopRight },
            _ => new[] { rect.TopLeft, rect.TopRight, rect.BottomRight, rect.BottomLeft },
        };
        return Polygon(corners);
    }

    /// <summary>An arrow across the tile pointing the way the game's angle does (0 east, 90 north)</summary>
    private static StreamGeometry Arrow(Rect rect, int angles)
    {
        double radians = angles * Math.PI / 180, r = rect.Width * 0.4;
        Point At(double angle, double length) => new(rect.Center.X + Math.Cos(angle) * length, rect.Center.Y - Math.Sin(angle) * length);
        return Polygon([At(radians, r), At(radians + 2.5, r), At(radians + Math.PI, r * 0.3), At(radians - 2.5, r)]);
    }

    private static StreamGeometry Polygon(Point[] points)
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(points[0], true);
        foreach (var point in points.Skip(1))
            g.LineTo(point);
        g.EndFigure(true);
        return geometry;
    }

    /// <summary>A plane's non-zero values: each value its own color, with its number when there's room</summary>
    private void DrawPlaneValues(DrawingContext context, MapTiles tiles, int plane, int x0, int y0, int x1, int y1)
    {
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int value = tiles[plane, x, y];
                if (value == 0)
                    continue;

                var rect = TileRect(x, y);
                context.FillRectangle(ValueBrush(plane, value), rect);
                if (_zoom < 20)
                    continue;

                var text = new FormattedText(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, LabelFont, Math.Min(_zoom * 0.35, 16), Brushes.White);
                context.DrawText(text, new Point(rect.Center.X - text.Width / 2, rect.Center.Y - text.Height / 2));
            }
        }
    }

    private readonly Dictionary<(int, int), IBrush> _valueBrushes = [];

    private IBrush ValueBrush(int plane, int value)
    {
        if (!_valueBrushes.TryGetValue((plane, value), out var brush))
        {
            // Spread the hues so neighbouring values look different
            double hue = (value * 0.618034 + plane * 0.17) % 1.0;
            var color = HsvColor.ToRgb(hue * 360, 0.75, 0.9);
            _valueBrushes[(plane, value)] = brush = new SolidColorBrush(Color.FromArgb(110, color.R, color.G, color.B));
        }
        return brush;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _dragStart = e.GetPosition(this);
        _dragOrigin = _origin;
        e.Pointer.Capture(this);
        Focus();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragStart = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        if (_dragStart is { } start)
        {
            _origin = _dragOrigin + (position - start);
            InvalidateVisual();
        }

        UpdateHover(position);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_dragStart == null)
            SetHover(-1, -1);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var position = e.GetPosition(this);
        var zoom = Math.Clamp(_zoom * Math.Pow(1.2, e.Delta.Y), MinZoom, MaxZoom);

        // Keep the point under the pointer where it is
        _origin = position - (position - _origin) * (zoom / _zoom);
        _zoom = zoom;
        InvalidateVisual();
        UpdateHover(position);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Home)
        {
            FitToView();
            e.Handled = true;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.PreviousSize.Width <= 0)
            _fitPending = true;
    }

    private void UpdateHover(Point position)
    {
        if (Tiles is not { } tiles)
            return;

        int x = (int)Math.Floor((position.X - _origin.X) / _zoom), y = (int)Math.Floor((position.Y - _origin.Y) / _zoom);
        if (x < 0 || y < 0 || x >= tiles.Width || y >= tiles.Height)
            x = y = -1;
        SetHover(x, y);
    }

    private void SetHover(int x, int y)
    {
        if (_hover == (x, y))
            return;
        _hover = (x, y);
        InvalidateVisual();
        TileHovered?.Invoke(this, new TileEventArgs(x, y));
    }
}
