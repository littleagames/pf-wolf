using System.Globalization;
using static PFWolf.Tests.HeadlessGame;

namespace PFWolf.Tests;

/// <summary>
/// actor:move in play (Wolf3D E1M1): a probe mod's game.ontick moves the player and a guard once a
/// second in, and prints what each move returned and how far the actor really went. Needs Wolf3D's
/// data files, as DemoRegressionTests does.
/// </summary>
[Category("GameData")]
public class ScriptMoveGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-scriptmove-" + Guid.NewGuid().ToString("N"));
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
    public void A_Player_Moves_As_Walking_Does_And_Walls_Stop_Them()
    {
        // Act
        var moves = Play();

        // Assert: every move returns how far they really went
        foreach (var move in moves.Where(m => m.Who == "player"))
        {
            Assert.That(move.Dx, Is.EqualTo(move.WentX), move.ToString());
            Assert.That(move.Dy, Is.EqualTo(move.WentY), move.ToString());
            Assert.That(move.Walkable, Is.True, $"{move}: they're on open floor");
        }

        // A quarter tile in the open goes all the way; 4 tiles at a time, four times each way,
        // they run into a wall somewhere and stop short
        var small = moves.Single(m => m.What == "small");
        Assert.That(Math.Abs(small.Dx) + Math.Abs(small.Dy), Is.EqualTo(0.25).Within(1e-4));
        foreach (var way in new[] { "east", "west", "north", "south" })
        {
            var far = moves.Where(m => m.What == way).ToList();
            Assert.That(far, Has.Count.EqualTo(4));
            Assert.That(far.Sum(m => Math.Abs(m.Dx) + Math.Abs(m.Dy)), Is.LessThan(16), $"{way}: stopped by a wall");
        }
    }

    [Test]
    public void A_Living_Guard_Is_Only_Shoved_So_Far_Off_Its_Walk()
    {
        // Act
        var guard = Play().Single(m => m.Who == "guard");

        // Assert: no more than 3/8 tile either way, and what's returned is how far it went
        Assert.That(Math.Abs(guard.Dx), Is.LessThanOrEqualTo(0.375 + 1e-4), guard.ToString());
        Assert.That(Math.Abs(guard.Dy), Is.LessThanOrEqualTo(0.375 + 1e-4), guard.ToString());
        Assert.That(guard.Dx, Is.EqualTo(guard.WentX), guard.ToString());
        Assert.That(guard.Dy, Is.EqualTo(guard.WentY), guard.ToString());
    }

    private readonly record struct Move(string Who, string What, double Dx, double Dy, double WentX, double WentY, bool Walkable);

    // Stands on E1M1 with a probe making the moves a second in
    private List<Move> Play()
    {
        var probe = Path.Combine(_workFolder, "probe");
        Directory.CreateDirectory(Path.Combine(probe, "scripts"));
        File.WriteAllText(Path.Combine(probe, "modinfo.yaml"), "name: Probe\nversion: \"1\"\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(probe, "scripts", "probe.lua"), """
            local function try(who, what, actor, dx, dy)
              local x, y = actor.x, actor.y
              local mx, my = actor:move(dx, dy)
              print(string.format("MOVE %s %s %.6f %.6f %.6f %.6f %s", who, what, mx, my,
                actor.x - x, actor.y - y, tostring(game.walkable(actor.tilex, actor.tiley))))
            end

            local done = false
            game.ontick(function()
              if done or game.time() < 70 then return end
              done = true
              local me = game.players()[1]
              try("player", "small", me, 0.25, 0)
              try("player", "small-back", me, -0.25, 0)
              for _, way in ipairs({ { "east", 4, 0 }, { "west", -4, 0 }, { "north", 0, -4 }, { "south", 0, 4 } }) do
                for _ = 1, 4 do try("player", way[1], me, way[2], way[3]) end
              end
              for _, guard in ipairs(game.actors("Guard")) do
                if guard.health > 0 then
                  try("guard", "shove", guard, 1, 1)
                  break
                end
              end
            end)
            """);
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO1.dmo"), ScriptedDemo(0, new DemoStep(0, 0, 0, 140)));

        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", "demotest 1; quit", Timeout, probe);

        var log = string.Join(Environment.NewLine, output.TakeLast(30));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(output, Has.None.Contains("Exception").And.None.Contains("left out").And.None.Contains("switched off")
            .And.None.Contains("nothing to play"), log);

        var lines = output.Where(line => line.StartsWith("MOVE ")).ToList();
        TestContext.Out.WriteLine(string.Join(Environment.NewLine, lines));
        Assert.That(lines, Is.Not.Empty, log);
        return [.. lines.Select(line => line.Split(' ')).Select(p => new Move(p[1], p[2],
            Number(p[3]), Number(p[4]), Number(p[5]), Number(p[6]), p[7] == "true"))];
    }

    private static double Number(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
}
