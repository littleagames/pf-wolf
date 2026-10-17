namespace PFWolf;

internal static partial class GameHost
{
    // The drop-down console drawn over the screen (Ui/ConsoleDrawing.cs)
    static ConsoleDrawing consoleDrawing = null!;       // made in InitServices

    internal static void RestoreBorderAfterConsole() => consoleDrawing.RestoreBorderAfterConsole();
    internal static void DrawConsole() => consoleDrawing.DrawConsole();
}
