using PFWolf.Assets;
using PFWolf.Definitions;
using PFWolf.Editor.Definitions;
using PFWolf.Editor.ViewModels;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Tests;

/// <summary>The definitions window's fields: made from YAML and a schema, and built back as edited</summary>
public class FieldNodeTests
{
    private static readonly FieldContext Context = new();

    private static FieldNode Fields(Type type, string yaml)
    {
        var node = new FieldNode(FieldSchema.For(type), "entry", YamlTree.Parse(yaml), null, null, Context, isEntry: true);
        node.EnsureChildren();
        return node;
    }

    private static FieldNode Child(FieldNode node, string key)
    {
        node.EnsureChildren();
        return node.Children.First(child => child.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void Fields_Build_Back_What_They_Were_Made_From()
    {
        // Arrange
        const string door = "open-sound: doors/open\nVertical: true\nEast: DOOR1_2\nlocked: { North: X }\nmystery: [1, 2]\n";
        var node = Fields(typeof(MapTextureTranslation), door);

        // Act
        var built = node.Build([]);

        // Assert
        Assert.That(YamlNodes.Equal(built, YamlTree.Parse(door)), Is.True, YamlText.Inline(built!));
        Assert.That(Child(node, "mystery").Kind, Is.EqualTo(FieldKind.Raw));
        Assert.That(Child(node, "vertical").Kind, Is.EqualTo(FieldKind.Choice));
        Assert.That(Child(node, "north").Text, Is.Empty);
    }

    [Test]
    public void An_Emptied_Field_Is_Left_Out_And_A_Typed_One_Set()
    {
        // Arrange
        var node = Fields(typeof(MapTextureTranslation), "North: A\nSouth: A\n");

        // Act
        Child(node, "North").Text = "";
        Child(node, "lock").Text = "GoldKey";
        var built = node.Build([]);

        // Assert
        Assert.That(YamlNodes.Equal(built, YamlTree.Parse("South: A\nlock: GoldKey\n")), Is.True, YamlText.Inline(built!));
    }

    [Test]
    public void A_Changed_Value_Keeps_Its_Quotes()
    {
        // Arrange
        var node = Fields(typeof(DefaultMapInfo), "ceiling-color: \"#383838\"\nfloor-color: '#717171'\n");

        // Act
        Child(node, "ceiling-color").Text = "#202020";
        var text = YamlText.Sync("ceiling-color: \"#383838\"\nfloor-color: '#717171'\n", [], (YamlMappingNode)node.Build([])!);

        // Assert
        Assert.That(text, Is.EqualTo("ceiling-color: \"#202020\"\nfloor-color: '#717171'\n"));
    }

    [Test]
    public async Task A_Group_Counts_What_Is_Added_And_Taken_Out()
    {
        // Arrange
        var node = new FieldNode(DefinitionSchemas.Actor, "Guard", YamlTree.Parse("states:\n  Spawn:\n    - sprite: GARD\n"), null, null, Context, isEntry: true);
        node.EnsureChildren();
        var spawn = Child(Child(node, "states"), "Spawn");
        var summaries = new List<string>();
        spawn.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FieldNode.Summary))
                summaries.Add(spawn.Summary);
        };

        // Act
        await spawn.AddCommand.ExecuteAsync(null);
        spawn.Children[0].RemoveCommand.Execute(null);

        // Assert
        Assert.That(summaries, Is.EqualTo(new[] { "2 items", "1 item" }));
    }

    [Test]
    public async Task A_Section_That_Is_A_List_Can_Be_Added_To()
    {
        // Arrange: game-info's end-strings, as Copy to mod starts it
        var node = new FieldNode(DefinitionSchemas.GameInfo.Field("end-strings"), "end-strings", new YamlSequenceNode(), null, null, Context);
        node.EnsureChildren();

        // Act
        await node.AddCommand.ExecuteAsync(null);
        node.Children[0].Text = "Leaving so soon?";
        var built = node.Build([]);

        // Assert
        Assert.That(node.Children, Has.Count.EqualTo(1));
        Assert.That(YamlNodes.Equal(built, new YamlSequenceNode(new YamlScalarNode("Leaving so soon?"))), Is.True);
    }

    [Test]
    public void A_Word_Where_A_Number_Goes_Is_An_Error()
    {
        // Arrange
        var node = Fields(typeof(MapActorTranslation), "class: Guard\nangles: 90\n");
        Child(node, "angles").Text = "north";
        var errors = new List<string>();

        // Act
        node.Build(errors);

        // Assert
        Assert.That(errors, Has.Count.EqualTo(1));
        Assert.That(errors[0], Does.Contain("angles"));
    }

    [Test]
    public void Inline_Lists_Are_Written_As_A_List()
    {
        // Arrange
        var node = new FieldNode(DefinitionSchemas.Actor, "Guard", YamlTree.Parse("flags: [SHOOTABLE]\n"), null, null, Context, isEntry: true);
        node.EnsureChildren();

        // Act
        Child(node, "flags").Text = "SHOOTABLE, COUNTKILL";
        var built = node.Build([]);

        // Assert
        Assert.That(YamlNodes.Equal(YamlNodes.Get(built, ["flags"]), new YamlSequenceNode(new YamlScalarNode("SHOOTABLE"), new YamlScalarNode("COUNTKILL"))), Is.True);
    }

    [Test]
    public void A_Group_Not_Set_Stays_Out_Until_Something_In_It_Is()
    {
        // Arrange
        var node = Fields(typeof(MapTextureTranslation), "North: A\n");
        var locked = Child(node, "locked");

        // Act
        var before = node.Build([]);
        locked.IsExpanded = true;
        Child(locked, "North").Text = "LOCKED";
        var after = node.Build([]);

        // Assert
        Assert.That(YamlNodes.Get(before, ["locked"]), Is.Null);
        Assert.That(((YamlScalarNode)YamlNodes.Get(after, ["locked", "North"])!).Value, Is.EqualTo("LOCKED"));
    }

    [Test]
    public void Schema_Has_The_Doc_Comments_And_Defaults()
    {
        // Act
        var menuStripe = DefinitionSchemas.GameInfo.Field("menu-stripe")!;
        var height = menuStripe.Field("height")!;

        // Assert
        Assert.That(DefinitionSchemas.GameInfo.Field("maps")!.Item!.Field("par-time"), Is.Not.Null);
        Assert.That(height.Default, Is.EqualTo("24"));
        Assert.That(DefinitionSchemas.MapDefs.Field("doors")!.Item!.Field("lock")!.Description, Does.Contain("inventory item"));
        Assert.That(DefinitionSchemas.GamePack.Field("base-pack"), Is.Not.Null);
    }
}

/// <summary>Every definition of the real games made into fields and built back. Needs a built game folder.</summary>
[Category("GameData")]
public class FieldNodeGameTests
{
    private static Editor.Data.GameContent Load(string game)
    {
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        var folder = new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate) && File.Exists(Path.Combine(candidate, "pfwolf.pk3")));
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3");

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            return Editor.Data.GameContent.Load(folder!, game, []);
        }
        catch (PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"{game}'s data files aren't in {folder}");
            throw;
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
        }
    }

    [TestCase("wolf3d")]
    [TestCase("spear")]
    public void Every_Definition_Builds_Back_Unchanged(string game)
    {
        // Arrange
        var content = Load(game);
        var context = new FieldContext();

        foreach (var kind in DefinitionKinds.All)
        {
            var set = DefinitionSet.Load(content, kind);
            var entries = kind == DefinitionKind.GameInfo
                ? set.Merged.Children.Keys.OfType<YamlScalarNode>().Select(key => (IReadOnlyList<string>)[key.Value!])
                : set.Entries(baseOnly: false).Select(entry => entry.Path);

            foreach (var path in entries)
            {
                var value = set.ValueOf(path, baseOnly: false);
                var schema = kind switch
                {
                    DefinitionKind.GameInfo => DefinitionSchemas.GameInfo.Field(path[0]),
                    DefinitionKind.GamePackInfo => DefinitionSchemas.GamePack,
                    DefinitionKind.Actors => DefinitionSchemas.Actor,
                    DefinitionKind.Animations => DefinitionSchemas.Animation,
                    _ => DefinitionSchemas.MapDefs.Field(path[0]) is { } table && path.Count > 1 ? table.Item : DefinitionSchemas.MapDefs.Field(path[0]),
                };

                // Act
                var node = new FieldNode(schema, path[^1], value, null, null, context, isEntry: kind != DefinitionKind.GameInfo);
                OpenAll(node);
                var errors = new List<string>();
                var built = node.Build(errors);

                // Assert
                Assert.That(errors, Is.Empty, $"{kind} {YamlNodes.Describe(path)}");
                Assert.That(YamlNodes.Equal(built, value), Is.True,
                    $"{kind} {YamlNodes.Describe(path)}:\n{(value == null ? "null" : YamlText.Inline(value))}\nbuilt\n{(built == null ? "null" : YamlText.Inline(built))}");
            }
        }
    }

    // Opens every group that's set, as a person clicking through it all would
    private static void OpenAll(FieldNode node)
    {
        node.EnsureChildren();
        foreach (var child in node.Children.Where(child => child.IsGroup && !child.Summary.Equals("not set")))
            OpenAll(child);
    }
}
