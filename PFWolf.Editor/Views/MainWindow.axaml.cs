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

    /// <summary>
    /// The window a question is asked over: the browser or definitions window in use when it's
    /// one of those asking, so the question isn't hidden behind it, else the editor
    /// </summary>
    private Window DialogOwner => ActiveOwned(this) ?? this;

    // The active window among those a window owns, and theirs (a pixel editor the art browser opened)
    private static Window? ActiveOwned(Window window)
    {
        foreach (var owned in window.OwnedWindows)
        {
            if (owned.IsActive)
                return owned;
            if (ActiveOwned(owned) is { } inner)
                return inner;
        }
        return null;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Dialogs = this;
            viewModel.GoToTile += (_, tile) => Canvas.CenterOn(tile.X, tile.Y);
            viewModel.FitRequested += (_, _) => Canvas.FitToView();
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

    private DemoBrowserWindow? _demoBrowser;

    public void ShowDemoBrowser(DemoBrowserViewModel browser)
    {
        if (_demoBrowser == null)
        {
            _demoBrowser = new DemoBrowserWindow { DataContext = browser };
            _demoBrowser.Closed += (_, _) => _demoBrowser = null;
            _demoBrowser.Show(this);
        }
        else
            _demoBrowser.Activate();
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

    private ScriptBrowserWindow? _scriptBrowser;

    public void ShowScriptBrowser(ScriptBrowserViewModel browser)
    {
        if (_scriptBrowser == null)
        {
            _scriptBrowser = new ScriptBrowserWindow { DataContext = browser };
            _scriptBrowser.Closed += (_, _) => _scriptBrowser = null;
            _scriptBrowser.Show(this);
        }
        else
            _scriptBrowser.Activate();
    }

    private DefinitionsWindow? _definitions;

    public void ShowDefinitions(DefinitionsViewModel definitions)
    {
        if (_definitions == null)
        {
            _definitions = new DefinitionsWindow { DataContext = definitions };
            _definitions.Closed += (_, _) => _definitions = null;
            _definitions.Show(this);
        }
        else
        {
            if (_definitions.WindowState == WindowState.Minimized)
                _definitions.WindowState = WindowState.Normal;
            _definitions.Activate();
        }
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

    /// <summary>
    /// The map's and the 3D view's right-click menu: Create in mapdefs, for the tile's values they
    /// don't know, and Go to map definition, for the ones they do
    /// </summary>
    private void OnTileContextRequested(object? sender, TileEventArgs e)
    {
        var known = ViewModel.KnownDefinitionsAt(e.X, e.Y);
        var goTo = new MenuItem
        {
            Header = known.Count > 0 ? "Go to map definition" : "Go to map definition (the mapdefs know nothing of this tile)",
            IsEnabled = known.Count > 0,
            [ToolTip.TipProperty] = "Opens the definitions window on the mapdefs entry",
            ItemsSource = known.Select(item =>
            {
                var choice = new MenuItem { Header = item.Header };
                choice.Click += (_, _) => ViewModel.GoToMapDefinition(item.Table, item.Id);
                return choice;
            }).ToList(),
        };

        var missing = ViewModel.MissingDefinitionsAt(e.X, e.Y);
        var create = new MenuItem
        {
            Header = missing.Count > 0 ? "Create in mapdefs" : "Create in mapdefs (the mapdefs know all of this tile)",
            IsEnabled = missing.Count > 0,
            [ToolTip.TipProperty] = "Opens the definitions window on a new mapdefs entry for it, to fill in and save into a mod",
            ItemsSource = missing.Select(item =>
            {
                var choice = new MenuItem { Header = item.Header };
                choice.Click += (_, _) => ViewModel.CreateInMapDefs(item.Table, item.Id);
                return choice;
            }).ToList(),
        };
        var breakable = ViewModel.BreakableWallAt(e.X, e.Y);
        var makeBreakable = new MenuItem
        {
            Header = breakable is { Breaks: true } ? "Edit how it breaks…" : breakable != null ? "Make breakable…" : "Make breakable… (not a wall the mapdefs know)",
            IsEnabled = breakable != null,
            [ToolTip.TipProperty] = "Opens the definitions window on the wall's destructible block, started (breaking to bullets) when it has none, "
                                    + "to fill in and save into a mod. Every wall of this number breaks the same way.",
        };
        if (breakable is { } picked)
            makeBreakable.Click += (_, _) => ViewModel.MakeBreakable(picked.Wall);
        var menu = new ContextMenu { ItemsSource = new[] { create, goTo, makeBreakable } };
        menu.Open(sender as Control ?? Canvas);
    }

    private void ExitEditor(object? sender, RoutedEventArgs e) => Close();

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

        await dialog.ShowDialog(DialogOwner);
        return answer;
    }

    public async Task<Editing.NewModRequest?> AskNewMod(IReadOnlyList<GameChoice> games, string modsFolder)
    {
        var standalone = new RadioButton { Content = "A stand-alone game: needs no other game's files", GroupName = "kind", IsChecked = true };
        var based = new RadioButton { Content = "A mod of:", GroupName = "kind" };
        var game = new ComboBox { ItemsSource = games, SelectedIndex = 0, MinWidth = 220, IsEnabled = false };
        var name = new TextBox { Text = "My Game" };
        var id = new TextBox();
        var path = new TextBox();
        var browse = new Button { Content = "Browse…" };
        var error = new TextBlock { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
        var ok = new Button { Content = "Make it", IsDefault = true, Classes = { "accent" } };
        var cancel = new Button { Content = "Cancel", IsCancel = true };

        // The id follows the name ("Deep  Blue Sea" -> deep-blue-sea), and the pk3's file name the
        // id, each until it's typed over: one that still reads as it was last filled in follows.
        // (Avalonia raises TextChanged after the change, so it's the text that says, not a flag.)
        string followedId = "", followedPath = "";
        string DefaultPath() => Path.Combine(modsFolder, (id.Text is { Length: > 0 } text ? text : "my-mod") + ".pk3");
        void Follow()
        {
            if ((id.Text ?? "") == followedId)
                id.Text = followedId = Editing.NewMod.IdFrom(name.Text ?? "");
            if ((path.Text ?? "") == followedPath)
                path.Text = followedPath = DefaultPath();
        }
        Follow();
        name.TextChanged += (_, _) => Follow();
        id.TextChanged += (_, _) => Follow();
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
                    Text = "A stand-alone game starts ready to play (F5): a copy of the example stand-alone game's "
                           + "fonts, menu pictures, sounds, player, walls, doors and level MAP01, with a copy of Wolf3D's palette. "
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

        await dialog.ShowDialog(DialogOwner);
        return answer;
    }

    public async Task<RawDataModRequest?> AskRawDataMod(string modsFolder, DetectRawDataBase detect)
    {
        var folder = new TextBox { Watermark = "The mod's VSWAP, VGAGRAPH… folder" };
        var browse = new Button { Content = "Browse…" };
        var game = new ComboBox { MinWidth = 300, IsEnabled = false };
        var found = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };
        var whichIsWhich = new Button { Content = "Which file is which…", IsEnabled = false, Margin = new Thickness(0, 2, 0, 0) };
        // Which file is which, once the modder has said; null: by their names, else by what's in them
        PFWolf.Loaders.DataFileSet? assigned = null;
        string askedFor = "";
        var title = new TextBox();
        var target = new TextBox();
        var palette = new TextBox { Watermark = "Optional: the mod's own palette (.pal, .act, .gpl)" };
        var browsePalette = new Button { Content = "Browse…" };
        const string noPalette = "Left empty, the game keeps the palette of the one it's built on (a classic mod's exe may have had its own).";
        var paletteNote = new TextBlock { Text = noPalette, TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };
        var swatch = new Avalonia.Controls.Primitives.UniformGrid { Columns = 32, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Left };
        var error = new TextBlock { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
        var ok = new Button { Content = "Make it", IsDefault = true, Classes = { "accent" }, IsEnabled = false };
        var cancel = new Button { Content = "Cancel", IsCancel = true };

        // The palette file: how it reads, and its colors
        PFWolf.Managers.RawDataModPalette? ReadPalette(out string? problem)
        {
            problem = null;
            var path = palette.Text?.Trim() ?? "";
            if (path.Length == 0)
                return null;
            try
            {
                return PFWolf.Managers.RawDataModPalette.FromFile(path);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                problem = $"The palette can't be read: {e.Message}";
                return null;
            }
        }
        void ShowPalette()
        {
            swatch.Children.Clear();
            var read = ReadPalette(out var problem);
            swatch.IsVisible = read != null;
            paletteNote.Text = problem ?? (read != null ? $"Read as {read.How}: it goes in as the game palette." : noPalette);
            foreach (var color in read?.Colors ?? [])
                swatch.Children.Add(new Border { Width = 15, Height = 15, Background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(color.Red, color.Green, color.Blue)) });
        }
        palette.TextChanged += (_, _) =>
        {
            var path = palette.Text?.Trim() ?? "";
            if (path.Length == 0 || File.Exists(path))
                ShowPalette();
            else
            {
                // Still being typed, most likely: no colors until it's a file
                swatch.Children.Clear();
                swatch.IsVisible = false;
                paletteNote.Text = "There's no file there.";
            }
        };
        palette.LostFocus += (_, _) => ShowPalette();
        browsePalette.Click += async (_, _) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "The mod's palette",
                SuggestedStartLocation = await FolderOrNull(folder.Text?.Trim() ?? ""),
                FileTypeFilter =
                [
                    new FilePickerFileType("Palettes (pal, act, gpl)") { Patterns = ["*.pal", "*.act", "*.gpl"] },
                    new FilePickerFileType("Any file") { Patterns = ["*"] },
                ],
            });
            if (files is [var file, ..] && file.TryGetLocalPath() is { } local)
                palette.Text = local;
        };

        // The title, mod folder and palette follow the data folder until typed over, as in New mod
        string followedTitle = "", followedTarget = "", followedPalette = "";
        void Look()
        {
            var path = folder.Text?.Trim() ?? "";
            var name = Path.GetFileName(path.TrimEnd('\\', '/'));
            if ((title.Text ?? "") == followedTitle)
                title.Text = followedTitle = name;
            if ((target.Text ?? "") == followedTarget)
                target.Text = followedTarget = name.Length > 0 ? Path.Combine(modsFolder, name) : "";
            if ((palette.Text ?? "") == followedPalette)
                palette.Text = followedPalette = PFWolf.Managers.RawDataModGenerator.FindPaletteFile(path) ?? "";

            IReadOnlyList<PFWolf.Managers.BaseCandidate> candidates = [];
            try
            {
                candidates = Directory.Exists(path) ? detect(path, assigned) : [];
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or KeyNotFoundException)
            {
                error.Text = e.Message;
            }
            game.ItemsSource = candidates.Select(candidate => new GameChoice(candidate.ReleaseId,
                $"{candidate.Title}: " + (candidate.Share is { } share ? $"{PFWolf.Managers.RawDataModGenerator.ShareText(share)} the same" : "its files aren't here to compare"))).ToList();
            game.SelectedIndex = candidates.Count > 0 ? 0 : -1;
            game.IsEnabled = ok.IsEnabled = candidates.Count > 0;
            whichIsWhich.IsEnabled = Directory.Exists(path);
            var sniffed = candidates.Count > 0 && assigned == null ? candidates[0].Sniffed : 0;
            found.Text = candidates.Count == 0
                ? (path.Length == 0 ? "" : "No data files of a game PFWolf plays are there by name or by what's in them: say which file is which.")
                : "Its files: " + string.Join(", ", candidates[0].ModFiles.Assigned.Select(file => $"{Path.GetFileName(file.File)} ({PFWolf.Loaders.DataFileSniffer.StemOf(file.Kind)})"))
                  + (candidates[0].ModFiles.Assigned.Count() < PFWolf.Loaders.DataFileSniffer.Kinds.Count ? ". The rest come from the game's own files in the PFWolf folder." : ".")
                  + (sniffed > 0 ? $" {sniffed} of them told by what's in them, not their names: check which file is which." : "");
            _rawDataCandidates = candidates;

            // Files not named as the game names them: ask which is which, once per folder
            if (Directory.Exists(path) && assigned == null && askedFor != path
                && (sniffed > 0 || candidates.Count == 0 && Directory.EnumerateFiles(path).Any()))
            {
                askedFor = path;
                Avalonia.Threading.Dispatcher.UIThread.Post(async () => await AskWhich());
            }
        }

        async Task AskWhich()
        {
            var path = folder.Text?.Trim() ?? "";
            var current = assigned ?? (_rawDataCandidates.Count > 0 ? _rawDataCandidates[0].ModFiles : new PFWolf.Loaders.DataFileSet());
            if (await AskDataFiles(path, current) is { } answer)
            {
                assigned = answer;
                Look();
            }
        }
        whichIsWhich.Click += async (_, _) => await AskWhich();
        // Looked at when the folder field is left, or as soon as what's typed (or pasted) is a folder
        string looked = "";
        void LookIfNew()
        {
            if ((folder.Text ?? "") != looked)
            {
                looked = folder.Text ?? "";
                assigned = null;        // another folder: by its names again
                Look();
            }
        }
        folder.LostFocus += (_, _) => LookIfNew();
        folder.TextChanged += (_, _) =>
        {
            if (Directory.Exists(folder.Text?.Trim()))
                LookIfNew();
        };
        browse.Click += async (_, _) =>
        {
            var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "The folder of data files" });
            if (picked is [var chosen, ..] && chosen.TryGetLocalPath() is { } local)
            {
                folder.Text = local;
                LookIfNew();
            }
        };

        var dialog = MakeDialog("New mod from data files", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Makes a mod folder that plays a classic mod's edited data files as they are. Their chunks get the names of the "
                           + "game they're built on where they're its own (or replace its own); new ones get names like WALL0106 to change "
                           + "in the mod's raw-data-map.yaml.",
                    TextWrapping = TextWrapping.Wrap,
                },
                new TextBlock { Text = "Data files:" },
                new DockPanel { Children = { DockRight(browse), folder } },
                found,
                whichIsWhich,
                new TextBlock { Text = "Built on:" },
                game,
                new TextBlock { Text = "Title:" },
                title,
                new TextBlock { Text = "Palette:" },
                new DockPanel { Children = { DockRight(browsePalette), palette } },
                paletteNote,
                swatch,
                new TextBlock
                {
                    Text = "Mod folder (the data files are copied in; made here again later, the names given since are kept):",
                    TextWrapping = TextWrapping.Wrap,
                },
                target,
                error,
                Buttons(ok, cancel),
            },
        });
        dialog.Width = 540;

        RawDataModRequest? answer = null;
        ok.Click += (_, _) =>
        {
            var gameTitle = title.Text?.Trim() ?? "";
            var modFolder = target.Text?.Trim() ?? "";
            var candidate = game.SelectedIndex >= 0 && game.SelectedIndex < _rawDataCandidates.Count ? _rawDataCandidates[game.SelectedIndex] : null;
            var paletteRead = ReadPalette(out var paletteProblem);
            var problem = candidate == null ? "Pick a folder of data files a game PFWolf plays has."
                : gameTitle.Length == 0 ? "Give the game a title."
                : modFolder.Length == 0 ? "Pick the mod folder."
                : Editing.ModFiles.IsArchive(modFolder) ? "A raw data mod is a folder, not a pk3."
                : paletteProblem;
            if (problem != null)
            {
                error.Text = problem;
                return;
            }

            answer = new RawDataModRequest(folder.Text!.Trim(), candidate!, gameTitle, PFWolf.Managers.RawDataModWriter.GameIdFrom(gameTitle), Path.GetFullPath(modFolder),
                paletteRead != null ? Path.GetFullPath(palette.Text!.Trim()) : null);
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        dialog.Opened += (_, _) => folder.Focus();

        await dialog.ShowDialog(DialogOwner);
        return answer;
    }

    private IReadOnlyList<PFWolf.Managers.BaseCandidate> _rawDataCandidates = [];

    // What each data file holds, for the Which file is which… rows
    private static readonly Dictionary<PFWolf.Loaders.DataFileKind, string> DataFileRoles = new()
    {
        [PFWolf.Loaders.DataFileKind.Vswap] = "walls, sprites and digitized sounds (the biggest, often over 1 MB)",
        [PFWolf.Loaders.DataFileKind.VgaDict] = "the pictures' compression table (about 1 KB)",
        [PFWolf.Loaders.DataFileKind.VgaHead] = "where each picture is in VGAGRAPH (a few hundred bytes)",
        [PFWolf.Loaders.DataFileKind.VgaGraph] = "pictures, fonts, help and end texts",
        [PFWolf.Loaders.DataFileKind.AudioHed] = "where each sound is in AUDIOT (about 1 KB)",
        [PFWolf.Loaders.DataFileKind.AudioT] = "PC speaker and AdLib sounds, and the music",
        [PFWolf.Loaders.DataFileKind.MapHead] = "where each level is in GAMEMAPS (about 400 bytes)",
        [PFWolf.Loaders.DataFileKind.GameMaps] = "the levels (starts with TED5v1.0, TED6v1.0 or CED, the editor that saved it)",
    };

    /// <summary>
    /// Which file is which: a row per kind of data file, each picking one of the folder's files
    /// (with its size, and what it looks like by what's in it) or none (the game's own); null when
    /// cancelled
    /// </summary>
    private async Task<PFWolf.Loaders.DataFileSet?> AskDataFiles(string folder, PFWolf.Loaders.DataFileSet current)
    {
        var files = PFWolf.Managers.RawDataModGenerator.FolderFiles(folder);
        var looksLike = PFWolf.Loaders.DataFileSniffer.Guess(files.Select(file => file.File))
            .ToDictionary(guess => guess.Value, guess => guess.Key, StringComparer.OrdinalIgnoreCase);
        string Label(string file, long size)
            => $"{Path.GetFileName(file)}   {size:N0} bytes"
               + (looksLike.TryGetValue(file, out var kind) ? $"   · looks like {PFWolf.Loaders.DataFileSniffer.StemOf(kind)}" : "");
        const string none = "(none: the game's own)";
        var choices = new List<string> { none };
        choices.AddRange(files.Select(file => Label(file.File, file.Size)));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowSpacing = 6, ColumnSpacing = 10 };
        var boxes = new Dictionary<PFWolf.Loaders.DataFileKind, ComboBox>();
        var row = 0;
        foreach (var kind in PFWolf.Loaders.DataFileSniffer.Kinds)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var label = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = PFWolf.Loaders.DataFileSniffer.StemOf(kind), FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = DataFileRoles[kind], Opacity = 0.7, FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxWidth = 200 },
                },
            };
            var index = current[kind] is { } chosen ? files.FindIndex(file => file.File.Equals(chosen, StringComparison.OrdinalIgnoreCase)) + 1 : 0;
            var box = new ComboBox { ItemsSource = choices, SelectedIndex = Math.Max(0, index), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(label, row);
            Grid.SetRow(box, row);
            Grid.SetColumn(box, 1);
            grid.Children.Add(label);
            grid.Children.Add(box);
            boxes[kind] = box;
            row++;
        }

        var error = new TextBlock { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
        var ok = new Button { Content = "OK", IsDefault = true, Classes = { "accent" } };
        var guess = new Button { Content = "Guess again" };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var dialog = MakeDialog("Which file is which", new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = $"Which of {Path.GetFileName(folder.TrimEnd('\\', '/'))}'s files is each of the game's data files? A classic mod may have "
                           + "renamed them (FOXTROT1.WL6 for VSWAP.WL6). The sizes, and what each looks like by what's in it, help tell. "
                           + "One the mod doesn't have comes from the game's own files.",
                    TextWrapping = TextWrapping.Wrap,
                },
                grid,
                error,
                Buttons(guess, ok, cancel),
            },
        });
        dialog.Width = 720;

        guess.Click += (_, _) =>
        {
            error.Text = "";
            foreach (var (kind, box) in boxes)
                box.SelectedIndex = looksLike.FirstOrDefault(entry => entry.Value == kind).Key is { } file
                    ? files.FindIndex(f => f.File.Equals(file, StringComparison.OrdinalIgnoreCase)) + 1
                    : 0;
        };

        PFWolf.Loaders.DataFileSet? answer = null;
        ok.Click += (_, _) =>
        {
            var set = new PFWolf.Loaders.DataFileSet();
            foreach (var (kind, box) in boxes)
                set = set.With(kind, box.SelectedIndex > 0 ? files[box.SelectedIndex - 1].File : null);
            var twice = set.Assigned.GroupBy(file => file.File, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
            if (twice != null)
            {
                error.Text = $"{Path.GetFileName(twice.Key)} can't be both {string.Join(" and ", twice.Select(file => PFWolf.Loaders.DataFileSniffer.StemOf(file.Kind)))}.";
                return;
            }
            if (!set.Files.Any())
            {
                error.Text = "Pick at least one of the mod's files.";
                return;
            }
            answer = set;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(DialogOwner);
        return answer;
    }

    public async Task ShowText(string title, string text)
    {
        var close = new Button { Content = "Close", IsDefault = true, IsCancel = true, Classes = { "accent" } };
        var dialog = MakeDialog(title, new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBox
                {
                    Text = text, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Cascadia Mono,Consolas,monospace"), Height = 360,
                },
                Buttons(close),
            },
        });
        dialog.Width = 680;
        close.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(DialogOwner);
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

        await dialog.ShowDialog(DialogOwner);
        return answer;
    }

    public async Task<CodeAnswer?> AskCode(Editing.CodeForm form)
    {
        // A drop-down where the field has choices, else a number
        var inputs = new List<Func<string?>>();
        var rows = new StackPanel { Spacing = 6 };
        foreach (var field in form.Fields)
        {
            Control input;
            if (field.Choices is { Count: > 0 } choices)
            {
                var combo = new ComboBox
                {
                    ItemsSource = choices,
                    SelectedItem = choices.FirstOrDefault(choice => Editing.CodeField.Parse(choice) == field.Initial) ?? choices[0],
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    MaxDropDownHeight = 400,
                };
                inputs.Add(() => combo.SelectedItem as string);
                input = combo;
            }
            else
            {
                var number = new NumericUpDown { Value = field.Initial, Minimum = field.Min, Maximum = field.Max, Increment = 1, FormatString = "0", MinWidth = 140 };
                inputs.Add(() => number.Value?.ToString("0", System.Globalization.CultureInfo.InvariantCulture));
                input = number;
            }
            rows.Children.Add(new DockPanel
            {
                Children = { DockLeft(new TextBlock { Text = field.Label, Width = 110, VerticalAlignment = VerticalAlignment.Center }), input },
            });
        }

        var error = new TextBlock { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
        var ok = new Button { Content = "OK", IsDefault = true, Classes = { "accent" } };
        var pick = new Button { Content = "Pick on the map…", IsVisible = form.PicksTile };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var dialog = MakeDialog(form.Title, new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = form.Description, TextWrapping = TextWrapping.Wrap },
                rows,
                error,
                Buttons(ok, pick, cancel),
            },
        });
        dialog.Width = 520;

        CodeAnswer? answer = null;
        void Finish(bool pickOnMap)
        {
            var values = new int[form.Fields.Count];
            for (int i = 0; i < values.Length; i++)
            {
                // The target's x and y needn't be right when it's to be picked
                bool target = pickOnMap && i >= values.Length - 2;
                var text = inputs[i]();
                if (!target && form.Fields[i].Check(text) is { } problem)
                {
                    error.Text = problem + ".";
                    return;
                }
                values[i] = Editing.CodeField.Parse(text) ?? 0;
            }
            answer = new CodeAnswer(values, pickOnMap);
            dialog.Close();
        }
        ok.Click += (_, _) => Finish(false);
        pick.Click += (_, _) => Finish(true);
        cancel.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(DialogOwner);
        return answer;
    }

    public async Task<ReplaceRequest?> AskReplace(ReplaceSetup setup)
    {
        var from = new ComboBox
        {
            ItemsSource = setup.Found,
            SelectedItem = setup.Found.FirstOrDefault(choice => choice.Value == setup.From) ?? setup.Found.FirstOrDefault(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxDropDownHeight = 400,
        };
        var to = new ComboBox
        {
            ItemsSource = setup.Choices,
            SelectedItem = setup.Choices?.FirstOrDefault(choice => choice.Value == setup.To) ?? setup.Choices?.FirstOrDefault(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxDropDownHeight = 400,
            IsVisible = setup.Choices != null,
        };
        var toNumber = new NumericUpDown
        {
            Value = setup.To, Minimum = 0, Maximum = ushort.MaxValue, Increment = 1, FormatString = "0",
            IsVisible = setup.Choices == null,
        };
        var inSelection = new RadioButton { Content = "In the selection", GroupName = "where", IsEnabled = setup.HasSelection, IsChecked = setup.HasSelection };
        var inLevel = new RadioButton { Content = "In the whole level", GroupName = "where", IsChecked = !setup.HasSelection };
        var ok = new Button { Content = "Replace", IsDefault = true, Classes = { "accent" }, IsEnabled = setup.Found.Count > 0 };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var dialog = MakeDialog("Replace", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = $"On {setup.PlaneName}, put", TextWrapping = TextWrapping.Wrap },
                to,
                toNumber,
                new TextBlock { Text = "wherever the level has" },
                from,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, Children = { inSelection, inLevel } },
                new TextBlock { Text = "One undo step undoes it.", Opacity = 0.7 },
                Buttons(ok, cancel),
            },
        });
        dialog.Width = 480;

        ReplaceRequest? answer = null;
        ok.Click += (_, _) =>
        {
            if (from.SelectedItem is not ReplaceChoice found)
                return;
            ushort value = setup.Choices != null ? (to.SelectedItem as ReplaceChoice)?.Value ?? found.Value : (ushort)Math.Clamp(toNumber.Value ?? 0, 0, ushort.MaxValue);
            answer = new ReplaceRequest(found.Value, value, inSelection.IsChecked == true);
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(DialogOwner);
        return answer;
    }

    public async Task<bool> AskResize(string levelName, MapSizeViewModel size)
    {
        var error = new TextBlock { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        var ok = new Button { Content = "Resize", IsDefault = true, Classes = { "accent" } };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var dialog = MakeDialog($"Resize {levelName}", new StackPanel
        {
            Spacing = 12,
            Children = { new MapSizeView { DataContext = size }, error, Buttons(ok, cancel) },
        });
        dialog.Width = 520;

        var answer = false;
        ok.Click += (_, _) =>
        {
            try
            {
                size.Build();
                answer = true;
                dialog.Close();
            }
            catch (FormatException e)
            {
                error.Text = char.ToUpperInvariant(e.Message[0]) + e.Message[1..] + ".";
                error.IsVisible = true;
            }
        };
        cancel.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(DialogOwner);
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

        await dialog.ShowDialog(DialogOwner);
        return choice;
    }

    public async Task<bool> Confirm(string title, string message, string yes)
    {
        var ok = new Button { Content = yes, Classes = { "accent" } };
        var cancel = new Button { Content = "Cancel", IsDefault = true, IsCancel = true };
        var dialog = MakeDialog(title, new StackPanel
        {
            Spacing = 12,
            Children = { new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, Buttons(ok, cancel) },
        });

        bool answer = false;
        ok.Click += (_, _) => { answer = true; dialog.Close(); };
        cancel.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(DialogOwner);
        return answer;
    }

    public async Task<string?> PickMapFile()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "A map to import",
            SuggestedStartLocation = await FolderOrNull(ViewModel.GameFolder),
            FileTypeFilter =
            [
                new FilePickerFileType("Maps (WDC .map, ECWolf .wad)") { Patterns = ["*.map", "*.wad"] },
                new FilePickerFileType("All files") { Patterns = ["*"] },
            ],
        });
        return files is [var file, ..] ? file.TryGetLocalPath() : null;
    }

    public async Task<Editing.ImportRequest?> AskImport(ImportSetup setup)
    {
        var level = new ComboBox
        {
            ItemsSource = setup.Maps.Select((map, i) => $"{i + 1}. {Editing.MapImport.Label(map)}  ({map.Width}x{map.Height}, {map.Planes.Length} planes)").ToList(),
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var planeRows = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,260"), ColumnSpacing = 8, RowSpacing = 4 };
        var all = new Button { Content = "All" };
        var none = new Button { Content = "None" };
        var intoCurrent = new RadioButton
        {
            Content = new TextBlock
            {
                Text = setup.CurrentLevel != null
                    ? $"Onto {setup.CurrentLevel}, the level being edited (replaces those planes; Ctrl+Z undoes it)"
                    : "Onto the level being edited (none is open)",
                TextWrapping = TextWrapping.Wrap,
            },
            GroupName = "into",
            IsEnabled = setup.CurrentLevel != null,
            IsChecked = setup.CurrentLevel != null,
        };
        var intoNew = new RadioButton { Content = "As a new level named:", GroupName = "into", IsChecked = setup.CurrentLevel == null };
        var name = new TextBox { Text = setup.SuggestedName, Width = 120, IsEnabled = setup.CurrentLevel == null };
        var error = new TextBlock { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
        var ok = new Button { Content = "Import", IsDefault = true, Classes = { "accent" } };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        intoNew.IsCheckedChanged += (_, _) => name.IsEnabled = intoNew.IsChecked == true;

        // One row per plane the file's level has: whether it's imported, and onto which plane
        var rows = new List<(int From, CheckBox Use, ComboBox To)>();
        void ShowPlanes()
        {
            planeRows.Children.Clear();
            planeRows.RowDefinitions.Clear();
            rows.Clear();
            error.Text = "";
            var map = setup.Maps[Math.Max(level.SelectedIndex, 0)];
            if (Editing.MapImport.Check(map) is { } problem)
            {
                error.Text = problem;
                ok.IsEnabled = false;
                return;
            }
            ok.IsEnabled = true;

            foreach (var plane in Editing.MapImport.ImportablePlanes(map))
            {
                int used = Editing.MapImport.TilesInUse(map, plane);
                var use = new CheckBox
                {
                    Content = $"Plane {plane}",
                    IsChecked = used > 0,
                };
                var count = new TextBlock
                {
                    Text = used == 0 ? "empty" : $"{used} tiles",
                    Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center,
                };
                var to = new ComboBox
                {
                    ItemsSource = setup.Planes,
                    SelectedItem = setup.Planes.FirstOrDefault(option => option.Plane == plane),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    [ToolTip.TipProperty] = "The level's plane it goes onto",
                };
                use.IsCheckedChanged += (_, _) => to.IsEnabled = use.IsChecked == true;
                to.IsEnabled = use.IsChecked == true;

                int row = planeRows.RowDefinitions.Count;
                planeRows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(use, row);
                Grid.SetRow(count, row);
                Grid.SetColumn(count, 1);
                Grid.SetRow(to, row);
                Grid.SetColumn(to, 2);
                planeRows.Children.AddRange([use, count, to]);
                rows.Add((plane, use, to));
            }

            var extra = map.Planes.Length - rows.Count;
            if (extra > 0)
                error.Text = $"The file's last {extra} plane{(extra == 1 ? " is" : "s are")} past the {setup.Planes.Count} a level has, so {(extra == 1 ? "it's" : "they're")} left out.";
        }
        ShowPlanes();
        level.SelectionChanged += (_, _) => ShowPlanes();
        all.Click += (_, _) => rows.ForEach(row => row.Use.IsChecked = true);
        none.Click += (_, _) => rows.ForEach(row => row.Use.IsChecked = false);

        var dialog = MakeDialog($"Import {setup.FileName}", new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "Level in the file:" },
                level,
                new DockPanel
                {
                    Children =
                    {
                        DockRight(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { all, none } }),
                        new TextBlock { Text = "Planes to import, and onto which:", VerticalAlignment = VerticalAlignment.Center },
                    },
                },
                planeRows,
                intoCurrent,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { intoNew, name } },
                new TextBlock
                {
                    Text = "A plane imported replaces the whole plane. A new level gets a walled square on the planes not imported.",
                    TextWrapping = TextWrapping.Wrap, Opacity = 0.7,
                },
                error,
                Buttons(ok, cancel),
            },
        });
        dialog.Width = 560;
        level.IsEnabled = setup.Maps.Count > 1;

        Editing.ImportRequest? answer = null;
        ok.Click += (_, _) =>
        {
            var picked = rows.Where(row => row.Use.IsChecked == true)
                .Select(row => new Editing.PlaneImport(row.From, (row.To.SelectedItem as PlaneOption)?.Plane ?? row.From))
                .ToList();
            string? newName = intoNew.IsChecked == true ? name.Text?.Trim() ?? "" : null;
            var problem = picked.Count == 0 ? "Pick at least one plane to import."
                : picked.GroupBy(p => p.To).FirstOrDefault(g => g.Count() > 1) is { } clash ? $"Two planes go onto plane {clash.Key}; pick another for one of them."
                : newName != null ? setup.CheckName(newName)
                : null;
            if (problem != null)
            {
                error.Text = problem;
                return;
            }

            answer = new Editing.ImportRequest(setup.Maps[Math.Max(level.SelectedIndex, 0)], picked, newName);
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(DialogOwner);
        return answer;
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
