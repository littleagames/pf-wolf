using PFWolf.Loaders;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.Definitions;

/// <summary>A change to a YAML document: the value at a key path set (or, with null, the key taken out)</summary>
public sealed record YamlChange(IReadOnlyList<string> Path, YamlNode? Value);

/// <summary>
/// YAML trees as the definitions editor works with them: found by key path, compared, and the
/// difference between two worked out as a list of changes or as an overlay that merges one over
/// the other, the way the loader lays a mod's file over the game's (see YamlTree)
/// </summary>
public static class YamlNodes
{
    /// <summary>The key in a mapping matching <paramref name="name"/>: exactly, else ignoring case</summary>
    public static YamlNode? FindKey(YamlMappingNode mapping, string name)
        => mapping.Children.Keys.FirstOrDefault(key => key is YamlScalarNode { Value: var value } && value == name)
           ?? mapping.Children.Keys.FirstOrDefault(key => key is YamlScalarNode { Value: var value }
                                                          && string.Equals(value, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The value at a key path, or null when a key on the way isn't there</summary>
    public static YamlNode? Get(YamlNode? node, IEnumerable<string> path)
    {
        foreach (var name in path)
        {
            if (node is not YamlMappingNode mapping || FindKey(mapping, name) is not { } key)
                return null;
            node = mapping.Children[key];
        }
        return node;
    }

    /// <summary>Sets the value at a key path in a tree, adding mappings on the way; null takes the key out</summary>
    public static void Set(YamlMappingNode root, IReadOnlyList<string> path, YamlNode? value)
    {
        if (path.Count == 0)
            throw new ArgumentException("A key path needs at least one key", nameof(path));

        var mapping = root;
        for (int i = 0; i < path.Count - 1; i++)
        {
            var key = FindKey(mapping, path[i]);
            if (key != null && mapping.Children[key] is YamlMappingNode child)
            {
                mapping = child;
                continue;
            }
            if (value == null)
                return;
            var created = new YamlMappingNode();
            if (key != null)
                mapping.Children[key] = created;
            else
                mapping.Children.Add(Scalar(path[i]), created);
            mapping = created;
        }

        var last = FindKey(mapping, path[^1]);
        if (value == null)
        {
            if (last != null)
                mapping.Children.Remove(last);
        }
        else if (last != null)
            mapping.Children[last] = value;
        else
            mapping.Children.Add(Scalar(path[^1]), value);
    }

    public static YamlScalarNode Scalar(string? value) => new(value ?? "");

    /// <summary>A key tagged !remove, which takes the key out of what it's merged over</summary>
    public static YamlScalarNode Remove() => new("") { Tag = YamlTree.RemoveTag, Style = ScalarStyle.Plain };

    public static YamlNode Clone(YamlNode node) => node switch
    {
        YamlMappingNode mapping => CloneMapping(mapping),
        YamlSequenceNode sequence => new YamlSequenceNode(sequence.Children.Select(Clone)) { Style = sequence.Style, Tag = sequence.Tag },
        YamlScalarNode scalar => new YamlScalarNode(scalar.Value) { Style = scalar.Style, Tag = scalar.Tag },
        _ => node,
    };

    private static YamlMappingNode CloneMapping(YamlMappingNode mapping)
    {
        var copy = new YamlMappingNode { Style = mapping.Style, Tag = mapping.Tag };
        foreach (var (key, value) in mapping.Children)
            copy.Children.Add(Clone(key), Clone(value));
        return copy;
    }

    /// <summary>
    /// Whether two nodes say the same: scalars by their text and tag (not how they're quoted),
    /// mappings by their keys and values in any order, sequences item by item
    /// </summary>
    public static bool Equal(YamlNode? a, YamlNode? b)
    {
        if (a == null || b == null)
            return a == null && b == null;

        switch (a)
        {
            case YamlScalarNode sa when b is YamlScalarNode sb:
                return (sa.Value ?? "") == (sb.Value ?? "") && TagOf(sa) == TagOf(sb);

            case YamlSequenceNode qa when b is YamlSequenceNode qb:
                return qa.Children.Count == qb.Children.Count && qa.Children.Zip(qb.Children).All(pair => Equal(pair.First, pair.Second));

            case YamlMappingNode ma when b is YamlMappingNode mb:
                if (ma.Children.Count != mb.Children.Count || TagOf(ma) != TagOf(mb))
                    return false;
                foreach (var (key, value) in ma.Children)
                {
                    if (key is not YamlScalarNode { Value: { } name } || ExactKey(mb, name) is not { } other || !Equal(value, mb.Children[other]))
                        return false;
                }
                return true;

            default:
                return false;
        }
    }

    private static string TagOf(YamlNode node) => node.Tag.IsEmpty ? "" : node.Tag.Value;

    private static YamlNode? ExactKey(YamlMappingNode mapping, string name)
        => mapping.Children.Keys.FirstOrDefault(key => key is YamlScalarNode { Value: var value } && value == name);

    /// <summary>
    /// The changes that make <paramref name="from"/> into <paramref name="to"/>, found under
    /// <paramref name="path"/>: mappings both have are gone into (where
    /// <paramref name="goInto"/> allows), and anything else that differs is set whole
    /// </summary>
    public static List<YamlChange> Changes(YamlNode? from, YamlNode? to, IReadOnlyList<string> path, Func<YamlMappingNode, bool>? goInto = null)
    {
        var changes = new List<YamlChange>();
        AddChanges(changes, from, to, [.. path], goInto ?? (_ => true));
        return changes;
    }

    private static void AddChanges(List<YamlChange> changes, YamlNode? from, YamlNode? to, List<string> path, Func<YamlMappingNode, bool> goInto)
    {
        if (Equal(from, to))
            return;

        if (from is YamlMappingNode fromMapping && to is YamlMappingNode toMapping && path.Count > 0 && goInto(fromMapping)
            && TagOf(fromMapping) == TagOf(toMapping) && toMapping.Children.Count > 0)
        {
            foreach (var (key, value) in toMapping.Children)
            {
                if (key is not YamlScalarNode { Value: { } name })
                    continue;
                var before = ExactKey(fromMapping, name) is { } fromKey ? fromMapping.Children[fromKey] : null;
                AddChanges(changes, before, value, [.. path, name], goInto);
            }
            // Taken out last, so the mapping is never left empty on the way (which would take it out too)
            foreach (var (key, _) in fromMapping.Children)
            {
                if (key is YamlScalarNode { Value: { } name } && ExactKey(toMapping, name) == null)
                    changes.Add(new YamlChange([.. path, name], null));
            }
            return;
        }

        changes.Add(new YamlChange([.. path], to));
    }

    /// <summary>
    /// What to lay over <paramref name="baseNode"/> so it merges into <paramref name="edited"/>:
    /// the keys that differ (going into mappings both have, until <paramref name="wholeAtDepth"/>
    /// keys down, where a value replaces what's there whole, as the loader merges), and the keys
    /// edited out, tagged !remove. Null when the two are the same.
    /// </summary>
    /// <param name="depth">How many keys down the two nodes are in their document</param>
    public static YamlNode? Overlay(YamlNode? baseNode, YamlNode? edited, int depth = 0, int wholeAtDepth = int.MaxValue)
    {
        if (Equal(baseNode, edited))
            return null;
        if (edited == null)
            return Remove();

        if (baseNode is YamlMappingNode baseMapping && edited is YamlMappingNode editedMapping && depth < wholeAtDepth
            && !YamlTree.IsReplace(editedMapping))
        {
            var overlay = new YamlMappingNode { Style = editedMapping.Style };
            foreach (var (key, value) in baseMapping.Children)
            {
                if (key is YamlScalarNode { Value: { } name } && ExactKey(editedMapping, name) == null)
                    overlay.Children.Add(Clone(key), Remove());
            }
            foreach (var (key, value) in editedMapping.Children)
            {
                if (key is not YamlScalarNode { Value: { } name })
                    continue;
                var before = ExactKey(baseMapping, name) is { } baseKey ? baseMapping.Children[baseKey] : null;
                if (Overlay(before, value, depth + 1, wholeAtDepth) is { } child)
                    overlay.Children.Add(Clone(key), child);
            }

            return overlay.Children.Count > 0 ? overlay : null;
        }

        return Clone(edited);
    }

    /// <summary>The key path written as it's shown: walls › 12</summary>
    public static string Describe(IEnumerable<string> path) => string.Join(" › ", path);
}
