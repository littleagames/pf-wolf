using PFWolf.Assets;
using PFWolf.Configuration;
using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Tests;

public class ConsoleArgsTests
{
    [TestCase("5", 5)]
    [TestCase("0", 0)]
    [TestCase("10", 10)]
    public void ParseInt_Reads_Numbers_In_Range(string arg, int expected)
    {
        Assert.That(ParseInt(arg, 0, 10), Is.EqualTo(expected));
    }

    [TestCase("11")]
    [TestCase("-1")]
    [TestCase("five")]
    public void ParseInt_Rejects_Others(string arg)
    {
        Assert.Throws<ArgumentException>(() => ParseInt(arg, 0, 10));
    }

    [TestCase("1", true)]
    [TestCase("ON", true)]
    [TestCase("true", true)]
    [TestCase("0", false)]
    [TestCase("off", false)]
    [TestCase("False", false)]
    public void ParseBool_Reads_Words_And_Numbers(string arg, bool expected)
    {
        Assert.That(ParseBool(arg), Is.EqualTo(expected));
    }

    [Test]
    public void ParseBool_Rejects_Others()
    {
        Assert.Throws<ArgumentException>(() => ParseBool("maybe"));
    }

    [Test]
    public void Toggle_Flips_Without_An_Argument_And_Sets_With_One()
    {
        Assert.That(Toggle([], current: true), Is.False);
        Assert.That(Toggle([], current: false), Is.True);
        Assert.That(Toggle(["1"], current: true), Is.True);
    }

    [TestCase("plain", "plain")]
    [TestCase("two words", "\"two words\"")]
    [TestCase("a;b", "\"a;b\"")]
    [TestCase("", "\"\"")]
    public void QuoteArg_Quotes_What_Would_Split(string arg, string expected)
    {
        Assert.That(QuoteArg(arg), Is.EqualTo(expected));
    }

    [Test]
    public void ParseInput_Reads_Keys_And_Buttons_Whatever_The_Case()
    {
        Assert.That(ParseInput("f5"), Is.EqualTo(ParseInput("F5")));
        Assert.That(ParseInput("mouse 1").Device, Is.EqualTo(InputDevice.MouseButton));
    }

    [Test]
    public void ParseInput_Folds_Right_Hand_Modifiers()
    {
        Assert.That(ParseInput("Right Shift"), Is.EqualTo(ParseInput("Left Shift")));
    }

    [Test]
    public void ParseInput_Rejects_Unknown_Names()
    {
        Assert.Throws<ArgumentException>(() => ParseInput("no such key"));
    }

    [TestCase("2.5", 2.5)]
    [TestCase("0", 0.0)]
    public void ParseTiles_Reads_Fractions(string arg, double expected)
    {
        Assert.That(ParseTiles(arg), Is.EqualTo(expected));
    }

    [TestCase("-1")]
    [TestCase("1000")]
    public void ParseTiles_Rejects_Off_The_Map(string arg)
    {
        Assert.Throws<ArgumentException>(() => ParseTiles(arg));
    }

    [Test]
    public void ParseFilter_Reads_Names_Only()
    {
        Assert.That(ParseFilter("Linear"), Is.EqualTo(ScaleFilter.Linear));
        Assert.Throws<ArgumentException>(() => ParseFilter("7"));
    }

    [Test]
    public void Values_Completes_Only_The_First_Argument()
    {
        // Arrange
        var complete = Values("a", "b");

        // Act / Assert
        Assert.That(complete([], 0), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(complete(["a"], 1), Is.Empty);
    }
}

public class CoreCommandsTests
{
    private ConsoleManager _console = null!;
    private string _folder = "";

    [SetUp]
    public void MakeConsole()
    {
        _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pfwolf-cmds-" + Guid.NewGuid().ToString("N"))).FullName;
        _console = new ConsoleManager();
        new CoreCommands(_console, name => Path.Combine(_folder, name)).Register();
    }

    [TearDown]
    public void RemoveFolder() => Directory.Delete(_folder, recursive: true);

    [Test]
    public void Echo_Prints_Its_Arguments()
    {
        // Act
        _console.Execute("echo hello \"big world\"");

        // Assert
        Assert.That(_console.Scrollback[^1], Is.EqualTo("hello big world"));
    }

    [Test]
    public void Clear_Empties_The_Output()
    {
        // Arrange
        _console.Print("something");

        // Act
        _console.Execute("cls");

        // Assert
        Assert.That(_console.Scrollback, Is.Empty);
    }

    [Test]
    public void Help_Describes_A_Command()
    {
        // Act
        _console.Execute("help cls");

        // Assert
        Assert.That(_console.Scrollback, Has.Some.StartsWith("clear - "));
        Assert.That(_console.Scrollback, Has.Some.Contains("aliases: cls"));
    }

    [Test]
    public void Help_Lists_Every_Command()
    {
        // Act
        _console.Execute("help");

        // Assert
        Assert.That(_console.Scrollback.Select(line => line.Split(' ')[0]),
            Is.SupersetOf(new[] { "clear", "con_pause", "echo", "exec", "help", "history" }));
    }

    [Test]
    public void Con_Pause_Sets_And_Shows()
    {
        // Act
        _console.Execute("con_pause 0");

        // Assert
        Assert.That(_console.PauseWhenOpen, Is.False);
        Assert.That(_console.Scrollback[^1], Is.EqualTo("con_pause is 0"));
    }

    [Test]
    public void Exec_Runs_A_Script_From_The_Config_Folder()
    {
        // Arrange
        File.WriteAllLines(Path.Combine(_folder, "test.cfg"), ["// a comment", "echo from the script"]);

        // Act
        _console.Execute("exec test.cfg");

        // Assert
        Assert.That(_console.Scrollback[^1], Is.EqualTo("from the script"));
    }

    [Test]
    public void Exec_Reports_A_Missing_Script()
    {
        // Act
        _console.Execute("exec missing.cfg");

        // Assert
        Assert.That(_console.Scrollback[^1], Does.Contain("couldn't find"));
    }

    [Test]
    public void Exec_Completes_The_Config_Folders_Scripts()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_folder, "autoexec.cfg"), "");
        _console.TryGetCommand("exec", out var exec);

        // Act / Assert
        Assert.That(exec.Complete!([], 0), Is.EqualTo(new[] { "autoexec.cfg" }));
    }
}

public class BindCommandsTests
{
    private ConsoleManager _console = null!;
    private ControlBindings _controls = null!;
    private ControlSettings _settings = null!;

    [SetUp]
    public void MakeConsole()
    {
        _console = new ConsoleManager();
        _controls = new ControlBindings();
        _settings = new ControlSettings();
        new BindCommands(_console, _controls, _settings).Register();
    }

    [Test]
    public void Bind_Binds_A_Command_Line()
    {
        // Act
        _console.Execute("bind F5 \"god; noclip\"");

        // Assert
        Assert.That(_console.Binds[ParseInput("F5")], Is.EqualTo("god; noclip"));
    }

    [Test]
    public void Bind_Rejoins_Several_Words_Requoting_Them()
    {
        // Act
        _console.Execute("bind F5 msg \"two words\"");

        // Assert
        Assert.That(_console.Binds[ParseInput("F5")], Is.EqualTo("msg \"two words\""));
    }

    [TestCase("Escape")]
    [TestCase("`")]
    public void Bind_Leaves_The_Menu_And_Console_Keys_Alone(string key)
    {
        // Act
        _console.Execute($"bind \"{key}\" god");

        // Assert
        Assert.That(_console.Binds, Is.Empty);
        Assert.That(_console.Scrollback[^1], Does.Contain("reserved"));
    }

    [Test]
    public void Unbind_And_Unbindall_Remove_Binds()
    {
        // Arrange
        _console.Execute("bind F5 god; bind F6 noclip");

        // Act / Assert
        _console.Execute("unbind f5");
        Assert.That(_console.Binds.Keys, Is.EqualTo(new[] { ParseInput("F6") }));
        _console.Execute("unbindall");
        Assert.That(_console.Binds, Is.Empty);
    }

    [Test]
    public void Bindaction_Sets_A_Controls_Keys()
    {
        // Act
        _console.Execute("bindaction attack F5 \"Mouse 2\"");

        // Assert
        var attack = ControlAction.Of(ButtonType.Attack);
        Assert.That(_controls[attack, 0], Is.EqualTo(ParseInput("F5")));
        Assert.That(_controls[attack, 1], Is.EqualTo(ParseInput("Mouse 2")));
    }

    [Test]
    public void Bindaction_With_A_Bad_Name_Leaves_The_Control_Alone()
    {
        // Arrange
        var attack = ControlAction.Of(ButtonType.Attack);
        var before = _controls.FormatInputs(attack);

        // Act
        _console.Execute("bindaction attack F5 nosuchkey");

        // Assert
        Assert.That(_controls.FormatInputs(attack), Is.EqualTo(before));
    }

    [Test]
    public void Settings_Are_Set_In_Range()
    {
        // Act
        _console.Execute("joy_deadzone 30; m_look 1; m_invert on; joy_sticks classic");
        _console.Execute("joy_turnspeed 99");

        // Assert
        Assert.That(_settings.JoyDeadzone, Is.EqualTo(30));
        Assert.That(_settings.JoyTurnSpeed, Is.EqualTo(4));        // 99 is past the fastest: left alone
        Assert.That(_settings.MouseLook, Is.True);
        Assert.That(_settings.MouseInvert, Is.True);
        Assert.That(_settings.JoyClassicSticks, Is.True);
    }

    [Test]
    public void Saved_Settings_Read_Back_The_Same()
    {
        // Arrange
        _settings.MouseLook = true;
        _settings.JoyDeadzone = 35;
        _settings.JoyTurnSpeed = 7;
        _settings.JoyClassicSticks = true;
        var loaded = new ControlSettings();
        var console = new ConsoleManager();
        new BindCommands(console, new ControlBindings(), loaded).Register();

        // Act
        foreach (var command in _settings.SettingCommands())
            console.Execute(command);

        // Assert
        Assert.That(loaded.SettingCommands(), Is.EqualTo(_settings.SettingCommands()));
    }
}

public class MessageCommandsTests
{
    [TestCase("0", false)]
    [TestCase("1", true)]
    public void Msg_Enabled_Sets_The_Players_Choice(string arg, bool expected)
    {
        // Arrange: no game-info needed once the player has chosen
        var console = new ConsoleManager();
        var messages = new HudMessageManager(new Lazy<AssetManager>(() => throw new InvalidOperationException()), new Lazy<ConsoleManager>(console));
        new MessageCommands(console, messages).Register();

        // Act
        console.Execute($"msg_enabled {arg}");

        // Assert
        Assert.That(messages.EnabledSetting, Is.EqualTo(expected));
        Assert.That(console.Scrollback[^1], Is.EqualTo($"msg_enabled is {arg}"));
    }
}
