namespace PFWolf.Editor.Editing;

/// <summary>A rectangle of tiles, corners included, whichever way it was dragged</summary>
public readonly record struct TileRect(int Left, int Top, int Right, int Bottom)
{
    public static TileRect FromCorners(int x0, int y0, int x1, int y1)
        => new(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));

    public int Width => Right - Left + 1;
    public int Height => Bottom - Top + 1;

    public bool Contains(int x, int y) => x >= Left && x <= Right && y >= Top && y <= Bottom;

    public TileRect Offset(int dx, int dy) => new(Left + dx, Top + dy, Right + dx, Bottom + dy);
}

/// <summary>The tiles the line, box and fill tools cover</summary>
public static class TileShapes
{
    /// <summary>The tiles on a line between two tiles, both ends included (Bresenham)</summary>
    public static IEnumerable<(int X, int Y)> Line(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            yield return (x0, y0);
            if (x0 == x1 && y0 == y1)
                yield break;

            int e2 = 2 * error;
            if (e2 >= dy)
            {
                error += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    /// <summary>Every tile of a rectangle, or only its edge</summary>
    public static IEnumerable<(int X, int Y)> Rectangle(TileRect rect, bool outline)
    {
        for (int y = rect.Top; y <= rect.Bottom; y++)
        {
            for (int x = rect.Left; x <= rect.Right; x++)
            {
                if (!outline || x == rect.Left || x == rect.Right || y == rect.Top || y == rect.Bottom)
                    yield return (x, y);
            }
        }
    }

    /// <summary>
    /// The tiles joined to (x, y) through edges that hold the same value on the plane as it does
    /// </summary>
    public static List<(int X, int Y)> FloodFill(MapDocument document, int plane, int x, int y)
    {
        var tiles = new List<(int, int)>();
        if (!document.Contains(x, y))
            return tiles;

        var value = document[plane, x, y];
        var seen = new bool[document.Width * document.Height];
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((x, y));
        seen[y * document.Width + x] = true;
        while (queue.Count > 0)
        {
            var (tx, ty) = queue.Dequeue();
            tiles.Add((tx, ty));
            foreach (var (nx, ny) in new[] { (tx - 1, ty), (tx + 1, ty), (tx, ty - 1), (tx, ty + 1) })
            {
                if (!document.Contains(nx, ny) || seen[ny * document.Width + nx] || document[plane, nx, ny] != value)
                    continue;
                seen[ny * document.Width + nx] = true;
                queue.Enqueue((nx, ny));
            }
        }
        return tiles;
    }
}

/// <summary>
/// Tiles copied from a level: the values of every plane, or of one, in a rectangle. Planes
/// not copied are left as they are where it's pasted.
/// </summary>
public sealed class TileClip
{
    private readonly ushort[]?[] _planes;

    private TileClip(int width, int height, ushort[]?[] planes)
    {
        Width = width;
        Height = height;
        _planes = planes;
    }

    public int Width { get; }
    public int Height { get; }

    public bool HasPlane(int plane) => plane < _planes.Length && _planes[plane] != null;

    public ushort this[int plane, int x, int y] => _planes[plane]![y * Width + x];

    /// <param name="plane">The one plane to copy, or null for all of them</param>
    public static TileClip Copy(MapDocument document, TileRect rect, int? plane)
    {
        var planes = new ushort[]?[document.Planes];
        for (int p = 0; p < document.Planes; p++)
        {
            if (plane != null && plane != p)
                continue;

            var values = new ushort[rect.Width * rect.Height];
            for (int y = 0; y < rect.Height; y++)
                for (int x = 0; x < rect.Width; x++)
                    values[y * rect.Width + x] = document.Contains(rect.Left + x, rect.Top + y) ? document[p, rect.Left + x, rect.Top + y] : (ushort)0;
            planes[p] = values;
        }
        return new TileClip(rect.Width, rect.Height, planes);
    }

    /// <summary>Puts the tiles down with their top left corner on (left, top); what falls off the level is dropped</summary>
    public void Paste(MapEdit edit, int left, int top)
    {
        for (int p = 0; p < _planes.Length; p++)
        {
            if (_planes[p] == null)
                continue;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    edit.Set(p, left + x, top + y, this[p, x, y]);
        }
    }
}
