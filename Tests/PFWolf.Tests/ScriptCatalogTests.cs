using PFWolf.Editor.Data;

namespace PFWolf.Tests;

/// <summary>The editor's script checks: compiling without running, and reading what a script gives</summary>
public class ScriptCatalogTests
{
    [Test]
    public void A_Script_That_Compiles_Lists_Its_Actions_And_Hooks_By_Line()
    {
        // Arrange
        var text = "-- comment\nfunction A_One(self) end\nlocal function helper() end\nT_Two = function(self) end\n"
            + "function notAnAction() end\ngame.onlevel(function(reason) end)\ngame . ontick(function(tics) end)\n";

        // Act
        var report = ScriptCatalog.Check(text, "scripts/x.lua");

        // Assert
        Assert.That(report.Compiles, Is.True);
        Assert.That(report.Problems, Is.Empty);
        Assert.That(report.Actions, Is.EqualTo(new[] { new ScriptItem("A_One", 2), new ScriptItem("T_Two", 4) }));
        Assert.That(report.Hooks, Is.EqualTo(new[] { new ScriptItem("game.onlevel", 6), new ScriptItem("game.ontick", 7) }));
    }

    [Test]
    public void A_Compile_Error_Is_An_Error_On_Its_Line_And_Nothing_Runs()
    {
        // Arrange: running it would recurse forever, so it must only be compiled
        var text = "function A_X(self)\n  if then end\nend\nwhile true do end\n";

        // Act
        var report = ScriptCatalog.Check(text, "scripts/x.lua");

        // Assert
        Assert.That(report.Compiles, Is.False);
        Assert.That(report.Problems, Has.Count.EqualTo(1));
        Assert.That(report.Problems[0].Line, Is.EqualTo(2));
        Assert.That(report.Problems[0].IsError, Is.True);
    }

    [TestCase("local n = math.random(6)", "math.random")]
    [TestCase("math.randomseed(1)", "math.randomseed")]
    [TestCase("local t = os.time()", "os.")]
    [TestCase("io.write('x')", "io.")]
    [TestCase("require('other')", "require")]
    [TestCase("dofile('x.lua')", "dofile")]
    [TestCase("local f = load('return 1')", "load")]
    public void What_The_Game_Leaves_Out_Is_A_Warning(string line, string what)
    {
        // Act
        var report = ScriptCatalog.Check($"-- first line\n{line}\n", "scripts/x.lua");

        // Assert
        Assert.That(report.Compiles, Is.True);
        Assert.That(report.Problems, Has.Count.EqualTo(1));
        Assert.That(report.Problems[0].Line, Is.EqualTo(2));
        Assert.That(report.Problems[0].IsError, Is.False);
        Assert.That(report.Problems[0].Message, Does.StartWith(what));
    }

    [TestCase("-- math.random is gone")]
    [TestCase("print('use os.time or require(x) in the docs')")]
    [TestCase("--[[ io.write\n dofile(x) ]] local x = 1")]
    [TestCase("local myos = {}; myos.x = 1; local reload = 1")]
    public void Comments_Strings_And_Lookalikes_Are_Not_Warned_About(string text)
    {
        // Act
        var report = ScriptCatalog.Check(text, "scripts/x.lua");

        // Assert
        Assert.That(report.Problems, Is.Empty);
    }

    [Test]
    public void An_Action_Defined_Twice_Says_The_Later_One_Is_Used()
    {
        // Act
        var report = ScriptCatalog.Check("function A_X() end\n\nfunction A_X() end\n", "scripts/x.lua");

        // Assert
        Assert.That(report.Problems.Select(problem => (problem.Line, problem.IsError)), Is.EqualTo(new[] { (3, false) }));
    }

    [TestCase("traps", null)]
    [TestCase("my-traps_2", null)]
    [TestCase("", "Give it a name")]
    [TestCase("my traps", "Letters, digits")]
    [TestCase("traps.lua", "Letters, digits")]
    public void New_Script_Names_Are_File_Names(string name, string? problemStart)
    {
        // Arrange
        var content = GameContent.FromDefinitions(new PFWolf.Assets.MapObjectTranslationAsset());

        // Act
        var problem = ScriptCatalog.CheckNewName(content, name);

        // Assert
        if (problemStart == null)
            Assert.That(problem, Is.Null);
        else
            Assert.That(problem, Does.StartWith(problemStart));
        Assert.That(ScriptCatalog.NewEntryPath("Traps"), Is.EqualTo("scripts/traps.lua"));
    }

    [Test]
    public void The_New_Script_Template_Compiles_Cleanly()
    {
        // Act
        var report = ScriptCatalog.Check(ScriptCatalog.NewScript, "scripts/new.lua");

        // Assert
        Assert.That(report.Problems, Is.Empty);
        Assert.That(report.Actions.Select(action => action.Name), Is.EqualTo(new[] { "A_Hello" }));
        Assert.That(report.Hooks, Has.Count.EqualTo(2));
    }
}
