using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PFWolf.Editor.Audio.Opl;
using PFWolf.Editor.Data;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The song editor, beside the sound browser it was opened from</summary>
public partial class SongEditorWindow : Window
{
    private bool _closeConfirmed;

    public SongEditorWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not SongEditorViewModel editor)
                return;
            Roll.Editor = editor;
            editor.PropertyChanged += OnEditorChanged;
        };
        Opened += (_, _) =>
        {
            // In view: the middle of the song's notes
            if (DataContext is SongEditorViewModel { Song.Notes.Count: > 0 } editor)
            {
                var notes = editor.Song.Notes.Select(note => note.NearestNote.Note).Order().ToList();
                Roll.ScrollToNote(notes[notes.Count / 2]);
            }
            else
                Roll.ScrollToNote(60);
            Roll.Focus();
        };
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            if (DataContext is SongEditorViewModel editor)
            {
                editor.PropertyChanged -= OnEditorChanged;
                editor.Dispose();
            }
        };
    }

    private SongEditorViewModel ViewModel => (SongEditorViewModel)DataContext!;

    /// <summary>Asks whether to close with changes not saved: true to close</summary>
    public Func<string, Task<bool>>? ConfirmDiscard { get; set; }

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SongEditorViewModel.PlayheadTick))
            Roll.FollowPlayhead();
        else if (e.PropertyName == nameof(SongEditorViewModel.KeptWrites))
            RawList.ItemsSource = ViewModel.KeptWrites.ToList();
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || DataContext is not SongEditorViewModel { IsDirty: true } editor || ConfirmDiscard == null)
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
        else
            return;
        e.Handled = true;
    }

    private void OnCopyInstrument(object? sender, RoutedEventArgs e)
    {
        if (Library.SelectedItem is InstrumentSource source)
            ViewModel.CopyInstrument(source);
    }

    private void OnMoveToChannel(object? sender, RoutedEventArgs e) => ViewModel.MoveSelectionToChannel(ViewModel.CurrentChannel.Channel);

    private void OnUseInstrument(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.CurrentInstrument is { } instrument)
            ViewModel.SelectionInstrument = instrument;
    }

    private void OnShowCommands(object? sender, RoutedEventArgs e) => RawList.ItemsSource = ViewModel.CommandLines();

    private async void OnExportImf(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {ViewModel.Name} as an IMF file",
            SuggestedFileName = ViewModel.Name + ".imf",
            DefaultExtension = "imf",
            FileTypeChoices = [new FilePickerFileType("IMF song") { Patterns = ["*.imf"] }],
        });
        if (file == null)
            return;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(ViewModel.ToImf());
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
        var samples = await Task.Run(ViewModel.ToSamples);
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(SoundCatalog.ToWav(samples, PFWolf.Assets.Sounds.ImfMusicSource.MusicSampleRate));
    }
}
