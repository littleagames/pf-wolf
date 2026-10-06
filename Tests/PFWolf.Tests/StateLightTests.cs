using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class StateLightTests
{
    [Test]
    public void Null_Is_No_Override()
    {
        Assert.That(StateLight.Parse(null), Is.Null);
    }

    [TestCase("none")]
    [TestCase("OFF")]
    [TestCase(" none ")]
    public void None_Or_Off_Turns_The_Light_Off(string value)
    {
        // Act
        var light = StateLight.Parse(value);

        // Assert
        Assert.That(light, Is.EqualTo(new StateLight(true, null, null)));
    }

    [TestCase("200", 200)]
    [TestCase("300", 255)]
    [TestCase("-5", 0)]
    public void A_Number_Is_A_Clamped_Intensity(string value, int expected)
    {
        // Act
        var light = StateLight.Parse(value);

        // Assert
        Assert.That(light, Is.EqualTo(new StateLight(false, expected, null)));
    }

    [Test]
    public void A_Word_Is_No_Intensity()
    {
        // Act
        var light = StateLight.Parse("bright");

        // Assert
        Assert.That(light, Is.EqualTo(new StateLight(false, null, null)));
    }

    [Test]
    public void A_Mapping_Sets_Each_Field_Given()
    {
        // Arrange: as YamlDotNet hands over a mapping
        var fields = new Dictionary<object, object>
        {
            ["Intensity"] = "180",
            ["radius"] = "2.5",
            ["color"] = "#FFD080",
        };

        // Act
        var light = StateLight.Parse(fields);

        // Assert
        Assert.That(light, Is.EqualTo(new StateLight(false, 180, 2.5, "#FFD080")));
    }

    [Test]
    public void A_Mapping_Drops_A_Negative_Radius()
    {
        // Arrange
        var fields = new Dictionary<object, object> { ["radius"] = "-1" };

        // Act
        var light = StateLight.Parse(fields);

        // Assert
        Assert.That(light, Is.EqualTo(new StateLight(false, null, null)));
    }
}
