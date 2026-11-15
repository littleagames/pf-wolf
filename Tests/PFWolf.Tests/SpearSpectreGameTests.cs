namespace PFWolf.Tests;

/// <summary>
/// Spear's Spectres in the real game, headless, on MAP01: one summoned in front of the player,
/// shot down, comes back once its corpse has lain 300 tics and nothing's in the way, and is never
/// in the kill ratio. Needs the Spear data files.
/// </summary>
[Category("GameData")]
public class SpearSpectreGameTests
{
    private static List<string> Play(string commands)
    {
        var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-spectre-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);
        try
        {
            HeadlessGame.PrepareFolder(workFolder, "spear");
            var (exitCode, output) = HeadlessGame.Run(workFolder, "spear", "wait 5; killall; " + commands + "; quit",
                TimeSpan.FromMinutes(2), extraArgs: ["--warp", "MAP01", "--cheats"]);
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

    [Test]
    public void A_Spectre_Shot_Down_Comes_Back_And_Is_Not_Counted()
    {
        // Act: summoned and seen, killed, then left to lie well past its 300-tic fade, with the
        // player stepped back out of its way (one standing within a tile keeps it down)
        var output = Play("god; count; summon Spectre; wait 20; count; killall; wait 10; actors; count; tp 32 61; wait 500; actors; count");

        // Assert: the kill ratio never moves (the level's own enemies were all killed first)
        var kills = output.Where(line => line.StartsWith("Kills: ")).Select(line => line.Split("  ")[0]).ToList();
        Assert.That(kills, Has.Count.EqualTo(4));
        Assert.That(kills.Distinct().ToList(), Has.Count.EqualTo(1), string.Join(", ", kills));

        // It lay dead, then rose again
        var spectre = output.Where(line => line.StartsWith("Spectre at ")).ToList();
        Assert.That(spectre, Has.Count.EqualTo(2));
        Assert.That(spectre[0], Does.Contain("state Death"));
        Assert.That(spectre[1], Does.Contain("state Spawn").Or.Contain("state Chase"));
    }
}
