using System.Reflection;
using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Tests;

/// <summary>
/// Tiles' blocks (plane 3: start and stop stories) against the player's story: what's solid,
/// walked on or overhead (MapManager's blocks)
/// </summary>
public class StoriesTests
{
    private const int AreaTile = 108;
    private const int WallId = 12, OtherWall = 13, BlockWall = 250;
    private const int SidedFloor = 5;

    private MapManager _map = null!;

    private static MapObjectTranslationAsset MapData() => new()
    {
        Walls =
        {
            [WallId] = new MapTextureTranslation { North = "PLAIN", South = "PLAIN", East = "PLAINE", West = "PLAINE" },
            [OtherWall] = new MapTextureTranslation { North = "OTHER" },
        },
        Flats = new MapFlatsTranslation { Sides = { [SidedFloor] = "SIDES" } },
    };

    [SetUp]
    public void MakeLevel()
    {
        var session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, session, null)
        {
            MapWidth = MapConstants.ClassicMapSize, MapHeight = MapConstants.ClassicMapSize,
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            LevelWallStories = 2,
            BlockWall = BlockWall,
        };
        typeof(MapManager).GetField("mapsegs", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        typeof(MapManager).GetField("_floors", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, new FloorCodes(AreaTile, 45, 106, -1, 162));
        typeof(MapManager).GetField("_levelMapData", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_map, MapData());

        // open floor in area 3 everywhere
        for (int y = 0; y < MapConstants.ClassicMapSize; y++)
            for (int x = 0; x < MapConstants.ClassicMapSize; x++)
                _map.SetMapSpot(x, y, 0, AreaTile + 3);
    }

    // A wall placed as LoadMap leaves one solid at story 1
    private void Wall(int x, int y, int id = WallId)
    {
        _map.SetMapSpot(x, y, 0, (ushort)id);
        _map.TileMap[x, y] = (ushort)id;
        _map.ActorAt[x, y] = new Wall(id);
    }

    [TestCase(0, null, null)]
    [TestCase(3, 1, 3)]                 // a plain stop: a tall wall from story 1
    [TestCase(0x0305, 3, 5)]
    [TestCase(0x0105, 1, 5)]
    [TestCase(0x0503, null, null)]      // starting above where it stops
    [TestCase(9, null, null)]           // past story 8
    [TestCase(0x0900 | 9, null, null)]
    public void A_Height_Plane_Value_Is_A_Start_And_A_Stop(int value, int? start, int? stop)
    {
        var block = MapManager.DecodeBlock(value);
        Assert.That(block?.Start, Is.EqualTo(start));
        Assert.That(block?.Stop, Is.EqualTo(stop));
    }

    [TestCase(1, 3, 3)]                 // from story 1 it's written as a tall wall always was
    [TestCase(3, 5, 0x0305)]
    public void A_Block_Is_Written_Back_As_It_Is_Read(int start, int stop, int value)
    {
        Assert.That(MapManager.EncodeBlock(start, stop), Is.EqualTo(value));
        Assert.That(MapManager.DecodeBlock(value), Is.EqualTo(((int, int)?)(start, stop)));
    }

    [Test]
    public void A_Wall_With_No_Block_Of_Its_Own_Runs_From_Story_1_To_The_Levels_Wall_Height()
    {
        Wall(10, 10);
        Assert.That(_map.BlockSpan(10, 10), Is.EqualTo(((int, int)?)(1, 2)));
        Assert.That(_map.BlockSpan(11, 10), Is.Null);       // open floor: no block at all
    }

    [TestCase(1, true)]
    [TestCase(3, true)]
    [TestCase(5, true)]
    [TestCase(2, false)]                // below it: walked under
    [TestCase(6, false)]                // above it: walked on
    public void A_Block_Is_Solid_Where_It_Takes_In_The_Players_Story(int story, bool solid)
    {
        // Arrange: a wall from story 3 to 5 (from 1 to 5 for story 1)
        Wall(10, 10);
        _map.SetBlock(10, 10, story == 1 ? 1 : 3, 5);
        _map.Story = story;

        // Act
        _map.ApplyStory();

        // Assert
        Assert.That(_map.TileMap[10, 10] != 0, Is.EqualTo(solid));
        Assert.That(_map.ActorAt[10, 10] is Wall, Is.EqualTo(solid));
        Assert.That(_map.MapSpot(10, 10, 0), Is.EqualTo(WallId));      // plane 0 keeps the wall either way
    }

    [Test]
    public void A_Wall_Walked_On_Takes_Its_Neighbours_Area()
    {
        // Arrange: a wall up to story 2 in area 3, the player on story 3
        Wall(10, 10);
        _map.Story = 3;

        // Act
        _map.ApplyStory();

        // Assert
        Assert.That(_map.FloorCode(10, 10), Is.EqualTo(AreaTile + 3));
        Assert.That(_map.AreaAt(10, 10), Is.EqualTo(3));
    }

    [Test]
    public void A_Run_Of_Walls_Walked_On_Takes_The_Nearest_Area()
    {
        // Arrange: walls from x 10 to 14 on row 10, area 4 east of them, area 3 west
        for (int x = 10; x <= 14; x++)
            Wall(x, 10);
        _map.SetMapSpot(15, 10, 0, AreaTile + 4);
        _map.Story = 3;

        // Act
        _map.ApplyStory();

        // Assert: area 3 on every side of most of them; the one beside area 4 is nearer it too
        Assert.That(_map.AreaAt(10, 10), Is.EqualTo(3));
        Assert.That(_map.AreaAt(14, 10), Is.AnyOf(3, 4));
    }

    [Test]
    public void A_Floor_Tile_With_A_Block_Is_Solid_Where_It_Takes_In_The_Story()
    {
        // Arrange: open floor with a block from 1 to 2
        _map.SetBlock(10, 10, 1, 2);

        // Act / Assert: solid at story 1, as a wall id the mapdefs don't give
        _map.ApplyStory();
        Assert.That(_map.TileMap[10, 10], Is.EqualTo(BlockWall));
        Assert.That(_map.MapSpot(10, 10, 0), Is.EqualTo(AreaTile + 3));    // still floor on plane 0

        // stood on at story 3
        _map.Story = 3;
        _map.ApplyStory();
        Assert.That((_map.TileMap[10, 10], _map.ActorAt[10, 10]), Is.EqualTo(((ushort)0, (Actor?)null)));
    }

    [Test]
    public void A_Floor_Tile_With_A_Block_Above_The_Story_Is_Overhead()
    {
        _map.SetBlock(10, 10, 2, 3);
        _map.ApplyStory();
        Assert.That(_map.TileMap[10, 10], Is.Zero);
        Assert.That(_map.IsOverhead(10, 10), Is.True);
        Assert.That(_map.IsOverhead(11, 10), Is.False);
    }

    [Test]
    public void A_Walls_Thrown_Switch_Stays_As_It_Is_When_It_Stays_Solid()
    {
        // Arrange: a solid wall that's become another (a switch, a crack)
        Wall(10, 10);
        _map.TileMap[10, 10] = OtherWall | GameConstants.BitWall;

        // Act
        _map.ApplyStory();

        // Assert
        Assert.That(_map.TileMap[10, 10], Is.EqualTo(OtherWall | GameConstants.BitWall));
    }

    [Test]
    public void Doors_And_Moving_Pushwalls_Are_Left_Alone()
    {
        _map.TileMap[10, 10] = GameConstants.BitDoor | 3;
        _map.TileMap[11, 10] = GameConstants.BitWall;
        _map.Story = 5;
        _map.ApplyStory();
        Assert.That((_map.TileMap[10, 10], _map.TileMap[11, 10]), Is.EqualTo(((ushort)(GameConstants.BitDoor | 3), (ushort)GameConstants.BitWall)));
    }

    [Test]
    public void A_Floor_Blocks_Sides_Are_Its_Flats_Sides_Else_A_Wall_Beside_It()
    {
        _map.SetMapSpot(10, 10, MapManager.FlatPlane, SidedFloor);
        Assert.That(_map.BlockSideTexture(10, 10), Is.EqualTo("SIDES"));

        Wall(21, 20);                   // east of 20,20: its west face
        Assert.That(_map.BlockSideTexture(20, 20), Is.EqualTo("PLAINE"));
        Assert.That(_map.BlockSideTexture(20, 20, neighbours: false), Is.Null);
        Assert.That(_map.BlockSideTexture(30, 30), Is.Null);
    }

    [Test]
    public void Moving_A_Block_Takes_Its_Start_And_Stop()
    {
        _map.SetBlock(10, 10, 3, 5);
        _map.MoveWallStories(10, 10, 11, 10);
        Assert.That(_map.BlockSpan(11, 10), Is.EqualTo(((int, int)?)(3, 5)));
        Assert.That(_map.MapSpot(11, 10, MapManager.HeightPlane), Is.EqualTo(0x0305));
        Assert.That(_map.MapSpot(10, 10, MapManager.HeightPlane), Is.Zero);
    }

    [TestCase(1, "stories 3 to 5, overhead")]
    [TestCase(4, "stories 3 to 5, solid")]
    [TestCase(6, "stories 3 to 5, floor below the player")]
    public void The_Console_Says_What_A_Block_Is_At_The_Players_Story(int story, string text)
    {
        _map.SetBlock(10, 10, 3, 5);
        _map.Story = story;
        Assert.That(_map.DescribeBlock(10, 10), Is.EqualTo(text));
        Assert.That(_map.DescribeBlock(11, 10), Is.EqualTo("no block: open floor"));
    }

    [Test]
    public void A_Levels_Own_Sides_Lie_Over_The_Games()
    {
        var game = new MapObjectTranslationAsset();
        game.Flats.Sides[5] = "GSTONEA1";
        game.Flats.Sides[6] = "BSTONEA1";
        var level = new MapObjectTranslationAsset();
        level.Flats.Sides[6] = "WOOD1";

        Assert.That(game.With(level).Flats.Sides, Is.EquivalentTo(new Dictionary<int, string> { [5] = "GSTONEA1", [6] = "WOOD1" }));
    }
}
