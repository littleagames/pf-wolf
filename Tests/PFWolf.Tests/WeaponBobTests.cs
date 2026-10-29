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

    // Doom's side-to-side swing, at walking pace (a tic's BaseMove) and its view of 168 lines,
    // whose 16 pixels are the swing's widest
    private const int SwayView = 167;
    private const double WalkTic = 35 * 150;

    private static WeaponBob SwayBob() => new() { Enabled = true, Sway = true };

    [Test]
    public void Sway_Starts_At_Rest()
    {
        var bob = SwayBob();

        Assert.That((bob.Across(SwayView), bob.Drop(SwayView)), Is.EqualTo((0, 0)));
    }

    [Test]
    public void Sway_Off_Never_Moves_Across()
    {
        // Arrange
        var bob = EnabledBob();

        // Act
        bob.Update(controlY: -100, tics: 40, moved: WalkTic * 40);

        // Assert
        Assert.That(bob.Across(SwayView), Is.EqualTo(0));
    }

    [Test]
    public void Standing_Still_Doesnt_Sway()
    {
        // Arrange
        var bob = SwayBob();

        // Act
        bob.Update(controlY: 0, tics: 100);

        // Assert
        Assert.That((bob.Across(SwayView), bob.Drop(SwayView)), Is.EqualTo((0, 0)));
    }

    [Test]
    public void Walking_Swings_It_Both_Ways_Within_Its_Width()
    {
        // Arrange
        var bob = SwayBob();
        int left = 0, right = 0;

        // Act / Assert: once round is 128 tics
        for (int i = 0; i < 256; i++)
        {
            bob.Update(controlY: -100, tics: 1, moved: WalkTic);
            left = Math.Min(left, bob.Across(SwayView));
            right = Math.Max(right, bob.Across(SwayView));
            Assert.That(bob.Drop(SwayView), Is.InRange(0, 16), $"tic {i}");
        }

        Assert.That((left, right), Is.EqualTo((-15, 15)));
    }

    [Test]
    public void Sway_Dips_In_The_Middle_And_Rises_At_The_Sides()
    {
        // Arrange: walking half way round (128 tics), long enough for the swing to be full width
        var bob = SwayBob();
        bob.Update(controlY: -100, tics: 64, moved: WalkTic * 64);

        // Act: a quarter round on is the middle of the swing, and another the other side
        bob.Update(controlY: -100, tics: 32, moved: WalkTic * 32);
        var middle = (bob.Across(SwayView), bob.Drop(SwayView));
        bob.Update(controlY: -100, tics: 32, moved: WalkTic * 32);
        var side = (bob.Across(SwayView), bob.Drop(SwayView));

        // Assert
        Assert.That((middle, side), Is.EqualTo(((0, 15), (15, 0))));
    }

    [Test]
    public void Moving_Slower_Swings_Narrower()
    {
        // Arrange
        var walking = SwayBob();
        var creeping = SwayBob();

        // Act: half way round, the far side
        walking.Update(controlY: -100, tics: 64, moved: WalkTic * 64);
        creeping.Update(controlY: -100, tics: 64, moved: WalkTic * 64 / 4);

        // Assert
        Assert.That(Math.Abs(creeping.Across(SwayView)), Is.LessThan(Math.Abs(walking.Across(SwayView)) / 2));
    }

    [Test]
    public void Stopping_Lets_The_Sway_Settle()
    {
        // Arrange
        var bob = SwayBob();
        bob.Update(controlY: -100, tics: 64, moved: WalkTic * 64);

        // Act
        bob.Update(controlY: 0, tics: 64);

        // Assert
        Assert.That((bob.Across(SwayView), bob.Drop(SwayView)), Is.EqualTo((0, 0)));
    }

    [Test]
    public void Firing_Holds_The_Weapon_Where_It_Was()
    {
        // Arrange
        var bob = SwayBob();
        bob.Update(controlY: -100, tics: 64, moved: WalkTic * 64);
        var before = (bob.Across(SwayView), bob.Drop(SwayView));

        // Act
        bob.Update(controlY: -100, tics: 20, moved: WalkTic * 20, ready: false);

        // Assert
        Assert.That((bob.Across(SwayView), bob.Drop(SwayView)), Is.EqualTo(before));
    }

    [Test]
    public void Init_Puts_The_Sway_Back_At_Rest()
    {
        // Arrange
        var bob = SwayBob();
        bob.Update(controlY: -100, tics: 40, moved: WalkTic * 40);

        // Act
        bob.Init();

        // Assert
        Assert.That((bob.Across(SwayView), bob.Drop(SwayView)), Is.EqualTo((0, 0)));
    }
}
