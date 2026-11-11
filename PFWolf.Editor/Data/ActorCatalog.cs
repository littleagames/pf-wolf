using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using PFWolf.Assets;
using PFWolf.Definitions;
using PFWolf.Editor.Definitions;
using PFWolf.Entities.Actors;
using PFWolf.Loaders;
using PFWolf.Managers;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.Data;

/// <summary>What sort of thing a class is, for the actor window's filter</summary>
public enum ActorKind
{
    Enemy,
    Item,
    Weapon,
    Projectile,
    Decoration,
    Player,
    /// <summary>Nothing to see: a marker, an emitter, a base class</summary>
    Unseen,
}

/// <summary>
/// Where Go to takes a class's use: another class in the actor window, an entry in the
/// Definitions window (its entry's path, then the field's), a script's line, or a level's tile
/// </summary>
public sealed record ActorUseTarget(string? Actor = null, DefinitionKind? Kind = null, IReadOnlyList<string>? EntryPath = null,
    IReadOnlyList<string>? FieldPath = null, string? Script = null, int Line = 0, string? Map = null, int X = 0, int Y = 0);

/// <summary>One place that names a class</summary>
/// <param name="Names">Whether it names the class to use it (a level, mapdefs, another class's property or action, a script), rather than the engine knowing it or a class being built on it</param>
public sealed record ActorUse(string Label, ActorUseTarget? Target = null, bool Names = true)
{
    public override string ToString() => Label;
}

/// <summary>An actordefs file that has a class, and the class's lines in it</summary>
public sealed record ActorDefinitionSite(string File, string Yaml);

/// <summary>An actordefs class as the game builds it: merged with its parents, its states resolved, and what's wrong with it</summary>
public sealed class ActorEntry
{
    public required string Name { get; init; }

    /// <summary>The class as written (the last file's, with mods' extend laid over it)</summary>
    public required ActorData Own { get; init; }

    /// <summary>With what its parents give it, as the game builds it; null when it can't be built</summary>
    public ActorData? Merged { get; init; }

    /// <summary>Its parents, nearest first</summary>
    public IReadOnlyList<string> Ancestors { get; init; } = [];

    /// <summary>The engine class it is (Monster, Weapon...): ActorClasses.EngineClass</summary>
    public string EngineClass { get; init; } = "Actor";

    public ActorKind Kind { get; init; }

    /// <summary>Its states as the game plays them, in the order the game has them (its own, then its parents', then a Raise made from its Death)</summary>
    public IReadOnlyDictionary<string, ActorStateFrame> States { get; init; } = new Dictionary<string, ActorStateFrame>();

    public List<string> Problems { get; } = [];

    /// <summary>What's odd but harmless: frames with no sprite in a class nothing here uses</summary>
    public List<string> Notes { get; } = [];

    /// <summary>Its frames (sprite and letter, GARDZ) the game has no sprite for in any view</summary>
    public List<string> MissingSprites { get; } = [];

    public List<ActorDefinitionSite> DefinedIn { get; } = [];
    public List<ActorUse> Uses { get; } = [];

    /// <summary>The class that gives it a property, flag or state: its own name, or the nearest parent's that has it</summary>
    public string? SourceOfProperty(string key, IReadOnlyDictionary<string, ActorData> actors)
        => Own.Properties.ContainsKey(key) ? Name : Ancestors.FirstOrDefault(name => actors.GetValueOrDefault(name)?.Properties.ContainsKey(key) == true);

    public string? SourceOfState(string state, IReadOnlyDictionary<string, ActorData> actors)
        => Own.States.ContainsKey(state) ? Name : Ancestors.FirstOrDefault(name => actors.GetValueOrDefault(name)?.States.ContainsKey(state) == true);

    public string? SourceOfFlag(string flag, IReadOnlyDictionary<string, ActorData> actors)
        => Own.Flags.Contains(flag) ? Name : Ancestors.FirstOrDefault(name => actors.GetValueOrDefault(name)?.Flags.Contains(flag) == true);

    public override string ToString() => Name;
}

/// <summary>
/// Every actordefs class of the loaded game as the game builds it (ActorInheritance,
/// ActorStateResolver), where each is defined, what names it and what's wrong with it
/// </summary>
public static partial class ActorCatalog
{
    public static List<ActorEntry> Build(GameContent content, bool levels = true)
    {
        var actors = content.Actors;
        var luaActions = LuaActions(content);
        var sprites = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        bool HasSprite(string name) => sprites.TryGetValue(name, out var has) ? has : sprites[name] = content.Find<SpriteAsset>(name) != null;

        var entries = actors.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => Entry(pair.Key, pair.Value, actors, luaActions, HasSprite)).ToList();

        var byName = entries.ToDictionary(entry => entry.Name, StringComparer.Ordinal);
        AddDefinitionSites(content, byName);
        AddUses(content, byName);
        if (levels)
            AddLevelUses(content, byName);
        foreach (var entry in entries)
            JudgeMissingSprites(entry);
        return entries;
    }

    /// <summary>
    /// A class built again from <paramref name="own"/> (its fields as edited) in place of what the
    /// files have, among the game's other classes; what names it and where it's defined are kept
    /// from <paramref name="before"/> (its entry as the files have it, or none for a new class)
    /// </summary>
    public static ActorEntry Rebuild(GameContent content, string name, ActorData own, ActorEntry? before, HashSet<string> scriptActions)
    {
        var actors = content.Actors.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        actors[name] = own;
        var entry = Entry(name, own, actors, scriptActions, sprite => content.Find<SpriteAsset>(sprite) != null);
        if (before != null)
        {
            entry.Uses.AddRange(before.Uses);
            entry.DefinedIn.AddRange(before.DefinedIn);
        }
        JudgeMissingSprites(entry);
        return entry;
    }

    // One class merged with its parents and resolved, with what's wrong with it
    private static ActorEntry Entry(string name, ActorData own, IReadOnlyDictionary<string, ActorData> actors,
        HashSet<string> luaActions, Func<string, bool> hasSprite)
    {
        var problems = new List<string>();
        ActorData? merged = null;
        List<string> ancestors = [];
        try
        {
            ancestors = [.. ActorInheritance.Ancestors(name, own, actors).Select(ancestor => ancestor.Name)];
            merged = ActorInheritance.Merged(name, own, actors);
        }
        catch (Exception e)
        {
            problems.Add($"It can't be made: {e.Message}");
        }

        var states = merged != null
            ? ActorStateResolver.Resolve(merged.States, (state, _, marker) => problems.Add(ActorStateResolver.IgnoredMessage(name, state, marker)))
            : [];
        var engineClass = ActorClasses.EngineClass(name, ancestors);
        var entry = new ActorEntry
        {
            Name = name,
            Own = own,
            Merged = merged,
            Ancestors = ancestors,
            EngineClass = engineClass,
            Kind = KindOf(engineClass, merged, states),
            States = states,
        };
        entry.Problems.AddRange(problems);
        if (merged != null)
        {
            entry.Problems.AddRange(Problems(entry, merged, actors, luaActions));
            entry.MissingSprites.AddRange(MissingSprites(merged, hasSprite));
        }
        return entry;
    }

    // A frame with no sprite matters when a level places the class; shared classes (and mapdefs
    // things for another release's levels) have frames only other games draw
    private static void JudgeMissingSprites(ActorEntry entry)
    {
        if (entry.MissingSprites.Count == 0)
            return;
        var text = $"No sprite for {string.Join(", ", entry.MissingSprites)} (in any view): those frames show nothing";
        if (entry.Uses.Any(use => use.Target?.Map != null))
            entry.Problems.Add(text);
        else
            entry.Notes.Add(text + ", but no level here places it");
    }

    // Each frame (sprite and letter) the game can't find in any view
    private static List<string> MissingSprites(ActorData merged, Func<string, bool> hasSprite)
    {
        var missing = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var frame in merged.States.Values.SelectMany(list => list.OfType<ActorStatesData>()))
        {
            if (string.IsNullOrEmpty(frame.Sprite) || frame.Sprite == "TNT1")
                continue;
            foreach (var letter in frame.Frames)
                if (!Enumerable.Range(0, 9).Any(view => hasSprite($"{frame.Sprite}{letter}{view}")))
                    missing.Add($"{frame.Sprite}{letter}");
        }
        return [.. missing];
    }

    private static ActorKind KindOf(string engineClass, ActorData? merged, IReadOnlyDictionary<string, ActorStateFrame> states)
    {
        if (ActorClasses.IsA(engineClass, "Weapon"))
            return ActorKind.Weapon;
        if (ActorClasses.IsA(engineClass, "Inventory"))
            return ActorKind.Item;
        if (ActorClasses.IsA(engineClass, "PlayerPawn"))
            return ActorKind.Player;
        if (ActorClasses.IsA(engineClass, "Monster"))
            return ActorKind.Enemy;
        if (merged?.Flags.Any(flag => flag.Equals("PROJECTILE", StringComparison.OrdinalIgnoreCase)) == true)
            return ActorKind.Projectile;
        return states.GetValueOrDefault("Spawn") is { Sprite: { Length: > 0 } sprite } && sprite != "TNT1" ? ActorKind.Decoration : ActorKind.Unseen;
    }

    //
    // What's wrong with a class
    //

    private static IEnumerable<string> Problems(ActorEntry entry, ActorData merged, IReadOnlyDictionary<string, ActorData> actors,
        HashSet<string> luaActions)
    {
        var name = entry.Name;

        foreach (var (state, entries) in merged.States)
        {
            foreach (var modifier in ActorStateResolver.UnknownModifiers(entries))
                yield return ActorStateResolver.UnknownModifierMessage(name, state, modifier);
            foreach (var target in entries.Select(item => item switch { ActorStatesData frames => frames.NextState, GoToStateData goTo => goTo.NextState, _ => null }))
                if (!string.IsNullOrEmpty(target) && !merged.States.ContainsKey(target))
                    yield return $"Its {state} state goes to {target}, which it has no state called";
        }


        // Thinks and actions: ones nothing has, or for another kind of actor
        var calls = merged.States.Values.SelectMany(list => list.OfType<ActorStatesData>())
            .SelectMany(frame => new[] { frame.Think, frame.Action }).Where(call => !string.IsNullOrWhiteSpace(call))
            .Select(call => CallName(call!)).Distinct(StringComparer.Ordinal);
        foreach (var call in calls)
        {
            if (GameActions.Find(call) is { } action)
            {
                if (action.Needs is { } needs && !ActorClasses.IsA(entry.EngineClass, needs))
                    yield return $"{call} is for a {needs} (parent: {needs}), so it does nothing here";
            }
            else if (!luaActions.Contains(call))
                yield return $"{call} isn't an action the engine or a script has";
        }

        // Its own properties and flags: ones the engine doesn't read, or only another kind reads
        foreach (var key in entry.Own.Properties.Keys)
        {
            if (ActorProperties.Find(key) is not { } property)
                yield return $"The engine doesn't read the property {key} (a script may)";
            else if (property.Needs is { } needs && !ActorClasses.IsA(entry.EngineClass, needs))
                yield return $"{key} is only for a {needs} (parent: {needs}), so it does nothing here";
        }
        foreach (var flag in entry.Own.Flags.Select(flag => flag.TrimStart('~')))
        {
            if (ActorFlags.Find(flag) is not { } known)
                yield return $"The engine doesn't check the flag {flag}";
            else if (!entry.Own.Flags.Contains("~" + flag) && known.Needs is { } needs && !ActorClasses.IsA(entry.EngineClass, needs))
                yield return $"{flag} is only for a {needs} (parent: {needs}), so it does nothing here";
        }
    }

    /// <summary>The actions the game's scripts define (game.defineaction), by name</summary>
    public static HashSet<string> LuaActions(GameContent content)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var script in ScriptCatalog.Build(content))
        {
            if (ScriptCatalog.Read(content, script.Name) is not { } text)
                continue;
            try
            {
                foreach (var action in ScriptCatalog.Check(text, script.Name).Actions)
                    names.Add(action.Name);
            }
            catch (Exception)
            {
                // a script that can't be read defines nothing the editor can see
            }
        }
        return names;
    }

    /// <summary>A think's or action's name: A_FireProjectile("Rocket") → A_FireProjectile</summary>
    public static string CallName(string call)
    {
        call = call.Trim();
        int paren = call.IndexOf('(');
        return (paren < 0 ? call : call[..paren]).Trim();
    }

    /// <summary>A call's arguments as written, quotes taken off: A_GiveInventory("Clip", 25) → Clip, 25</summary>
    public static List<string> CallArguments(string call)
    {
        int open = call.IndexOf('('), close = call.LastIndexOf(')');
        var args = new List<string>();
        if (open < 0 || close <= open)
            return args;
        var inside = call[(open + 1)..close];
        var current = new System.Text.StringBuilder();
        bool quoted = false;
        foreach (var c in inside)
        {
            if (c == '"')
                quoted = !quoted;
            else if (c == ',' && !quoted)
            {
                args.Add(current.ToString().Trim());
                current.Clear();
            }
            else
                current.Append(c);
        }
        if (current.Length > 0 || args.Count > 0)
            args.Add(current.ToString().Trim());
        return args;
    }

    //
    // Pictures
    //

    /// <summary>
    /// The sprite a frame shows from a view (1 front to 8; a sprite with a single view, 0, shows
    /// it from every side), or null for none (TNT1, no sprite); <paramref name="found"/> says
    /// whether the game has it
    /// </summary>
    public static string? PictureName(GameContent content, ActorStateFrame frame, int view, out bool found)
    {
        found = false;
        if (string.IsNullOrEmpty(frame.Sprite) || frame.Sprite == "TNT1")
            return null;
        var single = frame.ShapeName(0);
        if (content.Find<SpriteAsset>(single) != null)
        {
            found = true;
            return single;
        }
        var turned = frame.ShapeName(Math.Clamp(view, 1, 8));
        found = content.Find<SpriteAsset>(turned) != null;
        return turned;
    }

    /// <summary>Whether a frame's sprite turns (has views 1 to 8 rather than a single one)</summary>
    public static bool Turns(GameContent content, ActorStateFrame frame)
        => !string.IsNullOrEmpty(frame.Sprite) && content.Find<SpriteAsset>(frame.ShapeName(0)) == null
           && content.Find<SpriteAsset>(frame.ShapeName(1)) != null;

    /// <summary>A state's frames from its first, in order, while they're the state's own (not where it goes after)</summary>
    public static List<ActorStateFrame> FramesOf(ActorStateFrame first)
    {
        var frames = new List<ActorStateFrame>();
        var seen = new HashSet<ActorStateFrame>(ReferenceEqualityComparer.Instance);
        for (var frame = first; frame != null && frame.StateName == first.StateName && seen.Add(frame); frame = frame.Next)
            frames.Add(frame);
        return frames;
    }

    //
    // Where each class is defined
    //

    private static void AddDefinitionSites(GameContent content, Dictionary<string, ActorEntry> entries)
    {
        var basePk3 = new Pk3AssetSource(Path.Combine(content.GameFolder, AssetManager.BasePk3FileName));
        foreach (var assetName in new[] { "actordefs", $"{content.PackId}/actordefs" })
        {
            var origins = content.Assets.FindAssetOrigins(assetName)
                .Where(asset => asset.Type == nameof(ActorTranslationAsset))
                .SelectMany(asset => asset.Origins)
                .Where(origin => origin.Action != AssetOrigin.LeftOut);
            foreach (var origin in origins)
            {
                DefinitionFile? file = null;
                try
                {
                    if (origin.Source.Equals(basePk3.Name, StringComparison.OrdinalIgnoreCase))
                        file = DefinitionFile.Read(basePk3, null, origin.Path);
                    else if (content.Assets.LoadedMods.FirstOrDefault(mod => mod.Source.Name.Equals(origin.Source, StringComparison.OrdinalIgnoreCase)) is { } mod)
                        file = DefinitionFile.Read(mod.Source, mod.FullPath, origin.Path);
                }
                catch (Exception)
                {
                    continue;
                }
                if (file?.Tree is not { } tree)
                    continue;

                var keys = tree.Children.Keys.OfType<YamlScalarNode>().ToList();
                var lines = file.Text.Replace("\r\n", "\n").Split('\n');
                for (int i = 0; i < keys.Count; i++)
                {
                    if (!entries.TryGetValue(keys[i].Value ?? "", out var entry))
                        continue;
                    int start = (int)keys[i].Start.Line - 1;
                    int end = i + 1 < keys.Count ? (int)keys[i + 1].Start.Line - 1 : lines.Length;
                    // Comments and blank lines before the next class are about it, not this one
                    while (end > start + 1 && (string.IsNullOrWhiteSpace(lines[end - 1]) || lines[end - 1].TrimStart().StartsWith('#')))
                        end--;
                    entry.DefinedIn.Add(new ActorDefinitionSite(file.Label, string.Join("\n", lines[start..end])));
                }
            }
        }
    }

    //
    // What names each class
    //

    private static void AddUses(GameContent content, Dictionary<string, ActorEntry> entries)
    {
        void Add(string className, ActorUse use)
        {
            if (entries.TryGetValue(className, out var entry) && !entry.Uses.Contains(use))
                entry.Uses.Add(use);
        }

        foreach (var (name, label) in ActorClasses.EngineUses)
            Add(name, new ActorUse(label, Names: false));

        // Other classes: built on it, a property naming it, an action given it
        foreach (var entry in entries.Values.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
        {
            var own = entry.Own;
            if (!string.IsNullOrEmpty(own.Parent))
                Add(own.Parent, new ActorUse($"{entry.Name} is built on it (parent)", new ActorUseTarget(Actor: entry.Name), Names: false));
            // A property that names a class (dropitem, spawn.random...), not one that happens to match (a tag)
            foreach (var (key, value) in own.Properties.Where(property => ActorProperties.Find(property.Key)?.Hint == FieldHint.Class))
                foreach (var named in Strings(value).Where(named => entries.ContainsKey(named) && named != entry.Name))
                    Add(named, new ActorUse($"{entry.Name}: property {key}", new ActorUseTarget(Actor: entry.Name)));
            foreach (var (state, list) in own.States)
                foreach (var frame in list.OfType<ActorStatesData>())
                    foreach (var call in new[] { frame.Action, frame.Think }.Where(call => !string.IsNullOrWhiteSpace(call)))
                        foreach (var named in CallArguments(call!).Where(entries.ContainsKey))
                            Add(named, new ActorUse($"{entry.Name}: its {state} state's {CallName(call!)}", new ActorUseTarget(Actor: entry.Name)));
        }

        // mapdefs, game-info and factions: wherever a value is a class's name
        Scan(content.MapDefs, "mapdefs", DefinitionKind.MapDefs, 2, entries, Add);
        Scan(content.GameInfo, "game-info", DefinitionKind.GameInfo, 1, entries, Add);
        Scan(content.Assets.FindInGamePackIfAny<FactionsAsset>("factions"), "factions", DefinitionKind.Factions, 1, entries, Add);

        // Lua: a class's name in a string given to spawn, spawnprojectile, actors or tagged
        foreach (var script in ScriptCatalog.Build(content))
        {
            if (ScriptCatalog.Read(content, script.Name) is not { } text)
                continue;
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                foreach (Match call in ClassCall().Matches(lines[i]))
                    foreach (Match literal in StringLiteral().Matches(call.Groups["args"].Value))
                        if (entries.ContainsKey(literal.Groups["s"].Value))
                            Add(literal.Groups["s"].Value, new ActorUse($"script {script.Name}, line {i + 1}: {call.Groups["fn"].Value}",
                                new ActorUseTarget(Script: script.Name, Line: i + 1)));
        }
    }

    [GeneratedRegex(@"(?<fn>spawnprojectile|spawn|actors|tagged)\s*\((?<args>[^)]*)\)")]
    private static partial Regex ClassCall();

    [GeneratedRegex(@"""(?<s>[^""]*)""|'(?<s>[^']*)'")]
    private static partial Regex StringLiteral();

    private static IEnumerable<string> Strings(object? value) => value switch
    {
        string text => [text],
        IEnumerable<object> list => list.SelectMany(Strings),
        _ => [],
    };

    private static void Scan(object? asset, string file, DefinitionKind kind, int entryKeys, Dictionary<string, ActorEntry> entries, Action<string, ActorUse> add)
    {
        var found = new List<(IReadOnlyList<string> Path, string Value)>();
        Walk(asset, [], found, entries, 0);

        // mapdefs things: a line for each class with every thing id it's under, not one for each id
        if (kind == DefinitionKind.MapDefs)
        {
            var things = found.Where(item => item.Path is ["things", _, "class"]).ToList();
            foreach (var group in things.GroupBy(item => item.Value, StringComparer.Ordinal))
            {
                var ids = group.Select(item => int.TryParse(item.Path[1], out var id) ? id : -1).Where(id => id >= 0).Order().ToList();
                add(group.Key, new ActorUse($"mapdefs: thing{(ids.Count > 1 ? "s" : "")} {Ranges(ids)}",
                    new ActorUseTarget(Kind: kind, EntryPath: ["things", group.First().Path[1]], FieldPath: ["class"])));
            }
            found = [.. found.Except(things)];
        }

        foreach (var (path, className) in found)
        {
            var label = $"{file}: {string.Join(" › ", path)}";
            add(className, new ActorUse(label, path.Count >= entryKeys
                ? new ActorUseTarget(Kind: kind, EntryPath: [.. path.Take(entryKeys)], FieldPath: path.Count > entryKeys ? [.. path.Skip(entryKeys)] : null)
                : null));
        }
    }

    private static readonly HashSet<string> ShownText = new(StringComparer.OrdinalIgnoreCase)
        { "name", "tag", "title", "description", "label", "text", "message", "caption", "hint" };

    private static void Walk(object? value, List<string> path, List<(IReadOnlyList<string>, string)> found, Dictionary<string, ActorEntry> entries, int depth)
    {
        if (value == null || depth > 12)
            return;
        if (value is string text)
        {
            // A name or words shown to the player can happen to be a class's name (a player class called Guard)
            if (entries.ContainsKey(text) && path.Count > 0 && !ShownText.Contains(path[^1]))
                found.Add(([.. path], text));
            return;
        }
        var type = value.GetType();
        if (value is byte[] || type.IsPrimitive || type.IsEnum || value is decimal)
            return;

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry item in dictionary)
                Walk(item.Value, [.. path, Convert.ToString(item.Key, CultureInfo.InvariantCulture) ?? ""], found, entries, depth + 1);
            return;
        }
        if (value is IEnumerable list)
        {
            int index = 0;
            foreach (var item in list)
                Walk(item, [.. path, (index++).ToString(CultureInfo.InvariantCulture)], found, entries, depth + 1);
            return;
        }
        if (type.Namespace?.StartsWith("PFWolf", StringComparison.Ordinal) != true)
            return;

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || property.DeclaringType == typeof(Asset))
                continue;
            object? child;
            try
            {
                child = property.GetValue(value);
            }
            catch (TargetInvocationException)
            {
                continue;
            }
            Walk(child, [.. path, FontCatalog.Kebab(property.Name)], found, entries, depth + 1);
        }
    }

    /// <summary>Numbers as runs: 108-115, 144, 146-151</summary>
    public static string Ranges(IReadOnlyList<int> numbers)
    {
        var parts = new List<string>();
        for (int i = 0; i < numbers.Count;)
        {
            int j = i;
            while (j + 1 < numbers.Count && numbers[j + 1] == numbers[j] + 1)
                j++;
            parts.Add(j > i ? $"{numbers[i]}-{numbers[j]}" : numbers[i].ToString(CultureInfo.InvariantCulture));
            i = j + 1;
        }
        return string.Join(", ", parts);
    }

    //
    // The levels that place each class
    //

    private static void AddLevelUses(GameContent content, Dictionary<string, ActorEntry> entries)
    {
        foreach (var map in content.Maps)
        {
            if (content.FindMap(map.Name) is not { } asset)
                continue;
            MapTiles tiles;
            try
            {
                tiles = new MapTiles(content, asset);
            }
            catch (Exception)
            {
                continue;
            }
            var counts = new Dictionary<string, (int Count, int X, int Y)>(StringComparer.Ordinal);
            void Count(string className, int x, int y)
                => counts[className] = counts.TryGetValue(className, out var had) ? (had.Count + 1, had.X, had.Y) : (1, x, y);
            for (int y = 0; y < tiles.Height; y++)
                for (int x = 0; x < tiles.Width; x++)
                    if (tiles.Thing(x, y) is { Class.Length: > 0 } thing)
                        Count(thing.Class, x, y);
            foreach (var thing in tiles.ExtraThings)
                Count(thing.Class, thing.TileX, thing.TileY);

            foreach (var (className, (count, x, y)) in counts)
                if (entries.TryGetValue(className, out var entry))
                    entry.Uses.Add(new ActorUse($"level {map.Name}{(string.IsNullOrWhiteSpace(map.Title) ? "" : $" ({map.Title})")}: {count} placed",
                        new ActorUseTarget(Map: map.Name, X: x, Y: y)));
        }
    }
}
