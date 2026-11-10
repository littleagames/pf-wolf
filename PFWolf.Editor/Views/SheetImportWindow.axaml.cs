using Avalonia.Controls;
using Avalonia.Interactivity;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>Bringing in a sheet of characters as a font (from the font window)</summary>
public partial class SheetImportWindow : Window
{
    public SheetImportWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SheetImportViewModel sheet)
                sheet.Done += (_, _) => Close();
        };
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    // AsSheet is AsVga turned round
    private void OnSheet(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SheetImportViewModel sheet)
            sheet.AsVga = false;
    }
}
