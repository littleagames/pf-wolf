using PFWolf.Editor.Definitions;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.Editing;

/// <summary>
/// A menu as its fields edit it, and back: menudefs give a component's params as a list of
/// one-key mappings (`- x: 68`), which the fields edit as one mapping (MenuSchema.Params). Back
/// in menudefs' form, params keep the order they had where they're the same keys.
/// </summary>
public static class MenuEditing
{
    /// <summary>The menu with each component's params as one mapping</summary>
    public static YamlMappingNode ToEditable(YamlMappingNode menu)
    {
        var copy = (YamlMappingNode)YamlNodes.Clone(menu);
        foreach (var component in Components(copy))
        {
            if (ParamsKey(component) is not { } key || component.Children[key] is not YamlSequenceNode list)
                continue;
            var mapping = new YamlMappingNode();
            foreach (var item in list.Children.OfType<YamlMappingNode>())
                foreach (var (name, value) in item.Children)
                    if (YamlTree.FindKey(mapping, name) == null)
                        mapping.Add(YamlNodes.Clone(name), YamlNodes.Clone(value));
            component.Children[key] = mapping;
        }
        return copy;
    }

    /// <summary>
    /// The edited menu in menudefs' form: params a list of one-key mappings again, in the order
    /// <paramref name="reference"/>'s same component had them (by id, else by place), new ones after
    /// </summary>
    public static YamlMappingNode FromEditable(YamlMappingNode edited, YamlMappingNode? reference)
    {
        var copy = (YamlMappingNode)YamlNodes.Clone(edited);
        var referenceComponents = reference == null ? [] : Components(reference).ToList();
        var components = Components(copy).ToList();
        for (int i = 0; i < components.Count; i++)
        {
            var component = components[i];
            if (ParamsKey(component) is not { } key || component.Children[key] is not YamlMappingNode mapping)
                continue;

            var id = Scalar(component, "id");
            var before = id != null
                ? referenceComponents.FirstOrDefault(other => string.Equals(Scalar(other, "id"), id, StringComparison.OrdinalIgnoreCase))
                : i < referenceComponents.Count && Scalar(referenceComponents[i], "id") == null ? referenceComponents[i] : null;
            var order = (before != null && ParamsKey(before) is { } beforeKey && before.Children[beforeKey] is YamlSequenceNode beforeList
                    ? beforeList.Children.OfType<YamlMappingNode>().SelectMany(item => item.Children.Keys).OfType<YamlScalarNode>().Select(name => name.Value ?? "")
                    : [])
                .ToList();

            var list = new YamlSequenceNode();
            foreach (var name in order.Concat(mapping.Children.Keys.OfType<YamlScalarNode>().Select(name => name.Value ?? "")).Distinct(StringComparer.OrdinalIgnoreCase))
                if (YamlNodes.FindKey(mapping, name) is { } found)
                    list.Add(new YamlMappingNode { { YamlNodes.Clone(found), YamlNodes.Clone(mapping.Children[found]) } });
            component.Children[key] = list;
        }
        return copy;
    }

    private static IEnumerable<YamlMappingNode> Components(YamlMappingNode menu)
        => YamlNodes.FindKey(menu, "components") is { } key && menu.Children[key] is YamlSequenceNode list
            ? list.Children.OfType<YamlMappingNode>()
            : [];

    private static YamlNode? ParamsKey(YamlMappingNode component) => YamlNodes.FindKey(component, "params");

    private static string? Scalar(YamlMappingNode node, string key)
        => YamlNodes.FindKey(node, key) is { } found && node.Children[found] is YamlScalarNode { Value: { Length: > 0 } value } ? value : null;
}
