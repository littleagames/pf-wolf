namespace Wolf3D;

internal partial class Program
{
    /// <summary>
    /// Registers the in-game console's commands. Commands that need Program's game state live
    /// here so they can reach it directly, the same way the actor actions do.
    /// </summary>
    internal static void RegisterConsoleCommands()
    {
        _consoleManager.CheatsEnabled = () => DebugOk != 0;
        _consoleManager.LevelLoaded = () => _mapManager.Player != null;

        _consoleManager.Register(new ConsoleCommand(
            "help", "Lists commands, or describes one.", "help [command]", Cmd_Help, Aliases: ["?"]));

        _consoleManager.Register(new ConsoleCommand(
            "echo", "Prints its arguments.", "echo <text...>",
            args => _consoleManager.Print(string.Join(' ', args))));

        _consoleManager.Register(new ConsoleCommand(
            "clear", "Clears the console output.", "clear", _ => _consoleManager.Clear(), Aliases: ["cls"]));

        _consoleManager.Register(new ConsoleCommand(
            "history", "Lists previously entered lines.", "history", Cmd_History));
    }

    private static void Cmd_Help(string[] args)
    {
        if (args.Length > 0)
        {
            if (!_consoleManager.TryGetCommand(args[0], out var command))
            {
                _consoleManager.Print($"Unknown command \"{args[0]}\".");
                return;
            }

            _consoleManager.Print($"{command.Name} - {command.Help}");
            _consoleManager.Print($"  usage: {command.Usage}");
            if (command.Aliases is { Length: > 0 })
                _consoleManager.Print($"  aliases: {string.Join(", ", command.Aliases)}");
            if (command.Flags.HasFlag(ConsoleCommandFlags.Cheat))
                _consoleManager.Print("  (cheat)");
            return;
        }

        foreach (var command in _consoleManager.Commands)
        {
            var cheat = command.Flags.HasFlag(ConsoleCommandFlags.Cheat) ? " (cheat)" : "";
            _consoleManager.Print($"{command.Name,-12}{command.Help}{cheat}");
        }
    }

    private static void Cmd_History(string[] args)
    {
        var history = _consoleManager.History;
        for (int i = 0; i < history.Count; i++)
            _consoleManager.Print($"{i + 1,3}  {history[i]}");
    }
}
