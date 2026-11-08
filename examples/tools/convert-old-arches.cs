#:project ../../PFWolf.Editor/PFWolf.Editor.csproj
// Finds the old arches in a mod's levels (open floor with a plain height N: an arch from story 2,
// before blocks had a start story), and with --convert writes them as 2-N, as the editor's
// Level → Convert old arches does, saving each level back into the mod in its own format.
//
//   dotnet run examples/tools/convert-old-arches.cs -- <PFWolf folder> <mod folder or pk3> [game pack] [--convert]
//
// The PFWolf folder is the one pfwolf.pk3 and the game's data files are in (PFWolf/bin/x64/Debug/net10.0).
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;

if (args.Length < 2)
{
    Console.WriteLine("usage: dotnet run convert-old-arches.cs -- <PFWolf folder> <mod> [game pack] [--convert]");
    return 1;
}
string gameFolder = Path.GetFullPath(args[0]), mod = Path.GetFullPath(args[1]);
string pack = args.Length > 2 && !args[2].StartsWith("--") ? args[2] : "";
bool convert = args.Contains("--convert");

var content = GameContent.Load(gameFolder, pack, [mod]);
int total = 0;
foreach (var entry in content.Maps)
{
    var modPath = content.ModPathOf(entry.Name);
    if (modPath == null || !Path.GetFullPath(modPath).TrimEnd('\\', '/').Equals(mod.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
        continue;
    var map = content.FindMap(entry.Name)!;
    var info = content.MapInfoOf(entry.Name);
    var document = new MapDocument(entry.Name, map, properties: info != null ? MapProperties.From(info) : null) { SaveFolder = modPath };
    var tiles = new MapTiles(content, document.Map, () => document.Properties);
    var arches = OldArches.Find(tiles);
    if (arches.Count == 0)
        continue;
    total += arches.Count;
    Console.WriteLine($"{entry.Name}: {arches.Count} old arch tiles ("
        + string.Join(", ", arches.GroupBy(arch => arch.Old).OrderBy(group => group.Key).Select(group => $"{group.Count()} of height {group.Key}")) + ")");
    var entry2 = content.ModFileOf(entry.Name, nameof(PFWolf.Assets.MapAsset))?.EntryPath.Replace('\\', '/');
    if (convert && entry2 != null && !entry2.Equals(MapFiles.EntryPath(entry.Name), StringComparison.OrdinalIgnoreCase)
        && !entry2.Equals(MapFiles.TextEntryPath(entry.Name), StringComparison.OrdinalIgnoreCase))
    {
        // a pack's own folder (maps/wolf3d/NAME.wad): saving would write a second copy beside it
        Console.WriteLine($"  kept in {entry2}: not converted here (change what makes it, or move it to {MapFiles.EntryPath(entry.Name)})");
        continue;
    }
    if (convert)
    {
        OldArches.Convert(document, tiles);
        Console.WriteLine("  saved " + MapFiles.Save(document, modPath, content.MapDefs, warning => Console.WriteLine("  " + warning)));
    }
}
Console.WriteLine(total == 0 ? "No old arches." : convert ? $"Converted {total} tiles." : $"{total} tiles; --convert writes them as 2-N.");
return 0;
