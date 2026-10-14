namespace PFWolf.Startup;

/// <summary>
/// A crash writes the error to crash.log beside the exe (or in the temp folder when that can't
/// be written) and keeps the console open, so a double-clicked exe doesn't vanish with it.
/// </summary>
internal static class CrashReport
{
    internal const string FileName = "crash.log";

    /// <summary>Reports every crash from here on</summary>
    internal static void Install() => AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var report = Text(e.ExceptionObject, DateTime.Now);
        var logPath = Save(report, [AppContext.BaseDirectory, Path.GetTempPath()]);

        try
        {
            Console.Error.WriteLine(report);
            if (logPath != null)
                Console.Error.WriteLine($"Saved to {logPath}");
            if (!Console.IsInputRedirected)
            {
                Console.Error.WriteLine("Press any key to exit.");
                Console.ReadKey(true);
            }
        }
        catch
        {
            // No console to report to: the log is all there is
        }
    }

    /// <summary>What crash.log says</summary>
    internal static string Text(object exception, DateTime when) =>
        $"PFWolf crashed at {when:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{exception}{Environment.NewLine}";

    /// <summary>Writes the report to crash.log in the first of the folders that can take it: its path, or null if none could</summary>
    internal static string? Save(string report, IEnumerable<string> folders)
    {
        foreach (var folder in folders)
        {
            try
            {
                var logPath = Path.Combine(folder, FileName);
                File.WriteAllText(logPath, report);
                return logPath;
            }
            catch
            {
                // on to the next folder
            }
        }
        return null;
    }
}
