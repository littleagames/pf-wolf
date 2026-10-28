using static PFWolf.Tests.HeadlessGame;

namespace PFWolf.Tests;

/// <summary>
/// The footsteps demo (examples/mods/footsteps-demo) in play (Wolf3D): a scripted demo walks the
/// player east down its hall, over the plain floor, the dirt and the grating, and a probe mod
/// loaded after it prints each step the script plays (footsteps.play, wrapped). Needs Wolf3D's
/// data files, as DemoRegressionTests does.
/// </summary>
[Category("GameData")]
public class FootstepsGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-footsteps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workFolder);
        HeadlessGame.PrepareFolder(_workFolder, "wolf3d");
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
    public void Walking_The_Hall_Steps_Sound_By_The_Floor_Under_The_Player()
    {
        // Arrange: walk east for 6 seconds (the hall's 26 tiles take about 5), then stand
        var steps = Play(new DemoStep(0, 0, -35, 420), new DemoStep(0, 0, 0, 70));

        // Assert: the plain floor (x 17-25, no flat) steps the default, then dirt (26-33), then grating (34-42)
        Assert.That(steps, Has.Some.Matches<Step>(s => s.Sound == "footsteps/default" && s.Floor == "nil" && s.X <= 25));
        Assert.That(steps, Has.Some.Matches<Step>(s => s.Sound == "footsteps/dirt" && s.Floor == "FSDIRT" && s.X is >= 26 and <= 33));
        Assert.That(steps, Has.Some.Matches<Step>(s => s.Sound == "footsteps/metal" && s.Floor == "FSGRATE" && s.X is >= 34 and <= 42));
        Assert.That(steps, Is.Ordered.By(nameof(Step.Time)).And.Ordered.By(nameof(Step.X)), "steps go east, one after another");

        // About a step every 2.5 tiles: 26 tiles walked, and none standing at the end
        Assert.That(steps, Has.Count.InRange(8, 12));
        Assert.That(steps.Zip(steps.Skip(1), (a, b) => b.Time - a.Time), Has.All.GreaterThanOrEqualTo(12), "mintics apart");
    }

    [Test]
    public void Standing_Still_Makes_No_Steps()
    {
        // Arrange: turn on the spot, then stand
        var steps = Play(new DemoStep(0, 20, 0, 140), new DemoStep(0, 0, 0, 140));

        // Assert
        Assert.That(steps, Is.Empty);
    }

    private readonly record struct Step(int Time, string Sound, string Floor, int X);

    // Plays the steps on the demo's MAP01 with a probe printing each footstep (still playing it)
    private List<Step> Play(params DemoStep[] steps)
    {
        var demo = Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "footsteps-demo");
        var probe = Path.Combine(_workFolder, "probe");
        Directory.CreateDirectory(Path.Combine(probe, "scripts"));
        File.WriteAllText(Path.Combine(probe, "modinfo.yaml"), "name: Probe\nversion: \"1\"\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(probe, "scripts", "probe.lua"), """
            local play = footsteps.play
            function footsteps.play(player, sound, floor)
              print("STEP " .. game.time() .. " " .. sound .. " " .. tostring(floor) .. " " .. player.tilex)
              play(player, sound, floor)
            end
            """);
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO1.dmo"), ScriptedDemo(0, steps));

        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", "demotest 1; quit", Timeout, demo, probe);

        var log = string.Join(Environment.NewLine, output.TakeLast(30));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(output, Has.None.Contains("Exception").And.None.Contains("left out").And.None.Contains("switched off")
            .And.None.Contains("nothing to play"), log);

        var lines = output.Where(line => line.StartsWith("STEP ")).ToList();
        TestContext.Out.WriteLine(string.Join(Environment.NewLine, lines));
        return [.. lines.Select(line => line.Split(' ')).Select(p => new Step(int.Parse(p[1]), p[2], p[3], int.Parse(p[4])))];
    }
}
