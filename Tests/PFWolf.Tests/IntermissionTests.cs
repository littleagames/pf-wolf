using PFWolf.Assets;

namespace PFWolf.Tests;

public class IntermissionTests
{
    [Test]
    public void Ratios_Are_Percentages_Rounded_Down()
    {
        // Arrange
        var game = new GameState { KillCount = 2, KillTotal = 3, SecretCount = 1, SecretTotal = 1, TreasureCount = 0, TreasureTotal = 8 };

        // Act / Assert
        Assert.That(IntermissionScreen.Ratios(game), Is.EqualTo((66, 100, 0)));
    }

    [Test]
    public void A_Level_With_None_Of_Something_Rates_It_0()
    {
        Assert.That(IntermissionScreen.Ratios(new GameState()), Is.EqualTo((0, 0, 0)));
    }

    // Midnight Assault's extra lines: intel (a percentage), stealth kills, Kilroys (done/total)
    private static readonly Dictionary<string, IntermissionValue> ExtraValues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kill"] = new(),
        ["objective.intel"] = new(),
        ["stealth"] = new(),
        ["objective.kilroys"] = new() { Count = true },
    };

    private static (int Done, int Total)? Objectives(string name) => name switch
    {
        "intel" => (3, 4),
        "kilroys" => (1, 2),
        _ => null,
    };

    [Test]
    public void Objectives_And_Stealth_Are_Tallied_In_Their_Order()
    {
        // Arrange: finished unseen, two stealth kills of five
        var player = new Entities.PlayerState { Undetected = true, StealthKills = 2 };
        var game = new GameState { KillCount = 2, KillTotal = 5 };

        // Act
        var tallies = IntermissionScreen.ExtraTallies(ExtraValues, Objectives, player, game);

        // Assert
        Assert.That(tallies.Select(t => (t.Name, t.Target, t.Text(t.Target))), Is.EqualTo(new[]
        {
            ("objective.intel", 75, "75"),
            ("stealth", 40, "40"),
            ("objective.kilroys", 1, "1/2"),
        }));
    }

    [TestCase(true, 0, 0, 5, true)]         // unseen, nobody killed: shown (0%)
    [TestCase(true, 0, 3, 5, false)]        // unseen, but killed without stealth
    [TestCase(false, 2, 2, 5, false)]       // seen
    [TestCase(true, 0, 0, 0, false)]        // nobody there to kill
    public void Stealth_Kills_Show_Only_For_A_Level_Done_Unseen(bool undetected, int stealthKills, int kills, int total, bool shown)
    {
        var player = new Entities.PlayerState { Undetected = undetected, StealthKills = stealthKills };
        var game = new GameState { KillCount = (short)kills, KillTotal = (short)total };
        var tallies = IntermissionScreen.ExtraTallies(ExtraValues, Objectives, player, game);
        Assert.That(tallies.Any(t => t.Name == "stealth"), Is.EqualTo(shown));
    }

    [Test]
    public void A_Percentage_With_Nothing_To_Do_Is_100_And_A_Count_Of_Nothing_Isnt_Shown()
    {
        var tallies = IntermissionScreen.ExtraTallies(ExtraValues, _ => null, new Entities.PlayerState(), new GameState());
        Assert.That(tallies.Select(t => (t.Name, t.Target)), Is.EqualTo(new[] { ("objective.intel", 100) }));
    }

    [TestCase(0, 0, 0, 0, 0)]
    [TestCase(30, 99, 99, 99, 3000)]            // 30 seconds under par at 100 a second
    [TestCase(0, 100, 50, 99, 10000)]           // one perfect ratio
    [TestCase(10, 100, 100, 100, 31000)]        // all three, and time
    public void The_Bonus_Is_Time_Left_And_Perfect_Ratios(int timeleft, int kr, int sr, int tr, int expected)
    {
        Assert.That(IntermissionScreen.LevelBonus(timeleft, 100, 10000, kr, sr, tr), Is.EqualTo(expected));
    }

    private static LevelRatio Floor(short kill, short secret, short treasure, int time) =>
        new() { Kill = kill, Secret = secret, Treasure = treasure, Time = time };

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
        var ratios = new Dictionary<string, LevelRatio>
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
        var ratios = new Dictionary<string, LevelRatio> { ["E1M1"] = Floor(0, 0, 0, 100 * 60 + 5) };

        // Act / Assert
        Assert.That(IntermissionScreen.ClusterTotals(ratios, Maps, 1), Is.EqualTo((99, 99, 0, 0, 0)));
    }

    [Test]
    public void The_Victory_Tallies_Average_Or_Add_Up_The_Floors_Objectives_And_Count_Items()
    {
        // Arrange: intel half done on one floor, none on the other (100%); 2 of 3 Kilroys and 1 of 2;
        // 4 of 9 dog tags held
        var values = new Dictionary<string, IntermissionValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["objective.intel"] = new(),
            ["objective.kilroys"] = new() { Count = true },
            ["objective.sabotage"] = new() { Count = true },
            ["item.DogTag"] = new() { Count = true },
            ["item.Nothing"] = new() { Count = true },
        };
        var floors = new List<LevelRatio>
        {
            new() { Objectives = { ["intel"] = (1, 2), ["kilroys"] = (2, 3) } },
            new() { Objectives = { ["kilroys"] = (1, 2) } },
        };

        // Act
        var tallies = IntermissionScreen.VictoryTallies(values, floors, name => name == "DogTag" ? (4, 9) : null);

        // Assert: no sabotage anywhere, and no such item, isn't shown
        Assert.That(tallies.Select(t => (t.Name, t.Text(t.Target))), Is.EqualTo(new[]
        {
            ("objective.intel", "75"),
            ("objective.kilroys", "3/5"),
            ("item.DogTag", "4/9"),
        }));
    }

    [Test]
    public void A_Level_Ratio_Keeps_Its_Objectives_In_A_Save()
    {
        var ratio = new LevelRatio { Kill = 50, Secret = 25, Treasure = 10, Time = 99, Objectives = { ["intel"] = (3, 4) } };
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            ratio.Write(bw);
        ms.Position = 0;
        var back = LevelRatio.Read(new BinaryReader(ms));

        Assert.That((back.Kill, back.Secret, back.Treasure, back.Time), Is.EqualTo(((short)50, (short)25, (short)10, 99)));
        Assert.That(back.Objectives["INTEL"], Is.EqualTo((3, 4)));
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
        new() { Name = "A", Score = 5000, Completed = 3 },
        new() { Name = "B", Score = 3000, Completed = 2 },
        new() { Name = "C", Score = 1000, Completed = 1 },
    ];

    [Test]
    public void A_Score_Goes_In_Its_Place_And_The_Last_Falls_Off()
    {
        // Arrange
        var table = Table();

        // Act
        int row = HighScore.Insert(table, new HighScore { Name = "New", Score = 4000 });

        // Assert
        Assert.That(row, Is.EqualTo(1));
        Assert.That(table.Select(s => s.Name), Is.EqualTo(new[] { "A", "New", "B" }));
    }

    [Test]
    public void A_Score_Too_Low_Doesnt_Make_It()
    {
        // Arrange
        var table = Table();

        // Act / Assert
        Assert.That(HighScore.Insert(table, new HighScore { Score = 500 }), Is.EqualTo(-1));
        Assert.That(table.Select(s => s.Name), Is.EqualTo(new[] { "A", "B", "C" }));
    }

    [Test]
    public void A_Tie_Goes_To_Whoever_Got_Further()
    {
        // Arrange
        var table = Table();

        // Act
        int further = HighScore.Insert(table, new HighScore { Name = "Further", Score = 3000, Completed = 5 });
        int same = HighScore.Insert(Table(), new HighScore { Score = 3000, Completed = 2 });

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
        Assert.That((score.Score, (int)score.Completed, (int)score.Episode, (int)score.Ratio), Is.EqualTo((1234, completed, episode, ratio)));
    }

}

/// <summary>The level-end screen's pictures, stars and stealth line (Midnight Assault's)</summary>
public class LevelEndScreenTests
{
    [Test]
    public void BJ_Breathes_Through_The_Levels_Own_Pictures_Where_It_Has_Them()
    {
        // Arrange: Midnight Assault's photos of a level, in place of BJ's own
        var bj = new IntermissionBj { Pics = ["L_GUY", "L_GUY2"] };
        var photos = new MapInfo { IntermissionPics = ["L_CAMP01_1", "L_CAMP01_2", "L_CAMP01_3"] };

        // Act / Assert
        Assert.That(IntermissionScreen.BreathPics(bj, photos), Is.EqualTo(new[] { "L_CAMP01_1", "L_CAMP01_2", "L_CAMP01_3" }));
        Assert.That(IntermissionScreen.BreathPics(bj, new MapInfo()), Is.EqualTo(new[] { "L_GUY", "L_GUY2" }));
        Assert.That(IntermissionScreen.BreathPics(null, photos), Is.Empty);      // no bj: nowhere to show them
    }

    [Test]
    public void The_Backdrop_Photos_And_Colors_Read_From_Their_Files()
    {
        // Act
        var screen = Loaders.YamlDataEntryLoader.Deserialize<IntermissionAsset>("""
            color: Black
            value-color: "#A01414"
            value-background-pic: L_BACKDROP
            pics:
              - { pic: L_BACKDROP, x: 0, y: 0 }
            bj: { x: 37, y: 50, breath-tics: 25 }
            labels:
              - { text: "%", x: 270, y: 102, color: "#A01414" }
            """);
        var gameInfo = Loaders.YamlDataEntryLoader.Deserialize<GameInfoAsset>("""
            maps:
              CAMP01: { intermission-pics: [L_CAMP01_1, L_CAMP01_2, L_CAMP01_3] }
            """);

        // Assert
        Assert.That((screen.ValueColor, screen.ValueBackgroundPic), Is.EqualTo(("#A01414", "L_BACKDROP")));
        Assert.That(screen.Pics!.Select(p => p.Pic), Is.EqualTo(new[] { "L_BACKDROP" }));
        Assert.That((screen.Bj!.X, screen.Bj.Y, screen.Bj.BreathTics), Is.EqualTo((37, 50, 25)));
        Assert.That(screen.Labels![0].Color, Is.EqualTo("#A01414"));
        Assert.That(gameInfo.Maps["CAMP01"].IntermissionPics, Has.Count.EqualTo(3));
    }

    [TestCase(100, 40, "Gold")]
    [TestCase(60, 40, "Silver")]     // bettered the level's best
    [TestCase(40, 40, "None")]
    [TestCase(10, null, "Silver")]   // never finished before: anything bettered it
    [TestCase(0, null, "None")]
    public void A_Ratio_Earns_Gold_At_100_And_Silver_For_A_New_Best(int percent, int? before, string star)
    {
        Assert.That(IntermissionScreen.RatioStar(percent, before).ToString(), Is.EqualTo(star));
    }

    [Test]
    public void The_Time_Earns_Gold_Under_Par_And_Silver_When_Quicker_Than_Ever()
    {
        var best = new Gameplay.LevelResult { Time = 700 };

        Assert.That(IntermissionScreen.TimeStar(underPar: true, 9999, best), Is.EqualTo(IntermissionScreen.Star.Gold));
        Assert.That(IntermissionScreen.TimeStar(underPar: false, 600, best), Is.EqualTo(IntermissionScreen.Star.Silver));
        Assert.That(IntermissionScreen.TimeStar(underPar: false, 800, best), Is.EqualTo(IntermissionScreen.Star.None));
        Assert.That(IntermissionScreen.TimeStar(underPar: false, 800, null), Is.EqualTo(IntermissionScreen.Star.Silver));
    }

    [Test]
    public void A_Records_Percentages_Are_As_The_Tally_Shows_Them()
    {
        // Arrange
        var record = new Gameplay.LevelResult
        {
            Kills = 3, KillTotal = 4, Secrets = 0, SecretTotal = 0,
            Objectives = new(StringComparer.OrdinalIgnoreCase) { ["intel"] = (1, 2), ["kilroys"] = (0, 0) },
        };

        // Act / Assert: none of a ratio's 0, an objective with none to do 100
        Assert.That(IntermissionScreen.RecordPercent(record, "kill"), Is.EqualTo(75));
        Assert.That(IntermissionScreen.RecordPercent(record, "secret"), Is.EqualTo(0));
        Assert.That(IntermissionScreen.RecordPercent(record, "objective.intel"), Is.EqualTo(50));
        Assert.That(IntermissionScreen.RecordPercent(record, "objective.kilroys"), Is.EqualTo(100));
        Assert.That(IntermissionScreen.RecordPercent(null, "kill"), Is.Null);
    }

    private static readonly IntermissionStealthBonus Bonus = new() { AllKills = "all", NoKills = "none", SomeKills = "some" };

    [TestCase(true, 5, 5, 5, "all")]      // every enemy killed unseen
    [TestCase(true, 0, 0, 5, "none")]     // none killed
    [TestCase(true, 2, 2, 5, "some")]     // every kill unseen, some left
    [TestCase(true, 1, 2, 5, null)]       // a kill that wasn't unseen
    [TestCase(false, 0, 0, 5, null)]      // seen
    [TestCase(true, 0, 0, 0, null)]       // nobody to kill: no stealth tally
    public void The_Stealth_Bonus_Line_Is_The_Originals(bool undetected, int stealthKills, int kills, int killTotal, string? line)
    {
        // Arrange
        var player = new Entities.PlayerState { Undetected = undetected, StealthKills = stealthKills };
        var game = new GameState { KillCount = (short)kills, KillTotal = (short)killTotal };

        // Act / Assert
        Assert.That(IntermissionScreen.StealthBonusText(Bonus, player, game), Is.EqualTo(line));
    }

    [Test]
    public void Only_An_Unseen_Run_Without_Kills_Of_Some_There_Is_An_Infiltration()
    {
        Assert.That(IntermissionScreen.UnseenNoKills(new Entities.PlayerState { Undetected = true }, new GameState { KillTotal = 3 }), Is.True);
        Assert.That(IntermissionScreen.UnseenNoKills(new Entities.PlayerState { Undetected = true }, new GameState { KillCount = 1, KillTotal = 3 }), Is.False);
        Assert.That(IntermissionScreen.UnseenNoKills(new Entities.PlayerState { Undetected = false }, new GameState { KillTotal = 3 }), Is.False);
    }

    [Test]
    public void The_Stars_Dog_Tags_And_Stealth_Line_Read_From_Their_Files()
    {
        // Act
        var screen = Loaders.YamlDataEntryLoader.Deserialize<IntermissionAsset>("""
            stars:
              gold: L_GOLDSTAR
              silver: L_SILVERSTAR
              at:
                time: { x: 278, y: 85 }
                objective.intel: { x: 278, y: 127 }
            unlock-pics:
              unlocks: [DT01, DT02]
              unlocked: L_DT_FOUND
              locked: L_DT_NOTFOUND
              x: 152
              y: 40
              step-x: 13
              clusters: [1]
            stealth-bonus:
              y: 188
              sound: misc/stealth_bonus
              no-kills: "$STR_STEALTHNOKILLS"
            """);
        var gameInfo = Loaders.YamlDataEntryLoader.Deserialize<GameInfoAsset>("stealth: { unseen-no-kills-counts-all: true }");

        // Assert
        Assert.That(screen.Stars!.At["objective.intel"], Is.EqualTo(new IntermissionPlace { X = 278, Y = 127 }));
        Assert.That((screen.UnlockPics!.Unlocks.Count, screen.UnlockPics.StepX, screen.UnlockPics.Clusters.Single()), Is.EqualTo((2, 13, 1)));
        Assert.That((screen.StealthBonus!.Y, screen.StealthBonus.Sound, screen.StealthBonus.NoKills), Is.EqualTo((188, "misc/stealth_bonus", "$STR_STEALTHNOKILLS")));
        Assert.That(gameInfo.Stealth.UnseenNoKillsCountsAll, Is.True);
    }
}
