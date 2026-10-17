using static PFWolf.Commands.ConsoleArgs;

namespace PFWolf.Commands;

/// <summary>The console's own commands: help, echo, clear, history, con_pause and exec</summary>
/// <param name="configFilePath">A file's path in the config folder (GameEngineManager.GetConfigFilePath)</param>
internal sealed class CoreCommands(ConsoleManager console, Func<string, string> configFilePath)
{
    internal void Register()
    {
        console.Register("help", "Lists commands, or describes one.", "help [command]", Cmd_Help, aliases: ["?"],
            complete: (_, i) => i == 0 ? CommandNames() : []);
        console.Register("echo", "Prints its arguments.", "echo <text...>",
            args => console.Print(string.Join(' ', args)));
        console.Register("clear", "Clears the console output.", "clear", _ => console.Clear(), aliases: ["cls"]);
        console.Register("history", "Lists previously entered lines.", "history", Cmd_History);
        console.Register("con_pause", "Whether the game pauses while the console is open.", "con_pause [0|1]", Cmd_ConPause,
            complete: Values("0", "1"));
        console.Register("exec", "Runs the commands in a script file (in the config folder unless a full path is given).",
            "exec <file>", Cmd_Exec, complete: (_, i) => i == 0 ? ConfigScriptNames() : []);
        // Handled by ConsoleManager.Execute itself (the rest of the line waits); here for help
        console.Register("wait", "Puts the rest of the line off until so many tics of play have gone by (default 1).",
            "wait [tics]", _ => { });
    }

    IEnumerable<string> CommandNames() => console.Commands.Select(c => c.Name);

    IEnumerable<string> ConfigScriptNames()
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(configFilePath("x.cfg")));
        return dir != null && Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.cfg").Select(Path.GetFileName).OfType<string>()
            : [];
    }

    private void Cmd_Help(string[] args)
    {
        if (args.Length > 0)
        {
            if (!console.TryGetCommand(args[0], out var command))
            {
                console.Print($"Unknown command \"{args[0]}\".");
                return;
            }

            console.Print($"{command.Name} - {command.Help}");
            console.Print($"  usage: {command.Usage}");
            if (command.Aliases is { Length: > 0 })
                console.Print($"  aliases: {string.Join(", ", command.Aliases)}");
            if (command.Flags.HasFlag(ConsoleCommandFlags.Cheat))
                console.Print("  (cheat)");
            return;
        }

        foreach (var command in console.Commands)
        {
            var cheat = command.Flags.HasFlag(ConsoleCommandFlags.Cheat) ? " (cheat)" : "";
            console.Print($"{command.Name,-12}{command.Help}{cheat}");
        }
    }

    private void Cmd_History(string[] args)
    {
        var history = console.History;
        for (int i = 0; i < history.Count; i++)
            console.Print($"{i + 1,3}  {history[i]}");
    }

    private void Cmd_ConPause(string[] args)
    {
        if (args.Length > 0)
            console.PauseWhenOpen = ParseBool(args[0]);

        console.Print($"con_pause is {(console.PauseWhenOpen ? 1 : 0)}");
    }

    private void Cmd_Exec(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("exec which file?");

        var path = Path.IsPathRooted(args[0]) ? args[0] : configFilePath(args[0]);
        if (!console.ExecFile(path))
            throw new ArgumentException($"couldn't find \"{path}\"");
    }
}
