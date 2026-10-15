namespace PFWolf.Tests;

public class GameConstantsTests
{
    [Test]
    public void The_Derived_Constants_Come_Out_As_Id_Had_Them()
    {
        Assert.That((GameConstants.BIT_WALL, GameConstants.BIT_DOOR, GameConstants.BIT_ALLTILES), Is.EqualTo((64, 128, 256)));
        Assert.That((GameConstants.ANGLEQUAD, GameConstants.ANG90, GameConstants.ANG360), Is.EqualTo((90, 900, 3600)));
        Assert.That((GameConstants.TEXTURESIZE, GameConstants.TEXTUREMASK, GameConstants.FIXED2TEXSHIFT), Is.EqualTo((64, 64 * 63, 4)));
        Assert.That(GameConstants.PLAYERSIZE, Is.EqualTo(GameConstants.MINDIST));
    }

    [Test]
    public void Programs_Names_Are_The_Same_Values()
    {
        Assert.That((Program.ANGLES, Program.BIT_DOOR, Program.MAXTICS), Is.EqualTo((GameConstants.ANGLES, GameConstants.BIT_DOOR, GameConstants.MAXTICS)));
    }
}