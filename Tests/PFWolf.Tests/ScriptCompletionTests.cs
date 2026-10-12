using PFWolf.Editor.Data;
using PFWolf.Scripting;

namespace PFWolf.Tests;

/// <summary>The script editor's autocomplete: what's offered for the line up to the caret</summary>
public class ScriptCompletionTests
{
    private static readonly CompletionSources Sources = new(
        Classes: ["Fireball", "FireballRing", "Guard"],
        States: ["Spawn", "Fire", "Death"],
        Flags: ["PROJECTILE", "HURTMONSTERS"],
        PropertyKeys: ["speed", "light.color"],
        ActionCallers: new Dictionary<string, List<string>>
        {
            ["A_FireRing"] = ["FireballRing: Fire"],
            ["T_Chase"] = ["Guard: Chase"],
            ["A_Projectile(3, 10)"] = [],
        });

    private static List<string> Offered(string before, bool asked = false, params string[] scriptActions)
        => ScriptCompletion.Find(before, Sources, scriptActions, asked)?.Options.Select(option => option.Text).ToList() ?? [];

    [Test]
    public void After_Game_Dot_The_Game_Functions()
    {
        // Act
        var context = ScriptCompletion.Find("  local t = game.", Sources, [], asked: false);

        // Assert
        Assert.That(context!.PrefixLength, Is.EqualTo(0));
        Assert.That(context.Options.Select(option => option.Text), Is.EquivalentTo(ScriptApi.Game.Select(entry => entry.Name)));
        Assert.That(context.Options.First(option => option.Text == "trigger").Signature, Does.StartWith("game.trigger(tag"));
    }

    [Test]
    public void Part_Of_A_Member_Narrows_It_And_Says_How_Much_It_Replaces()
    {
        // Act
        var context = ScriptCompletion.Find("game.tag", Sources, [], asked: false);

        // Assert
        Assert.That(context!.PrefixLength, Is.EqualTo(3));
        Assert.That(context.Options.Select(option => option.Text), Is.EquivalentTo(new[] { "tagged", "taggedtiles", "tag" }));
    }

    [Test]
    public void After_A_Colon_The_Actor_Methods_And_After_A_Dot_Its_Fields()
    {
        Assert.That(Offered("self:"), Is.EquivalentTo(ScriptApi.ActorMethods.Select(entry => entry.Name)));
        Assert.That(Offered("pile:near"), Is.EqualTo(new[] { "nearestplayer" }));
        Assert.That(Offered("self."), Is.EquivalentTo(ScriptApi.ActorFields.Select(entry => entry.Name)));
        Assert.That(Offered("game.players()[1]:dist"), Is.EqualTo(new[] { "distanceto" }));
    }

    [TestCase("string.fo", "format")]
    [TestCase("math.fl", "floor")]
    [TestCase("table.ins", "insert")]
    [TestCase("bit32.ba", "band")]
    public void After_A_Library_Its_Members(string before, string expected)
    {
        Assert.That(Offered(before), Is.EqualTo(new[] { expected }));
    }

    [Test]
    public void Math_Random_Is_Not_Offered()
    {
        Assert.That(Offered("math.ra"), Is.EqualTo(new[] { "rad" }));
    }

    [Test]
    public void A_Word_Being_Typed_Offers_Globals_Libraries_Keywords_And_Actions()
    {
        Assert.That(Offered("  pr"), Is.EqualTo(new[] { "print" }));
        Assert.That(Offered("x = ga"), Is.EqualTo(new[] { "game" }));
        Assert.That(Offered("  A_"), Is.EqualTo(new[] { "A_FireRing" }));
        Assert.That(Offered("  A_F", false, "A_Fizz"), Is.EquivalentTo(new[] { "A_FireRing", "A_Fizz" }));
        Assert.That(Offered("  whi"), Is.EqualTo(new[] { "while" }));
    }

    [Test]
    public void One_Letter_Waits_Unless_Asked()
    {
        Assert.That(Offered("  p"), Is.Empty);
        Assert.That(Offered("  p", asked: true), Does.Contain("print").And.Contain("pairs").And.Contain("pcall"));
        Assert.That(Offered("", asked: true), Has.Count.GreaterThan(40));
    }

    [Test]
    public void A_Word_Typed_In_Full_Offers_Nothing_Until_Asked()
    {
        Assert.That(Offered("  print"), Is.Empty);
        Assert.That(Offered("  print", asked: true), Is.EqualTo(new[] { "print" }));
    }

    [TestCase("x = 12")]
    [TestCase("x = 3.5")]
    [TestCase("-- game.")]
    [TestCase("local y = 1 -- self:")]
    [TestCase("print('game.")]
    public void Nothing_In_Numbers_Comments_Or_Ordinary_Strings(string before)
    {
        Assert.That(Offered(before), Is.Empty);
    }

    [TestCase("self:spawnprojectile(\"", new[] { "Fireball", "FireballRing", "Guard" })]
    [TestCase("pile:spawn('Fire", new[] { "Fireball", "FireballRing" })]
    [TestCase("for _, a in ipairs(game.actors(\"G", new[] { "Guard" })]
    [TestCase("game.tagged(3, \"", new[] { "Fireball", "FireballRing", "Guard" })]
    [TestCase("self:jump(\"D", new[] { "Death" })]
    [TestCase("self:hasflag(\"HURT", new[] { "HURTMONSTERS" })]
    [TestCase("self:property('li", new[] { "light.color" })]
    public void Strings_Offer_The_Games_Names_For_What_Takes_Them(string before, string[] expected)
    {
        Assert.That(Offered(before), Is.EqualTo(expected));
    }

    [Test]
    public void Game_Trigger_Offers_The_Switch_Actions_It_Can_Run()
    {
        // Act
        var offered = Offered("game.trigger(1, \"A_");

        // Assert
        Assert.That(offered, Is.EquivalentTo(ScriptApi.TriggerActions));
        Assert.That(offered, Does.Not.Contain("A_PushWall"));
    }

    [Test]
    public void Naming_A_Function_Offers_Only_Actions_When_Asked()
    {
        Assert.That(Offered("function A_Fi"), Is.Empty);
        Assert.That(Offered("function A_Fi", asked: true), Is.EqualTo(new[] { "A_FireRing" }));
    }

    [TestCase("print(\"a\" .. 'b', \"c", 18)]
    [TestCase("x = \"it\\\"s", 4)]
    [TestCase("x = 'done' y = 2", null)]
    [TestCase("-- 'not a string", null)]
    public void InString_Finds_The_Open_Quote(string before, int? expected)
    {
        Assert.That(ScriptCompletion.InString(before), Is.EqualTo(expected));
    }
}
