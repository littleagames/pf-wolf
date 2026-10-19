using PFWolf.Assets;

namespace PFWolf.Tests;

/// <summary>A level against the clock (Gameplay/LevelTimer.cs: game-info map time-limit)</summary>
public class LevelTimerTests
{
    private GameSession _session = null!;
    private int _limit, _timeUps, _secondChanges;
    private LevelTimer _timer = null!;

    [SetUp]
    public void SetUp()
    {
        _session = new GameSession { PlayState = PlayState.StillPlaying };
        _session.PlayerState.Health = 100;
        (_limit, _timeUps, _secondChanges) = (0, 0, 0);
        _timer = new LevelTimer(_session, new LevelTimerHooks(() => _limit, () => _timeUps++, () => _secondChanges++));
    }

    [Test]
    public void A_Level_Without_A_Limit_Isnt_Timed()
    {
        // Arrange
        _session.GameState.TimeCount = 100000;

        // Act
        _timer.Tick();

        // Assert
        Assert.That(_timer.TicsLeft, Is.EqualTo(-1));
        Assert.That(_timeUps, Is.Zero);
        Assert.That(_secondChanges, Is.Zero);
    }

    [TestCase(0, 42000)]
    [TestCase(70, 41930)]
    [TestCase(42000, 0)]
    [TestCase(50000, 0)]
    public void The_Time_Left_Counts_Down_On_The_Levels_Clock(int timeCount, int left)
    {
        // Arrange: the Timed Challenge's ten minutes
        _limit = 600;
        _session.GameState.TimeCount = timeCount;

        // Act / Assert
        Assert.That(_timer.TicsLeft, Is.EqualTo(left));
    }

    [Test]
    public void The_Player_Dies_Once_The_Time_Is_Up()
    {
        // Arrange
        _limit = 10;
        _session.GameState.TimeCount = 699;

        // Act: one tic short, then out of time
        _timer.Tick();
        int before = _timeUps;
        _session.GameState.TimeCount = 700;
        _timer.Tick();

        // Assert
        Assert.That(before, Is.Zero);
        Assert.That(_timeUps, Is.EqualTo(1));
    }

    [Test]
    public void Nothing_Happens_Once_The_Player_Is_Dead_Or_The_Level_Is_Over()
    {
        // Arrange
        _limit = 10;
        _session.GameState.TimeCount = 700;

        // Act: dead already, then the level ending another way
        _session.PlayerState.Health = 0;
        _timer.Tick();
        _session.PlayerState.Health = 100;
        _session.PlayState = PlayState.Completed;
        _timer.Tick();

        // Assert
        Assert.That(_timeUps, Is.Zero);
    }

    [Test]
    public void The_Shown_Time_Changes_Once_A_Second()
    {
        // Arrange
        _limit = 10;

        // Act: two seconds of tics
        for (int tic = 0; tic < 140; tic++)
        {
            _session.GameState.TimeCount = tic;
            _timer.Tick();
        }

        // Assert: whole seconds rounded up, so 10 for the first second, then 9
        Assert.That(_secondChanges, Is.EqualTo(2));
    }

    [Test]
    public void A_Map_Reads_Its_Time_Limit()
    {
        var map = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<MapInfo>("time-limit: 600\n");
        Assert.That(map.TimeLimit, Is.EqualTo(600));
    }
}
