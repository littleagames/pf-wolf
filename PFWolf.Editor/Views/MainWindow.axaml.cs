using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PFWolf.Editor.Rendering;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private async void BrowseGameFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "The folder PFWolf is in",
            SuggestedStartLocation = await FolderOrNull(ViewModel.GameFolder),
        });
        if (folders is [var folder, ..] && folder.TryGetLocalPath() is { } path)
        {
            ViewModel.GameFolder = path;
            if (ViewModel.CanLoad)
                await ViewModel.LoadAsync();
        }
    }

    private async void AddModFile(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Mods to load over pfwolf.pk3",
            AllowMultiple = true,
            SuggestedStartLocation = await FolderOrNull(Path.Combine(ViewModel.GameFolder, "mods")),
            FileTypeFilter = [new FilePickerFileType("Mods (pk3, zip)") { Patterns = ["*.pk3", "*.zip"] }],
        });
        foreach (var file in files)
        {
            if (file.TryGetLocalPath() is { } path)
                ViewModel.AddMod(path);
        }
    }

    private async void AddModFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "A mod folder to load over pfwolf.pk3",
            SuggestedStartLocation = await FolderOrNull(Path.Combine(ViewModel.GameFolder, "mods")),
        });
        if (folders is [var folder, ..] && folder.TryGetLocalPath() is { } path)
            ViewModel.AddMod(path);
    }

    private void OnTileHovered(object? sender, TileEventArgs e) => ViewModel.Hover(e.X, e.Y);

    private async Task<IStorageFolder?> FolderOrNull(string path)
        => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) ? await StorageProvider.TryGetFolderFromPathAsync(path) : null;
}
