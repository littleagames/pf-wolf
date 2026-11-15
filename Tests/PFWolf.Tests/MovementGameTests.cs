using System.Globalization;
using static PFWolf.Tests.HeadlessGame;

namespace PFWolf.Tests;

/// <summary>
/// The movement demo's script (examples/mods/movement-demo/scripts/movement.lua) in play
/// (Wolf3D): on the footsteps demo's long east hall, a probe mod puts one surface under
/// everything (movement.surfacefor, replaced) and prints where the player is each tic, while a
/// scripted demo walks them east and lets go. Needs Wolf3D's data files, as DemoRegressionTests
/// does.
/// </summary>
[Category("GameData")]
public class MovementGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    // Walk east, then stand (a demo frame is 4 tics: 4 seconds of walking, 8 of standing)
    private static readonly DemoStep[] WalkThenStand = [new(0, 0, -35, 70), new(0, 0, 0, 140)];

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-movement-" + Guid.NewGuid().ToString("N"));
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
    public void On_Ice_A_Player_Let_Go_Slides_On()
    {
        // Arrange: when a player on plain floor stops, after letting go
        var plain = Play("nil", WalkThenStand);
        int stopped = LastMoved(plain);

        // Act
        var ice = Play("{ slip = 0.97 }", WalkThenStand);

        // Assert: on the ice they're still going half a second later, and on further than the
        // plain floor took them; walking the same, they end up about as far as the plain floor
        // (the ice only gets them going and stops them slower)
        double slid = XAt(ice, stopped + 35) - XAt(ice, stopped);
        Assert.That(slid, Is.GreaterThan(0.3), "still sliding after letting go");
        Assert.That(LastMoved(ice), Is.GreaterThan(stopped + 35));
        Assert.That(ice[0].X, Is.EqualTo(plain[0].X).Within(1e-6), "the same start");
        Assert.That(ice[^1].X - ice[0].X, Is.EqualTo(plain[^1].X - plain[0].X).Within(0.5));
    }

    [Test]
    public void Mud_Takes_Them_About_Half_As_Far()
    {
        // Act
        var plain = Play("nil", WalkThenStand);
        var mud = Play("{ scale = 0.5 }", WalkThenStand);

        // Assert
        double ratio = (mud[^1].X - mud[0].X) / (plain[^1].X - plain[0].X);
        Assert.That(ratio, Is.InRange(0.4, 0.6));
    }

    [Test]
    public void A_Conveyor_Carries_A_Player_Standing_On_It()
    {
        // Act: standing on a floor carrying east at 2 tiles a second (a demo frame is 4 tics, so
        // 70 frames are 4 seconds: the hall has room for 8 tiles of it)
        var carried = Play("{ carry = { 2, 0 } }", new DemoStep(0, 0, 0, 70));

        // Assert: 2 tiles each second of it, east; none north or south
        double seconds = (carried[^1].Time - carried[0].Time) / 70.0;
        Assert.That(carried[^1].X - carried[0].X, Is.EqualTo(2 * seconds).Within(0.1));
        Assert.That(carried[^1].Y, Is.EqualTo(carried[0].Y).Within(1e-6));
    }

    [Test]
    public void The_Runways_Boost_Pads_Send_A_Player_Down_It()
    {
        // Arrange: the demo's own level; put on the runway west of its pads (3 tiles south of the
        // start), then strafing east onto them for a moment and letting go -- with the pads'
        // kicks, and with them taken off
        var strafeEast = new[] { new DemoStep(Strafe, 35, 0, 10), new DemoStep(0, 0, 0, 120) };
        const string ontoRunway = "local placed = false\ngame.ontick(function() if not placed then placed = true game.players()[1]:move(0, 3) end end)";

        // Act
        var kicked = PlayDemoLevel(ontoRunway, strafeEast);
        var unkicked = PlayDemoLevel(ontoRunway + "\nmovement.floors.MVBOOST.kick = nil", strafeEast);

        // Assert: on the runway (map row 38), and the pads take them much further east
        Assert.That(kicked[^1].Y, Is.InRange(38, 39));
        Assert.That(kicked[^1].X - unkicked[^1].X, Is.GreaterThan(3), $"kicked to {kicked[^1].X}, not to {unkicked[^1].X}");
    }

    [Test]
    public void A_Body_On_The_Conveyor_Rides_It_To_The_End()
    {
        // Act: every enemy killed at once (killall waits for the level to be up; it puts them in
        // their Death states, their health left as it was), then four seconds standing; the
        // probe prints the guards
        var output = HeadlessGame.Run(_workFolder, "wolf3d", "wait 4; killall; wait 280; quit", Timeout,
            ["--warp", "MAP01", "--cheats"], DemoMod(), Probe("""
                game.ontick(function()
                  for _, guard in ipairs(game.actors("Guard")) do
                    print(string.format("GUARD %d %.6f %.6f %s", game.time(), guard.x, guard.y, tostring(guard.state)))
                  end
                end)
                """)).Output;

        // Assert: the one on the belt (it starts at 31.5, 32.5) went east, as far as the belt
        // runs (it stops carrying once the body's on x 38, past its end); the ones on the ice
        // stayed where they fell
        var guards = output.Where(line => line.StartsWith("GUARD ")).Select(line => line.Split(' '))
            .Select(p => (Time: int.Parse(p[1], CultureInfo.InvariantCulture), X: Number(p[2]), Y: Number(p[3]), State: p[4]))
            .ToList();
        Assert.That(guards, Is.Not.Empty, string.Join(Environment.NewLine, output.TakeLast(30)));
        var last = guards.Where(g => g.Time == guards[^1].Time).ToList();
        TestContext.Out.WriteLine(string.Join(Environment.NewLine, last));
        Assert.That(last.Select(g => g.State), Has.None.EqualTo("Spawn"), "all killed");
        var belt = last.Single(g => g.Y is > 32 and < 33);
        Assert.That(belt.X, Is.InRange(37.9, 38.5), "carried to the end of the belt");
        var first = guards.Where(g => g.Time == guards[0].Time && !(g.Y is > 32 and < 33)).ToList();
        Assert.That(first, Has.Count.EqualTo(3));
        foreach (var guard in first)
            Assert.That(last, Has.Some.Matches<(int Time, double X, double Y, string State)>(g => g.X == guard.X && g.Y == guard.Y),
                $"the one at {guard.X},{guard.Y} stayed where it fell");
    }

    private const byte Strafe = 2;

    private readonly record struct Spot(int Time, double X, double Y);

    private static string DemoMod() => Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "movement-demo");

    // A probe mod with a script of its own, loaded after the others
    private string Probe(string lua)
    {
        var probe = Path.Combine(_workFolder, "probe");
        Directory.CreateDirectory(Path.Combine(probe, "scripts"));
        File.WriteAllText(Path.Combine(probe, "modinfo.yaml"), "name: Probe\nversion: \"1\"\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(probe, "scripts", "probe.lua"), lua);
        return probe;
    }

    // Plays the steps on the movement demo's own level, the probe's script as well, printing
    // where the player is each tic
    private List<Spot> PlayDemoLevel(string lua, params DemoStep[] steps)
    {
        var probe = Probe(lua + """

            game.ontick(function()
              local me = game.players()[1]
              if me then print(string.format("SPOT %d %.6f %.6f", game.time(), me.x, me.y)) end
            end)
            """);
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO1.dmo"), ScriptedDemo(0, steps));

        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", "demotest 1; quit", Timeout, DemoMod(), probe);
        var log = string.Join(Environment.NewLine, output.TakeLast(30));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(output, Has.None.Contains("Exception").And.None.Contains("left out").And.None.Contains("switched off"), log);

        var spots = output.Where(line => line.StartsWith("SPOT ")).Select(line => line.Split(' '))
            .Select(p => new Spot(int.Parse(p[1], CultureInfo.InvariantCulture), Number(p[2]), Number(p[3]))).ToList();
        Assert.That(spots, Is.Not.Empty, log);
        TestContext.Out.WriteLine($"{spots[0]} .. {spots[^1]}");
        return spots;
    }

    // The last tic the player moved on
    private static int LastMoved(List<Spot> spots) =>
        spots.Zip(spots.Skip(1)).Last(pair => pair.Second.X != pair.First.X || pair.Second.Y != pair.First.Y).Second.Time;

    private static double XAt(List<Spot> spots, int time) => spots.Last(spot => spot.Time <= time).X;

    // Plays the steps on the footsteps demo's hall with the surface (Lua) under everything,
    // printing where the player is each tic
    private List<Spot> Play(string surface, params DemoStep[] steps)
    {
        var examples = Path.Combine(TestPaths.RepoRoot(), "examples", "mods");
        var probe = Path.Combine(_workFolder, "probe");
        Directory.CreateDirectory(Path.Combine(probe, "scripts"));
        File.WriteAllText(Path.Combine(probe, "modinfo.yaml"), "name: Probe\nversion: \"1\"\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(probe, "scripts", "probe.lua"), $$"""
            local surface = {{surface}}
            function movement.surfacefor(actor) return surface end
            game.ontick(function()
              local me = game.players()[1]
              if me then print(string.format("SPOT %d %.6f %.6f", game.time(), me.x, me.y)) end
            end)
            """);
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO1.dmo"), ScriptedDemo(0, steps));

        // The footsteps demo after the movement demo, so its level is the one played
        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", "demotest 1; quit", Timeout,
            Path.Combine(examples, "movement-demo"), Path.Combine(examples, "footsteps-demo"), probe);

        var log = string.Join(Environment.NewLine, output.TakeLast(30));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(output, Has.None.Contains("Exception").And.None.Contains("left out").And.None.Contains("switched off")
            .And.None.Contains("nothing to play"), log);

        var spots = output.Where(line => line.StartsWith("SPOT ")).Select(line => line.Split(' '))
            .Select(p => new Spot(int.Parse(p[1], CultureInfo.InvariantCulture), Number(p[2]), Number(p[3]))).ToList();
        Assert.That(spots, Has.Count.GreaterThanOrEqualTo(steps.Sum(step => step.Tics)), log);
        TestContext.Out.WriteLine($"{surface}: {spots[0]} .. {spots[^1]}, last moved at {LastMoved(spots)}");
        return spots;
    }

    private static double Number(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
}
