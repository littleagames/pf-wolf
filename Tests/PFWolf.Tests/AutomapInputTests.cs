using PFWolf.Assets;
using PFWolf.Configuration;

namespace PFWolf.Tests;

public class AutomapInputTests
{
    private ConsoleManager _console = null!;
    private InputManager _input = null!;
    private AutomapManager _automap = null!;
    private ControlBindings _controls = null!;
    private InputPolling _polling = null!;
    private GameSession _session = null!;
    private AutomapInput _keys = null!;

    [SetUp]
    public void Make()
    {
        _console = new ConsoleManager();
        _input = new InputManager(new Lazy<ConsoleManager>(() => _console));
        _automap = new AutomapManager();
        _controls = new ControlBindings();
        _session = new GameSession { tics = 2 };
        var render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.noshade);
        var frame = new FrameRefresh(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, render, _session, null!);
        _polling = new InputPolling(_input, _console, _automap, _controls, new ControlSettings(), frame, new DemoBuffer(), _session,
            new InputPollingHooks(() => false, () => false, () => true, () => { }));
        _keys = new AutomapInput(_input, _console, _automap, null!, _controls, new ControlSettings(), _polling, _session);
    }

    private static readonly InputCode KeyF = InputCode.FromKey(ScanCodes.F);

    [Test]
    public void The_Automap_Opens_And_Shuts_But_Not_In_A_Demo()
    {
        _keys.ToggleAutomap();
        Assert.That(_automap.IsOpen, Is.True);
        _keys.ToggleAutomap();
        Assert.That(_automap.IsOpen, Is.False);

        _session.demoplayback = true;
        _keys.ToggleAutomap();
        Assert.That(_automap.IsOpen, Is.False);
    }

    [Test]
    public void Its_Keys_Only_Work_While_Its_Open()
    {
        // Arrange: F bound to follow
        _controls.Set(ControlAction.Of(AutomapAction.Follow), 0, KeyF);

        // Act / Assert
        Assert.That(_keys.HandleAutomapKey(KeyF), Is.False);
        _automap.Open();
        Assert.That(_keys.HandleAutomapKey(KeyF), Is.True);
        Assert.That(_automap.Follow, Is.False);         // toggled from following the player
    }

    [Test]
    public void A_Key_That_Isnt_The_Automaps_Is_Left_For_Binds()
    {
        _automap.Open();
        Assert.That(_keys.HandleAutomapKey(InputCode.FromKey(ScanCodes.F12)), Is.False);
    }

    [Test]
    public void In_Pan_Mode_Walking_Moves_The_Map_And_The_Player_Stands_Still()
    {
        // Arrange: open on a player mid-map, panned off them, then walking forward
        _automap.Open();
        _automap.Update(32, 32, 0);
        _automap.Pan(1, 0);
        float y = _automap.CenterY;
        _polling.controly = -2 * GameConstants.BASEMOVE;

        // Act
        _keys.RouteMovementToAutomap();

        // Assert: the map moved up, the player won't
        Assert.That(_automap.CenterY, Is.LessThan(y));
        Assert.That((_polling.controlx, _polling.controly, _polling.controlstrafe), Is.EqualTo((0, 0, 0)));
    }

    [Test]
    public void Following_The_Player_Walking_Walks()
    {
        _automap.Open();
        _polling.controly = -2 * GameConstants.BASEMOVE;

        _keys.RouteMovementToAutomap();

        Assert.That(_polling.controly, Is.EqualTo(-2 * GameConstants.BASEMOVE));
    }

    [Test]
    public void The_Am_Commands_Set_And_Show_Its_Settings()
    {
        // Act / Assert
        _keys.AmGridCommand(["1"]);
        Assert.That((_automap.ShowGrid, _console.Scrollback[^1]), Is.EqualTo((true, "am_grid is 1")));

        _keys.AmStyleCommand(["color"]);
        Assert.That(_console.Scrollback[^1], Is.EqualTo("am_style is color"));

        _keys.AmRevealCommand([]);
        Assert.That((_session.mapreveal != 0, _console.Scrollback[^1]), Is.EqualTo((true, "Automap reveal ON")));
    }
}
