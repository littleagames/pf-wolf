using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>A definitions tab: the definitions of one kind, and the one picked as fields</summary>
public partial class EntryEditorView : UserControl
{
    public EntryEditorView() => InitializeComponent();

    // A name picked from a field's list goes in the field, and the list closes
    private void OnNamePicked(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: string name, DataContext: FieldNode field } list)
            return;
        field.Text = name;
        list.SelectedItem = null;
        list.FindLogicalAncestorOfType<Popup>()?.Close();
    }

    // One menu item for each kind New… can make (mapdefs: walls, doors, things...), the table picked first
    private static List<MenuItem> NewChoiceItems(EntryEditorViewModel editor)
    {
        var picked = editor.SelectedItem?.Entry.Path[0];
        return editor.NewChoices
            .OrderBy(choice => choice.Key.Equals(picked, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Select(choice =>
            {
                var item = new MenuItem { Header = choice.Key, Command = editor.NewEntryCommand, CommandParameter = choice.Key };
                if (!string.IsNullOrEmpty(choice.Description))
                    ToolTip.SetTip(item, choice.Description);
                return item;
            })
            .ToList();
    }

    // New… (mapdefs) asks which table the new entry goes in
    private void ShowNewChoices(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control button || DataContext is not EntryEditorViewModel editor)
            return;
        var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        foreach (var item in NewChoiceItems(editor))
            flyout.Items.Add(item);
        flyout.ShowAt(button);
    }

    // The list's right-click New has a submenu of the tables, made as it opens
    private void OnListMenuOpening(object? sender, CancelEventArgs e)
    {
        if (sender is not ContextMenu menu || DataContext is not EntryEditorViewModel { HasNewChoices: true } editor
            || menu.Items.OfType<MenuItem>().FirstOrDefault(item => item.Tag as string == "new-choices") is not { } newItem)
            return;
        newItem.Items.Clear();
        foreach (var item in NewChoiceItems(editor))
            newItem.Items.Add(item);
    }

    // A field's right-click menu has its row buttons; one with none doesn't open
    private void OnFieldMenuOpening(object? sender, CancelEventArgs e)
    {
        if (sender is ContextMenu { DataContext: FieldNode field })
            e.Cancel = !(field.CanAdd || field.CanMove || field.CanRemove || field.CanReset);
    }
}
