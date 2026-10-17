namespace PFWolf.Tests;

public class DebugCommandsTests
{
    private ConsoleManager _console = null!;
    private FrameRefresh _frame = null!;
    private GameSession _session = null!;

    [SetUp]
    public void Make()
    {
        _console = new ConsoleManager { LevelLoaded = () => true, CheatsEnabled = () => true };
        var session = _session = new GameSession();
        var render = new RenderState(new Assets.TextureAsset { RawData = new byte[64 * 64] }, Lighting.noshade);
        _frame = new FrameRefresh(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, render, session, null!);
        new DebugCommands(_console, null!, null!, null!, null!, _frame, null!, null!, render, null!, session,
            new Configuration.GameSettings { viewsize = 15 }).Register();
    }

    [Test]
    public void Fps_Toggles_The_Counter()
    {
        _console.Execute("fps");
        Assert.That((_frame.fpscounter, _console.Scrollback[^1]), Is.EqualTo((true, "FPS counter ON")));
        _console.Execute("fps 0");
        Assert.That((_frame.fpscounter, _console.Scrollback[^1]), Is.EqualTo((false, "FPS counter OFF")));
    }

    [Test]
    public void Slowmo_And_Vbls_Set_The_Frames_Waits()
    {
        _console.Execute("slowmo 5");
        _console.Execute("vbls 2");

        Assert.That((_session.singlestep, _session.extravbls), Is.EqualTo(((byte)5, 2)));
        Assert.That(_console.Scrollback[^1], Is.EqualTo("vbls is 2"));
    }

    [TestCase("overhead")]
    [TestCase("where")]
    [TestCase("pos")]
    [TestCase("count")]
    [TestCase("actors")]
    [TestCase("maps")]
    [TestCase("fade")]
    [TestCase("screenshot")]
    public void Each_Command_Is_There(string name)
    {
        Assert.That(_console.TryGetCommand(name, out _), Is.True);
    }
}