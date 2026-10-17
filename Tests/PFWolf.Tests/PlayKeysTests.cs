using PFWolf.Assets;
using PFWolf.Configuration;
using SDL2;

namespace PFWolf.Tests;

public class PlayKeysTests
{
    private ConsoleManager _console = null!;
    private InputManager _input = null!;
    private PlayKeys _keys = null!;

    private GameSession _session = null!;
    private bool _chatting, _automapTakes;
    private readonly List<string> _calls = [];

    [SetUp]
    public void Make()
    {
        _console = new ConsoleManager();
        _input = new InputManager(new Lazy<ConsoleManager>(() => _console));
        var session = _session = new GameSession { netgame = true };
        var render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.noshade);
        var frame = new FrameRefresh(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, render, session, null!);
        var polling = new InputPolling(_input, _console, new AutomapManager(), new ControlBindings(), new ControlSettings(), frame,
            new DemoBuffer(), session,
            new InputPollingHooks(() => false, () => false, () => true, () => { }));

        (_chatting, _automapTakes) = (false, false);
        _calls.Clear();

        // With others, so CheckKeys hands the rest to NetCheckKeys after the binds (no menus,
        // pausing or cheat combos to reach)
        _keys = new PlayKeys(new AssetManager(), new VideoManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test"))), _input, null!, null!, null!, _console, new AutomapManager(), null!, null!,
            polling, null!, null!, null!, null!, frame, session,
            new Configuration.GameSettings { viewsize = 21 }, new PlayKeysHooks(() => _chatting,
                () => _calls.Add("chat"), scan => _calls.Add($"net {scan}"), () => _calls.Add("automap"),
                _ => _automapTakes, () => { }, () => { }, _ => { }, () => { }));
    }

    private void Press(ScanCodes key)
    {
        var e = new SDL.SDL_Event { type = SDL.SDL_EventType.SDL_KEYDOWN };
        e.key.keysym.scancode = (SDL.SDL_Scancode)key;
        _input.HandleEvent(e);
    }

    private bool Ran(string command) => _console.Scrollback.Any(line => line.Contains($"\"{command}\""));

    [Test]
    public void A_Bound_Key_Runs_Its_Command()
    {
        // Arrange
        _console.Bind(InputCode.FromKey(ScanCodes.sc_K), "bound_k");
        Press(ScanCodes.sc_K);

        // Act
        _keys.CheckKeys();

        // Assert: then, with others, the key goes on to the game's own keys
        Assert.That(Ran("bound_k"), Is.True);
        Assert.That(_calls, Is.EqualTo(new[] { $"net {ScanCodes.sc_K}" }));
    }

    [Test]
    public void Binds_Dont_Run_While_Recording_A_Demo()
    {
        _session.demorecord = true;
        _console.Bind(InputCode.FromKey(ScanCodes.sc_K), "bound_k");
        Press(ScanCodes.sc_K);

        _keys.CheckKeys();

        Assert.That(Ran("bound_k"), Is.False);
    }

    [Test]
    public void While_A_Demo_Plays_Keys_Are_Thrown_Away_Not_Kept_For_Later()
    {
        // Arrange
        _console.Bind(InputCode.FromKey(ScanCodes.sc_K), "bound_k");
        Press(ScanCodes.sc_K);
        _session.demoplayback = true;

        // Act: the demo ends, and the next frame's keys are checked
        _keys.CheckKeys();
        _session.demoplayback = false;
        _keys.CheckKeys();

        // Assert
        Assert.That(Ran("bound_k"), Is.False);
    }

    [Test]
    public void The_Grave_Key_Opens_The_Console_And_Nothing_Else_That_Frame()
    {
        Press(ScanCodes.sc_Grave);

        _keys.CheckKeys();

        Assert.That(_console.IsOpen, Is.True);
        Assert.That(_calls, Is.Empty);
    }

    [Test]
    public void Not_While_Recording_A_Demo()
    {
        _session.demorecord = true;
        Press(ScanCodes.sc_Grave);

        _keys.CheckKeys();

        Assert.That(_console.IsOpen, Is.False);
    }

    [Test]
    public void With_The_Console_Open_The_Game_Gets_No_Keys()
    {
        _console.Open();

        _keys.CheckKeys();

        Assert.That(_calls, Is.Empty);
    }

    [Test]
    public void While_Chatting_The_Keys_Are_The_Chat_Lines()
    {
        _chatting = true;
        _console.Bind(InputCode.FromKey(ScanCodes.sc_K), "bound_k");
        Press(ScanCodes.sc_K);

        _keys.CheckKeys();

        Assert.That(_calls, Is.EqualTo(new[] { "chat" }));
        Assert.That(Ran("bound_k"), Is.False);
    }

    [Test]
    public void The_Automaps_Own_Keys_Come_Before_Binds()
    {
        _automapTakes = true;
        _console.Bind(InputCode.FromKey(ScanCodes.sc_K), "bound_k");
        Press(ScanCodes.sc_K);

        _keys.CheckKeys();

        Assert.That(Ran("bound_k"), Is.False);
    }

    [Test]
    public void The_Automap_Button_Opens_The_Automap()
    {
        _input.SetButtonPressed(ButtonType.bt_automap, true);

        _keys.CheckKeys();

        Assert.That(_calls, Does.Contain("automap"));
    }
}
