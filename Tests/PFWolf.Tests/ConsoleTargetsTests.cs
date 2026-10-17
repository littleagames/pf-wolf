using PFWolf.Constants;

namespace PFWolf.Tests;

public class ConsoleTargetsTests
{
    private const int Tile = 1 << MapConstants.TILESHIFT;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private ConsoleTargets _targets = null!;

    [SetUp]
    public void Make()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => new AssetManager()), null!, _session)
        {
            tilemap = new ushort[MapManager.MAPSIZE, MapManager.MAPSIZE],
            actorat = new Actor?[MapManager.MAPSIZE, MapManager.MAPSIZE],
            mapwidth = MapManager.MAPSIZE, mapheight = MapManager.MAPSIZE,
        };
        _session.playerstate.Pawn = new Entities.Actors.PlayerPawn
        {
            State = _session.playerstate, X = 10 * Tile + Tile / 2, Y = 10 * Tile + Tile / 2, TileX = 10, TileY = 10,
        };
        _targets = new ConsoleTargets(_map, new Lighting(null!, null!, _map, null!), _session);
    }

    [TestCase(0, 11, 10)]       // facing east: the tile to the east
    [TestCase(90, 10, 9)]       // north is up the map
    [TestCase(180, 9, 10)]
    [TestCase(270, 10, 11)]
    public void Without_A_Tile_It_Means_The_One_Faced(int angle, int x, int y)
    {
        _session.player.Angle = (short)angle;
        Assert.That(_targets.TileArg([], 0), Is.EqualTo((x, y)));
    }

    [Test]
    public void A_Tile_Given_Is_That_One()
    {
        Assert.That(_targets.TileArg(["give", "3", "40"], 1), Is.EqualTo((3, 40)));
        Assert.Throws<ArgumentException>(() => _targets.TileArg(["3", "99"], 0));     // off the map
    }

    [Test]
    public void With_No_Class_The_Nearest_Living_Enemy_Is_Chosen()
    {
        // Arrange: a far guard, a near one, and a nearer one that's dead
        Entities.Actors.Monster Guard(int tiles, bool alive) => new()
        {
            Name = "Guard", X = _session.player.X + tiles * Tile, Y = _session.player.Y,
            RuntimeFlags = alive ? ObjFlags.FL_SHOOTABLE : 0,
        };
        var far = Guard(8, true);
        var near = Guard(3, true);
        foreach (var guard in new[] { far, near, Guard(1, false) })
            _map.GetActors().AddLast(guard);

        // Act / Assert
        Assert.That(_targets.ChosenActors(null), Is.EqualTo(new[] { near }));
        Assert.That(_targets.ChosenActors("guard"), Is.EquivalentTo(new[] { far, near }));
        Assert.Throws<ArgumentException>(() => _targets.ChosenActors("Dog"));
    }

    [Test]
    public void No_Patch_And_No_Zone_Read_As_Such()
    {
        Assert.That(_targets.DescribePatch(0), Is.EqualTo("no patch"));
        Assert.That(_targets.DescribeZone(0), Is.EqualTo("the level's light, 255"));
        Assert.That(_targets.DescribeZone(7), Is.EqualTo("not in the map's zones: the level's light"));
    }
}
