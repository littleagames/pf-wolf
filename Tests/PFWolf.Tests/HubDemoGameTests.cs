namespace PFWolf.Tests;

/// <summary>
/// The Hub Demo (examples/mods/hub-demo) played headless: A_ChangeMap from the hub (run as its
/// levers and pad run it), coming back to it by an entrance, and the levels kept as they were left
/// </summary>
[Category("GameData")]
public class HubDemoGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-hubdemo-" + Guid.NewGuid().ToString("N"));
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

    private static string Mod => Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "hub-demo");

    // The "Tile: x,y  Area: n  Map: NAME" lines `where` printed, in order
    private static List<string> Wheres(List<string> output) =>
        [.. output.Where(line => line.StartsWith("Tile: ", StringComparison.Ordinal))
            .Select(line => System.Text.RegularExpressions.Regex.Replace(line, @"\s+Area: \d+", ""))];

    [Test]
    public void The_Hubs_Ways_Out_Go_To_Its_Levels_And_Each_Comes_Back_By_Its_Entrance()
    {
        // Arrange
        HeadlessGame.PrepareFolder(_workFolder, "wolf3d");
        var commands = string.Join("; ",
            "wait 5",                                       // the level up first: the commands below need one
            "god",
            "where",                                        // the hub's own start
            "tag 7 32 30",                                  // something to find again: the hub is kept
            "lua game.trigger(0, 'A_ChangeMap', 'HUB1')",   // the west elevator's lever
            "wait 10", "where",
            "tp 30 31",                                     // down the Cellblock's corridor
            "exitlevel",                                    // the Cellblock's elevator: back in the west elevator
            "wait 10", "where", "lua print('kept tag', game.tag(32, 30))",
            "lua game.trigger(0, 'A_ChangeMap', 'HUB3')",   // the teleport pad's action (tp onto it doesn't walk onto it)
            "wait 10", "where",
            "exitlevel",                                    // back south of the pad
            "wait 10", "where",
            "lua game.trigger(0, 'A_ChangeMap', 'HUB2')",   // the east elevator's lever
            "wait 10", "where",
            "exitlevel",                                    // back in the east elevator
            "wait 10", "where",
            "lua game.trigger(0, 'A_ChangeMap', 'HUB1')",   // the Cellblock again: kept, so where it was left
            "wait 10", "where",
            "exitlevel",
            "wait 10", "doors",
            "quit");

        // Act
        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", commands, Timeout, ["--warp", "MAP01", "--cheats"], Mod);

        // Assert
        var log = string.Join(Environment.NewLine, output.TakeLast(60));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(Wheres(output), Is.EqualTo(new[]
        {
            "Tile: 32,31  Map: MAP01",      // the hub's own start
            "Tile: 21,27  Map: HUB1",       // the Cellblock's start
            "Tile: 24,29  Map: MAP01",      // entrance west: in the west elevator
            "Tile: 32,33  Map: HUB3",       // the Chapel's start
            "Tile: 32,23  Map: MAP01",      // entrance north: south of the pad
            "Tile: 30,34  Map: HUB2",       // the Armory's start
            "Tile: 40,29  Map: MAP01",      // entrance east: in the east elevator
            "Tile: 30,31  Map: HUB1",       // the Cellblock as it was left
        }), log);
        Assert.That(output, Has.Some.Matches<string>(line => line.Contains("kept tag") && line.TrimEnd().EndsWith('7')), log);
        Assert.That(output, Has.Some.Contains("at 32,33: Closed  locked (CellblockEmblem+ArmoryEmblem+ChapelEmblem)"), log);
        Assert.That(output, Has.None.Contains("Asset not found"), log);
    }

    [Test]
    public void Walking_Onto_The_Teleport_Pad_Leaves_The_Hub()
    {
        // Arrange: a demo from the hub's start, facing north: up the hall to the pad room's door,
        // open it, and on north onto the pad (32,22). Longer than it takes, so it's cut short only
        // by the level ending.
        HeadlessGame.PrepareFolder(_workFolder, "wolf3d");
        var modFolder = Path.Combine(_workFolder, "hub-demo");
        CopyFolder(Mod, modFolder);
        Directory.CreateDirectory(Path.Combine(modFolder, "demos"));
        var steps = new[]
        {
            new HeadlessGame.DemoStep(0, 0, -40, 80),                   // to the door
            new HeadlessGame.DemoStep(HeadlessGame.Use, 0, 0, 4),       // open it
            new HeadlessGame.DemoStep(0, 0, 0, 50),
            new HeadlessGame.DemoStep(0, 0, -40, 120),                  // through, and onto the pad
        };
        File.WriteAllBytes(Path.Combine(modFolder, "demos", "pad.dmo"), HeadlessGame.ScriptedDemo(0, steps));
        var tracePath = Path.Combine(_workFolder, "trace.txt");

        // Act
        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", $"demotrace pad \"{tracePath}\"; quit", Timeout, modFolder);

        // Assert: it ended on the pad, before the demo ran out
        Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));
        var trace = PFWolf.Assets.Demos.DemoTrace.Read(tracePath);
        var last = trace.Player[^1];
        Assert.That(((int)last.X, (int)last.Y), Is.EqualTo((32, 22)));
        Assert.That(trace.FrameCount, Is.LessThan(steps.Sum(s => s.Tics)));
    }

    private static void CopyFolder(string from, string to)
    {
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
