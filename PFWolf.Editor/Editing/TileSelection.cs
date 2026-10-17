namespace PFWolf.Editor.Editing;

/// <summary>How a new selection goes with the one there already</summary>
public enum SelectMode
{
    /// <summary>It takes the old one's place</summary>
    Replace,

    /// <summary>It's added to the old one (Shift)</summary>
    Add,

    /// <summary>It's taken out of the old one (Ctrl)</summary>
    Subtract,
}

/// <summary>
/// The tiles selected: a rectangle, or any shape inside one (rectangles added and taken away,
/// the magic wand's areas), kept as which tiles of its bounds are in it
/// </summary>
public sealed class TileSelection
{
    // Over Bounds, row by row; null when every tile of it is selected
    private readonly bool[]? _mask;

    public TileSelection(TileRect bounds, bool[]? mask = null)
    {
        Bounds = bounds;
        _mask = mask != null && Array.TrueForAll(mask, selected => selected) ? null : mask;
    }

    public static TileSelection Rect(TileRect rect) => new(rect);

    /// <summary>Tiles in any shape; null for none</summary>
    public static TileSelection? FromTiles(IEnumerable<(int X, int Y)> tiles)
    {
        var list = tiles.ToList();
        if (list.Count == 0)
            return null;
        var bounds = new TileRect(list.Min(t => t.X), list.Min(t => t.Y), list.Max(t => t.X), list.Max(t => t.Y));
        var mask = new bool[bounds.Width * bounds.Height];
        foreach (var (x, y) in list)
            mask[(y - bounds.Top) * bounds.Width + x - bounds.Left] = true;
        return new TileSelection(bounds, mask);
    }

    public TileRect Bounds { get; }

    public int Left => Bounds.Left;
    public int Top => Bounds.Top;
    public int Right => Bounds.Right;
    public int Bottom => Bounds.Bottom;
    public int Width => Bounds.Width;
    public int Height => Bounds.Height;

    /// <summary>Every tile of its bounds is in it</summary>
    public bool IsRectangle => _mask == null;

    /// <summary>Which tiles of its bounds are in it, row by row; null: all of them</summary>
    public bool[]? Mask => _mask;

    public bool Contains(int x, int y)
        => Bounds.Contains(x, y) && (_mask == null || _mask[(y - Top) * Width + x - Left]);

    public IEnumerable<(int X, int Y)> Tiles
        => TileShapes.Rectangle(Bounds, outline: false).Where(tile => Contains(tile.X, tile.Y));

    public TileSelection Offset(int dx, int dy) => new(Bounds.Offset(dx, dy), _mask);

    /// <summary>This and another together (Add), or this without the other's tiles (Subtract); null when nothing's left</summary>
    public TileSelection? Combine(TileSelection other, SelectMode mode) => mode switch
    {
        SelectMode.Add => FromTiles(Tiles.Concat(other.Tiles).Distinct()),
        SelectMode.Subtract => FromTiles(Tiles.Where(tile => !other.Contains(tile.X, tile.Y))),
        _ => other,
    };

    /// <summary>The part of it on the level; null when none is</summary>
    public TileSelection? Clip(int width, int height)
    {
        var level = new TileRect(0, 0, width - 1, height - 1);
        if (Bounds.Intersect(level) is not { } inside)
            return null;
        if (inside == Bounds)
            return this;
        return _mask == null ? new TileSelection(inside) : FromTiles(Tiles.Where(tile => inside.Contains(tile.X, tile.Y)));
    }

    public override bool Equals(object? obj)
        => obj is TileSelection other && other.Bounds == Bounds
           && (_mask == null ? other._mask == null : other._mask != null && _mask.AsSpan().SequenceEqual(other._mask));

    public override int GetHashCode() => Bounds.GetHashCode();
}
