using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Tests;

public class YamlTreeTests
{
    private static YamlMappingNode Parse(string yaml) => YamlTree.Parse(yaml)!;

    private static string Scalar(YamlMappingNode node, params string[] path)
    {
        YamlNode current = node;
        foreach (var key in path)
            current = ((YamlMappingNode)current).Children[new YamlScalarNode(key)];
        return ((YamlScalarNode)current).Value!;
    }

    [Test]
    public void Parse_Returns_Null_For_Empty_Or_Non_Mapping_Documents()
    {
        Assert.That(YamlTree.Parse(""), Is.Null);
        Assert.That(YamlTree.Parse("- a\n- b\n"), Is.Null);
    }

    [Test]
    public void DeepMerge_Merges_Nested_Mappings()
    {
        // Arrange
        var target = Parse("guard:\n  health: 25\n  speed: 512\n");
        var overlay = Parse("guard:\n  health: 50\n  color: red\n");

        // Act
        YamlTree.DeepMerge(target, overlay);

        // Assert
        Assert.That(Scalar(target, "guard", "health"), Is.EqualTo("50"));
        Assert.That(Scalar(target, "guard", "speed"), Is.EqualTo("512"));
        Assert.That(Scalar(target, "guard", "color"), Is.EqualTo("red"));
    }

    [Test]
    public void DeepMerge_Replaces_Lists_Rather_Than_Appending()
    {
        // Arrange
        var target = Parse("drops: [ammo, clip]\n");
        var overlay = Parse("drops: [key]\n");

        // Act
        YamlTree.DeepMerge(target, overlay);

        // Assert
        var drops = (YamlSequenceNode)target.Children[new YamlScalarNode("drops")];
        Assert.That(drops.Children.Select(n => ((YamlScalarNode)n).Value), Is.EqualTo(new[] { "key" }));
    }

    [Test]
    public void DeepMerge_Replaces_A_Mapping_With_A_Value()
    {
        // Arrange
        var target = Parse("light:\n  intensity: 200\n");
        var overlay = Parse("light: none\n");

        // Act
        YamlTree.DeepMerge(target, overlay);

        // Assert
        Assert.That(Scalar(target, "light"), Is.EqualTo("none"));
    }

    [Test]
    public void DeepMerge_Keeps_The_Targets_Key_Order()
    {
        // Arrange
        var target = Parse("a: 1\nb: 2\nc: 3\n");
        var overlay = Parse("d: 4\nb: 20\n");

        // Act
        YamlTree.DeepMerge(target, overlay);

        // Assert
        var keys = target.Children.Keys.Select(k => ((YamlScalarNode)k).Value);
        Assert.That(keys, Is.EqualTo(new[] { "a", "b", "c", "d" }));
        Assert.That(Scalar(target, "b"), Is.EqualTo("20"));
    }

    [Test]
    public void DeepMerge_Keys_Are_Case_Sensitive()
    {
        // Arrange
        var target = Parse("Guard: 1\n");
        var overlay = Parse("guard: 2\n");

        // Act
        YamlTree.DeepMerge(target, overlay);

        // Assert
        Assert.That(target.Children, Has.Count.EqualTo(2));
    }

    [Test]
    public void DeepMerge_Remove_Tag_Takes_Keys_Out_At_Any_Depth()
    {
        // Arrange
        var target = Parse("guard:\n  health: 25\n  speed: 512\ndog:\n  health: 1\n");
        var overlay = Parse("guard:\n  speed: !remove\ndog: !remove\nmissing: !remove\n");

        // Act
        YamlTree.DeepMerge(target, overlay);

        // Assert
        var guard = (YamlMappingNode)target.Children[new YamlScalarNode("guard")];
        Assert.That(guard.Children.Keys.Select(k => ((YamlScalarNode)k).Value), Is.EqualTo(new[] { "health" }));
        Assert.That(target.Children.Keys.Select(k => ((YamlScalarNode)k).Value), Is.EqualTo(new[] { "guard" }));
    }

    [Test]
    public void DeepMerge_Leaves_Remove_Tags_Out_Of_New_Entries()
    {
        // Arrange
        var target = Parse("guard:\n  health: 25\n");
        var overlay = Parse("officer:\n  health: 50\n  speed: !remove\n");

        // Act
        YamlTree.DeepMerge(target, overlay);

        // Assert
        var officer = (YamlMappingNode)target.Children[new YamlScalarNode("officer")];
        Assert.That(officer.Children, Has.Count.EqualTo(1));
        Assert.That(YamlTree.HasMergeTags(target), Is.False);
    }

    [Test]
    public void WithoutMergeTags_Reads_As_Plain_Yaml()
    {
        // Arrange
        var node = Parse("guard:\n  health: 50\n  speed: !remove\n");

        // Act
        var stripped = YamlTree.WithoutMergeTags(node);

        // Assert
        Assert.That(YamlTree.HasMergeTags(node), Is.True);
        Assert.That(YamlTree.ToText(stripped), Does.Not.Contain("remove"));
        Assert.That(Scalar(stripped, "guard", "health"), Is.EqualTo("50"));
    }

    [Test]
    public void DeepMerge_Replace_Tag_Replaces_A_Mapping_Whole()
    {
        // Arrange
        var target = Parse("maps:\n  MAP01:\n    par: 90\n  MAP02:\n    par: 120\nother: 1\n");
        var overlay = Parse("maps: !replace\n  MAP01:\n    next: MAP02\n");

        // Act
        YamlTree.DeepMerge(target, overlay);

        // Assert: MAP02 and MAP01's par are gone; the rest of the document is as it was
        var maps = (YamlMappingNode)target.Children[new YamlScalarNode("maps")];
        Assert.That(maps.Children.Keys.Select(k => ((YamlScalarNode)k).Value), Is.EqualTo(new[] { "MAP01" }));
        Assert.That(Scalar(target, "maps", "MAP01", "next"), Is.EqualTo("MAP02"));
        Assert.That(((YamlMappingNode)maps.Children[new YamlScalarNode("MAP01")]).Children, Has.Count.EqualTo(1));
        Assert.That(Scalar(target, "other"), Is.EqualTo("1"));
        Assert.That(YamlTree.HasMergeTags(target), Is.False, "the tag doesn't stay in the merged document");
    }

    [Test]
    public void MergeLevels_Replace_Tag_Stops_The_Merge_At_Its_Key()
    {
        // Arrange: mapdefs merge two levels down, but this thing is replaced whole
        var target = Parse("things:\n  guard:\n    id: 108\n    angle: 0\n");
        var overlay = Parse("things: !replace\n  dog:\n    id: 138\n");

        // Act
        YamlTree.MergeLevels(target, overlay, 2);

        // Assert
        var things = (YamlMappingNode)target.Children[new YamlScalarNode("things")];
        Assert.That(things.Children.Keys.Select(k => ((YamlScalarNode)k).Value), Is.EqualTo(new[] { "dog" }));
    }

    [Test]
    public void WithoutMergeTags_Takes_Off_Replace_Tags()
    {
        // Act
        var stripped = YamlTree.WithoutMergeTags(Parse("maps: !replace\n  MAP01: { par: 90 }\nname: !replace x\n"));

        // Assert
        Assert.That(YamlTree.ToText(stripped), Does.Not.Contain("!replace"));
        Assert.That(Scalar(stripped, "maps", "MAP01", "par"), Is.EqualTo("90"));
        Assert.That(Scalar(stripped, "name"), Is.EqualTo("x"));
    }

    [Test]
    public void MergeLevels_One_Replaces_Whole_Top_Level_Entries()
    {
        // Arrange
        var target = Parse("guard:\n  health: 25\n  speed: 512\n");
        var overlay = Parse("guard:\n  health: 50\n");

        // Act
        YamlTree.MergeLevels(target, overlay, 1);

        // Assert
        var guard = (YamlMappingNode)target.Children[new YamlScalarNode("guard")];
        Assert.That(guard.Children, Has.Count.EqualTo(1));
        Assert.That(Scalar(target, "guard", "health"), Is.EqualTo("50"));
    }

    [Test]
    public void MergeLevels_Two_Merges_One_Level_Down_Only()
    {
        // Arrange
        var target = Parse("things:\n  guard:\n    id: 108\n    angle: 0\n  dog:\n    id: 138\n");
        var overlay = Parse("things:\n  guard:\n    id: 999\n");

        // Act
        YamlTree.MergeLevels(target, overlay, 2);

        // Assert: dog stays, but guard is replaced whole (its angle is gone)
        var things = (YamlMappingNode)target.Children[new YamlScalarNode("things")];
        var guard = (YamlMappingNode)things.Children[new YamlScalarNode("guard")];
        Assert.That(things.Children.ContainsKey(new YamlScalarNode("dog")), Is.True);
        Assert.That(guard.Children, Has.Count.EqualTo(1));
        Assert.That(Scalar(target, "things", "guard", "id"), Is.EqualTo("999"));
    }

    [Test]
    public void Clone_Is_Independent_Of_The_Original()
    {
        // Arrange
        var original = Parse("guard:\n  health: 25\n");

        // Act
        var clone = YamlTree.Clone(original);
        YamlTree.DeepMerge(clone, Parse("guard:\n  health: 99\n"));

        // Assert
        Assert.That(Scalar(original, "guard", "health"), Is.EqualTo("25"));
        Assert.That(Scalar(clone, "guard", "health"), Is.EqualTo("99"));
    }
}
