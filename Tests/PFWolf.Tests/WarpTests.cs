using System.Diagnostics;

namespace PFWolf.Tests;

/// <summary>
/// --warp, --skill and --start: the game built beside the tests, run headless in a built game
/// folder until it says what it did (it then plays on, so it's stopped)
/// </summary>
[Category("GameData")]
public class WarpTests
{
    private string _configFolder = "";

    [SetUp]
    public void MakeFolder()
    {
        _configFolder = Path.Combine(Path.GetTempPath(), "pfwolf-warp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configFolder);
    }

    [TearDown]
    public void RemoveFolder()
    {
        try
        {
            Directory.Delete(_configFolder, recursive: true);
        }
        catch (IOException)
        {
            // The stopped game may still be letting go of a file
        }
    }

    [TestCase("--warp map02 --skill 1 --start 30,57,90", "Warping to MAP02, skill 1", "Starting on 30,57")]
    [TestCase("--warp MAP02 --start 0,0", "Warping to MAP02, skill 3", "--start 0,0: tile 0,0 is not open floor")]
    [TestCase("--warp NOPE", "--warp NOPE: game-info doesn't list a level of that name", null)]
    public void Warp_Starts_The_Level_Asked_For(string arguments, string expected, string? alsoExpected)
    {
        // Arrange
        var gameFolder = new[] { "Debug", "Release" }
            .Select(configuration => Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64", configuration, "net10.0"))
            .FirstOrDefault(folder => File.Exists(Path.Combine(folder, "pfwolf.pk3")) && File.Exists(Path.Combine(folder, "GAMEMAPS.WL6")));
        if (gameFolder == null)
            Assert.Ignore("No built game folder with Wolf3D's data files; build PFWolf for x64");
        var exe = Path.Combine(TestContext.CurrentContext.TestDirectory, "PFWolf.exe");

        // Act
        var output = RunUntil(exe, gameFolder!, $"--game wolf3d --nowait --configdir \"{_configFolder}\" --savedir \"{_configFolder}\" --demodir \"{_configFolder}\" {arguments}",
            alsoExpected ?? expected);

        // Assert
        Assert.That(output, Has.Some.Contains(expected));
        if (alsoExpected != null)
            Assert.That(output, Has.Some.Contains(alsoExpected));
    }

    /// <summary>The game's output up to the line holding <paramref name="until"/> (or 30 seconds), then it's stopped</summary>
    private static List<string> RunUntil(string exe, string workingFolder, string arguments, string until)
    {
        var start = new ProcessStartInfo(exe, arguments)
        {
            WorkingDirectory = workingFolder,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["SDL_VIDEODRIVER"] = "dummy";
        start.Environment["SDL_AUDIODRIVER"] = "dummy";

        var lines = new List<string>();
        var seen = new ManualResetEventSlim();
        using var game = Process.Start(start)!;
        game.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null)
                return;
            lock (lines)
                lines.Add(e.Data);
            if (e.Data.Contains(until))
                seen.Set();
        };
        game.BeginOutputReadLine();
        game.BeginErrorReadLine();

        seen.Wait(TimeSpan.FromSeconds(30));
        if (!game.HasExited)
            game.Kill();
        game.WaitForExit();
        lock (lines)
            return [.. lines];
    }
}
