using System.Text.RegularExpressions;

namespace PFWolf.Editor.Data;

/// <summary>A script as go-to-definition reads it: its name, its place in run order, and its text as it stands</summary>
public sealed record ScriptSource(string Name, int Order, string Text);

/// <summary>Where a name is defined: the script and line (1 for the first), and anything to say about it</summary>
public sealed record DefinitionTarget(string Script, int Line, string? Note = null);

/// <summary>
/// The script editor's go-to-definition: an action or think (A_..., T_...) to the script function
/// the game runs for it, which is the last script to define it in run order, since later ones
/// replace earlier ones; another function to where this script defines it.
/// </summary>
public static partial class ScriptNavigation
{
    /// <summary>The name at a column (from 0) of a line, when there's one there (not in a comment or string)</summary>
    public static string? NameAt(string line, int column)
    {
        if (column < 0 || column > line.Length)
            return null;
        // just after a name counts too: the caret at its end
        if (column == line.Length || !IsNameChar(line[column]))
        {
            if (column == 0 || !IsNameChar(line[column - 1]))
                return null;
            column--;
        }
        var before = line[..column];
        if (ScriptCompletion.InComment(before) || ScriptCompletion.InString(before) != null)
            return null;

        int start = column, end = column;
        while (start > 0 && IsNameChar(line[start - 1]))
            start--;
        while (end < line.Length && IsNameChar(line[end]))
            end++;
        var name = line[start..end];
        if (char.IsDigit(name[0]))
            return null;
        // a member (game.trigger, self:jump) is the API's, not something a script defines
        var head = line[..start].TrimEnd();
        if (head.EndsWith('.') || head.EndsWith(':'))
            return null;
        return name;
    }

    /// <summary>
    /// Where the name is defined, from the script it's in: for an action, the definition the game
    /// runs (the last script to define it); for another function, this script's. Null when no
    /// script defines it, with <paramref name="why"/> saying so.
    /// </summary>
    public static DefinitionTarget? Find(string name, string current, IReadOnlyList<ScriptSource> scripts, out string why)
    {
        why = "";
        if (ActionName().IsMatch(name))
        {
            var definitions = scripts
                .Select(script => (Script: script, Line: ScriptCatalog.Check(script.Text, script.Name).Actions.LastOrDefault(action => action.Name == name)?.Line))
                .Where(found => found.Line != null)
                .OrderBy(found => found.Script.Order)
                .ToList();
            if (definitions.Count == 0)
            {
                why = $"No script defines {name}: it's a built-in action, or one only actordefs names";
                return null;
            }

            var used = definitions[^1];
            var here = definitions.FirstOrDefault(found => found.Script.Name == current);
            string? note = here.Script != null && here.Script != used.Script
                ? $"{used.Script.Name} defines {name} too, and runs later: its {name} is the one the game uses"
                : definitions.Count > 1 ? $"{name} is defined in {definitions.Count} scripts: the game uses this one, which runs last" : null;
            return new DefinitionTarget(used.Script.Name, used.Line!.Value, note);
        }

        var self = scripts.FirstOrDefault(script => script.Name == current);
        if (self != null && FunctionLine(self.Text, name) is int line)
            return new DefinitionTarget(self.Name, line);
        why = $"{name} isn't a function this script defines";
        return null;
    }

    // The line a function is defined on (function name, local function name, name = function)
    private static int? FunctionLine(string text, string name)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var pattern = new Regex($@"^\s*(?:(?:local\s+)?function\s+{Regex.Escape(name)}\s*\(|(?:local\s+)?{Regex.Escape(name)}\s*=\s*function\b)");
        for (int i = 0; i < lines.Length; i++)
        {
            if (pattern.IsMatch(lines[i]))
                return i + 1;
        }
        return null;
    }

    private static bool IsNameChar(char ch) => char.IsAsciiLetterOrDigit(ch) || ch == '_';

    [GeneratedRegex(@"^[AT]_\w+$")]
    private static partial Regex ActionName();
}
