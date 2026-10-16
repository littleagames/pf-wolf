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

    private static LRstruct Floor(short kill, short secret, short treasure, int time) =>
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
        var ratios = new Dictionary<string, LRstruct>
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
        var ratios = new Dictionary<string, LRstruct> { ["E1M1"] = Floor(0, 0, 0, 100 * 60 + 5) };

        // Act / Assert
        Assert.That(IntermissionScreen.ClusterTotals(ratios, Maps, 1), Is.EqualTo((99, 99, 0, 0, 0)));
    }

    [Test]
    public void A_Value_With_No_Format_Is_Drawn_As_It_Is()
    {
        Assert.That(IntermissionScreen.FormatValue(new IntermissionValue(), "Ann", _ => "unused"), Is.EqualTo("Ann"));
    }

    [Test]
    public void A_Values_Format_Is_Looked_Up_And_Takes_The_Value_Where_Its_Placeholder_Is()
    {
        // Arrange
        var value = new IntermissionValue { Format = "$STR_MAPBY" };
        string Localize(string key) => key == "$STR_MAPBY" ? "Map by {0}." : key;

        // Act / Assert
        Assert.That(IntermissionScreen.FormatValue(value, "Ann", Localize), Is.EqualTo("Map by Ann."));
    }

    [Test]
    public void A_Format_Without_A_Placeholder_Goes_Before_The_Value()
    {
        var value = new IntermissionValue { Format = "By " };
        Assert.That(IntermissionScreen.FormatValue(value, "Ann", f => f), Is.EqualTo("By Ann"));
    }

    [TestCase(10, null, null, 10)]
    [TestCase(10, 200, null, 160)]      // right edge at 200
    [TestCase(10, null, 160, 140)]      // middle at 160
    public void A_Value_Starts_From_X_Back_From_Right_Or_Either_Side_Of_Center(int x, int? right, int? center, int start)
    {
        Assert.That(IntermissionScreen.ValueX(new IntermissionValue { X = x, Right = right, Center = center }, 40), Is.EqualTo(start));
    }

    [Test]
    public void A_Map_Can_Name_Its_Author()
    {
        // Arrange
        var yaml = "name: \"The Bunker\"\nauthor: \"Ann Example\"\nnext: MAP02\n";

        // Act: as game-info.yaml is read
        var map = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<MapInfo>(yaml);

        // Assert
        Assert.That((map.Name, map.Author), Is.EqualTo(("The Bunker", "Ann Example")));
        Assert.That(new MapInfo().Author, Is.Null);         // optional
    }

    [TestCase("new", false, false, false, true)]
    [TestCase("restarted", false, false, false, false)]     // after dying
    [TestCase("loaded", false, false, false, false)]
    [TestCase("returned", false, false, false, false)]      // a kept hub level
    [TestCase("new", true, false, false, false)]            // switched to by A_ChangeMap
    [TestCase("new", false, true, false, false)]            // a demo
    [TestCase("new", false, false, true, false)]            // a game with others
    public void A_Briefing_Shows_Only_For_A_Level_Entered_Afresh(string levelStart, bool warped, bool demo, bool net, bool shows)
    {
        Assert.That(MainGameLoop.ShowsBriefing(levelStart, warped, demo, net), Is.EqualTo(shows));
    }

    [Test]
    public void A_Map_Can_Have_A_Briefing_And_A_Debriefing()
    {
        // Act: as game-info.yaml is read
        var map = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<MapInfo>("briefing: \"End Art-258\"\ndebriefing: \"End Art-259\"\n");

        // Assert
        Assert.That((map.Briefing, map.Debriefing), Is.EqualTo(("End Art-258", "End Art-259")));
        Assert.That(new MapInfo().Briefing, Is.Null);
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
