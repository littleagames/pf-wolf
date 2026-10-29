using PFWolf.Constants;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

/// <summary>Something the player rides (Gameplay/Vehicles.cs: Midnight Assault's boat)</summary>
public class VehiclesTests
{
    // A boat with a top speed of 100, picking up 10 a tic and slowing 25 a tic
    private const string Yaml = """
        Boat:
          properties:
            "vehicle.maxspeed": 100
            "vehicle.accel": 10
            "vehicle.brake": 25
            "vehicle.exitmessage": "Out of the boat"
            "vehicle.exitsound": "boat/out"
            "vehicle.noexit": "Nowhere to get out"
            "vehicle.exithint": "BOATOUT"
            "vehicle.dockmessage": "Use: step onto the dock"
          states:
            Spawn:
              - sprite: BOAT
                frames: [A]
                tics-per-frame: -1
        """;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private Vehicles _vehicles = null!;
    private PlayerPawn _pawn = null!;
    private Entities.Actors.Actor _boat = null!;
    private List<string> _sounds = null!, _messages = null!;
    private List<(int X, int Y, string Class)> _respawned = null!;

    [SetUp]
    public void MakeLevel()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        var inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
        inventory.SetHolder(_session.PlayerState.Items);

        // The player at 10,10 facing east, the boat a tile ahead
        _pawn = new PlayerPawn { State = _session.PlayerState, Angle = 0 };
        _pawn.SetPosition(10, 10);
        _session.PlayerState.Pawn = _pawn;
        _boat = inventory.CreateActor("Boat")!;
        _boat.SetPosition(11, 10);
        _map.GetActors().AddLast(_boat);

        (_sounds, _messages, _respawned) = ([], [], []);
        _vehicles = new Vehicles(_map, inventory, _session,
            new VehiclesHooks(_sounds.Add, _messages.Add, () => { }, (x, y, vehicle) => _respawned.Add((x, y, vehicle))));
    }

    [Test]
    public void A_Board_Puts_The_Player_On_Its_Tile_Riding_It()
    {
        // Arrange: facing a little off east
        _pawn.Angle = 15;

        // Act
        _vehicles.Board(_boat, ["boat/in", "In the boat"]);

        // Assert
        Assert.That(_vehicles.IsRiding, Is.True);
        Assert.That(_session.PlayerState.Vehicle, Is.EqualTo("Boat"));
        Assert.That((_pawn.TileX, _pawn.TileY, _pawn.Angle), Is.EqualTo(((byte)11, (byte)10, (short)0)));
        Assert.That(_sounds, Is.EqualTo(new[] { "boat/in" }));
        Assert.That(_messages, Is.EqualTo(new[] { "In the boat" }));
    }

    [Test]
    public void Forward_Picks_Up_Speed_To_The_Top_And_Back_Slows_It()
    {
        // Arrange
        _vehicles.Board(_boat, []);

        // Act / Assert: forward is a negative controlY, as the player's movement has it
        Assert.That(_vehicles.Throttle(-1, 3), Is.EqualTo(30 * 3));
        Assert.That(_session.PlayerState.VehicleThrottle, Is.EqualTo(30));
        _vehicles.Throttle(-1, 20);
        Assert.That(_session.PlayerState.VehicleSpeed, Is.EqualTo(100));
        Assert.That(_vehicles.Throttle(0, 1), Is.EqualTo(100));            // holds its speed with nothing pressed
        _vehicles.Throttle(1, 2);
        Assert.That(_session.PlayerState.VehicleSpeed, Is.EqualTo(50));
        Assert.That(_vehicles.Throttle(1, 10), Is.Zero);                   // never backwards
    }

    [Test]
    public void Getting_Out_Onto_The_Dock_Ahead_Leaves_The_Boat_Behind()
    {
        // Arrange: docks ahead and behind
        _vehicles.Board(_boat, []);
        _vehicles.Throttle(-1, 5);
        _map.SetDock(12, 10, true);
        _map.SetDock(10, 10, true);

        // Act
        Assert.That(_vehicles.Hint(), Is.EqualTo("BOATOUT"));
        Assert.That(_vehicles.DockMessage(), Is.EqualTo("Use: step onto the dock"));
        bool wasRiding = _vehicles.TryLeave();

        // Assert
        Assert.That(wasRiding, Is.True);
        Assert.That(_vehicles.IsRiding, Is.False);
        Assert.That(_session.PlayerState.VehicleSpeed, Is.Zero);
        Assert.That((_pawn.TileX, _pawn.TileY), Is.EqualTo(((byte)12, (byte)10)));
        Assert.That(_respawned, Is.EqualTo(new[] { (11, 10, "Boat") }));
        Assert.That(_sounds, Is.EqualTo(new[] { "boat/out" }));
        Assert.That(_messages, Is.EqualTo(new[] { "Out of the boat" }));
    }

    [Test]
    public void With_No_Dock_Beside_Them_They_Stay_In_And_Are_Told()
    {
        // Arrange
        _vehicles.Board(_boat, []);

        // Act
        bool wasRiding = _vehicles.TryLeave();

        // Assert
        Assert.That(wasRiding, Is.True);
        Assert.That(_vehicles.IsRiding, Is.True);
        Assert.That(_vehicles.Hint(), Is.Null);
        Assert.That(_vehicles.DockMessage(), Is.Null);
        Assert.That(_messages, Is.EqualTo(new[] { "Nowhere to get out" }));
        Assert.That(_respawned, Is.Empty);
    }

    [Test]
    public void Use_When_Not_Riding_Isnt_Getting_Out()
    {
        Assert.That(_vehicles.TryLeave(), Is.False);
    }

    [Test]
    public void Dying_Or_A_New_Level_Ends_The_Ride()
    {
        // Arrange
        _vehicles.Board(_boat, []);
        _session.GameState.MapOn = "NEXTMAP";

        // Act
        _vehicles.Tick();

        // Assert
        Assert.That(_vehicles.IsRiding, Is.False);
    }

    [Test]
    public void A_Dock_Marks_Only_Its_Tile()
    {
        _map.SetDock(3, 4, true);
        Assert.That(_map.IsDock(3, 4), Is.True);
        Assert.That(_map.IsDock(4, 4), Is.False);
        Assert.That(_map.IsDock(-1, 4), Is.False);
    }
}
