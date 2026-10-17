using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Tests;

public class FrameRefreshTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    private RenderState _render = null!;
    private GameSession _session = null!;
    private FrameRefresh _frame = null!;

    [OneTimeSetUp]
    public void Tables() => TrigTables.BuildTables();

    [SetUp]
    public void Make()
    {
        // A view 100 rows tall: the horizon looking straight ahead at row 50, 50 pixels to the
        // focal length
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.NoShade)
        {
            BaseCenterY = 50, CenterY = 50, Scale = 50, FocalLength = 0x5700,
        };
        _session = new GameSession();
        _frame = new FrameRefresh(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, _render, _session, null!);
    }

    [Test]
    public void The_View_Can_Look_As_Far_As_Keeps_The_Horizon_On_Screen()
    {
        // 49 rows above or below at a focal length of 50 pixels
        Assert.That(_frame.MaxPitch(), Is.EqualTo(Math.Atan(49 / 50.0) * 180 / Math.PI).Within(1e-9));
    }

    [Test]
    public void With_No_Projection_Yet_It_Cant_Look_At_All()
    {
        _render.Scale = 0;
        Assert.That(_frame.MaxPitch(), Is.EqualTo(0));
    }

    [TestCase(0, 50)]
    [TestCase(45, 100 - 1)]     // 50 pixels down at 45 degrees, kept a row inside the view
    [TestCase(-20, 50 - 18)]    // looking down moves the horizon up: tan 20 degrees x 50 = 18.2
    public void Looking_Up_Moves_The_Horizon_Down(double pitch, int centery)
    {
        // Arrange
        _render.ViewPitch = pitch;

        // Act
        _frame.SetupPitch();

        // Assert
        Assert.That(_render.CenterY, Is.EqualTo(centery));
    }

    [Test]
    public void Too_Steep_A_Pitch_Is_Brought_Back_To_The_Most_It_Can_Be()
    {
        _render.ViewPitch = 80;

        _frame.SetupPitch();

        Assert.That(_render.ViewPitch, Is.EqualTo(_frame.MaxPitch()).Within(1e-9));
    }

    [Test]
    public void The_View_Is_Set_Up_From_The_Camera_A_Focal_Length_Behind_It()
    {
        // Arrange: a fixed camera in the middle of tile (10, 20), looking north
        _session.Camera.SetFixed(10 * Tile + Tile / 2, 20 * Tile + Tile / 2, 90);

        // Act
        _frame.Setup3DView();

        // Assert: the view point is behind the camera (south of it, as north is up the map)
        Assert.That((_render.ViewAngle, _render.MidAngle), Is.EqualTo(((short)90, (short)(90 * GameConstants.FineAngles / GameConstants.Angles))));
        Assert.That((_render.ViewSin, _render.ViewCos), Is.EqualTo((TrigTables.SinTable[90], TrigTables.CosTable[90])));
        Assert.That(_render.ViewX, Is.EqualTo(10 * Tile + Tile / 2).Within(1));
        Assert.That(_render.ViewY, Is.EqualTo(20 * Tile + Tile / 2 + _render.FocalLength).Within(1));
        Assert.That((_render.FocalTx, _render.FocalTy), Is.EqualTo(((short)10, (short)20)));
    }

    [Test]
    public void Where_The_View_Is_In_Its_Tile_Is_Kept_Both_Ways()
    {
        // A camera a quarter of the way into tile (3, 3) east and south, looking east
        _session.Camera.SetFixed(3 * Tile + Tile / 4 + _render.FocalLength, 3 * Tile + Tile / 4, 0);

        _frame.Setup3DView();

        Assert.That((_render.XPartialDown, _render.XPartialUp), Is.EqualTo(((uint)(Tile / 4), (uint)(Tile - 1 - Tile / 4))));
        Assert.That((_render.YPartialDown, _render.YPartialUp), Is.EqualTo(((uint)(Tile / 4), (uint)(Tile - 1 - Tile / 4))));
    }

    [TestCase(GameConstants.EyeDefault, GameConstants.EyeDefault)]
    [TestCase(0, GameConstants.MinEye)]
    [TestCase(200, GameConstants.MaxEye)]
    public void The_Eye_Stays_Within_A_Story(int eyez, int seen)
    {
        _session.Camera.SetFixed(5 * Tile, 5 * Tile, 0, eyez: eyez);

        _frame.Setup3DView();

        Assert.That(_render.ViewEyeZ, Is.EqualTo(seen));
    }

    [Test]
    public void The_Cameras_Pitch_Moves_The_Horizon()
    {
        _session.Camera.SetFixed(5 * Tile, 5 * Tile, 0, pitch: 30);

        _frame.Setup3DView();

        // tan 30 degrees x 50 = 28.9 rows down
        Assert.That((_render.ViewPitch, _render.CenterY), Is.EqualTo((30.0, (short)(50 + 29))));
    }
}
