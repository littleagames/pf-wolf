namespace PFWolf.Tests;

/// <summary>
/// The Detonator Demo (examples/mods/detonator-demo) played headless in Planet Strike: the
/// fission detonator picked up, dropped on the red spot by the security cube as the slot key
/// would (A_DropDetonator, from Lua), and left to go off. Round the spot: a plain door north
/// (24,23), a one-way door west (23,24), a red-locked door south (24,25), and a plain door two
/// tiles off (26,23). Needs the Planet Strike data files.
/// </summary>
[Category("GameData")]
public class DetonatorDemoGameTests
{
    private static string Mod => Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "detonator-demo");

    private const string Drop = "lua print(game.trigger(1, 'A_DropDetonator', 'PlasmaDetonatorArmed', 'SecurityCube', 'PlasmaDetonator'))";

    [Test]
    public void The_Detonator_Blows_Open_Only_The_Plain_Door_Beside_It_And_It_Stays_Open_After_A_Load()
    {
        var workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-detonator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);
        try
        {
            HeadlessGame.PrepareFolder(workFolder, "blake-ps");

            // Act: the detonator picked up, dropped on the spot, the player well off down the hall
            // for its 4-second fuse and blast; the doors read out, then again after a save and load
            var (exitCode, output) = HeadlessGame.Run(workFolder, "planetstrike",
                $"wait 5; tp 33 24; wait 2; tp 24 24; {Drop}; tp 31 24; wait 300; doors; actors SecurityCube; "
                + "save blown; wait 5; load blown; wait 5; doors; quit",
                TimeSpan.FromMinutes(2), extraArgs: ["--warp", "MAP01", "--cheats", "--file", Mod]);
            var log = string.Join(Environment.NewLine, output.Where(line => !line.StartsWith("Movie ")).TakeLast(40));
            var doors = output.Where(line => line.StartsWith("door ")).ToList();

            // Assert: dropped where it was allowed, the cube broken, and the doors as bstone's
            // TryBlastDoor leaves them, twice over
            Assert.That(exitCode, Is.Zero, log);
            Assert.That(output, Has.Some.EqualTo("WARNING: FISSION DETONATOR DROPPED!"), log);
            Assert.That(output, Has.Some.Matches<string>(line => line.StartsWith("SecurityCube at 26,25  state Death")), log);
            Assert.That(doors, Has.Count.EqualTo(8), log);
            foreach (var readout in new[] { doors[..4], doors[4..] })
            {
                Assert.That(readout[0], Is.EqualTo("door 0 at 24,23: Open  blown open"), "plain, beside it\n" + log);
                Assert.That(readout[1], Is.EqualTo("door 1 at 26,23: Closed"), "two tiles off\n" + log);
                Assert.That(readout[2], Is.EqualTo("door 2 at 23,24: Closed"), "one-way\n" + log);
                Assert.That(readout[3], Is.EqualTo("door 3 at 24,25: Closed  locked (RedCard)"), "locked\n" + log);
            }
        }
        finally
        {
            try { Directory.Delete(workFolder, recursive: true); } catch (IOException) { }
        }
    }
}
