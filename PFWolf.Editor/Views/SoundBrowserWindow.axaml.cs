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
}
