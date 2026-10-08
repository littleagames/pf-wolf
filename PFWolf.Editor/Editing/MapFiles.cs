using System.Text.RegularExpressions;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Loaders;

namespace PFWolf.Editor.Editing;

/// <summary>Levels as files in a mod folder: maps/NAME.wad, as the game reads them</summary>
public static partial class MapFiles
{
    /// <summary>Where a level of this asset name goes in a mod folder</summary>
    public static string PathIn(string modFolder, string assetName)
        => Path.Combine(modFolder, "maps", assetName.ToUpperInvariant() + ".wad");

    /// <summary>Writes the level to maps/NAME.wad in the mod folder; returns the file's path</summary>
    public static string Save(MapDocument document, string modFolder)
    {
        var path = PathIn(modFolder, document.AssetName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Written beside the old file first, so a failed write leaves the old one whole
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, EcWolfMapLoader.Save(document.Map, document.AssetName.ToUpperInvariant()));
        File.Move(temp, path, overwrite: true);

        document.SaveFolder = modFolder;
        document.MarkSaved();
        return path;
    }

    /// <summary>
    /// A level name the game can load as maps/NAME.wad: letters, digits and underscores, 8 at most
    /// (a WAD lump name), or null when it's fine
    /// </summary>
    public static string? CheckName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Give the level a name, like MAP61.";
        if (!LumpName().IsMatch(name))
            return "A level name is 1 to 8 letters, digits or underscores (it's a WAD lump name).";
        return null;
    }

    [GeneratedRegex("^[A-Za-z0-9_]{1,8}$")]
    private static partial Regex LumpName();

    /// <summary>
    /// A new, empty level: a border of <paramref name="wall"/> round open floor (the first area
    /// code), nothing on the other planes
    /// </summary>
    public static MapAsset NewMap(string title, ushort wall, ushort floor)
    {
        int size = MapConstants.MAPSIZE;
        var planes = new ushort[MapConstants.LEVELPLANES][];
        for (int p = 0; p < planes.Length; p++)
            planes[p] = new ushort[MapConstants.MAPAREA];

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                planes[0][y * size + x] = x == 0 || y == 0 || x == size - 1 || y == size - 1 ? wall : floor;

        return new MapAsset { Width = (ushort)size, Height = (ushort)size, Name = title, MapData = planes };
    }
}
