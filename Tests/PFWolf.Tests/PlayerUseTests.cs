using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class PlayerUseTests
{
    private const int Tile = 1 << MapConstants.TILESHIFT;

    // A prisoner who can be used, and two items used by keys: one by an empty weapon slot, one
    // by holding use (their actions are registered by the tests)
    private const string Yaml = """
        Inventory:
          properties:
            "inventory.amount": 1
            "inventory.maxamount": 1
        Detonator:
          parent: Inventory
          properties:
            "inventory.useslot": 6
            "inventory.useholdtics": 60
            "inventory.use": A_TestUseDetonator
        RedKey:
          parent: Inventory
        Monster: {}
        Prisoner:
          parent: Monster
          states:
            Spawn:
              - sprite: PRIS
                frames: [A]
                tics-per-frame: -1
            Use:
              - sprite: PRIS
                frames: [B]
                tics-per-frame: -1
        LogBook:
          states:
            Spawn:
              - sprite: LOGB
                frames: [A]
                tics-per-frame: -1
            Use:
              - sprite: LOGB
                frames: [B]
                tics-per-frame: -1
        """;

    // The item's action is left unregistered (the registry is shared, and other tests check every
    // name in it), so each use shows as the registry saying it has no handler for it
    private StringWriter _console = null!;
    private TextWriter _realConsole = null!;

    private int Detonations => _console.ToString().Split('\n').Count(line => line.Contains("'A_TestUseDetonator'"));

    [TearDown]
    public void PutConsoleBack() => Console.SetOut(_realConsole);

    private MapManager _map = null!;
    private GameSession _session = null!;
    private InventoryManager _inventory = null!;
    private PlayerUse _use = null!;

    [SetUp]
    public void MakeLevel()
    {
        _realConsole = Console.Out;
        _console = new StringWriter();
        Console.SetOut(_console);
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session)
        {
            tilemap = new ushort[MapManager.MAPSIZE, MapManager.MAPSIZE],
            actorat = new Actor?[MapManager.MAPSIZE, MapManager.MAPSIZE],
        };
        var pawn = new PlayerPawn { State = _session.playerstate, X = 10 * Tile + Tile / 2, Y = 10 * Tile + Tile / 2, TileX = 10, TileY = 10, Angle = 0 };
        _session.playerstate.Pawn = pawn;
        _inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
        _inventory.SetHolder(_session.playerstate.Items);
        _use = new PlayerUse(_map, null!, _inventory, null!, new LevelAI(_map, null!, null!, _session), _session, new PlayerUseHooks(_ => { }, _ => { }, () => false));
    }

    // A prisoner dx, dy (in 1/65536 tiles) from the player, east being +x and south +y
    private Monster Prisoner(int dx, int dy)
    {
        var prisoner = (Monster)_inventory.CreateActor("Prisoner")!;
        (prisoner.X, prisoner.Y) = (_session.player.X + dx, _session.player.Y + dy);
        prisoner.RuntimeFlags |= objflags.FL_SHOOTABLE;
        prisoner.SetState(prisoner.ResolvedStates["Spawn"]);
        _map.GetActors().AddLast(prisoner);
        return prisoner;
    }

    [Test]
    public void Use_Facing_An_Actor_With_A_Use_State_Sends_It_There()
    {
        // Arrange: a tile ahead (the player faces east)
        var prisoner = Prisoner(Tile, 0);

        // Act
        bool used = _use.TryUseActor();

        // Assert
        Assert.That(used, Is.True);
        Assert.That(prisoner.CurrentState!.StateName, Is.EqualTo("Use"));
        Assert.That(_session.playerinput.IsHeld(buttontypes.bt_use), Is.True);
    }

    [Test]
    public void The_Nearest_Of_Those_Ahead_Is_Used()
    {
        // Arrange
        var far = Prisoner(Tile * 5 / 4, 0);
        var near = Prisoner(Tile, Tile / 4);

        // Act
        _use.TryUseActor();

        // Assert
        Assert.That(near.CurrentState!.StateName, Is.EqualTo("Use"));
        Assert.That(far.CurrentState!.StateName, Is.EqualTo("Spawn"));
    }

    [TestCase(-1, 0)]       // behind
    [TestCase(0, 1)]        // off to the side, more than half a right angle
    [TestCase(2, 0)]        // more than a tile and a half away
    public void One_Behind_Aside_Or_Too_Far_Isnt(int tilesX, int tilesY)
    {
        // Arrange
        var prisoner = Prisoner(tilesX * Tile, tilesY * Tile);

        // Act / Assert
        Assert.That(_use.TryUseActor(), Is.False);
        Assert.That(prisoner.CurrentState!.StateName, Is.EqualTo("Spawn"));
    }

    [Test]
    public void One_Going_After_Someone_Or_Use_Held_Down_Isnt()
    {
        // Arrange
        var prisoner = Prisoner(Tile, 0);
        prisoner.RuntimeFlags |= objflags.FL_ATTACKMODE;

        // Act / Assert
        Assert.That(_use.TryUseActor(), Is.False);
        prisoner.RuntimeFlags &= ~objflags.FL_ATTACKMODE;
        _session.playerinput.SetHeld(buttontypes.bt_use, true);
        Assert.That(_use.TryUseActor(), Is.False);
    }

    // Scenery with a Use state (a log book): never shootable, and not a Monster
    private Entities.Actors.Actor LogBook(int dx, int dy)
    {
        var book = _inventory.CreateActor("LogBook")!;
        (book.X, book.Y) = (_session.player.X + dx, _session.player.Y + dy);
        (book.TileX, book.TileY) = ((byte)(book.X >> MapConstants.TILESHIFT), (byte)(book.Y >> MapConstants.TILESHIFT));
        book.SetState(book.ResolvedStates["Spawn"]);
        _map.GetActors().AddLast(book);
        return book;
    }

    [Test]
    public void Scenery_With_A_Use_State_Is_Used_Too()
    {
        // Arrange: a tile ahead
        var book = LogBook(Tile, 0);

        // Act / Assert
        Assert.That(_use.TryUseActor(), Is.True);
        Assert.That(book.CurrentState!.StateName, Is.EqualTo("Use"));
    }

    [Test]
    public void Scenery_On_The_Players_Own_Tile_Is_Used_Whichever_Way_They_Face()
    {
        // Arrange: just behind the player, on their tile
        var book = LogBook(-Tile / 4, 0);

        // Act / Assert
        Assert.That(_use.TryUseActor(), Is.True);
        Assert.That(book.CurrentState!.StateName, Is.EqualTo("Use"));
    }

    [Test]
    public void An_Empty_Slots_Key_Uses_The_Item_Held_For_It()
    {
        // Arrange
        _inventory.Give("Detonator", 1);

        // Act
        _use.UseSlotItem(5);
        _use.UseSlotItem(6);

        // Assert: once, for slot 6
        Assert.That(Detonations, Is.EqualTo(1));
        Assert.That(_use.HeldUsableItem(_ => true), Is.EqualTo("A_TestUseDetonator"));
    }

    [Test]
    public void Nothing_Is_Used_Without_The_Item()
    {
        _use.UseSlotItem(6);
        _use.UseHeldItem();
        Assert.That(Detonations, Is.Zero);
    }

    [Test]
    public void Holding_Use_Uses_The_Item_Every_So_Many_Tics()
    {
        // Arrange
        _inventory.Give("Detonator", 1);
        _session.tics = 20;

        // Act / Assert
        _use.UseHeldItem();
        _use.UseHeldItem();
        Assert.That(Detonations, Is.Zero);
        _use.UseHeldItem();         // 60 tics
        Assert.That(Detonations, Is.EqualTo(1));
        _use.UseHeldItem();
        _use.ResetUseHeld();        // let go
        _use.UseHeldItem();
        _use.UseHeldItem();
        Assert.That(Detonations, Is.EqualTo(1));
    }

    [Test]
    public void A_Switch_Without_A_Lock_Or_With_Its_Item_Held_Isnt_Refused()
    {
        // Arrange
        _inventory.Give("RedKey", 1);

        // Act / Assert
        Assert.That(_use.RefuseSwitchLock(new MapSwitchTranslation()), Is.False);
        Assert.That(_use.RefuseSwitchLock(new MapSwitchTranslation { Lock = "RedKey" }), Is.False);
    }
}
