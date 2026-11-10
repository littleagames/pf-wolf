using System.Collections;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.LogicalTree;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>
/// A definition's fields as rows (FieldNode): typed, picked from the names the game has, groups
/// opening to their own rows. The Definitions window's tabs and the font window show them.
/// </summary>
public partial class FieldTree : UserControl
{
    public static readonly StyledProperty<IEnumerable?> RowsProperty =
        AvaloniaProperty.Register<FieldTree, IEnumerable?>(nameof(Rows));

    public static readonly StyledProperty<FieldNode?> SelectedRowProperty =
        AvaloniaProperty.Register<FieldTree, FieldNode?>(nameof(SelectedRow), defaultBindingMode: BindingMode.TwoWay);

    public FieldTree()
    {
        InitializeComponent();
        Tree.SelectionChanged += (_, _) => SelectedRow = Tree.SelectedItem as FieldNode;
    }

    public IEnumerable? Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }

    public FieldNode? SelectedRow { get => GetValue(SelectedRowProperty); set => SetValue(SelectedRowProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RowsProperty)
            Tree.ItemsSource = Rows;
        else if (change.Property == SelectedRowProperty && !ReferenceEquals(Tree.SelectedItem, SelectedRow))
            Tree.SelectedItem = SelectedRow;
    }

    /// <summary>Scrolls a row into view, once its opened parents have their rows</summary>
    public void BringRowIntoView(FieldNode row)
        => Avalonia.Threading.Dispatcher.UIThread.Post(() => Tree.TreeContainerFromItem(row)?.BringIntoView(),
            Avalonia.Threading.DispatcherPriority.Background);

    // A name picked from a field's list goes in the field, and the list closes
    private void OnNamePicked(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: string name, DataContext: FieldNode field } list)
            return;
        field.Text = name;
        list.SelectedItem = null;
        list.FindLogicalAncestorOfType<Popup>()?.Close();
    }

    // A field's right-click menu has its row buttons; one with none doesn't open
    private void OnFieldMenuOpening(object? sender, CancelEventArgs e)
    {
        if (sender is ContextMenu { DataContext: FieldNode field })
            e.Cancel = !(field.CanAdd || field.CanMove || field.CanRemove || field.CanReset);
    }
}
