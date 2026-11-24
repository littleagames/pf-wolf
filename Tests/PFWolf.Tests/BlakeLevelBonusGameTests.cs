namespace PFWolf.Tests;

/// <summary>
/// Blake Stone's floor bonuses in the real game, headless (game-info level-bonuses): killing every
/// enemy on the floor shows "ALL ENEMY DESTROYED!" in the info area, once. Aliens of Gold mission 1
/// floor 1 (MAP02) and Planet Strike's first floor (MAP01, its level-bonuses from blake's).
/// Needs the Aliens of Gold and Planet Strike data files.
/// </summary>
[Category("GameData")]
public class BlakeLevelBonusGameTests
{
    [TestCase("blake-aog", "blake", "MAP02")]
    [TestCase("blake-ps", "planetstrike", "MAP01")]
    public void Killing_Every_Enemy_Shows_The_Enemy_Bonus_Once(string release, string game, string map)
    {
        var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-bonus-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);
        try
        {
            // Act: everyone killed (their points scored), then again after a while
            HeadlessGame.PrepareFolder(workFolder, release);
            var (exitCode, output) = HeadlessGame.Run(workFolder, game,
                "wait 5; killall; wait 10; count; wait 500; killall; wait 10; quit",
                TimeSpan.FromMinutes(2), extraArgs: ["--warp", map, "--cheats"]);
            foreach (var line in output.Where(line => !line.StartsWith("Movie ")))
                TestContext.Out.WriteLine(line);
            Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));

            // Assert
            Assert.That(output.Count(line => line.Contains("ALL ENEMY DESTROYED!")), Is.EqualTo(1));
            Assert.That(output.Any(line => line.Contains("50,000 POINT BONUS!")), Is.True);
        }
        finally
        {
            try { Directory.Delete(workFolder, recursive: true); } catch (IOException) { }
        }
    }

    // Mission 1 floor 1 (find the exit), and mission 6 floor 1 (destroy the generators)
    [TestCase("MAP02", "COMPLETE THIS MISSION")]
    [TestCase("MAP77", "PROJECTION GENERATORS!")]
    public void Killing_A_Guardian_Alien_Says_What_Is_Left_To_Do(string map, string told)
    {
        var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-bonus-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);
        try
        {
            // Act
            HeadlessGame.PrepareFolder(workFolder, "blake-aog");
            var (exitCode, output) = HeadlessGame.Run(workFolder, "blake",
                "wait 5; god; summon SpiderMutant; wait 2; killall; wait 10; quit",
                TimeSpan.FromMinutes(2), extraArgs: ["--warp", map, "--cheats"]);
            foreach (var line in output.Where(line => !line.StartsWith("Movie ")))
                TestContext.Out.WriteLine(line);
            Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));

            // Assert: before the floor's own (all enemies destroyed, with the guardian)
            var guardian = output.FindIndex(line => line.Contains("GUARDIAN ALIEN DESTROYED!"));
            Assert.That(guardian, Is.GreaterThanOrEqualTo(0));
            Assert.That(output[guardian], Does.Contain(told));
        }
        finally
        {
            try { Directory.Delete(workFolder, recursive: true); } catch (IOException) { }
        }
    }

    [Test]
    public void Score_Bonuses_Give_An_Extra_Life_Then_Full_Health_And_A_Million_Points_Then_Roll_Over()
    {
        var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-bonus-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);
        try
        {
            // Act: 400,000 (an extra life), then to 500,000 (GREAT SCORE: health, ammo, a million
            // more), then past 9,999,999 (the score rolls over; its message waits out GREAT SCORE's
            // 7 seconds); `hurt 1` reads the health out
            HeadlessGame.PrepareFolder(workFolder, "blake-aog");
            var (exitCode, output) = HeadlessGame.Run(workFolder, "blake",
                "wait 5; hurt 50; give points 400000; wait 10; give points 100000; wait 450; hurt 1; "
                + "give points 1000000; give points 1000000; give points 1000000; give points 1000000; give points 1000000; "
                + "give points 1000000; give points 1000000; give points 1000000; give points 500000; wait 430; quit",
                TimeSpan.FromMinutes(2), extraArgs: ["--warp", "MAP02", "--cheats"]);
            foreach (var line in output.Where(line => !line.StartsWith("Movie ")))
                TestContext.Out.WriteLine(line);
            Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));

            // Assert: one extra life (Blake's at his 4 after it), shown first; then GREAT SCORE's
            int extraLife = output.FindIndex(line => line.Contains("EXTRA LIFE BONUS!"));
            int greatScore = output.FindIndex(line => line.Contains("GREAT SCORE!") && line.Contains("1,000,000 POINT BONUS!"));
            Assert.That(extraLife, Is.GreaterThanOrEqualTo(0));
            Assert.That(output.Count(line => line.Contains("EXTRA LIFE BONUS!")), Is.EqualTo(1));
            Assert.That(greatScore, Is.GreaterThan(extraLife));
            Assert.That(output, Has.Some.StartsWith("Health is 99,"), "healed to 100 by GREAT SCORE, then 1 off");
            Assert.That(output, Has.Some.EqualTo("Score is 2500000"), "the million came on top of the 500,000");
            Assert.That(output, Has.Some.EqualTo("Score is 10000000"));
            Assert.That(output, Has.Some.Contains("ROLLED SCORE DISPLAY!"));
        }
        finally
        {
            try { Directory.Delete(workFolder, recursive: true); } catch (IOException) { }
        }
    }
}
