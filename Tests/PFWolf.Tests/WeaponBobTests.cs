namespace PFWolf.Tests;

public class WeaponBobTests
{
    // A view 130 pixels high draws bstone's 200-line pixels one to one
    private const int ViewHeight = 129;
    private const int RestDrop = 6;

    private static WeaponBob EnabledBob() => new() { Enabled = true };

    [Test]
    public void Disabled_Never_Drops()
    {
        // Arrange
        var bob = new WeaponBob();

        // Act
        bob.Update(controlY: 100, tics: 4);

        // Assert
        Assert.That(bob.Drop(ViewHeight), Is.EqualTo(0));
    }

    [Test]
    public void Starts_At_Rest()
    {
        Assert.That(EnabledBob().Drop(ViewHeight), Is.EqualTo(RestDrop));
    }

    [Test]
    public void Standing_Still_Stays_At_Rest()
    {
        // Arrange
        var bob = EnabledBob();

        // Act
        bob.Update(controlY: 0, tics: 50);

        // Assert
        Assert.That(bob.Drop(ViewHeight), Is.EqualTo(RestDrop));
    }

    [Test]
    public void Walking_Swings_The_Weapon_Up()
    {
        // Arrange
        var bob = EnabledBob();

        // Act: four tics' spring pull takes it past the top of its swing (offset 11)
        bob.Update(controlY: -100, tics: 4);

        // Assert
        Assert.That(bob.Drop(ViewHeight), Is.EqualTo(1));
    }

    [Test]
    public void Walking_Backwards_Swings_It_Too()
    {
        // Arrange
        var bob = EnabledBob();

        // Act
        bob.Update(controlY: 100, tics: 4);

        // Assert
        Assert.That(bob.Drop(ViewHeight), Is.EqualTo(1));
    }

    [Test]
    public void Walking_Stays_Within_Its_Overshoot()
    {
        // Arrange
        var bob = EnabledBob();

        // Act / Assert: offsets 0 to 12 are drops 12 to 0
        for (int i = 0; i < 500; i++)
        {
            bob.Update(controlY: -100, tics: 1);
            Assert.That(bob.Drop(ViewHeight), Is.InRange(0, 12), $"tic {i}");
        }
    }

    [Test]
    public void Stopping_Settles_Back_To_Rest()
    {
        // Arrange
        var bob = EnabledBob();
        bob.Update(controlY: -100, tics: 4);

        // Act
        bob.Update(controlY: 0, tics: 30);

        // Assert
        Assert.That(bob.Drop(ViewHeight), Is.EqualTo(RestDrop));
    }

    [Test]
    public void Init_Puts_It_Back_At_Rest()
    {
        // Arrange
        var bob = EnabledBob();
        bob.Update(controlY: -100, tics: 4);

        // Act
        bob.Init();

        // Assert
        Assert.That(bob.Drop(ViewHeight), Is.EqualTo(RestDrop));
    }

    [TestCase(259, 12)]     // twice the view: twice the pixels
    [TestCase(64, 3)]
    public void Drop_Scales_With_The_View(int viewHeight, int expected)
    {
        Assert.That(EnabledBob().Drop(viewHeight), Is.EqualTo(expected));
    }
}
