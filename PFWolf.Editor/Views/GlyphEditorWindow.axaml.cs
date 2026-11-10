using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The glyph editor, beside the font window it was opened from</summary>
public partial class GlyphEditorWindow : Window
{
    private bool _closeConfirmed;

    public GlyphEditorWindow()
    {
        InitializeComponent();

        Canvas.Pressed += (_, pointer) => ViewModel.Press(pointer.X, pointer.Y, erase: pointer.Secondary);
        Canvas.Dragged += (_, pointer) => ViewModel.Drag(pointer.X, pointer.Y);
        Canvas.Released += (_, _) => ViewModel.Release();
        Canvas.Hovered += (_, pixel) => HoverText.Text = pixel is { } at ? $"Pixel {at.X}, {at.Y}" : "";

        Grid.PointerPressed += (_, e) =>
        {
            var at = e.GetPosition(Grid);
            ViewModel.PickAt(at.X / 2, at.Y / 2);
        };
        Grid.PointerMoved += (_, e) =>
        {
            var at = e.GetPosition(Grid);
            HoverText.Text = ViewModel.CharacterAt(at.X / 2, at.Y / 2);
        };
        Grid.PointerExited += (_, _) => HoverText.Text = "";

        Closing += OnClosing;
    }

    private GlyphEditorViewModel ViewModel => (GlyphEditorViewModel)DataContext!;

    /// <summary>Asks whether to close with changes not saved: true to close</summary>
    public Func<string, Task<bool>>? ConfirmDiscard { get; set; }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || DataContext is not GlyphEditorViewModel { Document.IsDirty: true } editor || ConfirmDiscard == null)
            return;
        e.Cancel = true;
        if (await ConfirmDiscard($"{editor.Name} has changes that aren't saved in a mod. Close it and lose them?"))
        {
            _closeConfirmed = true;
            Close();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;
        var ctrl = e.KeyModifiers == KeyModifiers.Control;
        var typing = FocusManager?.GetFocusedElement() is TextBox;
        if (ctrl && e.Key == Key.Z && !typing)
            ViewModel.UndoCommand.Execute(null);
        else if ((ctrl && e.Key == Key.Y || e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Z) && !typing)
            ViewModel.RedoCommand.Execute(null);
        else if (ctrl && e.Key == Key.S)
            ViewModel.SaveCommand.Execute(null);
        else if (ctrl && e.Key == Key.C && !typing)
            ViewModel.CopyGlyphCommand.Execute(null);
        else if (ctrl && e.Key == Key.V && !typing)
            ViewModel.PasteGlyphCommand.Execute(null);
        else
            return;
        e.Handled = true;
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        byte[] data;
        try
        {
            data = ViewModel.ToFile();
        }
        catch (InvalidDataException problem)
        {
            ViewModel.Status = $"Not exported: {problem.Message}";
            return;
        }
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {ViewModel.Name} as a Wolf3D font file",
            SuggestedFileName = ViewModel.Name + ".fnt",
            DefaultExtension = "fnt",
            FileTypeChoices = [new FilePickerFileType("Wolf3D font") { Patterns = ["*.fnt"] }],
        });
        if (file == null)
            return;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(data);
        ViewModel.Status = $"Exported {file.Name}";
    }
}
