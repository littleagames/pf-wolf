using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Loaders;

namespace PFWolf.Editor.Editing;

/// <summary>One plane of the file going onto one plane of the level</summary>
public readonly record struct PlaneImport(int From, int To);

/// <summary>
/// What to import: which of the file's levels, which planes onto which, and where: the level
/// being edited, or a new one named <see cref="NewLevelName"/>
/// </summary>
public sealed record ImportRequest(WdcMap Source, IReadOnlyList<PlaneImport> Planes, string? NewLevelName);

/// <summary>Planes from a WDC map file (.map), or an ECWolf binary map (.wad), into a level</summary>
public static class MapImport
{
    /// <summary>The levels in the file, or why it can't be read</summary>
    public static IReadOnlyList<WdcMap> ReadFile(string path) => WdcMapFile.ReadFile(File.ReadAllBytes(path));

    /// <summary>Why a level of the file can't be imported, or null when it can</summary>
    public static string? Check(WdcMap map)
    {
        if (map.Width != MapConstants.MAPSIZE || map.Height != MapConstants.MAPSIZE)
            return $"{Label(map)} is {map.Width}x{map.Height}; PFWolf levels are {MapConstants.MAPSIZE}x{MapConstants.MAPSIZE}.";
        if (map.Planes.Length == 0)
            return $"{Label(map)} has no planes.";
        return null;
    }

    public static string Label(WdcMap map) => string.IsNullOrWhiteSpace(map.Name) ? "(no name)" : map.Name;

    /// <summary>How many of a plane's tiles aren't 0</summary>
    public static int TilesInUse(WdcMap map, int plane) => map.Planes[plane].Count(tile => tile != 0);

    /// <summary>The planes the file has that a level can take, each onto the same plane</summary>
    public static IEnumerable<int> ImportablePlanes(WdcMap map) => Enumerable.Range(0, Math.Min(map.Planes.Length, MapConstants.LEVELPLANES));

    /// <summary>Puts the planes onto the level as one undo step; returns how many tiles changed</summary>
    public static int Apply(MapDocument document, WdcMap source, IReadOnlyList<PlaneImport> planes, string description)
    {
        var edit = document.BeginEdit(description);
        int changed = 0;
        foreach (var (from, to) in planes)
        {
            for (int y = 0; y < document.Height; y++)
            {
                for (int x = 0; x < document.Width; x++)
                {
                    var value = source.Planes[from][y * source.Width + x];
                    if (document[to, x, y] != value)
                        changed++;
                    edit.Set(to, x, y, value);
                }
            }
        }
        edit.Commit();
        return changed;
    }

    /// <summary>
    /// A new level of the planes: the rest as a new level is (a border of <paramref name="wall"/>
    /// round open floor when the walls aren't imported, nothing on the other planes)
    /// </summary>
    public static MapAsset NewLevel(string title, WdcMap source, IReadOnlyList<PlaneImport> planes, ushort wall, ushort floor)
    {
        var map = MapFiles.NewMap(title, wall, floor);
        foreach (var (from, to) in planes)
            map.MapData[to] = (ushort[])source.Planes[from].Clone();
        return map;
    }
}
