using System.Text;

namespace Wolf3D.Managers;

[Flags]
internal enum ConsoleCommandFlags
{
    None = 0,
    /// <summary>Only runs once cheats are unlocked (the Backspace+LShift+Alt debug combo).</summary>
    Cheat = 1,
    /// <summary>Only runs while a level is loaded and the player exists.</summary>
    RequiresLevel = 2,
}

internal record ConsoleCommand(
    string Name,
    string Help,
    string Usage,
    Action<string[]> Run,
    ConsoleCommandFlags Flags = ConsoleCommandFlags.None,
    string[]? Aliases = null);

/// <summary>
/// The in-game command console: a registry of named commands, a scrollback buffer of output lines
/// and the history of entered lines. Commands are registered at startup (see
/// Program.RegisterConsoleCommands) and run through <see cref="Execute"/>, which accepts several
/// `;`-separated commands per line and double-quoted arguments containing spaces.
/// </summary>
internal class ConsoleManager
{
    internal const int MaxScrollback = 256;
    internal const int MaxHistory = 64;

    private readonly Dictionary<string, ConsoleCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _scrollback = [];
    private readonly List<string> _history = [];

    /// <summary>Whether cheat-flagged commands may run. Wired to Program.DebugOk at registration.</summary>
    internal Func<bool> CheatsEnabled { get; set; } = () => false;

    /// <summary>Whether a level is loaded. Wired to the player's existence at registration.</summary>
    internal Func<bool> LevelLoaded { get; set; } = () => false;

    internal bool IsOpen { get; set; }

    internal IReadOnlyList<string> Scrollback => _scrollback;
    internal IReadOnlyList<string> History => _history;

    /// <summary>Every registered command once, ordered by name (aliases are not repeated).</summary>
    internal IEnumerable<ConsoleCommand> Commands =>
        _commands.Values.Distinct().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

    internal void Register(ConsoleCommand command)
    {
        _commands[command.Name] = command;
        foreach (var alias in command.Aliases ?? [])
            _commands[alias] = command;
    }

    internal bool TryGetCommand(string name, out ConsoleCommand command) =>
        _commands.TryGetValue(name, out command!);

    /// <summary>Adds text to the scrollback (one entry per line) and mirrors it to stdout.</summary>
    internal void Print(string text)
    {
        foreach (var line in text.Replace("\r", "").Split('\n'))
        {
            _scrollback.Add(line);
            Console.WriteLine(line);
        }

        if (_scrollback.Count > MaxScrollback)
            _scrollback.RemoveRange(0, _scrollback.Count - MaxScrollback);
    }

    internal void Clear() => _scrollback.Clear();

    /// <summary>
    /// Runs a line typed by the user: echoes it, records it in history, then executes each
    /// `;`-separated command in turn.
    /// </summary>
    internal void Submit(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        Print($"] {line}");

        if (_history.Count == 0 || _history[^1] != line)
            _history.Add(line);
        if (_history.Count > MaxHistory)
            _history.RemoveAt(0);

        Execute(line);
    }

    /// <summary>Executes every `;`-separated command on the line without echoing it.</summary>
    internal void Execute(string line)
    {
        foreach (var args in Tokenize(line))
        {
            if (args.Length > 0)
                ExecuteCommand(args[0], args[1..]);
        }
    }

    private void ExecuteCommand(string name, string[] args)
    {
        if (!_commands.TryGetValue(name, out var command))
        {
            Print($"Unknown command \"{name}\". Type \"help\" for a list of commands.");
            return;
        }

        if (command.Flags.HasFlag(ConsoleCommandFlags.Cheat) && !CheatsEnabled())
        {
            Print($"\"{command.Name}\" is a cheat; cheats are not enabled.");
            return;
        }

        if (command.Flags.HasFlag(ConsoleCommandFlags.RequiresLevel) && !LevelLoaded())
        {
            Print($"\"{command.Name}\" can only be used during a level.");
            return;
        }

        try
        {
            command.Run(args);
        }
        catch (Exception ex)
        {
            // A bad argument shouldn't take the game down with it.
            Print($"{command.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Splits a line into commands on `;` and each command into arguments on whitespace. Double
    /// quotes group words into one argument (and protect `;` inside them); the quotes are removed.
    /// </summary>
    internal static List<string[]> Tokenize(string line)
    {
        var commands = new List<string[]>();
        var args = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false, inToken = false;

        void EndToken()
        {
            if (inToken)
                args.Add(current.ToString());
            current.Clear();
            inToken = false;
        }

        void EndCommand()
        {
            EndToken();
            if (args.Count > 0)
                commands.Add([.. args]);
            args.Clear();
        }

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                inToken = true;     // "" is a valid, empty argument
            }
            else if (inQuotes)
                current.Append(c);
            else if (c == ';')
                EndCommand();
            else if (char.IsWhiteSpace(c))
                EndToken();
            else
            {
                current.Append(c);
                inToken = true;
            }
        }

        EndCommand();
        return commands;
    }
}
