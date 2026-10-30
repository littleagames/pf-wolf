namespace PFWolf.Tests;

/// <summary>
/// The text presenter's ^PM and ^DS codes in play (Aliens of Gold): a mod's level start message
/// plays music by its number in raw-data-map's music list. Needs Aliens of Gold's data files, as
/// BlakeLevelAIGameTests does.
/// </summary>
[Category("GameData")]
public class PresenterCodesGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-presenter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workFolder);
        HeadlessGame.PrepareFolder(_workFolder, "blake-aog");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_workFolder, recursive: true);
        }
        catch (IOException)
        {
            // The game may still be letting go of a file; it's only a temp folder
        }
    }

    [Test]
    public void A_PM_Code_Plays_Music_By_Its_Number()
    {
        // Arrange: "Get Ready" with music 0E, RHUMBAAS (twice: it starts once), after a display
        // string not yet set (no game has ended), which prints nothing
        var mod = Path.Combine(_workFolder, "pm-test");
        Directory.CreateDirectory(mod);
        File.WriteAllText(Path.Combine(mod, "game-info.yaml"),
            "level-start-message: \"^ST1^CEGet Ready^DS00^PM0E^PM0E\\r^XX\"\n");

        // Act
        var (plainExit, plain) = HeadlessGame.Run(_workFolder, "blake", "wait 5; changemus; quit", Timeout, extraArgs: ["--warp", "MAP04"]);
        var (exitCode, output) = HeadlessGame.Run(_workFolder, "blake", "wait 5; changemus; quit", Timeout, extraArgs: ["--warp", "MAP04"], mod);

        // Assert
        Assert.That(plainExit, Is.Zero);
        Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(20)));
        Assert.That(plain, Has.Some.StartsWith("Music: ").And.None.StartsWith("Music: RHUMBAAS"), "the level's own music without the mod");
        Assert.That(output, Has.Some.StartsWith("Music: RHUMBAAS"));
    }
}
