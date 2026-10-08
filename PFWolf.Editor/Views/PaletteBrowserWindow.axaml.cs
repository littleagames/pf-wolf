using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PFWolf.Editor.Data;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The palette browser and editor, beside the main window (F9)</summary>
public partial class PaletteBrowserWindow : Window
{
    public PaletteBrowserWindow()
    {
        InitializeComponent();

        Grid.Picked += (_, pick) => ViewModel.Pick(pick.Index, pick.Extend);
        Grid.Hovered += (_, index) => ViewModel.Hover(index);
        Preview.PointerPressed += OnPreviewPressed;

        // The hex box sets the color on Enter, and when it's left
        Hex.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ViewModel.ApplyHex();
                Hex.SelectAll();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        Hex.LostFocus += (_, _) => ViewModel.ApplyHex();

        FindBox.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ViewModel.FindCommand.Execute(null);
                Grid.Focus();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
    }

    private PaletteBrowserViewModel ViewModel => (PaletteBrowserViewModel)DataContext!;

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;

        // Copy and paste are the text boxes' own while typing in one
        var typing = FocusManager?.GetFocusedElement() is TextBox;
        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            FindBox.Focus();
            FindBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Control && !typing)
        {
            e.Handled = true;
            if (ViewModel.CopyCommand.CanExecute(null))
            {
                ViewModel.CopyCommand.Execute(null);
                // As #RRGGBB lines, for pasting elsewhere too
                if (Clipboard is { } clipboard)
                    await clipboard.SetValueAsync(DataFormat.Text, ViewModel.CopiedText);
            }
        }
        else if (e.Key == Key.V && e.KeyModifiers == KeyModifiers.Control && !typing)
        {
            e.Handled = true;
            if (ViewModel.PasteCommand.CanExecute(null))
                ViewModel.PasteCommand.Execute(null);
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    // A click on the preview picks the color of the pixel under it
    private void OnPreviewPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel.PreviewSize is not { } size || Preview.Bounds.Width <= 0 || Preview.Bounds.Height <= 0)
            return;

        // Stretch="Uniform": the picture is scaled to fit and centered
        var (width, height) = size;
        var bounds = Preview.Bounds;
        var scale = Math.Min(bounds.Width / width, bounds.Height / height);
        var left = (bounds.Width - width * scale) / 2;
        var top = (bounds.Height - height * scale) / 2;
        var at = e.GetPosition(Preview);
        ViewModel.PickPixel((int)Math.Floor((at.X - left) / scale), (int)Math.Floor((at.Y - top) / scale),
            e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        Grid.Focus();
        e.Handled = true;
    }

    private void OnUseDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.UsePictureCommand.CanExecute(null))
            ViewModel.UsePictureCommand.Execute(null);
    }

    private async void CopyName(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem?.Name is { Length: > 0 } name && Clipboard is { } clipboard)
            await clipboard.SetValueAsync(DataFormat.Text, name);
    }

    private async void Import(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem is not { } item)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"A palette file to replace {item.Name}'s colors with",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Palettes") { Patterns = ["*.pal", "*.gpl", "*.act"] },
                FilePickerFileTypes.All,
            ],
        });
        if (files.Count == 0)
            return;

        await using var stream = await files[0].OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        ViewModel.Import(files[0].Name, memory.ToArray());
    }

    private void ExportRaw(object? sender, RoutedEventArgs e) => Export(PaletteCatalog.FileFormat.Raw, "pal", "Raw palette");
    private void ExportJasc(object? sender, RoutedEventArgs e) => Export(PaletteCatalog.FileFormat.Jasc, "pal", "JASC-PAL palette");
    private void ExportGimp(object? sender, RoutedEventArgs e) => Export(PaletteCatalog.FileFormat.Gimp, "gpl", "GIMP palette");

    private async void Export(PaletteCatalog.FileFormat format, string extension, string description)
    {
        if (ViewModel.SelectedItem is not { } item || ViewModel.Export(format) is not { } data)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {item.Name} as a {description}",
            SuggestedFileName = $"{item.Name.ToLowerInvariant()}.{extension}",
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(description) { Patterns = [$"*.{extension}"] }],
        });
        if (file == null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(data);
        ViewModel.Status = $"Exported {item.Name} to {file.Name}";
    }
}
