using System.Text.RegularExpressions;

namespace PFWolf.Tests;

/// <summary>
/// Blake Stone's patrollers as the map spawns them, headless, on AOG mission 4 floor 2 (MAP48).
/// bstone's SpawnPatrol tries the first step (TryWalk) where Wolf3D's stepped blindly: the
/// volatile transport placed at 38,29 faces the wall at 39,29 from the elevator floor (area 0,
/// where TryWalk never moves an enemy), so it stays parked; it used to start inside that wall and
/// come out of it onto the canister at 39,30. Patrollers off the elevator floor (MAP78's) still
/// go their way. Needs the Aliens of Gold data files.
/// </summary>
[Category("GameData")]
public class BlakePatrolGameTests
{
    private static List<string> Play(string map, string commands)
    {
        var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-patrol-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);
        try
        {
            HeadlessGame.PrepareFolder(workFolder, "blake-aog");
            var (exitCode, output) = HeadlessGame.Run(workFolder, "blake", commands + "; quit",
                TimeSpan.FromMinutes(2), extraArgs: ["--warp", map, "--cheats"]);
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

    // Each `actors VolatileTransport` in turn: where the patrolling ones (not the Fixed) stand
    private static List<List<string>> Patrollers(List<string> output)
    {
        var lists = new List<List<string>> { new() };
        foreach (var line in output)
        {
            if (Regex.Match(line, @"^VolatileTransport at (\d+,\d+) ") is { Success: true } match)
                lists[^1].Add(match.Groups[1].Value);
            else if (line.EndsWith(" actor(s)") && lists[^1].Count > 0)
                lists.Add([]);
        }
        return [.. lists.Where(list => list.Count > 0)];
    }

    [Test]
    public void A_Transport_Facing_A_Wall_From_The_Elevator_Floor_Stays_Parked()
    {
        // Act
        var seen = Patrollers(Play("MAP48", "wait 5; actors VolatileTransport; wait 300; actors VolatileTransport"));

        // Assert: at 38,29 (not 39,29, in the wall) both times
        Assert.That(seen, Has.Count.EqualTo(2));
        Assert.That(seen[0], Does.Contain("38,29").And.Not.Contain("39,29"), "never stepped into the wall");
        Assert.That(seen[1], Does.Contain("38,29"), "still parked");
    }

    [Test]
    public void Transports_Off_The_Elevator_Floor_Still_Patrol()
    {
        // Act
        var seen = Patrollers(Play("MAP78", "wait 5; actors VolatileTransport; wait 300; actors VolatileTransport"));

        // Assert: all 13 on their way (none where it was 300 tics before)
        Assert.That(seen, Has.Count.EqualTo(2));
        Assert.That(seen[0], Has.Count.EqualTo(13));
        Assert.That(seen[1].Intersect(seen[0]), Is.Empty, "every one moved");
    }
}
