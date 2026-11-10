using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The font window, beside the main window (Ctrl+Shift+F)</summary>
public partial class FontBrowserWindow : Window
{
    public FontBrowserWindow()
    {
        InitializeComponent();

        // Enter in the search goes to the fonts found
        Search.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                (List.ContainerFromItem(List.SelectedItem ?? new object()) ?? (Control)List).Focus();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        // The character under the mouse on the grid
        Characters.PointerMoved += (_, e) =>
        {
            var at = e.GetPosition(Characters);
            var zoom = Math.Max(ViewModel.Zoom, 1);
            CharacterInfo.Text = ViewModel.CharacterAt(at.X / zoom, at.Y / zoom);
        };
        Characters.PointerExited += (_, _) => CharacterInfo.Text = "";
        // A click picks the character (a graphic font's picture to edit)
        Characters.PointerPressed += (_, e) =>
        {
            var at = e.GetPosition(Characters);
            var zoom = Math.Max(ViewModel.Zoom, 1);
            ViewModel.PickCharacterAt(at.X / zoom, at.Y / zoom);
        };

        DataContextChanged += (_, _) =>
        {
            if (DataContext is not FontBrowserViewModel browser)
                return;
            browser.CopyText += async (_, text) =>
            {
                if (Clipboard is { } clipboard)
                    await clipboard.SetValueAsync(DataFormat.Text, text);
            };
            // The glyph editor beside this window
            browser.GlyphEditRequested += (_, editor) => new GlyphEditorWindow
            {
                DataContext = editor,
                ConfirmDiscard = async message => browser.Confirm == null || await browser.Confirm("Close the glyph editor", message, "Close"),
            }.Show(this);
        };
    }

    private FontBrowserViewModel ViewModel => (FontBrowserViewModel)DataContext!;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;
        var ctrl = e.KeyModifiers == KeyModifiers.Control;
        // A text field keeps its own undo
        var typing = FocusManager?.GetFocusedElement() is TextBox;
        if (ctrl && e.Key == Key.F)
        {
            Search.Focus();
            Search.SelectAll();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.S)
        {
            ViewModel.SaveStyleCommand.Execute(null);
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.Z && !typing)
        {
            ViewModel.UndoStyleCommand.Execute(null);
            e.Handled = true;
        }
        else if ((ctrl && e.Key == Key.Y || e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Z) && !typing)
        {
            ViewModel.RedoStyleCommand.Execute(null);
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
        if (ViewModel.GoToUseCommand.CanExecute(null))
            ViewModel.GoToUseCommand.Execute(null);
    }

    private async void ExportFont(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.ExportableFont is not { } font)
            return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {font.Name} as a Wolf3D font file",
            SuggestedFileName = font.Name + ".fnt",
            DefaultExtension = "fnt",
            FileTypeChoices = [new FilePickerFileType("Wolf3D font") { Patterns = ["*.fnt"] }],
        });
        if (file == null)
            return;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(font.Data);
        ViewModel.Status = $"Saved {font.Name} as {file.Name}";
    }
}
