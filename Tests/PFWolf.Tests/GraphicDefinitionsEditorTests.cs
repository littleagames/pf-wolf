using PFWolf.Editor.Definitions;
using PFWolf.Editor.ViewModels;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Tests;

/// <summary>The definitions editor's graphicdefs: merged as the game merges them, edited in fields, and saved as overlays</summary>
public class GraphicDefinitionsEditorTests
{
    private const string Mod = @"C:\mods\mymod";

    private static DefinitionSet Graphics(params DefinitionFile[] mods)
        => new(DefinitionKind.Graphics, "wolf3d",
        [
            new DefinitionFile(null, "graphicdefs/graphicdefs.yaml", DefinitionFile.SharedLayer,
                "HEAD:\n  source: GARDA1\n  x: 20\n  width: 24\n  height: 32\nWALLBIT:\n  source: GSTONEA1\n  width: 8\n  height: 8\n"),
            new DefinitionFile(null, "graphicdefs/wolf3d/graphicdefs.yaml", "", "HEAD:\n  source: OFFIA1\n  width: 16\n  height: 16\n"),
            .. mods,
        ]);

    private static string? Scalar(YamlNode? node, params string[] path) => (YamlNodes.Get(node, path) as YamlScalarNode)?.Value;

    [Test]
    public void The_Packs_Picture_Replaces_A_Shared_One_Of_Its_Name()
    {
        // Act
        var set = Graphics();

        // Assert: whole, so the shared one's x doesn't carry over
        Assert.That(Scalar(set.Merged, "HEAD", "source"), Is.EqualTo("OFFIA1"));
        Assert.That(YamlNodes.Get(set.Merged, ["HEAD", "x"]), Is.Null);
        Assert.That(set.Entries(baseOnly: false).Select(entry => entry.Name), Is.EquivalentTo(new[] { "HEAD", "WALLBIT" }));
    }

    [Test]
    public void A_Mod_Changes_The_Packs_Picture_Key_By_Key()
    {
        // Act
        var set = Graphics(new DefinitionFile(Mod, "graphicdefs/mine.yaml", "", "HEAD:\n  x: 4\n"));

        // Assert
        Assert.That(Scalar(set.Merged, "HEAD", "x"), Is.EqualTo("4"));
        Assert.That(Scalar(set.Merged, "HEAD", "source"), Is.EqualTo("OFFIA1"));
    }

    [Test]
    public void Editing_The_Packs_Picture_In_A_Mod_Writes_Only_What_Changes()
    {
        // Arrange
        var set = Graphics();
        var edited = (YamlMappingNode)YamlNodes.Clone(set.ValueOf(["HEAD"], baseOnly: false)!);
        YamlNodes.Set(edited, ["y"], new YamlScalarNode("6"));

        // Act
        var write = set.PlanWrite(["HEAD"], edited, Mod);

        // Assert
        Assert.That(write.File.EntryPath, Is.EqualTo("graphicdefs/graphicdefs.yaml"));
        Assert.That(Scalar(write.Value, "y"), Is.EqualTo("6"));
        Assert.That(YamlNodes.Get(write.Value, ["source"]), Is.Null);
    }

    [Test]
    public void Editing_A_Shared_Picture_In_A_Mod_Writes_It_Whole()
    {
        // Arrange
        var set = Graphics();
        var edited = (YamlMappingNode)YamlNodes.Clone(set.ValueOf(["WALLBIT"], baseOnly: false)!);
        YamlNodes.Set(edited, ["width"], new YamlScalarNode("16"));

        // Act
        var write = set.PlanWrite(["WALLBIT"], edited, Mod);

        // Assert: the pack has no WALLBIT for a part of it to merge into
        Assert.That(Scalar(write.Value, "width"), Is.EqualTo("16"));
        Assert.That(Scalar(write.Value, "source"), Is.EqualTo("GSTONEA1"));
    }

    [Test]
    public void A_Pictures_Fields_Build_Back_Unchanged()
    {
        // Arrange
        var value = YamlNodes.Get(YamlText.ParseRoot("A:\n  source: GARDA1\n  kind: sprite\n  x: 20\n  y: 2\n  width: 24\n  height: 32\n  background: \"#484848\"\n"), ["A"]);
        var node = new FieldNode(DefinitionSchemas.Graphic, "A", value, null, null, new FieldContext(), isEntry: true);
        node.EnsureChildren();

        // Act
        var errors = new List<string>();
        var built = node.Build(errors);

        // Assert
        Assert.That(errors, Is.Empty);
        Assert.That(YamlNodes.Equal(built, value), Is.True, YamlText.Inline(built!));
        Assert.That(node.Children.Select(child => child.Key), Is.EqualTo(new[] { "source", "kind", "x", "y", "width", "height", "background" }));
    }

    [Test]
    public void A_Mods_Graphicdefs_File_Is_Read_As_Cropped_Pictures()
    {
        // Act / Assert
        Assert.That(YamlFileTypes.KindOf("graphicdefs/mine.yaml", isMod: true), Is.EqualTo(DefinitionKind.Graphics));
        Assert.That(YamlFileTypes.LoadedPath("graphicdefs/mine.yaml", isMod: true, "wolf3d"), Is.EqualTo("graphicdefs/wolf3d/mine.yaml"));
        Assert.That(YamlFileTypes.ModPathFor("graphicdefs/wolf3d/faces.yaml", out _), Is.EqualTo("graphicdefs/faces.yaml"));
        Assert.That(YamlFileTypes.Describe("graphicdefs/mine.yaml", isMod: true), Is.EqualTo("cropped pictures"));
        Assert.That(DefinitionKinds.IsModFile(DefinitionKind.Graphics, "graphicdefs/faces.yaml"), Is.True);
        Assert.That(YamlFileTypes.Check("graphicdefs/mine.yaml", isMod: true, "A:\n  width: wide\n"), Is.Not.Null);
        Assert.That(YamlFileTypes.Check("graphicdefs/mine.yaml", isMod: true, "A: { source: X, x: 1, y: 2, width: 3, height: 4 }\n"), Is.Null);
    }
}
