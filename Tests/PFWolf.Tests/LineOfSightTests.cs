using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class LineOfSightTests
{
    private MapManager _map = null!;
    private Doors _doors = null!;
    private bool _wallSpriteInTheWay;

    [SetUp]
    public void MakeMap()
    {
        // An open map with nothing on it; the tests put walls and doors where they need them
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession(), null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        _doors = new Doors(null!, null!, _map, null!, null!, new GameSession(), null!);
        _wallSpriteInTheWay = false;
    }

    private LineOfSight Sight() => new(_map, _doors, new GameSession(), (_, _, _, _) => _wallSpriteInTheWay);

    private static int Middle(int tile) => (tile << MapConstants.TileShift) + 0x8000;

    // From the middle of tile (5, 10) to a guard in the middle of tile (12, 10)
    private bool CanSeeAlongRow10() =>
        Sight().CheckLine(Middle(5), Middle(10), new Monster { Name = "Guard", X = Middle(12), Y = Middle(10) });

    [Test]
    public void Nothing_In_The_Way_Is_A_Clear_Line()
    {
        Assert.That(CanSeeAlongRow10(), Is.True);
    }

    [Test]
    public void A_Wall_In_The_Way_Blocks_It()
    {
        // Arrange
        _map.TileMap[8, 10] = 1;

        // Act / Assert
        Assert.That(CanSeeAlongRow10(), Is.False);
    }

    [Test]
    public void A_Wall_Off_The_Line_Doesnt()
    {
        // Arrange
        _map.TileMap[8, 12] = 1;

        // Act / Assert
        Assert.That(CanSeeAlongRow10(), Is.True);
    }

    // Vanilla's quirk, kept for the demos: the door's position (0 to 0xffff across the tile) is
    // compared with where the line crosses in whole map coordinates (8.8 fixed point, so 0x0A80
    // in the middle of row 10), not where it crosses the tile. A door on row 10 lets sight
    // through once it's past 0x0A80, long before it has really slid out of the way.
    [TestCase((ushort)0, false)]            // closed
    [TestCase((ushort)0x0A00, false)]       // short of the crossing's whole value
    [TestCase((ushort)0x0B00, true)]        // past it
    [TestCase((ushort)0xffff, true)]        // all the way open
    public void A_Door_Blocks_It_Until_It_Has_Slid_Past_The_Line(ushort position, bool clear)
    {
        // Arrange: door 0 across the row at x 8
        _map.TileMap[8, 10] = GameConstants.BitDoor | 0;
        _doors.DoorObjList[0] = new DoorObject { TileX = 8, TileY = 10, Vertical = true, Position = position };

        // Act / Assert
        Assert.That(CanSeeAlongRow10(), Is.EqualTo(clear));
    }

    [Test]
    public void A_Wall_Sprite_Across_It_Blocks_It()
    {
        // Arrange
        _wallSpriteInTheWay = true;

        // Act / Assert
        Assert.That(CanSeeAlongRow10(), Is.False);
    }

    [Test]
    public void A_Diagonal_Step_Is_Blocked_Only_By_What_Stands_There()
    {
        // Arrange
        _map.ActorAt[3, 3] = new Wall(1);
        _map.ActorAt[4, 4] = new Door(0);

        // Act / Assert
        Assert.That(Sight().CheckDiag(2, 2), Is.True);
        Assert.That(Sight().CheckDiag(3, 3), Is.False);
        Assert.That(Sight().CheckDiag(4, 4), Is.False);
    }
}
