using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PFWolf.Common;

public class ConsoleManager
{
    public Dictionary<string, IConsoleCommand> _commands = new Dictionary<string, IConsoleCommand>
    {
        //("quit", (args) => Environment.Exit(0)),
        {"give", new GiveCommand() }//,
        //("remove", (args) => Console.WriteLine("remove something")),
        //("spawn", (args) => Console.WriteLine("spawn something at x,y")),
        //("kill", (args) => Console.WriteLine("kill something at x,y (or all)")),
    };

    public void ProcessCommand(string input)
    {
        string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        string commandName = parts[0].ToLower();
        string[] args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

        if (_commands.TryGetValue(commandName, out var command))
        {
            try
            {
                command.Execute(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing command '{commandName}': {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"Unknown command: {commandName}");
        }
    }
}
public interface IConsoleCommand
{
    void Execute(string[] args);
}

public class GiveCommand : IConsoleCommand
{
    public void Execute(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: give <item>");
            return;
        }

        string item = args[0];
        Console.WriteLine($"[Stub] Giving player item: {item}");
    }
}