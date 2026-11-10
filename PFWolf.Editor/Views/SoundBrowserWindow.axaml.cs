using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PFWolf.Editor.Data;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The sound browser, beside the main window (F8)</summary>
public partial class SoundBrowserWindow : Window
{
    public SoundBrowserWindow()
    {
        InitializeComponent();

        // Enter in the search goes to the sounds found (the text box would take it on the way up)
        Search.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                (List.ContainerFromItem(List.SelectedItem ?? new object()) ?? (Control)List).Focus();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        // Enter and Space in the list play (Space would otherwise just select)
        List.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space && e.KeyModifiers == KeyModifiers.None)
            {
                Play();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        Waveform.Seek += (_, fraction) => ViewModel.Seek(fraction);
        // Nothing plays on once the window's gone
        Closed += (_, _) => ViewModel.Stop();
    }

    private SoundBrowserViewModel ViewModel => (SoundBrowserViewModel)DataContext!;

    private void Play()
    {
        if (ViewModel.PlayCommand.CanExecute(null))
            ViewModel.PlayCommand.Execute(null);
    }

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
        else if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None && !Search.IsFocused)
        {
            Play();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Starts it again rather than stopping it
        ViewModel.Stop();
        Play();
    }

    private void OnUseDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.GoToUseCommand.CanExecute(null))
            ViewModel.GoToUseCommand.Execute(null);
    }

    private void GoToVariant(object? sender, RoutedEventArgs e) => ViewModel.GoToVariant();

    private async void CopyName(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem?.Name is { Length: > 0 } name && Clipboard is { } clipboard)
            await clipboard.SetValueAsync(DataFormat.Text, name);
    }

    private async void ExportWav(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.ExportableSound is not { } sound || ViewModel.SelectedItem is not { } item)
            return;

        // A sound-seq name ("doors/open") isn't a file name
        var name = string.Join("_", item.Name.Split(Path.GetInvalidFileNameChars()));
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {item.Name} as a WAV",
            SuggestedFileName = $"{name}.wav",
            DefaultExtension = "wav",
            FileTypeChoices = [new FilePickerFileType("WAV sound") { Patterns = ["*.wav"] }],
        });
        if (file == null)
            return;

        await using var stream = await file.OpenWriteAsync();
        var wav = SoundCatalog.ToWav(sound.Samples, sound.SampleRate);
        await stream.WriteAsync(wav);
    }

    private async void ExportFile(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.ExportableFile is not { } export || ViewModel.SelectedItem is not { } item)
            return;

        var name = ViewModel.SelectedVariant?.Variant?.AssetName ?? item.Name;
        name = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
        var (label, pattern) = export.Extension switch
        {
            ".imf" => ("IMF song", "*.imf"),
            ".adl" => ("AdLib sound", "*.adl"),
            _ => ("PC speaker sound", "*.pcs"),
        };
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {name} as a file",
            SuggestedFileName = name + export.Extension,
            DefaultExtension = export.Extension.TrimStart('.'),
            FileTypeChoices = [new FilePickerFileType(label) { Patterns = [pattern] }],
        });
        if (file == null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(export.Data);
    }

    private async void ImportMidi(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "A MIDI file to make into a song",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("MIDI files") { Patterns = ["*.mid", "*.midi", "*.smf"] }, FilePickerFileTypes.All],
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
            return;

        MidiImportViewModel import;
        try
        {
            import = new MidiImportViewModel(path, await File.ReadAllBytesAsync(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ViewModel.Status = $"Couldn't import {Path.GetFileName(path)}: {ex.Message}";
            return;
        }
        ViewModel.Stop();
        import.Save = async (name, imf) => await ViewModel.SaveSongToModAsync(name, imf, namedAfterPicked: true) != null;
        import.Edit = (name, song) => ShowSongEditor(ViewModel.SongEditor(name, song, isNew: true));
        await new MidiImportWindow { DataContext = import }.ShowDialog(this);
    }

    private void EditSong(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.EditSong() is { } editor)
            ShowSongEditor(editor);
    }

    private void NewSong(object? sender, RoutedEventArgs e) => ShowSongEditor(ViewModel.NewSong());

    private void EditAdLib(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.EditAdLib() is { } editor)
            ShowAdLibEditor(editor);
    }

    private void NewAdLib(object? sender, RoutedEventArgs e) => ShowAdLibEditor(ViewModel.NewAdLib());

    private void ShowAdLibEditor(AdLibEditorViewModel editor)
    {
        var window = new AdLibEditorWindow
        {
            DataContext = editor,
            ConfirmDiscard = async message => ViewModel.Confirm == null || await ViewModel.Confirm("Close the AdLib sound editor", message, "Close"),
        };
        window.Show(this);
    }

    private void EditPc(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.EditPc() is { } editor)
            ShowPcEditor(editor);
    }

    private void NewPc(object? sender, RoutedEventArgs e) => ShowPcEditor(ViewModel.NewPc());

    private void ShowPcEditor(PcSpeakerEditorViewModel editor)
    {
        var window = new PcSpeakerEditorWindow
        {
            DataContext = editor,
            ConfirmDiscard = async message => ViewModel.Confirm == null || await ViewModel.Confirm("Close the PC speaker sound editor", message, "Close"),
        };
        window.Show(this);
    }

    private void ShowSongEditor(SongEditorViewModel editor)
    {
        var window = new SongEditorWindow
        {
            DataContext = editor,
            ConfirmDiscard = async message => ViewModel.Confirm == null || await ViewModel.Confirm("Close the song editor", message, "Close"),
        };
        window.Show(this);
    }

    private void AddSong(object? sender, RoutedEventArgs e) => AddFile(asSong: true);
    private void AddSound(object? sender, RoutedEventArgs e) => AddFile(asSong: false);

    private async void AddFile(bool asSong)
    {
        var extensions = asSong ? SoundFiles.SongExtensions : SoundFiles.SoundExtensions;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = asSong ? "A song to put in a mod" : "A sound to put in a mod",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(asSong ? "Songs (IMF, OGG, MP3, WAV)" : "Sounds (WAV, OGG, MP3, AdLib, PC speaker)")
                {
                    Patterns = extensions.Select(extension => "*" + extension).ToArray(),
                },
            ],
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
            return;

        byte[] data;
        try
        {
            data = await File.ReadAllBytesAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ViewModel.Status = $"Couldn't read {Path.GetFileName(path)}: {ex.Message}";
            return;
        }
        await ViewModel.AddToModAsync(asSong, path, data);
    }
}
