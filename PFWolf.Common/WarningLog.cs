namespace PFWolf;

/// <summary>
/// Where the shared code's warnings go (an asset that can't be read, a bad YAML value). The game
/// leaves them on stdout, which its signon screen captures; the editor shows them in its own list.
/// </summary>
public static class WarningLog
{
    public static Action<string> Sink { get; set; } = Console.WriteLine;

    public static void Write(string message) => Sink(message);
}
