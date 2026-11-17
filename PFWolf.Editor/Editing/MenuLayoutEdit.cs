using System.Globalization;
using PFWolf.Definitions;
using PFWolf.Editor.Definitions;
using PFWolf.Editor.Rendering;
using PFWolf.Menus;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.Editing;

/// <summary>What dragging a part of a menu's picture does</summary>
public enum MenuDragMode
{
    /// <summary>Moves it</summary>
    Move,
    /// <summary>Its right edge: its width</summary>
    ResizeWidth,
    /// <summary>Its bottom edge: its height</summary>
    ResizeHeight,
    /// <summary>Its bottom right corner: both</summary>
    Resize,
    /// <summary>A row with Shift: across, the text's indent; down, the rows' height</summary>
    Spacing,
}

/// <summary>
/// A part of a menu's picture moved or resized, as changes to its fields (the menu as the fields
/// edit it: MenuEditing.ToEditable's form): a component's params, the rows' position (or indent and
/// row-height), the highlight bar's, the highlighted row's picture's (selection-pic) and the stats
/// panel's. An axis an orientation sets (a centered picture or label) stays put unless
/// <c>unlock</c>, which makes it an x or y of its own where it is now.
/// </summary>
public static class MenuLayoutEdit
{
    /// <summary>Whether a part can be moved: anything but the background</summary>
    public static bool CanMove(MenuPart part, MenuMetadata menu)
        => part.Kind switch
        {
            MenuPartKind.Component => part.Index >= 0 && part.Index < menu.Components.Count && menu.Components[part.Index] is not Background,
            MenuPartKind.Row => menu.ItemsSource != null || menu.MenuItems.Count > 0,
            _ => true,
        };

    /// <summary>Whether a part has a size to drag: windows and fills, a label with a width, the bar, the picture's box, the stats panel</summary>
    public static bool CanResize(MenuPart part, MenuMetadata menu)
        => part.Kind switch
        {
            MenuPartKind.Component => part.Index >= 0 && part.Index < menu.Components.Count
                                      && menu.Components[part.Index] is Window or Fill or Label { Width: > 0 },
            MenuPartKind.Cursor => menu.Cursor != null,
            MenuPartKind.Picture or MenuPartKind.StatsPanel => true,
            _ => false,
        };

    /// <summary>
    /// The menu with the part dragged by (dx, dy) layout pixels; null when that changes nothing
    /// </summary>
    /// <param name="editable">The menu as the fields edit it (left as it is: a changed copy is returned)</param>
    /// <param name="menu">What it reads as (the part's index is its components' or rows')</param>
    /// <param name="unlock">An axis an orientation sets gets an x or y of its own (Alt held)</param>
    /// <param name="snap">Values rounded to this (8 on the grid), or 1</param>
    public static YamlMappingNode? Apply(YamlMappingNode editable, MenuMetadata menu, MenuPart part, MenuDragMode mode, int dx, int dy,
        bool unlock = false, int snap = 1)
    {
        if (dx == 0 && dy == 0)
            return null;
        var copy = (YamlMappingNode)YamlNodes.Clone(editable);
        bool changed = part.Kind switch
        {
            MenuPartKind.Component => Component(copy, menu, part, mode, dx, dy, unlock, snap),
            MenuPartKind.Row => Rows(copy, menu, part, mode, dx, dy, snap),
            MenuPartKind.Cursor => Box(Child(copy, "cursor"), "x", "y-offset", "width", "height", mode, dx, dy, snap, 0, 0),
            MenuPartKind.Picture => Picture(copy, part, mode, dx, dy, snap),
            MenuPartKind.StatsPanel => Box(Child(copy, "stats-panel"), "x", "y", "width", "height", mode, dx, dy, snap, 104, 64),
            _ => false,
        };
        return changed && !YamlNodes.Equal(copy, editable) ? copy : null;
    }

    // A component's entry: its params, as the fields edit them (one mapping)
    private static bool Component(YamlMappingNode menu, MenuMetadata read, MenuPart part, MenuDragMode mode, int dx, int dy, bool unlock, int snap)
    {
        if (part.Index < 0 || part.Index >= read.Components.Count || Entry(menu, part.Index) is not { } entry)
            return false;
        var parameters = Child(entry, "params");
        switch (read.Components[part.Index])
        {
            case Window or Fill:
                return Box(parameters, "x", "y", "width", "height", mode, dx, dy, snap, 0, 0);

            case Stripe:
                return mode == MenuDragMode.Move && Add(parameters, "y", dy, 0, snap);

            case Graphic graphic:
                if (mode != MenuDragMode.Move)
                    return false;
                bool moved = Axis(parameters, "x", "horizontal-orientation", graphic.HorizontalOrientation != HorizontalOrientation.Left, part.X, dx, unlock, snap);
                return Axis(parameters, "y", "vertical-orientation", graphic.VerticalOrientation != VerticalOrientation.Top, part.Y, dy, unlock, snap) | moved;

            case Label label:
                if (mode is MenuDragMode.ResizeWidth or MenuDragMode.Resize)
                    return label.Width > 0 && Add(parameters, "width", dx, label.Width, snap, minimum: 1);
                if (mode != MenuDragMode.Move)
                    return false;
                // Centered across the screen (no width): x means nothing until it's unlocked
                bool across = label.HorizontalOrientation == HorizontalOrientation.Center && label.Width <= 0;
                bool x = Axis(parameters, "x", "horizontal-orientation", across, part.X, dx, unlock, snap);
                return Add(parameters, "y", dy, label.Y, snap) | x;

            default:
                return false;
        }
    }

    // The rows: moved, their position; with Shift, the text's indent across and the rows' height down
    private static bool Rows(YamlMappingNode menu, MenuMetadata read, MenuPart part, MenuDragMode mode, int dx, int dy, int snap)
    {
        if (mode == MenuDragMode.Spacing)
        {
            bool indent = Add(menu, "indent", dx, read.Indent, 1);
            // The row dragged follows the mouse: the rows above it each get a share
            int rowHeight = read.RowHeight is > 0 and int set ? set : 13;
            int share = (int)Math.Round(dy / (double)Math.Max(part.Index, 1));
            bool height = share != 0 && Set(menu, "row-height", Math.Max(rowHeight + share, 1), 1);
            return indent | height;
        }
        if (mode != MenuDragMode.Move)
            return false;
        var position = Child(menu, "position");
        bool across = Add(position, "x", dx, (int)read.Position.X, snap);
        return Add(position, "y", dy, (int)read.Position.Y, snap) | across;
    }

    // The highlighted row's picture: its selection-pic, made where the picture is when the menu has none
    private static bool Picture(YamlMappingNode menu, MenuPart part, MenuDragMode mode, int dx, int dy, int snap)
    {
        bool had = YamlNodes.FindKey(menu, "selection-pic") != null;
        var at = Child(menu, "selection-pic");
        if (!had)
        {
            Set(at, "x", part.X, 1);
            Set(at, "y", part.Y, 1);
        }
        return Box(at, "x", "y", "width", "height", mode, dx, dy, snap, part.Width, part.Height) || !had;
    }

    // A box's place (x, y) or size (width, height), whichever the mode drags
    private static bool Box(YamlMappingNode box, string x, string y, string width, string height, MenuDragMode mode,
        int dx, int dy, int snap, int defaultWidth, int defaultHeight)
    {
        switch (mode)
        {
            case MenuDragMode.Move:
                bool across = Add(box, x, dx, 0, snap);
                return Add(box, y, dy, 0, snap) | across;
            case MenuDragMode.ResizeWidth:
                return Add(box, width, dx, defaultWidth, snap, minimum: 1);
            case MenuDragMode.ResizeHeight:
                return Add(box, height, dy, defaultHeight, snap, minimum: 1);
            case MenuDragMode.Resize:
                bool wide = Add(box, width, dx, defaultWidth, snap, minimum: 1);
                return Add(box, height, dy, defaultHeight, snap, minimum: 1) | wide;
            default:
                return false;
        }
    }

    // An axis of a picture or label: where an orientation sets it, it stays (or, unlocked, gets a value of its own where it is)
    private static bool Axis(YamlMappingNode parameters, string key, string orientationKey, bool oriented, int shownAt, int delta, bool unlock, int snap)
    {
        if (delta == 0)
            return false;
        if (!oriented)
            return Add(parameters, key, delta, 0, snap);
        if (!unlock)
            return false;
        if (YamlNodes.FindKey(parameters, orientationKey) is { } orientation)
            parameters.Children.Remove(orientation);
        return Set(parameters, key, shownAt + delta, snap);
    }

    // The n-th entry of the components the menu reads (an unknown type's left out, as the game leaves it out)
    private static YamlMappingNode? Entry(YamlMappingNode menu, int index)
    {
        if (YamlNodes.Get(menu, ["components"]) is not YamlSequenceNode list)
            return null;
        var read = list.Children.OfType<YamlMappingNode>()
            .Where(entry => YamlNodes.Get(entry, ["type"]) is YamlScalarNode { Value: { } type } && MenuSchema.ComponentTypes.ContainsKey(type))
            .ToList();
        return index < read.Count ? read[index] : null;
    }

    // A mapping under a key, made when there's none
    private static YamlMappingNode Child(YamlMappingNode parent, string key)
    {
        if (YamlNodes.FindKey(parent, key) is { } found && parent.Children[found] is YamlMappingNode mapping)
            return mapping;
        var made = new YamlMappingNode();
        if (YamlNodes.FindKey(parent, key) is { } other)
            parent.Children[other] = made;
        else
            parent.Add(key, made);
        return made;
    }

    private static int Value(YamlMappingNode mapping, string key, int fallback)
        => YamlNodes.FindKey(mapping, key) is { } found && mapping.Children[found] is YamlScalarNode { Value: { } text }
           && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? (int)Math.Round(value)
            : fallback;

    // A value moved by delta from what it is (or fallback, when it isn't set)
    private static bool Add(YamlMappingNode mapping, string key, int delta, int fallback, int snap, int minimum = int.MinValue)
        => delta != 0 && Set(mapping, key, Math.Max(Value(mapping, key, fallback) + delta, minimum), snap);

    private static bool Set(YamlMappingNode mapping, string key, int value, int snap)
    {
        if (snap > 1)
            value = (int)Math.Round(value / (double)snap) * snap;
        var text = value.ToString(CultureInfo.InvariantCulture);
        if (YamlNodes.FindKey(mapping, key) is { } found)
            mapping.Children[found] = new YamlScalarNode(text);
        else
            mapping.Add(key, text);
        return true;
    }
}
