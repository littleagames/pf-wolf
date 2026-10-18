using System.Diagnostics;
using PFWolf.Assets.Demos;

namespace PFWolf.Editor.Editing;

/// <summary>
/// Has the game play a demo and write down what happened (the console's demotrace), with no
/// window or sound, for the demo browser. Its settings, saves and recorded demos go in a
/// folder of its own, so the player's aren't touched.
/// </summary>
public static class DemoTraceRunner
{
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// The trace of <paramref name="demo"/> played by the game in <paramref name="gameFolder"/> with
    /// the mods; throws InvalidOperationException saying why when the game doesn't write one
    /// </summary>
    public static async Task<DemoTrace> RunAsync(string exe, string gameFolder, string gamePackId, IEnumerable<string> mods,
        byte[] demo, CancellationToken cancel = default)
    {
        var work = Path.Combine(Path.GetTempPath(), "pfwolf-editor-demotrace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var demoPath = Path.Combine(work, "demo.dmo");
            var tracePath = Path.Combine(work, "trace.txt");
            await File.WriteAllBytesAsync(demoPath, demo, cancel);

            var start = new ProcessStartInfo(exe)
            {
                WorkingDirectory = gameFolder,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var args = new List<string>
            {
                "--nowait", "--game", gamePackId,
                "--configdir", Path.Combine(work, "config"),
                "--savedir", Path.Combine(work, "saves"),
                "--demodir", Path.Combine(work, "demos"),
                "--exec", $"demotrace \"{demoPath}\" \"{tracePath}\"; quit",
            };
            foreach (var mod in mods)
                args.AddRange(["--file", mod]);
            foreach (var arg in args)
                start.ArgumentList.Add(arg);
            start.Environment["SDL_VIDEODRIVER"] = "dummy";
            start.Environment["SDL_AUDIODRIVER"] = "dummy";

            using var process = Process.Start(start) ?? throw new InvalidOperationException($"Couldn't start {exe}");
            var stdout = process.StandardOutput.ReadToEndAsync(cancel);
            var stderr = process.StandardError.ReadToEndAsync(cancel);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            timeout.CancelAfter(Timeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                if (cancel.IsCancellationRequested)
                    throw;
                throw new InvalidOperationException($"The game didn't finish the demo within {Timeout.TotalMinutes:0} minutes");
            }

            var output = (await stdout) + (await stderr);
            if (!File.Exists(tracePath))
                throw new InvalidOperationException("The game wrote no trace. " + LastLines(output));
            var trace = DemoTrace.Read(tracePath);
            if (trace.FrameCount == 0)
                throw new InvalidOperationException($"The game didn't play the demo ({trace.EndedHow}). " + LastLines(output));
            return trace;
        }
        finally
        {
            try
            {
                Directory.Delete(work, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Only a temp folder; the game may still be letting go of a file
            }
        }
    }

    // What the game said last, for why it went wrong
    private static string LastLines(string output)
    {
        var lines = output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" | ", lines.TakeLast(4));
    }
}
