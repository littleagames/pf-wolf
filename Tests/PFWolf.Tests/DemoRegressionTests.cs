using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// Plays every demo of a game with the console's `demotest` and checks each ends exactly as it did
/// before (score, kills, the player's spot, a hash of every actor): a change to the game's rules
/// shows up here. The built game runs on its own, headless (SDL's dummy drivers), in a temporary
/// folder holding the release's data files and a fresh pfwolf.pk3 zipped from pfwolf-pk3/.
///
/// Needs the game's data files: from PFWOLF_GAMEDATA, else the game's own x64 build folder. A game
/// whose files aren't there (or are another version, by md5) is skipped.
///
/// The expected results are Baselines/demotest-{pack}.txt. A change that's meant to change how the
/// game plays updates them: run with PFWOLF_UPDATE_BASELINES=1 and check the new files in. A
/// missing baseline is written on the first run.
/// </summary>
[Category("GameData")]
public class DemoRegressionTests
{
    // Releases with demos to play (Blake Stone and Planet Strike ship none)
    private static readonly string[] Releases = ["wolf3d", "spear", "wolf3d-shareware", "spear-demo"];

    private static IEnumerable<TestCaseData> ReleaseCases() =>
        Releases.Select(release => new TestCaseData(release).SetArgDisplayNames(release));

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-demotest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workFolder);
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

    [TestCaseSource(nameof(ReleaseCases))]
    public void Demos_Play_Out_As_They_Did_Before(string release)
    {
        // Arrange
        var dataFolder = CopyDataFiles(release);
        ZipFile.CreateFromDirectory(TestPaths.Pk3SourceFolder(), Path.Combine(_workFolder, "pfwolf.pk3"));

        // Act
        var (exitCode, output) = RunGame(release, "demotest; quit");
        var results = output.Where(line => line.StartsWith("demotest ")).ToList();

        // Assert
        Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));
        Assert.That(results, Is.Not.Empty, "demotest printed nothing");
        Assert.That(results, Has.None.Contains("no such demo"));

        var baselinePath = Path.Combine(TestPaths.RepoRoot(), "Tests", "PFWolf.Tests", "Baselines", $"demotest-{release}.txt");
        var update = Environment.GetEnvironmentVariable("PFWOLF_UPDATE_BASELINES") == "1";
        if (update || !File.Exists(baselinePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            File.WriteAllLines(baselinePath, results);
            Assert.Warn($"Wrote {baselinePath} from {dataFolder}; check it in");
            return;
        }

        Assert.That(results, Is.EqualTo(File.ReadAllLines(baselinePath).Where(line => line.Length > 0)),
            $"A demo ended differently from {baselinePath}. If the change is meant to change how the game plays, " +
            "run with PFWOLF_UPDATE_BASELINES=1 and check the new baseline in.");
    }

    /// <summary>
    /// Copies the release's data files (as gamepack-info names them) into the work folder from
    /// the first folder that has them all; ignores the test when none does. Returns that folder.
    /// </summary>
    private string CopyDataFiles(string release)
    {
        var info = PfWolfPk3Loader.ReadGamePackInfo(new DirectoryAssetSource(TestPaths.Pk3SourceFolder()))
            ?? throw new InvalidOperationException("pfwolf-pk3 has no gamepack-info");
        var files = (info.GetGamePack(release).FilePack?.FileLoaders ?? [])
            .Where(loader => !loader.Key.Equals("JamMovieFileLoader", StringComparison.OrdinalIgnoreCase))
            .SelectMany(loader => new[] { loader.Value.Header, loader.Value.Data, loader.Value.Dict })
            .Where(file => !string.IsNullOrWhiteSpace(file?.File))
            .Select(file => file!)
            .ToList();

        foreach (var folder in DataFolderCandidates())
        {
            if (!files.All(file => File.Exists(Path.Combine(folder, file.File!)) && Md5Matches(Path.Combine(folder, file.File!), file.Md5)))
                continue;

            foreach (var file in files)
                File.Copy(Path.Combine(folder, file.File!), Path.Combine(_workFolder, file.File!));
            return folder;
        }

        Assert.Ignore($"{release}'s data files ({string.Join(", ", files.Select(f => f.File))}) aren't in "
            + $"{string.Join(" or ", DataFolderCandidates())}; set PFWOLF_GAMEDATA to a folder that has them");
        return "";
    }

    private static IEnumerable<string> DataFolderCandidates()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            yield return fromEnvironment;

        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        foreach (var configuration in new[] { "Debug", "Release" })
            yield return Path.Combine(gameBin, configuration, "net10.0");
    }

    private static bool Md5Matches(string path, string? md5)
    {
        if (string.IsNullOrWhiteSpace(md5))
            return true;
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(MD5.HashData(stream)).Equals(md5.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The game built beside the tests, run headless on <paramref name="commands"/> in the work folder</summary>
    private (int ExitCode, List<string> Output) RunGame(string gamePack, string commands)
    {
        var exe = Path.Combine(TestContext.CurrentContext.TestDirectory, "PFWolf.exe");
        Assert.That(File.Exists(exe), Is.True, $"{exe} wasn't built beside the tests");

        var start = new ProcessStartInfo(exe)
        {
            WorkingDirectory = _workFolder,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // Settings, saves and recorded demos all in the work folder, so the player's own aren't touched or used
        foreach (var arg in new[]
        {
            "--nowait", "--game", gamePack,
            "--configdir", Path.Combine(_workFolder, "config"),
            "--savedir", Path.Combine(_workFolder, "saves"),
            "--demodir", Path.Combine(_workFolder, "demos"),
            "--exec", commands,
        })
            start.ArgumentList.Add(arg);
        start.Environment["SDL_VIDEODRIVER"] = "dummy";
        start.Environment["SDL_AUDIODRIVER"] = "dummy";

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(Timeout))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"The game didn't finish within {Timeout.TotalMinutes} minutes");
        }

        var output = (stdout.Result + stderr.Result)
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        return (process.ExitCode, output);
    }
}
