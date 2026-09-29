using YamlDotNet.RepresentationModel;

namespace Wolf3D.Loaders;

/// <summary>
/// YAML documents as trees, so a mod's file can change just part of an asset. Laid over another
/// document, its mappings merge in key by key, all the way down; anything else (a value, a list)
/// replaces what was there. Keys match exactly, as they do in the assets' dictionaries.
/// </summary>
internal static class YamlTree
{
    /// <summary>The document's top-level mapping; null when it's empty or isn't a mapping</summary>
    public static YamlMappingNode? Parse(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return stream.Documents.Count > 0 ? stream.Documents[0].RootNode as YamlMappingNode : null;
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
    /// or with <paramref name="levels"/> 2 one level down (mapdefs, thing by thing inside things:)
    /// </summary>
    public static void MergeLevels(YamlMappingNode target, YamlMappingNode overlay, int levels)
    {
        foreach (var (key, value) in overlay.Children)
        {
            var existingKey = FindKey(target, key);
            if (levels > 1 && existingKey != null && target.Children[existingKey] is YamlMappingNode targetChild
                && value is YamlMappingNode overlayChild)
            {
                MergeLevels(targetChild, overlayChild, levels - 1);
                continue;
            }

            Set(target, key, value);
        }
    }

    /// <summary>Lays the overlay over the target, merging the mappings both have</summary>
    public static void DeepMerge(YamlMappingNode target, YamlMappingNode overlay)
    {
        foreach (var (key, value) in overlay.Children)
        {
            var existingKey = FindKey(target, key);
            if (existingKey != null && target.Children[existingKey] is YamlMappingNode targetChild
                && value is YamlMappingNode overlayChild)
            {
                DeepMerge(targetChild, overlayChild);
                continue;
            }

            Set(target, key, value);
        }
    }

    // Replacing an existing key keeps its place, so the merged document reads in the base's order
    private static void Set(YamlMappingNode target, YamlNode key, YamlNode value)
    {
        var existingKey = FindKey(target, key);
        if (existingKey != null)
            target.Children[existingKey] = value;
        else
            target.Children.Add(key, value);
    }

    private static YamlNode? FindKey(YamlMappingNode node, YamlNode key)
    {
        if (key is not YamlScalarNode scalarKey)
            return node.Children.ContainsKey(key) ? key : null;

        return node.Children.Keys.FirstOrDefault(existing =>
            existing is YamlScalarNode scalar && string.Equals(scalar.Value, scalarKey.Value, StringComparison.Ordinal));
    }
}
