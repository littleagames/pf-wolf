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
        GuardPlayer:
          parent: Player
        EliteGuardPlayer:
          parent: GuardPlayer
        Jeep:
          properties:
            vehicle.ridersprites:
              Player: JPBJ
              GuardPlayer: JPGD
          states:
            Spawn:
              - sprite: JEEP
                frames: [A]
                tics-per-frame: -1
            Ride:
              - sprite: JEEP
                frames: [B]
                tics-per-frame: -1
            RideSee:
              - sprite: JEEP
                frames: [C, D]
                tics-per-frame: 4
        Boat:
          states:
            Spawn:
              - sprite: BOAT
                frames: [A]
                tics-per-frame: -1
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
    private RenderState _render = null!;

    [SetUp]
    public void Make()
    {
        _session = new GameSession { Tics = 1 };
        _map = new MapManager(new Lazy<AssetManager>(() => new AssetManager()), null!, _session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        _inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
        _session.PlayerState.PlayerClass = "Player";
        _session.PlayerState.Health = 100;
        _session.PlayerState.Pawn = new Entities.Actors.PlayerPawn { State = _session.PlayerState, X = 5 * Tile, Y = 5 * Tile };
        // The viewer looks west, the body in the middle of the view
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.NoShade)
        {
            ViewAngle = 180, CenterX = 160, ProjectionWidth = 320, Scale = 160, HeightNumerator = 0x100000,
        };
        var drawing = new SpriteDrawing(null!, null!, null!, null!, null!, null!, null!, null!, null!, _render, null!, _session,
            new SpriteDrawingHooks(() => null, ob => _bodies.IsPlayerBody(ob), _ => null, ob => ob.CurrentState!.Sprite,
                name => _bodies.SpriteExists(name), () => null, () => 1));
        _bodies = new PlayerBodies(new AssetManager(), _map, _inventory, drawing, _session);
    }

    // Sprite pictures there are (any other isn't)
    private void Pictures(params string[] names)
    {
        foreach (var name in names)
            _bodies.SpriteExistsCache[name] = true;
    }

    // Riding the jeep as a class, facing a way (0 east, toward the viewer; 180 west, away), still
    private Entities.Actors.Actor RideJeepAs(string playerClass, short angle = 0)
    {
        _session.PlayerState.PlayerClass = playerClass;
        _session.PlayerState.Vehicle = "Jeep";
        _session.PlayerState.Pawn!.Angle = angle;
        _bodies.TickPlayerBodies(1);
        _bodies.TickPlayerBodies(1);        // the first counts as having moved there
        var body = _session.PlayerState.Body!;
        body.ViewX = (short)_render.CenterX;
        return body;
    }

    private string? BodyState => _session.PlayerState.Body?.CurrentState?.StateName;

    [Test]
    public void A_Still_Player_Stands_And_A_Moving_One_Walks()
    {
        // Act / Assert
        _bodies.TickPlayerBodies(1);
        _bodies.TickPlayerBodies(1);
        Assert.That(BodyState, Is.EqualTo("Spawn"));

        _session.PlayerState.Pawn!.X += Tile / 4;
        _bodies.TickPlayerBodies(1);
        Assert.That(BodyState, Is.EqualTo("See"));
        Assert.That(_session.PlayerState.Body!.X, Is.EqualTo(_session.PlayerState.Pawn.X));
    }

    [Test]
    public void A_Dead_Player_Lies_Dead()
    {
        _session.PlayerState.Health = 0;

        _bodies.TickPlayerBodies(1);

        Assert.That(BodyState, Is.EqualTo("Death"));
    }

    [Test]
    public void A_Rider_Is_Seen_As_Their_Vehicle_Through_Its_Ride_States()
    {
        // Arrange
        _session.PlayerState.Vehicle = "Jeep";

        // Act / Assert: still, then moving
        _bodies.TickPlayerBodies(1);
        _bodies.TickPlayerBodies(1);
        Assert.That(_session.PlayerState.Body!.Name, Is.EqualTo("Jeep"));
        Assert.That(BodyState, Is.EqualTo("Ride"));
        _session.PlayerState.Pawn!.X += Tile / 4;
        _bodies.TickPlayerBodies(1);
        Assert.That(BodyState, Is.EqualTo("RideSee"));
    }

    [Test]
    public void A_Vehicle_With_No_Ride_States_Is_Seen_As_It_Stands()
    {
        _session.PlayerState.Vehicle = "Boat";
        _session.PlayerState.Pawn!.X += Tile / 4;

        _bodies.TickPlayerBodies(1);

        Assert.That((_session.PlayerState.Body!.Name, BodyState), Is.EqualTo(("Boat", "Spawn")));
    }

    [Test]
    public void A_Rider_Is_Seen_With_Their_Class_At_The_Wheel()
    {
        // Arrange
        Pictures("JPGDB0", "JPBJB0");

        // Act / Assert
        Assert.That(_bodies.DrawnSprite(RideJeepAs("GuardPlayer")), Is.EqualTo("JPGD"));
        Assert.That(_bodies.DrawnSprite(RideJeepAs("Player")), Is.EqualTo("JPBJ"));
    }

    [Test]
    public void A_Class_Not_Listed_Rides_As_Its_Parent_Else_As_The_Vehicle()
    {
        Pictures("JPGDB0");

        Assert.That(_bodies.DrawnSprite(RideJeepAs("EliteGuardPlayer")), Is.EqualTo("JPGD"));
        Assert.That(_bodies.DrawnSprite(RideJeepAs("Ghost")), Is.EqualTo("JEEP"));
    }

    [Test]
    public void A_Frame_The_Riders_Sprite_Lacks_Shows_The_Vehicle()
    {
        // Arrange: only the still frame (B) made for the Guard; driving shows C and D
        Pictures("JPGDB0");
        RideJeepAs("GuardPlayer");
        _session.PlayerState.Pawn!.X += Tile / 4;

        // Act
        _bodies.TickPlayerBodies(1);

        // Assert
        Assert.That((BodyState, _bodies.DrawnSprite(_session.PlayerState.Body!)), Is.EqualTo(("RideSee", "JEEP")));
    }

    [Test]
    public void A_Single_Rider_Picture_On_A_Vehicle_Seen_From_8_Sides_Is_Only_Its_Front()
    {
        // Arrange: the jeep drawn from every side, the Guard's head only from the front
        Pictures("JEEPB1", "JEEPB5", "JPGDB0");

        // Act / Assert: coming toward the viewer, then going away
        Assert.That(_bodies.DrawnSprite(RideJeepAs("GuardPlayer", 0)), Is.EqualTo("JPGD"));
        Assert.That(_bodies.DrawnSprite(RideJeepAs("GuardPlayer", 180)), Is.EqualTo("JEEP"));
    }

    [Test]
    public void A_Rider_Sprite_With_Views_Shows_The_One_For_Its_Angle()
    {
        Pictures("JEEPB1", "JEEPB5", "JPGDB1", "JPGDB5");

        Assert.That(_bodies.DrawnSprite(RideJeepAs("GuardPlayer", 180)), Is.EqualTo("JPGD"));
    }

    [Test]
    public void A_Rider_Drawn_For_Some_Views_Isnt_Its_Front_Seen_From_The_Others()
    {
        // Arrange: the jeep from the front and back, the Guard's front view and single picture
        Pictures("JEEPB1", "JEEPB5", "JPGDB0", "JPGDB1");

        // Act / Assert: from behind, the jeep's own back, not the Guard's front
        Assert.That(_bodies.DrawnSprite(RideJeepAs("GuardPlayer", 0)), Is.EqualTo("JPGD"));
        Assert.That(_bodies.DrawnSprite(RideJeepAs("GuardPlayer", 180)), Is.EqualTo("JEEP"));
    }

    [Test]
    public void A_Single_Rider_Picture_Shows_Where_The_Vehicle_Shows_Its_Own_Single_Picture()
    {
        // Arrange: the jeep's front is its single picture, with a side view (B3) only
        Pictures("JEEPB0", "JEEPB3", "JPGDB0");

        // Act / Assert: from behind it falls back to its front, and so does the Guard
        Assert.That(_bodies.DrawnSprite(RideJeepAs("GuardPlayer", 180)), Is.EqualTo("JPGD"));
    }

    [Test]
    public void A_Parked_Vehicle_Shows_No_Rider()
    {
        // Arrange: the Guard has a picture for the parked frame too
        Pictures("JPGDA0");
        _session.PlayerState.PlayerClass = "GuardPlayer";
        var parked = _inventory.CreateActor("Jeep")!;

        // Act / Assert
        Assert.That(_bodies.DrawnSprite(parked), Is.EqualTo("JEEP"));
    }

    [Test]
    public void A_Rider_Killed_Lies_Dead_As_Themselves()
    {
        // Arrange: riding, then killed (the ride's over on their next think)
        _session.PlayerState.Vehicle = "Jeep";
        _bodies.TickPlayerBodies(1);
        _session.PlayerState.Health = 0;

        // Act
        _bodies.TickPlayerBodies(1);

        // Assert
        Assert.That((_session.PlayerState.Body!.Name, BodyState), Is.EqualTo(("Player", "Death")));
    }

    [Test]
    public void A_Class_With_No_Spawn_State_Has_No_Body()
    {
        _session.PlayerState.PlayerClass = "Ghost";

        _bodies.TickPlayerBodies(1);

        Assert.That(_session.PlayerState.Body, Is.Null);
    }

    [Test]
    public void Only_The_Players_Bodies_Are_Player_Bodies()
    {
        _bodies.TickPlayerBodies(1);

        Assert.That(_bodies.IsPlayerBody(_session.PlayerState.Body!), Is.True);
        Assert.That(_bodies.IsPlayerBody(new Entities.Actors.Monster { Name = "Guard" }), Is.False);
    }

    [Test]
    public void The_Camera_Doesnt_See_The_Body_It_Looks_Through()
    {
        // Arrange
        _bodies.TickPlayerBodies(1);
        var pawn = _session.PlayerState.Pawn!;

        // Act / Assert: following the player, then fixed elsewhere
        _session.Camera.Follow(pawn);
        Assert.That(_bodies.VisiblePlayerBody(pawn), Is.Null);
        _session.Camera.SetFixed(0, 0, 0);
        Assert.That(_bodies.VisiblePlayerBody(pawn), Is.SameAs(_session.PlayerState.Body));
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
        Assert.That(_session.Camera.Target, Is.SameAs(first));
        spectating.SpectateCommand(["next"]);
        Assert.That(_session.Camera.Target, Is.SameAs(second));
        spectating.SpectateCommand(["next"]);
        Assert.That(_session.Camera.Target, Is.SameAs(first));      // round again
        spectating.SpectateCommand(["player"]);
        Assert.That(console.Scrollback[^1], Is.EqualTo("Watching through your own eyes"));
    }
}
