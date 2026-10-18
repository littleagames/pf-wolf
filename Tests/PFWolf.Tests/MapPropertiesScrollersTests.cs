using PFWolf.Assets;
using PFWolf.Editor.Editing;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Tests;

public class MapPropertiesScrollersTests
{
    private static MapPropertiesViewModel Dialog(params ScrollerProperties[] scrollers)
        => new("MAP01", new MapProperties { Scrollers = scrollers }, new DefaultMapInfo(), [], []);

    [Test]
    public void The_Dialog_Shows_A_Levels_Scrollers_And_Gives_Them_Back_As_They_Were()
    {
        // Arrange
        var scroller = new ScrollerProperties(6, X: 2, Y: -0.5, Walls: false, Active: false);

        // Act
        var dialog = Dialog(scroller);
        var built = dialog.Build();

        // Assert
        var row = dialog.Scrollers.Single();
        Assert.That((row.Tag, row.X, row.Y, row.Walls, row.Floor, row.Ceiling, row.Active), Is.EqualTo(("6", "2", "-0.5", false, true, true, false)));
        Assert.That(built!.Scrollers, Is.EqualTo(new[] { scroller }));
    }

    [Test]
    public void A_New_Scroller_Takes_The_Next_Tag_And_Moves_Nothing_Until_Given_A_Speed()
    {
        // Arrange
        var dialog = Dialog(new ScrollerProperties(4, X: 1));

        // Act
        dialog.AddScrollerCommand.Execute(null);

        // Assert
        Assert.That(dialog.Build()!.Scrollers[1], Is.EqualTo(new ScrollerProperties(5)));
    }

    [TestCase("0", "1", "tag")]             // no tag 0
    [TestCase("3", "fast", "speed")]        // not a number
    [TestCase("3", "65", "speed")]          // faster than a tile a tic
    public void A_Scroller_That_Wont_Do_Says_Why(string tag, string x, string expected)
    {
        // Arrange
        var dialog = Dialog();
        dialog.AddScrollerCommand.Execute(null);
        dialog.Scrollers[0].Tag = tag;
        dialog.Scrollers[0].X = x;

        // Act
        var built = dialog.Build();

        // Assert
        Assert.That(built, Is.Null);
        Assert.That(dialog.Error, Does.Contain(expected));
    }

    [Test]
    public void Two_Scrollers_Cant_Share_A_Tag()
    {
        // Arrange
        var dialog = Dialog(new ScrollerProperties(3), new ScrollerProperties(4));
        dialog.Scrollers[1].Tag = "3";

        // Act / Assert
        Assert.That(dialog.Build(), Is.Null);
        Assert.That(dialog.Error, Does.Contain("two scrollers for tag 3"));
    }
}
