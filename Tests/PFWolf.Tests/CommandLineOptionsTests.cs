using System.Text.RegularExpressions;
using PFWolf.DocGen;
using PFWolf.Managers;

namespace PFWolf.Tests;

/// <summary>
/// The command-line options (GameParams), which the documentation reads as they are
/// (PFWolf.DocGen's command-line page) and --help prints: each says what it does
/// </summary>
public class CommandLineOptionsTests
{
    private static readonly List<(string Key, Definitions.FieldType Type, string Help)> Options = [.. CommandLineSchema.Options()];

    [Test]
    public void Every_Option_Is_There()
    {
        // Assert
        Assert.That(Options.Select(o => o.Key), Does.Contain("--game").And.Contain("--warp").And.Contain("--exec").And.Contain("mods"));
    }

    [Test]
    public void Each_Has_Help_As_A_Sentence()
    {
        // Act
        var bad = Options.Where(o => !Regex.IsMatch(o.Help ?? "", @"^[A-Z].*\.$")).Select(o => $"{o.Key}: {o.Help}");

        // Assert
        Assert.That(bad, Is.Empty, "help starts with a capital and ends with a full stop");
    }

    [Test]
    public void Names_Are_Lower_Case_And_Unique()
    {
        // Act
        var keys = Options.Select(o => o.Key).ToList();

        // Assert
        Assert.That(keys, Is.Unique);
        Assert.That(keys.Where(key => key != key.ToLowerInvariant()), Is.Empty);
    }

    [Test]
    public void Game_Names_Every_Game_Pack()
    {
        // Arrange
        var help = Options.Single(o => o.Key == "--game").Help;

        // Act
        var missing = GameTypes.KnownGamePackIds.Where(id => !Regex.IsMatch(help, $@"(?<![\w-]){Regex.Escape(id)}(?![\w-])"));

        // Assert
        Assert.That(missing, Is.Empty, "GameParams' --game help leaves these out");
    }

    [Test]
    public void A_Switch_Is_A_Boolean_And_A_List_A_List()
    {
        // Act
        var types = Options.ToDictionary(o => o.Key, o => o.Type);

        // Assert
        Assert.That(types["--cheats"], Is.EqualTo(Definitions.FieldType.Boolean));
        Assert.That(types["--skill"], Is.EqualTo(Definitions.FieldType.Integer));
        Assert.That(types["--file"], Is.EqualTo(Definitions.FieldType.List));
        Assert.That(types["--warp"], Is.EqualTo(Definitions.FieldType.Text));
    }

    [Test]
    public void Each_Option_Is_In_One_Group()
    {
        // Act
        var grouped = CommandLineSchema.Groups.SelectMany(group => group.Options).ToList();

        // Assert
        Assert.That(grouped, Is.EquivalentTo(Options.Select(o => o.Key)), "CommandLineSchema.Groups");
    }

    [TestCase("--cheats", "--cheats")]
    [TestCase("--skill", "--skill <n>")]
    [TestCase("--file", "--file <path>...")]
    [TestCase("--warp", "--warp <text>")]
    [TestCase("mods", "<mods...>")]
    public void The_Signature_Shows_What_It_Takes(string key, string signature)
    {
        // Arrange
        var option = Options.Single(o => o.Key == key);

        // Act / Assert
        Assert.That(CommandLineSchema.Signature(option.Key, option.Type), Is.EqualTo(signature));
    }
}
