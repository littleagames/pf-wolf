using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PFWolf.Assets.Demos;
using PFWolf.Editor.Data;

namespace PFWolf.Editor.Rendering;

using Point = Avalonia.Point;

/// <summary>
/// A traced demo over its level (the demo browser's map): at the frame shown, the tiles and doors
/// as they were, pushwalls on the move, every actor as its sprite then, the player's path (the
/// part walked brighter), the events as markers and the player as an arrow.
/// </summary>
public sealed class DemoOverlay : IMapOverlay
{
    public static readonly IBrush KillBrush = new SolidColorBrush(Color.FromRgb(0xe0, 0x40, 0x40));
    public static readonly IBrush PickupBrush = new SolidColorBrush(Color.FromRgb(0xf0, 0xc8, 0x40));
    public static readonly IBrush HurtBrush = new SolidColorBrush(Color.FromRgb(0xf0, 0x80, 0x30));
    public static readonly IBrush DoorBrush = new SolidColorBrush(Color.FromRgb(0x50, 0xa8, 0xf0));
    public static readonly IBrush PushWallBrush = new SolidColorBrush(Color.FromRgb(0xb0, 0x70, 0xe0));
    public static readonly IBrush DeathBrush = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));

    private static readonly IBrush FloorBrush = new SolidColorBrush(Color.FromRgb(56, 56, 60));
    private static readonly IBrush WallBrush = new SolidColorBrush(Color.FromRgb(120, 120, 128));
    private static readonly IBrush PlayerBrush = new SolidColorBrush(Color.FromRgb(64, 220, 96));
    private static readonly IBrush MarkerBack = new SolidColorBrush(Color.FromArgb(200, 20, 20, 24));
    private static readonly IPen PlayerPen = new Pen(Brushes.Black, 1.5);
    private static readonly IPen WalkedPen = new Pen(new SolidColorBrush(Color.FromArgb(230, 64, 220, 96)), 2, lineJoin: PenLineJoin.Round);
    private static readonly IPen AheadPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 64, 220, 96)), 1.5, new DashStyle([3, 3], 0), lineJoin: PenLineJoin.Round);
    private static readonly IPen DeadPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 224, 64, 64)), 1.5);
    private static readonly Typeface MarkerFont = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

    private DemoTrace? _trace;
    private int _frame;
    private bool _showPath = true, _showGhosts = true, _showEvents = true;

    public event EventHandler? Changed;

    public DemoTrace? Trace { get => _trace; set { _trace = value; Changed?.Invoke(this, EventArgs.Empty); } }
    public int Frame { get => _frame; set { _frame = value; Changed?.Invoke(this, EventArgs.Empty); } }
    public bool ShowPath { get => _showPath; set { _showPath = value; Changed?.Invoke(this, EventArgs.Empty); } }
    public bool ShowGhosts { get => _showGhosts; set { _showGhosts = value; Changed?.Invoke(this, EventArgs.Empty); } }
    public bool ShowEvents { get => _showEvents; set { _showEvents = value; Changed?.Invoke(this, EventArgs.Empty); } }

    /// <summary>The pictures and the level the trace is drawn over</summary>
    public ArtCache? Art { get; set; }
    public MapTiles? Tiles { get; set; }

    public void Render(DrawingContext context, Func<double, double, Point> toScreen, double zoom)
    {
        if (_trace is not { FrameCount: > 0 } trace)
            return;
        int frame = Math.Clamp(_frame, 0, trace.FrameCount - 1);
        Rect TileRect(double x, double y) => new(toScreen(x, y), new Size(zoom, zoom));

        if (_showGhosts)
        {
            DrawTiles(context, trace, frame, TileRect);
            DrawDoors(context, trace, frame, TileRect, zoom);
            DrawPushWalls(context, trace, frame, TileRect);
            DrawActors(context, trace, frame, toScreen, zoom);
        }
        if (_showPath)
            DrawPath(context, trace, frame, toScreen);
        if (_showEvents)
            DrawEvents(context, trace, frame, toScreen, zoom);
        DrawPlayer(context, trace.Player[frame], toScreen, zoom);
    }

    // Walls gone (pushed, switched away) as floor, walls come as their texture
    private void DrawTiles(DrawingContext context, DemoTrace trace, int frame, Func<double, double, Rect> tileRect)
    {
        foreach (var ((x, y), changes) in trace.Tiles)
        {
            if (DemoTrace.At(changes, frame) is not { } tile)
                continue;
            var rect = tileRect(x, y);
            // A moving pushwall's two tiles are walls with no id: the wall itself is drawn sliding
            if (tile.Kind == TraceTileKind.Floor || tile.Kind == TraceTileKind.Wall && tile.WallId == 0)
                context.FillRectangle(FloorBrush, rect);
            else if (tile.Kind == TraceTileKind.Wall)
                DrawWall(context, WallTexture(tile.WallId), rect);
        }
    }

    private string? WallTexture(int id) => Tiles?.Defs.Walls.TryGetValue(id, out var wall) == true ? wall.North : null;

    private void DrawWall(DrawingContext context, string? texture, Rect rect)
    {
        if (texture != null && Art?.Texture(texture) is { } bitmap)
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                context.DrawImage(bitmap, new Rect(bitmap.Size), rect);
        else
            context.FillRectangle(WallBrush, rect);
    }

    // The open part of each door, as floor: a door slides along its length (a vertical one down the tile)
    private static void DrawDoors(DrawingContext context, DemoTrace trace, int frame, Func<double, double, Rect> tileRect, double zoom)
    {
        foreach (var (door, positions) in trace.Doors)
        {
            if (DemoTrace.At(positions, frame) is not { } position || position == 0)
                continue;
            var rect = tileRect(door.X, door.Y);
            double open = position / 65535.0 * zoom;
            context.FillRectangle(FloorBrush, door.Vertical
                ? new Rect(rect.X, rect.Y, rect.Width, open)
                : new Rect(rect.X, rect.Y, open, rect.Height));
        }
    }

    private static readonly (int X, int Y)[] Directions = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    // A pushwall on the move, partway into the tile ahead, in the texture of the wall it was
    private void DrawPushWalls(DrawingContext context, DemoTrace trace, int frame, Func<double, double, Rect> tileRect)
    {
        if (!trace.PushWalls.TryGetValue(frame, out var walls))
            return;
        foreach (var wall in walls)
        {
            var (dx, dy) = Directions[Math.Clamp(wall.Dir, 0, 3)];
            double along = wall.Pos / 64.0;
            DrawWall(context, PushWallTexture(trace, wall), tileRect(wall.X + dx * along, wall.Y + dy * along));
        }
    }

    // The wall a pushwall started as: follow it back to the tile it started from in the level
    private string? PushWallTexture(DemoTrace trace, TracePushWall wall)
    {
        var (dx, dy) = Directions[Math.Clamp(wall.Dir, 0, 3)];
        for (int back = 0; back < 64; back++)
        {
            int x = wall.X - dx * back, y = wall.Y - dy * back;
            if (Tiles == null || x < 0 || y < 0 || x >= Tiles.Width || y >= Tiles.Height)
                break;
            if (Tiles.Wall(x, y) is { } original)
                return original.North;
        }
        return null;
    }

    private readonly Dictionary<string, Bitmap?> _sprites = [];

    // A frame's picture from the front (view 1), else its single view (0)
    private Bitmap? Sprite(string sprite)
    {
        if (Art == null || sprite == "-")
            return null;
        if (!_sprites.TryGetValue(sprite, out var bitmap))
            _sprites[sprite] = bitmap = Art.Whole(ArtKind.Sprite, sprite + "1") ?? Art.Whole(ArtKind.Sprite, sprite + "0");
        return bitmap;
    }

    /// <summary>The pictures came from another ArtCache: drop the ones kept</summary>
    public void ForgetPictures() => _sprites.Clear();

    private void DrawActors(DrawingContext context, DemoTrace trace, int frame, Func<double, double, Point> toScreen, double zoom)
    {
        using var _ = context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None });
        foreach (var (_, _, state) in trace.ActorsAt(frame))
        {
            var centre = toScreen(state.X, state.Y);
            if (Sprite(state.Sprite) is { } bitmap)
            {
                // A sprite's 64x64 cell fills a tile
                double scale = zoom / 64;
                var size = new Size(bitmap.Size.Width * scale, bitmap.Size.Height * scale);
                context.DrawImage(bitmap, new Rect(bitmap.Size), new Rect(centre.X - size.Width / 2, centre.Y - size.Height / 2, size.Width, size.Height));
            }
            else
                context.DrawEllipse(WallBrush, null, centre, zoom * 0.2, zoom * 0.2);
        }
    }

    // Where the player has been (bright) and is going (dashed), split at the frame shown
    private static void DrawPath(DrawingContext context, DemoTrace trace, int frame, Func<double, double, Point> toScreen)
    {
        Geometry Line(int from, int to)
        {
            var geometry = new StreamGeometry();
            using var g = geometry.Open();
            g.BeginFigure(toScreen(trace.Player[from].X, trace.Player[from].Y), false);
            for (int i = from + 1; i <= to; i++)
                g.LineTo(toScreen(trace.Player[i].X, trace.Player[i].Y));
            g.EndFigure(false);
            return geometry;
        }

        if (frame < trace.FrameCount - 1)
            context.DrawGeometry(null, AheadPen, Line(frame, trace.FrameCount - 1));
        if (frame > 0)
            context.DrawGeometry(null, WalkedPen, Line(0, frame));
    }

    private static string Letter(TraceEventKind kind) => kind switch
    {
        TraceEventKind.Kill => "K",
        TraceEventKind.Pickup => "+",
        TraceEventKind.Hurt => "!",
        TraceEventKind.Door => "D",
        TraceEventKind.PushWall => "P",
        TraceEventKind.Death => "X",
        _ => "E",
    };

    // A marker for each event: those still to come fainter
    private static void DrawEvents(DrawingContext context, DemoTrace trace, int frame, Func<double, double, Point> toScreen, double zoom)
    {
        double radius = Math.Clamp(zoom * 0.28, 5, 11);
        foreach (var e in trace.Events)
        {
            // Doors and pushwalls show by themselves; a marker only once they've happened
            if (e.Kind is TraceEventKind.Door or TraceEventKind.PushWall && e.Frame > frame)
                continue;
            var at = toScreen(e.X, e.Y);
            using var opacity = context.PushOpacity(e.Frame <= frame ? 1 : 0.35);
            var brush = DemoTimeline.EventBrush(e.Kind);
            context.DrawEllipse(MarkerBack, new Pen(brush, 1.5), at, radius, radius);
            if (radius >= 7)
            {
                var text = new FormattedText(Letter(e.Kind), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, MarkerFont, radius * 1.2, brush);
                context.DrawText(text, new Point(at.X - text.Width / 2, at.Y - text.Height / 2));
            }
        }
    }

    private static void DrawPlayer(DrawingContext context, TracePlayer player, Func<double, double, Point> toScreen, double zoom)
    {
        var centre = toScreen(player.X, player.Y);
        double size = Math.Clamp(zoom * 0.45, 7, 18);
        double angle = player.Angle * Math.PI / 180;
        Point At(double turn, double length) => new(centre.X + Math.Cos(angle + turn) * length, centre.Y - Math.Sin(angle + turn) * length);

        var arrow = new StreamGeometry();
        using (var g = arrow.Open())
        {
            g.BeginFigure(At(0, size), true);
            g.LineTo(At(2.5, size * 0.75));
            g.LineTo(At(Math.PI, size * 0.3));
            g.LineTo(At(-2.5, size * 0.75));
            g.EndFigure(true);
        }
        context.DrawGeometry(player.Health > 0 ? PlayerBrush : DeathBrush, player.Health > 0 ? PlayerPen : DeadPen, arrow);
    }
}
