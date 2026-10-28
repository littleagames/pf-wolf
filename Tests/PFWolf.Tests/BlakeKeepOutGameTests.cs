using System.Text.RegularExpressions;

namespace PFWolf.Tests;

/// <summary>
/// Blake Stone's area 0 floors (floors monster-keep-out, bstone's ElevatorFloor) in the real
/// game, headless: enemies never walk onto them. Needs the Aliens of Gold data files.
/// </summary>
[Category("GameData")]
public class BlakeKeepOutGameTests
{
    [Test]
    public void Enemies_Chasing_The_Player_Never_Cross_Area_0()
    {
        // Arrange: AOG floor 1's room at 6-20,14-22 is cut in two by row 18, all area 0 (code
        // 108) or wall. The player stands north of it; the genetic guards south of it wake and
        // come for them, with a pro guard summoned beside them
        var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-keepout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);
        try
        {
            HeadlessGame.PrepareFolder(workFolder, "blake-aog");
            var commands = "wait 5; god; tp 8 16 270; summon ProGuard 90 9 21"
                + string.Concat(Enumerable.Range(0, 40).Select(_ => "; wait 10; actors Guard"))
                + "; quit";

            // Act: where the guards are every 10 tics, for 400 (`actors` marks one on a keep-out tile)
            var (exitCode, output) = HeadlessGame.Run(workFolder, "blake", commands,
                TimeSpan.FromMinutes(2), extraArgs: ["--warp", "MAP01", "--cheats"]);
            var log = string.Join(Environment.NewLine, output.TakeLast(40));
            var southOfRow = output.Select(line => Regex.Match(line, @"^\w+Guard at (\d+),19 "))
                .Count(m => m.Success && int.Parse(m.Groups[1].Value) is >= 6 and <= 20);

            // Assert: they came up to the row, and none ever stood on it
            Assert.That(exitCode, Is.Zero, log);
            Assert.That(southOfRow, Is.Positive, "no guard came up to row 18: " + log);
            Assert.That(output.Where(line => line.Contains("keep-out")), Is.Empty, log);
        }
        finally
        {
            try { Directory.Delete(workFolder, recursive: true); } catch (IOException) { }
        }
    }
}
