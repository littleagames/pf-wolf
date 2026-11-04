using PFWolf.Assets;
using PFWolf.Configuration;

namespace PFWolf.Tests;

public class OptionToggleKeysTests
{
    private InputManager _input = null!;
    private AudioManager _audio = null!;
    private GameOptions _options = null!;
    private bool _heartMonitor;
    private readonly List<string> _said = [];
    private OptionToggleKeys _keys = null!;

    [SetUp]
    public void Make()
    {
        _input = new InputManager(new Lazy<ConsoleManager>(() => new ConsoleManager()));
        _audio = new AudioManager(new Lazy<AssetManager>(() => null!), new GameSession());
        _options = new GameOptions();
        _heartMonitor = true;
        _said.Clear();
        _keys = new OptionToggleKeys(_input, _audio, _options, () => _heartMonitor, _said.Add);
    }

    // The control goes down this frame
    private void Press(ButtonType button)
    {
        _input.SetButtonPressed(button, true);
        _keys.CheckKeys();
    }

    [TestCase(ButtonType.ToggleAttackInfo, GameOption.AttackInfo, "ATTACKINFO")]
    [TestCase(ButtonType.ToggleLighting, GameOption.Lighting, "LIGHTING")]
    [TestCase(ButtonType.ToggleCeilings, GameOption.ShowCeilings, "CEILINGS")]
    [TestCase(ButtonType.ToggleFloors, GameOption.ShowFloors, "FLOORS")]
    [TestCase(ButtonType.ToggleHeartbeat, GameOption.HeartbeatSound, "HEARTBEAT")]
    public void A_Press_Turns_Its_Setting_Over_And_Says_So(object buttonArg, object optionArg, string name)
    {
        // Arrange
        var (button, option) = ((ButtonType)buttonArg, (GameOption)optionArg);
        bool before = _options[option];

        // Act
        Press(button);

        // Assert
        Assert.That(_options[option], Is.EqualTo(!before));
        Assert.That(_options.Choice(option), Is.EqualTo(!before), "it's the player's choice now");
        Assert.That(_said, Is.EqualTo(new[] { $"$STR_TOGGLE_{name}_{(before ? "OFF" : "ON")}" }));
    }

    [Test]
    public void Held_Down_It_Turns_Over_Once()
    {
        // Act: down for three frames
        Press(ButtonType.ToggleLighting);
        _input.ProcessButtons();
        Press(ButtonType.ToggleLighting);
        _input.ProcessButtons();
        Press(ButtonType.ToggleLighting);

        // Assert
        Assert.That(_options.Lighting, Is.False);
        Assert.That(_said, Has.Count.EqualTo(1));
    }

    [Test]
    public void The_Heartbeat_Key_Does_Nothing_Without_A_Heart_Monitor()
    {
        // Arrange: Planet Strike's status bar
        _heartMonitor = false;

        // Act
        Press(ButtonType.ToggleHeartbeat);

        // Assert
        Assert.That(_options.HeartbeatSound, Is.False);
        Assert.That(_said, Is.Empty);
    }

    [Test]
    public void The_Sound_Key_Turns_Every_Device_Off_Then_Back_As_They_Were()
    {
        // Arrange: no PC speaker
        _audio.PcSoundEnabled = false;

        // Act / Assert
        Press(ButtonType.ToggleSound);
        Assert.That((_audio.PcSoundEnabled, _audio.AdLibSoundEnabled, _audio.DigitizedSoundEnabled), Is.EqualTo((false, false, false)));
        Assert.That(_said.Last(), Is.EqualTo("$STR_TOGGLE_SOUND_OFF"));

        _input.ProcessButtons();
        _input.ProcessButtons();
        Press(ButtonType.ToggleSound);
        Assert.That((_audio.PcSoundEnabled, _audio.AdLibSoundEnabled, _audio.DigitizedSoundEnabled), Is.EqualTo((false, true, true)));
        Assert.That(_said.Last(), Is.EqualTo("$STR_TOGGLE_SOUND_ON"));
    }

    [Test]
    public void The_Sound_Key_Turns_Every_Device_On_When_All_Were_Off_To_Begin_With()
    {
        // Arrange: switched off in the Sound menu
        _audio.PcSoundEnabled = _audio.AdLibSoundEnabled = _audio.DigitizedSoundEnabled = false;

        // Act
        Press(ButtonType.ToggleSound);

        // Assert
        Assert.That((_audio.PcSoundEnabled, _audio.AdLibSoundEnabled, _audio.DigitizedSoundEnabled), Is.EqualTo((true, true, true)));
    }

    [Test]
    public void The_Music_Key_Turns_Music_Over()
    {
        // Act / Assert
        Press(ButtonType.ToggleMusic);
        Assert.That(_audio.MusicEnabled, Is.False);
        Assert.That(_said.Last(), Is.EqualTo("$STR_TOGGLE_MUSIC_OFF"));

        _input.ProcessButtons();
        _input.ProcessButtons();
        Press(ButtonType.ToggleMusic);
        Assert.That(_audio.MusicEnabled, Is.True);
        Assert.That(_said.Last(), Is.EqualTo("$STR_TOGGLE_MUSIC_ON"));
    }

    [Test]
    public void Nothing_Pressed_Changes_Nothing()
    {
        // Act
        _keys.CheckKeys();

        // Assert
        Assert.That(_said, Is.Empty);
        foreach (GameOption option in Enum.GetValues<GameOption>().Where(o => o != GameOption.Count))
            Assert.That(_options.Choice(option), Is.Null, option.ToString());
    }
}
