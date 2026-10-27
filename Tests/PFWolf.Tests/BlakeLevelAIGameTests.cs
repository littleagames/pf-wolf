using System.Text.RegularExpressions;

namespace PFWolf.Tests;

/// <summary>
/// Blake Stone's wall outlets and Goldfire's warp sites (Managers/LevelAI.cs) in the real game,
/// headless: their waits through a save and load. Needs the Aliens of Gold data files.
/// </summary>
[Category("GameData")]
public class BlakeLevelAIGameTests
{
    [Test]
    public void Outlet_And_Warp_Site_Waits_Come_Back_With_A_Loaded_Game()
    {
        // Arrange: AOG mission 1 floor 3, six electro-alien outlets and two Goldfire sites (and
        // electro-spheres beside its elevators, which once hung the game there)
        var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-levelai-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);
        try
        {
            HeadlessGame.PrepareFolder(workFolder, "blake-aog");

            // Act: the waits when saved, 300 tics on, then a few tics after the load
            var (exitCode, output) = HeadlessGame.Run(workFolder, "blake",
                "wait 70; levelai; save aitest; wait 300; levelai; load aitest; wait 5; levelai; quit",
                TimeSpan.FromMinutes(2), extraArgs: ["--warp", "MAP04", "--cheats"]);
            var log = string.Join(Environment.NewLine, output.TakeLast(40));
            var waits = output.Where(line => line.StartsWith("Outlet ") || line.StartsWith("Warp sites "))
                .Select(line => int.Parse(Regex.Match(line, @"(?:: |wait )(\d+) tics").Groups[1].Value))
                .ToList();

            // Assert: three read-outs of 7 waits; after the load, each is its saved wait less the
            // same few tics, not a new random one or the one before the load
            Assert.That(exitCode, Is.Zero, log);
            Assert.That(waits, Has.Count.EqualTo(21), log);
            var (saved, later, loaded) = (waits[..7], waits[7..14], waits[14..]);
            var played = saved[0] - later[0];       // 300, and a tic or so for the save
            Assert.That(played, Is.InRange(300, 310), log);
            Assert.That(later.Zip(saved, (l, s) => s - l), Has.All.EqualTo(played), "every wait went down while played");
            var gone = saved[0] - loaded[0];
            Assert.That(gone, Is.InRange(1, 30), log);
            Assert.That(loaded.Zip(saved, (l, s) => s - l), Has.All.EqualTo(gone), log);
        }
        finally
        {
            try { Directory.Delete(workFolder, recursive: true); } catch (IOException) { }
        }
    }
}
