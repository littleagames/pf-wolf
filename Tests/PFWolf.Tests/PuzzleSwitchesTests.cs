using System.Reflection;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Gameplay;
using PFWolf.SaveGames;

namespace PFWolf.Tests;

/// <summary>
/// Switches with a puzzle (Gameplay/PuzzleSwitches.cs): only a success throws them, a fail runs
/// its actions and may lock them out or break them, a skip item throws them at once, and their
/// state goes into a save
/// </summary>
public class PuzzleSwitchesTests
{
    private const int SwitchX = 10, SwitchY = 12, SwitchWall = 70, BrokenWall = 75;
    private const ushort Tag = 3;

    private GameSession _session = null!;
    private MapManager _map = null!;
    private InventoryManager _inventory = null!;
    private PuzzleSwitches _switches = null!;
    private readonly List<string> _log = [];
    private Action<string>? _ended;
    private bool _opens;

    [SetUp]
    public void SetUp()
    {
        _session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session, null)
        {
            MapWidth = MapConstants.ClassicMapSize, MapHeight = MapConstants.ClassicMapSize,
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        typeof(MapManager).GetField("mapsegs", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_map, Enumerable.Range(0, MapManager.LevelPlanes).Select(_ => new ushort[MapConstants.ClassicMapArea]).ToArray());
        typeof(MapManager).GetField("_levelMapData", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_map, new MapObjectTranslationAsset());
        _map.TileMap[SwitchX, SwitchY] = SwitchWall;

        _inventory = new InventoryManager(ActorDefs.Metadata("""
            Inventory: {}
            AutoHack: { parent: Inventory }
            """));
        _inventory.SetHolder(_session.PlayerState.Items);

        _log.Clear();
        _ended = null;
        _opens = true;
        _switches = new PuzzleSwitches(_map, _inventory, _session, new PuzzleSwitchHooks(
            (name, args, x, y, tag, ended) =>
            {
                _log.Add($"open {name} [{string.Join(" ", args)}] {x},{y} {tag}");
                _ended = ended;
                return _opens;
            },
            message => _log.Add($"message {message}")));
    }

    // The acting player uses the switch: whether its use was the puzzle's
    private bool Use(MapSwitchTranslation sw) =>
        _switches.Use(sw, SwitchX, SwitchY, patch: false, Tag, () => _log.Add("thrown"), actions => _log.Add($"actions {string.Join(" ", actions)}"));

    private static MapSwitchTranslation Hack(int retry = 0, int tries = 0, int broken = 0, string skipItem = "") => new()
    {
        To = 71,
        Puzzle = "needle",
        PuzzleArgs = new() { ["size"] = "6" },
        Fail = ["A_Alarm", "A_HurtPlayer(10)"],
        Cancel = ["A_Message(\"left\")"],
        Retry = retry,
        RetryMessage = "Not yet",
        Tries = tries,
        Broken = broken,
        SkipItem = skipItem,
    };

    [Test]
    public void A_Switch_With_No_Puzzle_Is_Thrown_As_Ever()
    {
        // Act
        bool puzzles = Use(new MapSwitchTranslation { To = 71 });

        // Assert
        Assert.That(puzzles, Is.False);
        Assert.That(_log, Is.Empty);
    }

    [Test]
    public void Its_Puzzle_Opens_And_Only_A_Success_Throws_It()
    {
        // Act
        bool puzzles = Use(Hack());
        var opened = _log.ToList();
        _ended!("success");

        // Assert
        Assert.That(puzzles, Is.True);
        Assert.That(opened, Is.EqualTo(new[] { $"open needle [size=6] {SwitchX},{SwitchY} {Tag}" }));
        Assert.That(_log.Last(), Is.EqualTo("thrown"));
    }

    [TestCase("fail", "actions A_Alarm A_HurtPlayer(10)")]
    [TestCase("cancel", "actions A_Message(\"left\")")]
    [TestCase("lights", null)]
    public void A_Puzzle_That_Isnt_Won_Runs_Its_Results_Actions_And_Throws_Nothing(string result, string? expected)
    {
        // Act
        Use(Hack());
        _log.Clear();
        _ended!(result);

        // Assert
        Assert.That(_log, Is.EqualTo(expected == null ? Array.Empty<string>() : new[] { expected }));
    }

    [Test]
    public void A_Fail_Keeps_It_Shut_Until_Its_Retry_Is_Up()
    {
        // Arrange: failed at time 100, retry 350
        _session.GameState.TimeCount = 100;
        Use(Hack(retry: 350));
        _ended!("fail");
        _log.Clear();

        // Act
        _session.GameState.TimeCount = 449;
        bool early = Use(Hack(retry: 350));
        var tooSoon = _log.ToList();
        _session.GameState.TimeCount = 450;
        Use(Hack(retry: 350));

        // Assert
        Assert.That(early, Is.True);
        Assert.That(tooSoon, Is.EqualTo(new[] { "message Not yet" }));
        Assert.That(_log.Last(), Does.StartWith("open needle"));
    }

    [Test]
    public void Its_Last_Try_Breaks_It_Into_Its_Broken_Wall()
    {
        // Act: two tries
        Use(Hack(tries: 2, broken: BrokenWall));
        _ended!("fail");
        int afterOne = _map.TileMap[SwitchX, SwitchY];
        Use(Hack(tries: 2, broken: BrokenWall));
        _ended!("fail");

        // Assert
        Assert.That(afterOne, Is.EqualTo(SwitchWall));
        Assert.That(_map.TileMap[SwitchX, SwitchY], Is.EqualTo(BrokenWall));
        Assert.That(_switches.States, Is.Empty);
    }

    [Test]
    public void With_No_Broken_Wall_Its_Last_Try_Shuts_It_For_Good()
    {
        // Arrange
        Use(Hack(tries: 1));
        _ended!("fail");
        _log.Clear();

        // Act
        _session.GameState.TimeCount = 1_000_000;
        Use(Hack(tries: 1));

        // Assert
        Assert.That(_log, Is.EqualTo(new[] { "message Not yet" }));
        Assert.That(_map.TileMap[SwitchX, SwitchY], Is.EqualTo(SwitchWall));
    }

    [Test]
    public void A_Success_Forgets_Its_Fails()
    {
        // Act
        Use(Hack(tries: 2, broken: BrokenWall));
        _ended!("fail");
        Use(Hack(tries: 2, broken: BrokenWall));
        _ended!("success");
        Use(Hack(tries: 2, broken: BrokenWall));
        _ended!("fail");

        // Assert: one fail since the success, so it isn't broken
        Assert.That(_map.TileMap[SwitchX, SwitchY], Is.EqualTo(SwitchWall));
    }

    [Test]
    public void A_Skip_Item_Throws_It_With_No_Puzzle_And_Is_Used_Up()
    {
        // Arrange
        _inventory.Give("AutoHack", 1);

        // Act
        Use(Hack(skipItem: "AutoHack"));
        var first = _log.ToList();
        _log.Clear();
        Use(Hack(skipItem: "AutoHack"));

        // Assert
        Assert.That(first, Is.EqualTo(new[] { "thrown" }));
        Assert.That(_inventory.Has("AutoHack"), Is.False);
        Assert.That(_log.Single(), Does.StartWith("open needle"));
    }

    [Test]
    public void Its_Fails_And_Lockout_Go_Into_A_Save()
    {
        // Arrange: one fail of three, locked out till 500
        _session.GameState.TimeCount = 150;
        Use(Hack(retry: 350, tries: 3));
        _ended!("fail");

        // Act
        using var stream = new MemoryStream();
        using (var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            PlayerSaveState.Capture(_session.PlayerState, [], [], _switches.States).Write(bw);
        stream.Position = 0;
        var saved = PlayerSaveState.Read(new BinaryReader(stream));
        _switches.Restore([]);
        _switches.Restore(saved.PuzzleSwitches);

        // Assert
        var state = _switches.States.Single();
        Assert.That((state.X, state.Y, state.Patch, state.Fails, state.ReadyAt), Is.EqualTo((SwitchX, SwitchY, false, 1, 500)));
    }
}
