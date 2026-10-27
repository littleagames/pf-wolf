namespace PFWolf.Tests;

/// <summary>
/// Blake Stone's security lights, steam, ooze and mission bosses in the real game, headless, on
/// AOG mission 1 floor 1 (MAP02). Its own enemies are killed first; things are summoned into the
/// big room the player starts in (at 37,54, facing west). Needs the Aliens of Gold data files.
/// </summary>
[Category("GameData")]
public class BlakeMachinesGameTests
{
    private static List<string> Play(string commands, int minutes = 2)
    {
        var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-machines-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);
        try
        {
            HeadlessGame.PrepareFolder(workFolder, "blake-aog");
            var (exitCode, output) = HeadlessGame.Run(workFolder, "blake", "wait 5; killall; " + commands + "; quit",
                TimeSpan.FromMinutes(minutes), extraArgs: ["--warp", "MAP02", "--cheats"]);
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

    // The `actors` line for whatever's on a tile, from each listing in turn
    private static List<string> ActorsAt(List<string> output, string className, int x, int y) =>
        [.. output.Where(line => line.StartsWith($"{className} at {x},{y} "))];

    [Test]
    public void A_Security_Light_Flashes_Once_Shooting_Is_Heard()
    {
        // Act: a light alone, then a boss shooting at the player across the room
        var output = Play("god; summon SecurityLight 0 33 52; wait 30; actors; summon BreatherBeast 0 26 55; wait 400; actors");

        // Assert
        var light = ActorsAt(output, "SecurityLight", 33, 52);
        Assert.That(light, Has.Count.EqualTo(2));
        Assert.That(light[0], Does.Contain("state Spawn"), "quiet, it waits");
        Assert.That(light[1], Does.Contain("state Alert"), "shots were fired");
    }

    [Test]
    public void A_Steam_Grate_In_View_Lets_Off_Steam_After_Four_Seconds()
    {
        // Act: seen from the start, its first wait is 4 seconds (240 tics); steaming lasts 73
        var output = Play("summon SteamGrate 0 34 54; wait 200; actors; wait 70; actors");

        // Assert: it never hurts (bstone's doesn't either)
        var grate = ActorsAt(output, "SteamGrate", 34, 54);
        Assert.That(grate, Has.Count.EqualTo(2));
        Assert.That(grate[0], Does.Contain("state Spawn"));
        Assert.That(grate[1], Does.Contain("state Release"));
    }

    [TestCase("GreenOoze")]
    [TestCase("BlackOoze")]
    public void Standing_In_Ooze_Hurts(string ooze)
    {
        // Act: under the player, who stands in it for 26 of its 90-tic rounds (4 points, 1 in 8)
        var output = Play($"summon {ooze} 0 37 54; wait 2400; hurt 1");

        // Assert: the 1 from hurt, and a few bites, not one every round (rolling every tic of its
        // frame, as it once did, would have taken nearly all 100)
        var health = output.Single(line => line.StartsWith("Health is "));
        Assert.That(int.Parse(health.Split(' ', ',')[2]), Is.InRange(60, 98), health);
        Assert.That(output, Has.Some.StartsWith("ATTACKING:"), "the REBA shows what hurt");
    }

    [TestCase("SpiderMutant", "MSUP")]
    [TestCase("BreatherBeast", "BBST")]
    [TestCase("CyborgWarrior", "CYBW")]
    [TestCase("ReptilianWarrior", "REPW")]
    [TestCase("AcidDragon", "ADRG")]
    [TestCase("BioMechGuardian", "BIOM")]
    public void A_Mission_Boss_Fights_Dies_And_Leaves_The_Gold_Card(string boss, string sprite)
    {
        // Act: across the room from the player (in god mode), then killed
        var output = Play($"god; summon {boss} 0 26 53; wait 400; actors; killall; wait 150; actors");

        // Assert: it came for the player and hit them, then lay dead with the gold card dropped
        Assert.That(output, Has.Some.StartsWith("ATTACKING:"), "it hit the player");
        Assert.That(output, Has.Some.EqualTo("Killed 1 enemies"));
        var last = output.FindLastIndex(line => line.StartsWith("Player at "));
        var after = output[last..];
        Assert.That(after, Has.Some.Matches($@"^{boss} at \d+,\d+  state Death \({sprite}M\)"));
        Assert.That(after, Has.Some.StartsWith("GoldCard at "));
    }
}
