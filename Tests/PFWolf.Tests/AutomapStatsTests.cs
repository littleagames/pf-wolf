using PFWolf.Assets;
using PFWolf.Gameplay;

namespace PFWolf.Tests;

public class AutomapStatsTests
{
    private GameState _game = null!;
    private Dictionary<string, ObjectiveCount> _objectives = null!;
    private StatValues _stats = null!;

    [SetUp]
    public void MakeGame()
    {
        _game = new GameState { MapOn = "MAP01", KillCount = 2, KillTotal = 5, TreasureCount = 1, TreasureTotal = 3, SecretCount = 0, SecretTotal = 1, TimeCount = 70 };
        _objectives = new(StringComparer.OrdinalIgnoreCase)
        {
            ["sabotage"] = new ObjectiveCount { Done = 1, Total = 3 },
            ["targets"] = new ObjectiveCount { Done = 0, Total = 1 },
        };
        _stats = new StatValues(new StatSources(() => _game, () => _objectives, () => new Dictionary<string, LevelRatio>(),
            () => new Dictionary<string, MapInfo>(), name => name == "DogTag" ? (2, 9) : null, () => [], map => "Jungle Shore", tics => "0:01"));
    }

    private static readonly Dictionary<string, string> Language = new()
    {
        ["$STR_AM_KILLS"] = "Kills {0}/{1}",
        ["$STR_AM_TREASURE"] = "Treasure {0}/{1}",
        ["$STR_AM_SECRETS"] = "Secrets {0}/{1}",
        ["$STR_AM_TIME"] = "Time {0}",
    };

    private List<(string Text, string? Color)> Lines(IEnumerable<AutomapStatsLine> lines, Func<string, bool>? inEpisode = null) =>
        AutomapStats.Lines(lines, _stats, text => Language.GetValueOrDefault(text, text), inEpisode ?? (_ => false),
            () => ["Sabotages: 1 of 3"]);

    [Test]
    public void The_Default_Lines_Are_The_Automaps_Own()
    {
        // Act
        var lines = Lines(new GameInfoAsset().AutomapStatsLines);

        // Assert: as the automap always drew them
        Assert.That(lines, Is.EqualTo(new (string, string?)[]
        {
            ("Jungle Shore", "AutomapTitle"),
            ("Kills 2/5", null),
            ("Treasure 1/3", null),
            ("Secrets 0/1", null),
            ("Sabotages: 1 of 3", null),
            ("Time 0:01", null),
        }));
    }

    [Test]
    public void Only_With_Leaves_A_Line_Out_Without_Enough_Of_Its_Stat()
    {
        // Arrange: Midnight Assault's target enemies only with two or more; Kilroys only with some
        AutomapStatsLine[] lines =
        [
            new() { Text = "Targets %d of %d", Values = ["objective.targets"], MinTotal = 2 },
            new() { Text = "Kilroys %d of %d", Values = ["objective.kilroys"], OnlyWith = "objective.kilroys" },
            new() { Text = "Sabotages %d of %d", Values = ["objective.sabotage"], OnlyWith = "objective.sabotage" },
        ];

        // Act / Assert
        Assert.That(Lines(lines).Select(l => l.Text), Is.EqualTo(new[] { "Sabotages 1 of 3" }));
    }

    [Test]
    public void A_Line_For_Some_Episodes_Shows_Only_In_Them()
    {
        // Arrange
        AutomapStatsLine[] lines = [new() { Text = "Dog tags: %d of %d", Values = ["item.DogTag"], Episodes = ["EP01"], Color = "Green" }];

        // Act / Assert
        Assert.That(Lines(lines, id => id == "EP01"), Is.EqualTo(new (string, string?)[] { ("Dog tags: 2 of 9", "Green") }));
        Assert.That(Lines(lines, id => id == "EP02"), Is.Empty);
    }
}
