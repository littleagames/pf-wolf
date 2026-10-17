using PFWolf.Assets;
using PFWolf.Configuration;

namespace PFWolf.Tests;

public class InputPollingTests
{
    private InputManager _input = null!;
    private ConsoleManager _console = null!;
    private ControlSettings _settings = null!;
    private RenderState _render = null!;
    private FrameRefresh _frame = null!;
    private GameSession _session = null!;
    private InputPolling _polling = null!;

    [SetUp]
    public void Make()
    {
        _console = new ConsoleManager();
        _input = new InputManager(new Lazy<ConsoleManager>(() => _console));
        _settings = new ControlSettings();
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.noshade);
        _session = new GameSession { tics = 2 };
        _frame = new FrameRefresh(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, _render, _session, null!);
        _polling = new InputPolling(_input, _console, new AutomapManager(), new ControlBindings(), _settings, _frame,
            new DemoBuffer(), _session,
            new InputPollingHooks(() => false, () => false,
                DemoTesting: () => true, () => { }));
    }

    [Test]
    public void The_Movement_Buttons_Walk_And_Turn_A_Tics_Worth()
    {
        // Arrange
        _input.SetButtonPressed(ButtonType.MoveForward, true);
        _input.SetButtonPressed(ButtonType.TurnRight, true);

        // Act
        _polling.PollButtonMove();

        // Assert: two tics at walking speed
        Assert.That((_polling.controly, _polling.controlx), Is.EqualTo((-2 * GameConstants.BASEMOVE, 2 * GameConstants.BASEMOVE)));
    }

    [Test]
    public void Running_Goes_At_Run_Speed()
    {
        _input.SetButtonPressed(ButtonType.Run, true);
        _input.SetButtonPressed(ButtonType.MoveBackward, true);
        _input.SetButtonPressed(ButtonType.TurnLeft, true);

        _polling.PollButtonMove();

        Assert.That((_polling.controly, _polling.controlx), Is.EqualTo((2 * GameConstants.RUNMOVE, -2 * GameConstants.RUNMOVE)));
    }

    [Test]
    public void The_Look_Keys_Look_Up_And_Down_And_Center()
    {
        // Act / Assert
        _input.SetButtonPressed(ButtonType.LookUp, true);
        _polling.PollButtonMove();
        Assert.That(_polling.controlpitch, Is.EqualTo(2 * InputPolling.LOOKSPEED).Within(1e-9));

        _input.SetButtonPressed(ButtonType.LookUp, false);
        _input.SetButtonPressed(ButtonType.LookDown, true);
        _input.SetButtonPressed(ButtonType.CenterView, true);
        _polling.PollButtonMove();
        Assert.That(_polling.controlpitch, Is.EqualTo(0).Within(1e-9));
        Assert.That(_polling.controlcenterview, Is.True);
    }

    [Test]
    public void The_Buttons_Pressed_Are_A_Bit_Each()
    {
        _input.SetButtonPressed(ButtonType.Attack, true);
        _input.SetButtonPressed(ButtonType.Use, true);

        Assert.That(_polling.LocalButtons(), Is.EqualTo((1u << (int)ButtonType.Attack) | (1u << (int)ButtonType.Use)));
    }

    [TestCase(2, 2, 0)]
    [TestCase(-3, 0, 3)]
    [TestCase(0, 0, 0)]
    public void The_Wheel_Counts_Notches_Its_Way(int turned, int up, int down)
    {
        _polling.wheelnotches = turned;
        Assert.That((_polling.WheelNotches(InputCode.WheelUp), _polling.WheelNotches(InputCode.WheelDown)), Is.EqualTo((up, down)));
        Assert.That(_polling.WheelNotches(InputCode.FromKey(ScanCodes.A)), Is.EqualTo(0));
    }

    [Test]
    public void The_Mouse_Wheel_Only_Counts_With_The_Mouse_Enabled()
    {
        // Arrange
        _polling.wheelnotches = 1;

        // Act / Assert
        _settings.mouseenabled = false;
        Assert.That(_polling.IsInputActive(InputCode.WheelUp), Is.False);
        _settings.mouseenabled = true;
        Assert.That(_polling.IsInputActive(InputCode.WheelUp), Is.True);
        Assert.That(_polling.IsInputActive(InputCode.WheelDown), Is.False);
    }

    [Test]
    public void A_Bound_Button_Fires_Once_Per_Press_But_The_Wheel_Every_Notch()
    {
        // Arrange: a bind on the wheel
        _settings.mouseenabled = true;
        _console.Bind(InputCode.WheelUp, "echo up");
        _polling.wheelnotches = 1;

        // Act / Assert: every frame it turns is a press
        Assert.That(_polling.TakeFreshPresses(), Is.EqualTo(new[] { InputCode.WheelUp }));
        Assert.That(_polling.TakeFreshPresses(), Is.EqualTo(new[] { InputCode.WheelUp }));
        _polling.wheelnotches = 0;
        Assert.That(_polling.TakeFreshPresses(), Is.Empty);
    }

    [Test]
    public void A_Fixed_Step_Is_That_Many_Tics()
    {
        // Act: demotest doesn't wait for it
        _polling.WaitFixedTics(4);

        // Assert
        Assert.That(_session.tics, Is.EqualTo(4));
    }
}
