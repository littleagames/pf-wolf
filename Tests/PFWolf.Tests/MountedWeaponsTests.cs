using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class MountedWeaponsTests
{
    private const int Tile = 1 << MapConstants.TILESHIFT;

    // A pistol to start with, the gun on a nest, and the nest
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
        NestGun:
          parent: Weapon
          properties:
            "weapon.mounted": true
            "weapon.mountdamage": 50
            "weapon.mountnoroom": "No room!"
        Nest:
          states:
            Spawn:
              - sprite: NEST
                frames: [A]
                tics-per-frame: -1
        """;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private InventoryManager _inventory = null!;
    private MountedWeapons _mounted = null!;
    private PlayerPawn _pawn = null!;
    private Entities.Actors.Actor _nest = null!;
    private List<string> _sounds = null!, _messages = null!;

    [SetUp]
    public void MakeLevel()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session)
        {
            tilemap = new ushort[MapManager.MAPSIZE, MapManager.MAPSIZE],
            actorat = new Actor?[MapManager.MAPSIZE, MapManager.MAPSIZE],
        };
        _inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
        _inventory.SetHolder(_session.playerstate.Items);
        _inventory.Give("Pistol", 1);
        _session.playerstate.weapon = _session.playerstate.chosenweapon = "Pistol";

        // The player at 10,10 facing east, the nest a tile ahead
        _pawn = new PlayerPawn { State = _session.playerstate, Angle = 0 };
        _pawn.SetPosition(10, 10);
        _session.playerstate.Pawn = _pawn;
        _nest = _inventory.CreateActor("Nest")!;
        _nest.SetPosition(11, 10);
        _map.GetActors().AddLast(_nest);

        var bar = new StatusBarDrawing(null!, null, null!, null!, _inventory, null!, null!, null!, _session,
            new Configuration.GameSettings(), new StatusBarHooks(map => map, (text, _, _) => [text]));
        var weapons = new PlayerWeapons(_inventory, null!, bar, _session, new PlayerWeaponsHooks(_ => { }));
        _sounds = [];
        _messages = [];
        _mounted = new MountedWeapons(_map, _inventory, weapons, _session, new MountedWeaponsHooks(_sounds.Add, _messages.Add, () => { }));
    }

    [Test]
    public void A_Mount_Puts_The_Player_On_Its_Tile_With_The_Gun()
    {
        // Arrange: facing a little off east
        _pawn.Angle = 20;

        // Act
        _mounted.A_Mount(_nest, ["NestGun", "nest/load"]);

        // Assert
        Assert.That(_mounted.IsMounted, Is.True);
        Assert.That((_pawn.TileX, _pawn.TileY, _pawn.Angle), Is.EqualTo(((byte)11, (byte)10, (short)0)));
        Assert.That(_session.playerstate.weapon, Is.EqualTo("NestGun"));
        Assert.That(_inventory.Has("NestGun"), Is.True);
        Assert.That(_sounds, Is.EqualTo(new[] { "nest/load" }));
        Assert.That(_session.playerinput.IsHeld(ButtonType.bt_attack), Is.True);
    }

    [Test]
    public void A_Mount_Without_A_Weapon_Class_Does_Nothing()
    {
        _mounted.A_Mount(_nest, ["NoSuchGun"]);
        Assert.That(_mounted.IsMounted, Is.False);
        Assert.That((_pawn.TileX, _pawn.TileY), Is.EqualTo(((byte)10, (byte)10)));
    }

    [Test]
    public void Getting_Off_Steps_Back_With_The_Weapon_They_Had()
    {
        // Arrange
        _mounted.A_Mount(_nest, ["NestGun"]);

        // Act
        bool wasOn = _mounted.TryDismount();

        // Assert: back where they came from, the gun gone
        Assert.That(wasOn, Is.True);
        Assert.That(_mounted.IsMounted, Is.False);
        Assert.That((_pawn.TileX, _pawn.TileY), Is.EqualTo(((byte)10, (byte)10)));
        Assert.That(_session.playerstate.weapon, Is.EqualTo("Pistol"));
        Assert.That(_inventory.Has("NestGun"), Is.False);
    }

    [Test]
    public void Getting_Off_Tries_The_Sides_Then_The_Front_When_Behind_Is_Blocked()
    {
        // Arrange: a wall behind and on the north side, a solid-free south side
        _mounted.A_Mount(_nest, ["NestGun"]);
        _map.tilemap[10, 10] = 1;
        _map.tilemap[11, 9] = 1;

        // Act
        _mounted.TryDismount();

        // Assert
        Assert.That((_pawn.TileX, _pawn.TileY), Is.EqualTo(((byte)11, (byte)11)));
    }

    [Test]
    public void With_Nowhere_To_Go_They_Stay_On_And_Are_Told()
    {
        // Arrange: walled in all round, and someone shootable standing where a wall isn't
        _mounted.A_Mount(_nest, ["NestGun"]);
        _map.tilemap[10, 10] = _map.tilemap[11, 9] = _map.tilemap[11, 11] = 1;
        var guard = new Monster { Name = "Guard" };
        guard.SetPosition(12, 10);
        guard.RuntimeFlags |= ObjFlags.FL_SHOOTABLE;
        _map.GetActors().AddLast(guard);

        // Act
        bool wasOn = _mounted.TryDismount();

        // Assert
        Assert.That(wasOn, Is.True);
        Assert.That(_mounted.IsMounted, Is.True);
        Assert.That(_messages, Is.EqualTo(new[] { "No room!" }));
    }

    [Test]
    public void Use_When_Not_On_One_Isnt_A_Dismount()
    {
        Assert.That(_mounted.TryDismount(), Is.False);
    }

    [Test]
    public void A_New_Level_Lets_Them_Off_Where_They_Are()
    {
        // Arrange
        _mounted.A_Mount(_nest, ["NestGun"]);
        _session.gamestate.mapon = "NEXTMAP";

        // Act
        _mounted.Tick();

        // Assert
        Assert.That(_mounted.IsMounted, Is.False);
        Assert.That(_session.playerstate.weapon, Is.EqualTo("Pistol"));
        Assert.That(_inventory.Has("NestGun"), Is.False);
    }

    [Test]
    public void A_Mounted_Gun_Held_Without_Manning_It_Goes()
    {
        // Arrange: as a saved game might bring back
        _inventory.Give("NestGun", 1);
        _session.playerstate.weapon = "NestGun";

        // Act
        _mounted.Tick();

        // Assert
        Assert.That(_inventory.Has("NestGun"), Is.False);
        Assert.That(_session.playerstate.weapon, Is.EqualTo("Pistol"));
    }

    [TestCase(ControlDir.di_east, -1, 0)]
    [TestCase(ControlDir.di_north, 0, 1)]
    [TestCase(ControlDir.di_west, 1, 0)]
    [TestCase(ControlDir.di_south, 0, -1)]
    public void The_First_Tile_Tried_Is_Behind(ControlDir facing, int dx, int dy)
    {
        var tiles = MountedWeapons.ExitTiles(10, 10, facing).ToList();
        Assert.That(tiles[0], Is.EqualTo((10 + dx, 10 + dy)));
        Assert.That(tiles[3], Is.EqualTo((10 - dx, 10 - dy)));
    }
}
