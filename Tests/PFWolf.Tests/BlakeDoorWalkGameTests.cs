using System.Globalization;
using System.Text.RegularExpressions;

namespace PFWolf.Tests;

/// <summary>
/// Blake Stone's enemies going through a door in the real game, headless, on AOG mission 1 floor
/// 1 (MAP02): a rent-a-cop that has seen the player at 50,21 chases them through the door at
/// 50,22 (the passage runs north-south). It used to wait for the door on its own tile, walk a
/// tile into the doorway once it opened and be put back on that tile's middle before going on:
/// a jump back of most of a tile. Needs the Aliens of Gold data files.
/// </summary>
[Category("GameData")]
public class BlakeDoorWalkGameTests
{
    [Test]
    public void An_Enemy_Chasing_Through_A_Door_Never_Jumps_Back()
    {
        var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-doorwalk-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);
        try
        {
            // Act: the cop sees the player, who then stands south of the door; its place each tic
            HeadlessGame.PrepareFolder(workFolder, "blake-aog");
            var (exitCode, output) = HeadlessGame.Run(workFolder, "blake",
                "wait 5; killall; god; tp 50 21 90; summon RentACop 270 50 19; wait 40; tp 50 24 90; "
                + "lua game.ontick(function(t) for _, a in ipairs(game.actors('RentACop')) do "
                + "if a.health > 0 and math.abs(a.x - 50.5) < 1.5 and a.y > 17 and a.y < 26 then "
                + "print('COP', a.x, a.y, a.tiley) end end end); wait 400; quit",
                TimeSpan.FromMinutes(2), extraArgs: ["--warp", "MAP02", "--cheats"]);
            Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));

            var places = output.Where(line => line.StartsWith("COP"))
                .Select(line => Regex.Split(line, @"\s+"))
                .Select(parts => (X: double.Parse(parts[1], CultureInfo.InvariantCulture),
                    Y: double.Parse(parts[2], CultureInfo.InvariantCulture), TileY: int.Parse(parts[3])))
                .ToList();

            // Assert: it got through the door, and never moved more than a few walking tics' worth at once
            Assert.That(places, Has.Some.Matches<(double X, double Y, int TileY)>(p => p.TileY >= 23), "through the door");
            for (int i = 1; i < places.Count; i++)
            {
                double step = Math.Max(Math.Abs(places[i].X - places[i - 1].X), Math.Abs(places[i].Y - places[i - 1].Y));
                Assert.That(step, Is.LessThan(0.25), $"a jump from {places[i - 1]} to {places[i]}");
            }
        }
        finally
        {
            try { Directory.Delete(workFolder, recursive: true); } catch (IOException) { }
        }
    }
}
