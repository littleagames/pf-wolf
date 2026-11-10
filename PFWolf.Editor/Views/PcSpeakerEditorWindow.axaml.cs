using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PFWolf.Editor.Data;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The PC speaker sound editor, beside the sound browser it was opened from</summary>
public partial class PcSpeakerEditorWindow : Window
{
    private bool _closeConfirmed;

    public PcSpeakerEditorWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is PcSpeakerEditorViewModel editor)
                Strip.Editor = editor;
        };
        Strip.Hover += (_, text) => HoverText.Text = text;
        Opened += (_, _) => Strip.Focus();
        Closing += OnClosing;
        Closed += (_, _) => (DataContext as PcSpeakerEditorViewModel)?.Dispose();
    }

    private PcSpeakerEditorViewModel ViewModel => (PcSpeakerEditorViewModel)DataContext!;

    /// <summary>Asks whether to close with changes not saved: true to close</summary>
    public Func<string, Task<bool>>? ConfirmDiscard { get; set; }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || DataContext is not PcSpeakerEditorViewModel { IsDirty: true } editor || ConfirmDiscard == null)
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
        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None && !typing)
            ViewModel.PlayCommand.Execute(null);
        else if (ctrl && e.Key == Key.Z)
            ViewModel.UndoCommand.Execute(null);
        else if (ctrl && e.Key == Key.Y || e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Z)
            ViewModel.RedoCommand.Execute(null);
        else if (ctrl && e.Key == Key.S)
            ViewModel.SaveCommand.Execute(null);
        else if (e.Key == Key.Delete && !typing)
            ViewModel.DeleteRangeCommand.Execute(null);
        else if (e.Key == Key.Escape && !typing)
            ViewModel.Range = null;
        else
            return;
        e.Handled = true;
    }

    private async void OnExportPcs(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {ViewModel.Name} as a PC speaker sound",
            SuggestedFileName = ViewModel.Name + ".pcs",
            DefaultExtension = "pcs",
            FileTypeChoices = [new FilePickerFileType("PC speaker sound") { Patterns = ["*.pcs"] }],
        });
        if (file == null)
            return;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(ViewModel.ToPcs());
    }

    private async void OnExportWav(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {ViewModel.Name} as a WAV",
            SuggestedFileName = ViewModel.Name + ".wav",
            DefaultExtension = "wav",
            FileTypeChoices = [new FilePickerFileType("WAV sound") { Patterns = ["*.wav"] }],
        });
        if (file == null)
            return;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(SoundCatalog.ToWav(ViewModel.ToSamples(), PFWolf.Assets.Sounds.ImfMusicSource.MusicSampleRate));
    }
}
