using PFWolf.Assets;

namespace PFWolf.Tests;

public class IntermissionTests
{
    [Test]
    public void Ratios_Are_Percentages_Rounded_Down()
    {
        // Arrange
        var game = new gametype { killcount = 2, killtotal = 3, secretcount = 1, secrettotal = 1, treasurecount = 0, treasuretotal = 8 };

        // Act / Assert
        Assert.That(IntermissionScreen.Ratios(game), Is.EqualTo((66, 100, 0)));
    }

    [Test]
    public void A_Level_With_None_Of_Something_Rates_It_0()
    {
        Assert.That(IntermissionScreen.Ratios(new gametype()), Is.EqualTo((0, 0, 0)));
    }

    [TestCase(0, 0, 0, 0, 0)]
    [TestCase(30, 99, 99, 99, 3000)]            // 30 seconds under par at 100 a second
    [TestCase(0, 100, 50, 99, 10000)]           // one perfect ratio
    [TestCase(10, 100, 100, 100, 31000)]        // all three, and time
    public void The_Bonus_Is_Time_Left_And_Perfect_Ratios(int timeleft, int kr, int sr, int tr, int expected)
    {
        Assert.That(IntermissionScreen.LevelBonus(timeleft, 100, 10000, kr, sr, tr), Is.EqualTo(expected));
    }

    private static Program.LRstruct Floor(short kill, short secret, short treasure, int time) =>
        new() { kill = kill, secret = secret, treasure = treasure, time = time };

    private static readonly Dictionary<string, MapInfo> Maps = new()
    {
        ["E1M1"] = new MapInfo { Cluster = 1 },
        ["E1M2"] = new MapInfo { Cluster = 1 },
        ["E2M1"] = new MapInfo { Cluster = 2 },
    };

    [Test]
    public void The_Victory_Tally_Averages_The_Clusters_Floors_Played()
    {
        // Arrange: another cluster's floor doesn't count
        var ratios = new Dictionary<string, Program.LRstruct>
        {
            ["E1M1"] = Floor(100, 50, 80, 90),
            ["E1M2"] = Floor(50, 0, 21, 75),
            ["E2M1"] = Floor(0, 0, 0, 600),
        };

        // Act / Assert: 165 seconds is 2:45
        Assert.That(IntermissionScreen.ClusterTotals(ratios, Maps, 1), Is.EqualTo((2, 45, 75, 25, 50)));
    }

    [Test]
    public void No_Floors_Played_Is_Nothing()
    {
        Assert.That(IntermissionScreen.ClusterTotals([], Maps, 1), Is.EqualTo((0, 0, 0, 0, 0)));
    }

    [Test]
    public void The_Time_Stops_At_99_99()
    {
        // Arrange
        var ratios = new Dictionary<string, Program.LRstruct> { ["E1M1"] = Floor(0, 0, 0, 100 * 60 + 5) };

        // Act / Assert
        Assert.That(IntermissionScreen.ClusterTotals(ratios, Maps, 1), Is.EqualTo((99, 99, 0, 0, 0)));
    }

    [Test]
    public void A_Won_Clusters_Fade_Color_Is_Its_Own_Or_Black()
    {
        Assert.That(IntermissionScreen.VictoryFadeColor(new ClusterInfo()).Alpha, Is.EqualTo(255));
        Assert.That(IntermissionScreen.VictoryFadeColor(new ClusterInfo { VictoryFadeColor = "#FF0000" }).Red, Is.EqualTo(255));
    }
}

public class HighScoreRankingTests
{
    private static HighScore[] Table() =>
    [
        new() { name = "A", score = 5000, completed = 3 },
        new() { name = "B", score = 3000, completed = 2 },
        new() { name = "C", score = 1000, completed = 1 },
    ];

    [Test]
    public void A_Score_Goes_In_Its_Place_And_The_Last_Falls_Off()
    {
        // Arrange
        var table = Table();

        // Act
        int row = HighScore.Insert(table, new HighScore { name = "New", score = 4000 });

        // Assert
        Assert.That(row, Is.EqualTo(1));
        Assert.That(table.Select(s => s.name), Is.EqualTo(new[] { "A", "New", "B" }));
    }

    [Test]
    public void A_Score_Too_Low_Doesnt_Make_It()
    {
        // Arrange
        var table = Table();

        // Act / Assert
        Assert.That(HighScore.Insert(table, new HighScore { score = 500 }), Is.EqualTo(-1));
        Assert.That(table.Select(s => s.name), Is.EqualTo(new[] { "A", "B", "C" }));
    }

    [Test]
    public void A_Tie_Goes_To_Whoever_Got_Further()
    {
        // Arrange
        var table = Table();

        // Act
        int further = HighScore.Insert(table, new HighScore { name = "Further", score = 3000, completed = 5 });
        int same = HighScore.Insert(Table(), new HighScore { score = 3000, completed = 2 });

        // Assert: a tie that got no further stays below
        Assert.That(further, Is.EqualTo(1));
        Assert.That(same, Is.EqualTo(2));
    }

    [Test]
    public void A_Won_Game_Ranks_Above_The_Same_Score_That_Wasnt()
    {
        // Arrange
        var table = Table();
        var won = HighScoresScreen.NewScore(3000, won: true, floor: 2, cluster: 1, floorScore: 0);

        // Act / Assert
        Assert.That(HighScore.Insert(table, won), Is.EqualTo(1));
    }

    [TestCase(9, true, 3, 300, 9 | HighScore.Won, 2, 100)]
    [TestCase(4, false, 1, 150, 4, 0, 50)]
    [TestCase(1, false, 0, 999, 1, 0, 100)]     // cluster 0 is episode 1, the rating tops out at 100
    public void A_New_Score_Records_The_Floor_Episode_And_Rating(int floor, bool won, int cluster, int floorScore,
        int completed, int episode, int ratio)
    {
        // Act
        var score = HighScoresScreen.NewScore(1234, won, floor, cluster, floorScore);

        // Assert
        Assert.That((score.score, (int)score.completed, (int)score.episode, (int)score.ratio), Is.EqualTo((1234, completed, episode, ratio)));
    }
}
