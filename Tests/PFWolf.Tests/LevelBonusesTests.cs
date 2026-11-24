using PFWolf.Assets;

namespace PFWolf.Tests;

/// <summary>
/// Game-info level-bonuses (Gameplay/LevelBonuses.cs): Blake Stone's floor bonuses for all enemies
/// destroyed, all points collected and then all informants alive, checked as the score goes up,
/// each given once, their messages one at a time
/// </summary>
public class LevelBonusesTests
{
    private GameSession _session = null!;
    private Dictionary<string, (int, int)> _stats = null!;
    private List<string> _shown = null!;
    private List<string> _sounds = null!;
    private LevelBonuses _bonuses = null!;

    private static readonly LevelBonusesInfo BlakeBonuses = new()
    {
        Sound = "bs/roll_score",
        Style = "Bonus",
        Bonuses =
        [
            new() { When = "kills", Points = 50000, Message = "ENEMY" },
            new() { When = "points", Points = 50000, Message = "POINTS" },
            new() { When = "informants", NeedsSome = true, After = ["kills", "points"], Points = 50000, Message = "INFORMANTS" },
        ],
    };

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
        _bonuses = new LevelBonuses(_session, new LevelBonusHooks(
            () => BlakeBonuses,
            when => _stats.TryGetValue(when, out var stat) ? stat : null,
            (player, points) => player.Score += points,
            (message, style) => _shown.Add($"{message} ({style})"),
            _sounds.Add,
            _ => 420));
        _bonuses.Start();
    }

    // Points scored this tic (a kill or a pickup), then the tic's end
    private void Score(int points, int tics = 1)
    {
        _session.PlayerState.Score += points;
        _bonuses.Tick(tics);
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
        for (int tic = 0; tic < 419; tic++)
            _bonuses.Tick(1);
        var shownBeforeTimeUp = _shown.ToList();
        _bonuses.Tick(1);

        // Assert: the informants' bonus comes after the other two (all alive)
        Assert.That(scoreAtOnce, Is.EqualTo(500 + 3 * 50000));
        Assert.That(shownAtOnce, Is.EqualTo(new[] { "ENEMY (Bonus)" }));
        Assert.That(shownBeforeTimeUp, Has.Count.EqualTo(1));
        Assert.That(_shown, Is.EqualTo(new[] { "ENEMY (Bonus)", "POINTS (Bonus)" }));

        for (int tic = 0; tic < 420; tic++)
            _bonuses.Tick(1);
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
        var bonuses = new LevelBonuses(_session, new LevelBonusHooks(
            () => new LevelBonusesInfo(), _ => (1, 1), (player, points) => player.Score += points,
            (message, _) => _shown.Add(message), _sounds.Add, _ => 420));
        bonuses.Start();

        // Act
        _session.PlayerState.Score += 100;
        bonuses.Tick(1);

        // Assert
        Assert.That(_session.PlayerState.Score, Is.EqualTo(100));
        Assert.That(_shown, Is.Empty);
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
            """).LevelBonuses;

        // Assert
        Assert.That((info.Sound, info.Style), Is.EqualTo(("bs/roll_score", "Bonus")));
        var bonus = info.Bonuses.Single();
        Assert.That((bonus.When, bonus.NeedsSome, bonus.Points, bonus.Message), Is.EqualTo(("informants", true, 50000, "$BS_BONUS_INFORMANTS")));
        Assert.That(bonus.After, Is.EqualTo(new[] { "kills", "points" }));
    }
}
