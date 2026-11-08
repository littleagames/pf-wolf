using PFWolf.Constants;
using PFWolf.Definitions;
using PFWolf.Editor.Data;

namespace PFWolf.Editor.Editing;

/// <summary>
/// Arches as levels wrote them before blocks had a start story: an open floor tile with a plain
/// height N (plane 3) was an arch from story 2 up to N over the walkway. Now a plain N is a block
/// from story 1 (solid on story 1), so an arch is written 2-N (0x020N). A plain 1 on open floor
/// was nothing at all.
/// </summary>
public static class OldArches
{
    /// <summary>An open floor tile with an old arch height, and what it becomes</summary>
    public readonly record struct OldArch(int X, int Y, int Old, int New);

    /// <summary>The open floor tiles whose height is a plain number (1 to 8): each old arch, and what it's written as now</summary>
    public static List<OldArch> Find(MapTiles tiles)
    {
        var found = new List<OldArch>();
        for (int y = 0; y < tiles.Height; y++)
        {
            for (int x = 0; x < tiles.Width; x++)
            {
                int value = tiles[MapConstants.HeightPlane, x, y];
                if (StoryBlocks.IsPlain(value) && tiles.KindAt(x, y) == TileKind.Floor)
                    found.Add(new OldArch(x, y, value, value == 1 ? 0 : StoryBlocks.Encode(2, value)));
            }
        }
        return found;
    }

    /// <summary>Converts a level's old arches as one edit (undoable); how many tiles changed</summary>
    public static int Convert(MapDocument document, MapTiles tiles)
    {
        var arches = Find(tiles);
        if (arches.Count == 0)
            return 0;
        using var edit = document.BeginEdit("Convert old arches");
        foreach (var arch in arches)
            edit.Set(MapConstants.HeightPlane, arch.X, arch.Y, (ushort)arch.New);
        return arches.Count;
    }
}
