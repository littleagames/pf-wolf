using System.Text;

namespace PFWolf.Startup;

/// <summary>
/// Writes through to stdout, keeping each line written until it's stopped: what startup prints
/// is shown on the signon screen as warnings, and starts the console's scrollback
/// </summary>
internal sealed class StartupOutput : TextWriter
{
    private readonly TextWriter original;
    private readonly List<string> lines = [];
    private readonly StringBuilder line = new();

    internal StartupOutput(TextWriter original) => this.original = original;

    public override Encoding Encoding => original.Encoding;

    /// <summary>Starts keeping what's written to stdout</summary>
    public static StartupOutput Capture()
    {
        var output = new StartupOutput(Console.Out);
        Console.SetOut(output);
        return output;
    }

    /// <summary>Puts stdout back, and returns the lines written to it (with an unfinished last one)</summary>
    public List<string> Stop()
    {
        Console.SetOut(original);
        lock (lines)
        {
            if (line.Length > 0)
                lines.Add(line.ToString());
            line.Clear();
            return [.. lines];
        }
    }

    public override void Write(char value)
    {
        original.Write(value);
        Keep(value);
    }

    public override void Write(string? value)
    {
        original.Write(value);
        foreach (var ch in value ?? "")
            Keep(ch);
    }

    public override void Flush() => original.Flush();

    private void Keep(char ch)
    {
        lock (lines)
        {
            if (ch == '\n')
            {
                lines.Add(line.ToString().TrimEnd('\r'));
                line.Clear();
            }
            else
                line.Append(ch);
        }
    }
}
