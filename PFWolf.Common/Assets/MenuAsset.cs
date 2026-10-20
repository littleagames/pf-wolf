using System.Numerics;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Assets;

public record MenuAsset : Asset
{
    /// <summary>
    /// Lays a mod's menudefs file over the menu of the same name (MENU HOOKS): its keys replace the
    /// menu's, except `menu-items` and `components`, merged entry by entry. An entry with the `id`
    /// (or, for a control row, the `control`) of one the menu has changes that one, key by key;
    /// with `remove: true` takes it out; one the menu hasn't is added at the end, or with
    /// `before:`/`after:` an entry's id, next to it (an existing one moves there too). A list
    /// tagged !replace replaces the menu's whole.
    /// </summary>
    public static void MergeYaml(YamlMappingNode target, YamlMappingNode overlay, bool deep)
    {
        foreach (var (key, value) in overlay.Children)
        {
            var name = (key as YamlScalarNode)?.Value;
            var existingKey = YamlTree.FindKey(target, key);
            if (name is "menu-items" or "components" && value is YamlSequenceNode incoming && !YamlTree.IsReplace(value)
                && existingKey != null && target.Children[existingKey] is YamlSequenceNode existing)
            {
                MergeEntries(existing, incoming);
                continue;
            }
            YamlTree.MergeLevels(target, new YamlMappingNode { { key, value } }, 1);
        }
    }

    // The keys an entry is matched by, and the merge's own keys (left out of what's kept)
    static readonly string[] MergeKeys = ["before", "after", "remove"];

    static string? Scalar(YamlMappingNode node, string key) =>
        YamlTree.FindKey(node, new YamlScalarNode(key), StringComparison.OrdinalIgnoreCase) is { } found
        && node.Children[found] is YamlScalarNode { Value: { Length: > 0 } value } ? value : null;

    static string? EntryKey(YamlMappingNode entry) => Scalar(entry, "id") ?? Scalar(entry, "control");

    static int IndexOf(YamlSequenceNode list, string? key)
    {
        if (key == null)
            return -1;
        for (int i = 0; i < list.Children.Count; i++)
            if (list.Children[i] is YamlMappingNode entry && string.Equals(EntryKey(entry), key, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    static void MergeEntries(YamlSequenceNode existing, YamlSequenceNode incoming)
    {
        foreach (var node in incoming.Children)
        {
            if (node is not YamlMappingNode entry)
            {
                existing.Children.Add(node);
                continue;
            }

            int at = IndexOf(existing, EntryKey(entry));
            if (bool.TryParse(Scalar(entry, "remove"), out var remove) && remove)
            {
                if (at >= 0)
                    existing.Children.RemoveAt(at);
                continue;
            }

            string? before = Scalar(entry, "before"), after = Scalar(entry, "after");
            var clean = new YamlMappingNode(entry.Children.Where(c =>
                !MergeKeys.Contains((c.Key as YamlScalarNode)?.Value ?? "", StringComparer.OrdinalIgnoreCase)));

            YamlNode placed = clean;
            if (at >= 0)
            {
                var merged = (YamlMappingNode)existing.Children[at];
                YamlTree.MergeLevels(merged, clean, 1);
                placed = merged;
                if (before == null && after == null)
                    continue;
                existing.Children.RemoveAt(at);
            }

            int anchor = IndexOf(existing, before ?? after);
            if (anchor < 0)
                existing.Children.Add(placed);
            else
                existing.Children.Insert(before != null ? anchor : anchor + 1, placed);
        }
    }

    public string? Music { get; init; }
    public string? Type { get; init; }
    public Vector2? Position { get; init; }
    public int Indent { get; init; } = 0;
    public string? ItemsSource { get; init; }
    public int? DefaultSelection { get; init; }
    public List<ComponentEntry>? Components { get; init; }
    public List<MenuItemEntry>? MenuItems { get; init; }
    public int? RowHeight { get; init; }
    /// <summary>How many items are shown at once (visible-rows); the rest scroll into view with the cursor. Unset, all of them.</summary>
    public int? VisibleRows { get; init; }
    public string? Font { get; init; }
    public string? ItemShadow { get; init; }
    public MenuCursorInfo? Cursor { get; init; }
    public bool? SpacerRows { get; init; }
    public MenuPoint? SelectionPic { get; init; }
    public MenuPoint? CheckboxOffset { get; init; }

    /// <summary>
    /// new-episode and new-level: the highlighted episode's or level's best (records.cfg) in a
    /// window beside the list, as Midnight Assault's DrawEpisodeStats and DrawLevelStats
    /// </summary>
    public MenuStatsPanel? StatsPanel { get; init; }
}

/// <summary>
/// "stats-panel": a window (x, y, width, height) with a title and lines of the highlighted
/// level's best, or the episode's levels' (their times added up, their percentages averaged with a
/// level never finished counting as nothing, their counts added up). Text starts at x + margin,
/// the title at title-x, each line line-height below the last, its value at value-x.
/// </summary>
public record MenuStatsPanel
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; } = 104;
    public int Height { get; init; } = 64;
    public string WindowColor { get; init; } = "BKGDCOLOR";
    public int Margin { get; init; } = 8;
    public int Top { get; init; } = 4;
    public string Font { get; init; } = "SmallFont";
    public string Color { get; init; } = "TEXTCOLOR";

    /// <summary>The title (text or a $language key), at title-x, in title-color</summary>
    public string Title { get; init; } = "";
    public int TitleX { get; init; } = 32;
    public string TitleColor { get; init; } = "HIGHLIGHT";

    public int LineHeight { get; init; } = 12;
    public int ValueX { get; init; } = 64;
    public List<MenuStatsLine> Lines { get; init; } = [];
}

/// <summary>
/// A stats-panel line: its label (text or a $language key) and value: time (h:mm:ss), kill,
/// secret (percentages), or objective.NAME, a percentage, or with count: true the number done.
/// With hide-empty, an objective line shows only where there's any of it (the campaign's Kilroys).
/// value-x moves the value from the panel's (the time's sits further left).
/// </summary>
public record MenuStatsLine
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";
    public bool Count { get; init; }
    public bool HideEmpty { get; init; }
    public int? ValueX { get; init; }
}

/// <summary>
/// A menu's highlight bar (Blake Stone's), in place of Wolf3D's gun: a bar behind the item the
/// cursor is on, flashing on and off. Colors are color names, #RRGGBB or palette indices.
/// </summary>
public record MenuCursorInfo
{
    /// <summary>The bar's left edge</summary>
    public int X { get; init; }

    /// <summary>How far below an item's top the bar starts</summary>
    public int YOffset { get; init; }

    public int Width { get; init; } = 100;
    public int Height { get; init; } = 9;
    public string Color { get; init; } = "HIGHLIGHT";

    /// <summary>What the bar is erased to: the menu's background</summary>
    public string EraseColor { get; init; } = "BKGDCOLOR";

    /// <summary>Tics the bar stays on, and then off, as it flashes</summary>
    public int FlashTics { get; init; } = 40;
}

public record MenuPoint
{
    public int X { get; init; }
    public int Y { get; init; }
}

public record ComponentEntry
{
    public string? Type { get; init; }
    /// <summary>
    /// Each param entry in YAML is a single-key mapping; represent as a list of dictionaries
    /// so values can be strings or numbers depending on the YAML file.
    /// </summary>
    public List<Dictionary<string, string>> Params { get; init; } = [];
    /// <summary>
    /// Only draw this in these game packs ("wolf3d", "spear"); unset means every pack
    /// </summary>
    public List<string>? GamePacks { get; init; }

    /// <summary>A name a mod's menudefs can change or remove this by ("title", say: MergeYaml)</summary>
    public string? Id { get; init; }

    /// <summary>MergeYaml's own (a mod's menudefs placing or removing an entry); nothing at run time</summary>
    public string? Before { get; init; }
    public string? After { get; init; }
    public bool Remove { get; init; }
}

public record MenuItemEntry
{
    /// <summary>
    /// Stable name code uses to find this item, so the YAML can be reordered safely
    /// </summary>
    public string? Id { get; init; } = null;
    public string Type { get; init; } = null!;
    public string Text { get; init; } = null!;
    public string? ShortKey { get; init; } = null;
    public bool Enabled { get; init; } = true;
    public string? Action { get; init; } = null;
    public string? Music { get; init; } = null;
    /// <summary>
    /// The control a row of the Customize Controls screen binds, by its controls.cfg name ("attack", "am_zoomin")
    /// </summary>
    public string? Control { get; init; } = null;
    /// <summary>
    /// Only include this item in these game packs ("wolf3d", "spear"); unset means every pack
    /// </summary>
    public List<string>? GamePacks { get; init; } = null;
    /// <summary>The presenter text a CP_TextScreen item shows (a VGAGRAPH text)</summary>
    public string? Script { get; init; } = null;
    /// <summary>Drawn in the read-it colors (READCOLOR, READHCOLOR), like Wolf3D's "Read This!"</summary>
    public bool Highlighted { get; init; } = false;

    /// <summary>The menudefs menu a CP_Menu item opens</summary>
    public string? Menu { get; init; } = null;

    /// <summary>The text (texts/) a CP_Article item shows, as an article (a level's briefing's reader)</summary>
    public string? Article { get; init; } = null;

    /// <summary>The script function (a Lua global) a CP_Lua item calls</summary>
    public string? Function { get; init; } = null;

    /// <summary>MergeYaml's own (a mod's menudefs placing or removing an entry); nothing at run time</summary>
    public string? Before { get; init; }
    public string? After { get; init; }
    public bool Remove { get; init; }
}