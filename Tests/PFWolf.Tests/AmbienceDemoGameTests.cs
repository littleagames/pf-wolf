using System.Text.RegularExpressions;

namespace PFWolf.Tests;

/// <summary>
/// The ambience demo (examples/mods/ambience-demo) in play (Wolf3D): the player is moved from
/// room to room (tp) and the ambient command says what the ambient loop plays, so each room's
/// zone sound, its fade, the stealth variant and the script's hold over the generator room's hum
/// are checked as the game runs them. OpenAL runs on its null device, so nothing is heard. Needs
/// Wolf3D's data files, as DemoRegressionTests does.
/// </summary>
[Category("GameData")]
public class AmbienceDemoGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-ambience-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workFolder);
        HeadlessGame.PrepareFolder(_workFolder, "wolf3d");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_workFolder, recursive: true);
        }
        catch (IOException)
        {
            // The game may still be letting go of a file; it's only a temp folder
        }
    }

    [Test]
    public void Each_Room_Plays_Its_Ambience_Fading_From_The_Last()
    {
        var said = Play(
            "wait 60", "god", "ambient",                // 1. the hall, faded in
            "tp 16 14", "wait 35", "ambient",           // 2. the courtyard, half way over from the hall (both at its 70 tics)
            "wait 70", "ambient",                       // 3. the courtyard
            "tp 18 26", "wait 70", "ambient",           // 4. the generator room
            "lua A_GeneratorUse(game.actors('AmbienceGenerator')[1])", "wait 140", "ambient",    // 5. switched off: silent
            "lua A_GeneratorUse(game.actors('AmbienceGenerator')[1])", "wait 140", "ambient",    // 6. on again
            "tp 36 26", "wait 90", "ambient",           // 7. the guard post, behind the guards
            "tp 46 24 180", "wait 280", "ambient",      // 8. in front of one: seen
            "tp 16 35", "wait 70", "ambient",           // 9. the radio room: none
            "tp 30 35", "wait 70", "tp 40 35", "wait 20", "ambient");    // 10. the elevator room: no zone, the hall's goes on

        Assert.That(said, Has.Count.EqualTo(10), string.Join(Environment.NewLine, said));
        Assert.That(said[0], Is.EqualTo("Ambient: ambience/hall 100%"));
        Assert.That(said[1], Does.Match(@"^Ambient: ambience/wind (4\d|5\d)%, fading in; ambience/hall (4\d|5\d)%, fading out$"));
        Assert.That(said[2], Is.EqualTo("Ambient: ambience/wind 100%"));
        Assert.That(said[3], Is.EqualTo("Ambient: ambience/hum 100%"));
        Assert.That(said[4], Is.EqualTo("No ambient sound"));
        Assert.That(said[5], Is.EqualTo("Ambient: ambience/hum 100%"));
        Assert.That(said[6], Is.EqualTo("Ambient: ambience/tense 100%"));
        Assert.That(said[7], Is.EqualTo("Ambient: ambience/alarm 100%"));
        Assert.That(said[8], Is.EqualTo("No ambient sound"));
        Assert.That(said[9], Is.EqualTo("Ambient: ambience/hall 100%"));
    }

    // Warps to the demo's MAP01 and runs the commands, a wait after each; what the ambient command printed
    private List<string> Play(params string[] commands)
    {
        var demo = Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "ambience-demo");
        var previous = Environment.GetEnvironmentVariable("ALSOFT_DRIVERS");
        Environment.SetEnvironmentVariable("ALSOFT_DRIVERS", "null");      // the game's OpenAL Soft plays to nothing
        try
        {
            var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", string.Join("; ", commands) + "; quit", Timeout,
                ["--warp", "MAP01", "--cheats"], demo);

            var log = string.Join(Environment.NewLine, output.TakeLast(40));
            Assert.That(exitCode, Is.Zero, log);
            Assert.That(output, Has.None.Contains("Exception").And.None.Contains("left out").And.None.Contains("nothing to play")
                .And.None.Contains("Sound is off"), log);
            TestContext.Out.WriteLine(log);
            return [.. output.Select(line => Regex.Replace(line, @"^\S*> ?", ""))
                .Where(line => line.StartsWith("Ambient: ") || line == "No ambient sound")];
        }
        finally
        {
            Environment.SetEnvironmentVariable("ALSOFT_DRIVERS", previous);
        }
    }
}
