using PFWolf.Assets;
using PFWolf.Editor.Definitions;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.Editing;

/// <summary>
/// A mod's menudefs file worked out from a menu edited whole: the least it has to say for the menu
/// beneath it (what pfwolf.pk3 and the mods before make) to become the edited one, in
/// MenuAsset.MergeYaml's terms. Keys that changed are written as they are; menu-items and
/// components entries by their id (a Customize row by its control) with only their changed keys,
/// `remove: true` for one taken out, and `before:`/`after:` where the order changed. A list whose
/// change can't be said that way (entries without ids moved or changed) is written whole, tagged
/// !replace. Every overlay is checked by laying it over the menu beneath as the game does.
/// </summary>
public static class MenuOverlay
{
    private static readonly string[] Lists = ["menu-items", "components"];

    /// <summary>The overlay, and notes on what had to be written whole (and why)</summary>
    /// <param name="under">The menu as the files before the mod's make it; null when it's the mod's own new menu</param>
    public static (YamlMappingNode Overlay, List<string> Notes) Plan(YamlMappingNode? under, YamlMappingNode edited)
    {
        var notes = new List<string>();
        if (under == null)
            return ((YamlMappingNode)YamlNodes.Clone(edited), notes);

        var overlay = new YamlMappingNode();

        // Every key but the lists: a changed one whole (it replaces the one beneath), a gone one !remove
        foreach (var (key, value) in edited.Children)
        {
            var name = (key as YamlScalarNode)?.Value ?? "";
            if (Lists.Contains(name, StringComparer.Ordinal))
                continue;
            var below = YamlNodes.FindKey(under, name) is { } underKey ? under.Children[underKey] : null;
            if (!YamlNodes.Equal(below, value))
                overlay.Add(name, YamlNodes.Clone(value));
        }
        foreach (var (key, _) in under.Children)
        {
            var name = (key as YamlScalarNode)?.Value ?? "";
            if (!Lists.Contains(name, StringComparer.Ordinal) && YamlNodes.FindKey(edited, name) == null)
                overlay.Add(name, YamlNodes.Remove());
        }

        foreach (var list in Lists)
        {
            var below = List(under, list);
            var now = List(edited, list);
            if (YamlNodes.Equal(below, now))
                continue;
            if (now == null)
            {
                overlay.Add(list, YamlNodes.Remove());
                continue;
            }
            if (below == null)
            {
                overlay.Add(list, YamlNodes.Clone(now));
                continue;
            }

            // Entry by entry, checked; else the whole list
            var entries = Entries(below, now);
            if (entries != null && Lays(below, entries, now))
            {
                if (entries.Children.Count > 0)
                    overlay.Add(list, entries);
            }
            else
            {
                var whole = (YamlSequenceNode)YamlNodes.Clone(now);
                whole.Tag = YamlTree.ReplaceTag;
                overlay.Add(list, whole);
                notes.Add($"{list} is written whole (!replace): entries without an id changed or moved, which a mod can't say entry by entry. "
                          + "It won't take in later changes pfwolf.pk3 makes to that list; give its entries ids to avoid that.");
            }
        }

        // Laid over the menu beneath as the game lays it, it has to make the edited one
        if (!YamlNodes.Equal(Apply(under, overlay), edited))
        {
            var whole = (YamlMappingNode)YamlNodes.Clone(edited);
            foreach (var list in Lists)
                if (List(whole, list) is { } sequence)
                    sequence.Tag = YamlTree.ReplaceTag;
            foreach (var (key, _) in under.Children)
                if (YamlNodes.FindKey(whole, (key as YamlScalarNode)?.Value ?? "") == null)
                    whole.Add(YamlNodes.Clone(key), YamlNodes.Remove());
            notes.Add("The whole menu is written (its lists !replace): the change couldn't be said as changed keys.");
            return (whole, notes);
        }
        return (overlay, notes);
    }

    /// <summary>The menu an overlay makes laid over the one beneath, as the game lays a mod's file (MenuAsset.MergeYaml)</summary>
    public static YamlMappingNode Apply(YamlMappingNode? under, YamlMappingNode overlay)
    {
        if (under == null)
            return YamlTree.WithoutMergeTags((YamlMappingNode)YamlNodes.Clone(overlay));
        var merged = YamlTree.Clone(under);
        MenuAsset.MergeYaml(merged, (YamlMappingNode)YamlNodes.Clone(overlay), true);
        return merged;
    }

    /// <summary>A menu file's text, without the document-end mark YAML writes after it</summary>
    public static string ToText(YamlMappingNode menu)
    {
        var text = YamlTree.ToText(menu).Replace("\r\n", "\n").TrimEnd();
        if (text.EndsWith("\n...", StringComparison.Ordinal))
            text = text[..^4].TrimEnd();
        return text == "{}" ? "" : text + "\n";
    }

    private static YamlSequenceNode? List(YamlMappingNode menu, string key)
        => YamlNodes.FindKey(menu, key) is { } found ? menu.Children[found] as YamlSequenceNode : null;

    // What an entry is matched by: its id, else (a Customize row) its control; null for neither
    private static (string Key, string Value)? KeyOf(YamlNode entry)
    {
        if (entry is not YamlMappingNode mapping)
            return null;
        foreach (var key in new[] { "id", "control" })
            if (YamlNodes.FindKey(mapping, key) is { } found && mapping.Children[found] is YamlScalarNode { Value: { Length: > 0 } value })
                return (key, value);
        return null;
    }

    private static bool SameKey((string Key, string Value)? a, (string Key, string Value)? b)
        => a != null && b != null && string.Equals(a.Value.Value, b.Value.Value, StringComparison.OrdinalIgnoreCase);

    // The entries that make the list beneath into the edited one, or null when its entries can't
    // be told apart by id (an entry without one that isn't just as it was, where it was)
    private static YamlSequenceNode? Entries(YamlSequenceNode below, YamlSequenceNode now)
    {
        // Entries without ids: only the ones beneath, unchanged and in the same order among themselves, can stay
        var unkeyedBelow = below.Children.Where(entry => KeyOf(entry) == null).ToList();
        var unkeyedNow = now.Children.Where(entry => KeyOf(entry) == null).ToList();
        if (unkeyedBelow.Count != unkeyedNow.Count || !unkeyedBelow.Zip(unkeyedNow).All(pair => YamlNodes.Equal(pair.First, pair.Second)))
            return null;
        var keys = now.Children.Select(KeyOf).Where(key => key != null).ToList();
        if (keys.Count != keys.DistinctBy(key => key!.Value.Value.ToLowerInvariant()).Count())
            return null;    // two entries with one id: the game would merge them into one

        var entries = new YamlSequenceNode();

        // Taken out
        foreach (var entry in below.Children)
        {
            if (KeyOf(entry) is { } key && !now.Children.Any(other => SameKey(KeyOf(other), key)))
                entries.Add(new YamlMappingNode { { key.Key, key.Value }, { "remove", "true" } });
        }

        // Changed and new, in the edited order
        var byKey = new Dictionary<string, YamlMappingNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in now.Children)
        {
            if (KeyOf(entry) is not { } key)
                continue;
            var old = below.Children.FirstOrDefault(other => SameKey(KeyOf(other), key)) as YamlMappingNode;
            var mapping = (YamlMappingNode)entry;
            YamlMappingNode written;
            if (old == null)
                written = (YamlMappingNode)YamlNodes.Clone(mapping);
            else
            {
                written = new YamlMappingNode { { key.Key, key.Value } };
                foreach (var (field, value) in mapping.Children)
                {
                    var name = (field as YamlScalarNode)?.Value ?? "";
                    if (name.Equals(key.Key, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var before = YamlNodes.FindKey(old, name) is { } oldKey ? old.Children[oldKey] : null;
                    if (!YamlNodes.Equal(before, value))
                        written.Add(name, YamlNodes.Clone(value));
                }
                foreach (var (field, _) in old.Children)
                {
                    var name = (field as YamlScalarNode)?.Value ?? "";
                    if (YamlNodes.FindKey(mapping, name) == null)
                        written.Add(name, YamlNodes.Remove());
                }
            }
            byKey[key.Value] = written;
        }

        // Out of place as they're laid: the first one out of place goes after the one before it
        // (the first before what's first), and again, until they're in order. Written in the
        // edited order, each is placed after the one it goes after has been.
        var hints = new Dictionary<string, (string Key, string Value)>(StringComparer.OrdinalIgnoreCase);
        var wanted = now.Children.Select(KeyOf).Where(key => key != null).Select(key => key!.Value.Value).ToList();
        YamlSequenceNode Written()
        {
            var list = new YamlSequenceNode(entries.Children);
            foreach (var key in wanted)
            {
                var written = (YamlMappingNode)YamlNodes.Clone(byKey[key]);
                if (hints.TryGetValue(key, out var hint))
                    written.Add(hint.Key, hint.Value);
                if (written.Children.Count > 1 || IsNew(below, written))
                    list.Add(written);
            }
            return list;
        }

        for (int tries = 0; tries <= wanted.Count; tries++)
        {
            var laidKeys = Lay(below, Written()).Children.Select(KeyOf).Where(key => key != null).Select(key => key!.Value.Value).ToList();
            int wrong = Enumerable.Range(0, wanted.Count).FirstOrDefault(i => i >= laidKeys.Count || !laidKeys[i].Equals(wanted[i], StringComparison.OrdinalIgnoreCase), -1);
            if (wrong < 0 || hints.ContainsKey(wanted[wrong]))
                break;
            hints[wanted[wrong]] = wrong > 0 ? ("after", wanted[wrong - 1]) : ("before", laidKeys.FirstOrDefault(key => !key.Equals(wanted[0], StringComparison.OrdinalIgnoreCase)) ?? wanted[0]);
        }
        return Written();
    }

    // A new entry (not beneath): written even with nothing but its id
    private static bool IsNew(YamlSequenceNode below, YamlMappingNode written)
        => !below.Children.Any(other => SameKey(KeyOf(other), KeyOf(written)));

    // The list the entries make laid over the one beneath (MenuAsset.MergeYaml's own merge)
    private static YamlSequenceNode Lay(YamlSequenceNode below, YamlSequenceNode entries)
    {
        var under = new YamlMappingNode { { "components", YamlNodes.Clone(below) } };
        MenuAsset.MergeYaml(under, new YamlMappingNode { { "components", YamlNodes.Clone(entries) } }, true);
        return (YamlSequenceNode)under.Children[new YamlScalarNode("components")];
    }

    private static bool Lays(YamlSequenceNode below, YamlSequenceNode entries, YamlSequenceNode now)
        => YamlNodes.Equal(Lay(below, entries), now);

    private static bool SameOrder(YamlSequenceNode a, YamlSequenceNode b)
        => a.Children.Select(entry => KeyOf(entry)?.Value?.ToLowerInvariant()).SequenceEqual(b.Children.Select(entry => KeyOf(entry)?.Value?.ToLowerInvariant()));
}
