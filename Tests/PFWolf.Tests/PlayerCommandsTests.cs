namespace PFWolf.Tests;

public class PlayerCommandsTests
{
    private ConsoleManager _console = null!;
    private GameSession _session = null!;
    private Deathmatch _deathmatch = null!;
    private PlayerCommands _commands = null!;

    [SetUp]
    public void Make()
    {
        _console = new ConsoleManager();
        _session = new GameSession();
        _deathmatch = new Deathmatch(null!, null!, null!, _session, null!);
        _commands = new PlayerCommands(_console, null!, null!, null!, null!, null!, _deathmatch, _session,
            new Configuration.GameSettings { viewsize = 21 }, new PlayerCommandsHooks(() => { }));
    }

    [Test]
    public void With_One_Player_The_Mode_Shows_But_Cant_Change()
    {
        // Act / Assert
        _commands.GameModeCommand([]);
        Assert.That(_console.Scrollback[^1], Is.EqualTo("Game mode: single, 1 player(s)"));
        Assert.Throws<ArgumentException>(() => _commands.GameModeCommand(["coop"]));
    }

    [Test]
    public void With_Two_A_Deathmatch_Takes_Its_Frag_Limit()
    {
        // Arrange
        _session.players.Add(new Entities.PlayerState { Number = 1 });

        // Act
        _commands.GameModeCommand(["dm", "15"]);

        // Assert
        Assert.That(_session.gamemode, Is.EqualTo(GameMode.Deathmatch));
        Assert.That(_deathmatch.netrules.ScoreLimit, Is.EqualTo(15));
        Assert.That(_console.Scrollback[^1], Is.EqualTo("Game mode: deathmatch, 2 player(s)"));
    }

    [TestCase("0")]
    [TestCase("3")]
    [TestCase("x")]
    public void Only_A_Player_There_Is_Can_Be_Taken_Over(string n)
    {
        _session.players.Add(new Entities.PlayerState { Number = 1 });
        Assert.Throws<ArgumentException>(() => _commands.ControlPlayerCommand([n]));
    }

    [Test]
    public void Taking_Over_Another_Player_Makes_Them_Yours()
    {
        // Arrange
        _session.players.Add(new Entities.PlayerState { Number = 1, playerclass = "Guard" });

        // Act
        _commands.ControlPlayerCommand(["2"]);

        // Assert
        Assert.That(_session.consoleplayer, Is.EqualTo(1));
        Assert.That(_console.Scrollback[^1], Is.EqualTo("You're player 2 (Guard)"));
    }

    [Test]
    public void There_Can_Only_Be_So_Many_Players()
    {
        while (_session.players.Count < PlayerCommands.MaxPlayers)
            _session.players.Add(new Entities.PlayerState { Number = _session.players.Count });

        Assert.Throws<ArgumentException>(() => _commands.AddPlayerCommand([]));
    }
}
