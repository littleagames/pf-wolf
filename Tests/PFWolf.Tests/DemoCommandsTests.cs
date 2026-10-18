namespace PFWolf.Tests;

public class DemoCommandsTests
{
    private ConsoleManager _console = null!;
    private DemoCommands _commands = null!;

    [SetUp]
    public void Make()
    {
        _console = new ConsoleManager();
        _commands = new DemoCommands(_console, null!, null!, new DemoBuffer(), new GameSession { InGame = true });
    }

    [Test]
    public void Playdemo_Needs_A_Demo()
    {
        Assert.Throws<ArgumentException>(() => _commands.PlayDemoCommand([]));
    }

    [Test]
    public void Recorddemo_Needs_A_Map()
    {
        Assert.Throws<ArgumentException>(() => _commands.RecordDemoCommand([]));
    }

    [Test]
    public void Demotest_Doesnt_Run_During_A_Game()
    {
        _commands.DemoTestCommand([]);

        Assert.That(_console.Scrollback[^1], Is.EqualTo("demotest runs from the title or --exec, not during a game"));
        Assert.That(_commands.DemoTesting, Is.False);
    }
}