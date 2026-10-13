namespace PFWolf.Tests;

/// <summary>
/// Plays every demo of a game with the console's `demotest` and checks each ends exactly as it did
/// before (score, kills, the player's spot, a hash of every actor): a change to the game's rules
/// shows up here. The built game runs on its own, headless (HeadlessGame), in a temporary folder
/// holding the release's data files and a fresh pfwolf.pk3 zipped from pfwolf-pk3/.
///
/// Needs the game's data files: from PFWOLF_GAMEDATA, else the game's own x64 build folder. A game
/// whose files aren't there (or are another version, by md5) is skipped.
///
/// The expected results are Baselines/demotest-{pack}.txt. A change that's meant to change how the
/// game plays updates them: run with PFWOLF_UPDATE_BASELINES=1 and check the new files in. A
/// missing baseline is written on the first run.
/// </summary>
[Category("GameData")]
public class DemoRegressionTests
{
    // Releases with demos to play (Blake Stone and Planet Strike ship none)
    private static readonly string[] Releases = ["wolf3d", "spear", "wolf3d-shareware", "spear-demo"];

    private static IEnumerable<TestCaseData> ReleaseCases() =>
        Releases.Select(release => new TestCaseData(release).SetArgDisplayNames(release));

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-demotest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workFolder);
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

    [TestCaseSource(nameof(ReleaseCases))]
    public void Demos_Play_Out_As_They_Did_Before(string release)
    {
        // Arrange
        var dataFolder = HeadlessGame.PrepareFolder(_workFolder, release);

        // Act
        var (exitCode, output) = HeadlessGame.Run(_workFolder, release, "demotest; quit", Timeout);
        var results = output.Where(line => line.StartsWith("demotest ")).ToList();

        // Assert
        Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));
        Assert.That(results, Is.Not.Empty, "demotest printed nothing");
        Assert.That(results, Has.None.Contains("no such demo"));

        var baselinePath = Path.Combine(TestPaths.RepoRoot(), "Tests", "PFWolf.Tests", "Baselines", $"demotest-{release}.txt");
        var update = Environment.GetEnvironmentVariable("PFWOLF_UPDATE_BASELINES") == "1";
        if (update || !File.Exists(baselinePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            File.WriteAllLines(baselinePath, results);
            Assert.Warn($"Wrote {baselinePath} from {dataFolder}; check it in");
            return;
        }

        Assert.That(results, Is.EqualTo(File.ReadAllLines(baselinePath).Where(line => line.Length > 0)),
            $"A demo ended differently from {baselinePath}. If the change is meant to change how the game plays, " +
            "run with PFWOLF_UPDATE_BASELINES=1 and check the new baseline in.");
    }
}
