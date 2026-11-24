using PFWolf.Assets;

namespace PFWolf.Tests;

/// <summary>
/// Game-info level-bonuses (Gameplay/LevelBonuses.cs): Blake Stone's pinball bonuses -- the floor's
/// all enemies destroyed, all points collected and then all informants alive, the score reaching
/// 500,000 and rolling over, an extra life, a guardian alien killed -- checked as the score goes up,
/// each given once (unless it recurs), their messages one at a time
/// </summary>
public class LevelBonusesTests
{
    private GameSession _session = null!;
    private Dictionary<string, (int, int)> _stats = null!;
    private List<string> _shown = null!;
    private List<string> _sounds = null!;
    private List<string> _given = null!;
    private int _redraws;
    private LevelBonusesInfo _settings = null!;
    private LevelBonuses _bonuses = null!;

    private static readonly LevelBonusInfo[] FloorBonuses =
    [
        new() { When = "kills", Points = 50000, Message = "ENEMY" },
        new() { When = "points", Points = 50000, Message = "POINTS" },
        new() { When = "informants", NeedsSome = true, After = ["kills", "points"], Points = 50000, Message = "INFORMANTS" },
    ];

    // Blake Stone's whole table, in bstone's order
    private static readonly LevelBonusInfo[] AllBonuses =
    [
        new() { When = "killed", Actors = ["SpiderMutant", "AcidDragon"], Message = "GUARDIAN", ClusterMessages = { [6] = "GENERATORS" } },
        new()
        {
            When = "score", At = 10000000, Recurs = true, Points = 1000000, Health = 99, Give = { ["ChargeUnit"] = 99 },
            Message = "ROLLED", ScoreText = "-ROLL-", ScoreTextTics = 600,
        },
        new() { When = "score", At = 500000, Points = 1000000, Health = 99, Give = { ["ChargeUnit"] = 99 }, Message = "GREAT" },
        new() { When = "extra-life", Recurs = true, Message = "EXTRA", Sound = "bs/extra_man" },
        .. FloorBonuses,
    ];

    [SetUp]
    public void Make()
    {
        _session = new GameSession();
        _session.GameState.MapOn = "MAP02";
        _stats = new(StringComparer.OrdinalIgnoreCase)
        {
            ["kills"] = (0, 10),
            ["points"] = (0, 5000),
            ["informants"] = (2, 2),
        };
        _shown = [];
        _sounds = [];
        _given = [];
        _redraws = 0;
        _settings = new LevelBonusesInfo { Sound = "bs/roll_score", Style = "Bonus", Bonuses = [.. FloorBonuses] };
        _bonuses = new LevelBonuses(_session, new LevelBonusHooks(
            () => _settings,
            when => _stats.TryGetValue(when, out var stat) ? stat : null,
            (player, points) => player.Score += points,
            (_, health) => _given.Add($"health {health}"),
            (_, item, amount) => _given.Add($"{item} {amount}"),
            (message, style) => _shown.Add($"{message} ({style})"),
            _sounds.Add,
            _ => 420,
            () => _redraws++));
        _bonuses.Start();
    }

    private void UseAllBonuses()
    {
        _settings = _settings with { Bonuses = [.. AllBonuses] };
        _bonuses.Start();
    }

    // Points scored this tic (a kill or a pickup), then the tic's end
    private void Score(int points, int tics = 1)
    {
        _session.PlayerState.Score += points;
        _bonuses.Tick(tics);
    }

    private void Tics(int count)
    {
        for (int tic = 0; tic < count; tic++)
            _bonuses.Tick(1);
    }

    [Test]
    public void The_Last_Kill_Gives_The_Enemy_Bonus_With_Its_Message_And_Sound()
    {
        // Arrange
        _stats["kills"] = (10, 10);

        // Act
        Score(500);

        // Assert
        Assert.That(_session.PlayerState.Score, Is.EqualTo(500 + 50000));
        Assert.That(_shown, Is.EqualTo(new[] { "ENEMY (Bonus)" }));
        Assert.That(_sounds, Is.EqualTo(new[] { "bs/roll_score" }));
    }

    [Test]
    public void Nothing_Is_Checked_Until_The_Score_Goes_Up()
    {
        // Arrange: all killed, but by nothing that scored (as bstone checks in GivePoints)
        _stats["kills"] = (10, 10);

        // Act
        _bonuses.Tick(1);

        // Assert
        Assert.That(_shown, Is.Empty);
        Assert.That(_session.PlayerState.Score, Is.Zero);
    }

    [Test]
    public void A_Bonus_Is_Given_Once()
    {
        // Arrange
        _stats["kills"] = (10, 10);
        Score(500);

        // Act
        Score(500, tics: 500);

        // Assert
        Assert.That(_session.PlayerState.Score, Is.EqualTo(1000 + 50000));
        Assert.That(_shown, Has.Count.EqualTo(1));
    }

    [Test]
    public void Two_At_Once_Are_Given_Together_But_Shown_One_After_The_Other()
    {
        // Arrange: the last enemy was the last of the points too
        _stats["kills"] = (10, 10);
        _stats["points"] = (5000, 5000);

        // Act
        Score(500);
        int scoreAtOnce = _session.PlayerState.Score;
        var shownAtOnce = _shown.ToList();
        Tics(419);
        var shownBeforeTimeUp = _shown.ToList();
        _bonuses.Tick(1);

        // Assert: the informants' bonus comes after the other two (all alive)
        Assert.That(scoreAtOnce, Is.EqualTo(500 + 3 * 50000));
        Assert.That(shownAtOnce, Is.EqualTo(new[] { "ENEMY (Bonus)" }));
        Assert.That(shownBeforeTimeUp, Has.Count.EqualTo(1));
        Assert.That(_shown, Is.EqualTo(new[] { "ENEMY (Bonus)", "POINTS (Bonus)" }));

        Tics(420);
        Assert.That(_shown.Last(), Is.EqualTo("INFORMANTS (Bonus)"));
    }

    [Test]
    public void The_Informants_Bonus_Waits_For_The_Enemy_And_Points_Bonuses()
    {
        // Arrange: informants all alive, enemies left
        _stats["points"] = (5000, 5000);

        // Act
        Score(100);

        // Assert
        Assert.That(_session.PlayerState.Score, Is.EqualTo(100 + 50000));
        Assert.That(_bonuses.Given, Is.EquivalentTo(new[] { 1 }));
    }

    [Test]
    public void A_Floor_With_No_Informants_Gives_No_Informants_Bonus()
    {
        // Arrange
        _stats["kills"] = (10, 10);
        _stats["points"] = (5000, 5000);
        _stats["informants"] = (0, 0);

        // Act
        Score(100);

        // Assert
        Assert.That(_bonuses.Given, Is.EquivalentTo(new[] { 0, 1 }));
    }

    [Test]
    public void A_Floor_With_No_Enemies_Gives_The_Enemy_Bonus_With_The_First_Points_Scored_There()
    {
        // Arrange: as bstone's check (total_enemy <= accum_enemy)
        _stats["kills"] = (0, 0);

        // Act
        Score(100);

        // Assert
        Assert.That(_bonuses.Given, Is.EquivalentTo(new[] { 0 }));
    }

    [Test]
    public void A_Level_Taken_Up_Again_Has_Given_What_It_Has_All_Of()
    {
        // Arrange: a kept floor (or a saved game) with every enemy dead already
        _stats["kills"] = (10, 10);
        _session.GameState.TimeCount = 3000;

        // Act
        _bonuses.Start();
        Score(500);

        // Assert
        Assert.That(_session.PlayerState.Score, Is.EqualTo(500));
        Assert.That(_shown, Is.Empty);
    }

    [Test]
    public void No_Bonuses_Without_The_Setting()
    {
        // Arrange
        _settings = new LevelBonusesInfo();
        _bonuses.Start();

        // Act
        Score(100);

        // Assert
        Assert.That(_session.PlayerState.Score, Is.EqualTo(100));
        Assert.That(_shown, Is.Empty);
    }

    [Test]
    public void Reaching_500000_Gives_Full_Ammo_And_Health_And_A_Million_Points_Once()
    {
        // Arrange
        UseAllBonuses();
        _session.PlayerState.Score = 499000;
        _bonuses.Start();

        // Act: reached, then again (a death putting the score back as Blake came onto the floor)
        Score(1000);
        int scoreAfter = _session.PlayerState.Score;
        Tics(500);
        _session.PlayerState.Score = 499000;
        _bonuses.Tick(1);
        Score(1000);
        Tics(500);

        // Assert
        Assert.That(scoreAfter, Is.EqualTo(500000 + 1000000));
        Assert.That(_shown, Is.EqualTo(new[] { "GREAT (Bonus)" }));
        Assert.That(_given, Is.EqualTo(new[] { "health 99", "ChargeUnit 99" }));
    }

    [Test]
    public void A_Bonus_s_Own_Points_Can_Reach_A_Score_Mark()
    {
        // Arrange: the last kill's bonus takes the score past 500,000
        UseAllBonuses();
        _stats["kills"] = (10, 10);
        _session.PlayerState.Score = 460000;
        _bonuses.Start();

        // Act
        Score(500);
        _bonuses.Tick(1);
        Tics(420);

        // Assert: the 50,000 is news the next tic; shown in the table's order after what came first
        Assert.That(_session.PlayerState.Score, Is.EqualTo(460500 + 50000 + 1000000));
        Assert.That(_shown, Is.EqualTo(new[] { "ENEMY (Bonus)", "GREAT (Bonus)" }));
    }

    [Test]
    public void The_Score_Rolling_Over_Shows_Its_Score_Text_For_A_While_And_Can_Come_Again()
    {
        // Arrange
        UseAllBonuses();
        _session.PlayerState.Score = 9999000;
        _bonuses.Start();

        // Act
        Score(1000);
        var during = _bonuses.ScoreText;
        Tics(599);
        var nearlyDone = _bonuses.ScoreText;
        _bonuses.Tick(1);

        // Assert
        Assert.That((during, nearlyDone, _bonuses.ScoreText), Is.EqualTo(("-ROLL-", "-ROLL-", (string?)null)));
        Assert.That(_redraws, Is.EqualTo(2), "drawn as it comes up and as it goes");
        Assert.That(_bonuses.Given, Is.Empty, "it recurs");
    }

    [Test]
    public void An_Extra_Life_Shows_Its_Message_With_Its_Own_Sound()
    {
        // Arrange
        UseAllBonuses();

        // Act: given within the score's rise (PlayerStats.GivePoints)
        _bonuses.ExtraLife(_session.PlayerState);
        Score(100);
        _bonuses.ExtraLife(_session.PlayerState);
        Score(100);
        Tics(420);

        // Assert
        Assert.That(_shown, Is.EqualTo(new[] { "EXTRA (Bonus)", "EXTRA (Bonus)" }));
        Assert.That(_sounds, Is.EqualTo(new[] { "bs/extra_man", "bs/extra_man" }));
    }

    [Test]
    public void A_Guardian_Killed_Says_So_Even_With_No_Points_For_It()
    {
        // Arrange
        UseAllBonuses();

        // Act
        _bonuses.Killed(new Entities.Actors.Monster { Name = "RentACop" });
        _bonuses.Tick(1);
        _bonuses.Killed(new Entities.Actors.Monster { Name = "SpiderMutant" });
        _bonuses.Tick(1);

        // Assert
        Assert.That(_shown, Is.EqualTo(new[] { "GUARDIAN (Bonus)" }));
    }

    [Test]
    public void A_Guardian_Killed_On_Mission_6_Says_To_Destroy_The_Generators()
    {
        // Arrange
        UseAllBonuses();
        _session.GameState.Cluster = 6;

        // Act
        _bonuses.Killed(new Entities.Actors.Monster { Name = "AcidDragon" });
        Score(25000);

        // Assert
        Assert.That(_shown, Is.EqualTo(new[] { "GENERATORS (Bonus)" }));
    }

    [Test]
    public void Game_Info_Reads_Level_Bonuses()
    {
        // Act
        var info = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<GameInfoAsset>("""
            level-bonuses:
              sound: bs/roll_score
              style: Bonus
              bonuses:
                - when: informants
                  needs-some: true
                  after: [kills, points]
                  points: 50000
                  message: "$BS_BONUS_INFORMANTS"
                - when: score
                  at: 10000000
                  recurs: true
                  health: 99
                  give: { ChargeUnit: 99 }
                  score-text: "-ROLL-"
                  score-text-tics: 600
                - when: killed
                  actors: [SpiderMutant]
                  cluster-messages:
                    6: "$BS_BONUS_GUARDIAN_GENERATORS"
            """).LevelBonuses;

        // Assert
        Assert.That((info.Sound, info.Style), Is.EqualTo(("bs/roll_score", "Bonus")));
        var informants = info.Bonuses[0];
        Assert.That((informants.When, informants.NeedsSome, informants.Points, informants.Message), Is.EqualTo(("informants", true, 50000, "$BS_BONUS_INFORMANTS")));
        Assert.That(informants.After, Is.EqualTo(new[] { "kills", "points" }));
        var rolled = info.Bonuses[1];
        Assert.That((rolled.At, rolled.Recurs, rolled.Health, rolled.Give["ChargeUnit"], rolled.ScoreText, rolled.ScoreTextTics),
            Is.EqualTo((10000000, true, 99, 99, "-ROLL-", 600)));
        var guardian = info.Bonuses[2];
        Assert.That((guardian.Actors.Single(), guardian.ClusterMessages[6]), Is.EqualTo(("SpiderMutant", "$BS_BONUS_GUARDIAN_GENERATORS")));
    }
}
