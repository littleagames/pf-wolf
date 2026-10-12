using System.Text.RegularExpressions;
using PFWolf.Assets;
using PFWolf.Scripting;

namespace PFWolf.Editor.Data;

/// <summary>Something the script editor offers to type: a name, how it's written, and what it is</summary>
public sealed record CompletionOption(string Text, string Signature, string Summary, string Kind);

/// <summary>What to offer at the caret: the options, and how much of the word before it they replace</summary>
public sealed record CompletionContext(int PrefixLength, IReadOnlyList<CompletionOption> Options);

/// <summary>The game's names a script's strings take: actor classes, their states, flags and property keys</summary>
public sealed record CompletionSources(
    IReadOnlyList<string> Classes,
    IReadOnlyList<string> States,
    IReadOnlyList<string> Flags,
    IReadOnlyList<string> PropertyKeys,
    IReadOnlyDictionary<string, List<string>> ActionCallers)
{
    public static CompletionSources Empty { get; } = new([], [], [], [], new Dictionary<string, List<string>>());

    /// <summary>Gathered from a loaded game's actordefs</summary>
    public static CompletionSources From(GameContent content)
    {
        var actors = content.Actors;
        return new CompletionSources(
            actors.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList(),
            actors.Values.SelectMany(actor => actor.States.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.OrdinalIgnoreCase).ToList(),
            actors.Values.SelectMany(actor => actor.Flags).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList(),
            actors.Values.SelectMany(actor => actor.Properties.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList(),
            ScriptCatalog.Callers(content));
    }
}

/// <summary>
/// The script editor's autocomplete: from the line up to the caret, what could come next. After
/// `game.` the game's functions, after `string.` (and table., math., bit32.) the library's, after
/// `x.` an actor's fields, after `x:` its methods; a word being typed, Lua's globals, libraries
/// and keywords and the actions there are (the script's own and those actordefs call). In a
/// string: actor classes for spawnprojectile, spawn, game.actors and game.tagged, the switch
/// actions for game.trigger, state names for jump, flags for hasflag and properties for property.
/// The names come from PFWolf.Common's ScriptApi, which is checked against the engine.
/// </summary>
public static partial class ScriptCompletion
{
    /// <summary>
    /// What to offer with the caret after <paramref name="before"/> (the line up to it); null for
    /// nothing. Unasked (as a word is typed), only after a '.', ':', a quote or two letters of a word.
    /// </summary>
    public static CompletionContext? Find(string before, CompletionSources sources, IEnumerable<string> scriptActions, bool asked)
    {
        if (InString(before) is int quote)
        {
            var prefix = before[(quote + 1)..];
            var options = StringOptions(before[..quote], sources);
            return Offer(options, prefix, asked: true);    // offered as soon as the quote's typed
        }
        if (InComment(before))
            return null;

        var word = WordAtEnd().Match(before).Value;
        var head = before[..^word.Length];
        if (word.Length > 0 && char.IsDigit(word[0]))
            return null;

        var dot = MemberOf().Match(head);
        if (dot.Success)
        {
            var owner = dot.Groups["owner"].Value;
            var options = dot.Groups["sep"].Value == ":"
                ? ScriptApi.ActorMethods.Select(entry => Option(entry, "method"))
                : owner == "game" ? ScriptApi.Game.Select(entry => Option(entry, "game"))
                : ScriptApi.Libraries.Any(library => library.Name == owner)
                    ? ScriptApi.LibraryMembers.Where(entry => entry.Library == owner).Select(entry => Option(entry, owner))
                : ScriptApi.ActorFields.Select(entry => Option(entry, "field"));
            return Offer(options, word, asked: true);
        }

        if (!asked && word.Length < 2)
            return null;
        // a function being named: no point offering what's there
        if (FunctionName().IsMatch(head))
            return asked ? Offer(Actions(sources, scriptActions), word, asked) : null;

        var everything = ScriptApi.Globals.Select(entry => Option(entry, "global"))
            .Concat(ScriptApi.Libraries.Select(entry => Option(entry, "library")))
            .Concat(Actions(sources, scriptActions))
            .Concat(ScriptApi.Keywords.Select(entry => Option(entry, "keyword")));
        return Offer(everything, word, asked);
    }

    // The options for a string being typed, by what it's an argument to
    private static IEnumerable<CompletionOption> StringOptions(string head, CompletionSources sources)
    {
        if (ClassArgument().IsMatch(head))
            return sources.Classes.Select(name => new CompletionOption(name, name, "An actordefs class", "class"));
        if (TriggerArgument().IsMatch(head))
            return ScriptApi.TriggerActions.Select(name => new CompletionOption(name, name, "A switch action (mapdefs/wolf3d/walls.yaml describes them at wall 21)", "switch action"));
        if (JumpArgument().IsMatch(head))
            return sources.States.Select(name => new CompletionOption(name, name, "A state an actordefs class has", "state"));
        if (FlagArgument().IsMatch(head))
            return sources.Flags.Select(name => new CompletionOption(name, name, "An actordefs flag", "flag"));
        if (PropertyArgument().IsMatch(head))
            return sources.PropertyKeys.Select(name => new CompletionOption(name, name, "An actordefs property", "property"));
        return [];
    }

    // The actions and thinks there are: the script's own, and those actordefs states call
    private static IEnumerable<CompletionOption> Actions(CompletionSources sources, IEnumerable<string> scriptActions)
    {
        var own = scriptActions.ToHashSet(StringComparer.Ordinal);
        return own.Concat(sources.ActionCallers.Keys.Where(name => ActionName().IsMatch(name)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name =>
            {
                var callers = sources.ActionCallers.GetValueOrDefault(name, []);
                var summary = (own.Contains(name) ? "This script's" : "An action actordefs call")
                    + (callers.Count > 0 ? $": called by {string.Join(", ", callers.Take(4))}{(callers.Count > 4 ? ", ..." : "")}" : "");
                return new CompletionOption(name, name, summary, "action");
            });
    }

    private static CompletionContext? Offer(IEnumerable<CompletionOption> options, string prefix, bool asked)
    {
        var matching = options
            .Where(option => option.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .GroupBy(option => option.Text, StringComparer.Ordinal).Select(group => group.First())
            .ToList();
        // the word typed in full, and nothing else: nothing to offer
        if (matching.Count == 0 || !asked && matching.Count == 1 && matching[0].Text == prefix)
            return null;
        return new CompletionContext(prefix.Length, matching);
    }

    private static CompletionOption Option(ScriptApiEntry entry, string kind) => new(entry.Name, entry.Signature, entry.Summary, kind);

    /// <summary>Where the string the caret is in starts (its opening quote), or null when it isn't in one</summary>
    public static int? InString(string before)
    {
        int? open = null;
        char quote = '\0';
        for (int i = 0; i < before.Length; i++)
        {
            char ch = before[i];
            if (open != null)
            {
                if (ch == '\\')
                    i++;
                else if (ch == quote)
                    open = null;
            }
            else if (ch is '"' or '\'')
            {
                open = i;
                quote = ch;
            }
            else if (ch == '-' && i + 1 < before.Length && before[i + 1] == '-')
                return null;    // the rest is a comment
        }
        return open;
    }

    /// <summary>Whether the caret is in a -- comment (outside strings)</summary>
    public static bool InComment(string before)
    {
        bool inString = false;
        char quote = '\0';
        for (int i = 0; i < before.Length; i++)
        {
            char ch = before[i];
            if (inString)
            {
                if (ch == '\\')
                    i++;
                else if (ch == quote)
                    inString = false;
            }
            else if (ch is '"' or '\'')
            {
                inString = true;
                quote = ch;
            }
            else if (ch == '-' && i + 1 < before.Length && before[i + 1] == '-')
                return true;
        }
        return false;
    }

    // A name being typed, or a number (not the digits ending a name: bit32)
    [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_]*$|(?<![A-Za-z0-9_])\d[A-Za-z0-9_.]*$")]
    private static partial Regex WordAtEnd();

    // What a '.' or ':' follows: a name, or a call's or index's end (game.players()[1]:)
    [GeneratedRegex(@"(?<owner>[A-Za-z_][A-Za-z0-9_]*|[)\]])\s*(?<sep>[.:])\s*$")]
    private static partial Regex MemberOf();

    [GeneratedRegex(@"^[AT]_\w+$")]
    private static partial Regex ActionName();

    [GeneratedRegex(@"\bfunction\s+$")]
    private static partial Regex FunctionName();

    [GeneratedRegex(@"(?::\s*(?:spawnprojectile|spawn)|\bgame\s*\.\s*actors)\s*\(\s*$|\bgame\s*\.\s*tagged\s*\([^,()]*,\s*$")]
    private static partial Regex ClassArgument();

    [GeneratedRegex(@"\bgame\s*\.\s*trigger\s*\([^,()]*,\s*$")]
    private static partial Regex TriggerArgument();

    [GeneratedRegex(@":\s*jump\s*\(\s*$")]
    private static partial Regex JumpArgument();

    [GeneratedRegex(@":\s*hasflag\s*\(\s*$")]
    private static partial Regex FlagArgument();

    [GeneratedRegex(@":\s*property\s*\(\s*$")]
    private static partial Regex PropertyArgument();
}
