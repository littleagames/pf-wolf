using PFWolf.Editor.Data;

namespace PFWolf.Tests;

/// <summary>The script editor's go-to-definition: finding the name, and the definition the game runs</summary>
public class ScriptNavigationTests
{
    [TestCase("  A_FireRing(pile, 'Fireball', 12)", 5, "A_FireRing")]
    [TestCase("  A_FireRing(pile)", 12, "A_FireRing")]     // just after the name: the caret at its end
    [TestCase("local n = helper(3)", 12, "helper")]
    [TestCase("local n = helper(3)", 2, "local")]
    public void NameAt_Finds_The_Name(string line, int column, string expected)
    {
        Assert.That(ScriptNavigation.NameAt(line, column), Is.EqualTo(expected));
    }

    [TestCase("  game.trigger(1, 'A_OpenDoor')", 8)]     // the API's
    [TestCase("  self:jump('Fire')", 8)]
    [TestCase("  print('A_FireRing')", 10)]              // in a string
    [TestCase("  -- A_FireRing", 6)]                     // in a comment
    [TestCase("  x = 360", 7)]
    [TestCase("  x = 1 + 2", 7)]
    [TestCase("", 0)]
    public void NameAt_Has_Nothing_For_The_API_Strings_Comments_Or_Numbers(string line, int column)
    {
        Assert.That(ScriptNavigation.NameAt(line, column), Is.Null);
    }

    private static readonly ScriptSource Patterns = new("patterns", 1,
        "local function helper(n)\n  return n\nend\n\nfunction A_FireRing(self)\n  helper(1)\nend\n\nfunction A_Only(self) end\n");
    private static readonly ScriptSource Override = new("override", 2, "-- a mod's change\nfunction A_FireRing(self) end\n");

    [Test]
    public void An_Action_In_This_Script_Goes_To_Its_Line()
    {
        // Act
        var target = ScriptNavigation.Find("A_Only", "patterns", [Patterns, Override], out _);

        // Assert
        Assert.That(target, Is.EqualTo(new DefinitionTarget("patterns", 9)));
    }

    [Test]
    public void An_Action_A_Later_Script_Replaces_Goes_To_That_One_And_Says_So()
    {
        // Act: from the script whose definition loses
        var target = ScriptNavigation.Find("A_FireRing", "patterns", [Patterns, Override], out _);

        // Assert
        Assert.That(target!.Script, Is.EqualTo("override"));
        Assert.That(target.Line, Is.EqualTo(2));
        Assert.That(target.Note, Does.Contain("runs later").And.Contain("the one the game uses"));
    }

    [Test]
    public void From_Elsewhere_The_Game_Uses_The_Last_And_It_Says_There_Are_More()
    {
        // Act: run order, not list order, decides
        var target = ScriptNavigation.Find("A_FireRing", "other", [Override, Patterns], out _);

        // Assert
        Assert.That(target!.Script, Is.EqualTo("override"));
        Assert.That(target.Note, Does.Contain("2 scripts"));
    }

    [Test]
    public void Another_Function_Goes_To_This_Scripts_Definition()
    {
        // Act
        var target = ScriptNavigation.Find("helper", "patterns", [Patterns, Override], out _);

        // Assert
        Assert.That(target, Is.EqualTo(new DefinitionTarget("patterns", 1)));
    }

    [TestCase("A_Projectile", "No script defines A_Projectile")]
    [TestCase("nothing", "nothing isn't a function this script defines")]
    public void What_No_Script_Defines_Says_Why(string name, string why)
    {
        // Act
        var target = ScriptNavigation.Find(name, "patterns", [Patterns, Override], out var message);

        // Assert
        Assert.That(target, Is.Null);
        Assert.That(message, Does.StartWith(why));
    }
}
