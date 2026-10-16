using PFWolf.Constants;

namespace PFWolf.Tests;

public class GameConstantsTests
{
    [Test]
    public void The_Derived_Constants_Come_Out_As_Id_Had_Them()
    {
        Assert.That((GameConstants.ANGLEQUAD, GameConstants.ANG90, GameConstants.ANG360), Is.EqualTo((90, 900, 3600)));
        Assert.That((GameConstants.TEXTURESIZE, GameConstants.TEXTUREMASK, GameConstants.FIXED2TEXSHIFT), Is.EqualTo((64, 64 * 63, 4)));
        Assert.That(GameConstants.PLAYERSIZE, Is.EqualTo(GameConstants.MINDIST));
    }

    [Test]
    public void Wall_Ids_Reach_255_Below_The_Tile_Flags()
    {
        // WALLSHIFT 8 rather than Wolf3D's 6: every plane-0 code can be a wall
        Assert.That((GameConstants.BIT_WALL, GameConstants.BIT_DOOR, GameConstants.BIT_ALLTILES), Is.EqualTo((256, 512, 1024)));
        Assert.That(GameConstants.BIT_WALL - 1, Is.EqualTo(MapConstants.MAXWALLID));
        Assert.That(GameConstants.MAXDOORS, Is.LessThanOrEqualTo(GameConstants.BIT_WALL));
        Assert.That(GameConstants.BIT_DOOR | (GameConstants.MAXDOORS - 1), Is.LessThanOrEqualTo(ushort.MaxValue));
    }

    [Test]
    public void Programs_Names_Are_The_Same_Values()
    {
        Assert.That((GameHost.ANGLES, GameHost.BIT_DOOR, GameHost.MAXTICS), Is.EqualTo((GameConstants.ANGLES, GameConstants.BIT_DOOR, GameConstants.MAXTICS)));
    }
}