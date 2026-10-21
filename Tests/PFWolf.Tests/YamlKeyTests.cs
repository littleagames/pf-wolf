using PFWolf.Assets;
using PFWolf.Definitions;
using PFWolf.DocGen;
using PFWolf.Editor.Definitions;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Tests;

/// <summary>
/// Every key in the repository's YAML (pfwolf-pk3, multiplayer-pk3, the example mods) is one the
/// game reads. The loader skips a key it doesn't know without a word (IgnoreUnmatchedProperties),
/// so a misspelt one (deathcam for death-cam) just does nothing.
/// </summary>
[Category("Content")]
public class YamlKeyTests
{
    [Test]
    public void Every_Key_Is_One_The_Game_Reads()
    {
        // Arrange
        var root = TestPaths.RepoRoot();
        var problems = new List<string>();
        int checkedFiles = 0;

        // Act
        foreach (var (folder, isMod) in Sources(root))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.y*ml", SearchOption.AllDirectories))
            {
                var entry = Path.GetRelativePath(folder, file).Replace('\\', '/');
                if (SchemaFor(entry, isMod) is not { } schema)
                    continue;

                var stream = new YamlStream();
                stream.Load(new StringReader(File.ReadAllText(file)));
                foreach (var document in stream.Documents)
                    Check(document.RootNode, schema, "", Path.GetRelativePath(root, file).Replace('\\', '/'), problems);
                checkedFiles++;
            }
        }

        // Assert (each listed in the output too, which a long failure message cuts short)
        foreach (var problem in problems)
            TestContext.Out.WriteLine(problem);
        Assert.That(checkedFiles, Is.GreaterThan(50), "the files weren't found");
        Assert.That(problems, Is.Empty);
    }

    // pfwolf-pk3, multiplayer-pk3 and each example mod: (folder, laid out as a mod)
    private static IEnumerable<(string Folder, bool IsMod)> Sources(string root)
    {
        yield return (Path.Combine(root, "pfwolf-pk3"), false);
        yield return (Path.Combine(root, "multiplayer-pk3"), true);
        foreach (var mod in Directory.EnumerateDirectories(Path.Combine(root, "examples", "mods")))
            yield return (mod, true);
    }

    // The schema of what the game reads a file as, by its path as pfwolf.pk3 has it; null for a
    // file this can't check
    private static FieldSchema? SchemaFor(string entry, bool isMod)
    {
        if (isMod && !entry.Contains('/'))
        {
            if (entry.Equals("modinfo.yaml", StringComparison.OrdinalIgnoreCase))
                return Schema("modinfo");
            if (entry.Equals("gamepack-info.yaml", StringComparison.OrdinalIgnoreCase))
                return Schema("gamepack-info");
        }

        var path = YamlFileTypes.LoadedPath(entry, isMod);
        var parts = path.Split('/');
        var name = Path.GetFileNameWithoutExtension(parts[^1]).ToLowerInvariant();
        return parts[0].ToLowerInvariant() switch
        {
            // ActorData itself, as the loader reads it: the docs' ActorSchema is written by hand
            "actordefs" => FieldSchema.For(typeof(Dictionary<string, ActorData>)),
            "mapdefs" => Schema("mapdefs"),
            "animdefs" => Schema("animdefs"),
            "graphicdefs" => Schema("graphicdefs"),
            "menudefs" => Schema("menudefs"),
            "language" => Schema("language"),
            "sounds" when name.Contains("sound-seq") => Schema("sound-seq"),
            "gamepacks" when parts.Length == 2 && name == "gamepack-info" => Schema("gamepack-info"),
            "gamepacks" when parts.Length == 4 && parts[2].Equals("language", StringComparison.OrdinalIgnoreCase) => Schema("language"),
            "gamepacks" when parts.Length == 3 => Schema(name == "statusbar-fullscreen" ? "statusbar" : name),
            _ => null,
        };
    }

    private static FieldSchema? Schema(string id) => DefinitionFiles.All.FirstOrDefault(file => file.Id == id)?.Schema();

    // Each key of a record checked against its fields, and on down through dictionaries and lists.
    // A node of another shape than the schema expects is left alone: the loader reads some values
    // by hand (an actor state's stop, an animation frame's { name, tics }).
    private static void Check(YamlNode node, FieldSchema schema, string path, string file, List<string> problems)
    {
        // !remove takes a key out of what it's laid over: there's nothing below it to read
        if (node.Tag is { IsEmpty: false, IsNonSpecific: false } tag && tag.Value == "!remove")
            return;

        switch (schema.Type)
        {
            case FieldType.Record when schema.ClrType == typeof(StateData) && node is YamlMappingNode frame:
                CheckFrame(frame, path, file, problems);
                break;

            case FieldType.Record when node is YamlMappingNode mapping:
                foreach (var (keyNode, value) in mapping.Children)
                {
                    if (keyNode is not YamlScalarNode { Value: { } key })
                        continue;
                    var keyPath = path.Length == 0 ? key : $"{path}.{key}";
                    if (schema.Field(key) is { } field)
                        Check(value, field, keyPath, file, problems);
                    else
                        problems.Add($"{file}:{keyNode.Start.Line}: {keyPath} isn't read ({Owner(schema)} has no {key})");
                }
                break;

            case FieldType.Dictionary when node is YamlMappingNode entries && schema.Item is { } item:
                foreach (var (keyNode, value) in entries.Children)
                    Check(value, item, path.Length == 0 ? $"{keyNode}" : $"{path}.{keyNode}", file, problems);
                break;

            case FieldType.List when node is YamlSequenceNode items && schema.Item is { } listItem:
                for (int i = 0; i < items.Children.Count; i++)
                    Check(items.Children[i], listItem, $"{path}[{i}]", file, problems);
                break;
        }
    }

    // The keys StateDataNodeDeserializer reads from an actor state's frame, hyphens left out
    // ("frame" too, for "frames"). A frame of { goto: State } has nothing else.
    private static readonly HashSet<string> FrameKeys =
        ["sprite", "frames", "frame", "ticsperframe", "modifiers", "light", "action", "think", "nextstate"];

    private static void CheckFrame(YamlMappingNode frame, string path, string file, List<string> problems)
    {
        foreach (var keyNode in frame.Children.Keys)
        {
            if (keyNode is not YamlScalarNode { Value: { } key })
                continue;
            var normalized = key.Replace("-", "").ToLowerInvariant();
            if (normalized == "goto" && frame.Children.Count > 1)
                problems.Add($"{file}:{keyNode.Start.Line}: {path}.{key} isn't read (a goto is read only on a frame of its own)");
            else if (normalized != "goto" && !FrameKeys.Contains(normalized))
                problems.Add($"{file}:{keyNode.Start.Line}: {path}.{key} isn't read (a state's frame has no {key})");
        }
    }

    private static string Owner(FieldSchema schema) => schema.ClrType == typeof(object) ? schema.Label : schema.ClrType.Name;
}
