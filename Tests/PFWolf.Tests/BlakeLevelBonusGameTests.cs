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
}
