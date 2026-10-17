using PFWolf.Assets;
using PFWolf.Constants;

namespace PFWolf.Tests;

public class PlayerBodiesTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    private const string Yaml = """
        Player:
          states:
            Spawn:
              - sprite: PLMP
                frames: [A]
                tics-per-frame: -1
            See:
              - sprite: PLMP
                frames: [A, B]
                tics-per-frame: 8
            Death:
              - sprite: PLMP
                frames: [H]
                tics-per-frame: -1
        Ghost: {}
        Guard:
          properties:
            tag: Guard
          states:
            Spawn:
              - sprite: GARD
                frames: [A]
                tics-per-frame: -1
            Chase:
              - sprite: GARD
                frames: [B]
                tics-per-frame: -1
        """;

    private MapManager _map = null!;
    private InventoryManager _inventory = null!;
    private GameSession _session = null!;
    private PlayerBodies _bodies = null!;

    [SetUp]
    public void Make()
    {
        _session = new GameSession { tics = 1 };
        _map = new MapManager(new Lazy<AssetManager>(() => new AssetManager()), null!, _session)
        {
            tilemap = new ushort[MapManager.MapSize, MapManager.MapSize],
            actorat = new Actor?[MapManager.MapSize, MapManager.MapSize],
        };
        _inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
        _session.playerstate.playerclass = "Player";
        _session.playerstate.health = 100;
        _session.playerstate.Pawn = new Entities.Actors.PlayerPawn { State = _session.playerstate, X = 5 * Tile, Y = 5 * Tile };
        _bodies = new PlayerBodies(new AssetManager(), _map, _inventory, null!, _session);
    }

    private string? BodyState => _session.playerstate.Body?.CurrentState?.StateName;

    [Test]
    public void A_Still_Player_Stands_And_A_Moving_One_Walks()
    {
        // Act / Assert
        _bodies.TickPlayerBodies(1);
        _bodies.TickPlayerBodies(1);
        Assert.That(BodyState, Is.EqualTo("Spawn"));

        _session.playerstate.Pawn!.X += Tile / 4;
        _bodies.TickPlayerBodies(1);
        Assert.That(BodyState, Is.EqualTo("See"));
        Assert.That(_session.playerstate.Body!.X, Is.EqualTo(_session.playerstate.Pawn.X));
    }

    [Test]
    public void A_Dead_Player_Lies_Dead()
    {
        _session.playerstate.health = 0;

        _bodies.TickPlayerBodies(1);

        Assert.That(BodyState, Is.EqualTo("Death"));
    }

    [Test]
    public void A_Class_With_No_Spawn_State_Has_No_Body()
    {
        _session.playerstate.playerclass = "Ghost";

        _bodies.TickPlayerBodies(1);

        Assert.That(_session.playerstate.Body, Is.Null);
    }

    [Test]
    public void Only_The_Players_Bodies_Are_Player_Bodies()
    {
        _bodies.TickPlayerBodies(1);

        Assert.That(_bodies.IsPlayerBody(_session.playerstate.Body!), Is.True);
        Assert.That(_bodies.IsPlayerBody(new Entities.Actors.Monster { Name = "Guard" }), Is.False);
    }

    [Test]
    public void The_Camera_Doesnt_See_The_Body_It_Looks_Through()
    {
        // Arrange
        _bodies.TickPlayerBodies(1);
        var pawn = _session.playerstate.Pawn!;

        // Act / Assert: following the player, then fixed elsewhere
        _session.camera.Follow(pawn);
        Assert.That(_bodies.VisiblePlayerBody(pawn), Is.Null);
        _session.camera.SetFixed(0, 0, 0);
        Assert.That(_bodies.VisiblePlayerBody(pawn), Is.SameAs(_session.playerstate.Body));
    }

    [Test]
    public void Spectate_Steps_Through_The_Living_Enemies_And_Back_To_The_Player()
    {
        // Arrange: two guards that can chase, one dead
        var console = new ConsoleManager();
        var messages = new HudMessageManager(new Lazy<AssetManager>(() => new AssetManager()), new Lazy<ConsoleManager>(console));
        var drawing = new HudMessageDrawing(null!, null!, _inventory, messages, null!, null!, null!,
            new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.NoShade), _session);
        var first = _inventory.CreateActor("Guard")!;
        var second = _inventory.CreateActor("Guard")!;
        var dead = _inventory.CreateActor("Guard")!;
        (first.Hitpoints, second.Hitpoints, dead.Hitpoints) = (25, 25, 0);
        foreach (var guard in new[] { first, dead, second })
            _map.GetActors().AddLast(guard);
        var spectating = new Spectating(_map, console, messages, drawing, _session);

        // Act / Assert
        Assert.That(spectating.SpectateCandidates(), Is.EqualTo(new[] { first, second }));
        spectating.SpectateCommand([]);
        Assert.That(_session.camera.Target, Is.SameAs(first));
        spectating.SpectateCommand(["next"]);
        Assert.That(_session.camera.Target, Is.SameAs(second));
        spectating.SpectateCommand(["next"]);
        Assert.That(_session.camera.Target, Is.SameAs(first));      // round again
        spectating.SpectateCommand(["player"]);
        Assert.That(console.Scrollback[^1], Is.EqualTo("Watching through your own eyes"));
    }
}
