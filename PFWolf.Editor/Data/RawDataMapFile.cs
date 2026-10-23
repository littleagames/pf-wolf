using System.Text;
using PFWolf.Editor.Editing;
using PFWolf.Managers;

namespace PFWolf.Editor.Data;

/// <summary>
/// A raw data mod's raw-data-map.yaml (docs/dev/raw-data-mods-plan.md): which of its chunks are
/// still under the made-up names the generator gave them, and naming them
/// </summary>
public static class RawDataMapFile
{
    /// <summary>Where the generator writes it in a mod</summary>
    public const string EntryPath = "raw-data-map.yaml";

    /// <summary>
    /// The chunks the loaded mods' raw-data-map.yaml files still mark to name ("new: name this"),
    /// each with the file that does
    /// </summary>
    public static Dictionary<string, ModEntry> NamesToGive(GameContent? content)
    {
        var names = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
        if (content == null)
            return names;
        foreach (var mod in content.Assets.LoadedMods)
        {
            foreach (var entry in new[] { EntryPath, $"gamepacks/{content.PackId}/{EntryPath}" })
            {
                if (ModFiles.ReadText(mod.FullPath, entry) is not { } text)
                    continue;
                foreach (var name in RawDataModWriter.NamesToGive(text))
                    names[name] = new ModEntry(mod.FullPath, entry);
            }
        }
        return names;
    }

    /// <summary>
    /// The change naming a chunk: its line in the raw-data-map.yaml, and the mapdefs walls the
    /// generator wrote with its made-up name
    /// </summary>
    public static ArtChange Rename(ModEntry file, ArtKind kind, string oldName, string newName)
    {
        var text = ModFiles.ReadText(file.ModPath, file.EntryPath);
        if (text == null || RawDataModWriter.RenameChunk(text, oldName, newName) is not { } renamed)
            throw new InvalidOperationException($"{oldName} isn't in {file.ModName}'s {file.EntryPath} any more");

        var writes = new List<(string EntryPath, byte[] Data)> { (file.EntryPath, Utf8(renamed)) };
        const string walls = "mapdefs/walls.yaml";
        if (ModFiles.ReadText(file.ModPath, walls) is { } wallsText && RawDataModWriter.RenameValues(wallsText, oldName, newName) is var renamedWalls
            && renamedWalls != wallsText)
            writes.Add((walls, Utf8(renamedWalls)));

        return new ArtChange(file.ModPath, writes, [], kind, newName, $"Named {oldName.ToUpperInvariant()} {newName.ToUpperInvariant()}");
    }

    private static byte[] Utf8(string text) => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);
}
