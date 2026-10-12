using PFWolf.Editor.Definitions;
using PFWolf.Editor.ViewModels;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Tests;

/// <summary>The definitions editor's animdefs: merged as the game merges them, edited in fields, and saved as overlays</summary>
public class AnimationDefinitionsEditorTests
{
    private const string Mod = @"C:\mods\mymod";

    private static DefinitionSet Animations(params DefinitionFile[] mods)
        => new(DefinitionKind.Animations, "wolf3d",
        [
            new DefinitionFile(null, "animdefs/animdefs.yaml", DefinitionFile.SharedLayer,
                "SLIME:\n  tics: 8\n  frames: [SLIME1, SLIME2]\nLAVA:\n  frames: [LAVA1, LAVA2]\n"),
            new DefinitionFile(null, "animdefs/wolf3d/animdefs.yaml", "", "SLIME:\n  tics: 4\n  frames: [WOLFSLIME1, WOLFSLIME2]\n"),
            .. mods,
        ]);

    private static string? Scalar(YamlNode? node, params string[] path) => (YamlNodes.Get(node, path) as YamlScalarNode)?.Value;

    [Test]
    public void The_Packs_Animation_Replaces_A_Shared_One_Of_Its_Name()
    {
        // Act
        var set = Animations();

        // Assert
        Assert.That(Scalar(set.Merged, "SLIME", "tics"), Is.EqualTo("4"));
        Assert.That(YamlText.Inline(YamlNodes.Get(set.Merged, ["SLIME", "frames"])!), Does.Contain("WOLFSLIME1"));
        Assert.That(set.Entries(baseOnly: false).Select(entry => entry.Name), Is.EquivalentTo(new[] { "SLIME", "LAVA" }));
    }

    [Test]
    public void A_Mod_Changes_The_Packs_Animation_Key_By_Key_But_Cannot_Remove_A_Shared_One()
    {
        // Arrange: as the game loads it, a mod's file merges into the pack's animations, not the shared ones
        var set = Animations(new DefinitionFile(Mod, "animdefs/mine.yaml", "", "SLIME:\n  tics: 2\nLAVA: !remove\n"));

        // Act / Assert
        Assert.That(Scalar(set.Merged, "SLIME", "tics"), Is.EqualTo("2"));
        Assert.That(YamlText.Inline(YamlNodes.Get(set.Merged, ["SLIME", "frames"])!), Does.Contain("WOLFSLIME1"));
        Assert.That(YamlNodes.Get(set.Merged, ["LAVA"]), Is.Not.Null);
    }

    [Test]
    public void Editing_A_Shared_Animation_In_A_Mod_Writes_It_Whole()
    {
        // Arrange
        var set = Animations();
        var edited = (YamlMappingNode)YamlNodes.Clone(set.ValueOf(["LAVA"], baseOnly: false)!);
        YamlNodes.Set(edited, ["tics"], new YamlScalarNode("12"));

        // Act
        var write = set.PlanWrite(["LAVA"], edited, Mod);

        // Assert: the pack has no LAVA for a part of it to merge into
        Assert.That(write.File.EntryPath, Is.EqualTo("animdefs/animdefs.yaml"));
        Assert.That(Scalar(write.Value, "tics"), Is.EqualTo("12"));
        Assert.That(YamlNodes.Get(write.Value, ["frames"]), Is.Not.Null);
    }

    [Test]
    public void Editing_The_Packs_Animation_In_A_Mod_Writes_Only_What_Changes()
    {
        // Arrange
        var set = Animations();
        var edited = (YamlMappingNode)YamlNodes.Clone(set.ValueOf(["SLIME"], baseOnly: false)!);
        YamlNodes.Set(edited, ["tics"], new YamlScalarNode("6"));

        // Act
        var write = set.PlanWrite(["SLIME"], edited, Mod);

        // Assert
        Assert.That(Scalar(write.Value, "tics"), Is.EqualTo("6"));
        Assert.That(YamlNodes.Get(write.Value, ["frames"]), Is.Null);
    }

    [Test]
    public void Frames_Of_Every_Kind_Build_Back_Unchanged_In_Fields()
    {
        // Arrange: a name, one with its own tics, and stop
        var value = YamlNodes.Get(YamlText.ParseRoot("A:\n  tics: 5\n  frames:\n    - ONE\n    - { name: TWO, tics: 16 }\n    - stop\n"), ["A"]);
        var node = new FieldNode(DefinitionSchemas.Animation, "A", value, null, null, new FieldContext(), isEntry: true);
        node.EnsureChildren();
        foreach (var child in node.Children)
            child.EnsureChildren();

        // Act
        var errors = new List<string>();
        var built = node.Build(errors);

        // Assert
        Assert.That(errors, Is.Empty);
        Assert.That(YamlNodes.Equal(built, value), Is.True, YamlText.Inline(built!));
        var frames = node.Children.Single(child => child.Key == "frames");
        Assert.That(frames.IsGroup, Is.True, "a row per frame, not one line");
        Assert.That(frames.Children.Select(frame => frame.Kind), Is.EqualTo(new[] { FieldKind.Text, FieldKind.Raw, FieldKind.Text }));
    }

    [Test]
    public void The_Art_Browsers_Preview_Loops_Or_Holds_The_Last_Frame_Then_Starts_Over()
    {
        // Arrange: 10 and 20 tics
        var loops = new PFWolf.Editor.Data.ArtAnimation([("A", 10), ("B", 20)], Loops: true);
        var once = loops with { Loops = false };

        // Act / Assert
        Assert.That(new[] { 0, 9, 10, 29, 30, 45 }.Select(loops.FrameAt), Is.EqualTo(new[] { 0, 0, 1, 1, 0, 1 }));
        int held = 30 + PFWolf.Editor.Data.ArtAnimation.HoldTics;
        Assert.That(new[] { 0, 10, 30, held - 1, held }.Select(once.FrameAt), Is.EqualTo(new[] { 0, 1, 1, 1, 0 }));
    }

    [Test]
    public void A_Mods_Animdefs_File_Is_Read_As_Animations()
    {
        // Act / Assert
        Assert.That(YamlFileTypes.KindOf("animdefs/mine.yaml", isMod: true), Is.EqualTo(DefinitionKind.Animations));
        Assert.That(YamlFileTypes.LoadedPath("animdefs/mine.yaml", isMod: true, "wolf3d"), Is.EqualTo("animdefs/wolf3d/mine.yaml"));
        Assert.That(YamlFileTypes.ModPathFor("animdefs/wolf3d/water.yaml", out _), Is.EqualTo("animdefs/water.yaml"));
        Assert.That(YamlFileTypes.Check("animdefs/mine.yaml", isMod: true, "A:\n  tics: lots\n"), Is.Not.Null);
        Assert.That(YamlFileTypes.Check("animdefs/mine.yaml", isMod: true, "A:\n  frames: [X, { name: Y, tics: 3 }, stop]\n"), Is.Null);
    }
}
