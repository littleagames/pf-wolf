using PFWolf.Constants;

namespace PFWolf.Tests;

public class GameConstantsTests
{
    [Test]
    public void The_Derived_Constants_Come_Out_As_Id_Had_Them()
    {
        Assert.That((GameConstants.AngleQuad, GameConstants.Ang90, GameConstants.Ang360), Is.EqualTo((90, 900, 3600)));
        Assert.That((GameConstants.TextureSize, GameConstants.TextureMask, GameConstants.Fixed2TexShift), Is.EqualTo((64, 64 * 63, 4)));
        Assert.That(GameConstants.PlayerSize, Is.EqualTo(GameConstants.MinDist));
    }

    [Test]
    public void Wall_Ids_Reach_255_Below_The_Tile_Flags()
    {
        // WallShift 8 rather than Wolf3D's 6: every plane-0 code can be a WallId
        Assert.That((GameConstants.BitWall, GameConstants.BitDoor, GameConstants.BitAllTiles), Is.EqualTo((256, 512, 1024)));
        Assert.That(GameConstants.BitWall - 1, Is.EqualTo(MapConstants.MaxWallId));
        Assert.That(GameConstants.MaxDoors, Is.LessThanOrEqualTo(GameConstants.BitWall));
        Assert.That(GameConstants.BitDoor | (GameConstants.MaxDoors - 1), Is.LessThanOrEqualTo(ushort.MaxValue));
    }

    [Test]
    public void Programs_Names_Are_The_Same_Values()
    {
        Assert.That((GameHost.Angles, GameHost.BitDoor, GameHost.MaxTics), Is.EqualTo((GameConstants.Angles, GameConstants.BitDoor, GameConstants.MaxTics)));
    }
}