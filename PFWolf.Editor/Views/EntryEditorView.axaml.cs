using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
}
