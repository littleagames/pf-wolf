using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities.Actors;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class StealthTests
{
    private const int Tile = 1 << MapConstants.TileShift;

    // A knife, the papers, the uniform, and enemies: a guard, a dog, a commander and an officer
    private const string Yaml = """
        Inventory:
          properties:
            "inventory.amount": 1
            "inventory.maxamount": 1
        Weapon:
          parent: Inventory
        Knife:
          parent: Weapon
          properties:
            "weapon.slot": 1
            "weapon.stealthkill": true
        Pistol:
          parent: Weapon
          properties:
            "weapon.slot": 2
        IDPapers:
          parent: Weapon
          properties:
            "weapon.slot": 1
            "weapon.papers": true
        Uniform:
          parent: Inventory
          properties:
            "inventory.disguise": true
            "inventory.pickupmessage": "Uniform taken."
            "inventory.pickupsound": "uniform"
        Monster: {}
        Guard:
          parent: Monster
          states:
            Spawn:
              - sprite: GURD
                frames: [A]
                tics-per-frame: -1
        Dog:
          parent: Guard
          properties:
            "monster.disguise": fooled
        Commander:
          parent: Guard
          properties:
            "monster.disguise": seesthrough
        Officer:
          parent: Guard
          properties:
            "monster.papersfail": "Not fooled!"
        CoatRack:
          states:
            Spawn:
              - sprite: UNIF
                frames: [A]
                tics-per-frame: -1
            Taken:
              - sprite: RACK
                frames: [A]
                tics-per-frame: -1
        """;

    private MapManager _map = null!;
    private GameSession _session = null!;
    private InventoryManager _inventory = null!;
    private Stealth _stealth = null!;
    private PlayerPawn _pawn = null!;
    private List<string> _messages = null!, _sounds = null!;
    private List<Entities.Actors.Actor> _caught = null!;
    private bool _stealthOnly;
    private int _papersRustles;

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
        _inventory.Give("Knife", 1);
        _inventory.Give("Pistol", 1);
        _session.PlayerState.Weapon = _session.PlayerState.ChosenWeapon = "Pistol";
        _session.PlayerState.Health = 100;
        _session.Tics = 1;

        _pawn = new PlayerPawn { State = _session.PlayerState };
        _pawn.SetPosition(10, 10);
        _session.PlayerState.Pawn = _pawn;

        var bar = new StatusBarDrawing(null!, null, null!, null!, _inventory, null!, null!, null!, _session,
            new Configuration.GameSettings(), new StatusBarHooks(map => map, (text, _, _) => [text]));
        var weapons = new PlayerWeapons(_inventory, null!, bar, _session, new PlayerWeaponsHooks(_ => { }));
        (_messages, _sounds, _caught, _stealthOnly, _papersRustles) = ([], [], [], false, 0);
        _stealth = new Stealth(_map, _inventory, weapons, _session, new StealthHooks(
            () => new StealthInfo { Suspected = "Suspected", Blown = "Blown", Lost = "Lost", Caught = "Caught", Alert = "Alert" },
            () => _stealthOnly,
            _messages.Add,
            _sounds.Add,
            () => _papersRustles++,
            _ => true,
            _caught.Add));
    }

    // In uniform, papers in hand
    private void Disguise()
    {
        _inventory.Give("Uniform", 1);
        _inventory.Give("IDPapers", 1);
        _session.PlayerState.Weapon = _session.PlayerState.ChosenWeapon = "IDPapers";
    }

    private Monster Enemy(string name, int tilesEast = 2)
    {
        var enemy = (Monster)_inventory.CreateActor(name)!;
        enemy.SetPosition(10 + tilesEast, 10);
        enemy.RuntimeFlags |= ObjFlags.Shootable | ObjFlags.Visible;
        enemy.Hitpoints = 25;
        _map.GetActors().AddLast(enemy);
        return enemy;
    }

    [Test]
    public void Out_Of_Disguise_Or_Without_Papers_In_Hand_Theyre_Seen_As_Ever()
    {
        // Arrange
        var guard = Enemy("Guard");

        // Act / Assert: no uniform
        Assert.That(_stealth.Overlooks(guard), Is.False);

        // in uniform, a pistol in hand
        _inventory.Give("Uniform", 1);
        Assert.That(_stealth.Overlooks(guard), Is.False);

        // in uniform, papers in hand, but already noticed
        Disguise();
        _session.PlayerState.Undetected = false;
        Assert.That(_stealth.Overlooks(guard), Is.False);
    }

    [Test]
    public void Game_Info_Has_Stealth_Settings_And_Stealth_Only_Maps()
    {
        // Act
        var settings = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<StealthInfo>("suspected: \"$STR_STEALTH_SUSPECTED\"\nsuspicion-tics: 350\n");
        var map = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<MapInfo>("stealth-only: true\n");

        // Assert
        Assert.That((settings.Suspected, settings.SuspicionTics, settings.Range), Is.EqualTo(("$STR_STEALTH_SUSPECTED", 350, 10)));
        Assert.That(map.StealthOnly, Is.True);
        Assert.That(new GameInfoAsset().Stealth.SuspicionTics, Is.EqualTo(700));
    }

    [Test]
    public void One_Too_Far_Off_Ignores_Them()
    {
        Disguise();
        Assert.That(_stealth.Overlooks(Enemy("Commander", 11)), Is.True);
    }

    [Test]
    public void A_Suspicious_Enemy_Waits_Then_Notices_Them()
    {
        // Arrange: suspicious, two tics left
        Disguise();
        var guard = Enemy("Guard");
        guard.Suspicion = 2;

        // Act / Assert
        Assert.That(_stealth.Overlooks(guard), Is.True);
        Assert.That(_session.PlayerState.SuspicionPending, Is.GreaterThan(0));
        Assert.That(_stealth.Overlooks(guard), Is.False);
        Assert.That(guard.Suspicion, Is.Zero);
    }

    [Test]
    public void The_Meter_Shows_What_The_Enemies_Told_It_Last_Tic()
    {
        // Arrange
        Disguise();
        var guard = Enemy("Guard");
        guard.Suspicion = 350;          // half of 700

        // Act / Assert
        _stealth.Overlooks(guard);
        _stealth.Tick();
        Assert.That(_session.PlayerState.SuspicionMeter, Is.EqualTo(50));
        _stealth.Tick();
        Assert.That(_session.PlayerState.SuspicionMeter, Is.Zero);
    }

    [Test]
    public void A_Dog_Is_Fooled_And_A_Commander_Sees_Through_It()
    {
        Disguise();
        var dog = Enemy("Dog");
        Assert.That(_stealth.Overlooks(dog), Is.True);
        Assert.That(dog.Suspicion, Is.EqualTo(-1));
        Assert.That(_stealth.Overlooks(Enemy("Commander")), Is.False);
    }

    [Test]
    public void Papers_Satisfy_A_Suspicious_Enemy()
    {
        // Arrange
        Disguise();
        var guard = Enemy("Guard");
        guard.Suspicion = 300;

        // Act
        _stealth.ShowPapers(null!);

        // Assert
        Assert.That(_papersRustles, Is.EqualTo(1));
        Assert.That(guard.Suspicion, Is.EqualTo(-1));
        Assert.That(_stealth.Overlooks(guard), Is.True);
        Assert.That(_session.PlayerState.Undetected, Is.True);
    }

    [Test]
    public void An_Officer_Isnt_Fooled_By_Papers()
    {
        // Arrange
        Disguise();
        var officer = Enemy("Officer");
        officer.Suspicion = 300;

        // Act
        _stealth.ShowPapers(null!);

        // Assert
        Assert.That(_messages, Is.EqualTo(new[] { "Not fooled!" }));
        Assert.That(_session.PlayerState.Undetected, Is.False);
    }

    [Test]
    public void Noticed_In_Disguise_It_Is_Lost()
    {
        // Arrange
        Disguise();

        // Act
        bool caught = _stealth.Spotted(Enemy("Guard"));

        // Assert
        Assert.That(caught, Is.False);
        Assert.That(_messages, Is.EqualTo(new[] { "Blown" }));
        Assert.That(_session.PlayerState.Undetected, Is.False);
        Assert.That(_inventory.Has("Uniform"), Is.False);
        Assert.That(_inventory.Has("IDPapers"), Is.False);
        Assert.That(_session.PlayerState.Weapon, Is.Not.EqualTo("IDPapers"));
    }

    [Test]
    public void Noticed_After_Killing_Unseen_They_Hear_So()
    {
        // Arrange: a knife kill
        _session.PlayerState.Weapon = "Knife";
        _stealth.Killed(_pawn);

        // Act
        _stealth.Spotted(Enemy("Guard"));

        // Assert
        Assert.That(_session.PlayerState.StealthKills, Is.EqualTo(1));
        Assert.That(_messages, Is.EqualTo(new[] { "Lost" }));
    }

    [Test]
    public void Only_A_Stealth_Weapon_Or_A_Scope_Kills_Unseen()
    {
        // Act: a pistol, a zoomed scope, then a knife once seen
        _stealth.Killed(_pawn);
        _session.PlayerState.ZoomMode = ZoomMode.Zoomed;
        _stealth.Killed(_pawn);
        _session.PlayerState.ZoomMode = ZoomMode.Ready;
        _session.PlayerState.Undetected = false;
        _session.PlayerState.Weapon = "Knife";
        _stealth.Killed(_pawn);

        // Assert
        Assert.That(_session.PlayerState.StealthKills, Is.EqualTo(1));
    }

    [Test]
    public void Noticed_On_A_Stealth_Only_Map_Theyre_Caught()
    {
        // Arrange
        _stealthOnly = true;
        var guard = Enemy("Guard");

        // Act
        bool caught = _stealth.Spotted(guard);

        // Assert
        Assert.That(caught, Is.True);
        Assert.That(_caught, Is.EqualTo(new Entities.Actors.Actor[] { guard }));
        Assert.That(_messages, Is.EqualTo(new[] { "Caught" }));
    }

    [Test]
    public void The_Uniform_Goes_On_Unless_Enemies_Are_After_Them()
    {
        // Arrange: someone's after them
        var rack = _inventory.CreateActor("CoatRack")!;
        rack.SetState("Spawn");
        var guard = Enemy("Guard");
        guard.RuntimeFlags |= ObjFlags.AttackMode;
        _session.PlayerState.Undetected = false;

        // Act / Assert
        _stealth.Disguise(rack, ["Uniform", "IDPapers", "Taken"]);
        Assert.That(_messages, Is.EqualTo(new[] { "Alert" }));
        Assert.That(_inventory.Has("Uniform"), Is.False);

        guard.RuntimeFlags &= ~ObjFlags.AttackMode;
        _stealth.Disguise(rack, ["Uniform", "IDPapers", "Taken"]);
        Assert.That(_inventory.Has("Uniform") && _inventory.Has("IDPapers"), Is.True);
        Assert.That(_session.PlayerState.Undetected, Is.True);
        Assert.That(_messages.Last(), Is.EqualTo("Uniform taken."));
        Assert.That(_sounds, Is.EqualTo(new[] { "uniform" }));
        Assert.That(rack.CurrentState!.StateName, Is.EqualTo("Taken"));
    }
}
