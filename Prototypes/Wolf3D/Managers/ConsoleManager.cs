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

/// <summary>
/// Suggests values for one argument while Tab-completing: gets the arguments typed before it and
/// its index, and returns every valid value (the console filters them by what's been typed).
/// </summary>
internal delegate IEnumerable<string> ConsoleCompleter(string[] previousArgs, int argIndex);

internal record ConsoleCommand(
    string Name,
    string Help,
    string Usage,
    Action<string[]> Run,
    ConsoleCommandFlags Flags = ConsoleCommandFlags.None,
    string[]? Aliases = null,
    ConsoleCompleter? Complete = null);

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
    internal const int MaxInputLength = 120;
    internal const int MaxExecDepth = 8;
    internal const int MaxListedCompletions = 40;

    private readonly Dictionary<string, ConsoleCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _scrollback = [];
    private readonly List<string> _history = [];
    private readonly Queue<Action> _deferred = new();
    private readonly Dictionary<ScanCodes, string> _binds = [];
    private int _execDepth;

    // History browsing: _historyIndex == _history.Count means "editing a new line", and the
    // line being edited is kept in _draft while Up/Down walk through older entries.
    private int _historyIndex;
    private string _draft = "";

    /// <summary>Whether cheat-flagged commands may run. Wired to Program.DebugOk at registration.</summary>
    internal Func<bool> CheatsEnabled { get; set; } = () => false;

    /// <summary>Whether a level is loaded. Wired to the player's existence at registration.</summary>
    internal Func<bool> LevelLoaded { get; set; } = () => false;

    /// <summary>A key's display name, as `bind` accepts it. Wired to SDL's scancode names at registration.</summary>
    internal Func<ScanCodes, string> KeyName { get; set; } = key => key.ToString();

    internal bool IsOpen { get; private set; }

    /// <summary>Whether the world stops while the console is open (the `con_pause` setting, saved in the config).</summary>
    internal bool PauseWhenOpen { get; set; } = true;

    /// <summary>True while the console is open and set to pause the world.</summary>
    internal bool IsPausingGame => IsOpen && PauseWhenOpen;

    /// <summary>The line being typed. Kept across open/close so a half-typed command survives.</summary>
    internal string InputLine { get; private set; } = "";

    /// <summary>Caret position in <see cref="InputLine"/>, 0..InputLine.Length.</summary>
    internal int Cursor { get; private set; }

    /// <summary>How many lines the view is scrolled back from the newest output (0 = bottom).</summary>
    internal int ScrollOffset { get; private set; }

    internal IReadOnlyList<string> Scrollback => _scrollback;
    internal IReadOnlyList<string> History => _history;

    /// <summary>Commands run when a key is pressed during play, keyed by the (InputManager.MapKey-mapped) key.</summary>
    internal IReadOnlyDictionary<ScanCodes, string> Binds => _binds;

    internal void Bind(ScanCodes key, string command) => _binds[key] = command;
    internal bool Unbind(ScanCodes key) => _binds.Remove(key);
    internal void UnbindAll() => _binds.Clear();

    /// <summary>The current binds as `bind` commands, for saving to a file that `exec` can load back.</summary>
    internal IEnumerable<string> GetBindCommands() =>
        _binds.OrderBy(b => KeyName(b.Key), StringComparer.OrdinalIgnoreCase)
            .Select(b => $"bind \"{KeyName(b.Key)}\" \"{b.Value}\"");

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

    internal void Clear()
    {
        _scrollback.Clear();
        ScrollOffset = 0;
    }

    internal void Open()
    {
        IsOpen = true;
        ScrollOffset = 0;
    }

    internal void Close() => IsOpen = false;

    /// <summary>
    /// Queues work to run from the play loop rather than inside the key handler that executed
    /// the command, for commands that show a blocking screen or otherwise need the frame done.
    /// </summary>
    internal void Defer(Action action) => _deferred.Enqueue(action);

    internal void RunDeferred()
    {
        while (_deferred.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Print(ex.Message);
            }
        }
    }

    /// <summary>
    /// Handles a key press while the console is open. Printable characters arrive separately
    /// through <see cref="HandleText"/>; this covers editing, history and scrolling keys.
    /// </summary>
    internal void HandleKey(ScanCodes key)
    {
        switch (key)
        {
            case ScanCodes.sc_Escape:
            case ScanCodes.sc_Grave:
                Close();
                break;

            case ScanCodes.sc_Return:
                var line = InputLine;
                SetInputLine("");
                _draft = "";
                _historyIndex = _history.Count;
                ScrollOffset = 0;
                Submit(line);
                break;

            case ScanCodes.sc_BackSpace:
                if (Cursor > 0)
                {
                    InputLine = InputLine.Remove(Cursor - 1, 1);
                    Cursor--;
                }
                break;
            case ScanCodes.sc_Delete:
                if (Cursor < InputLine.Length)
                    InputLine = InputLine.Remove(Cursor, 1);
                break;

            case ScanCodes.sc_LeftArrow:
                if (Cursor > 0)
                    Cursor--;
                break;
            case ScanCodes.sc_RightArrow:
                if (Cursor < InputLine.Length)
                    Cursor++;
                break;
            case ScanCodes.sc_Home:
                Cursor = 0;
                break;
            case ScanCodes.sc_End:
                Cursor = InputLine.Length;
                break;

            case ScanCodes.sc_UpArrow:
                if (_historyIndex > 0)
                {
                    if (_historyIndex == _history.Count)
                        _draft = InputLine;
                    SetInputLine(_history[--_historyIndex]);
                }
                break;
            case ScanCodes.sc_DownArrow:
                if (_historyIndex < _history.Count)
                {
                    _historyIndex++;
                    SetInputLine(_historyIndex == _history.Count ? _draft : _history[_historyIndex]);
                }
                break;

            case ScanCodes.sc_PgUp:
                ScrollOffset = Math.Min(ScrollOffset + 4, Math.Max(0, _scrollback.Count - 1));
                break;
            case ScanCodes.sc_PgDn:
                ScrollOffset = Math.Max(ScrollOffset - 4, 0);
                break;

            case ScanCodes.sc_Tab:
                CompleteAtCursor();
                break;
        }
    }

    /// <summary>
    /// Tab completion of the word before the cursor: a command name for the first word of a
    /// command, otherwise whatever that command's completer offers. One match is filled in
    /// (with a trailing space); several are extended to their longest shared prefix, or listed
    /// when there's nothing more in common.
    /// </summary>
    internal void CompleteAtCursor()
    {
        // Scan the text before the cursor the way Tokenize does, but keep the unfinished word.
        var args = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false, inToken = false;

        foreach (char c in InputLine[..Cursor])
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                inToken = true;
            }
            else if (inQuotes)
                current.Append(c);
            else if (c == ';')
            {
                args.Clear();
                current.Clear();
                inToken = false;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (inToken)
                    args.Add(current.ToString());
                current.Clear();
                inToken = false;
            }
            else
            {
                current.Append(c);
                inToken = true;
            }
        }

        // Right after a closing quote the word is finished; there's nothing to complete.
        if (!inQuotes && Cursor > 0 && InputLine[Cursor - 1] == '"')
            return;

        string partial = current.ToString();
        var matches = GetCompletions(args)
            .Where(c => c.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matches.Count == 0)
            return;

        if (matches.Count == 1)
        {
            ReplaceWordBeforeCursor(partial.Length, FormatCompletion(matches[0], inQuotes, finished: true));
            return;
        }

        string prefix = matches[0][..CommonPrefixLength(matches)];
        if (prefix.Length > partial.Length)
        {
            ReplaceWordBeforeCursor(partial.Length, FormatCompletion(prefix, inQuotes, finished: false));
            return;
        }

        var listed = string.Join("  ", matches.Take(MaxListedCompletions));
        Print(matches.Count > MaxListedCompletions ? $"{listed}  ... and {matches.Count - MaxListedCompletions} more" : listed);
    }

    private IEnumerable<string> GetCompletions(List<string> args)
    {
        if (args.Count == 0)
            return _commands.Keys;

        if (!_commands.TryGetValue(args[0], out var command) || command.Complete == null)
            return [];

        try
        {
            return command.Complete([.. args.Skip(1)], args.Count - 1).ToList();
        }
        catch (Exception)
        {
            return [];      // e.g. a completer that needs a level when none is loaded
        }
    }

    // Values with spaces or ';' only survive Tokenize inside quotes, so add them as needed.
    private static string FormatCompletion(string text, bool inQuotes, bool finished)
    {
        if (inQuotes)
            return finished ? text + "\" " : text;
        if (text.Contains(' ') || text.Contains(';'))
            return "\"" + text + (finished ? "\" " : "");
        return finished ? text + " " : text;
    }

    private static int CommonPrefixLength(List<string> values)
    {
        int length = values.Min(v => v.Length);
        for (int i = 0; i < length; i++)
        {
            char c = char.ToLowerInvariant(values[0][i]);
            if (values.Any(v => char.ToLowerInvariant(v[i]) != c))
                return i;
        }
        return length;
    }

    private void ReplaceWordBeforeCursor(int wordLength, string replacement)
    {
        int start = Cursor - wordLength;
        var line = InputLine[..start] + replacement + InputLine[Cursor..];
        if (line.Length > MaxInputLength)
            return;

        InputLine = line;
        Cursor = start + replacement.Length;
    }

    /// <summary>
    /// Inserts typed text at the cursor. Only printable ASCII is kept, since that's all the game
    /// fonts carry; ` and ~ are dropped because the toggle key also produces them as text.
    /// </summary>
    internal void HandleText(string text)
    {
        foreach (char c in text)
        {
            if (c < ' ' || c > '~' || c == '`' || c == '~' || InputLine.Length >= MaxInputLength)
                continue;

            InputLine = InputLine.Insert(Cursor, c.ToString());
            Cursor++;
        }
    }

    private void SetInputLine(string text)
    {
        InputLine = text;
        Cursor = text.Length;
    }

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
        _historyIndex = _history.Count;

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

    /// <summary>
    /// Executes each line of a script file (autoexec.cfg, binds.cfg, `exec`). Blank lines and
    /// lines starting with // or # are skipped. Returns false if the file doesn't exist.
    /// </summary>
    internal bool ExecFile(string path)
    {
        if (!File.Exists(path))
            return false;

        // A script that execs itself (directly or through others) would otherwise recurse forever.
        if (_execDepth >= MaxExecDepth)
        {
            Print($"exec: \"{path}\" is nested too deeply; skipped");
            return true;
        }

        _execDepth++;
        try
        {
            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("//") || line.StartsWith('#'))
                    continue;
                Execute(line);
            }
        }
        finally
        {
            _execDepth--;
        }

        return true;
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
