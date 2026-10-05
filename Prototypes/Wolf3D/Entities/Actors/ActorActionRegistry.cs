using System.Text;

namespace Wolf3D.Entities.Actors;

/// <summary>
/// Dispatches the `Action`/`Think` name strings carried on <see cref="ActorStateFrame"/> (parsed
/// from actordefs YAML, e.g. `A_GiveInventory("Clip", 25)`) to real C# handlers. Handlers are
/// registered by name at startup; an unregistered name is logged and skipped rather than
/// throwing, since some action names authored today (e.g. the weapons' A_Raise/A_Lower) don't
/// have a handler wired up yet.
/// </summary>
internal static class ActorActionRegistry
{
    internal delegate void ActorAction(Actor actor, string[] args);

    private static readonly Dictionary<string, ActorAction> _actions = [];

    internal static void Register(string name, ActorAction action) => _actions[name] = action;

    internal static void Register(string name, Action<Actor> action) => _actions[name] = (actor, _) => action(actor);

    // The class each name registered with RegisterFor needs, and the actor classes already
    // warned about running one without being that class
    private static readonly Dictionary<string, Type> _neededClass = [];
    private static readonly HashSet<(string Class, string Action)> _warnedWrongClass = [];

    /// <summary>
    /// A think or action that runs on a <typeparamref name="T"/> (a <see cref="Monster"/>, say);
    /// any other actor that hits it is warned about once and skipped.
    /// </summary>
    internal static void RegisterFor<T>(string name, Action<T, string[]> action) where T : Actor
    {
        _neededClass[name] = typeof(T);
        _actions[name] = (actor, args) =>
        {
            if (actor is T needed)
                action(needed, args);
            else if (_warnedWrongClass.Add((actor.Name, name)))
                Console.WriteLine($"Actor '{actor.Name}' runs {name}, which needs a {typeof(T).Name} (`parent: {typeof(T).Name}` in its actordefs); skipped.");
        };
    }

    internal static void RegisterFor<T>(string name, Action<T> action) where T : Actor => RegisterFor<T>(name, (actor, _) => action(actor));

    /// <summary>The class a state's think/action call (`T_Chase`, `A_FireProjectile("Rocket")`) needs to run it, or null for any actor.</summary>
    internal static Type? NeededClass(string? actionCall) =>
        string.IsNullOrWhiteSpace(actionCall) ? null : _neededClass.GetValueOrDefault(Parse(actionCall).Name);

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
