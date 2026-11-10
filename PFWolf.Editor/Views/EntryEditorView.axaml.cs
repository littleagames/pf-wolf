using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>A definitions tab: the definitions of one kind, and the one picked as fields</summary>
public partial class EntryEditorView : UserControl
{
    public EntryEditorView() => InitializeComponent();

    /// <summary>Ctrl+F: the keyboard to the list's "Find by name"</summary>
    public void FocusFilter()
    {
        FilterBox.Focus();
        FilterBox.SelectAll();
    }

    private EntryEditorViewModel? _watched;

    // A field picked from elsewhere (a script's caller gone to) is scrolled to, once its opened
    // parents have their rows
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_watched != null)
            _watched.PropertyChanged -= OnViewModelChanged;
        _watched = DataContext as EntryEditorViewModel;
        if (_watched != null)
            _watched.PropertyChanged += OnViewModelChanged;
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EntryEditorViewModel.SelectedRow) || _watched?.SelectedRow is not { } row)
            return;
        Fields.BringRowIntoView(row);
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
}
