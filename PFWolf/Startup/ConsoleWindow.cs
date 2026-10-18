using System.Runtime.InteropServices;

namespace PFWolf.Startup;

/// <summary>
/// The exe is a Windows app, so it starts without a console window: --console opens one for
/// stdout and stderr. Output redirected to a file or pipe (scripted runs, tests) needs no window
/// and goes there either way.
/// </summary>
internal static partial class ConsoleWindow
{
    internal const string Option = "--console";

    private const int StdOutputHandle = -11;
    private const uint FileTypeDisk = 1;
    private const uint FileTypePipe = 3;

    /// <summary>Whether a console window was opened</summary>
    internal static bool IsOpen { get; private set; }

    /// <summary>Whether what's written to stdout is seen somewhere: a console window, a file or a pipe</summary>
    internal static bool HasOutput => !OperatingSystem.IsWindows() || IsOpen || IsOutputRedirected();

    /// <summary>
    /// Opens a console window when the command line asks for one. Before anything touches
    /// Console.Out or Console.Error, which take the standard handles they first see.
    /// </summary>
    internal static void OpenIfAsked(string[] args)
    {
        if (!OperatingSystem.IsWindows() || !args.Contains(Option, StringComparer.OrdinalIgnoreCase))
            return;
        if (IsOutputRedirected())
            return;
        IsOpen = AllocConsole();
    }

    private static bool IsOutputRedirected()
    {
        var handle = GetStdHandle(StdOutputHandle);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            return false;
        var type = GetFileType(handle);
        return type == FileTypeDisk || type == FileTypePipe;
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllocConsole();

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetStdHandle(int stdHandle);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetFileType(IntPtr handle);
}
