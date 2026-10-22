using System.Text.RegularExpressions;
using PFWolf.Scripting;

namespace PFWolf.Editor.Data;

/// <summary>What a word in a script is, for the tooltip over it: how it's written, and what it does</summary>
public sealed record HoverInfo(string Signature, string Summary, string Kind);

/// <summary>
/// The script editor's tooltips: what the word under the pointer is. The game's functions
/// (game.trigger), library functions (math.floor), an actor's methods (actor:spawnprojectile) and
/// fields (actor.temp1), Lua's globals and libraries, as PFWolf.Common's ScriptApi has them; an
/// action or think (the script's own, with its line, or one actordefs call), with what calls it;
/// and in a string, a name of the game's: an actor class, a switch action, a state.
/// </summary>
public static partial class ScriptHover
{
    /// <param name="line">The line the pointer is on</param>
    /// <param name="column">Where in it, from 0 (the character under the pointer)</param>
    /// <param name="scriptActions">The actions the script defines, with their lines</param>
    public static HoverInfo? Describe(string line, int column, CompletionSources sources, IEnumerable<ScriptItem> scriptActions)
    {
        if (column < 0 || column >= line.Length)
            return null;
        var before = line[..column];
        if (ScriptCompletion.InComment(before))
            return null;

        if (ScriptCompletion.InString(before) is int quote)
            return DescribeString(line, quote, sources);
        if (line[column] is '"' or '\'')
            return null;

        // The word under the pointer, from its start to its end
        int start = column, end = column;
        while (start > 0 && IsNameChar(line[start - 1]))
            start--;
        while (end < line.Length && IsNameChar(line[end]))
            end++;
        if (start == end || char.IsDigit(line[start]))
            return null;
        var word = line[start..end];

        var member = MemberOf().Match(line[..start]);
        if (member.Success)
        {
            var owner = member.Groups["owner"].Value;
            if (member.Groups["sep"].Value == ":")
                return Info(ScriptApi.ActorMethods.FirstOrDefault(entry => entry.Name == word), "actor method");
            if (owner == "game")
                return Info(ScriptApi.Game.FirstOrDefault(entry => entry.Name == word), "game function");
            if (ScriptApi.Libraries.Any(library => library.Name == owner))
                return Info(ScriptApi.LibraryMembers.FirstOrDefault(entry => entry.Library == owner && entry.Name == word), $"{owner} function")
                    ?? (owner == "math" && word.StartsWith("random", StringComparison.Ordinal)
                        ? new HoverInfo($"math.{word}", "Not in the game's scripts (it would differ between machines and demo runs): use game.random", "missing")
                        : null);
            return Info(ScriptApi.ActorFields.FirstOrDefault(entry => entry.Name == word), "actor field");
        }

        if (ActionName().IsMatch(word))
            return DescribeAction(word, sources, scriptActions);

        return Info(ScriptApi.Globals.FirstOrDefault(entry => entry.Name == word), "global")
            ?? Info(ScriptApi.Libraries.FirstOrDefault(entry => entry.Name == word), "library")
            ?? Missing(word);
    }

    private static HoverInfo? DescribeAction(string name, CompletionSources sources, IEnumerable<ScriptItem> scriptActions)
    {
        var defined = scriptActions.FirstOrDefault(action => action.Name == name);
        var callers = sources.ActionCallers.GetValueOrDefault(name, []);
        if (defined == null && callers.Count == 0)
            return null;

        var summary = (defined != null ? $"This script's {(name.StartsWith("T_", StringComparison.Ordinal) ? "think" : "action")}, defined on line {defined.Line}" : "An action actordefs states call")
            + (callers.Count > 0
                ? $". Called by {string.Join(", ", callers.Take(6))}{(callers.Count > 6 ? $" and {callers.Count - 6} more" : "")}"
                : ". No actordefs state calls it");
        return new HoverInfo(name, summary, "action");
    }

    // A string's value, when it's one of the game's names
    private static HoverInfo? DescribeString(string line, int quote, CompletionSources sources)
    {
        int close = quote + 1;
        while (close < line.Length && line[close] != line[quote])
            close += line[close] == '\\' ? 2 : 1;
        var value = line[(quote + 1)..Math.Min(close, line.Length)];
        if (value.Length == 0)
            return null;

        if (sources.Classes.Contains(value, StringComparer.OrdinalIgnoreCase))
            return new HoverInfo($"\"{value}\"", "An actordefs class", "class");
        if (ScriptApi.TriggerActions.Contains(value))
            return new HoverInfo($"\"{value}\"", SwitchActionText(value), "switch action");
        if (ScriptApi.OwnTileActions.Contains(value))
            return new HoverInfo($"\"{value}\"", "A switch action that works on the switch's own tile: game.trigger can't run it", "switch action");
        if (sources.States.Contains(value, StringComparer.Ordinal))
            return new HoverInfo($"\"{value}\"", "A state of actordefs classes", "state");
        return null;
    }

    /// <summary>A switch action game.trigger runs, as the actions reference has it: how it's called, and what it does</summary>
    internal static string SwitchActionText(string name) =>
        PFWolf.Definitions.GameActions.Find(name) is { } action
            ? $"A switch action game.trigger runs. {action.Signature}: {action.Description.TrimEnd('.')}."
            : "A switch action game.trigger runs";

    // What the game's sandbox takes out, so a script that uses it fails there
    private static HoverInfo? Missing(string word) => word switch
    {
        "os" or "io" or "debug" or "package" => new HoverInfo(word, "Not in the game's scripts: they can't reach outside the game", "missing"),
        "require" or "dofile" or "loadfile" or "loadstring" or "load" or "collectgarbage"
            => new HoverInfo(word, "Not in the game's scripts: every scripts/*.lua is loaded already, in load order", "missing"),
        _ => null,
    };

    private static HoverInfo? Info(ScriptApiEntry? entry, string kind)
        => entry == null ? null : new HoverInfo(entry.Signature, entry.Summary, kind);

    private static bool IsNameChar(char ch) => char.IsAsciiLetterOrDigit(ch) || ch == '_';

    // What a '.' or ':' before the word follows: a name, or a call's or index's end
    [GeneratedRegex(@"(?<owner>[A-Za-z_][A-Za-z0-9_]*|[)\]])\s*(?<sep>[.:])\s*$")]
    private static partial Regex MemberOf();

    [GeneratedRegex(@"^[AT]_\w+$")]
    private static partial Regex ActionName();
}
