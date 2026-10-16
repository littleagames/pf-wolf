namespace PFWolf;

// The old Tab+key debug keys are now console commands (GameHost.ConsoleCommands.cs); the screens
// they show and save are Ui/DebugScreens.cs.
internal static partial class GameHost
{
    static DebugScreens debugScreens = null!;       // made in InitServices
    static DebugCommands debugCommands = null!;     // (Commands/DebugCommands.cs)

    // Set by the "screenshot" command; ThreeDRefresh takes the shot before drawing the console.
    static bool screenshotPending { get => debugScreens.screenshotPending; set => debugScreens.screenshotPending = value; }

    internal static string TakeScreenshot() => debugScreens.TakeScreenshot();
    internal static void BasicOverhead() => debugScreens.BasicOverhead();
}