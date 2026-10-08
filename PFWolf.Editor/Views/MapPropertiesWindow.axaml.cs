using Avalonia.Controls;
using Avalonia.Interactivity;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The level properties dialog; closes with true once what's entered builds</summary>
public partial class MapPropertiesWindow : Window
{
    public MapPropertiesWindow() => InitializeComponent();

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MapPropertiesViewModel properties && properties.Build() != null)
            Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
