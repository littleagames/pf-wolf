using PFWolf.Editor.Data;

namespace PFWolf.Tests;

/// <summary>The script editor's tooltips: what the word under the pointer is</summary>
public class ScriptHoverTests
{
    private static readonly CompletionSources Sources = new(
        Classes: ["Fireball", "Guard"],
        States: ["Spawn", "Fire"],
        Flags: [],
        PropertyKeys: [],
        ActionCallers: new Dictionary<string, List<string>>
        {
            ["A_FireRing"] = ["FireballRing: Fire"],
            ["A_Projectile"] = ["Fireball: Spawn"],
        });

    private static readonly ScriptItem[] ScriptActions = [new("A_FireRing", 30), new("A_Lonely", 40)];

    // What's under the pointer at the first character of `word` in the line
    private static HoverInfo? Over(string line, string word, int skip = 0)
    {
        int at = -1;
        for (int i = 0; i <= skip; i++)
            at = line.IndexOf(word, at + 1, StringComparison.Ordinal);
        return ScriptHover.Describe(line, at, Sources, ScriptActions);
    }

    [TestCase("  game.trigger(3, 'A_OpenDoor')", "trigger", "game.trigger(tag, action, ...)", "game function")]
    [TestCase("local p = self:nearestplayer()", "nearestplayer", "actor:nearestplayer()", "actor method")]
    [TestCase("self.temp1 = 3", "temp1", "actor.temp1", "actor field")]
    [TestCase("x = math.floor(y)", "floor", "math.floor(x)", "math function")]
    [TestCase("print(string.format('%d', 1))", "format", "string.format(format, ...)", "string function")]
    [TestCase("for _, a in ipairs(t) do end", "ipairs", "ipairs(t)", "global")]
    [TestCase("x = game.time()", "game", "game", "library")]
    [TestCase("local d = game.players()[1]:distanceto(self)", "distanceto", "actor:distanceto(other)", "actor method")]
    public void The_Game_API_And_Lua_Say_What_They_Are(string line, string word, string signature, string kind)
    {
        // Act
        var info = Over(line, word);

        // Assert
        Assert.That(info, Is.Not.Null);
        Assert.That(info!.Signature, Is.EqualTo(signature));
        Assert.That(info.Kind, Is.EqualTo(kind));
        Assert.That(info.Summary, Is.Not.Empty);
    }

    [Test]
    public void The_Middle_Of_A_Word_Is_The_Word()
    {
        // Act
        var info = ScriptHover.Describe("game.trigger(1)", "game.trig".Length, Sources, ScriptActions);

        // Assert
        Assert.That(info!.Signature, Does.StartWith("game.trigger"));
    }

    [Test]
    public void A_Scripts_Own_Action_Says_Its_Line_And_Callers()
    {
        // Act
        var own = Over("function A_FireRing(self) end", "A_FireRing");
        var lonely = Over("A_Lonely(self)", "A_Lonely");
        var actordefs = Over("A_Projectile(self)", "A_Projectile");

        // Assert
        Assert.That(own!.Summary, Does.Contain("line 30").And.Contain("FireballRing: Fire"));
        Assert.That(lonely!.Summary, Does.Contain("line 40").And.Contain("No actordefs state calls it"));
        Assert.That(actordefs!.Summary, Does.StartWith("An action actordefs states call").And.Contain("Fireball: Spawn"));
        Assert.That(Over("A_Nobody(self)", "A_Nobody"), Is.Null);
    }

    [TestCase("self:spawnprojectile(\"Fireball\")", "Fireball", "class")]
    [TestCase("game.trigger(1, 'A_OpenDoor')", "OpenDoor", "switch action")]
    [TestCase("game.trigger(1, 'A_PushWall')", "PushWall", "switch action")]
    [TestCase("self:jump(\"Fire\")", "Fire", "state")]
    public void Names_In_Strings_Say_What_They_Are(string line, string word, string kind)
    {
        // Act
        var info = Over(line, word);

        // Assert
        Assert.That(info!.Kind, Is.EqualTo(kind));
    }

    [Test]
    public void A_Switch_Action_Says_How_Its_Called_And_What_It_Does()
    {
        // Act: from the actions reference (Definitions/GameActions.cs)
        var info = Over("game.trigger(1, 'A_OpenDoor')", "OpenDoor");

        // Assert
        Assert.That(info!.Summary, Does.Contain("A_OpenDoor([\"hold\"])").And.Contain("Opens the tagged doors"));
    }

    [Test]
    public void A_Switch_Action_Game_Trigger_Cannot_Run_Says_So()
    {
        Assert.That(Over("game.trigger(1, 'A_PushWall')", "PushWall")!.Summary, Does.Contain("can't run it"));
    }

    [TestCase("x = math.random(6)", "random")]
    [TestCase("local t = os.time()", "os")]
    [TestCase("require('x')", "require")]
    public void What_The_Game_Leaves_Out_Says_So(string line, string word)
    {
        // Act
        var info = Over(line, word);

        // Assert
        Assert.That(info!.Kind, Is.EqualTo("missing"));
        Assert.That(info.Summary, Does.StartWith("Not in the game's scripts"));
    }

    [TestCase("local count = 3", "count")]
    [TestCase("x = 360", "360")]
    [TestCase("-- game.trigger in a comment", "trigger")]
    [TestCase("print('just text')", "just")]
    [TestCase("local t = {} t.x = 1", "x", 1)]
    [TestCase("if x then end", "then")]
    public void Anything_Else_Has_No_Tooltip(string line, string word, int skip = 0)
    {
        Assert.That(Over(line, word, skip), Is.Null);
    }

    [TestCase(-1)]
    [TestCase(99)]
    public void Off_The_Line_Has_No_Tooltip(int column)
    {
        Assert.That(ScriptHover.Describe("game.time()", column, Sources, ScriptActions), Is.Null);
    }
}
