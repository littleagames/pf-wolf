using PFWolf.Managers;

namespace PFWolf.Tests;

/// <summary>
/// Which tile beside a wall outlet its actor comes out on (Managers/LevelAI.cs), as bstone's
/// CheckSpawnEA picks it: the sides in xy_offset order, and what may already be on the tile.
/// </summary>
public class LevelAIOutletTests
{
    [Test]
    public void Sides_Are_Tried_North_South_West_East()
    {
        Assert.That(LevelAI.OutletSides, Is.EqualTo(new[] { (0, -1), (0, 1), (-1, 0), (1, 0) }));
    }

    [TestCase(true, true, true, true, 10, 9)]       // all open: north
    [TestCase(false, true, false, true, 10, 11)]    // south before east
    [TestCase(false, false, true, true, 9, 10)]     // west before east
    [TestCase(false, false, false, true, 11, 10)]   // only east
    public void The_First_Side_Joining_The_Players_Area_Is_Picked(bool north, bool south, bool west, bool east, int x, int y)
    {
        var open = new Dictionary<(int, int), bool>
        {
            [(10, 9)] = north, [(10, 11)] = south, [(9, 10)] = west, [(11, 10)] = east,
        };

        var side = LevelAI.OutletSide(10, 10, (tx, ty) => open.GetValueOrDefault((tx, ty)));

        Assert.That(side, Is.EqualTo((x, y)));
    }

    [Test]
    public void No_Side_When_None_Joins_The_Players_Area()
    {
        Assert.That(LevelAI.OutletSide(10, 10, (_, _) => false), Is.Null);
    }

    [Test]
    public void An_Empty_Tile_Or_A_Solid_Static_Is_Open_But_Not_A_Wall_Or_Wall_Sprite()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LevelAI.OutletTileOpen(null), Is.True);
            Assert.That(LevelAI.OutletTileOpen(new BlockingActor()), Is.True);
            Assert.That(LevelAI.OutletTileOpen(new Wall(5)), Is.False);
            Assert.That(LevelAI.OutletTileOpen(new WallSpriteBlocker()), Is.False);
        });
    }
}
