using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PFWolf.Editor.Rendering;
using PFWolf.Editor.ViewModels;

namespace PFWolf.Editor.Views;

public partial class MainWindow : Window, IEditorDialogs
{
    // Set once the unsaved changes have been dealt with, so the second Close goes through
    private bool _closeConfirmed;

    public MainWindow() => InitializeComponent();

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Dialogs = this;
            viewModel.GoToTile += (_, tile) => Canvas.CenterOn(tile.X, tile.Y);
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.Show3D))
                    Arrange3D(viewModel.Show3D);
            };
            Arrange3D(viewModel.Show3D);
        }
    }

    /// <summary>The map takes the whole width without the 3D view, and half with it</summary>
    private void Arrange3D(bool show)
    {
        Views.ColumnDefinitions[2].Width = show ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        if (show)
            View3D.Focus();
    }

    private void OnSurfaceHovered(object? sender, SurfaceHoverEventArgs e) => ViewModel.Hover3D(e.Hit, e.Target);

    private void On3DFailed(object? sender, string message)
    {
        ViewModel.Problems.Add($"3D view: {message}");
        ViewModel.Status = "The 3D view couldn't start OpenGL.";
    }

    public async Task<bool> EditProperties(MapPropertiesViewModel properties)
        => await new MapPropertiesWindow { DataContext = properties }.ShowDialog<bool>(this);

    private ArtBrowserWindow? _artBrowser;

    public void ShowArtBrowser(ArtBrowserViewModel browser)
    {
        if (_artBrowser == null)
        {
            // Owned, so it stays above the editor and closes with it; not modal, so both can be used
            _artBrowser = new ArtBrowserWindow { DataContext = browser };
            _artBrowser.Closed += (_, _) => _artBrowser = null;
            _artBrowser.Show(this);
        }
        else
            _artBrowser.Activate();
    }

    private SoundBrowserWindow? _soundBrowser;

    public void ShowSoundBrowser(SoundBrowserViewModel browser)
    {
        if (_soundBrowser == null)
        {
            _soundBrowser = new SoundBrowserWindow { DataContext = browser };
            _soundBrowser.Closed += (_, _) => _soundBrowser = null;
            _soundBrowser.Show(this);
        }
        else
            _soundBrowser.Activate();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || DataContext is not MainWindowViewModel viewModel)
            return;

        e.Cancel = true;
        if (await viewModel.ResolveUnsaved("Closing the editor drops the changes to"))
        {
            _closeConfirmed = true;
            Close();
        }
    }

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
        if (await PickModFolder("A mod folder to load over pfwolf.pk3") is { } path)
            ViewModel.AddMod(path);
    }

    private void OnTileHovered(object? sender, TileEventArgs e) => ViewModel.Hover(e.X, e.Y);

    private void OnToolKeyPressed(object? sender, string key) => ViewModel.SelectToolByKey(key);

    private async Task<IStorageFolder?> FolderOrNull(string path)
        => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) ? await StorageProvider.TryGetFolderFromPathAsync(path) : null;

    //
    // IEditorDialogs
    //

    public async Task<string?> PickModFolder(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = await FolderOrNull(Path.Combine(ViewModel.GameFolder, "mods")),
        });
        return folders is [var folder, ..] ? folder.TryGetLocalPath() : null;
    }

    public async Task<string?> AskText(string title, string prompt, string initial, Func<string, string?> check)
    {
        var input = new TextBox { Text = initial };
        var error = new TextBlock { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
        var ok = new Button { Content = "OK", IsDefault = true, Classes = { "accent" } };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var dialog = MakeDialog(title, new StackPanel
        {
            Spacing = 8,
            Children = { new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap }, input, error, Buttons(ok, cancel) },
        });

        string? answer = null;
        ok.Click += (_, _) =>
        {
            var text = input.Text?.Trim() ?? "";
            if (check(text) is { } problem)
            {
                error.Text = problem;
                return;
            }
            answer = text;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        dialog.Opened += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };

        await dialog.ShowDialog(this);
        return answer;
    }

    public async Task<UnsavedChoice> AskUnsaved(string message)
    {
        var save = new Button { Content = "Save", IsDefault = true, Classes = { "accent" } };
        var discard = new Button { Content = "Don't save" };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var dialog = MakeDialog("Unsaved changes", new StackPanel
        {
            Spacing = 12,
            Children = { new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, Buttons(save, discard, cancel) },
        });

        var choice = UnsavedChoice.Cancel;
        save.Click += (_, _) => { choice = UnsavedChoice.Save; dialog.Close(); };
        discard.Click += (_, _) => { choice = UnsavedChoice.Discard; dialog.Close(); };
        cancel.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
        return choice;
    }

    private static Window MakeDialog(string title, Control content) => new()
    {
        Title = title,
        Width = 420,
        SizeToContent = SizeToContent.Height,
        CanResize = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        ShowInTaskbar = false,
        Content = new Border { Padding = new Thickness(16), Child = content },
    };

    private static StackPanel Buttons(params Button[] buttons)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        panel.Children.AddRange(buttons);
        return panel;
    }
}
