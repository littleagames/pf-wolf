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
        Assert.That(said[1], Does.Match(@"^Ambient: ambience/wind (4\d|5\d)%, fading in; ambience/hall (4\d|5\d)%, fading out \| layer 2: ambience/rain (4\d|5\d)%, fading in$"));
        Assert.That(said[2], Is.EqualTo("Ambient: ambience/wind 100% | layer 2: ambience/rain 100%"));     // the rain over the wind (ambient-2)
        Assert.That(said[3], Is.EqualTo("Ambient: ambience/hum 100%"));          // the generator room has no ambient-2: the rain's gone
        Assert.That(said[4], Is.EqualTo("No ambient sound"));
        Assert.That(said[5], Is.EqualTo("Ambient: ambience/hum 100%"));
        Assert.That(said[6], Is.EqualTo("Ambient: ambience/tense 100%"));
        Assert.That(said[7], Is.EqualTo("Ambient: ambience/alarm 100%"));
        Assert.That(said[8], Is.EqualTo("No ambient sound"));
        Assert.That(said[9], Is.EqualTo("Ambient: ambience/hall 100%"));
    }

    [Test]
    public void Things_Loop_Their_Sounds_From_Where_They_Are()
    {
        var said = Play(IsLoops,
            "wait 60", "god", "loops",                  // 1. the hall: the generators are too far off
            "tp 18 26", "wait 35", "loops",             // 2. the generator room: both, at full (well inside their radius)
            "lua A_GeneratorUse(game.actors('AmbienceGenerator')[1])", "wait 35", "loops",      // 3. switched off: loop-sound none
            "lua A_GeneratorUse(game.actors('AmbienceGenerator')[1])", "wait 35", "loops",      // 4. on again
            "tp 38 16", "wait 35", "loops",             // 5. the sewer's near end: the water, toward the edge of its radius
            "tp 46 16", "wait 35", "loops");            // 6. beside it

        Assert.That(said, Has.Count.EqualTo(12), string.Join(Environment.NewLine, said));
        Assert.That(said[0], Is.EqualTo("No looping sounds heard (3 out of reach)"));
        Assert.That(said[1], Is.EqualTo("Looping sounds (2 of 8 voices, 3 emitters):"));
        Assert.That(said[2..4], Has.All.Match(@"^\d: AmbienceGenerator ambience/generator, [23]\.\d tiles off, level 1\.00$"));
        Assert.That(said[4], Is.EqualTo("No looping sounds heard (1 out of reach)"));
        Assert.That(said[5], Is.EqualTo("Looping sounds (2 of 8 voices, 3 emitters):"));
        Assert.That(said[8], Is.EqualTo("Looping sounds (1 of 8 voices, 3 emitters):"));
        Assert.That(said[9], Does.Match(@"^\d: AmbienceWater ambience/water, 10\.0 tiles off, level 0\.6\d$"));     // its edge: (12 - 10) / 3
        Assert.That(said[10], Is.EqualTo("Looping sounds (1 of 8 voices, 3 emitters):"));
        Assert.That(said[11], Does.Match(@"^\d: AmbienceWater ambience/water, 2\.0 tiles off, level 1\.00$"));
    }

    [Test]
    public void A_Shut_Door_Muffles_What_Is_Beyond_It()
    {
        var said = Play(line => IsLoops(line) || line.StartsWith("EFX"),
            "wait 60", "god", "soundinfo",              // 0. OpenAL Soft's null device has EFX too
            "tp 28 26", "wait 35", "loops",             // 1. the hall, by the generator room's shut door: one in reach, muffled and dulled
            "s_muffle 0", "wait 35", "loops",           // 2. muffling off: back up over a few tics, the low-pass taken off
            "s_muffle 1", "wait 35",
            "tp 22 26", "wait 35", "loops");            // 3. in the room: nothing between

        Assert.That(said, Has.Count.EqualTo(8), string.Join(Environment.NewLine, said));
        Assert.That(said[0], Does.Match(@"^EFX 1\.\d+, \d+ auxiliary sends? a source$"));
        Assert.That(said[2], Does.Match(@"^\d: AmbienceGenerator ambience/generator, 7\.3 tiles off, level 0\.\d\d, muffled to 0\.35 and dulled$"));
        Assert.That(said[4], Does.Match(@"^\d: AmbienceGenerator ambience/generator, 7\.3 tiles off, level 0\.\d\d$"));
        Assert.That(said[5], Is.EqualTo("Looping sounds (2 of 8 voices, 3 emitters):"));
        Assert.That(said[6..8], Has.None.Contains("muffled"));
    }

    private static bool IsAmbient(string line) => line.StartsWith("Ambient: ") || line == "No ambient sound";

    private static bool IsLoops(string line) => line.StartsWith("Looping sounds") || line.StartsWith("No looping sounds")
        || Regex.IsMatch(line, @"^\d: Ambience");

    private List<string> Play(params string[] commands) => Play(IsAmbient, commands);

    // Warps to the demo's MAP01 and runs the commands, a wait after each; what the commands printed that the filter takes
    private List<string> Play(Func<string, bool> printed, params string[] commands)
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
            return [.. output.Select(line => Regex.Replace(line, @"^\S*> ?", "")).Where(printed)];
        }
        finally
        {
            Environment.SetEnvironmentVariable("ALSOFT_DRIVERS", previous);
        }
    }
}
