namespace PFWolf.Tests;

public class CheatCommandsTests
{
    private const string Yaml = """
        Player: {}
        Guard:
          parent: Player
        """;

    private ConsoleManager _console = null!;
    private byte _god, _noclip;
    private string? _newClass;
    private playstatetypes? _ended;

    [SetUp]
    public void Make()
    {
        _console = new ConsoleManager { LevelLoaded = () => true, CheatsEnabled = () => true };
        (_god, _noclip, _newClass, _ended) = (0, 0, null, null);
        var session = new GameSession();
        var inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
        inventory.SetHolder(session.playerstate.Items);
        new CheatCommands(_console, null!, null!, inventory, null!, null!, null!, null!, new PlayerClassRules(null!, inventory, session),
            null!, null!, null!, session,
            new CheatCommandsHooks(() => _god, g => _god = g, () => _noclip, n => _noclip = n, () => false, s => _ended = s,
                () => _newClass, c => _newClass = c, _ => { })).Register();
    }

    [Test]
    public void God_Toggles_And_Takes_A_Level()
    {
        _console.Execute("god");
        Assert.That(_god, Is.EqualTo(1));
        _console.Execute("god 2");
        Assert.That(_god, Is.EqualTo(2));
        _console.Execute("god");
        Assert.That(_god, Is.EqualTo(0));
    }

    [Test]
    public void Noclip_Toggles()
    {
        _console.Execute("noclip");
        Assert.That((_noclip, _console.Scrollback[^1]), Is.EqualTo(((byte)1, "No clipping ON")));
        _console.Execute("noclip");
        Assert.That(_noclip, Is.EqualTo(0));
    }

    [Test]
    public void Exitlevel_Completes_The_Level()
    {
        _console.Execute("exitlevel");
        Assert.That(_ended, Is.EqualTo(playstatetypes.ex_completed));
    }

    [TestCase("give")]
    [TestCase("map")]
    [TestCase("warp")]
    [TestCase("tp")]
    [TestCase("killall")]
    [TestCase("diag")]
    [TestCase("summon")]
    [TestCase("pitch")]
    [TestCase("eyeheight")]
    [TestCase("hurt")]
    [TestCase("playerclass")]
    public void Each_Command_Is_There(string name)
    {
        Assert.That(_console.TryGetCommand(name, out _), Is.True);
    }
}