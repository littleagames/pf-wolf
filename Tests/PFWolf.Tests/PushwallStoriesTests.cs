using System.Reflection;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

/// <summary>
/// Pushwalls moving all of their stories, the player's and up, or only the player's
/// (Gameplay/PushWalls.cs SplitStories, A_PushWall and A_MoveWall's stories, game-info pushwall-stories)
/// </summary>
public class PushwallStoriesTests
{
    private const int AreaTile = 108;
    private const int WallId = 12;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private PushWalls _pushWalls = null!;

    [SetUp]
    public void MakeLevel()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session, null)
        {
            MapWidth = MapConstants.ClassicMapSize, MapHeight = MapConstants.ClassicMapSize,
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            LevelWallStories = 4,
        };
        typeof(MapManager).GetField("mapsegs", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        typeof(MapManager).GetField("_floors", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, new FloorCodes(AreaTile, 45, 106, -1, 162));
        typeof(MapManager).GetField("_levelMapData", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_map, new MapObjectTranslationAsset
        {
            Walls = { [WallId] = new MapTextureTranslation { North = "PLAIN" } },
        });
        for (int y = 0; y < MapConstants.ClassicMapSize; y++)
            for (int x = 0; x < MapConstants.ClassicMapSize; x++)
                _map.SetMapSpot(x, y, 0, AreaTile + 3);

        // the player west of the wall at 10,10, pushing it east
        _session.PlayerState.Pawn = new PlayerPawn
        {
            State = _session.PlayerState, X = (8 << MapConstants.TileShift) + 0x8000, Y = (10 << MapConstants.TileShift) + 0x8000, AreaNumber = 3,
        };
        _pushWalls = new PushWalls(_map, null!, _session, () => 2);
    }

    // A wall placed as LoadMap leaves it, with blocks of its own, at the player's story
    private void Wall(int x, int y, int start, int stop, int start2 = 0, int stop2 = 0)
    {
        _map.SetMapSpot(x, y, 0, WallId);
        _map.SetBlocks(x, y, start, stop, start2, stop2);
        _map.ApplyStory();
    }

    // Lets every moving wall slide as far as it goes
    private void Slide()
    {
        _session.Tics = 128;
        for (int i = 0; i < 4 && _pushWalls.MovingWalls.Count > 0; i++)
            _pushWalls.MovePWalls();
    }

    private static (int, int) B(int start, int stop) => (start, stop);

    [TestCase("all", new[] { 1, 4 }, new int[0])]
    [TestCase("above", new[] { 2, 4 }, new[] { 1, 1 })]
    [TestCase("story", new[] { 2, 2 }, new[] { 1, 1, 3, 4 })]
    public void A_Pushwall_Takes_Its_Stories_And_Leaves_The_Rest(string mode, int[] piece, int[] left)
    {
        // Arrange: a wall from story 1 to 4, the player on story 2
        _map.Story = 2;
        Wall(10, 10, 1, 4);

        // Act
        bool fits = _pushWalls.SplitStories(10, 10, 1, 0, PushWalls.ParseStories(mode, "test")!.Value, out var moved, out var stays, out _);

        // Assert
        Assert.That(fits, Is.True);
        Assert.That(moved.SelectMany(b => new[] { b.Start, b.Stop }), Is.EqualTo(piece));
        Assert.That(stays.SelectMany(b => new[] { b.Start, b.Stop }), Is.EqualTo(left));
    }

    [Test]
    public void Only_A_Wall_With_No_Block_Of_Its_Own_Moving_Whole_Keeps_The_Levels_Height()
    {
        _map.SetMapSpot(10, 10, 0, WallId);
        _map.ApplyStory();
        Assert.That(_pushWalls.SplitStories(10, 10, 1, 0, PushwallStories.All, out _, out _, out bool whole) && whole, Is.True);
        _map.SetBlock(10, 10, 1, 4);
        Assert.That(_pushWalls.SplitStories(10, 10, 1, 0, PushwallStories.All, out _, out _, out whole) && whole, Is.False);
    }

    [Test]
    public void A_Push_That_Would_Leave_Three_Blocks_Is_Refused()
    {
        // Arrange: on story 2, a wall from 1 to 4 and another block at 6 to 7: taking story 2
        // out leaves 1, 3-4 and 6-7
        _map.Story = 2;
        Wall(10, 10, 1, 4, 6, 7);

        // Act / Assert
        Assert.That(_pushWalls.SplitStories(10, 10, 1, 0, PushwallStories.Story, out _, out _, out _), Is.False);
        Assert.That(_pushWalls.PushWall(10, 10, ControlDir.East, stories: PushwallStories.Story), Is.False);
        Assert.That(_map.TileMap[10, 10], Is.EqualTo(WallId));
    }

    [Test]
    public void A_Push_That_Could_End_On_A_Tile_Needing_Three_Blocks_Is_Refused()
    {
        // Arrange: on story 3, the tile ahead has blocks on stories 1 and 5
        _map.Story = 3;
        Wall(10, 10, 1, 4);
        _map.SetBlocks(11, 10, 1, 1, 5, 5);
        _map.ApplyStory();

        // Act / Assert: story 3 alone would sit between them
        Assert.That(_pushWalls.SplitStories(10, 10, 1, 0, PushwallStories.Story, out _, out _, out _), Is.False);
        Assert.That(_pushWalls.SplitStories(10, 10, 1, 0, PushwallStories.Above, out _, out _, out _), Is.True);   // 3-4 touches 5
    }

    [Test]
    public void Pushing_Only_The_Players_Story_Leaves_A_Hole_With_The_Rest_Around_It()
    {
        // Arrange
        _map.Story = 2;
        Wall(10, 10, 1, 4);

        // Act
        bool pushed = _pushWalls.PushWall(10, 10, ControlDir.East, stories: PushwallStories.Story);
        Slide();

        // Assert: the tile it left keeps its wall below and above, walked through at story 2
        Assert.That(pushed, Is.True);
        Assert.That((_map.BlockSpan(10, 10), _map.UpperSpan(10, 10)), Is.EqualTo((((int, int)?)B(1, 1), ((int, int)?)B(3, 4))));
        Assert.That((_map.TileMap[10, 10], _map.MapSpot(10, 10, 0)), Is.EqualTo(((ushort)0, WallId)));
        Assert.That(_map.AreaAt(10, 10), Is.EqualTo(3));
        // it stops two tiles on, just story 2
        Assert.That(_map.TileMap[12, 10], Is.EqualTo(WallId));
        Assert.That((_map.BlockSpan(12, 10), _map.UpperSpan(12, 10)), Is.EqualTo((((int, int)?)B(2, 2), ((int, int)?)null)));
        Assert.That(_map.TileBlocks(11, 10), Is.Empty);     // the tile it crossed is as it was
    }

    [Test]
    public void Where_It_Stops_It_Joins_The_Tiles_Own_Blocks()
    {
        // Arrange: on story 2, a floor block on story 1 where it stops
        _map.Story = 2;
        Wall(10, 10, 1, 4);
        _map.SetBlock(12, 10, 1, 1);

        // Act
        _pushWalls.PushWall(10, 10, ControlDir.East, stories: PushwallStories.Above);
        Slide();

        // Assert: 1 and 2-4 are one block from 1 to 4
        Assert.That((_map.BlockSpan(12, 10), _map.UpperSpan(12, 10)), Is.EqualTo((((int, int)?)B(1, 4), ((int, int)?)null)));
        Assert.That(_map.BlockSpan(10, 10), Is.EqualTo(((int, int)?)B(1, 1)));
    }

    [Test]
    public void A_Wall_With_No_Block_Of_Its_Own_Moves_As_It_Always_Did()
    {
        // Arrange: story 1, a plain wall
        _map.SetMapSpot(10, 10, 0, WallId);
        _map.ApplyStory();

        // Act
        _pushWalls.PushWall(10, 10, ControlDir.East);
        Slide();

        // Assert: nothing on the height plane, the floor where it was
        Assert.That(_map.MapSpot(12, 10, MapManager.HeightPlane), Is.Zero);
        Assert.That(_map.MapSpot(10, 10, MapManager.HeightPlane), Is.Zero);
        Assert.That((_map.TileMap[12, 10], _map.MapSpot(12, 10, 0)), Is.EqualTo(((ushort)WallId, WallId)));
        Assert.That(_map.MapSpot(10, 10, 0), Is.EqualTo(AreaTile + 3));
    }

    [Test]
    public void The_Levels_Setting_Is_Used_When_The_Action_Doesnt_Say()
    {
        _map.Story = 2;
        _map.PushwallStories = PushwallStories.Above;
        Wall(10, 10, 1, 4);

        _pushWalls.PushWall(10, 10, ControlDir.East);

        Assert.That((_pushWalls.MovingWalls[0].Start, _pushWalls.MovingWalls[0].Stop), Is.EqualTo(((byte)2, (byte)4)));
        Assert.That(_map.BlockSpan(10, 10), Is.EqualTo(((int, int)?)B(1, 1)));
    }

    [TestCase(0, 0, 0, 0, 1, 4)]        // a wall with no block of its own: the level's height
    [TestCase(2, 3, 0, 0, 2, 3)]
    [TestCase(1, 2, 4, 6, 1, 6)]        // two parts: drawn from the lower's start to the upper's stop
    public void A_Moving_Wall_Is_Drawn_Over_Its_Stories(int start, int stop, int start2, int stop2, int first, int top)
    {
        var wall = new MovingPushWall { Start = (byte)start, Stop = (byte)stop, Start2 = (byte)start2, Stop2 = (byte)stop2 };
        Assert.That(wall.Span(4), Is.EqualTo((first, top)));
    }

    [Test]
    public void A_Moving_Walls_Stories_Are_Saved()
    {
        var wall = new MovingPushWall { X = 3, Y = 4, Start = 2, Stop = 3, Start2 = 5, Stop2 = 6 };
        using var stream = new MemoryStream();
        wall.Write(new BinaryWriter(stream));
        stream.Position = 0;
        var read = MovingPushWall.Read(new BinaryReader(stream));
        Assert.That((read.Start, read.Stop, read.Start2, read.Stop2), Is.EqualTo(((byte)2, (byte)3, (byte)5, (byte)6)));
    }

    [TestCase("all", "All")]
    [TestCase(" Above ", "Above")]
    [TestCase("story", "Story")]
    [TestCase("", null)]
    [TestCase("sideways", null)]
    public void Stories_Are_All_Above_Or_Story(string text, string? mode)
    {
        Assert.That(PushWalls.ParseStories(text, "test")?.ToString(), Is.EqualTo(mode));
    }
}
