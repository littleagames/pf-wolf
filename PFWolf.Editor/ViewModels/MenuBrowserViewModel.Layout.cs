using PFWolf.Assets;
using PFWolf.Editor.Definitions;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;
using PFWolf.Loaders;
using PFWolf.Menus;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// The menu window's laying out on the picture: a part dragged, resized or nudged
/// (Editing/MenuLayoutEdit) is drawn as it's dragged, then made one change to the fields, which
/// saves and undoes as any other
/// </summary>
public sealed partial class MenuBrowserViewModel : IMenuLayoutTarget
{
    // The menu as the fields had it when a drag started, and what it read as: each move works from it
    private (YamlMappingNode Editable, MenuMetadata Menu)? _dragFrom;

    // The fields of the menu picked and what they read as; null when there's none (or it doesn't read)
    private (MenuEdit Edit, YamlMappingNode Editable, MenuMetadata Menu)? Current()
    {
        if (SelectedItem is not { } item || !_edits.TryGetValue(item.Name, out var edit) || edit.Root.Build([]) is not YamlMappingNode editable
            || MenuOf(editable, edit) is not { } menu)
            return null;
        return (edit, editable, menu);
    }

    // What the fields' menu reads as, or null when it doesn't
    private static MenuMetadata? MenuOf(YamlMappingNode editable, MenuEdit edit)
    {
        try
        {
            var tree = MenuEditing.FromEditable(editable, edit.Reference);
            return MenuMetadata.BuildFromAsset(YamlDataEntryLoader.Deserialize<MenuAsset>(YamlTree.ToText(tree)), _ => { });
        }
        catch (Exception e) when (e is YamlDotNet.Core.YamlException or InvalidOperationException or FormatException or ArgumentException)
        {
            return null;
        }
    }

    public bool CanMove(MenuPart part) => Current() is { } current && MenuLayoutEdit.CanMove(part, current.Menu);

    public bool CanResize(MenuPart part) => Current() is { } current && MenuLayoutEdit.CanResize(part, current.Menu);

    public MenuMetadata? Dragged(MenuPart part, MenuDragMode mode, int dx, int dy, bool unlock, int snap)
    {
        if (_dragFrom == null && Current() is { } current)
            _dragFrom = (current.Editable, current.Menu);
        if (_dragFrom is not { } from || SelectedItem is not { } item || !_edits.TryGetValue(item.Name, out var edit))
            return null;
        var moved = MenuLayoutEdit.Apply(from.Editable, from.Menu, part, mode, dx, dy, unlock, snap);
        Status = Describe(part, mode, dx, dy);
        return moved == null ? from.Menu : MenuOf(moved, edit);
    }

    public void Dropped(MenuPart part, MenuDragMode mode, int dx, int dy, bool unlock, int snap)
    {
        var from = _dragFrom;
        _dragFrom = null;
        if (Current() is not { } current)
            return;
        var start = from?.Editable ?? current.Editable;
        var moved = MenuLayoutEdit.Apply(start, from?.Menu ?? current.Menu, part, mode, dx, dy, unlock, snap);
        var edit = current.Edit;
        if (moved == null)
        {
            Status = part.Kind == MenuPartKind.Component && mode == MenuDragMode.Move
                ? "An orientation sets where that goes across: Alt-drag it for a place of its own"
                : "Nothing changed";
            UpdateFields(edit);
            return;
        }

        // One step to undo (a drag; nudges a second apart or less make one step, as typing does)
        bool nudge = from == null;
        if (!nudge || edit.Undo.Count == 0 || DateTime.UtcNow - edit.LastStep > TimeSpan.FromSeconds(1))
            edit.Undo.Add(edit.Last == null ? null : YamlNodes.Clone(edit.Last));
        edit.Redo.Clear();
        edit.LastStep = nudge ? DateTime.UtcNow : DateTime.MinValue;
        ShowFields(edit, moved);
        Preview.Pick(part.Kind, part.Index);
        Status = Describe(part, mode, dx, dy);
    }

    private static string Describe(MenuPart part, MenuDragMode mode, int dx, int dy)
    {
        var what = mode switch
        {
            MenuDragMode.ResizeWidth => $"{dx:+0;-0;0} wide",
            MenuDragMode.ResizeHeight => $"{dy:+0;-0;0} high",
            MenuDragMode.Resize => $"{dx:+0;-0;0} wide, {dy:+0;-0;0} high",
            MenuDragMode.Spacing => $"indent {dx:+0;-0;0}, rows {dy:+0;-0;0} lower",
            _ => $"{dx:+0;-0;0}, {dy:+0;-0;0}",
        };
        return $"{(part.Kind == MenuPartKind.Row ? "The rows" : part.Label)}: {what}";
    }
}
