using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

/// <summary>The Import MIDI dialog, opened from the sound browser</summary>
public partial class MidiImportWindow : Window
{
    public MidiImportWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MidiImportViewModel import)
                import.Saved += (_, _) => Close();
        };
        // Nothing plays on once it's closed
        Closed += (_, _) => (DataContext as MidiImportViewModel)?.Dispose();
    }

    private MidiImportViewModel ViewModel => (MidiImportViewModel)DataContext!;

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private async void OnLoadBank(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "A GENMIDI instrument bank",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("DMX GENMIDI bank (.op2)") { Patterns = ["*.op2", "*.lmp"] }, FilePickerFileTypes.All],
        });
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
            return;
        try
        {
            ViewModel.LoadBank(path, await File.ReadAllBytesAsync(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ViewModel.Status = $"Couldn't read {Path.GetFileName(path)}: {ex.Message}";
        }
    }
}
