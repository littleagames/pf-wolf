using System.Text.RegularExpressions;
using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Editor.Editing;

/// <summary>Which part of a level stays put as it's resized: a corner, an edge's middle, or the middle</summary>
public enum MapAnchor
{
    TopLeft, Top, TopRight,
    Left, Center, Right,
    BottomLeft, Bottom, BottomRight,
}

/// <summary>Level sizes: reading "W x H", and resizing a level's planes round an anchor</summary>
public static partial class MapResizing
{
    /// <summary>The anchors as they're typed ("top-left", "center", ...)</summary>
    public static readonly IReadOnlyList<string> AnchorNames =
        ["top-left", "top", "top-right", "left", "center", "right", "bottom-left", "bottom", "bottom-right"];

    /// <summary>A size typed as "W x H" (or "WxH", or one number for a square), or null if it isn't one</summary>
    public static (int Width, int Height)? ParseSize(string text)
    {
        var match = SizeText().Match(text);
        if (!match.Success)
            return null;
        int width = int.Parse(match.Groups[1].Value);
        int height = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : width;
        return (width, height);
    }

    /// <summary>Why the text isn't a size a level can be, or null when it is</summary>
    public static string? CheckSize(string text) =>
        ParseSize(text) is not var (width, height)
            ? "Type the size as width x height, like 64x64."
            : MapConstants.CheckMapSize(width, height) is { } bad ? $"That's too {(width < MapConstants.MinMapSize || height < MapConstants.MinMapSize ? "small" : "big")}: {bad}." : null;

    [GeneratedRegex(@"^\s*(\d{1,4})\s*(?:[xX×,]\s*(\d{1,4}))?\s*$")]
    private static partial Regex SizeText();

    /// <summary>An anchor as typed, or null if it isn't one</summary>
    public static MapAnchor? ParseAnchor(string text)
    {
        var name = text.Trim().ToLowerInvariant().Replace(' ', '-').Replace("centre", "center").Replace("middle", "center");
        int index = AnchorNames.ToList().IndexOf(name);
        return index < 0 ? null : (MapAnchor)index;
    }

    /// <summary>Where the old level's top-left tile goes in the new one</summary>
    public static (int X, int Y) Offset(int oldWidth, int oldHeight, int width, int height, MapAnchor anchor)
    {
        int column = (int)anchor % 3, row = (int)anchor / 3;
        return (column * (width - oldWidth) / 2, row * (height - oldHeight) / 2);
    }

    /// <summary>
    /// The level's extra things moved with it to <paramref name="width"/> x <paramref name="height"/>
    /// round <paramref name="anchor"/>; those that end up off the level are left out
    /// </summary>
    public static List<MapThing> ResizedExtras(MapAsset map, int width, int height, MapAnchor anchor)
    {
        var (dx, dy) = Offset(map.Width, map.Height, width, height, anchor);
        return map.ExtraThings
            .Select(thing => thing with { X = thing.X + dx, Y = thing.Y + dy })
            .Where(thing => thing.TileX >= 0 && thing.TileY >= 0 && thing.TileX < width && thing.TileY < height)
            .ToList();
    }

    /// <summary>
    /// The level's planes at <paramref name="width"/> x <paramref name="height"/>: what was there
    /// kept in place round <paramref name="anchor"/> (cut off where it no longer fits), new tiles
    /// <paramref name="fill"/> on plane 0 and nothing on the others
    /// </summary>
    public static ushort[][] Resized(MapAsset map, int width, int height, MapAnchor anchor, ushort fill)
    {
        var (dx, dy) = Offset(map.Width, map.Height, width, height, anchor);
        var planes = new ushort[map.MapData.Length][];
        for (int p = 0; p < planes.Length; p++)
        {
            var plane = planes[p] = new ushort[width * height];
            if (p == 0)
                Array.Fill(plane, fill);
            for (int y = 0; y < map.Height; y++)
            {
                int ny = y + dy;
                if (ny < 0 || ny >= height)
                    continue;
                for (int x = 0; x < map.Width; x++)
                {
                    int nx = x + dx;
                    if (nx >= 0 && nx < width)
                        plane[ny * width + nx] = map.MapData[p][y * map.Width + x];
                }
            }
        }
        return planes;
    }
}
