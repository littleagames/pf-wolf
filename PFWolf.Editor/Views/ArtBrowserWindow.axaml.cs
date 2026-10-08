using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The texture and sprite browser, beside the main window (F6)</summary>
public partial class ArtBrowserWindow : Window
{
    public ArtBrowserWindow() => InitializeComponent();

    private ArtBrowserViewModel ViewModel => (ArtBrowserViewModel)DataContext!;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;

        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            Search.Focus();
            Search.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnUseDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.UseInPaletteCommand.CanExecute(null))
            ViewModel.UseInPaletteCommand.Execute(null);
    }

    private async void CopyName(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem is { } item && Clipboard is { } clipboard)
            await clipboard.SetValueAsync(DataFormat.Text, item.Name);
    }

    private async void ExportPng(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem is not { Thumbnail: { } bitmap } item)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {item.Name} as a PNG",
            SuggestedFileName = $"{item.Name}.png",
            DefaultExtension = "png",
            FileTypeChoices = [FilePickerFileTypes.ImagePng],
        });
        if (file == null)
            return;

        await using var stream = await file.OpenWriteAsync();
        bitmap.Save(stream);
    }
}
