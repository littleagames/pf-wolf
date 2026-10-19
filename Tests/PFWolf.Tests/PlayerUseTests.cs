using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class PlayerUseTests
{
    private const int Tile = 1 << MapConstants.TileShift;

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
        Medkit:
          parent: Inventory
          properties:
            "inventory.useslot": 9
            "inventory.use": A_TestUseMedkit
            "inventory.usenone": "You do not have any medkits."
        Weapon:
          parent: Inventory
        Rifle:
          parent: Weapon
          properties:
            "weapon.slot": 7
            "weapon.notheld": "You don't have this weapon"
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
        Radio:
          properties:
            usehint: take
            usehint.except: Collected
          states:
            Spawn:
              - sprite: RADI
                frames: [A]
                tics-per-frame: -1
            Use:
              - sprite: RADI
                frames: [B]
                tics-per-frame: -1
            Collected:
              - sprite: RADI
                frames: [B]
                tics-per-frame: -1
        LogBook:
          properties:
            usehint: take
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
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        var pawn = new PlayerPawn { State = _session.PlayerState, X = 10 * Tile + Tile / 2, Y = 10 * Tile + Tile / 2, TileX = 10, TileY = 10, Angle = 0 };
        _session.PlayerState.Pawn = pawn;
        _inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
        _inventory.SetHolder(_session.PlayerState.Items);
        _use = new PlayerUse(_map, null!, _inventory, null!, new LevelAI(_map, null!, null!, _session), new TimedSwitches(_map, _session), _session, new PlayerUseHooks(_ => { }, () => false));
    }

    // A prisoner dx, dy (in 1/65536 tiles) from the player, east being +x and south +y
    private Monster Prisoner(int dx, int dy)
    {
        var prisoner = (Monster)_inventory.CreateActor("Prisoner")!;
        (prisoner.X, prisoner.Y) = (_session.Player.X + dx, _session.Player.Y + dy);
        prisoner.RuntimeFlags |= ObjFlags.Shootable;
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
        Assert.That(_session.PlayerInput.IsHeld(ButtonType.Use), Is.True);
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
        prisoner.RuntimeFlags |= ObjFlags.AttackMode;

        // Act / Assert
        Assert.That(_use.TryUseActor(), Is.False);
        prisoner.RuntimeFlags &= ~ObjFlags.AttackMode;
        _session.PlayerInput.SetHeld(ButtonType.Use, true);
        Assert.That(_use.TryUseActor(), Is.False);
    }

    // Scenery with a Use state (a log book): never shootable, and not a Monster
    private Entities.Actors.Actor LogBook(int dx, int dy)
    {
        var book = _inventory.CreateActor("LogBook")!;
        (book.X, book.Y) = (_session.Player.X + dx, _session.Player.Y + dy);
        (book.TileX, book.TileY) = ((byte)(book.X >> MapConstants.TileShift), (byte)(book.Y >> MapConstants.TileShift));
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

    // At the map's east edge, facing out of it: no tile ahead to give a hint, so the actors' show alone
    private void FaceOffTheMap()
    {
        var pawn = _session.Player;
        (pawn.TileX, pawn.TileY) = (MapConstants.ClassicMapSize - 1, 10);
        (pawn.X, pawn.Y) = ((MapConstants.ClassicMapSize - 1) * Tile + Tile / 2, 10 * Tile + Tile / 2);
        pawn.Angle = 0;
        _session.PlayerState.Health = 100;
    }

    [Test]
    public void An_Actor_There_To_Be_Used_Gives_Its_Use_Hint()
    {
        // Arrange: a log book underfoot (usehint: take), and a prisoner with none
        FaceOffTheMap();
        var book = _inventory.CreateActor("LogBook")!;
        (book.X, book.Y) = (_session.Player.X, _session.Player.Y);
        book.SetState(book.ResolvedStates["Spawn"]);
        _map.GetActors().AddLast(book);

        // Act / Assert
        Assert.That(_use.UseHint(null), Is.EqualTo("take"));
        _map.GetActors().Remove(book);
        Assert.That(_use.UseHint(null), Is.Null);
    }

    [Test]
    public void No_Use_Hint_On_A_State_Its_Usehint_Except_Names()
    {
        // Arrange: a radio underfoot (usehint: take, except once Collected)
        FaceOffTheMap();
        var radio = _inventory.CreateActor("Radio")!;
        (radio.X, radio.Y) = (_session.Player.X, _session.Player.Y);
        radio.SetState(radio.ResolvedStates["Spawn"]);
        _map.GetActors().AddLast(radio);

        // Act / Assert
        Assert.That(_use.UseHint(null), Is.EqualTo("take"));
        radio.SetState(radio.ResolvedStates["Collected"]);
        Assert.That(_use.UseHint(null), Is.Null);
    }

    [Test]
    public void No_Use_Hint_For_The_Dead()
    {
        FaceOffTheMap();
        _session.PlayerState.Health = 0;
        Assert.That(_use.UseHint(null), Is.Null);
    }

    [TestCase(0, 0, true)]          // facing the same way: its back is to the player
    [TestCase(20, 0, true)]
    [TestCase(345, 10, true)]       // across east: 25 apart
    [TestCase(340, 10, false)]      // 30 apart is too far round
    [TestCase(45, 0, false)]
    [TestCase(180, 0, false)]       // facing them
    public void An_Enemy_Facing_Within_30_Degrees_Of_The_Player_Has_Its_Back_To_Them(int enemy, int player, bool back)
    {
        Assert.That(PlayerAttacks.BackTurned(enemy, player), Is.EqualTo(back));
    }

    [TestCase(180, 0, true)]
    [TestCase(150, 0, true)]
    [TestCase(130, 0, false)]
    [TestCase(0, 0, false)]
    public void An_Enemy_Facing_Within_40_Degrees_Of_The_Player_Faces_Them(int enemy, int player, bool faces)
    {
        Assert.That(PlayerAttacks.FacesPlayer(enemy, player), Is.EqualTo(faces));
    }

    [Test]
    public void Nothing_Is_Used_Without_The_Item()
    {
        _use.UseSlotItem(6);
        _use.UseHeldItem();
        Assert.That(Detonations, Is.Zero);
    }

    [Test]
    public void A_Slots_Key_With_None_Held_Says_So_If_The_Item_Has_A_Message()
    {
        // Arrange
        var messages = new List<string>();
        var use = new PlayerUse(_map, null!, _inventory, null!, new LevelAI(_map, null!, null!, _session), new TimedSwitches(_map, _session), _session,
            new PlayerUseHooks(_ => { }, () => false, Message: messages.Add));

        // Act: the medkit's slot, then the detonator's (no message)
        use.UseSlotItem(9);
        use.UseSlotItem(6);

        // Assert
        Assert.That(messages, Is.EqualTo(new[] { "You do not have any medkits." }));
    }

    [Test]
    public void A_Weapon_Slots_Key_Says_So_Without_The_Weapon()
    {
        // Arrange
        var messages = new List<string>();
        var use = new PlayerUse(_map, null!, _inventory, null!, new LevelAI(_map, null!, null!, _session), new TimedSwitches(_map, _session), _session,
            new PlayerUseHooks(_ => { }, () => false, Message: messages.Add));

        // Act: the rifle's slot without it, then with it (out of ammo, say: nothing to say)
        use.UseSlotItem(7);
        _inventory.Give("Rifle", 1);
        use.UseSlotItem(7);

        // Assert
        Assert.That(messages, Is.EqualTo(new[] { "You don't have this weapon" }));
    }

    [Test]
    public void Holding_Use_Uses_The_Item_Every_So_Many_Tics()
    {
        // Arrange
        _inventory.Give("Detonator", 1);
        _session.Tics = 20;

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
    public void An_Exit_Waits_While_A_Level_Objective_Blocks_It()
    {
        // Arrange: the objectives say no
        var use = new PlayerUse(_map, null!, _inventory, null!, new LevelAI(_map, null!, null!, _session), new TimedSwitches(_map, _session), _session,
            new PlayerUseHooks(_ => { }, () => false, ExitAllowed: () => false));

        // Act / Assert: refused, the level goes on
        Assert.That(use.ExitAction(secret: false), Is.False);
        Assert.That(_session.PlayState, Is.EqualTo(PlayState.StillPlaying));
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
