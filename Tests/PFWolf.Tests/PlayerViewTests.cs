using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class PlayerViewTests
{
    private const int Tile = 1 << MapConstants.TileShift;
    private const int FocalLength = 0x5700;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private PlayerView _view = null!;

    [OneTimeSetUp]
    public void Tables() => TrigTables.BuildTables();      // the sines the view turns by

    [SetUp]
    public void MakeLevel()
    {
        // An open map with nothing on it; the tests put walls where they need them
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session)
        {
            tilemap = new ushort[MapManager.MapSize, MapManager.MapSize],
            actorat = new Actor?[MapManager.MapSize, MapManager.MapSize],
            spotvis = new bool[MapManager.MapSize, MapManager.MapSize],
        };
        var doors = new Doors(null!, null!, _map, null!, null!, _session, null!);
        var lineOfSight = new LineOfSight(_map, doors, _session, (_, _, _, _) => false);
        _view = new PlayerView(_map, lineOfSight, _session,
            new PlayerViewHooks(() => FocalLength, () => 320, (_, _, _, _, _) => { }, _ => false, _ => { }));
    }

    // In the middle of tile (10, 10), facing east
    private PlayerPawn Player()
    {
        var pawn = new PlayerPawn { State = _session.PlayerState, X = 10 * Tile + Tile / 2, Y = 10 * Tile + Tile / 2, TileX = 10, TileY = 10, Angle = 0 };
        _session.PlayerState.Pawn = pawn;
        // where a line to them aims: where their last move left them (T_Player sets these)
        (_session.PlayerState.PlUX, _session.PlayerState.PlUY) = ((ushort)(pawn.X >> GameConstants.UnsignedShift), (ushort)(pawn.Y >> GameConstants.UnsignedShift));
        return pawn;
    }

    private static Monster Guard(int tileX, int tileY) =>
        new() { Name = "Guard", X = tileX * Tile + Tile / 2, Y = tileY * Tile + Tile / 2, TileX = (byte)tileX, TileY = (byte)tileY };

    [Test]
    public void The_View_Point_Is_Behind_The_Player_By_The_Focal_Length()
    {
        // Arrange
        var pawn = Player();

        // Act
        var (x, y, sin, cos) = _view.PlayerViewOrigin();

        // Assert: facing east, behind is west
        Assert.That((x, y), Is.EqualTo((pawn.X - FocalLength, pawn.Y)));
        Assert.That((sin, cos), Is.EqualTo((0, 0x10000)));
    }

    [TestCase(15, 10, true)]        // straight ahead
    [TestCase(15, 12, true)]        // ahead, a little to the side
    [TestCase(5, 10, false)]        // behind
    [TestCase(11, 15, false)]       // off to the side, outside the view
    public void A_Player_Sees_What_Is_In_Front_Within_The_Views_Width(int x, int y, bool sees)
    {
        Assert.That(_view.InViewOf(Player(), Guard(x, y)), Is.EqualTo(sees));
    }

    [Test]
    public void A_Wall_In_The_Way_Hides_It()
    {
        // Arrange
        _map.tilemap[12, 10] = 1;

        // Act / Assert
        Assert.That(_view.InViewOf(Player(), Guard(15, 10)), Is.False);
    }

    [Test]
    public void Alone_With_The_Camera_On_Them_A_Players_Tile_Is_In_View_As_The_Renderer_Marked_It()
    {
        // Arrange
        var pawn = Player();
        _session.Camera.Follow(pawn);

        // Act / Assert
        Assert.That(_view.PlayerTileInView(pawn), Is.False);
        _map.spotvis[10, 10] = true;
        Assert.That(_view.PlayerTileInView(pawn), Is.True);
    }

    [Test]
    public void Otherwise_It_Is_In_View_On_Open_Floor()
    {
        // Arrange: with others playing, the screen isn't asked
        var pawn = Player();
        _session.Camera.Follow(pawn);
        _session.SetGameMode(GameMode.Coop);

        // Act / Assert
        Assert.That(_view.PlayerTileInView(pawn), Is.True);
        _map.tilemap[10, 10] = 1;       // somehow in a WallId
        Assert.That(_view.PlayerTileInView(pawn), Is.False);
    }
}
