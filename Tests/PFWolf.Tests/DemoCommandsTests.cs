namespace PFWolf.Tests;

public class DemoCommandsTests
{
    private ConsoleManager _console = null!;
    private DemoCommands _commands = null!;

    [SetUp]
    public void Make()
    {
        _console = new ConsoleManager();
        _commands = new DemoCommands(_console, null!, null!, new DemoBuffer(), new GameSession(),
            new DemoCommandsHooks(() => true, _ => { }));
    }

    [TestCase("")]
    [TestCase("10")]
    [TestCase("x")]
    public void Playdemo_Takes_A_Demo_Number(string arg)
    {
        Assert.Throws<ArgumentException>(() => _commands.Cmd_PlayDemo(arg == "" ? [] : [arg]));
    }

    [Test]
    public void Recorddemo_Needs_A_Map()
    {
        Assert.Throws<ArgumentException>(() => _commands.Cmd_RecordDemo([]));
    }

    [Test]
    public void Demotest_Doesnt_Run_During_A_Game()
    {
        _commands.Cmd_DemoTest([]);

        Assert.That(_console.Scrollback[^1], Is.EqualTo("demotest runs from the title or --exec, not during a game"));
        Assert.That(_commands.demoTesting, Is.False);
    }
}