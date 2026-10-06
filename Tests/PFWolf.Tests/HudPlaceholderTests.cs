using PFWolf.Managers;

namespace PFWolf.Tests;

public class HudPlaceholderTests
{
    private static readonly Dictionary<char, string> Obituary = new()
    {
        ['o'] = "Alice",
        ['k'] = "Bob",
    };

    [Test]
    public void Fills_Known_Placeholders()
    {
        // Act
        var text = HudMessageManager.FillPlaceholders("%o was shot by %k", Obituary);

        // Assert
        Assert.That(text, Is.EqualTo("Alice was shot by Bob"));
    }

    [Test]
    public void Double_Percent_Is_A_Percent_Sign()
    {
        // Act
        var text = HudMessageManager.FillPlaceholders("100%% health", Obituary);

        // Assert
        Assert.That(text, Is.EqualTo("100% health"));
    }

    [TestCase("%x stays", "%x stays")]
    [TestCase("ends in %", "ends in %")]
    [TestCase("%%o", "%o")]
    public void Leaves_Other_Percents_Alone(string input, string expected)
    {
        // Act
        var text = HudMessageManager.FillPlaceholders(input, Obituary);

        // Assert
        Assert.That(text, Is.EqualTo(expected));
    }

    [Test]
    public void No_Placeholders_Leaves_Codes_In_Place()
    {
        // Act
        var text = HudMessageManager.FillPlaceholders("%o died", null);

        // Assert
        Assert.That(text, Is.EqualTo("%o died"));
    }
}
