namespace PFWolf.Tests;

public class LevelEntryTests
{
    private const string Yaml = """
        CustomInventory: {}
        GoldKey:
          parent: CustomInventory
          properties:
            "inventory.maxamount": 1
        """;

    private GameSession _session = null!;
    private InventoryManager _inventory = null!;
    private Hubs _hubs = null!;
    private LevelEntry _entry = null!;

    [SetUp]
    public void Make()
    {
        _session = new GameSession();
        _inventory = new InventoryManager(ActorDefs.Metadata(Yaml));
        _inventory.SetHolder(_session.PlayerState.Items);
        _hubs = new Hubs(null!, null!, _session);
        _entry = new LevelEntry(null!, _inventory, _hubs, null!, null!, _session,
            new LevelEntryHooks(() => { }, () => [], _ => { }, _ => null!, _ => { }));
    }

    [Test]
    public void With_No_Level_Start_Kept_There_Is_Nothing_To_Go_Back_To()
    {
        Assert.That(_entry.RestoreLevelStart(), Is.False);
    }

    [Test]
    public void Death_Puts_The_Player_Back_As_They_Came_In_With_The_Lives_They_Have_Now()
    {
        // Arrange: into the level with 80 health, 1000 points, 3 lives and no key
        var player = _session.PlayerState;
        (player.Health, player.Score, player.Lives) = (80, 1000, 3);
        _session.GameState.MapOn = "MAP03";
        _entry.MarkLevelStart();

        // the level's played: a key, more points, then a life lost
        _inventory.Give("GoldKey", 1);
        (player.Health, player.Score, player.Lives) = (5, 4000, 2);
        _session.GameState.MapOn = "MAP04";

        // Act
        bool restored = _entry.RestoreLevelStart();

        // Assert
        Assert.That(restored, Is.True);
        Assert.That((player.Health, player.Score, player.Lives), Is.EqualTo((80, 1000, 2)));
        Assert.That(_inventory.GetCount("GoldKey"), Is.EqualTo(0));
        Assert.That(_session.GameState.MapOn, Is.EqualTo("MAP03"));
    }

    [Test]
    public void Leaving_A_Level_Thats_Not_In_A_Hub_Keeps_Nothing()
    {
        // No level entered yet, so none to keep (and no map to capture: that would throw)
        Assert.DoesNotThrow(() => _entry.LeaveLevel());
    }
}
