using System.Text.RegularExpressions;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Loaders;

namespace PFWolf.Editor.Editing;

/// <summary>Levels as files in a mod (a folder or a pk3): maps/NAME.wad, as the game reads them</summary>
public static partial class MapFiles
{
    /// <summary>Where a level of this asset name goes in a mod</summary>
    public static string EntryPath(string assetName) => "maps/" + assetName.ToUpperInvariant() + ".wad";

    /// <summary>Where a level of this asset name goes in a mod folder</summary>
    public static string PathIn(string modFolder, string assetName) => Path.Combine(modFolder, "maps", assetName.ToUpperInvariant() + ".wad");

    /// <summary>
    /// Writes the level to maps/NAME.wad in the mod (a folder or a pk3), and its changed
    /// properties into the mod's game-info.yaml; returns where the level went
    /// </summary>
    public static string Save(MapDocument document, string modPath)
    {
        // All worked out before anything's written: the properties are what can fail on a mod's
        // hand-written YAML, and then nothing has been
        var files = new List<(string, byte[])>
        {
            (EntryPath(document.AssetName), EcWolfMapLoader.Save(document.Map, document.AssetName.ToUpperInvariant())),
        };
        var keys = document.PropertyKeysToWrite();
        var properties = document.Properties;
        if (keys.Count > 0)
        {
            if (properties.Name == null && keys.Contains("name"))
                properties = properties with { Name = document.Map.Name };
            files.Add(GameInfoFile.Updated(modPath, document.AssetName.ToUpperInvariant(), properties, keys));
        }

        // In one go: a pk3 is rewritten once, with both
        var path = ModFiles.Write(modPath, [.. files]);

        document.SaveFolder = modPath;
        if (properties != document.Properties)
            document.SetProperties(properties);
        document.MarkPropertiesSaved();
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
