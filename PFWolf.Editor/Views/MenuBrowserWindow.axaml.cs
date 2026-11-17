using Avalonia.Platform.Storage;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The menu window (Resources → Menus…, Ctrl+Shift+M), beside the main window</summary>
public partial class MenuBrowserWindow : Window
{
    public MenuBrowserWindow()
    {
        InitializeComponent();

        // Enter in the search goes to the menus found (the text box would take it on the way up)
        Search.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                (List.ContainerFromItem(List.SelectedItem ?? new object()) ?? (Control)List).Focus();
                e.Handled = true;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private MenuBrowserViewModel ViewModel => (MenuBrowserViewModel)DataContext!;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MenuBrowserViewModel viewModel)
            viewModel.SelectYamlLines += (_, lines) => SelectLines(lines.First, lines.Last);
    }

    // Selects whole lines of the merged YAML (from 1) and scrolls to them, once the box has the text
    private void SelectLines(int first, int last)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var document = YamlBox.Document;
            if (document == null || document.LineCount == 0)
                return;
            var from = document.GetLineByNumber(Math.Clamp(first, 1, document.LineCount));
            var to = document.GetLineByNumber(Math.Clamp(last, first, document.LineCount));
            YamlBox.Select(from.Offset, to.EndOffset - from.Offset);
            YamlBox.ScrollTo(from.LineNumber, 0);
        });
    }

    private async void OnSavePicture(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var png = ViewModel.PicturePng();
        if (png == null)
            return;
        var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save the menu's picture",
            SuggestedFileName = ViewModel.PictureFileName,
            DefaultExtension = "png",
            FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("PNG picture") { Patterns = ["*.png"] }],
        });
        if (file?.TryGetLocalPath() is not { } path)
            return;
        try
        {
            await File.WriteAllBytesAsync(path, png);
            ViewModel.Status = $"Saved {path}";
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
        {
            ViewModel.Status = $"Not saved: {problem.Message}";
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;

        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control && !YamlBox.IsKeyboardFocusWithin)
        {
            Search.Focus();
            Search.SelectAll();
            e.Handled = true;
        }
        else if (e.Key is Key.Z or Key.Y && e.KeyModifiers == KeyModifiers.Control && FocusManager?.GetFocusedElement() is not TextBox)
        {
            var command = e.Key == Key.Z ? ViewModel.UndoMenuCommand : ViewModel.RedoMenuCommand;
            if (command.CanExecute(null))
                command.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (YamlBox.SearchPanel is { IsClosed: false } find)
                find.Close();
            else
                Close();
            e.Handled = true;
        }
    }
}
