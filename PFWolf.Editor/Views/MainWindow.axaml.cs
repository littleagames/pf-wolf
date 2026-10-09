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

    private PaletteBrowserWindow? _paletteBrowser;

    public void ShowPaletteBrowser(PaletteBrowserViewModel browser)
    {
        if (_paletteBrowser == null)
        {
            _paletteBrowser = new PaletteBrowserWindow { DataContext = browser };
            _paletteBrowser.Closed += (_, _) => _paletteBrowser = null;
            _paletteBrowser.Show(this);
        }
        else
            _paletteBrowser.Activate();
    }

    private TextBrowserWindow? _textBrowser;

    public void ShowTextBrowser(TextBrowserViewModel browser)
    {
        if (_textBrowser == null)
        {
            _textBrowser = new TextBrowserWindow { DataContext = browser };
            _textBrowser.Closed += (_, _) => _textBrowser = null;
            _textBrowser.Show(this);
        }
        else
            _textBrowser.Activate();
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

    private async Task<string?> PickModFolder(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = await FolderOrNull(Path.Combine(ViewModel.GameFolder, "mods")),
        });
        return folders is [var folder, ..] ? folder.TryGetLocalPath() : null;
    }

    private async Task<string?> PickModFile(string title)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = await FolderOrNull(Path.Combine(ViewModel.GameFolder, "mods")),
            FileTypeFilter = [new FilePickerFileType("Mods (pk3, zip)") { Patterns = ["*.pk3", "*.zip"] }],
        });
        return files is [var file, ..] ? file.TryGetLocalPath() : null;
    }

    //
    // IEditorDialogs
    //

    public async Task<string?> PickModToSaveIn(string title, IReadOnlyList<string> loadedMods)
    {
        var list = new ListBox
        {
            ItemsSource = loadedMods,
            Height = 120,
            SelectedIndex = loadedMods.Count - 1,
            // Its kind, then its path, cut short at the start (the end says which mod it is)
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((path, _) => new DockPanel
            {
                [ToolTip.TipProperty] = path,
                Children =
                {
                    DockLeft(new TextBlock { Text = Editing.ModFiles.IsArchive(path) ? "pk3" : "folder", Width = 48, Opacity = 0.7 }),
                    new TextBlock { Text = path, TextTrimming = TextTrimming.LeadingCharacterEllipsis },
                },
            }),
        };
        var here = new Button { Content = "Save here", IsDefault = true, Classes = { "accent" }, IsEnabled = loadedMods.Count > 0 };
        var folder = new Button { Content = "Other folder…" };
        var file = new Button { Content = "Other pk3…" };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var dialog = MakeDialog("Save in a mod", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap },
                new TextBlock
                {
                    Text = loadedMods.Count > 0 ? "The mods loaded:" : "No mods are loaded: pick a folder or a pk3 (or make one with New mod…).",
                    TextWrapping = TextWrapping.Wrap,
                },
                list,
                new TextBlock
                {
                    Text = "Saving into a pk3 rewrites it, keeping the last version as NAME.pk3.bak.",
                    TextWrapping = TextWrapping.Wrap, Opacity = 0.7,
                },
                Buttons(here, folder, file, cancel),
            },
        });
        list.IsVisible = loadedMods.Count > 0;
        list.SelectionChanged += (_, _) => here.IsEnabled = list.SelectedItem != null;

        string? answer = null;
        here.Click += (_, _) => { answer = list.SelectedItem as string; dialog.Close(); };
        folder.Click += async (_, _) =>
        {
            if (await PickModFolder(title) is { } path) { answer = path; dialog.Close(); }
        };
        file.Click += async (_, _) =>
        {
            if (await PickModFile(title) is { } path) { answer = path; dialog.Close(); }
        };
        cancel.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
        return answer;
    }

    public async Task<Editing.NewModRequest?> AskNewMod(IReadOnlyList<GameChoice> games, string modsFolder)
    {
        var standalone = new RadioButton { Content = "A stand-alone game: needs no other game's files", GroupName = "kind", IsChecked = true };
        var based = new RadioButton { Content = "A mod of:", GroupName = "kind" };
        var game = new ComboBox { ItemsSource = games, SelectedIndex = 0, MinWidth = 220, IsEnabled = false };
        var name = new TextBox { Text = "My Game" };
        var id = new TextBox { Text = Editing.NewMod.IdFrom("My Game") };
        var path = new TextBox();
        var browse = new Button { Content = "Browse…" };
        var error = new TextBlock { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
        var ok = new Button { Content = "Make it", IsDefault = true, Classes = { "accent" } };
        var cancel = new Button { Content = "Cancel", IsCancel = true };

        // The id and the file follow the name until they're typed over
        bool idTyped = false, pathTyped = false, settingText = false;
        string DefaultPath() => Path.Combine(modsFolder, (id.Text is { Length: > 0 } text ? text : "my-mod") + ".pk3");
        void Follow()
        {
            settingText = true;
            if (!idTyped)
                id.Text = Editing.NewMod.IdFrom(name.Text ?? "");
            if (!pathTyped)
                path.Text = DefaultPath();
            settingText = false;
        }
        Follow();
        name.TextChanged += (_, _) => Follow();
        id.TextChanged += (_, _) => { if (!settingText) { idTyped = true; Follow(); } };
        path.TextChanged += (_, _) => { if (!settingText) pathTyped = true; };
        standalone.IsCheckedChanged += (_, _) =>
        {
            id.IsEnabled = standalone.IsChecked == true;
            game.IsEnabled = standalone.IsChecked != true;
        };

        var dialog = MakeDialog("New mod", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                standalone,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { based, game } },
                new TextBlock { Text = "Name:" },
                name,
                new TextBlock { Text = "Game id (what --game names a stand-alone game by):" },
                id,
                new TextBlock { Text = "The pk3:" },
                new DockPanel { Children = { DockRight(browse), path } },
                new TextBlock
                {
                    Text = "A stand-alone game starts as a bare skeleton: gamepack-info, modinfo, game-info and an empty MAP01. "
                           + "A mod of a game starts with just its modinfo.",
                    TextWrapping = TextWrapping.Wrap, Opacity = 0.7,
                },
                error,
                Buttons(ok, cancel),
            },
        });
        dialog.Width = 480;

        browse.Click += async (_, _) =>
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "The new mod's pk3",
                SuggestedStartLocation = await FolderOrNull(modsFolder),
                SuggestedFileName = Path.GetFileName(path.Text ?? DefaultPath()),
                DefaultExtension = "pk3",
                FileTypeChoices = [new FilePickerFileType("pk3") { Patterns = ["*.pk3"] }],
            });
            if (file?.TryGetLocalPath() is { } chosen)
                path.Text = chosen;
        };

        Editing.NewModRequest? answer = null;
        ok.Click += (_, _) =>
        {
            var modName = name.Text?.Trim() ?? "";
            var gameId = id.Text?.Trim() ?? "";
            var file = path.Text?.Trim() ?? "";
            var isStandalone = standalone.IsChecked == true;
            var problem = modName.Length == 0 ? "Give the mod a name."
                : isStandalone ? Editing.NewMod.CheckGameId(gameId) : null;
            problem ??= Editing.NewMod.CheckPath(file);
            if (problem != null)
            {
                error.Text = problem;
                return;
            }

            answer = new Editing.NewModRequest(file, modName, gameId, isStandalone ? null : (game.SelectedItem as GameChoice)?.Id ?? "wolf3d");
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        dialog.Opened += (_, _) => { name.Focus(); name.SelectAll(); };

        await dialog.ShowDialog(this);
        return answer;
    }

    private static Control DockLeft(Control control)
    {
        DockPanel.SetDock(control, Dock.Left);
        return control;
    }

    private static Control DockRight(Control control)
    {
        DockPanel.SetDock(control, Dock.Right);
        control.Margin = new Thickness(6, 0, 0, 0);
        return control;
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
