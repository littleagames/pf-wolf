using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PFWolf.Constants;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Enums;
using Colors = Avalonia.Media.Colors;

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
/// other planes (flats, heights, tags, light zones) as numbered overlays. The left button works
/// the tools (through the <see cref="Controller"/>); the wheel zooms around the pointer, and
/// dragging with the middle or right button pans.
/// </summary>
public sealed class MapCanvas : Control
{
    public static readonly StyledProperty<MapTiles?> TilesProperty = AvaloniaProperty.Register<MapCanvas, MapTiles?>(nameof(Tiles));
    public static readonly StyledProperty<ToolController?> ControllerProperty = AvaloniaProperty.Register<MapCanvas, ToolController?>(nameof(Controller));
    public static readonly StyledProperty<ArtCache?> ArtProperty = AvaloniaProperty.Register<MapCanvas, ArtCache?>(nameof(Art));
    public static readonly StyledProperty<bool> ShowWallsProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowWalls), true);
    public static readonly StyledProperty<bool> ShowThingsProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowThings), true);
    public static readonly StyledProperty<bool> ShowFlatsProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowFlats));
    public static readonly StyledProperty<bool> ShowHeightsProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowHeights));
    public static readonly StyledProperty<bool> ShowTagsProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowTags));
    public static readonly StyledProperty<bool> ShowZonesProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowZones));
    public static readonly StyledProperty<bool> ShowGridProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowGrid), true);
    public static readonly StyledProperty<Camera3D?> CameraProperty = AvaloniaProperty.Register<MapCanvas, Camera3D?>(nameof(Camera));
    public static readonly StyledProperty<bool> ShowCameraProperty = AvaloniaProperty.Register<MapCanvas, bool>(nameof(ShowCamera));

    static MapCanvas()
    {
        AffectsRender<MapCanvas>(TilesProperty, ArtProperty, ShowWallsProperty, ShowThingsProperty, ShowFlatsProperty,
            ShowHeightsProperty, ShowTagsProperty, ShowZonesProperty, ShowGridProperty, ShowCameraProperty);
        FocusableProperty.OverrideDefaultValue<MapCanvas>(true);
        ClipToBoundsProperty.OverrideDefaultValue<MapCanvas>(true);
    }

    public MapTiles? Tiles { get => GetValue(TilesProperty); set => SetValue(TilesProperty, value); }
    public ToolController? Controller { get => GetValue(ControllerProperty); set => SetValue(ControllerProperty, value); }
    public ArtCache? Art { get => GetValue(ArtProperty); set => SetValue(ArtProperty, value); }
    public bool ShowWalls { get => GetValue(ShowWallsProperty); set => SetValue(ShowWallsProperty, value); }
    public bool ShowThings { get => GetValue(ShowThingsProperty); set => SetValue(ShowThingsProperty, value); }
    public bool ShowFlats { get => GetValue(ShowFlatsProperty); set => SetValue(ShowFlatsProperty, value); }
    public bool ShowHeights { get => GetValue(ShowHeightsProperty); set => SetValue(ShowHeightsProperty, value); }
    public bool ShowTags { get => GetValue(ShowTagsProperty); set => SetValue(ShowTagsProperty, value); }
    public bool ShowZones { get => GetValue(ShowZonesProperty); set => SetValue(ShowZonesProperty, value); }
    public bool ShowGrid { get => GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }

    /// <summary>The 3D view's camera, drawn on the map while <see cref="ShowCamera"/> is set; V puts it on the tile under the pointer</summary>
    public Camera3D? Camera { get => GetValue(CameraProperty); set => SetValue(CameraProperty, value); }
    public bool ShowCamera { get => GetValue(ShowCameraProperty); set => SetValue(ShowCameraProperty, value); }

    /// <summary>The pointer moved onto another tile, or off the map</summary>
    public event EventHandler<TileEventArgs>? TileHovered;

    /// <summary>A tool's key letter was pressed over the map</summary>
    public event EventHandler<string>? ToolKeyPressed;

    private const double MinZoom = 2, MaxZoom = 128;

    // Screen pixels per tile, and where the map's top left corner is on screen
    private double _zoom = 12;
    private Point _origin;
    private bool _fitPending = true;

    // Panning with the middle or right button
    private Point? _dragStart;
    private Point _dragOrigin;
    // The left button is down, working a tool
    private bool _toolDown;
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
    private static readonly IPen SelectionPen = new Pen(new SolidColorBrush(Color.FromRgb(80, 200, 255)), 2, new DashStyle([3, 2], 0));
    private static readonly IBrush SelectionBrush = new SolidColorBrush(Color.FromArgb(40, 80, 200, 255));
    private static readonly IBrush FacingBrush = new SolidColorBrush(Color.FromRgb(255, 90, 60));
    private static readonly IBrush PatrolBrush = new SolidColorBrush(Color.FromRgb(80, 220, 255));
    private static readonly IBrush SkillBadgeBrush = new SolidColorBrush(Color.FromArgb(200, 120, 60, 160));
    private static readonly IBrush AmbushBadgeBrush = new SolidColorBrush(Color.FromArgb(210, 180, 40, 40));
    private static readonly IBrush ZoneBadgeBrush = new SolidColorBrush(Color.FromArgb(170, 30, 30, 30));
    private static readonly IPen WallSpritePen = new Pen(new SolidColorBrush(Color.FromRgb(196, 150, 90)), 3);
    private static readonly IPen WallSpriteTickPen = new Pen(new SolidColorBrush(Color.FromRgb(196, 150, 90)), 2);
    private static readonly IPen SwitchLinkPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 220, 60)), 2);
    private static readonly IPen TagHighlightPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 220, 60)), 2);
    private static readonly Typeface LabelFont = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TilesProperty)
        {
            _fitPending = true;
            SetHover(-1, -1);
        }
        else if (change.Property == ControllerProperty)
        {
            if (change.OldValue is ToolController old)
                old.Changed -= OnControllerChanged;
            if (change.NewValue is ToolController controller)
                controller.Changed += OnControllerChanged;
        }
        else if (change.Property == CameraProperty)
        {
            if (change.OldValue is Camera3D old)
                old.Changed -= OnCameraChanged;
            if (change.NewValue is Camera3D camera)
                camera.Changed += OnCameraChanged;
        }
    }

    private void OnCameraChanged(object? sender, EventArgs e)
    {
        if (ShowCamera)
            InvalidateVisual();
    }

    private void OnControllerChanged(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>Scrolls a tile to the middle, zooming in if the level's too small to make it out</summary>
    public void CenterOn(int x, int y)
    {
        if (_fitPending)
            Fit();
        _zoom = Math.Max(_zoom, 24);
        _origin = new Point(Bounds.Width / 2 - (x + 0.5) * _zoom, Bounds.Height / 2 - (y + 0.5) * _zoom);
        InvalidateVisual();
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

        // Flats are drawn in place of the floor (DrawTile); here only their numbers
        if (ShowFlats) DrawPlaneValues(context, tiles, MapConstants.FLATPLANE, x0, y0, x1, y1, fill: false);
        if (ShowHeights) DrawPlaneValues(context, tiles, MapConstants.HEIGHTPLANE, x0, y0, x1, y1);
        if (ShowZones) DrawZones(context, tiles, x0, y0, x1, y1);
        if (ShowTags)
        {
            DrawPlaneValues(context, tiles, MapConstants.TAGPLANE, x0, y0, x1, y1);
            DrawSwitchLinks(context, tiles);
        }

        // Everything that shares the tag of the tile under the pointer
        if (_hover.X >= 0 && tiles[MapConstants.TAGPLANE, _hover.X, _hover.Y] is var hoverTag and not 0
            && tiles.TaggedTiles().TryGetValue(hoverTag, out var sameTag))
        {
            foreach (var (tx, ty) in sameTag)
                context.DrawRectangle(null, TagHighlightPen, TileRect(tx, ty).Deflate(1.5));
        }

        if (ShowGrid && _zoom >= 8)
        {
            for (int x = x0; x <= x1 + 1; x++)
                context.DrawLine(GridPen, new Point(_origin.X + x * _zoom, _origin.Y + y0 * _zoom), new Point(_origin.X + x * _zoom, _origin.Y + (y1 + 1) * _zoom));
            for (int y = y0; y <= y1 + 1; y++)
                context.DrawLine(GridPen, new Point(_origin.X + x0 * _zoom, _origin.Y + y * _zoom), new Point(_origin.X + (x1 + 1) * _zoom, _origin.Y + y * _zoom));
        }

        context.DrawRectangle(null, MapEdgePen, new Rect(_origin, new Size(tiles.Width * _zoom, tiles.Height * _zoom)));
        if (Controller?.Selection is { } selection)
        {
            var rect = new Rect(_origin.X + selection.Left * _zoom, _origin.Y + selection.Top * _zoom, selection.Width * _zoom, selection.Height * _zoom);
            context.DrawRectangle(SelectionBrush, SelectionPen, rect);
        }
        if (_hover.X >= 0)
            context.DrawRectangle(null, HoverPen, TileRect(_hover.X, _hover.Y));
        if (ShowCamera && Camera is { } camera)
            DrawCamera(context, camera);
    }

    private static readonly IBrush CameraBrush = new SolidColorBrush(Color.FromRgb(255, 120, 200));
    private static readonly IPen CameraPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 120, 200)), 1.5);

    /// <summary>The 3D view's eye: a dot, and the edges of what it sees along the ground</summary>
    private void DrawCamera(DrawingContext context, Camera3D camera)
    {
        var eye = new Point(_origin.X + camera.Position.X * _zoom, _origin.Y + camera.Position.Z * _zoom);
        double reach = Math.Max(_zoom * 3, 30);

        // The view is wider than it is tall; about the editor's usual 3D pane
        double half = Math.Atan(Math.Tan(Camera3D.FieldOfViewY * Math.PI / 360) * 1.4);
        double yaw = camera.Yaw * Math.PI / 180;
        Point Toward(double angle) => new(eye.X + Math.Cos(angle) * reach, eye.Y - Math.Sin(angle) * reach);

        context.DrawLine(CameraPen, eye, Toward(yaw + half));
        context.DrawLine(CameraPen, eye, Toward(yaw - half));
        context.DrawLine(CameraPen, Toward(yaw + half), Toward(yaw - half));
        context.DrawEllipse(CameraBrush, null, eye, 5, 5);
    }

    private Rect TileRect(int x, int y) => new(_origin.X + x * _zoom, _origin.Y + y * _zoom, _zoom, _zoom);

    private void DrawTile(DrawingContext context, MapTiles tiles, int x, int y)
    {
        var rect = TileRect(x, y);
        var kind = tiles.KindAt(x, y);

        if (!ShowWalls || kind == TileKind.Floor)
        {
            // With flats showing, the floor's own texture
            if (ShowFlats && kind == TileKind.Floor && tiles.FloorFlat(x, y) is { } flat && Art?.Texture(flat) is { } flatTexture)
                context.DrawImage(flatTexture, new Rect(flatTexture.Size), rect);
            else
                context.FillRectangle(FloorBrush, rect);
        }

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
            var content = tiles.Content;
            if (content.IsWallSprite(thing.Class))
                DrawWallSprite(context, rect, thing.Angles, content.WallSpriteOffset(thing.Class));
            else if (Art?.ThingSprite(thing.Class) is { } sprite)
            {
                if (_zoom >= 6)
                    context.DrawImage(sprite, new Rect(sprite.Size), rect);
                else
                    context.DrawEllipse(ThingDotBrush, null, rect.Center, rect.Width * 0.2, rect.Height * 0.2);

                // Which way an enemy faces, and whether it patrols
                if (_zoom >= 10 && content.IsRotating(thing.Class))
                    context.DrawGeometry(thing.Patrol != 0 ? PatrolBrush : FacingBrush, null, FacingMark(rect, thing.Angles));
            }
            else
            {
                // Nothing to see in the game (a patrol point): the way it points
                context.DrawGeometry(ThingDotBrush, null, Arrow(rect.Deflate(rect.Width * 0.2), thing.Angles));
            }

            // The skills it's on, when it's not on all of them
            if (thing.MinSkill > 0 && _zoom >= 16)
                DrawBadge(context, rect, (thing.MinSkill + 1).ToString(CultureInfo.InvariantCulture) + "+", SkillBadgeBrush, bottom: true);
        }
        else if (tiles.PlayerStart(x, y) is { } start)
            context.DrawGeometry(StartBrush, null, Arrow(rect, start.Angles));
        else if (tiles.Trigger(x, y) is not null)
            context.DrawRectangle(null, TriggerPen, rect.Deflate(2));

        // An ambush floor: whoever stands on it waits deaf until they see the player
        if (ShowWalls && _zoom >= 12 && tiles.IsAmbush(x, y))
            DrawBadge(context, rect, "A", AmbushBadgeBrush, bottom: false);
    }

    /// <summary>A small labelled box in a corner of the tile</summary>
    private void DrawBadge(DrawingContext context, Rect rect, string label, IBrush background, bool bottom)
    {
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFont,
            Math.Clamp(_zoom * 0.28, 8, 13), Brushes.White);
        var box = new Rect(bottom ? rect.Right - text.Width - 4 : rect.X + 1, bottom ? rect.Bottom - text.Height - 1 : rect.Y + 1,
            text.Width + 3, text.Height);
        context.FillRectangle(background, box);
        context.DrawText(text, new Point(box.X + 1.5, box.Y));
    }

    /// <summary>A notch on the tile's edge the way the thing faces (0 east, 90 north)</summary>
    private static StreamGeometry FacingMark(Rect rect, int angles)
    {
        double radians = angles * Math.PI / 180, r = rect.Width / 2;
        Point At(double angle, double length) => new(rect.Center.X + Math.Cos(angle) * length, rect.Center.Y - Math.Sin(angle) * length);
        return Polygon([At(radians, r), At(radians + 0.45, r * 0.62), At(radians - 0.45, r * 0.62)]);
    }

    /// <summary>
    /// A wall sprite's panel: across the tile, at right angles to the way it faces, moved
    /// toward its front by its offset (32 texels is the tile's edge), with a tick on its front
    /// </summary>
    private void DrawWallSprite(DrawingContext context, Rect rect, int angles, int offset)
    {
        double radians = angles * Math.PI / 180;
        var front = new Vector(Math.Cos(radians), -Math.Sin(radians));
        var along = new Vector(-front.Y, front.X);
        var middle = rect.Center + front * (offset / 64.0 * rect.Width);
        double half = rect.Width * (angles % 90 == 0 ? 0.5 : 0.7071);

        using (context.PushClip(rect))
        {
            context.DrawLine(WallSpritePen, middle - along * half, middle + along * half);
            context.DrawLine(WallSpriteTickPen, middle, middle + front * rect.Width * 0.18);
        }
    }

    /// <summary>
    /// The light zones: each tile dimmed to its zone's light and tinted with its color, with the
    /// zone's number when there's room
    /// </summary>
    private void DrawZones(DrawingContext context, MapTiles tiles, int x0, int y0, int x1, int y1)
    {
        var zones = tiles.Zones;
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int id = tiles[MapConstants.ZONEPLANE, x, y];
                if (id == 0)
                    continue;

                var rect = TileRect(x, y);
                context.FillRectangle(ZoneBrush(zones.GetValueOrDefault(id)), rect);
                if (_zoom >= 40)
                    DrawBadge(context, rect, $"z{id}", ZoneBadgeBrush, bottom: false);
            }
        }
    }

    private readonly Dictionary<ZoneProperties, IBrush> _zoneBrushes = [];
    private static readonly IBrush UndefinedZoneBrush = new SolidColorBrush(Color.FromArgb(90, 255, 0, 255));

    private IBrush ZoneBrush(ZoneProperties? zone)
    {
        if (zone == null)
            return UndefinedZoneBrush;     // a zone game-info doesn't define: lit as the level
        if (_zoneBrushes.TryGetValue(zone, out var brush))
            return brush;

        // Dark as the zone is dark, in its tint
        double light = Math.Clamp(zone.Light ?? 255, 0, 255) / 255.0;
        var tint = TryParseColor(zone.Color) ?? Colors.White;
        byte alpha = (byte)Math.Clamp(40 + (1 - light) * 180, 0, 230);
        var color = Color.FromArgb(alpha, (byte)(tint.R * light), (byte)(tint.G * light), (byte)(tint.B * light));
        return _zoneBrushes[zone] = new SolidColorBrush(color);
    }

    private static Color? TryParseColor(string? text)
        => !string.IsNullOrWhiteSpace(text) && Color.TryParse(text.Trim(), out var color) ? color : null;

    /// <summary>A line from each switch to everything that shares its tag: what it sets off</summary>
    private void DrawSwitchLinks(DrawingContext context, MapTiles tiles)
    {
        var tagged = tiles.TaggedTiles();
        foreach (var (tag, tagTiles) in tagged)
        {
            foreach (var (sx, sy) in tagTiles.Where(tile => tiles.IsSwitch(tile.X, tile.Y)))
            {
                var from = TileRect(sx, sy).Center;
                foreach (var (tx, ty) in tagTiles)
                {
                    if ((tx, ty) == (sx, sy))
                        continue;
                    var to = TileRect(tx, ty).Center;
                    context.DrawLine(SwitchLinkPen, from, to);
                    context.DrawEllipse(SwitchLinkPen.Brush, null, to, 3, 3);
                }
            }
        }
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
    private void DrawPlaneValues(DrawingContext context, MapTiles tiles, int plane, int x0, int y0, int x1, int y1, bool fill = true)
    {
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int value = tiles[plane, x, y];
                if (value == 0)
                    continue;

                var rect = TileRect(x, y);
                if (fill)
                    context.FillRectangle(ValueBrush(plane, value), rect);
                if (_zoom < 20)
                    continue;

                // Flats read as floor/ceiling indices
                var label = plane == MapConstants.FLATPLANE ? $"{value & 0xff}/{value >> 8}" : value.ToString(CultureInfo.InvariantCulture);
                var text = new FormattedText(label, CultureInfo.InvariantCulture,
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
        Focus();
        var point = e.GetCurrentPoint(this);
        var position = point.Position;

        if (point.Properties.IsLeftButtonPressed && Controller != null && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            var (x, y) = TileAt(position);
            _toolDown = true;
            e.Pointer.Capture(this);
            Controller.Press(x, y);
        }
        else
        {
            // Middle or right button (or Alt with the left): pan
            _dragStart = position;
            _dragOrigin = _origin;
            e.Pointer.Capture(this);
        }
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_toolDown)
        {
            _toolDown = false;
            Controller?.Release();
        }
        _dragStart = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_toolDown)
        {
            _toolDown = false;
            Controller?.Release();
        }
        _dragStart = null;
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
        if (Controller != null && (_toolDown || Controller.IsPasting))
        {
            var (x, y) = TileAt(position);
            Controller.Move(x, y);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_dragStart == null && !_toolDown)
            SetHover(-1, -1);
    }

    /// <summary>The tile under a point, which may be off the level</summary>
    private (int X, int Y) TileAt(Point position)
        => ((int)Math.Floor((position.X - _origin.X) / _zoom), (int)Math.Floor((position.Y - _origin.Y) / _zoom));

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
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        e.Handled = true;
        switch (e.Key)
        {
            case Key.Home:
                FitToView();
                break;
            case Key.C when ctrl:
                Controller?.Copy();
                break;
            case Key.X when ctrl:
                Controller?.Cut();
                break;
            case Key.V when ctrl:
                Controller?.StartPaste(Math.Max(_hover.X, 0), Math.Max(_hover.Y, 0));
                break;
            case Key.A when ctrl:
                Controller?.SelectAll();
                break;
            case Key.Delete:
                Controller?.Delete();
                break;
            case Key.Escape:
                Controller?.Escape();
                break;
            case Key.V when e.KeyModifiers == KeyModifiers.None && ShowCamera && _hover.X >= 0:
                // The 3D view's camera onto the tile, still facing the same way
                Camera?.PlaceOn(_hover.X, _hover.Y, Camera.Yaw);
                break;
            case >= Key.A and <= Key.Z when e.KeyModifiers == KeyModifiers.None:
                ToolKeyPressed?.Invoke(this, e.Key.ToString());
                break;
            default:
                e.Handled = false;
                break;
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
