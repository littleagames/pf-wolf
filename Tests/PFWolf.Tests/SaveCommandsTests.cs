namespace PFWolf.Tests;

public class SaveCommandsTests
{
    private ConsoleManager _console = null!;
    private GameSaves _saves = null!;
    private SaveCommands _commands = null!;

    [SetUp]
    public void Make()
    {
        _console = new ConsoleManager();
        _saves = new GameSaves(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            null!, null!, new Configuration.GameSettings(), new GameSession(), null!);
        _commands = new SaveCommands(_console, null!, _saves, null!, new GameSession());
        _commands.Register();
    }

    [Test]
    public void Load_Needs_A_Save_To_Load()
    {
        Assert.Throws<ArgumentException>(() => _commands.LoadCommand([]));
    }
}