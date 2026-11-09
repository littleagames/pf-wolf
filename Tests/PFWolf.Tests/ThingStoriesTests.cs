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

    // A guard standing on a tile, on a story (0: the player's)
    private Monster Guard(int x, int y, int story = 0)
    {
        var guard = new Monster { Name = "Guard", Story = story, RuntimeFlags = ObjFlags.Shootable };
        guard.SetPosition(x, y);
        _map.GetActors().AddLast(guard);
        return guard;
    }

    // Rooftops: the player on story 3, 2-story walls (roofs) at x 10 to 12 on row 10, a street
    // (open floor) at x 13 to 15 on it, a door at 16,10
    private void MakeRooftops()
    {
        _map.Story = 3;
        for (int x = 10; x <= 12; x++)
            _map.SetMapSpot(x, 10, 0, WallId);
        _map.TileMap[16, 10] = (ushort)(GameConstants.BitDoor | 0);
        _map.ApplyStory();
    }

    [Test]
    public void On_Another_Story_Whats_Solid_Comes_From_The_Blocks()
    {
        // Arrange
        MakeRooftops();

        // Act / Assert: the roofs are walls on the street (story 1), and walked on at story 3
        Assert.That(_map.StorySolid(10, 10, 1), Is.True, "a roof's wall on the street");
        Assert.That(_map.StorySolid(10, 10, 3), Is.False, "walked on at the player's story");
        Assert.That(_map.StorySolid(13, 10, 1), Is.False, "the street");
        Assert.That(_map.StorySolid(16, 10, 1), Is.True, "a door's tile, off the player's story");
        Assert.That(_map.StorySolid(0, 10, 1), Is.True, "the map's edge");
        Assert.That(_map.ActorOn(10, 10, 1), Is.InstanceOf<Wall>());
        Assert.That(_map.ActorOn(13, 10, 1), Is.Null);
    }

    [Test]
    public void An_Enemy_On_Another_Story_Is_Marked_On_Its_Storys_Grid_Not_ActorAt()
    {
        // Arrange
        MakeRooftops();
        var guard = Guard(13, 10, story: 1);

        // Act
        _map.MarkActorTile(guard);

        // Assert: the player walks over it (ActorAt), others on the street don't
        Assert.That(_map.ActorAt[13, 10], Is.Null);
        Assert.That((_map.ActorOn(13, 10, 1) as ActorMark)?.Who, Is.SameAs(guard));
        Assert.That(_map.ActorOn(13, 10, 3), Is.Null);

        _map.UnmarkActorTile(guard);
        Assert.That(_map.ActorOn(13, 10, 1), Is.Null);
    }

    [Test]
    public void Unmarking_An_Enemy_Off_The_Players_Story_Leaves_ActorAt_Alone()
    {
        // Arrange: one on the player's story and one under it, on the same tile
        MakeRooftops();
        var above = Guard(13, 10);
        var below = Guard(13, 10, story: 1);
        _map.MarkActorTile(above);
        _map.MarkActorTile(below);

        // Act
        _map.UnmarkActorTile(below);

        // Assert
        Assert.That(_map.ActorMarkAt(13, 10), Is.SameAs(above));
    }

    [Test]
    public void An_Enemy_Is_Never_Marked_Inside_A_Block_On_Its_Story()
    {
        MakeRooftops();
        var guard = Guard(10, 10, story: 1);
        _map.MarkActorTile(guard);
        Assert.That(_map.ActorOn(10, 10, 1), Is.InstanceOf<Wall>());
        Assert.That(_map.StoryGrid(1)[10, 10], Is.Null);
    }

    [Test]
    public void Someone_In_The_Way_Is_Only_Counted_On_Its_Own_Story()
    {
        MakeRooftops();
        Guard(13, 10, story: 1);
        Assert.That(_map.IsShootableActorAt(13, 10), Is.False, "the player's story");
        Assert.That(_map.IsShootableActorAt(13, 10, 1), Is.True);
    }

    [Test]
    public void A_Solid_Thing_Off_The_Players_Story_Blocks_Its_Own_Story()
    {
        // Arrange: a barrel on the street, put there from the player's story
        MakeRooftops();
        var barrel = new Health { Name = "Barrel", Flags = ["SOLID"] };
        barrel.SetPosition(14, 10);
        _map.GetActors().AddLast(barrel);
        _map.ActorAt[14, 10] = new BlockingActor();

        // Act
        _map.MoveToStory([barrel], 1);

        // Assert
        Assert.That(_map.ActorAt[14, 10], Is.Null, "the player walks over it");
        Assert.That(_map.ActorOn(14, 10, 1), Is.InstanceOf<BlockingActor>());
    }

    [Test]
    public void Moving_An_Enemy_To_The_Players_Story_Marks_It_In_ActorAt()
    {
        MakeRooftops();
        var guard = Guard(13, 10, story: 1);
        _map.MarkActorTile(guard);

        _map.MoveToStory([guard], 3);

        Assert.That(guard.Story, Is.EqualTo(0));
        Assert.That(_map.ActorMarkAt(13, 10), Is.SameAs(guard));
        Assert.That(_map.ActorOn(13, 10, 1), Is.Null);
    }

    [Test]
    public void When_The_Player_Goes_To_Another_Story_Its_Things_Move_Into_ActorAt()
    {
        // Arrange: a guard on the street, and one following the player (story 0)
        MakeRooftops();
        var street = Guard(13, 10, story: 1);
        var follower = Guard(14, 10);
        _map.MarkActorTile(street);
        _map.MarkActorTile(follower);

        // Act: the player goes down to the street (the `story` cheat)
        _map.Story = 1;
        _map.ApplyStory();
        _map.RebuildStoryGrids();

        // Assert: the street's guard is in ActorAt now; the one on the player's story moved with them
        Assert.That(_map.ActorMarkAt(13, 10), Is.SameAs(street));
        Assert.That(_map.ActorMarkAt(14, 10), Is.SameAs(follower));
        Assert.That(_map.ActorOn(13, 10, 3), Is.Null, "nothing left on the roofs' story");
    }

    // Sight across stories (phase 4): a LineOfSight on the test level, with no doors of its own
    private Gameplay.LineOfSight Sight() =>
        new(_map, new Gameplay.Doors(null!, null!, _map, null!, null!, new GameSession(), null!), new GameSession(), (_, _, _, _) => false);

    [Test]
    public void A_Guard_Under_A_Roof_Edge_Cant_See_Someone_Back_On_The_Roof()
    {
        // Arrange: the player on the roof at 11,10 (story 3), a guard on the street at 13,10
        MakeRooftops();
        var player = Guard(11, 10);
        var guard = Guard(13, 10, story: 1);

        // Act / Assert: the line climbs through the roof at 12,10
        Assert.That(Sight().CheckLine(guard, player), Is.False);
    }

    [Test]
    public void A_Guard_Out_On_The_Street_Sees_Someone_At_The_Roofs_Edge()
    {
        MakeRooftops();
        var player = Guard(12, 10);
        var guard = Guard(15, 10, story: 1);
        Assert.That(Sight().CheckLine(guard, player), Is.True);
        Assert.That(Sight().CheckLine(player, guard), Is.True, "and the other way");
    }

    [Test]
    public void A_Guard_Under_A_Bridge_Doesnt_See_The_Player_Standing_On_It()
    {
        // Arrange: a bridge's deck (a block on story 2) over the street at 14,10; a thing in a
        // wall (the roof at 11,10, inside its block on story 1) beside the street
        MakeRooftops();
        _map.SetBlock(14, 10, 2, 2);
        _map.ApplyStory();
        var player = Guard(14, 10);
        var under = Guard(14, 10, story: 1);
        var inWall = Guard(12, 10, story: 1);

        // Act / Assert: the deck between them on their own tile; one inside a block sees out of it
        Assert.That(Sight().CheckLine(under, player), Is.False);
        Assert.That(Sight().CheckLine(inWall, Guard(13, 10, story: 1)), Is.True);
    }

    [Test]
    public void On_Another_Story_The_Blocks_And_Doors_Stop_A_Line_Along_It()
    {
        // Arrange: on the street (story 1), open at 9,10 and 13 to 15,10, the roofs between,
        // the door at 16,10; the player's story is 3
        MakeRooftops();
        var west = Guard(9, 10, story: 1);
        var east = Guard(13, 10, story: 1);
        var near = Guard(15, 10, story: 1);
        var beyond = Guard(17, 10, story: 1);
        var sight = Sight();

        // Act / Assert
        Assert.That(sight.CheckLine(east, near), Is.True, "along the street");
        Assert.That(sight.CheckLine(west, east), Is.False, "through the roofs' walls");
        Assert.That(sight.CheckLine(near, beyond), Is.False, "a door's tile off the player's story");
    }

    [Test]
    public void Distance_Counts_A_Story_As_A_Tile()
    {
        MakeRooftops();
        var player = Guard(13, 10);
        Assert.That(_map.TileDistance(Guard(13, 10, story: 1), player), Is.EqualTo(2));
        Assert.That(_map.TileDistance(Guard(16, 11, story: 1), player), Is.EqualTo(3));
    }

    [Test]
    public void A_Projectile_Climbs_To_Meet_Its_Target_On_Another_Story()
    {
        // Arrange: from the street (story 1) to the roof (story 3), 2 tiles off, at 1/8 tile a tic
        MakeRooftops();
        var guard = Guard(13, 10, story: 1);
        var player = Guard(11, 10);
        var rocket = new Monster { Name = "Rocket", Speed = 0x2000 };
        var level = new Monster { Name = "Rocket", Speed = 0x2000 };

        // Act
        _map.AimAcrossStories(rocket, guard, player);
        _map.AimAcrossStories(level, player, Guard(14, 10));

        // Assert: 128 texels up over 16 tics; one on the player's story is left as it was
        Assert.That((rocket.Story, rocket.ZSpeed), Is.EqualTo((1, 8.0)));
        Assert.That((level.Story, level.ZSpeed), Is.EqualTo((0, 0.0)));
        Assert.That(MapManager.StoryAtHeight(_map.MiddleHeight(rocket) + 8 * 10), Is.EqualTo(2), "10 tics up it's in story 2");
        Assert.That(MapManager.StoryAtHeight(_map.MiddleHeight(rocket) + 8 * 16), Is.EqualTo(3), "and at the end in story 3");
    }

    [Test]
    public void A_Blast_Weakens_A_Tile_A_Story_Up_Or_Down()
    {
        Assert.That(Gameplay.Missiles.BlastFalloffAt(100, 3, 0, 0, 0, 0, stories: 2), Is.EqualTo(25));
        Assert.That(Gameplay.Missiles.BlastFalloffAt(100, 3, 0, 0, 0, 0, stories: 3), Is.EqualTo(0), "out of its reach");
        Assert.That(Gameplay.Missiles.BlastFalloffAt(100, 3, 0, 0, 0, 0), Is.EqualTo(100));
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
