using PFWolf.Assets;

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
    public void The_Switch_A_Level_Was_Left_By_Is_Put_Back_As_It_Was()
    {
        // Arrange: an elevator thrown (21 to 22, beside a door), and a patch switch flipped twice
        // (its own tile, then again as one tagged with it: 5 to 6, then 6 to 6)
        var map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session, null)
        {
            TileMap = new ushort[Constants.MapConstants.ClassicMapSize, Constants.MapConstants.ClassicMapSize],
        };
        var entry = new LevelEntry(map, _inventory, _hubs, null!, null!, _session,
            new LevelEntryHooks(() => { }, () => [], _ => { }, _ => null!, _ => { }));
        ushort beside = (ushort)(21 | GameConstants.BitWall);
        map.TileMap[11, 10] = (ushort)(22 | GameConstants.BitWall);
        map.SetPatch(3, 4, 6);
        map.ExitSwitch.AddRange([(11, 10, false, beside), (3, 4, true, 5), (3, 4, true, 6)]);

        // Act
        entry.PutBackExitSwitch();

        // Assert
        Assert.That(map.TileMap[11, 10], Is.EqualTo(beside));
        Assert.That(map.GetPatch(3, 4), Is.EqualTo(5));
        Assert.That(map.ExitSwitch, Is.Empty);
    }

    [Test]
    public void Leaving_A_Level_Thats_Not_In_A_Hub_Keeps_Nothing()
    {
        // No level entered yet, so none to keep (and no map to capture: that would throw)
        Assert.DoesNotThrow(() => _entry.LeaveLevel());
    }
}
