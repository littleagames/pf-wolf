using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class LatchingTests
{
    // A pistol to start with, the struggle a dog puts in the player's hands, and the dog
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
        DogStruggle:
          parent: Weapon
          properties:
            "weapon.latch": true
            "weapon.slowmove": 10
          states:
            Ready:
              - sprite: DGBT
                frames: [A]
                tics-per-frame: 1
                next-state: Ready
            Fire:
              - sprite: DGBT
                frames: [B]
                tics-per-frame: 6
        Monster: {}
        Dog:
          parent: Monster
          states:
            Spawn:
              - sprite: DOGG
                frames: [A]
                tics-per-frame: -1
            Chase:
              - sprite: DOGG
                frames: [C]
                tics-per-frame: 10
            Latched:
              - sprite: TNT1
                frames: [A]
                tics-per-frame: 70
            Death:
              - sprite: DOGG
                frames: [G]
                tics-per-frame: -1
        """;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private InventoryManager _inventory = null!;
    private Latching _latching = null!;
    private PlayerPawn _pawn = null!;
    private Monster _dog = null!;
    private List<(PlayerPawn Victim, int Points)> _playerHurts = null!;
    private int _strikeSounds;
    private bool _weaponReady;

    [SetUp]
    public void MakeLevel()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session, null)
        {
            TileMap = new ushort[MapManager.MapSize, MapManager.MapSize],
            ActorAt = new Actor?[MapManager.MapSize, MapManager.MapSize],
        };
        _inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
        _inventory.SetHolder(_session.PlayerState.Items);
        _inventory.Give("Pistol", 1);
        _session.PlayerState.Weapon = _session.PlayerState.ChosenWeapon = "Pistol";
        _session.PlayerState.Health = 100;

        // The player at 10,10, the dog biting from the tile east
        _pawn = new PlayerPawn { State = _session.PlayerState };
        _pawn.SetPosition(10, 10);
        _session.PlayerState.Pawn = _pawn;
        _dog = (Monster)_inventory.CreateActor("Dog")!;
        _dog.SetPosition(11, 10);
        _dog.Hitpoints = 1;
        _dog.RuntimeFlags |= ObjFlags.Shootable | ObjFlags.AttackMode;
        _dog.SetState("Chase");
        _map.GetActors().AddLast(_dog);

        var bar = new StatusBarDrawing(null!, null, null!, null!, _inventory, null!, null!, null!, _session,
            new Configuration.GameSettings(), new StatusBarHooks(map => map, (text, _, _) => [text]));
        var weapons = new PlayerWeapons(_inventory, null!, bar, _session, new PlayerWeaponsHooks(_ => { }));
        _playerHurts = [];
        _strikeSounds = 0;
        _weaponReady = false;
        _latching = new Latching(_map, _inventory, weapons, _session, new LatchingHooks(
            (victim, points, _) => _playerHurts.Add((victim, points)),
            () => _strikeSounds++,
            () => _weaponReady,
            (monster, damage, _) =>
            {
                monster.Hitpoints -= (short)damage;
                if (monster.Hitpoints <= 0)
                    monster.SetState("Death");
            }));
    }

    private Entities.Actors.Actor Struggle() => _inventory.CreateActor("DogStruggle")!;

    [Test]
    public void Latching_On_Puts_The_Struggle_In_Hand_And_The_Dog_Out_Of_Sight()
    {
        // Act
        _latching.LatchOn(_dog, "DogStruggle");

        // Assert
        var playerstate = _session.PlayerState;
        Assert.That(_latching.IsLatched, Is.True);
        Assert.That(playerstate.Weapon, Is.EqualTo("DogStruggle"));
        Assert.That(playerstate.LatchFrom, Is.EqualTo("Pistol"));
        Assert.That(playerstate.MoveSlowdown, Is.EqualTo(10));
        Assert.That(_session.PlayerInput.IsHeld(ButtonType.Attack), Is.True);
        Assert.That(_dog.RuntimeFlags.HasFlag(ObjFlags.Shootable), Is.False);
        Assert.That(_dog.RuntimeFlags.HasFlag(ObjFlags.NonMark), Is.True);
        Assert.That(_dog.CurrentState!.StateName, Is.EqualTo("Latched"));
    }

    [Test]
    public void An_Unlucky_Strike_Leaves_It_On_Until_The_Last_Try()
    {
        // Arrange
        _latching.LatchOn(_dog, "DogStruggle");
        var struggle = Struggle();

        // Act / Assert: never lucky, so the fifth strike does it
        for (int strike = 1; strike < 5; strike++)
        {
            _latching.ShakeOff(struggle, ["5", "0"]);
            Assert.That(_latching.StillHeld, Is.True, $"strike {strike}");
        }
        _latching.ShakeOff(struggle, ["5", "0"]);
        Assert.That(_latching.StillHeld, Is.False);
        Assert.That(_dog.CurrentState!.StateName, Is.EqualTo("Death"));
        Assert.That(_strikeSounds, Is.EqualTo(5));
    }

    [Test]
    public void A_Struck_Off_Dog_Dies_Where_The_Player_Is()
    {
        // Arrange
        _latching.LatchOn(_dog, "DogStruggle");

        // Act: always lucky
        _latching.ShakeOff(Struggle(), ["5", "256"]);

        // Assert
        Assert.That((_dog.X, _dog.Y, _dog.TileX, _dog.TileY), Is.EqualTo((_pawn.X, _pawn.Y, _pawn.TileX, _pawn.TileY)));
        Assert.That(_dog.Hitpoints, Is.LessThanOrEqualTo(0));
    }

    [Test]
    public void The_Struggle_Goes_On_Only_While_It_Holds_On()
    {
        // Arrange
        _latching.LatchOn(_dog, "DogStruggle");
        var struggle = Struggle();
        struggle.SetState("Fire");

        // Act / Assert
        _latching.JumpIfLatched(struggle, ["Ready"]);
        Assert.That(struggle.CurrentState!.StateName, Is.EqualTo("Ready"));

        struggle.SetState("Fire");
        _latching.ShakeOff(struggle, ["5", "256"]);
        _latching.JumpIfLatched(struggle, ["Ready"]);
        Assert.That(struggle.CurrentState!.StateName, Is.EqualTo("Fire"));
    }

    [Test]
    public void Letting_Go_Gives_The_Weapon_Back()
    {
        // Arrange
        _latching.LatchOn(_dog, "DogStruggle");
        _latching.ShakeOff(Struggle(), ["5", "256"]);

        // Act
        _latching.LatchRelease(Struggle());

        // Assert
        var playerstate = _session.PlayerState;
        Assert.That(_latching.IsLatched, Is.False);
        Assert.That(playerstate.Weapon, Is.EqualTo("Pistol"));
        Assert.That(_inventory.Has("DogStruggle"), Is.False);
        Assert.That(playerstate.MoveSlowdown, Is.EqualTo(1));
    }

    [Test]
    public void A_Latched_Dog_Hurts_The_One_It_Holds()
    {
        // Arrange
        _latching.LatchOn(_dog, "DogStruggle");

        // Act
        _latching.LatchHurt(_dog);

        // Assert
        Assert.That(_playerHurts, Has.Count.EqualTo(1));
        Assert.That(_playerHurts[0].Victim, Is.SameAs(_pawn));
        Assert.That(_playerHurts[0].Points, Is.InRange(0, 15));
    }

    [Test]
    public void A_Latched_Dog_Holding_No_One_Lets_Go()
    {
        // Arrange: latched in a saved game, say, with the player's side of it lost
        _latching.LatchOn(_dog, "DogStruggle");
        _session.PlayerState.Latch = null;

        // Act
        _latching.LatchHurt(_dog);

        // Assert
        Assert.That(_playerHurts, Is.Empty);
        Assert.That(_dog.RuntimeFlags.HasFlag(ObjFlags.Shootable), Is.True);
        Assert.That(_dog.RuntimeFlags.HasFlag(ObjFlags.NonMark), Is.False);
        Assert.That(_dog.CurrentState!.StateName, Is.EqualTo("Chase"));
    }

    [Test]
    public void A_New_Level_Ends_The_Struggle()
    {
        // Arrange
        _latching.LatchOn(_dog, "DogStruggle");
        _session.GameState.MapOn = "NEXTMAP";

        // Act
        _latching.Tick();

        // Assert
        Assert.That(_latching.IsLatched, Is.False);
        Assert.That(_session.PlayerState.Weapon, Is.EqualTo("Pistol"));
    }

    [Test]
    public void A_Dog_Killed_While_The_Struggle_Is_Idle_Lets_Them_Go()
    {
        // Arrange: killed some other way, the struggle waiting on Ready
        _latching.LatchOn(_dog, "DogStruggle");
        _dog.Hitpoints = 0;

        // Act / Assert: not while mid-strike
        _latching.Tick();
        Assert.That(_latching.IsLatched, Is.True);
        _weaponReady = true;
        _latching.Tick();
        Assert.That(_latching.IsLatched, Is.False);
    }

    [Test]
    public void A_Struggle_Held_With_Nothing_Latched_On_Goes()
    {
        // Arrange
        _inventory.Give("DogStruggle", 1);
        _session.PlayerState.Weapon = "DogStruggle";

        // Act
        _latching.Tick();

        // Assert
        Assert.That(_inventory.Has("DogStruggle"), Is.False);
        Assert.That(_session.PlayerState.Weapon, Is.EqualTo("Pistol"));
    }
}
