using PFWolf.Assets;
using PFWolf.Gameplay;

namespace PFWolf.Tests;

public class StatValuesTests
{
    // Two maps in cluster 1, one in cluster 2; MAP02 is being played
    private static readonly Dictionary<string, MapInfo> Maps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MAP01"] = new() { Cluster = 1 },
        ["MAP02"] = new() { Cluster = 1 },
        ["MAP09"] = new() { Cluster = 2 },
    };

    private GameState _game = null!;
    private Dictionary<string, ObjectiveCount> _objectives = null!;
    private Dictionary<string, LevelRatio> _finished = null!;
    private List<LevelResult> _records = null!;
    private StatValues _stats = null!;

    [SetUp]
    public void MakeGame()
    {
        _game = new GameState { MapOn = "MAP02", Cluster = 1, KillCount = 3, KillTotal = 10, SecretCount = 1, SecretTotal = 2, TimeCount = 70 * 30 };
        _objectives = new(StringComparer.OrdinalIgnoreCase) { ["intel"] = new ObjectiveCount { Done = 1, Total = 4 } };
        _finished = new(StringComparer.OrdinalIgnoreCase)
        {
            ["MAP01"] = new LevelRatio { KillCount = 5, KillTotal = 6, Time = 60, Objectives = { ["intel"] = (2, 2) } },
            ["MAP09"] = new LevelRatio { KillCount = 7, KillTotal = 7, Time = 90, Objectives = { ["intel"] = (3, 3) } },
            ["MAP02"] = new LevelRatio { KillCount = 99, KillTotal = 99 },      // an earlier run of the level being played
        };
        _records =
        [
            new LevelResult { Kills = 4, KillTotal = 6, Time = 700, Objectives = { ["intel"] = (2, 2) } },
            new LevelResult { Kills = 9, KillTotal = 10, Time = 1400 },
        ];
        _stats = new StatValues(new StatSources(() => _game, () => _objectives, () => _finished, () => Maps,
            name => name == "DogTag" ? (3, 9) : null, () => _records, map => $"Level {map}", tics => $"{tics / 70}s"));
    }

    [TestCase("kill", 3, 10)]
    [TestCase("level.kill", 3, 10)]
    [TestCase("secret", 1, 2)]
    [TestCase("objective.intel", 1, 4)]
    [TestCase("objective.none", 0, 0)]
    [TestCase("item.DogTag", 3, 9)]
    public void Level_Stats_Are_The_Level_As_It_Stands(string name, int done, int total)
    {
        Assert.That(_stats.Get(name), Is.EqualTo(new StatValue(done, total)));
    }

    [TestCase("episode.kill", 8, 16)]           // MAP01 and this level, not MAP09 (another cluster) nor MAP02's earlier run
    [TestCase("game.kill", 15, 23)]             // every finished level and this one
    [TestCase("game.objective.intel", 6, 9)]
    [TestCase("ever.kill", 13, 16)]             // the records alone
    [TestCase("ever.objective.intel", 2, 2)]
    public void Wider_Scopes_Add_Levels_Up(string name, int done, int total)
    {
        Assert.That(_stats.Get(name), Is.EqualTo(new StatValue(done, total)));
    }

    [TestCase("time", "30s")]
    [TestCase("episode.time", "90s")]           // MAP01's minute and this level's half
    [TestCase("ever.time", "30s")]              // records keep tics
    [TestCase("level", "Level MAP02")]
    public void Time_And_The_Level_Are_Text(string name, string text)
    {
        Assert.That(_stats.Get(name)?.Text, Is.EqualTo(text));
    }

    [TestCase("fly")]
    [TestCase("game.level.nonsense")]
    [TestCase("item.Nothing")]
    public void An_Unknown_Stat_Is_Nothing(string name)
    {
        Assert.That(_stats.Get(name), Is.Null);
    }

    [Test]
    public void Format_Fills_Places_And_Percent_Signs_From_The_Values_In_Order()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_stats.Format("K: %d/%d, S: %d/%d, I: %d/%d", ["kill", "secret", "objective.intel"]), Is.EqualTo("K: 3/10, S: 1/2, I: 1/4"));
            Assert.That(_stats.Format("Kills {0}/{1}", ["kill"]), Is.EqualTo("Kills 3/10"));
            Assert.That(_stats.Format("Time {0}", ["time"]), Is.EqualTo("Time 30s"));
            Assert.That(_stats.Format("{2} of {3}", ["kill", "item.DogTag"]), Is.EqualTo("3 of 9"));
            Assert.That(_stats.Format("Unknown: %d, %d; past the end: {5}", ["fly"]), Is.EqualTo("Unknown: 0, 0; past the end: "));
        });
    }
}
