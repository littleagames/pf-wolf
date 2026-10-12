using Lua;
using PFWolf.Scripting;

namespace PFWolf.Tests;

/// <summary>The Lua host on its own: the sandbox, loading, calls and what happens when scripts go wrong</summary>
public class ScriptHostTests
{
    // What the host printed (NUnit keeps one instance for every test, so it's emptied before each)
    private readonly List<string> _printed = [];

    [SetUp]
    public void ClearPrinted() => _printed.Clear();

    private ScriptHost NewHost() => new(_printed.Add);

    [TestCase("os")]
    [TestCase("io")]
    [TestCase("require")]
    [TestCase("dofile")]
    [TestCase("loadfile")]
    [TestCase("load")]
    [TestCase("debug")]
    [TestCase("package")]
    [TestCase("math.random")]
    [TestCase("math.randomseed")]
    public void What_Reaches_Outside_The_Game_Is_Not_There(string global)
    {
        // Arrange
        var host = NewHost();

        // Act
        bool loaded = host.Load($"print(type({global}))", "test");

        // Assert
        Assert.That(loaded, Is.True);
        Assert.That(_printed, Is.EqualTo(new[] { "nil" }));
    }

    [Test]
    public void The_Libraries_Scripts_Need_Are_There()
    {
        // Arrange
        var host = NewHost();

        // Act
        host.Load("print(string.rep('a', 3), table.concat({1, 2}, ','), math.floor(2.5), bit32.band(6, 3))", "test");

        // Assert
        Assert.That(_printed, Is.EqualTo(new[] { "aaa\t1,2\t2\t2" }));
    }

    [Test]
    public void A_Script_That_Does_Not_Compile_Is_Left_Out_With_Where_And_Why()
    {
        // Arrange
        var host = NewHost();

        // Act
        bool loaded = host.Load("function A_X(self)\n  if then end\nend\n", "mymod: scripts/broken.lua");

        // Assert
        Assert.That(loaded, Is.False);
        Assert.That(host.Loaded, Is.Empty);
        Assert.That(host.Failed, Has.Count.EqualTo(1));
        Assert.That(host.Failed[0], Does.StartWith("mymod: scripts/broken.lua:2:"));
    }

    [Test]
    public void GlobalFunctionNames_Are_Only_Functions_With_The_Prefixes()
    {
        // Arrange
        var host = NewHost();
        host.Load("function A_One() end\nfunction T_Two() end\nfunction helper() end\nA_NotAFunction = 3\n", "test");

        // Act
        var names = host.GlobalFunctionNames("A_", "T_").ToList();

        // Assert
        Assert.That(names, Is.EquivalentTo(new[] { "A_One", "T_Two" }));
    }

    [Test]
    public void Call_Passes_The_Arguments_And_Globals_Persist()
    {
        // Arrange
        var host = NewHost();
        host.Load("total = 0\nfunction A_Add(n, s) total = total + n; print(total, s) end\n", "test");

        // Act
        bool first = host.Call("A_Add", [2, "x"]);
        bool second = host.Call("A_Add", [3, "y"]);

        // Assert
        Assert.That(first && second, Is.True);
        Assert.That(_printed, Is.EqualTo(new[] { "2\tx", "5\ty" }));
    }

    [Test]
    public void A_Runtime_Error_Is_Shown_With_Its_Line_Then_Kept_Quiet()
    {
        // Arrange
        var host = NewHost();
        host.Load("function A_Bad()\n  local t = nil\n  return t.y\nend\nfunction A_Good() print('fine') end\n", "mymod: scripts/bad.lua");

        // Act
        var results = Enumerable.Range(0, 5).Select(_ => host.Call("A_Bad", [])).ToList();
        bool good = host.Call("A_Good", []);

        // Assert: shown three times, the last saying so, and the state still works
        Assert.That(results, Is.All.False);
        Assert.That(good, Is.True);
        Assert.That(_printed.Count(line => line.StartsWith("Script A_Bad: mymod: scripts/bad.lua:3:")), Is.EqualTo(3));
        Assert.That(_printed.Last(line => line.StartsWith("Script A_Bad")), Does.EndWith("(its later errors aren't shown)"));
        Assert.That(_printed[^1], Is.EqualTo("fine"));
    }

    [Test]
    public void A_Script_Stuck_In_A_Loop_Is_Stopped()
    {
        // Arrange
        var host = NewHost();
        host.Load("function A_Loop() while true do end end\nfunction A_Short() local n = 0 for i = 1, 1000 do n = n + i end print(n) end\n", "test");

        // Act
        bool looped = host.Call("A_Loop", []);
        bool shortRun = host.Call("A_Short", []);

        // Assert: the next call starts its count again
        Assert.That(looped, Is.False);
        Assert.That(_printed[0], Does.Contain("so it was stopped"));
        Assert.That(shortRun, Is.True);
        Assert.That(_printed[^1], Is.EqualTo("500500"));
    }

    [Test]
    public void Calling_A_Missing_Function_Does_Nothing()
    {
        // Arrange
        var host = NewHost();

        // Act / Assert
        Assert.That(host.Call("A_Nothing", []), Is.False);
        Assert.That(_printed, Is.Empty);
    }

    [Test]
    public void Execute_Returns_The_Error_Or_Null()
    {
        // Arrange
        var host = NewHost();

        // Act / Assert
        Assert.That(host.Execute("x = 1 + 1"), Is.Null);
        Assert.That(host.Globals["x"].Read<double>(), Is.EqualTo(2));
        Assert.That(host.Execute("oops("), Does.StartWith("console:1:"));
    }

    [Test]
    public void Describe_Leaves_Just_The_Script_Name_And_Line()
    {
        // Arrange
        var host = NewHost();
        host.Load("function A_E() error('boom') end", "mymod: scripts/e.lua");

        // Act
        host.Call("A_E", []);

        // Assert
        Assert.That(_printed[0], Is.EqualTo("Script A_E: mymod: scripts/e.lua:1: boom"));
    }
}
