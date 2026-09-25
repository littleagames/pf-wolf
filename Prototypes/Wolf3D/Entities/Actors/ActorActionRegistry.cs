using System.Text;

namespace Wolf3D.Entities.Actors;

/// <summary>
/// Dispatches the `Action`/`Think` name strings carried on <see cref="ActorStateFrame"/> (parsed
/// from actordefs YAML, e.g. `A_GiveInventory("Clip", 25)`) to real C# handlers. Handlers are
/// registered by name at startup; an unregistered name is logged and skipped rather than
/// throwing, since most action names authored today (e.g. weapon Ready/Fire behavior) don't have
/// a handler wired up yet.
/// </summary>
internal static class ActorActionRegistry
{
    internal delegate void ActorAction(Actor actor, string[] args);

    private static readonly Dictionary<string, ActorAction> _actions = [];

    internal static void Register(string name, ActorAction action) => _actions[name] = action;

    internal static void Register(string name, Action<Actor> action) => _actions[name] = (actor, _) => action(actor);

    internal static void Invoke(string? actionCall, Actor actor)
    {
        if (string.IsNullOrWhiteSpace(actionCall))
            return;

        var (name, args) = Parse(actionCall);
        if (_actions.TryGetValue(name, out var action))
        {
            action(actor, args);
            return;
        }

        Console.WriteLine($"No handler registered for actor action '{name}'.");
    }

    /// <summary>Splits `Name("arg", 2)` into its name and unquoted arguments (also used by MapTriggerRegistry).</summary>
    internal static (string Name, string[] Args) Parse(string call)
    {
        call = call.Trim();
        var parenIndex = call.IndexOf('(');
        if (parenIndex < 0)
            return (call, []);

        var name = call[..parenIndex].Trim();
        var closeIndex = call.LastIndexOf(')');
        var argsText = closeIndex > parenIndex ? call[(parenIndex + 1)..closeIndex] : "";
        var args = SplitArgs(argsText).Select(Unquote).ToArray();
        return (name, args);
    }

    private static List<string> SplitArgs(string text)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        var depth = 0;
        var inQuotes = false;
        var current = new StringBuilder();

        foreach (var c in text)
        {
            if (c == '"')
                inQuotes = !inQuotes;

            if (!inQuotes && c == '(')
                depth++;
            else if (!inQuotes && c == ')')
                depth--;

            if (!inQuotes && depth == 0 && c == ',')
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        result.Add(current.ToString());
        return result;
    }

    private static string Unquote(string arg)
    {
        arg = arg.Trim();
        return arg.Length >= 2 && arg[0] == '"' && arg[^1] == '"' ? arg[1..^1] : arg;
    }
}
