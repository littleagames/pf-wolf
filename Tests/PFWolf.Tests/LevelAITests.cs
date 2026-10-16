namespace PFWolf.Tests;

public class LevelAITests
{
    private GameSession _session = null!;
    private LevelAI _ai = null!;

    [SetUp]
    public void Make()
    {
        _session = new GameSession();
        var map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, _session);
        _ai = new LevelAI(map, null!, null!, _session);
    }

    [Test]
    public void The_Floor_Drop_Is_Given_Once_A_Level()
    {
        Assert.That((_ai.TakeFloorDrop(), _ai.TakeFloorDrop()), Is.EqualTo((true, false)));
    }

    [Test]
    public void Shooting_An_Informant_Warns_The_First_Time_Then_Only_On_The_Sessions_Low_Rolls()
    {
        // Arrange: the first warning takes no roll; then what the session's random numbers say for the next 50
        Assert.That(_ai.ShouldWarnKilledInformant(), Is.True);
        var expected = new RandomTable();
        var rolls = Enumerable.Range(0, 50).Select(_ => expected.RndT() < 25).ToList();

        // Act
        var warnings = Enumerable.Range(0, 50).Select(_ => _ai.ShouldWarnKilledInformant()).ToList();

        // Assert
        Assert.That(warnings, Is.EqualTo(rolls));
    }
}
