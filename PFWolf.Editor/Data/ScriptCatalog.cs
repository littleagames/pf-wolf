using System.Text.RegularExpressions;
using Lua;
using PFWolf.Assets;

namespace PFWolf.Editor.Data;

/// <summary>A Lua script the game runs (scripts/*.lua in pfwolf.pk3 or a mod), and where it comes from</summary>
public sealed record ScriptEntry(string Name, int Order, IReadOnlyList<string> Origins)
{
    /// <summary>The pack it comes from now ("fireball-demo"), for the list's tooltip</summary>
    public string Source { get; init; } = "";

    /// <summary>Its path in that pack (scripts/traps/fire.lua): where saving it back writes</summary>
    public string EntryPath { get; init; } = "";

    public override string ToString() => Name;
}

/// <summary>An actordefs state that calls an action: the actor, the state, and the frame (from 0) in its list</summary>
public sealed record CallerSite(string Actor, string State, int Frame)
{
    /// <summary>"FireballRing: Fire"</summary>
    public string Label => $"{Actor}: {State}";

    public override string ToString() => Label;
}

/// <summary>Something a script defines or adds, with the line it's on (1 for the first)</summary>
public sealed record ScriptItem(string Name, int Line);

/// <summary>What's wrong with a script, on a line (1 for the first); an error keeps the game from loading it</summary>
public sealed record ScriptProblem(int Line, string Message, bool IsError)
{
    public string Label => $"{(IsError ? "Error" : "Warning")}, line {Line}: {Message}";
}

/// <summary>A script read through: the actions and hooks it gives, and what's wrong with it</summary>
public sealed record ScriptReport(IReadOnlyList<ScriptItem> Actions, IReadOnlyList<ScriptItem> Hooks, IReadOnlyList<ScriptProblem> Problems)
{
    public bool Compiles => !Problems.Any(problem => problem.IsError);
}

/// <summary>
/// The Lua scripts the game runs at startup (PFWolf's GameHost.Scripting.cs): every scripts/*.lua
/// in pfwolf.pk3 and the mods, in the order they run, what each defines (its A_ and T_
/// functions, the actions and thinks actordefs states call) and what calls those. A script is
/// checked the way the game would load it, without running it: it must compile, and what the
/// game's sandbox leaves out (io, os, require, math.random...) is pointed out.
/// </summary>
public static partial class ScriptCatalog
{
    /// <summary>Where scripts go in a mod</summary>
    public const string Folder = "scripts/";

    public static List<ScriptEntry> Build(GameContent content)
    {
        var assets = content.Assets;
        var entries = new List<ScriptEntry>();
        foreach (var script in assets.GetScripts())
        {
            // named by its file name, as the loader names it
            var name = NameOf(script.Where);
            var origins = assets.FindAssetOrigins(name)
                .Where(asset => asset.Type == nameof(ScriptAsset))
                .SelectMany(asset => asset.Origins)
                .ToList();
            var used = origins.LastOrDefault(origin => origin.Action != PFWolf.Loaders.AssetOrigin.LeftOut);
            entries.Add(new ScriptEntry(name, script.Order, origins.Select(origin => $"{origin.Action} by {origin.Source}: {origin.Path}").ToList())
            {
                Source = used?.Source ?? "",
                EntryPath = used?.Path ?? $"{Folder}{name}.lua",
            });
        }
        return entries;
    }

    // "fireball-demo: scripts/fireball-patterns.lua" -> "fireball-patterns"
    private static string NameOf(string where)
    {
        var file = where[(where.LastIndexOf('/') + 1)..];
        return Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
    }

    /// <summary>A script as the game reads it, or null when there's none of that name</summary>
    public static string? Read(GameContent content, string name) => content.Find<ScriptAsset>(name)?.Text;

    /// <summary>
    /// Reads a script through: compiles it (nothing runs), finds the A_/T_ functions it defines
    /// and the hooks it adds, and what in it the game's scripts haven't got
    /// </summary>
    public static ScriptReport Check(string text, string chunkName)
    {
        var problems = new List<ScriptProblem>();
        try
        {
            var state = LuaState.Create();
            state.Load(text.AsSpan(), chunkName, state.Environment);
        }
        catch (LuaCompileException e)
        {
            problems.Add(new ScriptProblem(Math.Max(e.Position.Line, 1), $"{e.MainMessage} (the game leaves the script out)", IsError: true));
        }
        catch (LuaParseException e)
        {
            problems.Add(new ScriptProblem(Math.Max(e.Position.Line, 1), $"{StripChunk(e.Message)} (the game leaves the script out)", IsError: true));
        }

        var actions = new List<ScriptItem>();
        var hooks = new List<ScriptItem>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        bool inLongComment = false;
        for (int i = 0; i < lines.Length; i++)
        {
            var code = CodeOf(lines[i], ref inLongComment);
            if (code.Length == 0)
                continue;

            foreach (Match match in ActionDefinition().Matches(code))
                actions.Add(new ScriptItem(match.Groups["name"].Value, i + 1));
            foreach (Match match in HookCall().Matches(code))
                hooks.Add(new ScriptItem($"game.{match.Groups["hook"].Value}", i + 1));

            foreach (var (pattern, why) in Missing)
            {
                var found = pattern.Match(code);
                if (found.Success)
                    problems.Add(new ScriptProblem(i + 1, $"{found.Value.TrimEnd('(', ' ')}: {why}", IsError: false));
            }
        }

        foreach (var twice in actions.GroupBy(action => action.Name).Where(group => group.Count() > 1))
            problems.Add(new ScriptProblem(twice.Last().Line, $"{twice.Key} is defined again here: this one is used", IsError: false));

        return new ScriptReport(actions, hooks, problems.OrderBy(problem => problem.Line).ToList());
    }

    // `function A_FireRing(` or `A_FireRing = function(`, as a global
    [GeneratedRegex(@"^\s*(?:function\s+(?<name>[AT]_\w+)\s*\(|(?<name>[AT]_\w+)\s*=\s*function\b)")]
    private static partial Regex ActionDefinition();

    [GeneratedRegex(@"\bgame\s*\.\s*(?<hook>onlevel|ontick)\s*\(")]
    private static partial Regex HookCall();

    // What the game's scripts haven't got (Scripting/ScriptHost.cs), and why
    private static readonly (Regex Pattern, string Why)[] Missing =
    [
        (new Regex(@"\bmath\s*\.\s*random(?:seed)?\b"), "not in the game's scripts (it would differ between machines and demo runs): use game.random"),
        (new Regex(@"\b(?:os|io|debug|package)\s*\."), "not in the game's scripts: they can't reach outside the game"),
        (new Regex(@"\b(?:require|dofile|loadfile|loadstring|load|collectgarbage)\s*\("), "not in the game's scripts: every scripts/*.lua is loaded already, in load order"),
    ];

    // A line less its comments (-- to the end, --[[ ]] blocks) and the insides of its strings, roughly
    private static string CodeOf(string line, ref bool inLongComment)
    {
        var code = new System.Text.StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            if (inLongComment)
            {
                int end = line.IndexOf("]]", i, StringComparison.Ordinal);
                if (end < 0)
                    return code.ToString();
                inLongComment = false;
                i = end + 1;
                continue;
            }
            char ch = line[i];
            if (ch == '-' && i + 1 < line.Length && line[i + 1] == '-')
            {
                if (i + 3 < line.Length && line[i + 2] == '[' && line[i + 3] == '[')
                {
                    inLongComment = true;
                    i += 3;
                    continue;
                }
                break;
            }
            if (ch is '"' or '\'')
            {
                // the string's contents left out, so text in it isn't taken for code
                int close = i + 1;
                while (close < line.Length && line[close] != ch)
                    close += line[close] == '\\' ? 2 : 1;
                code.Append(ch).Append(ch);
                i = close;
                continue;
            }
            code.Append(ch);
        }
        return code.ToString();
    }

    private static string StripChunk(string message) => ChunkPrefix().Replace(message, "");

    [GeneratedRegex(@"^\[string ""[^""]*""\]:\d+:\s*")]
    private static partial Regex ChunkPrefix();

    /// <summary>
    /// The actordefs states that call each action or think, by its name: "FireballRing: Fire",
    /// one per actor and state (the state's frames may call it more than once)
    /// </summary>
    public static Dictionary<string, List<string>> Callers(GameContent content)
        => CallerSites(content).ToDictionary(caller => caller.Key, caller => caller.Value.Select(site => site.Label).ToList(), StringComparer.Ordinal);

    /// <summary>
    /// The actordefs states that call each action or think, by its name: the actor, the state
    /// and the first of its frames that calls it (its place in the state's list, from 0), one per
    /// actor and state, to show in the Definitions window
    /// </summary>
    public static Dictionary<string, List<CallerSite>> CallerSites(GameContent content)
    {
        var callers = new Dictionary<string, List<CallerSite>>(StringComparer.Ordinal);
        foreach (var (actorName, actor) in content.Actors.OrderBy(actor => actor.Key, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var (stateName, frames) in actor.States)
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    if (frames[i] is not ActorStatesData frame)
                        continue;
                    foreach (var call in new[] { frame.Action, frame.Think })
                    {
                        if (string.IsNullOrWhiteSpace(call))
                            continue;
                        var name = CallName(call);
                        if (!callers.TryGetValue(name, out var list))
                            callers[name] = list = [];
                        if (!list.Any(site => site.Actor == actorName && site.State == stateName))
                            list.Add(new CallerSite(actorName, stateName, i));
                    }
                }
            }
        }
        return callers;
    }

    // "A_FireRing("Fireball", 8)" -> "A_FireRing"
    private static string CallName(string call)
    {
        call = call.Trim();
        int paren = call.IndexOf('(');
        return (paren < 0 ? call : call[..paren]).Trim();
    }

    /// <summary>
    /// Writes a script into the mod (a folder or a pk3) at that path in it (scripts/NAME.lua), as
    /// UTF-8. Returns where it went.
    /// </summary>
    public static string Save(string modPath, string entryPath, string text)
        => Editing.ModFiles.Write(modPath, (entryPath, new System.Text.UTF8Encoding(false).GetBytes(text)));

    /// <summary>Where a script of this name goes in a mod it isn't in yet</summary>
    public static string NewEntryPath(string name) => $"{Folder}{name.ToLowerInvariant()}.lua";

    /// <summary>What's wrong with a name for a new script, or null when it will do</summary>
    public static string? CheckNewName(GameContent content, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Give it a name";
        if (!name.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-'))
            return "Letters, digits, - and _ only: it's the file name in scripts/";
        if (content.Assets.Exists<ScriptAsset>(name))
            return $"There's a script called {name.ToLowerInvariant()} already: a new one of that name would replace it";
        return null;
    }

    /// <summary>A script to start a new one from: an action, and the hooks, to change or take out</summary>
    public const string NewScript =
        "-- Each global function named A_... or T_... is an action (or think) actordefs states call\r\n" +
        "-- like a built-in one: action: A_Hello(\"world\"). self is the actor running the state.\r\n" +
        "function A_Hello(self, who)\r\n" +
        "  print(self.name .. \" says hello to \" .. tostring(who))\r\n" +
        "end\r\n" +
        "\r\n" +
        "-- Hooks the game calls itself, added as the script loads\r\n" +
        "game.onlevel(function(reason, map)\r\n" +
        "  -- reason: \"new\", \"returned\", \"restarted\" or \"loaded\"\r\n" +
        "end)\r\n" +
        "\r\n" +
        "game.ontick(function(tics)\r\n" +
        "  -- each frame the world moves; game.time() is the level's time\r\n" +
        "end)\r\n";
}
