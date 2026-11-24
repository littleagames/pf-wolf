using PFWolf.Assets.Demos;
using Step = PFWolf.Tests.HeadlessGame.DemoStep;

namespace PFWolf.Tests;

/// <summary>
/// The Mission Demo (examples/mods/mission-demo) played headless: MAP02's locked elevator, tried
/// before its exit objective is done and again after, by a scripted demo
/// </summary>
[Category("GameData")]
public class MissionDemoGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);
    private const int Elevator = 148, ElevatorThrown = 149, Transmitter = 146, TransmitterWrecked = 147;

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-missiondemo-" + Guid.NewGuid().ToString("N"));
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
        }
    }

    private static string Mod => Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "mission-demo");

    // A demo's frames each move the player forward × 600/65536 of a tile, unless that would
    // take them within 0x5800 (0.34375) of a wall; a quarter turn is 45 for 10 frames (+ right,
    // - left)
    private const double PerUnit = 600 / 65536.0, Near = 0x5800 / 65536.0;
    private static Step Wait(int frames) => new(0, 0, 0, frames);
    private static Step Right(int quarters = 1) => new(0, 45, 0, 10 * quarters);
    private static Step Left() => new(0, -45, 0, 10);
    private static Step Press(byte button) => new(button, 0, 0, 2);

    // So many tiles forward: at 40 a frame, the rest in one frame
    private static Step[] Walk(double tiles)
    {
        int frames = (int)(tiles / (40 * PerUnit));
        var rest = (sbyte)Math.Round((tiles - frames * 40 * PerUnit) / PerUnit);
        return [new(0, 0, -40, frames), new(0, 0, (sbyte)-rest, 1)];
    }

    // Up to a wall about so many tiles off, the last of it at a creep, to stop all but touching
    private static Step[] ToWall(double tiles) => [.. Walk(tiles - 0.4), new(0, 0, -2, 50)];

    private static Step[] Shoot(int shots) =>
        [.. Enumerable.Range(0, shots).SelectMany(_ => new[] { Press(HeadlessGame.Attack), Wait(10) })];

    // MAP02 from its start (13.5,26.5, facing north): to the hall's middle row, east to the key
    // room's far wall over the gold key, back and west to the corridor, north up it into the
    // elevator and east to its switch (the exit refused: the transmitter's sending), back down
    // and west to the transmitter to shoot it down, then back to the switch. Long enough that
    // only the level ending cuts it short.
    private static readonly Step[] Route =
    [
        Wait(5),
        .. Walk(2),                                 // 13.5,24.5
        Right(), .. ToWall(23 - Near - 13.5),       // east, over the key, to the key room's far wall
        Right(2), .. Walk(23 - Near - 9.5),         // west, back past the start to 9.5,24.5
        Right(), .. Walk(5),                        // north, up the corridor to 9.5,19.5
        Right(), .. ToWall(14 - Near - 9.5),        // east, through the elevator to its switch
        Press(HeadlessGame.Use), Wait(20),          // refused: the transmitter's still sending
        Right(2), .. Walk(14 - Near - 9.5),         // west, back to 9.5,19.5
        Left(), .. Walk(5),                         // south, back to 9.5,24.5
        Right(), .. ToWall(9.5 - (2 + Near)),       // west, up to the transmitter
        .. Shoot(8),
        Right(2), .. Walk(9.5 - (2 + Near)),        // east, back to 9.5,24.5
        Left(), .. Walk(5),                         // north, up the corridor to 9.5,19.5
        Right(), .. ToWall(14 - Near - 9.5),        // east, to the switch
        Press(HeadlessGame.Use), Wait(150),         // out, key and all
    ];

    [Test]
    public void The_Locked_Elevator_Refused_Keeps_Its_Key_And_Works_Once_The_Transmitter_Is_Down()
    {
        // Arrange
        HeadlessGame.PrepareFolder(_workFolder, "wolf3d");
        var modFolder = Path.Combine(_workFolder, "mission-demo");
        foreach (var file in Directory.GetFiles(Mod, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(modFolder, Path.GetRelativePath(Mod, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        Directory.CreateDirectory(Path.Combine(modFolder, "demos"));
        File.WriteAllBytes(Path.Combine(modFolder, "demos", "elevator.dmo"), HeadlessGame.ScriptedDemo(1, Route));
        var tracePath = Path.Combine(_workFolder, "trace.txt");

        // Act
        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", $"demotrace elevator \"{tracePath}\"; quit", Timeout, modFolder);

        // Assert
        Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));
        var trace = DemoTrace.Read(tracePath);
        var log = Describe(trace);
        var switchTile = trace.Tiles.GetValueOrDefault((14, 19)) ?? [];
        var transmitterTile = trace.Tiles.GetValueOrDefault((1, 24)) ?? [];

        Assert.That(trace.Events, Has.Some.Matches<TraceEvent>(e => e.Kind == TraceEventKind.Pickup && e.Text.Contains("GoldKey")), log);
        Assert.That(transmitterTile.Select(c => c.State.WallId).ToList(), Is.EqualTo(new[] { TransmitterWrecked }), log);
        // the switch thrown only once, after the transmitter went down: the first try left it as
        // it was, and the gold key with the player
        Assert.That(switchTile.Select(c => c.State.WallId).ToList(), Is.EqualTo(new[] { ElevatorThrown }), log);
        Assert.That(switchTile[0].Frame, Is.GreaterThan(transmitterTile[0].Frame), log);
        // and the level ended there, before the demo ran out
        var last = trace.Player[^1];
        Assert.That(((int)last.X, (int)last.Y), Is.EqualTo((13, 19)), log);
        Assert.That(trace.FrameCount, Is.LessThan(Route.Sum(s => s.Tics)), log);
    }

    // Where the player was as each step of the route ended (a demo step is a frame), the level's
    // changes and how it ended
    private static string Describe(DemoTrace trace)
    {
        var lines = new List<string>();
        int frames = 0, step = 0;
        foreach (var s in Route)
        {
            frames += s.Tics;
            var p = trace.Player[Math.Min(frames, trace.FrameCount) - 1];
            lines.Add($"step {step++} (frame {frames}): {p.X:0.00},{p.Y:0.00} angle {p.Angle}");
        }
        foreach (var ((x, y), changes) in trace.Tiles)
            lines.Add($"tile {x},{y}: {string.Join(", ", changes.Select(c => $"f{c.Frame} {c.State.Kind} {c.State.WallId}"))}");
        foreach (var e in trace.Events)
            lines.Add($"f{e.Frame} {e.Kind} {e.Text}");
        lines.Add($"frames {trace.FrameCount} of {Route.Sum(s => s.Tics)}, ended {trace.EndedHow}");
        return string.Join(Environment.NewLine, lines);
    }
}
