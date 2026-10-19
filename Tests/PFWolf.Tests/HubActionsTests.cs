using PFWolf.Constants;

namespace PFWolf.Tests;

public class HubActionsTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private HubActions _actions = null!;

    [SetUp]
    public void Make()
    {
        _session = new GameSession { Tics = 10 };
        _map = new MapManager(new Lazy<AssetManager>(() => new AssetManager()), null!, _session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        _actions = new HubActions(null!, _map, null!, null!, null!, null!, null!, null!, null!, null!, null!, _session,
            new HubActionsHooks(_ => { }, (_, _) => { }));
    }

    // Blake, running east from the middle of tile (10, 10)
    private Entities.Actors.Monster Runner(int tilex = 10) => new()
    {
        Name = "Blake",
        X = tilex * Tile + Tile / 2, Y = 10 * Tile + Tile / 2,
        TileX = (byte)tilex, TileY = 10,
        Temp1 = 10, Temp3 = 10, Temp2 = 0,
    };

    [Test]
    public void The_Runner_Runs_The_Way_The_Player_Faced()
    {
        // Arrange
        var blake = Runner();
        int x = blake.X;

        // Act: ten tics at 3000 a tic
        _actions.ThinkVictoryRun(blake);

        // Assert
        Assert.That(blake.X, Is.EqualTo(x + 30000));
        Assert.That(_session.PlayState, Is.EqualTo(PlayState.StillPlaying));
    }

    [Test]
    public void Six_Tiles_On_The_Cluster_Is_Won()
    {
        var blake = Runner(tilex: 15);
        blake.X += Tile / 2 - 1;        // about to cross into tile 16

        _actions.ThinkVictoryRun(blake);

        Assert.That(blake.TileX, Is.EqualTo(16));
        Assert.That(_session.PlayState, Is.EqualTo(PlayState.Victorious));
    }

    [Test]
    public void A_Wall_In_The_Way_Ends_The_Run_There()
    {
        // Arrange: a wall in the next tile east
        var blake = Runner();
        blake.X += Tile / 2 - 1;
        _map.TileMap[11, 10] = 1;
        int x = blake.X;

        // Act
        _actions.ThinkVictoryRun(blake);

        // Assert: it stays put, and the cluster is won
        Assert.That(blake.X, Is.EqualTo(x));
        Assert.That(_session.PlayState, Is.EqualTo(PlayState.Victorious));
    }

    [Test]
    public void Starting_Inside_A_Wall_It_Runs_On_Out()
    {
        // Behind the player can be inside a wall: only one it runs into stops it
        _map.TileMap[10, 10] = 1;
        _map.TileMap[11, 10] = 1;
        var blake = Runner();
        int x = blake.X;

        _actions.ThinkVictoryRun(blake);

        Assert.That(blake.X, Is.GreaterThan(x));
        Assert.That(_session.PlayState, Is.EqualTo(PlayState.StillPlaying));
    }
}
