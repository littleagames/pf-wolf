using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class PushWallsTests
{
    // Finding the wall on a tile only looks at the list
    private static PushWalls With(params MovingPushWall[] walls)
    {
        var pushWalls = new PushWalls(null!, null!, new GameSession());
        pushWalls.pushwalls.AddRange(walls);
        return pushWalls;
    }

    private static MovingPushWall Wall(ushort x, ushort y, ControlDir dir) => new() { X = x, Y = y, Dir = dir, Tile = 7 };

    [TestCase(10, 10, true)]        // the tile it's leaving
    [TestCase(11, 10, true)]        // the tile ahead, east
    [TestCase(9, 10, false)]        // behind it
    [TestCase(10, 11, false)]
    public void A_Wall_Is_On_The_Tile_It_Leaves_And_The_One_Ahead(int x, int y, bool on)
    {
        // Arrange
        var wall = Wall(10, 10, ControlDir.East);

        // Act / Assert
        Assert.That(With(wall).PushWallAt(x, y), on ? Is.SameAs(wall) : Is.Null);
    }

    [Test]
    public void The_Renderer_Keeps_The_Last_Wall_When_A_Tile_Has_None()
    {
        // Arrange
        var north = Wall(5, 5, ControlDir.North);
        var pushWalls = With(Wall(20, 20, ControlDir.South), north);

        // Act
        pushWalls.SelectPushWall(5, 4);
        pushWalls.SelectPushWall(40, 40);

        // Assert
        Assert.That(pushWalls.pwall, Is.SameAs(north));
    }

    [Test]
    public void Clearing_Stops_Them_All()
    {
        // Arrange
        var pushWalls = With(Wall(1, 1, ControlDir.North), Wall(2, 2, ControlDir.West));

        // Act
        pushWalls.ClearPushWalls();

        // Assert
        Assert.That(pushWalls.pushwalls, Is.Empty);
    }

    [TestCase(0, 0, true)]                  // the tile the player is in
    [TestCase(1, 0, true)]                  // reached into, east, by the box's edge
    [TestCase(-1, 0, false)]
    [TestCase(0, 1, false)]
    public void A_Players_Box_Reaches_Into_Neighbouring_Tiles(int dx, int dy, bool covers)
    {
        // Arrange: in tile (10, 10), close to its east edge
        var pawn = new PlayerPawn { X = (10 << MapConstants.TileShift) + 0xF000, Y = (10 << MapConstants.TileShift) + 0x8000 };

        // Act / Assert
        Assert.That(PushWalls.PlayerBoxCovers(pawn, 10 + dx, 10 + dy), Is.EqualTo(covers));
    }

    [Test]
    public void A_Moving_Wall_Reads_Back_What_It_Wrote()
    {
        // Arrange
        var wall = new MovingPushWall { State = 130, Pos = 2, X = 30, Y = 40, Dir = ControlDir.South, Tile = 200 };
        using var stream = new MemoryStream();

        // Act
        wall.Write(new BinaryWriter(stream));
        stream.Position = 0;
        var read = MovingPushWall.Read(new BinaryReader(stream));

        // Assert
        Assert.That((read.State, read.Pos, read.X, read.Y, read.Dir, read.Tile), Is.EqualTo((wall.State, wall.Pos, wall.X, wall.Y, wall.Dir, wall.Tile)));
    }

    [Test]
    public void A_Wall_Off_The_Map_Isnt_Read()
    {
        // Arrange
        var wall = new MovingPushWall { X = (ushort)MapManager.MapSize, Y = 1 };
        using var stream = new MemoryStream();
        wall.Write(new BinaryWriter(stream));
        stream.Position = 0;

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => MovingPushWall.Read(new BinaryReader(stream)));
    }

    [TestCase(null, 2)]         // game-info leaves it out: Wolf3D's two tiles
    [TestCase(3, 3)]            // End of Destiny's
    [TestCase(0, 1)]            // a wall always moves a tile
    public void A_Wall_Slides_As_Far_As_Game_Info_Says(int? setting, int tiles)
    {
        // Arrange
        var pushWalls = new PushWalls(null!, null!, new GameSession(), () => setting);

        // Act / Assert
        Assert.That(pushWalls.Distance, Is.EqualTo(tiles));
    }

    [Test]
    public void Directions_Are_North_East_South_West()
    {
        Assert.That(PushWalls.dirs[(int)ControlDir.North], Is.EqualTo(new[] { 0, -1 }));
        Assert.That(PushWalls.dirs[(int)ControlDir.East], Is.EqualTo(new[] { 1, 0 }));
        Assert.That(PushWalls.dirs[(int)ControlDir.South], Is.EqualTo(new[] { 0, 1 }));
        Assert.That(PushWalls.dirs[(int)ControlDir.West], Is.EqualTo(new[] { -1, 0 }));
    }
}
