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
