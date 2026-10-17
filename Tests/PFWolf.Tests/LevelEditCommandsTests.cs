namespace PFWolf.Tests;

public class LevelEditCommandsTests
{
    private ConsoleManager _console = null!;
    private RenderState _render = null!;
    private MapManager _map = null!;
    private LevelEditCommands _commands = null!;

    [SetUp]
    public void Make()
    {
        _console = new ConsoleManager { LevelLoaded = () => true, CheatsEnabled = () => true };
        _render = new RenderState(new Assets.TextureAsset { RawData = new byte[64 * 64] }, Lighting.NoShade);
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession(), null);
        _commands = new LevelEditCommands(_console, null!, _map, null!, null!, null!, null!, _render, null!, new GameSession());
        _commands.Register();
    }

    [Test]
    public void Wallheight_Shows_And_Sets_How_Tall_The_Walls_Are()
    {
        // Act / Assert
        _console.Execute("wallheight 3");
        Assert.That((_map.LevelWallStories, _console.Scrollback[^1]), Is.EqualTo((3, "Walls are 3 stories tall")));
        _console.Execute("wallheight 1");
        Assert.That(_console.Scrollback[^1], Is.EqualTo("Walls are 1 story tall"));
    }

    [TestCase("wallheight")]
    [TestCase("sky")]
    [TestCase("weather")]
    [TestCase("fog")]
    [TestCase("light")]
    [TestCase("lights")]
    [TestCase("zone")]
    [TestCase("zonelight")]
    [TestCase("zoneeffect")]
    [TestCase("height")]
    [TestCase("tag")]
    [TestCase("patch")]
    [TestCase("flat")]
    [TestCase("flats")]
    public void Each_Command_Is_There(string name)
    {
        Assert.That(_console.TryGetCommand(name, out _), Is.True);
    }
}