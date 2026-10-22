using System.Reflection;
using PFWolf.Constants;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>Where a player's shot leaves its puff (Gameplay/PlayerAttacks.cs HIT PUFFS), and the grenade's floor check</summary>
public class HitPuffTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    private MapManager _map = null!;
    private PlayerAttacks _attacks = null!;

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
        _attacks = new PlayerAttacks(_map, null!, null!, null!, session, null!);
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
        var at = _attacks.ShotWallPoint(Mid(10), Mid(10), angle);

        // Assert
        Assert.That(at, Is.Not.Null);
        Assert.That((at!.Value.X >> MapConstants.TileShift, at.Value.Y >> MapConstants.TileShift), Is.EqualTo((tilex, tiley)));
    }

    [Test]
    public void A_Shot_That_Leaves_The_Map_Puffs_Nowhere()
    {
        Assert.That(_attacks.ShotWallPoint(Mid(10), Mid(10), 0), Is.Null);
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
