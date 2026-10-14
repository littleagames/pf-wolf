using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class ProjectileActionsTests
{
    [TestCase(10, 0, 0)]        // east
    [TestCase(0, -10, 90)]      // north: y grows southward
    [TestCase(-10, 0, 180)]
    [TestCase(0, 10, 270)]
    [TestCase(10, -10, 45)]
    public void Angles_Go_Counter_Clockwise_From_East(int dx, int dy, int degrees)
    {
        Assert.That(ProjectileActions.AngleTo(100, 100, 100 + dx, 100 + dy), Is.EqualTo(degrees));
    }

    [Test]
    public void Reach_Is_The_Longer_Way_Across()
    {
        // Arrange
        var pawn = new PlayerPawn { X = 1000, Y = 5000 };

        // Act / Assert: 3000 across, 4000 down
        Assert.That(ProjectileActions.Reach(pawn, 4000, 1000), Is.EqualTo(4000));
    }

    [TestCase("2", 2.0)]
    [TestCase("1.5", 1.5)]
    [TestCase("0", 0.0)]
    public void Tiles_Can_Be_Fractions(string text, double tiles)
    {
        // Act
        bool ok = ProjectileActions.TryParseTiles(text, out var reach);

        // Assert
        Assert.That(ok, Is.True);
        Assert.That(reach, Is.EqualTo((long)(tiles * MapConstants.TILEGLOBAL)));
    }

    [Test]
    public void Tiles_That_Arent_A_Number_Arent_Read()
    {
        Assert.That(ProjectileActions.TryParseTiles("near", out _), Is.False);
    }

    [Test]
    public void A_Property_Is_Read_As_A_Whole_Number_Else_The_Fallback()
    {
        // Arrange
        var fireball = new Monster { Name = "Fireball" };
        fireball.Properties["speed"] = "12288";
        fireball.Properties["spread"] = "lots";

        // Act / Assert
        Assert.That(ProjectileActions.PropertyInt(fireball, "speed", 0x2000), Is.EqualTo(12288));
        Assert.That(ProjectileActions.PropertyInt(fireball, "spread", 0), Is.EqualTo(0));
        Assert.That(ProjectileActions.PropertyInt(fireball, "speed.random", 7), Is.EqualTo(7));
    }
}
