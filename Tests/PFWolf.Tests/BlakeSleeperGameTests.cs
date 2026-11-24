using System.Globalization;
using System.Text.RegularExpressions;

namespace PFWolf.Tests;

/// <summary>
/// Blake Stone's sleepers in the real game, headless, on AOG mission 1 floor 1 (MAP02): a
/// canister shot open still blocks its tile once its alien is out, as bstone's KeepSolid has it
/// (a volatile transport used to drive into one, and Blake could walk through it). The player
/// stands at 11,29 facing east along the open floor; the canister is put at 13,29 and walked at
/// with actor:move. Needs the Aliens of Gold data files.
/// </summary>
[Category("GameData")]
public class BlakeSleeperGameTests
{
    private static List<string> Play(string commands)
    {
        var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-sleepers-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);
        try
        {
            HeadlessGame.PrepareFolder(workFolder, "blake-aog");
            var (exitCode, output) = HeadlessGame.Run(workFolder, "blake", "wait 5; killall; god; " + commands + "; quit",
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

    // How far east each `WALKED` line says the player got
    private static List<double> Walked(List<string> output) =>
        [.. output.Where(line => line.StartsWith("WALKED "))
            .Select(line => double.Parse(Regex.Match(line, @"^WALKED (\S+)").Groups[1].Value, CultureInfo.InvariantCulture))];

    // (no `;` in it: the console splits commands on them)
    private const string WalkEast = "lua local me = game.players()[1] local x = me.x me:move(3, 0) print(string.format('WALKED %.4f', me.x - x))";

    [TestCase("ScanCanister", "ScanAlien")]
    [TestCase("LcanCanister", "LcanAlien")]
    public void A_Canister_Shot_Open_Still_Blocks_Once_Its_Alien_Is_Out(string canister, string alien)
    {
        // Act: the way east walked with nothing there; then the canister shot open, its alien
        // killed as it comes out (after 90 tics breaking), so its body lies on the canister's tile
        // (a body never blocks), and the way walked again from the same spot
        var output = Play($"tp 11 29 0; {WalkEast}; tp 11 29 0; summon {canister} 0 13 29; killall; wait 92; "
            + $"killall; wait 35; actors {alien}; actors {canister}; {WalkEast}");

        // Assert: 3 tiles in the open; short of the broken canister's tile (13,29) with it there:
        // from 11.5, about 1.5 tiles less the player's and its sizes
        var walked = Walked(output);
        Assert.That(walked, Has.Count.EqualTo(2));
        Assert.That(walked[0], Is.EqualTo(3).Within(1e-3), "the way's open without it");
        Assert.That(output, Has.Some.Matches($@"^{alien} at 13,29 .*state Death"), "its alien came out, and lies on its tile");
        Assert.That(walked[1], Is.InRange(0.5, 1.5), "stopped by the broken canister at 13,29");
    }

    [TestCase("LcanCanister", 30)]
    [TestCase("ScanCanister", 30)]
    [TestCase("BlakeCrate", 30)]
    public void Walking_Past_A_Canister_On_The_Next_Row_Is_Not_Stopped(string thing, int row)
    {
        // Act: walked east along row 29 past one standing a row off, at 13,row (bstone's
        // FL_FAKE_STATIC: it blocks its own tile, as a static does, not a tile out from its
        // center as an enemy does -- which caught the player walking by, M4F1's at 21,52)
        var output = Play($"tp 11 29 0; summon {thing} 0 13 {row}; wait 2; actors {thing}; {WalkEast}");

        // Assert: the whole 3 tiles
        Assert.That(output, Has.Some.Matches($@"^{thing} at 13,{row} "), "it's there");
        var walked = Walked(output);
        Assert.That(walked, Has.Count.EqualTo(1));
        Assert.That(walked[0], Is.EqualTo(3).Within(1e-3), $"walked past the one at 13,{row}");
    }
}
