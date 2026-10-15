namespace PFWolf.Tests;

public class PositionalSoundsTests
{
    [TestCase(0, 0f)]
    [TestCase(0x10000, 1f)]          // TILEGLOBAL
    [TestCase(0x28000, 2.5f)]
    public void A_Fixed_Point_Spot_Is_That_Many_Tiles(int value, float tiles)
    {
        Assert.That(PositionalSounds.FixedToTiles(value), Is.EqualTo(tiles));
    }
}