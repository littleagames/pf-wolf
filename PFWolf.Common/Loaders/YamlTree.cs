using YamlDotNet.RepresentationModel;

namespace PFWolf.Loaders;

/// <summary>
/// YAML documents as trees, which is how every YAML asset's files combine: pfwolf.pk3's files of
/// an asset (and a base pack's under the running pack's names) a set number of levels down (see
/// MergeLevels), a mod's file all the way down (see DeepMerge), and the asset is read from what
/// that makes. Keys match exactly, as they do in the assets' dictionaries. A key tagged
/// <c>!remove</c> (<c>health: !remove</c>, <c>guard: !remove</c>) takes that key out of the
/// document instead, and one tagged <c>!replace</c> (<c>maps: !replace</c> and its own maps)
/// replaces what's there whole rather than merging into it.
/// </summary>
public static class YamlTree
{
    public const string RemoveTag = "!remove";
    public const string ReplaceTag = "!replace";

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
    /// The overlay's keys tagged !remove take the target's out, and those tagged !replace
    /// replace the target's whole rather than merging into it, at any of those levels.
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

            if (levels > 1 && !IsReplace(value) && existingKey != null
                && target.Children[existingKey] is YamlMappingNode targetChild && value is YamlMappingNode overlayChild)
            {
                MergeLevels(targetChild, overlayChild, levels - 1);
                continue;
            }

            Set(target, key, WithoutMergeTags(value));
        }
    }

    /// <summary>Lays the overlay over the target, merging the mappings both have (see MergeLevels)</summary>
    public static void DeepMerge(YamlMappingNode target, YamlMappingNode overlay)
        => MergeLevels(target, overlay, int.MaxValue);

    /// <summary>Whether the document has a key tagged !remove or !replace anywhere in it</summary>
    public static bool HasMergeTags(YamlNode node) => IsReplace(node) || node switch
    {
        YamlMappingNode mapping => mapping.Children.Any(child => IsRemove(child.Value) || HasMergeTags(child.Value)),
        YamlSequenceNode sequence => sequence.Children.Any(HasMergeTags),
        _ => false,
    };

    /// <summary>
    /// The node with its !remove keys left out and its !replace tags taken off (so it reads on
    /// its own, where there's nothing to remove or replace); the node itself when it has none
    /// </summary>
    public static T WithoutMergeTags<T>(T node) where T : YamlNode
    {
        if (!HasMergeTags(node))
            return node;

        YamlNode stripped = node switch
        {
            YamlMappingNode mapping => new YamlMappingNode(mapping.Children
                .Where(child => !IsRemove(child.Value))
                .Select(child => new KeyValuePair<YamlNode, YamlNode>(child.Key, WithoutMergeTags(child.Value))))
                { Style = mapping.Style },
            YamlSequenceNode sequence => new YamlSequenceNode(sequence.Children.Select(WithoutMergeTags))
                { Style = sequence.Style },
            YamlScalarNode scalar => new YamlScalarNode(scalar.Value) { Style = scalar.Style },
            _ => node,
        };
        return (T)stripped;
    }

    /// <summary>Whether the value is tagged !remove</summary>
    public static bool IsRemove(YamlNode value)
        => !value.Tag.IsEmpty && value.Tag.Value == RemoveTag;

    /// <summary>Whether the value is tagged !replace</summary>
    public static bool IsReplace(YamlNode value)
        => !value.Tag.IsEmpty && value.Tag.Value == ReplaceTag;

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
