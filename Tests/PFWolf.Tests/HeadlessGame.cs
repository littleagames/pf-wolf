using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// Runs the game built beside the tests on its own, headless (SDL's dummy drivers), in a work
/// folder holding a release's data files and a fresh pfwolf.pk3 zipped from pfwolf-pk3/. For the
/// tests that play the real games (Category GameData).
/// </summary>
internal static class HeadlessGame
{
    /// <summary>
    /// Copies the release's data files (as gamepack-info names them) and a fresh pfwolf.pk3 into
    /// the work folder, from the first folder that has them all (PFWOLF_GAMEDATA, else the game's
    /// own x64 build folder); ignores the test when none does. Returns that folder.
    /// </summary>
    public static string PrepareFolder(string workFolder, string release)
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
                File.Copy(Path.Combine(folder, file.File!), Path.Combine(workFolder, file.File!));
            ZipFile.CreateFromDirectory(TestPaths.Pk3SourceFolder(), Path.Combine(workFolder, "pfwolf.pk3"));
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

    /// <summary>
    /// The game run on <paramref name="commands"/> in the work folder, its settings, saves and
    /// demos kept there (so the player's own aren't touched or used), with the mods given
    /// </summary>
    public static (int ExitCode, List<string> Output) Run(string workFolder, string gamePack, string commands,
        TimeSpan timeout, params string[] mods)
        => Run(workFolder, gamePack, commands, timeout, [], mods);

    /// <summary>The same, with more of the game's command line (--warp MAP09, say) before the mods</summary>
    public static (int ExitCode, List<string> Output) Run(string workFolder, string gamePack, string commands,
        TimeSpan timeout, IReadOnlyList<string> extraArgs, params string[] mods)
    {
        var exe = Path.Combine(TestContext.CurrentContext.TestDirectory, "PFWolf.exe");
        Assert.That(File.Exists(exe), Is.True, $"{exe} wasn't built beside the tests");

        var start = new ProcessStartInfo(exe)
        {
            WorkingDirectory = workFolder,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var args = new List<string>
        {
            "--nowait", "--game", gamePack,
            "--configdir", Path.Combine(workFolder, "config"),
            "--savedir", Path.Combine(workFolder, "saves"),
            "--demodir", Path.Combine(workFolder, "demos"),
            "--exec", commands,
        };
        args.AddRange(extraArgs);
        foreach (var mod in mods)
            args.AddRange(["--file", mod]);
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        start.Environment["SDL_VIDEODRIVER"] = "dummy";
        start.Environment["SDL_AUDIODRIVER"] = "dummy";

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            var last = (stdout.Result + stderr.Result).Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).TakeLast(20);
            Assert.Fail($"The game didn't finish within {timeout.TotalMinutes} minutes. Its last output:{Environment.NewLine}{string.Join(Environment.NewLine, last)}");
        }

        var output = (stdout.Result + stderr.Result)
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        return (process.ExitCode, output);
    }

    /// <summary>One step of a scripted demo: these buttons, turn and forward move, for so many tics</summary>
    public readonly record struct DemoStep(byte Buttons, sbyte Turn, sbyte Forward, int Tics);

    public const byte Attack = 1, Use = 8;

    /// <summary>id's demo format: the map, the length, then each tic's buttons, turn and forward move</summary>
    public static byte[] ScriptedDemo(byte map, params DemoStep[] steps)
    {
        var data = new List<byte> { map, 0, 0, 0 };
        foreach (var step in steps)
            for (var i = 0; i < step.Tics; i++)
                data.AddRange([step.Buttons, unchecked((byte)step.Turn), unchecked((byte)step.Forward)]);

        var bytes = data.ToArray();
        BitConverter.GetBytes((short)bytes.Length).CopyTo(bytes, 1);
        return bytes;
    }
}
