using PFWolf.Assets;

namespace PFWolf.Tests;

public class ElevatorScreensTests
{
    [Test]
    public void An_Overhead_Color_Is_The_Layouts_Own()
    {
        // Arrange
        var layout = new TeleporterLayout { Colors = new(StringComparer.OrdinalIgnoreCase) { ["door"] = 77 } };

        // Act / Assert
        Assert.That(ElevatorScreens.OverheadColor(layout, "DOOR", 88), Is.EqualTo("77"));
    }

    [Test]
    public void One_It_Leaves_Out_Is_The_Fallback()
    {
        Assert.That(ElevatorScreens.OverheadColor(new TeleporterLayout(), "door", 88), Is.EqualTo("88"));
        Assert.That(ElevatorScreens.OverheadColor(null, "door", 88), Is.EqualTo("88"));
    }
}
