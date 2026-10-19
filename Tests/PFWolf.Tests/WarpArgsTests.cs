namespace PFWolf.Tests;

public class WarpArgsTests
{
    [Test]
    public void The_Facing_Flags_And_Sound_Are_Told_Apart()
    {
        // Act
        var warp = WarpArgs.Parse(["West", " eod/teleport", "telefrag", "SwapColors", "fizzle"]);

        // Assert
        Assert.That(warp.Facing, Is.EqualTo("west"));
        Assert.That(warp.Sound, Is.EqualTo("eod/teleport"));
        Assert.That(warp.Telefrag && warp.SwapColors && warp.Fizzle, Is.True);
        Assert.That(warp.Monsters, Is.False);
        Assert.That(warp.Angle, Is.EqualTo(180));
        Assert.That(warp.Dir, Is.EqualTo(ObjDir.West));
        Assert.That(warp.Step, Is.EqualTo((-1, 0)));
    }

    [Test]
    public void With_No_Facing_The_Player_Keeps_Theirs()
    {
        var warp = WarpArgs.Parse(["monsters"]);

        Assert.That(warp.Angle, Is.Null);
        Assert.That(warp.Dir, Is.Null);
        Assert.That(warp.Step, Is.EqualTo((0, 0)));
        Assert.That(warp.Sound, Is.Null);
        Assert.That(warp.Monsters, Is.True);
    }

    [Test]
    public void Many_Pads_Lead_To_The_Last_Tile_That_Isnt_A_Pad()
    {
        // Arrange: three pads and two exits with the tag, row by row
        (int X, int Y)[] tagged = [(5, 2), (9, 3), (12, 7), (3, 20), (8, 40)];
        var pads = new HashSet<(int X, int Y)> { (5, 2), (12, 7), (8, 40) };

        // Act
        var exit = WarpArgs.ExitOf(tagged, pads.Contains);

        // Assert
        Assert.That(exit, Is.EqualTo((3, 20)));
    }

    [Test]
    public void Pads_With_No_Exit_Lead_Nowhere()
    {
        (int X, int Y)[] tagged = [(5, 2), (12, 7)];

        Assert.That(WarpArgs.ExitOf(tagged, _ => true), Is.Null);
    }
}
