using PFWolf.Assets;
using PFWolf.Configuration;

namespace PFWolf.Tests;

public class GameOptionsTests
{
    [Test]
    public void Without_Choices_Or_Game_Defaults_The_Engine_Defaults_Apply()
    {
        // Arrange
        var options = new GameOptions();

        // Act / Assert
        Assert.That((options.Look, options.Messages, options.AutomapStats, options.Autosave,
                options.WeaponBob, options.AlwaysRun, options.Crosshair),
            Is.EqualTo((true, false, true, true, false, false, false)));
    }

    [Test]
    public void Game_Info_Setting_Defaults_Replace_The_Engine_Defaults()
    {
        // Arrange
        var options = new GameOptions();

        // Act
        options.UseGameDefaults(new GameInfoAsset
        {
            SettingDefaults = new SettingDefaultsInfo { Look = false, Messages = true, Crosshair = true },
        });

        // Assert
        Assert.That((options.Look, options.Messages, options.Crosshair, options.Autosave), Is.EqualTo((false, true, true, true)));
    }

    [Test]
    public void The_Players_Choice_Wins_Until_It_Goes_Back_To_Default()
    {
        // Arrange
        var options = new GameOptions();
        options.UseGameDefaults(new GameInfoAsset { SettingDefaults = new SettingDefaultsInfo { Look = false } });

        // Act / Assert
        options.Toggle(GameOption.Look);
        Assert.That((options.Look, options.Choice(GameOption.Look)), Is.EqualTo((true, (bool?)true)));
        options.SetChoice(GameOption.Look, null);
        Assert.That(options.Look, Is.False);
    }
}

public class GameOptionCommandsTests
{
    private ConsoleManager _console = null!;
    private GameOptions _options = null!;

    [SetUp]
    public void Make()
    {
        _console = new ConsoleManager();
        _options = new GameOptions();
        new GameOptionCommands(_console, _options).Register();
    }

    [Test]
    public void Autosave_Shows_And_Sets_Whether_New_Levels_Save_Themselves()
    {
        // Act / Assert
        _console.Execute("autosave");
        Assert.That(_console.Scrollback[^1], Is.EqualTo("autosave is 1 (the game's default)"));
        _console.Execute("autosave 0");
        Assert.That((_options.Autosave, _console.Scrollback[^1]), Is.EqualTo((false, "autosave is 0")));
    }

    [TestCase("freelook", GameOption.Look)]
    [TestCase("am_stats", GameOption.AutomapStats)]
    [TestCase("weaponbob", GameOption.WeaponBob)]
    [TestCase("weaponbob_sway", GameOption.WeaponBobSway)]
    [TestCase("alwaysrun", GameOption.AlwaysRun)]
    [TestCase("crosshair", GameOption.Crosshair)]
    public void Each_Setting_Goes_Back_To_The_Games_Default(string command, object optionArg)
    {
        // Arrange: NUnit's test methods are public, so the internal enum comes in as object
        var option = (GameOption)optionArg;

        // Act
        _console.Execute($"{command} 1");
        _console.Execute($"{command} default");

        // Assert
        Assert.That(_options.Choice(option), Is.Null);
    }
}
