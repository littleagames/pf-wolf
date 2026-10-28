using System.Reflection;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;
using PFWolf.Scripting;

namespace PFWolf.Tests;

/// <summary>Walls taken away as the level's played (Gameplay/DestructibleWalls.cs, MapManager.RemoveWall)</summary>
public class DestructibleWallsTests
{
    private const int AreaTile = 108;
    private const int SecretTrigger = 98;
    private const int WallId = 12;
    private const ushort Tag = 4;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private DestructibleWalls _walls = null!;

    [SetUp]
    public void MakeLevel()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session, null)
        {
            MapWidth = MapConstants.ClassicMapSize, MapHeight = MapConstants.ClassicMapSize,
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        typeof(MapManager).GetField("mapsegs", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        typeof(MapManager).GetField("_floors", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, new FloorCodes(AreaTile, 45, 106, -1, 162));
        typeof(MapManager).GetField("_levelMapData", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, new MapObjectTranslationAsset { Triggers = { [SecretTrigger] = new MapTriggerTranslation { Action = "A_PushWall", Secret = true } } });
        _session.Areas.Reset(45);
        var doors = new Doors(null!, null!, _map, null!, null!, _session, null!);
        _walls = new DestructibleWalls(_map, doors, _session);
    }

    // Area 5 west of 10,10 and area 7 east of it, the wall between them
    private void WallBetweenAreas()
    {
        _map.SetMapSpot(9, 10, 0, AreaTile + 5);
        _map.SetMapSpot(11, 10, 0, AreaTile + 7);
        Wall(10, 10);
    }

    private void Wall(int x, int y)
    {
        _map.SetMapSpot(x, y, 0, WallId);
        _map.TileMap[x, y] = WallId;
        _map.ActorAt[x, y] = new Wall(WallId);
    }

    [Test]
    public void A_Removed_Wall_Leaves_Open_Floor_In_An_Area_Beside_It()
    {
        // Arrange
        WallBetweenAreas();

        // Act
        bool removed = _walls.Remove(10, 10);

        // Assert
        Assert.That(removed, Is.True);
        Assert.That(_map.TileMap[10, 10], Is.Zero);
        Assert.That(_map.ActorAt[10, 10], Is.Null);
        Assert.That(_map.MapSpot(10, 10, 0), Is.EqualTo(AreaTile + 5));
    }

    [Test]
    public void The_Areas_It_Stood_Between_Are_Joined_For_Good()
    {
        // Arrange
        WallBetweenAreas();

        // Act
        _walls.Remove(10, 10);

        // Assert
        Assert.That(_session.Areas.AreaConnect[5, 7], Is.EqualTo(1));
        Assert.That(_session.Areas.AreaConnect[7, 5], Is.EqualTo(1));
    }

    [Test]
    public void A_Wall_In_One_Area_Joins_Nothing()
    {
        // Arrange: area 5 on both sides
        WallBetweenAreas();
        _map.SetMapSpot(11, 10, 0, AreaTile + 5);

        // Act
        _walls.Remove(10, 10);

        // Assert
        Assert.That(_session.Areas.AreaConnect[5, 5], Is.Zero);
    }

    [TestCase(false, 3, 0)]     // every story goes
    [TestCase(true, 3, 3)]      // the upper ones stay up as an arch
    [TestCase(true, 1, 0)]      // a one-story wall leaves nothing to stand
    public void A_Tall_Wall_Leaves_An_Arch_Only_When_Asked(bool arch, int stories, int left)
    {
        // Arrange
        WallBetweenAreas();
        _map.SetWallStories(10, 10, stories);

        // Act
        _walls.Remove(10, 10, arch);

        // Assert
        Assert.That(_map.StoryMap[10, 10], Is.EqualTo(left));
        Assert.That(_map.MapSpot(10, 10, MapManager.HeightPlane), Is.EqualTo(left));
    }

    [Test]
    public void An_Arch_From_A_Wall_At_The_Levels_Height_Keeps_That_Height()
    {
        // Arrange: no height of its own, on a level of 2-story walls
        WallBetweenAreas();
        _map.LevelWallStories = 2;

        // Act
        _walls.Remove(10, 10, arch: true);

        // Assert
        Assert.That(_map.StoryMap[10, 10], Is.EqualTo(2));
    }

    [Test]
    public void Its_Shape_Patch_And_Door_Side_Mark_Go_With_It_But_Its_Tag_Stays()
    {
        // Arrange: a diagonal beside a door, with a patch and a tag
        WallBetweenAreas();
        _map.TileMap[10, 10] |= GameConstants.BitWall;
        _map.WallShapes[10, 10] = WallShape.SolidNE;
        _map.SetMapSpot(10, 10, 1, 401);
        _map.SetPatch(10, 10, 3);
        _map.SetTag(10, 10, Tag);

        // Act
        _walls.Remove(10, 10);

        // Assert
        Assert.That(_map.TileMap[10, 10], Is.Zero);
        Assert.That(_map.WallShapes[10, 10], Is.EqualTo(WallShape.Square));
        Assert.That(_map.MapSpot(10, 10, 1), Is.Zero);
        Assert.That(_map.GetPatch(10, 10), Is.Zero);
        Assert.That(_map.GetTag(10, 10), Is.EqualTo(Tag));
    }

    [Test]
    public void A_Secret_Trigger_On_It_Counts_As_Found()
    {
        // Arrange
        WallBetweenAreas();
        _map.SetMapSpot(10, 10, 1, SecretTrigger);

        // Act
        _walls.Remove(10, 10);

        // Assert
        Assert.That(_session.GameState.SecretCount, Is.EqualTo(1));
        Assert.That(_map.MapSpot(10, 10, 1), Is.Zero);
    }

    [Test]
    public void Something_Inside_It_Without_A_Death_State_Goes()
    {
        // Arrange: a trap shooter in the wall
        WallBetweenAreas();
        var shooter = new Entities.Actors.Monster { Name = "Shooter", TileX = 10, TileY = 10 };
        _map.GetActors().AddLast(shooter);

        // Act
        _walls.Remove(10, 10);

        // Assert
        Assert.That(shooter.IsRemoved, Is.True);
    }

    [TestCase(0, 10, "edge")]
    [TestCase(10, 10, "door")]
    [TestCase(20, 20, "isn't a wall")]
    [TestCase(30, 30, "moving pushwall")]
    public void Doors_Open_Floor_Moving_Pushwalls_And_The_Edge_Stay(int x, int y, string why)
    {
        // Arrange
        Wall(0, 10);
        _map.TileMap[10, 10] = (ushort)GameConstants.BitDoor;
        _map.TileMap[30, 30] = (ushort)GameConstants.BitWall;
        var before = _map.TileMap[x, y];

        // Act
        bool removed = _walls.Remove(x, y);

        // Assert
        Assert.That(removed, Is.False);
        Assert.That(_walls.WhyNot(x, y), Does.Contain(why));
        Assert.That(_map.TileMap[x, y], Is.EqualTo(before));
    }

    [Test]
    public void A_RemoveWall_Takes_The_Tagged_Walls_But_Not_The_Switch()
    {
        // Arrange: a switch at 20,10 and walls at 10,10 and 30,10, all tagged
        _walls.Register();
        WallBetweenAreas();
        Wall(20, 10);
        Wall(30, 10);
        foreach (var x in new[] { 10, 20, 30 })
            _map.SetTag(x, 10, Tag);
        var activation = new Entities.TriggerActivation(20, 10, ControlDir.North, new Entities.Actors.PlayerPawn(), Tag);

        // Act
        bool any = Entities.MapTriggerRegistry.Invoke("A_RemoveWall", [], activation);

        // Assert
        Assert.That(any, Is.True);
        Assert.That((_map.TileMap[10, 10], _map.TileMap[20, 10], _map.TileMap[30, 10]), Is.EqualTo((0, WallId, 0)));
    }

    [Test]
    public void Game_Removewall_Asks_For_The_Tile_And_Whether_To_Leave_An_Arch()
    {
        // Arrange
        var asked = new List<(int, int, bool)>();
        var console = new ConsoleManager();
        var scripts = new GameScripts(null!, _map, null!, null!, console, null, null!, _session, (_, _) => { })
        {
            RemoveWall = (x, y, arch) => { asked.Add((x, y, arch)); return true; },
        };
        scripts.LuaCommand(["x = 1"]);

        // Act
        scripts.Scripts!.Execute("print(game.removewall(10, 10), game.removewall(11, 12, 'arch'))");
        var error = scripts.Scripts!.Execute("game.removewall(-1, 3)");

        // Assert
        Assert.That(asked, Is.EqualTo(new[] { (10, 10, false), (11, 12, true) }));
        Assert.That(console.Scrollback, Has.Some.EqualTo("true\ttrue"));
        Assert.That(error, Does.Contain("isn't a tile on the map"));
    }
}
