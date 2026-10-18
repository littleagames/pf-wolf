using PFWolf.Assets.Demos;

namespace PFWolf.Tests;

/// <summary>
/// The console's demotrace, run on the real game headless (HeadlessGame): the trace has a frame
/// for each of the demo's, and ends where demotest says the demo ends.
/// </summary>
[Category("GameData")]
public class DemoTraceGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-demotrace-" + Guid.NewGuid().ToString("N"));
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

    [Test]
    public void A_Traced_Demo_Ends_Where_Demotest_Says()
    {
        // Arrange
        HeadlessGame.PrepareFolder(_workFolder, "wolf3d");
        var tracePath = Path.Combine(_workFolder, "trace.txt");

        // Act
        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", $"demotrace 0 \"{tracePath}\"; quit", Timeout);

        // Assert
        Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));
        var report = output.Single(line => line.StartsWith("demotest 0:"));
        var trace = DemoTrace.Read(tracePath);
        Assert.That(trace.Report, Is.EqualTo(report));
        Assert.That(trace.MapName, Does.StartWith("MAP"));
        Assert.That(trace.EndedHow, Is.Not.EqualTo("not-played"));
        Assert.That(trace.Actors, Is.Not.Empty);

        // The player's last spot is the report's "player x,y,angle"
        var spot = report.Split(" player ")[1].Split(' ')[0].Split(',').Select(int.Parse).ToArray();
        var last = trace.Player[^1];
        Assert.That((last.X * DemoTrace.TileUnit, last.Y * DemoTrace.TileUnit, last.Angle), Is.EqualTo(((double)spot[0], (double)spot[1], spot[2])));
    }

    [Test]
    public void A_Dmo_File_Is_Traced_Too()
    {
        // Arrange: stand on MAP01 for 20 frames, turning
        HeadlessGame.PrepareFolder(_workFolder, "wolf3d");
        var demoPath = Path.Combine(_workFolder, "turn.dmo");
        File.WriteAllBytes(demoPath, HeadlessGame.ScriptedDemo(0, new HeadlessGame.DemoStep(0, 10, 0, 20)));
        var tracePath = Path.Combine(_workFolder, "trace.txt");

        // Act
        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", $"demotrace \"{demoPath}\" \"{tracePath}\"; quit", Timeout);

        // Assert: the game reads a frame while a whole one is left, so it plays them all
        Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));
        var trace = DemoTrace.Read(tracePath);
        Assert.That((trace.Label, trace.MapName, trace.FrameCount), Is.EqualTo(("turn.dmo", "MAP01", 20)));
        Assert.That(trace.EndedHow, Is.EqualTo("out-of-data"));
        Assert.That(trace.Player[^1].Angle, Is.Not.EqualTo(trace.Player[0].Angle));
    }
}
