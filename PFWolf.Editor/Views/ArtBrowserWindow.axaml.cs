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
    public ArtBrowserWindow()
    {
        InitializeComponent();

        // Enter in the search goes to the pictures found (the text box would take it on the way up)
        Search.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                (Thumbs.ContainerFromItem(Thumbs.SelectedItem ?? new object()) ?? (Control)Thumbs).Focus();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
    }

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
        else if (e.Key is Key.OemComma or Key.OemPeriod && e.KeyModifiers == KeyModifiers.None && !Search.IsFocused)
        {
            // Turns a rotating sprite (not while typing in the search box)
            ViewModel.TurnCommand.Execute(e.Key == Key.OemComma ? "-1" : "1");
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
        // The side shown, for a rotating sprite ("GARDA3")
        if (ViewModel.CurrentAssetName is { Length: > 0 } name && Clipboard is { } clipboard)
            await clipboard.SetValueAsync(DataFormat.Text, name);
    }

    private async void ExportPng(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.Preview is not { } bitmap)
            return;

        var name = ViewModel.CurrentAssetName;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {name} as a PNG",
            SuggestedFileName = $"{name}.png",
            DefaultExtension = "png",
            FileTypeChoices = [FilePickerFileTypes.ImagePng],
        });
        if (file == null)
            return;

        await using var stream = await file.OpenWriteAsync();
        bitmap.Save(stream);
    }
}
