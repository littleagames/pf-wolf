using YamlDotNet.RepresentationModel;

namespace PFWolf.Loaders;

/// <summary>
/// YAML documents as trees, which is how every YAML asset's files combine: pfwolf.pk3's files of
/// an asset (and a base pack's under the running pack's names) a set number of levels down (see
/// MergeLevels), a mod's file all the way down (see DeepMerge), and the asset is read from what
/// that makes. Keys match exactly, as they do in the assets' dictionaries. A key tagged
/// <c>!remove</c> (<c>health: !remove</c>, <c>guard: !remove</c>) takes that key out of the
/// document instead.
/// </summary>
public static class YamlTree
{
    public const string RemoveTag = "!remove";

    /// <summary>The document's top-level mapping; null when it's empty or isn't a mapping</summary>
    public static YamlMappingNode? Parse(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return stream.Documents.Count > 0 ? stream.Documents[0].RootNode as YamlMappingNode : null;
    }

    /// <summary>Whether the text holds no document at all (nothing, or only comments)</summary>
    public static bool IsEmptyDocument(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return stream.Documents.Count == 0;
    }

    public static string ToText(YamlMappingNode node)
    {
        using var writer = new StringWriter();
        new YamlStream(new YamlDocument(node)).Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    /// <summary>A copy that can be changed without touching the original</summary>
    public static YamlMappingNode Clone(YamlMappingNode node)
        => Parse(ToText(node)) ?? new YamlMappingNode();

    /// <summary>
    /// Replaces or adds the overlay's keys the way the base pk3's files of the same asset
    /// combine: at the top level (actordefs/wolf3d/guards.yaml then bosses.yaml, actor by actor),
    /// or with <paramref name="levels"/> 2 one level down (mapdefs, thing by thing inside things:).
    /// The overlay's keys tagged !remove take the target's out, at any of those levels.
    /// </summary>
    public static void MergeLevels(YamlMappingNode target, YamlMappingNode overlay, int levels)
    {
        foreach (var (key, value) in overlay.Children)
        {
            var existingKey = FindKey(target, key);
            if (IsRemove(value))
            {
                if (existingKey != null)
                    target.Children.Remove(existingKey);
                continue;
            }

            if (levels > 1 && existingKey != null && target.Children[existingKey] is YamlMappingNode targetChild
                && value is YamlMappingNode overlayChild)
            {
                MergeLevels(targetChild, overlayChild, levels - 1);
                continue;
            }

            Set(target, key, WithoutRemovals(value));
        }
    }

    /// <summary>
    /// Lays the overlay over the target, merging the mappings both have; the overlay's keys
    /// tagged !remove take the target's key out (one the target doesn't have is ignored)
    /// </summary>
    public static void DeepMerge(YamlMappingNode target, YamlMappingNode overlay)
    {
        foreach (var (key, value) in overlay.Children)
        {
            var existingKey = FindKey(target, key);
            if (IsRemove(value))
            {
                if (existingKey != null)
                    target.Children.Remove(existingKey);
                continue;
            }

            if (existingKey != null && target.Children[existingKey] is YamlMappingNode targetChild
                && value is YamlMappingNode overlayChild)
            {
                DeepMerge(targetChild, overlayChild);
                continue;
            }

            Set(target, key, WithoutRemovals(value));
        }
    }

    /// <summary>Whether the document has a key tagged !remove anywhere in it</summary>
    public static bool HasRemovals(YamlNode node) => node switch
    {
        YamlMappingNode mapping => mapping.Children.Any(child => IsRemove(child.Value) || HasRemovals(child.Value)),
        YamlSequenceNode sequence => sequence.Children.Any(HasRemovals),
        _ => false,
    };

    /// <summary>
    /// The node with its !remove keys left out (so it reads on its own, where there's nothing
    /// to remove them from); the node itself when it has none
    /// </summary>
    public static T WithoutRemovals<T>(T node) where T : YamlNode
    {
        if (!HasRemovals(node))
            return node;

        YamlNode stripped = node switch
        {
            YamlMappingNode mapping => new YamlMappingNode(mapping.Children
                .Where(child => !IsRemove(child.Value))
                .Select(child => new KeyValuePair<YamlNode, YamlNode>(child.Key, WithoutRemovals(child.Value))))
                { Style = mapping.Style },
            YamlSequenceNode sequence => new YamlSequenceNode(sequence.Children.Select(WithoutRemovals))
                { Style = sequence.Style },
            _ => node,
        };
        return (T)stripped;
    }

    /// <summary>Whether the value is tagged !remove</summary>
    public static bool IsRemove(YamlNode value)
        => !value.Tag.IsEmpty && value.Tag.Value == RemoveTag;

    /// <summary>
    /// Replaces the key's value, or adds it at the end. Replacing an existing key keeps its place,
    /// so the merged document reads in the base's order.
    /// </summary>
    public static void Set(YamlMappingNode target, YamlNode key, YamlNode value)
    {
        var existingKey = FindKey(target, key);
        if (existingKey != null)
            target.Children[existingKey] = value;
        else
            target.Children.Add(key, value);
    }

    /// <summary>The node's own key matching <paramref name="key"/>, or null</summary>
    public static YamlNode? FindKey(YamlMappingNode node, YamlNode key, StringComparison comparison = StringComparison.Ordinal)
    {
        if (key is not YamlScalarNode scalarKey)
            return node.Children.ContainsKey(key) ? key : null;

        return node.Children.Keys.FirstOrDefault(existing =>
            existing is YamlScalarNode scalar && string.Equals(scalar.Value, scalarKey.Value, comparison));
    }
}
