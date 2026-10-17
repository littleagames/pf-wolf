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
    public void Autosave_Shows_And_Sets_Whether_New_Levels_Save_Themselves()
    {
        // Act / Assert
        _console.Execute("autosave");
        Assert.That(_console.Scrollback[^1], Is.EqualTo("autosave is 1"));
        _console.Execute("autosave 0");
        Assert.That((_saves.autosaveEnabled, _console.Scrollback[^1]), Is.EqualTo((false, "autosave is 0")));
    }

    [Test]
    public void Load_Needs_A_Save_To_Load()
    {
        Assert.Throws<ArgumentException>(() => _commands.Cmd_Load([]));
    }
}