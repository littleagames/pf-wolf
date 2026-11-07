using System.Text.RegularExpressions;

namespace PFWolf.Tests;

/// <summary>
/// Blake Stone's arc barriers in the real game, headless, on AOG mission 1 floor 1 (MAP02): the
/// row at 11..21,27..28 (tag 65535) that its barrier switches toggle, thrown here from Lua as a
/// switch would (game.trigger with A_ToggleActive). `tp` only goes onto open floor, so whether
/// it reaches 11,28 tells whether the barrier blocks. 11,29 is beside two of them (11,28 and
/// 12,28), both within their 1.375-tile reach. Needs the Aliens of Gold data files.
/// </summary>
[Category("GameData")]
public class BlakeBarrierGameTests
{
    private const string Toggle = "lua print(game.trigger(65535, 'A_ToggleActive')); wait 2";

    private static List<string> Play(string commands)
    {
        var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-barriers-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);
        try
        {
            HeadlessGame.PrepareFolder(workFolder, "blake-aog");
            var (exitCode, output) = HeadlessGame.Run(workFolder, "blake", "wait 5; killall; " + commands + "; quit",
                TimeSpan.FromMinutes(2), extraArgs: ["--warp", "MAP02", "--cheats"]);
            foreach (var line in output.Where(line => !line.StartsWith("Movie ")))
                TestContext.Out.WriteLine(line);
            Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));
            return output;
        }
        finally
        {
            try { Directory.Delete(workFolder, recursive: true); } catch (IOException) { }
        }
    }

    // Each `tp 11 28` in turn: true where it got onto the barrier's tile, false where it was refused
    private static List<bool> OntoBarrier(List<string> output) =>
        [.. output.Where(line => line is "Teleported to 11,28" or "tp: tile 11,28 is not open floor")
            .Select(line => line.StartsWith("Teleported"))];

    private static List<int> Healths(List<string> output) =>
        [.. output.Where(line => line.StartsWith("Health is "))
            .Select(line => int.Parse(Regex.Match(line, @"^Health is (\d+)").Groups[1].Value))];

    [Test]
    public void An_Arc_Barrier_Shocks_A_Player_Beside_It_Only_While_It_Is_On()
    {
        // Act: 70 tics beside two barriers that are on (bstone: 4 points, 1 in 16 a tic, each),
        // then 70 more with them off; `hurt 1` reads the health out
        var output = Play($"tp 11 29; wait 70; hurt 1; {Toggle}; wait 70; hurt 1");

        // Assert: about 35 points between the two barriers (the 1 is hurt's), none once they're off
        var health = Healths(output);
        Assert.That(health, Has.Count.EqualTo(2));
        Assert.That(100 - health[0], Is.InRange(9, 70), "some shocks, about one in 16 tics from each barrier");
        Assert.That((100 - health[0] - 1) % 4, Is.Zero, "4 points a shock");
        Assert.That(health[1], Is.EqualTo(health[0] - 1), "off, they never shock");
        Assert.That(output, Has.Some.EqualTo("ATTACKING: ELECTRIC ARC BARRIER."), "the info area says what hurt");
    }

    [Test]
    public void A_Barrier_Turned_Back_On_Blocks_Its_Tile_Again()
    {
        // Act: onto the barrier's tile while it's on, off, then on again (after stepping back)
        var output = Play($"god; tp 11 29; tp 11 28; {Toggle}; tp 11 28; tp 11 29; {Toggle}; tp 11 28; actors ArcBarrier");

        // Assert
        Assert.That(OntoBarrier(output), Is.EqualTo(new[] { false, true, false }));
        Assert.That(output.Last(line => line.StartsWith("ArcBarrier at 11,28 ")), Does.Contain("state Spawn"), "crackling again");
    }

    [Test]
    public void A_Barrier_Saved_Off_Loads_Off_And_Can_Be_Turned_Back_On()
    {
        // Act: saved off, turned on, loaded: is it off and open, and does it block again once on
        var output = Play($"god; tp 11 29; {Toggle}; save off; {Toggle}; tp 11 28; load off; wait 5; actors ArcBarrier; "
            + $"tp 11 28; tp 11 29; {Toggle}; tp 11 28");

        // Assert: blocked while on before the load; open after it; blocked once turned on again
        Assert.That(OntoBarrier(output), Is.EqualTo(new[] { false, true, false }));
        var afterLoad = output.Where(line => line.StartsWith("ArcBarrier at 11,28 ")).ToList();     // the one `actors`
        Assert.That(afterLoad, Has.Count.EqualTo(1));
        Assert.That(afterLoad[0], Does.Contain("state Inactive (ELCKD)"));
    }

    [Test]
    public void A_Barrier_Saved_On_Loads_On_Blocking_And_Shocking()
    {
        // Act: saved on with the player just put beside it, turned off, loaded; then 70 tics there
        var output = Play($"tp 11 29; save on; {Toggle}; load on; wait 5; tp 11 28; wait 70; hurt 1");

        // Assert: still blocking, and its shocks still come after the load
        Assert.That(OntoBarrier(output), Is.EqualTo(new[] { false }));
        var health = Healths(output);
        Assert.That(health, Has.Count.EqualTo(1));
        Assert.That(health[0], Is.LessThan(99), "shocked after the load");
    }
}
