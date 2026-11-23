using System.Text.RegularExpressions;

namespace PFWolf.Tests;

/// <summary>
/// Blake Stone's electro-sphere in the real game, headless, on AOG mission 1 floor 1 (MAP02): one
/// held still on the player's tile zaps them for 4, at most once every 3 tics (monster.touchtics).
/// bstone zaps every frame, 280 health a second at 70 frames, which kills Blake as a sphere
/// passes through him; DOS drew far fewer frames. Needs the Aliens of Gold data files.
/// </summary>
[Category("GameData")]
public class BlakeElectroSphereGameTests
{
    [Test]
    public void A_Sphere_Touching_The_Player_Zaps_Every_Third_Tic()
    {
        var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-spheres-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);
        try
        {
            // Act: a sphere put on the player's tile (away from the barriers' reach) and held
            // there for 60 tics; `hurt 1` reads the health out
            HeadlessGame.PrepareFolder(workFolder, "blake-aog");
            var (exitCode, output) = HeadlessGame.Run(workFolder, "blake",
                "wait 5; killall; tp 13 31; summon ElectroSphere 0 13 31; "
                + "lua for _, s in ipairs(game.actors('ElectroSphere')) do if s.state ~= 'Death' then s.speed = 0 end end; "
                + "wait 60; hurt 1; quit",
                TimeSpan.FromMinutes(2), extraArgs: ["--warp", "MAP02", "--cheats"]);
            foreach (var line in output.Where(line => !line.StartsWith("Movie ")))
                TestContext.Out.WriteLine(line);
            Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));

            // Assert: about 20 zaps of 4 in 60 tics -- fewer when a frame runs 2 tics, so the
            // countdown goes 3, 1, -1 -- not 60: the player's alive
            var health = output.Where(line => line.StartsWith("Health is "))
                .Select(line => int.Parse(Regex.Match(line, @"^Health is (\d+)").Groups[1].Value)).ToList();
            Assert.That(health, Has.Count.EqualTo(1));
            int taken = 100 - health[0] - 1;
            Assert.That(taken % 4, Is.Zero, "4 points a zap");
            Assert.That(taken / 4, Is.InRange(14, 22), "a zap every third tic");
        }
        finally
        {
            try { Directory.Delete(workFolder, recursive: true); } catch (IOException) { }
        }
    }
}
