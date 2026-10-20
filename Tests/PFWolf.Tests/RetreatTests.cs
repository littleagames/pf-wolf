using System.Reflection;
using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class RetreatTests
{
    // A Commander that runs off to a marker between phases, and the markers
    private const string Yaml = """
        Monster: {}
        Commander:
          parent: Monster
          states:
            Spawn:
              - sprite: CMFL
                frames: [A]
                tics-per-frame: -1
            Retreat:
              - sprite: CMDM
                frames: [A]
                tics-per-frame: 10
                next-state: Retreat
            Retreated:
              - sprite: CMDM
                frames: [A]
                tics-per-frame: -1
        RetreatPoint:
          states:
            Spawn:
              - sprite: TNT1
                frames: [A]
                tics-per-frame: -1
        """;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private InventoryManager _actors = null!;
    private Monster _commander = null!;

    [SetUp]
    public void MakeLevel()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => new AssetManager()), null!, _session, null)
        {
            MapWidth = MapConstants.ClassicMapSize, MapHeight = MapConstants.ClassicMapSize,
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        // empty planes, as a loaded level has (TryWalk looks under where it goes)
        typeof(MapManager).GetField("mapsegs", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        var world = new ActorWorld(null!, null!, null!, null!, null!, _map, null!, null!, null!, null!, null!, null!,
            null!, null!, null!, null!, null!, null!, null!, null, null, _session);
        _actors = new InventoryManager(ActorDefs.Metadata(Yaml));

        // The Commander at 10,10, beaten and about to run
        _commander = (Monster)_actors.CreateActor("Commander")!;
        _commander.World = world;
        _commander.SetPosition(10, 10);
        _commander.RuntimeFlags |= ObjFlags.Shootable;
        _commander.SetState("Retreat");
        _map.GetActors().AddLast(_commander);
    }

    private Entities.Actors.Actor Point(int x, int y, ushort tag)
    {
        var point = _actors.CreateActor("RetreatPoint")!;
        point.SetPosition(x, y);
        point.Tag = tag;
        _map.GetActors().AddLast(point);
        return point;
    }

    [Test]
    public void A_RetreatTo_Heads_For_The_Marker_With_Its_Tag_Out_Of_Reach()
    {
        // Arrange: the nearer marker is the wrong one
        Point(12, 10, 2);
        Point(30, 10, 1);

        // Act
        _commander.RetreatTo(["RetreatPoint", "1", "10240"]);

        // Assert
        Assert.That((_commander.RetreatX, _commander.RetreatY), Is.EqualTo((30, 10)));
        Assert.That(_commander.Speed, Is.EqualTo(10240));
        Assert.That(_commander.RuntimeFlags.Has(ObjFlags.Shootable), Is.False);
    }

    [Test]
    public void A_RetreatTo_With_No_Such_Marker_Has_No_Goal()
    {
        // Act
        _commander.RetreatTo(["RetreatPoint", "3"]);

        // Assert
        Assert.That((_commander.RetreatX, _commander.RetreatY), Is.EqualTo((-1, -1)));
    }

    [Test]
    public void It_Sets_Off_The_Open_Way_Round_A_Wall()
    {
        // Arrange: walled in but for the south; the marker due north, past the wall
        for (int x = 9; x <= 11; x++)
            _map.TileMap[x, 9] = 1;
        _map.TileMap[9, 10] = _map.TileMap[11, 10] = 1;
        Point(10, 2, 1);
        _commander.RetreatTo(["RetreatPoint", "1"]);

        // Act
        _commander.Retreat();

        // Assert
        Assert.That(_commander.Dir, Is.EqualTo(ObjDir.South));
        Assert.That(_commander.CurrentState!.StateName, Is.EqualTo("Retreat"));
    }

    [Test]
    public void Near_The_Marker_It_Is_Put_On_It_As_Retreated()
    {
        // Arrange
        Point(12, 12, 1);
        _commander.RetreatTo(["RetreatPoint", "1"]);

        // Act
        _commander.Retreat();

        // Assert
        Assert.That((_commander.TileX, _commander.TileY), Is.EqualTo(((byte)12, (byte)12)));
        Assert.That(_commander.CurrentState!.StateName, Is.EqualTo("Retreated"));
        Assert.That(_commander.RetreatX, Is.EqualTo(-1));
    }

    [Test]
    public void With_No_Way_There_It_Is_Put_There_At_Once()
    {
        // Arrange: boxed in
        for (int x = 9; x <= 11; x++)
            _map.TileMap[x, 9] = _map.TileMap[x, 11] = 1;
        _map.TileMap[9, 10] = _map.TileMap[11, 10] = 1;
        Point(30, 30, 1);
        _commander.RetreatTo(["RetreatPoint", "1"]);

        // Act
        _commander.Retreat();

        // Assert
        Assert.That((_commander.TileX, _commander.TileY), Is.EqualTo(((byte)30, (byte)30)));
        Assert.That(_commander.CurrentState!.StateName, Is.EqualTo("Retreated"));
    }
}
