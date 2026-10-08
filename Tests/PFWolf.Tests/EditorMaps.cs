using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

/// <summary>Levels and documents for the editor's tests</summary>
internal static class EditorMaps
{
    /// <summary>A level of open floor (100) on plane 0, with nothing on the others</summary>
    public static MapAsset Blank(string name = "Test")
    {
        var planes = new ushort[MapConstants.LEVELPLANES][];
        for (int p = 0; p < planes.Length; p++)
            planes[p] = new ushort[MapConstants.MAPAREA];
        Array.Fill(planes[0], (ushort)100);
        return new MapAsset { Width = MapConstants.MAPSIZE, Height = MapConstants.MAPSIZE, Name = name, MapData = planes };
    }

    /// <summary>A level game-info lists, with nothing set in its entry</summary>
    public static MapDocument Document(string assetName = "MAP01") => new(assetName, Blank(), properties: new MapProperties());

    /// <summary>Sets one tile as its own undo step</summary>
    public static void Set(MapDocument document, int plane, int x, int y, ushort value)
    {
        using var edit = document.BeginEdit("Set");
        edit.Set(plane, x, y, value);
    }
}
