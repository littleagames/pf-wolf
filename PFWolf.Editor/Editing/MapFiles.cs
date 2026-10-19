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

    /// <summary>Where a level of this asset name goes in a mod as a plain UWMF text file</summary>
    public static string TextEntryPath(string assetName) => "maps/" + assetName.ToUpperInvariant() + ".uwmf";

    /// <summary>
    /// Writes the level into the mod (a folder or a pk3) in its format: UWMF text (a TEXTMAP in
    /// maps/NAME.wad, or maps/NAME.uwmf where the mod already keeps it that way) or binary planes
    /// in maps/NAME.wad, taking out the other file if there is one; and its changed properties
    /// into the mod's game-info.yaml. A level with its own definitions or extra things is always
    /// UWMF. Returns where the level went.
    /// </summary>
    /// <param name="mapdefs">The game's mapdefs, which UWMF names walls, doors and things through</param>
    /// <param name="warn">Told what UWMF couldn't say about the level</param>
    public static string Save(MapDocument document, string modPath, MapObjectTranslationAsset? mapdefs = null, Action<string>? warn = null)
    {
        mapdefs ??= new MapObjectTranslationAsset();
        var name = document.AssetName.ToUpperInvariant();
        var map = document.Map;
        bool uwmf = map.Format == MapFormat.Uwmf || map.LocalDefs != null || map.ExtraThings.Count > 0;
        bool hasText = ModFiles.ReadBytes(modPath, TextEntryPath(name)) != null;
        bool hasWad = ModFiles.ReadBytes(modPath, EntryPath(name)) != null;

        // All worked out before anything's written: the properties are what can fail on a mod's
        // hand-written YAML, and then nothing has been
        var files = new List<(string, byte[])>();
        var deletes = new List<string>();
        if (uwmf && hasText)
        {
            files.Add((TextEntryPath(name), System.Text.Encoding.UTF8.GetBytes(Loaders.Uwmf.UwmfWriter.Write(map, mapdefs, warn))));
            if (hasWad)
                deletes.Add(EntryPath(name));
        }
        else
        {
            files.Add((EntryPath(name), uwmf ? EcWolfMapLoader.SaveUwmf(map, name, mapdefs, warn) : EcWolfMapLoader.Save(map, name)));
            if (hasText)
                deletes.Add(TextEntryPath(name));
        }
        var keys = document.PropertyKeysToWrite();
        var properties = document.Properties;
        if (keys.Count > 0)
        {
            if (properties.Name == null && keys.Contains("name"))
                properties = properties with { Name = document.Map.Name };
            files.Add(GameInfoFile.Updated(modPath, document.AssetName.ToUpperInvariant(), properties, keys));
        }

        // In one go: a pk3 is rewritten once, with both
        var path = ModFiles.Change(modPath, files, deletes);

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
    /// A new, empty level <paramref name="width"/> x <paramref name="height"/> tiles (64x64 left
    /// out): a border of <paramref name="wall"/> round open floor (the first area code), nothing
    /// on the other planes
    /// </summary>
    public static MapAsset NewMap(string title, ushort wall, ushort floor,
        int width = MapConstants.ClassicMapSize, int height = MapConstants.ClassicMapSize)
    {
        var planes = new ushort[MapConstants.LevelPlanes][];
        for (int p = 0; p < planes.Length; p++)
            planes[p] = new ushort[width * height];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                planes[0][y * width + x] = x == 0 || y == 0 || x == width - 1 || y == height - 1 ? wall : floor;

        // UWMF is the editor's format for new levels (readable, and it holds everything)
        return new MapAsset { Width = (ushort)width, Height = (ushort)height, Name = title, MapData = planes, Format = MapFormat.Uwmf };
    }
}
