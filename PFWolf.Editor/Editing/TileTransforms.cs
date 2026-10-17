using PFWolf.Enums;

namespace PFWolf.Editor.Editing;

/// <summary>A turn or mirror of a block of tiles, as seen on the map (north up)</summary>
public enum TileTransform
{
    RotateClockwise,
    RotateCounterClockwise,
    Rotate180,
    FlipHorizontal,
    FlipVertical,
}

/// <summary>
/// Where a transform puts a tile of a block, which way things then face, and the shapes and
/// faces that turn with them
/// </summary>
public static class TileTransforms
{
    public static string Describe(TileTransform transform) => transform switch
    {
        TileTransform.RotateClockwise => "Rotate clockwise",
        TileTransform.RotateCounterClockwise => "Rotate counter-clockwise",
        TileTransform.Rotate180 => "Rotate 180°",
        TileTransform.FlipHorizontal => "Flip horizontally",
        _ => "Flip vertically",
    };

    /// <summary>A quarter turn: the block's width and height swap</summary>
    public static bool SwapsAxes(TileTransform transform) => transform is TileTransform.RotateClockwise or TileTransform.RotateCounterClockwise;

    /// <summary>The block's size after the transform</summary>
    public static (int Width, int Height) Size(TileTransform transform, int width, int height)
        => SwapsAxes(transform) ? (height, width) : (width, height);

    /// <summary>Where tile (x, y) of a width × height block goes in the transformed block</summary>
    public static (int X, int Y) Map(TileTransform transform, int x, int y, int width, int height) => transform switch
    {
        TileTransform.RotateClockwise => (height - 1 - y, x),
        TileTransform.RotateCounterClockwise => (y, width - 1 - x),
        TileTransform.Rotate180 => (width - 1 - x, height - 1 - y),
        TileTransform.FlipHorizontal => (width - 1 - x, y),
        _ => (x, height - 1 - y),
    };

    /// <summary>
    /// Where a block turned in place goes: around its middle, so a quarter turn and its way back
    /// land where it started; then moved back onto the level if it fits there
    /// </summary>
    public static TileRect Place(TileTransform transform, TileRect rect, int levelWidth, int levelHeight)
    {
        var (width, height) = Size(transform, rect.Width, rect.Height);
        int left = rect.Left, top = rect.Top;
        if (SwapsAxes(transform))
        {
            // Clockwise rounds down and counter-clockwise up, so one undoes the other
            double dx = (rect.Width - width) / 2.0, dy = (rect.Height - height) / 2.0;
            bool down = transform == TileTransform.RotateClockwise;
            left += (int)(down ? Math.Floor(dx) : Math.Ceiling(dx));
            top += (int)(down ? Math.Floor(dy) : Math.Ceiling(dy));
        }
        if (width <= levelWidth)
            left = Math.Clamp(left, 0, levelWidth - width);
        if (height <= levelHeight)
            top = Math.Clamp(top, 0, levelHeight - height);
        return new TileRect(left, top, left + width - 1, top + height - 1);
    }

    /// <summary>The way an angle (0 east, 90 north, as mapdefs give them) points after the transform, 0 to 359</summary>
    public static int Angle(TileTransform transform, int angles)
    {
        int turned = transform switch
        {
            TileTransform.RotateClockwise => angles - 90,
            TileTransform.RotateCounterClockwise => angles + 90,
            TileTransform.Rotate180 => angles + 180,
            TileTransform.FlipHorizontal => 180 - angles,
            _ => -angles,
        };
        return ((turned % 360) + 360) % 360;
    }

    private static readonly (string Name, int Angle)[] FaceAngles = [("east", 0), ("north", 90), ("west", 180), ("south", 270)];

    /// <summary>The way a face ("north", "south", "east" or "west") looks out after the transform; anything else as it is</summary>
    public static string Face(TileTransform transform, string face)
    {
        foreach (var (name, angle) in FaceAngles)
        {
            if (name.Equals(face, StringComparison.OrdinalIgnoreCase))
            {
                int turned = Angle(transform, angle);
                return FaceAngles.First(f => f.Angle == turned).Name;
            }
        }
        return face;
    }

    /// <summary>Faces after the transform, as a set to compare</summary>
    public static HashSet<string> FaceSet(TileTransform transform, IEnumerable<string> faces)
        => faces.Select(face => Face(transform, face.ToLowerInvariant())).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>A diagonal wall's shape after the transform: its solid corner goes where its two solid edges do</summary>
    public static WallShape Shape(TileTransform transform, WallShape shape)
    {
        var (a, b) = shape switch
        {
            WallShape.SolidNW => ("north", "west"),
            WallShape.SolidNE => ("north", "east"),
            WallShape.SolidSW => ("south", "west"),
            WallShape.SolidSE => ("south", "east"),
            _ => ("", ""),
        };
        if (a.Length == 0)
            return shape;

        var edges = new[] { Face(transform, a), Face(transform, b) };
        bool north = edges.Contains("north"), west = edges.Contains("west");
        return (north, west) switch
        {
            (true, true) => WallShape.SolidNW,
            (true, false) => WallShape.SolidNE,
            (false, true) => WallShape.SolidSW,
            _ => WallShape.SolidSE,
        };
    }
}
