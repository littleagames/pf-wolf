using System.Reflection;
using PFWolf.Constants;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>Where a shot leaves its puff (LineOfSight.TraceShot, Gameplay/PlayerAttacks.cs HIT PUFFS), and the grenade's floor check</summary>
public class HitPuffTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    private MapManager _map = null!;
    private LineOfSight _sight = null!;

    [SetUp]
    public void MakeLevel()
    {
        var session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => new AssetManager()), null!, session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            MapWidth = MapConstants.ClassicMapSize, MapHeight = MapConstants.ClassicMapSize,
        };
        typeof(MapManager).GetField("mapsegs", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        _sight = new LineOfSight(_map, null!, session, (_, _, _, _) => false);
    }

    // The middle of a tile, in global coordinates
    private static int Mid(int tile) => tile * Tile + Tile / 2;

    [TestCase(0, 15, 10, 14, 10)]       // east, at a wall 5 tiles off: just short of it
    [TestCase(90, 10, 5, 10, 6)]        // north
    [TestCase(180, 4, 10, 5, 10)]       // west
    public void A_Shot_Puffs_Just_Short_Of_The_Wall_It_Meets(int angle, int wallx, int wally, int tilex, int tiley)
    {
        // Arrange
        _map.TileMap[wallx, wally] = 1;

        // Act
        var trace = _sight.TraceShot(Mid(10), Mid(10), angle);

        // Assert
        Assert.That(trace.End, Is.EqualTo(ShotEnd.Wall));
        Assert.That((trace.X >> MapConstants.TileShift, trace.Y >> MapConstants.TileShift), Is.EqualTo((tilex, tiley)));
    }

    [Test]
    public void A_Shot_That_Leaves_The_Map_Puffs_Nowhere()
    {
        Assert.That(_sight.TraceShot(Mid(10), Mid(10), 0).End, Is.EqualTo(ShotEnd.Edge));
    }

    [Test]
    public void A_Pellet_Out_Of_Range_Stops_Short_Of_The_Wall()
    {
        // Arrange: a wall 9 tiles east, the shotgun's 6 tiles of range
        _map.TileMap[19, 10] = 1;

        // Act
        var trace = _sight.TraceShot(Mid(10), Mid(10), 0, 6 * Tile);

        // Assert
        Assert.That(trace.End, Is.EqualTo(ShotEnd.Reach));
        Assert.That(trace.X >> MapConstants.TileShift, Is.EqualTo(16));
    }

    [Test]
    public void Something_In_The_Way_Stops_A_Shot_Where_It_Is()
    {
        // Arrange: a wall 9 tiles east, something 3 tiles east
        _map.TileMap[19, 10] = 1;

        // Act
        var trace = _sight.TraceShot(Mid(10), Mid(10), 0, stopAt: (x, _) => x >> MapConstants.TileShift == 13);

        // Assert
        Assert.That((trace.End, trace.X >> MapConstants.TileShift), Is.EqualTo((ShotEnd.Stopped, 13)));
    }

    [Test]
    public void A_Diagonal_Shot_Meets_The_Wall_On_Its_Line()
    {
        // Arrange: a wall 4 tiles north-east
        _map.TileMap[14, 6] = 1;

        // Act
        var trace = _sight.TraceShot(Mid(10), Mid(10), 45);

        // Assert
        Assert.That(trace.End, Is.EqualTo(ShotEnd.Wall));
        Assert.That((trace.X >> MapConstants.TileShift, trace.Y >> MapConstants.TileShift), Is.EqualTo((13, 7)).Or.EqualTo((14, 7)).Or.EqualTo((13, 6)));
    }

    [TestCase(0.0, null, true)]
    [TestCase(12.0, null, false)]       // still in the air
    [TestCase(4.0, "4", true)]          // its own zfloor
    public void A_Grenade_Is_On_The_Floor_At_Its_Zfloor(double z, string? zfloor, bool onFloor)
    {
        // Arrange
        var grenade = new Entities.Actors.Actor { Name = "ThrownGrenade", Properties = zfloor == null ? [] : new() { ["zfloor"] = zfloor } };
        typeof(Entities.Actors.Actor).GetProperty("Z")!.SetValue(grenade, z);

        // Act / Assert
        Assert.That(ProjectileActions.IsOnFloor(grenade), Is.EqualTo(onFloor));
    }

    [Test]
    public void A_Wall_Can_Refuse_Puffs()
    {
        // Act
        var walls = YamlDataEntryLoader.Deserialize<Dictionary<string, Assets.MapTextureTranslation>>("""
            '1': { North: FOLIAGE, Puffs: false }
            '2': { North: BRICK }
            """);

        // Assert
        Assert.That((walls["1"].Puffs, walls["2"].Puffs), Is.EqualTo(((bool?)false, (bool?)null)));
    }
}
