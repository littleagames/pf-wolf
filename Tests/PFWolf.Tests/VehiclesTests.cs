using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

/// <summary>Something the player rides (Gameplay/Vehicles.cs: Midnight Assault's boat)</summary>
public class VehiclesTests
{
    // A boat with a top speed of 100, picking up 10 a tic and slowing 25 a tic; a tank with a
    // reverse gear, drag, a cannon, getting out anywhere and running enemies down; a pistol to
    // start with, and a dog to run down
    private const string Yaml = """
        Inventory:
          properties:
            "inventory.amount": 1
            "inventory.maxamount": 1
        Weapon:
          parent: Inventory
        Pistol:
          parent: Weapon
          properties:
            "weapon.slot": 2
        Cannon:
          parent: Weapon
          properties:
            "weapon.slot": 3
            "weapon.mounted": true
        Monster: {}
        Dog:
          parent: Monster
          states:
            Spawn:
              - sprite: DOGG
                frames: [A]
                tics-per-frame: -1
        Tank:
          properties:
            "vehicle.maxspeed": 100
            "vehicle.accel": 10
            "vehicle.brake": 25
            "vehicle.reverse": 40
            "vehicle.drag": 5
            "vehicle.exitanywhere": true
            "vehicle.weapon": Cannon
            "vehicle.damagetaken": 0.25
            "vehicle.crushdamage": 30
            "vehicle.crushspeed": 50
            "vehicle.crushsound": "tank/crush"
          states:
            Spawn:
              - sprite: TANK
                frames: [A]
                tics-per-frame: -1
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
    private InventoryManager _inventory = null!;
    private Vehicles _vehicles = null!;
    private PlayerPawn _pawn = null!;
    private Entities.Actors.Actor _boat = null!;
    private List<string> _sounds = null!, _messages = null!;
    private List<(int X, int Y, string Class)> _respawned = null!;
    private List<(Monster Victim, int Damage)> _crushed = null!;

    [SetUp]
    public void MakeLevel()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        _inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
        _inventory.SetHolder(_session.PlayerState.Items);
        _inventory.Give("Pistol", 1);
        _session.PlayerState.Weapon = _session.PlayerState.ChosenWeapon = "Pistol";
        _session.PlayerState.Health = 100;

        // The player at 10,10 facing east, the boat a tile ahead
        _pawn = new PlayerPawn { State = _session.PlayerState, Angle = 0 };
        _pawn.SetPosition(10, 10);
        _session.PlayerState.Pawn = _pawn;
        _boat = _inventory.CreateActor("Boat")!;
        _boat.SetPosition(11, 10);
        _map.GetActors().AddLast(_boat);

        var bar = new StatusBarDrawing(null!, null, null!, null!, _inventory, null!, null!, null!, _session,
            new Configuration.GameSettings(), new StatusBarHooks(map => map, (text, _, _) => [text]));
        var weapons = new PlayerWeapons(_inventory, null!, bar, _session, new PlayerWeaponsHooks(_ => { }));
        (_sounds, _messages, _respawned, _crushed) = ([], [], [], []);
        _vehicles = new Vehicles(_map, _inventory, _session,
            new VehiclesHooks(_sounds.Add, _messages.Add, () => { }, (x, y, vehicle) => _respawned.Add((x, y, vehicle)),
                (victim, damage) => _crushed.Add((victim, damage))),
            weapons);
    }

    // The tank in the boat's place, boarded
    private Entities.Actors.Actor BoardTank()
    {
        _map.GetActors().Remove(_boat);
        var tank = _inventory.CreateActor("Tank")!;
        tank.SetPosition(11, 10);
        _map.GetActors().AddLast(tank);
        _vehicles.Board(tank, []);
        return tank;
    }

    [Test]
    public void Back_From_A_Stop_Goes_Into_Reverse_And_Forward_Brakes_Out_Of_It()
    {
        // Arrange
        BoardTank();

        // Act / Assert: back from a stop reverses, up to its reverse speed
        Assert.That(_vehicles.Throttle(1, 2), Is.EqualTo(-20 * 2));
        _vehicles.Throttle(1, 10);
        Assert.That(_session.PlayerState.VehicleSpeed, Is.EqualTo(-40));
        Assert.That(_session.PlayerState.VehicleThrottle, Is.EqualTo(100));    // all of its reverse
        Assert.That(_vehicles.Throttle(-1, 1), Is.EqualTo(-15));               // forward brakes...
        _vehicles.Throttle(-1, 1);
        Assert.That(_session.PlayerState.VehicleSpeed, Is.Zero);               // ...to a stop, not past it
        Assert.That(_vehicles.Throttle(-1, 1), Is.EqualTo(10));                // then forward
    }

    [Test]
    public void Drag_Slows_It_With_Nothing_Pressed()
    {
        // Arrange
        BoardTank();
        _vehicles.Throttle(-1, 5);

        // Act
        _vehicles.Throttle(0, 4);

        // Assert
        Assert.That(_session.PlayerState.VehicleSpeed, Is.EqualTo(50 - 4 * 5));
    }

    [Test]
    public void Running_Into_Something_Stops_It_Dead()
    {
        // Arrange
        BoardTank();
        _vehicles.Throttle(-1, 5);

        // Act
        _vehicles.Stalled();

        // Assert
        Assert.That((_session.PlayerState.VehicleSpeed, _session.PlayerState.VehicleThrottle), Is.EqualTo((0, 0)));
    }

    [Test]
    public void Exit_Anywhere_Gets_Out_At_The_Side_With_No_Dock()
    {
        // Arrange: a wall to the north, so the south side
        BoardTank();
        _map.TileMap[11, 9] = 1;

        // Act
        bool wasRiding = _vehicles.TryLeave();

        // Assert
        Assert.That(wasRiding, Is.True);
        Assert.That(_vehicles.IsRiding, Is.False);
        Assert.That((_pawn.TileX, _pawn.TileY), Is.EqualTo(((byte)11, (byte)11)));
        Assert.That(_respawned, Is.EqualTo(new[] { (11, 10, "Tank") }));
    }

    [Test]
    public void Exit_Anywhere_Never_Puts_Them_Where_Players_Are_Kept_Out()
    {
        // Arrange: walls all round but behind, where players are kept out
        BoardTank();
        (_map.TileMap[11, 9], _map.TileMap[11, 11], _map.TileMap[12, 10]) = (1, 1, 1);
        _map.SetMonsterBlock(10, 10, MapManager.MonsterBlock.Players);

        // Act
        _vehicles.TryLeave();

        // Assert
        Assert.That(_vehicles.IsRiding, Is.True);
        Assert.That(_vehicles.ExitBeside(), Is.Null);
    }

    [Test]
    public void Its_Weapon_Is_In_Hand_While_Riding_And_The_Old_One_Comes_Back()
    {
        // Act
        BoardTank();

        // Assert: the cannon in hand, kept there
        Assert.That(_session.PlayerState.Weapon, Is.EqualTo("Cannon"));
        Assert.That(_inventory.Has("Cannon"), Is.True);
        _session.PlayerState.Weapon = _session.PlayerState.ChosenWeapon = "Pistol";
        _vehicles.Tick();
        Assert.That(_session.PlayerState.Weapon, Is.EqualTo("Cannon"));

        // Getting out: the cannon goes, the pistol's back
        _vehicles.TryLeave();
        Assert.That(_session.PlayerState.Weapon, Is.EqualTo("Pistol"));
        Assert.That(_inventory.Has("Cannon"), Is.False);
        Assert.That(_session.PlayerState.VehicleGun, Is.Null);
    }

    [Test]
    public void Dying_In_It_Takes_Its_Weapon_Away()
    {
        // Arrange
        BoardTank();
        _session.PlayerState.Health = 0;

        // Act
        _vehicles.Tick();

        // Assert
        Assert.That(_vehicles.IsRiding, Is.False);
        Assert.That(_inventory.Has("Cannon"), Is.False);
    }

    [Test]
    public void At_Speed_It_Runs_Down_An_Enemy_Ahead_Once_In_A_While()
    {
        // Arrange: a dog a tile ahead, up to crushing speed
        BoardTank();
        var dog = (Monster)_inventory.CreateActor("Dog")!;
        dog.SetPosition(12, 10);
        dog.RuntimeFlags |= ObjFlags.Shootable;
        _map.GetActors().AddLast(dog);
        _vehicles.Throttle(-1, 4);
        Assert.That(_crushed, Is.Empty);        // 40: too slow

        // Act
        _vehicles.Throttle(-1, 1);
        _vehicles.Throttle(-1, 1);

        // Assert: hit once, and not again so soon
        Assert.That(_crushed, Is.EqualTo(new[] { (dog, 30) }));
        Assert.That(_sounds, Is.EqualTo(new[] { "tank/crush" }));
    }

    [Test]
    public void It_Doesnt_Run_Down_An_Enemy_Behind_It()
    {
        // Arrange: a dog a tile behind, going forward at speed
        BoardTank();
        var dog = (Monster)_inventory.CreateActor("Dog")!;
        dog.SetPosition(10, 10);
        dog.RuntimeFlags |= ObjFlags.Shootable;
        _map.GetActors().AddLast(dog);

        // Act
        _vehicles.Throttle(-1, 8);

        // Assert
        Assert.That(_crushed, Is.Empty);
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
