using System.Reflection;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

/// <summary>The story a thing stands on: plane 6, else the floor under it (MapManager.ThingStory, Actor.Story)</summary>
public class ThingStoriesTests
{
    private const int AreaTile = 108;
    private const int WallId = 12;

    private MapManager _map = null!;

    [SetUp]
    public void MakeLevel()
    {
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, new GameSession(), null)
        {
            MapWidth = MapConstants.ClassicMapSize, MapHeight = MapConstants.ClassicMapSize,
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            LevelWallStories = 2,
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
    }

    [Test]
    public void On_Story_1_Everything_Is_On_The_Players_Story()
    {
        Assert.That(_map.ThingStory(10, 10), Is.EqualTo(1));
    }

    [Test]
    public void On_A_Roof_A_Thing_Stands_On_The_Players_Floor_And_On_The_Street_On_The_Ground()
    {
        // Arrange: on story 3, a 2-story wall (a roof) at 10,10, open floor at 11,10
        _map.Story = 3;
        _map.SetMapSpot(10, 10, 0, WallId);
        _map.ApplyStory();

        // Act / Assert
        Assert.That(_map.ThingStory(10, 10), Is.EqualTo(3));
        Assert.That(_map.ThingStory(11, 10), Is.EqualTo(1));
    }

    [Test]
    public void A_Thing_Stands_On_The_Highest_Block_Under_The_Players_Story()
    {
        // Arrange: on story 4, a block on story 1 alone, and one on 1 and 6 to 7
        _map.Story = 4;
        _map.SetBlock(10, 10, 1, 1);
        _map.SetBlocks(11, 10, 1, 1, 6, 7);
        _map.ApplyStory();

        // Act / Assert: on story 2, its top; the block overhead doesn't count
        Assert.That(_map.ThingStory(10, 10), Is.EqualTo(2));
        Assert.That(_map.ThingStory(11, 10), Is.EqualTo(2));
    }

    [Test]
    public void Plane_6_Sets_A_Things_Story()
    {
        _map.Story = 3;
        _map.SetMapSpot(10, 10, MapManager.StoryPlane, 5);
        Assert.That(_map.ThingStory(10, 10), Is.EqualTo(5));
        Assert.That(_map.ThingStory(10, 10, set: 0), Is.EqualTo(1), "a UWMF extra thing's own setting in place of the plane");
    }

    [Test]
    public void A_Thing_In_A_Wall_Is_On_The_Players_Story()
    {
        _map.Story = 2;
        _map.SetMapSpot(10, 10, 0, WallId);
        _map.SetBlock(10, 10, 1, 3);
        _map.ApplyStory();
        Assert.That(_map.ThingStory(10, 10), Is.EqualTo(2));
    }

    [Test]
    public void An_Actors_Story_Is_Its_Own_Or_The_Players()
    {
        _map.Story = 3;
        var onPlayers = new Monster { Name = "Barrel" };
        var onStreet = new Monster { Name = "Lamp", Story = 1 };
        Assert.That((_map.StoryOf(onPlayers), _map.StoryOf(onStreet)), Is.EqualTo((3, 1)));
        Assert.That((_map.OnPlayersStory(onPlayers), _map.OnPlayersStory(onStreet)), Is.EqualTo((true, false)));
    }

    [Test]
    public void An_Actors_Story_Is_Saved()
    {
        var snapshot = ActorSnapshot.Capture(new Monster { Name = "Lamp", Story = 2 });
        using var stream = new MemoryStream();
        snapshot.Write(new BinaryWriter(stream));
        stream.Position = 0;
        Assert.That(ActorSnapshot.Read(new BinaryReader(stream)).Story, Is.EqualTo(2));
    }
}
