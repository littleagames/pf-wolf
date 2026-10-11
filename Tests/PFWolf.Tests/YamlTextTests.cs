using PFWolf.Editor.Definitions;
using PFWolf.Loaders;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Tests;

/// <summary>The definitions editor's YAML: changing a file's text in place, and working out overlays</summary>
public class YamlTextTests
{
    private static readonly string Walls = """
        # Walls by map tile number
        walls:
            '1':
                North: GSTONEA1   # grey stone
                South: GSTONEA1
            '2':
                North: GSTONEB1
                South: GSTONEB1
        doors:
            '90': { North: DOOR1, South: DOOR1 }
        """.Replace("\r\n", "\n");

    private static YamlMappingNode Parse(string text) => YamlText.ParseRoot(text)!;

    private static string Value(string text, params string[] path) => ((YamlScalarNode)YamlNodes.Get(Parse(text), path)!).Value!;

    [Test]
    public void Set_Changes_Only_The_Value_Keeping_Its_Comment()
    {
        // Act
        var text = YamlText.Set(Walls, ["walls", "1", "North"], new YamlScalarNode("REDBRICK"));

        // Assert
        Assert.That(text, Is.EqualTo(Walls.Replace("North: GSTONEA1   # grey stone", "North: REDBRICK   # grey stone")));
    }

    [Test]
    public void Set_Adds_A_Key_In_The_Mappings_Indentation()
    {
        // Act
        var text = YamlText.Set(Walls, ["walls", "2", "East"], new YamlScalarNode("GSTONEB2"));

        // Assert
        Assert.That(text, Does.Contain("        South: GSTONEB1\n        East: GSTONEB2\ndoors:"));
        Assert.That(Value(text, "walls", "2", "East"), Is.EqualTo("GSTONEB2"));
    }

    [Test]
    public void Set_Adds_An_Entry_Quoted_As_Its_Siblings_Are()
    {
        // Act
        var text = YamlText.Set(Walls, ["walls", "3", "North"], new YamlScalarNode("BLUWALL"));

        // Assert
        Assert.That(text, Does.Contain("    '3':\n        North: BLUWALL\ndoors:"));
    }

    [Test]
    public void Set_Rewrites_A_Flow_Mapping_Whole()
    {
        // Act
        var text = YamlText.Set(Walls, ["doors", "90", "lock"], new YamlScalarNode("GoldKey"));

        // Assert
        Assert.That(text, Does.Contain("'90': { North: DOOR1, South: DOOR1, lock: GoldKey }"));
    }

    [Test]
    public void Set_Adds_A_Top_Level_Key_At_The_End()
    {
        // Act
        var text = YamlText.Set(Walls, ["floors", "ambush"], new YamlScalarNode("106"));

        // Assert
        Assert.That(text, Does.EndWith("floors:\n    ambush: 106\n"));
        Assert.That(Value(text, "floors", "ambush"), Is.EqualTo("106"));
    }

    [Test]
    public void Set_Into_An_Empty_File_Writes_The_Path()
    {
        // Act
        var text = YamlText.Set("# a mod's mapdefs\n", ["walls", "5", "North"], new YamlScalarNode("X"));

        // Assert
        Assert.That(text, Is.EqualTo("# a mod's mapdefs\nwalls:\n  '5':\n    North: X\n".Replace("'5'", "5")));
    }

    [Test]
    public void Removing_The_Last_Key_Takes_Out_The_Mappings_It_Empties()
    {
        // Arrange
        var text = YamlText.Set(Walls, ["walls", "2"], null);
        text = YamlText.Set(text, ["walls", "1", "South"], null);

        // Act
        text = YamlText.Set(text, ["walls", "1", "North"], null);

        // Assert
        Assert.That(text, Is.EqualTo("# Walls by map tile number\ndoors:\n    '90': { North: DOOR1, South: DOOR1 }"));
    }

    [Test]
    public void Set_Writes_Lists_Under_Their_Key_And_Keeps_The_Keys_Comment()
    {
        // Arrange
        var actors = """
            Guard:
              parent: Monster
              states:
                Spawn: # s_grdstand
                  - sprite: GARD
                    frames: [A]
                    tics-per-frame: -1
                Path:
                  - sprite: GARD
            """.Replace("\r\n", "\n");
        var spawn = new YamlSequenceNode(
            new YamlMappingNode { { "sprite", "GARD" }, { "frames", new YamlSequenceNode(new YamlScalarNode("A"), new YamlScalarNode("B")) }, { "tics-per-frame", "10" } },
            new YamlMappingNode { { "goto", "Spawn" } });

        // Act
        var text = YamlText.Set(actors, ["Guard", "states", "Spawn"], spawn);

        // Assert
        Assert.That(text, Does.Contain("""
                Spawn: # s_grdstand
                  - sprite: GARD
                    frames: [A, B]
                    tics-per-frame: 10
                  - goto: Spawn
                Path:
            """.Replace("\r\n", "\n")));
        Assert.That(YamlNodes.Equal(YamlNodes.Get(Parse(text), ["Guard", "states", "Spawn"]), spawn), Is.True);
    }

    [Test]
    public void Remove_Tag_Is_Written_Bare()
    {
        // Act
        var text = YamlText.Set("Guard:\n  radius: 32\n", ["Guard", "flags"], YamlNodes.Remove());

        // Assert
        Assert.That(text, Is.EqualTo("Guard:\n  radius: 32\n  flags: !remove\n"));
        Assert.That(YamlTree.IsRemove(YamlNodes.Get(Parse(text), ["Guard", "flags"])!), Is.True);
    }

    [TestCase("plain", "plain")]
    [TestCase("-1", "-1")]
    [TestCase("$GOTCLIP", "$GOTCLIP")]
    [TestCase("a: b", "'a: b'")]
    [TestCase("#717171", "'#717171'")]
    [TestCase("it's", "it's")]
    [TestCase("'quoted'", "\"'quoted'\"")]
    [TestCase("", "''")]
    [TestCase("two\nlines", "\"two\\nlines\"")]
    public void Scalars_Are_Quoted_Only_When_They_Must_Be(string value, string written)
    {
        // Act
        var text = YamlText.Scalar(new YamlScalarNode(value), inFlow: false);

        // Assert
        Assert.That(text, Is.EqualTo(written));
    }

    [Test]
    public void Sync_Changes_Only_What_Differs()
    {
        // Arrange
        var edited = (YamlMappingNode)YamlNodes.Clone(YamlNodes.Get(Parse(Walls), ["walls", "1"])!);
        YamlNodes.Set(edited, ["South"], new YamlScalarNode("REDBRICK"));
        YamlNodes.Set(edited, ["North"], null);

        // Act
        var text = YamlText.Sync(Walls, ["walls", "1"], edited);

        // Assert
        Assert.That(text, Does.Contain("    '1':\n        South: REDBRICK\n    '2':"));
        Assert.That(text, Does.StartWith("# Walls by map tile number\n"));
    }

    [Test]
    public void Sync_Of_The_Whole_File_Keeps_Unchanged_Lines()
    {
        // Arrange
        var edited = Parse(Walls);
        YamlNodes.Set(edited, ["doors"], null);

        // Act
        var text = YamlText.Sync(Walls, [], edited);

        // Assert
        Assert.That(text, Is.EqualTo(Walls[..Walls.IndexOf("doors:")]));
    }

    [Test]
    public void Crlf_Files_Stay_Crlf()
    {
        // Arrange
        var crlf = Walls.Replace("\n", "\r\n");

        // Act
        var text = YamlText.Set(crlf, ["walls", "2", "East"], new YamlScalarNode("GSTONEB2"));

        // Assert
        Assert.That(text.Replace("\r\n", ""), Does.Not.Contain("\n"));
        Assert.That(Value(text, "walls", "2", "East"), Is.EqualTo("GSTONEB2"));
    }

    [Test]
    public void Overlay_Holds_What_Changed_And_Removes_What_Was_Taken_Out()
    {
        // Arrange
        var baseTree = Parse("Guard:\n  parent: Monster\n  radius: 32\n  properties:\n    health: 25\n    speed: 512\n");
        var edited = (YamlMappingNode)YamlNodes.Clone(baseTree);
        YamlNodes.Set(edited, ["Guard", "properties", "health"], new YamlScalarNode("40"));
        YamlNodes.Set(edited, ["Guard", "radius"], null);

        // Act
        var overlay = (YamlMappingNode)YamlNodes.Overlay(baseTree, edited)!;
        var merged = YamlTree.Clone(baseTree);
        YamlTree.DeepMerge(merged, overlay);

        // Assert
        Assert.That(YamlNodes.Get(overlay, ["Guard", "properties", "health"]), Is.EqualTo(new YamlScalarNode("40")));
        Assert.That(YamlNodes.Get(overlay, ["Guard", "properties", "speed"]), Is.Null);
        Assert.That(YamlTree.IsRemove(YamlNodes.Get(overlay, ["Guard", "radius"])!), Is.True);
        Assert.That(YamlNodes.Equal(merged, edited), Is.True);
    }

    [Test]
    public void Overlay_Sets_Values_Whole_At_The_Merge_Depth()
    {
        // Arrange
        var baseTree = Parse("walls:\n  '1': { North: A, South: A }\n");
        var edited = (YamlMappingNode)YamlNodes.Clone(baseTree);
        YamlNodes.Set(edited, ["walls", "1", "North"], new YamlScalarNode("B"));

        // Act
        var overlay = YamlNodes.Overlay(baseTree, edited, wholeAtDepth: 2);

        // Assert
        Assert.That(YamlNodes.Equal(YamlNodes.Get(overlay, ["walls", "1"]), YamlNodes.Get(edited, ["walls", "1"])), Is.True);
    }

    [Test]
    public void Equal_Ignores_Quoting_And_Key_Order()
    {
        // Act
        var equal = YamlNodes.Equal(Parse("a: '1'\nb: [x]\n"), Parse("b:\n  - x\na: 1\n"));

        // Assert
        Assert.That(equal, Is.True);
    }

    [Test]
    public void Equal_Tells_Tags_Apart()
    {
        // Act
        var equal = YamlNodes.Equal(new YamlScalarNode(""), YamlNodes.Remove());

        // Assert
        Assert.That(equal, Is.False);
        Assert.That(new YamlScalarNode("") { Style = ScalarStyle.Plain }.Tag.IsEmpty, Is.True);
    }
}
