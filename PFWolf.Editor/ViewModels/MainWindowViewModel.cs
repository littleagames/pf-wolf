using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Constants;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;
using PFWolf.Editor.Views;
using PFWolf.Exceptions;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// A game the editor can open: a game pack id, or empty to pick by the data files. FromMod: a
/// game one of the mods adds (a stand-alone game).
/// </summary>
public sealed record GameChoice(string Id, string Label, bool FromMod = false)
{
    public override string ToString() => Label;
}

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly EditorSettings _settings;

    // The levels opened since the game loaded, edited or not, by asset name
    private readonly Dictionary<string, MapDocument> _documents = new(StringComparer.OrdinalIgnoreCase);

    private List<PaletteItem> _allPaletteItems = [];
    private (int X, int Y) _hover = (-1, -1);
    // What the pointer was last on in the 3D view, while it's there (redescribed after an edit)
    private (SurfaceHit? Hit, (int X, int Y) Target)? _hover3D;

    public MainWindowViewModel(EditorSettings settings)
    {
        _settings = settings;
        _gameFolder = settings.GameFolder;
        _selectedTool = Tools.First(tool => tool.Tool == EditTool.Paint);
        _selectedPlane = Planes[0];
        _show3D = settings.Show3D;
        foreach (var mod in settings.Mods)
            Mods.Add(mod);
        RefreshModGames();
        _selectedGame = GameChoices.FirstOrDefault(choice => choice.Id.Equals(settings.Game, StringComparison.OrdinalIgnoreCase)) ?? GameChoices[0];

        Controller.ValuePicked += (_, value) => SelectValue(value);
        Controller.Changed += (_, _) => OnToolStateChanged();
        Controller.Transformed += (_, report) => OnTransformed(report);

        // The shared code's warnings (an asset that can't be read, say) go to the problems list
        WarningLog.Sink = message => Dispatcher.UIThread.Post(() => Problems.Add(message));
    }

    /// <summary>Answers the editor's questions; the window sets it</summary>
    public IEditorDialogs? Dialogs { get; set; }

    public ToolController Controller { get; } = new();

    /// <summary>
    /// Wolf3D and Spear for now; Blake Stone's games come later. Then the games the mods add
    /// (stand-alone games, which need no data files); the first choice picks one of those too.
    /// </summary>
    public ObservableCollection<GameChoice> GameChoices { get; } =
    [
        new("", "By the data files or mods"),
        new("wolf3d", "Wolfenstein 3D"),
        new("wolf3d-apogee", "Wolfenstein 3D (Apogee)"),
        new("wolf3d-shareware", "Wolfenstein 3D (shareware)"),
        new("spear", "Spear of Destiny"),
        new("spear-demo", "Spear of Destiny (demo)"),
    ];

    /// <summary>PFWolf's own games, which a new mod can be made for</summary>
    public IReadOnlyList<GameChoice> BaseGameChoices => GameChoices.Where(choice => choice.Id.Length > 0 && !choice.FromMod).ToList();

    /// <summary>Lists the games the mods add after PFWolf's own, keeping the one picked when it's still there</summary>
    private void RefreshModGames()
    {
        var picked = SelectedGame?.Id;
        foreach (var choice in GameChoices.Where(choice => choice.FromMod).ToList())
            GameChoices.Remove(choice);

        foreach (var mod in PFWolf.Loaders.ModSource.OpenGameMods(Mods))
            foreach (var (id, game) in mod.Games!.GamePacks)
                if (GameChoices.All(choice => !choice.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                    GameChoices.Add(new GameChoice(id, $"{game.Title ?? id} ({mod.DisplayName})", FromMod: true));

        if (picked != null)
            SelectedGame = GameChoices.FirstOrDefault(choice => choice.Id.Equals(picked, StringComparison.OrdinalIgnoreCase)) ?? GameChoices[0];
    }

    /// <summary>The tools, in the tool strip's order and groups: selecting, drawing, picking</summary>
    public IReadOnlyList<ToolOption> Tools { get; } =
    [
        new(EditTool.Select, "Select", "S",
            "Drag to select a block of tiles (Shift: add to the selection, Ctrl: take away); drag inside the selection or use the arrow keys to move it. "
            + "Ctrl+C, Ctrl+X, Ctrl+V and Ctrl+D copy, cut, paste and duplicate it; Delete clears it; ] and [ rotate it (or the paste being placed), "
            + "Shift+H and Shift+V flip it.",
            EditorIcons.Select),
        new(EditTool.MagicWand, "Magic wand", "W",
            "Click a tile to select the tiles joined to it that hold the same on the plane being edited (or every one on the level, with Joined only off). "
            + "Shift adds to the selection, Ctrl takes away.",
            EditorIcons.MagicWand),
        new(EditTool.Paint, "Paint", "P", "Puts what's picked in the palette on every tile the brush passes over (its size and shape in the options bar).",
            EditorIcons.Paint, GroupStart: true),
        new(EditTool.Erase, "Erase", "E", "Clears every tile the brush passes over: open floor on plane 0, nothing on the others.", EditorIcons.Erase),
        new(EditTool.Scatter, "Scatter", "A",
            "Sprinkles what's picked (or the scatter set's values, at random) on some of the tiles the brush passes over, as many as the density says: "
            + "decorations, rubble, a forest of pillars.",
            EditorIcons.Scatter),
        new(EditTool.Line, "Line", "L", "Drag a straight line of what's picked; Shift keeps it to a row, a column or a diagonal.", EditorIcons.Line),
        new(EditTool.Rectangle, "Rectangle", "R", "Drag a box of what's picked (Outline only: just its edge); Shift makes it square.", EditorIcons.Rectangle),
        new(EditTool.Ellipse, "Ellipse", "O", "Drag an ellipse of what's picked (Outline only: just its edge); Shift makes it a circle.", EditorIcons.Ellipse),
        new(EditTool.Room, "Room", "M", "Drag a room: walls of what's picked round the edge, open floor inside; Shift makes it square.", EditorIcons.Room),
        new(EditTool.Fill, "Fill", "F", "Fills the joined tiles that hold the same as the one clicked.", EditorIcons.Fill),
        new(EditTool.Stamp, "Stamp", "T",
            "Puts what's copied (Ctrl+C) down around the pointer at each click; a drag lays copies side by side. ] [ Shift+H Shift+V turn the stamp.",
            EditorIcons.Stamp),
        new(EditTool.Pick, "Pick", "I", "Click a tile to pick what's on it into the palette (an eyedropper).", EditorIcons.Pick, GroupStart: true),
    ];

    public IReadOnlyList<PlaneOption> Planes { get; } =
    [
        new(0, "Walls, doors and floors (plane 0)"),
        new(0, "Wall patches (plane 0, high byte)", Patches: true),
        new(1, "Things (plane 1)"),
        new(MapConstants.FlatPlane, "Flats (plane 2)"),
        new(MapConstants.HeightPlane, "Heights (plane 3)"),
        new(MapConstants.TagPlane, "Tags (plane 4)"),
        new(MapConstants.ZonePlane, "Light zones (plane 5)"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoad))]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    private string _gameFolder;

    [ObservableProperty]
    private GameChoice _selectedGame;

    public ObservableCollection<string> Mods { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveModCommand))]
    private string? _selectedMod;

    public ObservableCollection<MapItem> Maps { get; } = [];

    [ObservableProperty]
    private MapItem? _selectedMap;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NewMapCommand), nameof(ImportMapCommand))]
    private GameContent? _content;

    [ObservableProperty]
    private ArtCache? _art;

    [ObservableProperty]
    private MapTiles? _tiles;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand), nameof(RedoCommand), nameof(SaveCommand), nameof(SaveAsCommand),
        nameof(PlayCommand), nameof(PlayHereCommand))]
    private MapDocument? _document;

    public ObservableCollection<string> Problems { get; } = [];

    [ObservableProperty]
    private string _status = "Open the folder PFWolf is in to see its levels.";

    [ObservableProperty]
    private string _hoverInfo = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoad))]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    private bool _isLoading;

    /// <summary>What loading the game is doing now, shown over the map while it loads</summary>
    [ObservableProperty]
    private string _loadingStep = "";

    // Which layers the map shows
    [ObservableProperty] private bool _showWalls = true;
    [ObservableProperty] private bool _showThings = true;
    [ObservableProperty] private bool _showFlats;
    [ObservableProperty] private bool _showHeights;
    [ObservableProperty] private bool _showTags;
    [ObservableProperty] private bool _showZones;
    [ObservableProperty] private bool _showGrid = true;

    // The 3D view beside the map, and what it shows
    [ObservableProperty] private bool _show3D;
    [ObservableProperty] private bool _show3DThings = true;
    [ObservableProperty] private bool _show3DCeilings = true;
    [ObservableProperty] private bool _show3DShading = true;
    [ObservableProperty] private bool _show3DMarkers = true;

    /// <summary>Where the 3D view looks from; the map shows it too</summary>
    public Camera3D Camera { get; } = new();

    partial void OnShow3DChanged(bool value)
    {
        _settings.Show3D = value;
        _settings.Save();
    }

    [RelayCommand]
    private void Toggle3D() => Show3D = !Show3D;

    //
    // The texture and sprite browser
    //

    private ArtBrowserViewModel? _artBrowser;

    /// <summary>Opens the texture and sprite browser on what the palette has picked</summary>
    [RelayCommand]
    private void ShowArt()
    {
        if (Dialogs == null)
            return;

        if (_artBrowser == null)
        {
            _artBrowser = new ArtBrowserViewModel
            {
                PickMod = PickModToSaveIn,
                AskText = (title, prompt, initial, check) => Dialogs.AskText(title, prompt, initial, check),
                Confirm = (title, message, yes) => Dialogs.Confirm(title, message, yes),
                AskUnsaved = message => Dialogs.AskUnsaved(message),
                ImageEditor = _settings.ImageEditor,
            };
            _artBrowser.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ArtBrowserViewModel.ImageEditor))
                {
                    _settings.ImageEditor = _artBrowser.ImageEditor;
                    _settings.Save();
                }
            };
            _artBrowser.UseRequested += (_, use) => UseInPalette(use);
            _artBrowser.Changed += async (_, change) => await OnArtFilesChanged(change);
            _artBrowser.Load(Content, Art);
        }

        if (SelectedPaletteItem?.Entry is { } entry && HasPalette)
        {
            if (entry.Texture != null)
                _artBrowser.Show(ArtKind.Texture, entry.Texture);
            else if (entry.ThingClass != null && Content?.ThingSpriteName(entry.ThingClass) is { } sprite)
                _artBrowser.Show(ArtKind.Sprite, sprite);
        }
        Dialogs.ShowArtBrowser(_artBrowser);
    }

    // A mod's pictures changed: the game is loaded again with the mod, so everything shows the change
    private async Task OnArtFilesChanged(ArtChange change)
    {
        AddModFolder(change.ModPath);
        var (status, before, level) = (_artBrowser?.Status, Content, SelectedMap?.Name);
        if (CanLoad)
            await LoadAsync();
        if (_artBrowser == null)
            return;
        if (Content == before || Content == null)
        {
            _artBrowser.Status = status + "  (load the game again to see it)";
            return;
        }

        // Back on the level that was open
        if (Maps.FirstOrDefault(map => map.Name.Equals(level, StringComparison.OrdinalIgnoreCase)) is { } map)
            SelectedMap = map;
        _artBrowser.Show(change.Kind, change.ShowName);
        _artBrowser.Status = status ?? "";
    }

    // A browser took a file out of a mod: the game is loaded again without it, back on the level
    // that was open, and the browser still says what it did
    private async Task ReloadAfterDelete(Func<string> status, Action<string> setStatus)
    {
        var (said, before, level) = (status(), Content, SelectedMap?.Name);
        if (CanLoad)
            await LoadAsync();
        if (Content == before || Content == null)
        {
            setStatus(said + "  (load the game again to see it)");
            return;
        }
        if (Maps.FirstOrDefault(map => map.Name.Equals(level, StringComparison.OrdinalIgnoreCase)) is { } map)
            SelectedMap = map;
        setStatus(said);
    }

    // A game loaded again: the browsers list its pictures (the old ones' bitmaps are gone) and sounds
    partial void OnArtChanged(ArtCache? value)
    {
        // The same game in new colors: only the pictures change
        if (_recoloring)
        {
            if (value != null)
            {
                _artBrowser?.Recolor(value);
                _demoBrowser?.Recolor(value);
            }
            _textBrowser?.Recolor();
            return;
        }

        _artBrowser?.Load(Content, value);
        _soundBrowser?.Load(Content);
        _demoBrowser?.Load(Content, value);
        _paletteBrowser?.Load(Content);
        _textBrowser?.Load(Content);
        _scriptBrowser?.Load(Content);
        _definitions?.Load(Content, Mods.ToList());
    }

    //
    // The definitions window
    //

    private DefinitionsViewModel? _definitions;

    /// <summary>Opens the definitions window: game info, game packs, actors and map definitions</summary>
    [RelayCommand]
    private void ShowDefinitions()
    {
        if (Dialogs == null)
            return;

        if (_definitions == null)
        {
            _definitions = new DefinitionsViewModel
            {
                PickMod = PickModToSaveIn,
                AskText = (title, prompt, initial, check) => Dialogs.AskText(title, prompt, initial, check),
                Confirm = (title, message, yes) => Dialogs.Confirm(title, message, yes),
                Art = (hint, name) => hint == Definitions.FieldHint.Class ? Art?.ThingSprite(name) : Art?.Texture(name),
            };
            _definitions.SavedToMod += (_, folder) => AddModFolder(folder);
            _definitions.ReloadRequested += async (_, _) => await LoadAsync();
            Mods.CollectionChanged += (_, _) => _definitions.SetMods(Mods.ToList());
            _definitions.Load(Content, Mods.ToList());
        }
        Dialogs.ShowDefinitions(_definitions);
    }

    /// <summary>Opens the definitions window on the mapdefs entry of what the palette has picked</summary>
    [RelayCommand]
    private void EditPaletteDefinition()
    {
        if (SelectedPaletteItem?.Entry is not { } entry)
            return;
        var id = entry.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        IReadOnlyList<string>? path = entry.Group switch
        {
            "Walls" => ["walls", id],
            "Doors" => ["doors", id],
            "Floor" => ["floors"],
            "Player starts" => ["player-starts", id],
            "Things" => ["things", id],
            "Triggers" => ["triggers", id],
            "Diagonals" => ["diagonals", id],
            PaletteEntries.PatchGroup => ["patches", (entry.Value >> MapConstants.PatchShift).ToString(System.Globalization.CultureInfo.InvariantCulture)],
            _ => null,
        };
        if (path == null)
            return;
        ShowDefinitions();
        _definitions?.ShowEntry(Definitions.DefinitionKind.MapDefs, path);
    }

    private bool PaletteHasClass => SelectedPaletteItem?.Entry.ThingClass != null;

    /// <summary>Opens the definitions window on the actordefs class of the thing the palette has picked</summary>
    [RelayCommand(CanExecute = nameof(PaletteHasClass))]
    private void EditPaletteClass()
    {
        if (SelectedPaletteItem?.Entry.ThingClass is not { } thingClass)
            return;
        ShowDefinitions();
        _definitions?.ShowEntry(Definitions.DefinitionKind.Actors, [thingClass]);
    }

    //
    // The text browser
    //

    private TextBrowserViewModel? _textBrowser;

    /// <summary>Opens the text browser, on the first article the first time</summary>
    [RelayCommand]
    private void ShowTexts()
    {
        if (Dialogs == null)
            return;

        if (_textBrowser == null)
        {
            _textBrowser = new TextBrowserViewModel
            {
                PickMod = PickModToSaveIn,
                AskText = (title, prompt, initial, check) => Dialogs.AskText(title, prompt, initial, check),
                Confirm = (title, message, yes) => Dialogs.Confirm(title, message, yes),
            };
            _textBrowser.Saved += (_, folder) => AddModFolder(folder);
            _textBrowser.Deleted += async (_, _) => await ReloadAfterDelete(() => _textBrowser.Status, status => _textBrowser.Status = status);
            _textBrowser.Load(Content);
        }
        Dialogs.ShowTextBrowser(_textBrowser);
    }

    //
    // The script browser
    //

    private ScriptBrowserViewModel? _scriptBrowser;

    /// <summary>Opens the script browser, on the first script the first time</summary>
    [RelayCommand]
    private void ShowScripts()
    {
        if (Dialogs == null)
            return;

        if (_scriptBrowser == null)
        {
            _scriptBrowser = new ScriptBrowserViewModel
            {
                PickMod = PickModToSaveIn,
                AskText = (title, prompt, initial, check) => Dialogs.AskText(title, prompt, initial, check),
                Confirm = (title, message, yes) => Dialogs.Confirm(title, message, yes),
            };
            _scriptBrowser.Saved += (_, folder) => AddModFolder(folder);
            _scriptBrowser.Deleted += async (_, _) => await ReloadAfterDelete(() => _scriptBrowser.Status, status => _scriptBrowser.Status = status);
            // An action's caller: the actor class in the Definitions window, opened at the state's frame
            _scriptBrowser.ShowCaller += (_, caller) =>
            {
                ShowDefinitions();
                _definitions?.ShowEntry(Definitions.DefinitionKind.Actors, [caller.Actor],
                    ["states", caller.State, caller.Frame.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
            };
            _scriptBrowser.Load(Content);
        }
        Dialogs.ShowScriptBrowser(_scriptBrowser);
    }

    //
    // The palette browser
    //

    private PaletteBrowserViewModel? _paletteBrowser;

    /// <summary>Opens the palette browser, on the game palette the first time</summary>
    [RelayCommand]
    private void ShowPalettes()
    {
        if (Dialogs == null)
            return;

        if (_paletteBrowser == null)
        {
            _paletteBrowser = new PaletteBrowserViewModel
            {
                PickMod = PickModToSaveIn,
                Confirm = (title, message, yes) => Dialogs.Confirm(title, message, yes),
            };
            _paletteBrowser.Saved += (_, folder) => AddModFolder(folder);
            _paletteBrowser.Deleted += async (_, _) => await ReloadAfterDelete(() => _paletteBrowser.Status, status => _paletteBrowser.Status = status);
            _paletteBrowser.GamePaletteChanged += (_, colors) => QueueRecolor(colors);
            _paletteBrowser.Load(Content);
        }
        Dialogs.ShowPaletteBrowser(_paletteBrowser);
    }

    // Set while the art is swapped for the same pictures in new colors
    private bool _recoloring;
    private PFWolf.Assets.PaletteColor[]? _pendingPalette;
    private DispatcherTimer? _recolorTimer;

    // The map, 3D view, palette and texture browser redraw in the game palette being edited;
    // a slider being dragged redraws them a few times a second, not on every step
    private void QueueRecolor(PFWolf.Assets.PaletteColor[] colors)
    {
        _pendingPalette = colors;
        if (_recolorTimer == null)
        {
            _recolorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            _recolorTimer.Tick += (_, _) =>
            {
                _recolorTimer.Stop();
                if (_pendingPalette is { } pending)
                    Recolor(pending);
                _pendingPalette = null;
            };
        }
        if (!_recolorTimer.IsEnabled)
            _recolorTimer.Start();
    }

    /// <summary>Draws the loaded game in these colors: the map, 3D view, palette and texture browser</summary>
    public void Recolor(PFWolf.Assets.PaletteColor[] colors)
    {
        if (Content is not { } content)
            return;

        content.SetPalette(colors);
        var oldArt = Art;
        _recoloring = true;
        try
        {
            Art = new ArtCache(content);

            // The palette's pictures, keeping what's picked (and the thing's facing and skill)
            var selected = SelectedPaletteItem?.Entry;
            _allPaletteItems = _allPaletteItems.Select(item => item with { Icon = PaletteIcon(item.Entry) }).ToList();
            FilterPalette();
            if (selected != null)
                SelectedPaletteItem = PaletteItems.FirstOrDefault(item => item.Entry == selected);
        }
        finally
        {
            _recoloring = false;
        }
        oldArt?.Dispose();
    }

    private Avalonia.Media.Imaging.Bitmap? PaletteIcon(PaletteEntry entry)
        => entry.Texture == null ? entry.ThingClass != null ? Art?.ThingSprite(entry.ThingClass) : null
            : entry.Group == PaletteEntries.PatchGroup ? Art?.Flat(entry.Texture)     // see-through where the wall shows
            : Art?.Texture(entry.Texture);

    // The game only sees what's saved in a mod (a folder or a pk3) with the mod loaded
    private bool AddModFolder(string folder)
    {
        if (Mods.Any(mod => Path.GetFullPath(mod).TrimEnd('\\', '/').Equals(Path.GetFullPath(folder).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)))
            return false;
        Mods.Add(folder);
        RefreshModGames();
        SaveSettings();
        return true;
    }

    //
    // The sound browser
    //

    private SoundBrowserViewModel? _soundBrowser;

    /// <summary>Opens the sound browser, on the level's music the first time</summary>
    [RelayCommand]
    private void ShowSounds()
    {
        if (Dialogs == null)
            return;

        if (_soundBrowser == null)
        {
            _soundBrowser = new SoundBrowserViewModel
            {
                Confirm = (title, message, yes) => Dialogs.Confirm(title, message, yes),
            };
            _soundBrowser.Deleted += async (_, _) => await ReloadAfterDelete(() => _soundBrowser.Status, status => _soundBrowser.Status = status);
            _soundBrowser.Load(Content);
            if (Document?.Properties.Music is { Length: > 0 } music)
                _soundBrowser.Show(SoundKind.Music, music);
        }
        Dialogs.ShowSoundBrowser(_soundBrowser);
    }

    //
    // The demo browser
    //

    private DemoBrowserViewModel? _demoBrowser;

    /// <summary>Opens the demo browser, on the first demo the first time</summary>
    [RelayCommand]
    private void ShowDemos()
    {
        if (Dialogs == null)
            return;

        if (_demoBrowser == null)
        {
            _demoBrowser = new DemoBrowserViewModel
            {
                // The game plays the demos to trace them, as Play starts it: from its folder, with the mods
                GameToRun = () => Content == null ? null
                    : (Path.Combine(GameFolder, OperatingSystem.IsWindows() ? "PFWolf.exe" : "PFWolf"), GameFolder, Mods.ToList()),
            };
            _demoBrowser.Load(Content, Art);
        }
        Dialogs.ShowDemoBrowser(_demoBrowser);
    }

    /// <summary>Picks what uses a picture in the browser (a wall, a door, a flat, a thing) to put down</summary>
    public void UseInPalette(ArtUse use)
    {
        if (use.Target is not { } target
            || Planes.FirstOrDefault(option => option.Plane == target.Plane && option.Patches == target.Patch) is not { } plane)
            return;

        SelectedPlane = plane;
        if (HasPalette)
            SelectValue(target.Value);
        else
            PlaneValue = ((int)(PlaneValue ?? 0) & target.Keep) | target.Value;
        Status = $"Putting down: {use.Label}";
    }

    // The tools
    [ObservableProperty] private ToolOption _selectedTool;
    [ObservableProperty] private bool _outline;
    [ObservableProperty] private bool _copyAllPlanes = true;
    [ObservableProperty] private bool _wandJoined = true;
    [ObservableProperty] private bool _pasteSkipsBlank;
    [ObservableProperty] private int _brushSize = 1;
    [ObservableProperty] private bool _brushRound;
    [ObservableProperty] private int _scatterDensity = 20;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPalette), nameof(IsFlatsPlane), nameof(HasNextUnused), nameof(PlaneHint))]
    private PlaneOption _selectedPlane;

    public bool HasPalette => PaletteEntries.HasPalette(SelectedPlane.Plane);

    public ObservableCollection<PaletteItem> PaletteItems { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditPaletteClassCommand))]
    private PaletteItem? _selectedPaletteItem;
    [ObservableProperty] private string _paletteFilter = "";

    /// <summary>What the tools put down on planes 2 to 5, which take any number</summary>
    [ObservableProperty] private decimal? _planeValue = 1;

    // A thing class's codes (plane 1): which way it faces, the skills it's on, whether it patrols
    private Dictionary<string, List<ThingVariant>> _thingVariants = [];
    private List<ThingVariant> _currentVariants = [];
    private bool _syncing;

    public ObservableCollection<ChoiceOption> FacingOptions { get; } = [];
    public ObservableCollection<ChoiceOption> SkillOptions { get; } = [];
    [ObservableProperty] private ChoiceOption? _selectedFacing;
    [ObservableProperty] private ChoiceOption? _selectedSkill;
    [ObservableProperty] private bool _thingPatrols;
    [ObservableProperty] private bool _canPatrol;
    [ObservableProperty] private bool _hasThingOptions;

    // The flats (plane 2): floor and ceiling picked from the mapdefs flats tables
    public ObservableCollection<ChoiceOption> FlatFloorOptions { get; } = [];
    public ObservableCollection<ChoiceOption> FlatCeilingOptions { get; } = [];
    [ObservableProperty] private ChoiceOption? _selectedFlatFloor;
    [ObservableProperty] private ChoiceOption? _selectedFlatCeiling;

    public bool IsFlatsPlane => SelectedPlane.Plane == MapConstants.FlatPlane;
    public bool HasNextUnused => SelectedPlane.Plane is MapConstants.TagPlane or MapConstants.ZonePlane;

    /// <summary>What a plane's number means</summary>
    public string PlaneHint => SelectedPlane.Patches
        ? "A picture over a wall's faces (mapdefs patches). Patches only go on walls; the walls themselves stay as they are."
        : SelectedPlane.Plane switch
    {
        MapConstants.FlatPlane => "Ceiling × 256 + floor: the mapdefs flats indices. 0 is the level's default floor and ceiling.",
        MapConstants.HeightPlane => $"The wall's height in stories, 1 to 8. 0 uses the level's height ({LevelWallHeight}). Taller than 1 on open floor makes an arch.",
        MapConstants.TagPlane => "A switch acts on the doors, walls and things that share its tag. 0 is no tag.",
        MapConstants.ZonePlane => "The level's light zones (Level properties); 0 is lit as the level.",
        _ => "",
    };

    private int LevelWallHeight => Document?.Properties.WallHeight ?? Content?.DefaultMapInfo.WallHeight ?? 1;

    public bool CanLoad => !IsLoading && !string.IsNullOrWhiteSpace(GameFolder);

    public string WindowTitle => Document == null
        ? "PFWolf Editor"
        : $"{Document.AssetName.ToUpperInvariant()}{(Document.IsDirty ? " *" : "")} - PFWolf Editor";

    private IEnumerable<MapDocument> DirtyDocuments => _documents.Values.Where(document => document.IsDirty);

    /// <summary>Loads the game in the game folder, with the mods, and shows its first level</summary>
    [RelayCommand(CanExecute = nameof(CanLoad))]
    public async Task LoadAsync()
    {
        if (!await ResolveUnsaved("Loading the game again drops the changes to"))
            return;

        IsLoading = true;
        LoadingStep = "Starting";
        Problems.Clear();
        Status = $"Loading {GameFolder}...";

        string folder = GameFolder, game = SelectedGame.Id;
        var mods = Mods.ToList();
        try
        {
            // The loading thread's steps come back here to be shown
            IProgress<string> progress = new Progress<string>(step => LoadingStep = step);
            var content = await Task.Run(() => GameContent.Load(folder, game, mods, progress.Report));

            var oldArt = Art;
            Document = null;
            Controller.Document = null;
            Tiles = null;
            _documents.Clear();
            Content = content;
            Art = new ArtCache(content);
            oldArt?.Dispose();

            Maps.Clear();
            foreach (var map in content.Maps)
                Maps.Add(new MapItem(map.Name, map.Title));

            // The rest is on this thread: each step is shown before it starts
            LoadingStep = "Drawing the palette's pictures";
            await ShowLoadingStep();
            BuildPalette();
            FillPlaySkills(content);

            LoadingStep = Maps.Count > 0 ? $"Opening {Maps[0].Name.ToUpperInvariant()}" : "Finishing";
            await ShowLoadingStep();
            SelectedMap = Maps.FirstOrDefault();
            // The map and the 3D view draw it before the loading screen goes
            await ShowLoadingStep();

            Status = $"{content.Title}: {content.Maps.Count} levels" + mods.Count switch { 0 => "", 1 => ", 1 mod", _ => $", {mods.Count} mods" };
            SaveSettings();
        }
        catch (Exception e) when (e is DataFilesException or FileNotFoundException or InvalidDataException or IOException
                                      or KeyNotFoundException or PfWolfMapException or UnauthorizedAccessException)
        {
            Status = "Couldn't load the game.";
            Problems.Add(e.Message);
        }
        finally
        {
            IsLoading = false;
            LoadingStep = "";
        }
    }

    // Lets the window draw (the loading step, a level just opened) before more work on this thread
    private static async Task ShowLoadingStep()
        => await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

    partial void OnSelectedMapChanged(MapItem? value)
    {
        Document = value != null ? OpenDocument(value.Name) : null;
        Controller.Document = Document;
        var document = Document;
        Tiles = document != null && Content != null ? new MapTiles(Content, document.Map, () => document.Properties) : null;
        PlaceCameraAtStart();
        HoverInfo = "";
        OnPropertyChanged(nameof(PlaneHint));
        EditPropertiesCommand.NotifyCanExecuteChanged();
        CheckLevelCommand.NotifyCanExecuteChanged();
        Checks.Clear();
    }

    private MapDocument? OpenDocument(string name)
    {
        if (_documents.TryGetValue(name, out var document))
            return document;
        if (Content?.FindMap(name) is not { } map)
            return null;

        var info = Content.MapInfoOf(name);
        document = new MapDocument(name, map, properties: info != null ? MapProperties.From(info) : null) { SaveFolder = Content.ModPathOf(name) };
        document.Changed += OnDocumentChanged;
        _documents[name] = document;
        return document;
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        if (sender is not MapDocument document)
            return;

        if (Maps.FirstOrDefault(item => item.Name.Equals(document.AssetName, StringComparison.OrdinalIgnoreCase)) is { } item)
            item.IsDirty = document.IsDirty;
        OnPropertyChanged(nameof(WindowTitle));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        if (_hover3D is var (hit, target))
            Hover3D(hit, target);
        else if (_hover.X >= 0)
            Hover(_hover.X, _hover.Y);
    }

    //
    // Tools and palette
    //

    partial void OnSelectedToolChanged(ToolOption value)
    {
        Controller.Tool = value.Tool;
        Controller.Refresh();       // the pointer shows what the tool covers
    }

    partial void OnOutlineChanged(bool value) => Controller.Outline = value;
    partial void OnCopyAllPlanesChanged(bool value) => Controller.CopyAllPlanes = value;
    partial void OnWandJoinedChanged(bool value) => Controller.WandJoined = value;
    partial void OnPasteSkipsBlankChanged(bool value) => Controller.PasteSkipsBlank = value;

    partial void OnBrushSizeChanged(int value)
    {
        Controller.BrushSize = value;
        Controller.Refresh();
    }

    partial void OnBrushRoundChanged(bool value) => Controller.BrushRound = value;
    partial void OnScatterDensityChanged(int value) => Controller.ScatterDensity = value;

    /// <summary>The options bar shows the outline option: the rectangle's and the ellipse's</summary>
    public bool ShowsOutlineOption => SelectedTool.Tool is EditTool.Rectangle or EditTool.Ellipse;

    /// <summary>The options bar shows the magic wand's joined-only option</summary>
    public bool ShowsWandOption => SelectedTool.Tool == EditTool.MagicWand;

    /// <summary>The options bar shows the brush's size and shape</summary>
    public bool ShowsBrushOptions => SelectedTool.Tool is EditTool.Paint or EditTool.Erase or EditTool.Scatter;

    /// <summary>The options bar shows the scatter brush's density and set</summary>
    public bool ShowsScatterOptions => SelectedTool.Tool == EditTool.Scatter;

    /// <summary>The options bar shows the stamp's size</summary>
    public bool ShowsStampOptions => SelectedTool.Tool == EditTool.Stamp;

    /// <summary>The options bar shows what works on the selection: the selecting tools', or while pasting</summary>
    public bool ShowsSelectionOptions => SelectedTool.Tool is EditTool.Select or EditTool.MagicWand || Controller.IsPasting;

    /// <summary>The options bar shows the rotate and flip buttons and Paste over: for the selection, a paste or the stamp</summary>
    public bool ShowsTurnOptions => ShowsSelectionOptions || SelectedTool.Tool == EditTool.Stamp;

    /// <summary>What the stamp puts down</summary>
    public string StampInfo => Controller.ClipboardSize is var (width, height)
        ? $"{width} × {height} tiles (what was last copied)"
        : "Copy something first (select it, Ctrl+C)";

    /// <summary>The values the scatter brush picks from at random; empty, it scatters what's picked</summary>
    public ObservableCollection<ReplaceChoice> ScatterSet { get; } = [];

    public string ScatterSetInfo => ScatterSet.Count == 0 ? "Scatters what's picked" : string.Join(", ", ScatterSet.Select(choice => choice.Label));

    /// <summary>Adds what's picked in the palette to the scatter set</summary>
    [RelayCommand]
    private void AddToScatterSet()
    {
        var value = Controller.Value;
        if (ScatterSet.All(choice => choice.Value != value))
            ScatterSet.Add(new ReplaceChoice(value, ShortLabel(value)));
        OnScatterSetChanged();
    }

    [RelayCommand]
    private void ClearScatterSet()
    {
        ScatterSet.Clear();
        OnScatterSetChanged();
    }

    private void OnScatterSetChanged()
    {
        Controller.ScatterValues = ScatterSet.Select(choice => choice.Value).ToList();
        OnPropertyChanged(nameof(ScatterSetInfo));
    }

    // A value's palette name without its number
    private string ShortLabel(ushort value)
    {
        var label = ValueLabel(value);
        int cut = label.IndexOf("  (", StringComparison.Ordinal);
        return cut > 0 ? label[..cut] : label;
    }

    /// <summary>What the mouse does with the tool picked (or the paste being placed), for the status bar</summary>
    public string ToolHint => Controller.IsPasting
        ? "Pasting: click to put it down · ] [ rotate · Shift+H, Shift+V flip · Esc drops it"
        : SelectedTool.Tool switch
        {
            EditTool.Select when Controller.Selection != null
                => "Drag inside the selection to move it · drag outside for a new one · ] [ rotate · Shift+H, Shift+V flip · Esc: select nothing",
            EditTool.Select => "Drag to select a block of tiles · Shift adds · Ctrl takes away",
            EditTool.MagicWand => "Click to select the tiles like it · Shift adds · Ctrl takes away",
            EditTool.Paint => "Drag to paint",
            EditTool.Erase => "Drag to erase",
            EditTool.Scatter => "Drag to scatter",
            EditTool.Line => "Drag from one end of the line to the other · Shift: straight",
            EditTool.Rectangle or EditTool.Ellipse or EditTool.Room => "Drag from one corner to the other · Shift: square",
            EditTool.Stamp => "Click to stamp what's copied · drag to lay copies side by side · ] [ rotate",
            EditTool.Fill => "Click to fill",
            EditTool.Pick => "Click a tile to pick what's on it",
            _ => "",
        } + " · middle or right drag: pan · wheel: zoom (Shift/Alt: scroll) · Home: fit";

    // The selection or the paste changed: what the options bar, the status bar and the Edit menu offer
    private void OnToolStateChanged()
    {
        OnPropertyChanged(nameof(ShowsOutlineOption));
        OnPropertyChanged(nameof(ShowsWandOption));
        OnPropertyChanged(nameof(ShowsBrushOptions));
        OnPropertyChanged(nameof(ShowsScatterOptions));
        OnPropertyChanged(nameof(ShowsStampOptions));
        OnPropertyChanged(nameof(ShowsSelectionOptions));
        OnPropertyChanged(nameof(ShowsTurnOptions));
        OnPropertyChanged(nameof(StampInfo));
        DuplicateCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ToolHint));
        CopyCommand.NotifyCanExecuteChanged();
        CutCommand.NotifyCanExecuteChanged();
        DeleteSelectionCommand.NotifyCanExecuteChanged();
        PasteCommand.NotifyCanExecuteChanged();
        RotateClockwiseCommand.NotifyCanExecuteChanged();
        RotateCounterClockwiseCommand.NotifyCanExecuteChanged();
        Rotate180Command.NotifyCanExecuteChanged();
        FlipHorizontalCommand.NotifyCanExecuteChanged();
        FlipVerticalCommand.NotifyCanExecuteChanged();
    }

    //
    // Rotating and flipping the selection, or the paste being placed
    //

    private bool CanTransform => Controller.CanTransform;

    [RelayCommand(CanExecute = nameof(CanTransform))]
    private void RotateClockwise() => Controller.Transform(TileTransform.RotateClockwise);

    [RelayCommand(CanExecute = nameof(CanTransform))]
    private void RotateCounterClockwise() => Controller.Transform(TileTransform.RotateCounterClockwise);

    [RelayCommand(CanExecute = nameof(CanTransform))]
    private void Rotate180() => Controller.Transform(TileTransform.Rotate180);

    [RelayCommand(CanExecute = nameof(CanTransform))]
    private void FlipHorizontal() => Controller.Transform(TileTransform.FlipHorizontal);

    [RelayCommand(CanExecute = nameof(CanTransform))]
    private void FlipVertical() => Controller.Transform(TileTransform.FlipVertical);

    /// <summary>Says what a rotate or flip did to what points somewhere, and what it couldn't turn</summary>
    private void OnTransformed(TransformReport report)
    {
        var (turned, unmatched, paired) = report.Counts;
        var parts = new List<string>();
        if (turned > 0)
            parts.Add($"{Tiles_(turned)} turned to face the new way");
        if (unmatched > 0)
            parts.Add($"{Tiles_(unmatched)} left as {(unmatched == 1 ? "it was" : "they were")}: the mapdefs have no value pointing the new way");
        if (paired > 0)
            parts.Add($"{Tiles_(paired)} read the tile east of {(paired == 1 ? "it" : "them")}, which moved: check {(paired == 1 ? "it" : "them")}");
        var what = Controller.IsPasting ? " (the paste)" : SelectedTool.Tool == EditTool.Stamp ? " (the stamp)" : "";
        Status = $"{TileTransforms.Describe(report.Transform)}{what}"
                 + (parts.Count > 0 ? ": " + string.Join("; ", parts) : "");

        static string Tiles_(int count) => count == 1 ? "1 tile" : $"{count} tiles";
    }

    //
    // The Edit menu: the selection and the clipboard (the map's keys do the same over the map)
    //

    private bool HasSelection => Controller.Selection != null;
    private bool CanPaste => Controller.HasClipboard && Document != null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Copy()
    {
        Controller.Copy();
        Status = "Copied the selection";
        PasteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Cut()
    {
        Controller.Cut();
        Status = "Cut the selection";
        PasteCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Picks up the clipboard where the pointer was last on the map, else at the selection</summary>
    [RelayCommand(CanExecute = nameof(CanPaste))]
    private void Paste()
    {
        var (x, y) = _hover.X >= 0 ? _hover : Controller.Selection is { } selection ? (selection.Left, selection.Top) : (0, 0);
        Controller.StartPaste(x, y);
        Status = "Pasting: move the pointer over the map and click to put it down";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteSelection() => Controller.Delete();

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void SelectAll()
    {
        SelectedTool = Tools.First(tool => tool.Tool == EditTool.Select);
        Controller.SelectAll();
    }

    [RelayCommand]
    private void SelectNone() => Controller.Escape();

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void InvertSelection() => Controller.InvertSelection();

    /// <summary>Copies the selection and picks the copy up to put down somewhere else</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Duplicate()
    {
        Controller.Duplicate();
        Status = "Duplicated the selection: click to put the copy down";
    }

    /// <summary>One value for another on the plane being edited, in the selection or the whole level</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task ReplaceAsync()
    {
        if (Document is not { } document || Dialogs == null)
            return;
        Controller.CancelGesture();

        // What's on the level, most used first; what can go down instead
        var read = Controller.Read ?? (value => value);
        int plane = SelectedPlane.Plane;
        var found = document.Map.MapData[plane].GroupBy(value => read(value))
            .OrderByDescending(group => group.Count())
            .Select(group => new ReplaceChoice(group.Key, $"{ValueLabel(group.Key)}   ({group.Count()} tiles)"))
            .ToList();
        var choices = HasPalette
            ? ReplaceValues().Select(value => new ReplaceChoice(value, ValueLabel(value))).ToList()
            : null;
        var from = _hover.X >= 0 ? read(document[plane, _hover.X, _hover.Y])
            : Controller.Selection is { } selection ? read(document[plane, selection.Left, selection.Top])
            : found.FirstOrDefault()?.Value ?? 0;

        var setup = new ReplaceSetup(SelectedPlane.Label, found, choices, from, Controller.Value, Controller.Selection != null);
        if (await Dialogs.AskReplace(setup) is not { } request)
            return;
        int count = Controller.Replace(request.From, request.To, request.SelectionOnly);
        Status = $"Replaced {ValueLabel(request.From)} with {ValueLabel(request.To)} on {count} tile{(count == 1 ? "" : "s")}"
                 + (request.SelectionOnly ? " in the selection" : "");
    }

    /// <summary>Every value the palette can put down on the plane being edited, things in each way they come</summary>
    private IEnumerable<ushort> ReplaceValues()
    {
        var values = _allPaletteItems.Select(item => item.Entry.Value);
        if (SelectedPlane.Plane == 1)
            values = values.Concat(_thingVariants.Values.SelectMany(variants => variants.Select(v => v.Id)));
        return values.Distinct().Order();
    }

    /// <summary>A value on the plane being edited, as the palette names it (a thing with its facing, skill and patrolling)</summary>
    private string ValueLabel(ushort value)
    {
        if (!HasPalette)
            return SelectedPlane.Plane == MapConstants.FlatPlane ? $"{value} (floor {value & 0xff}, ceiling {value >> 8})" : value.ToString();
        if (SelectedPlane.Plane == 1 && Content?.MapDefs.Things.TryGetValue(value, out var thing) == true)
        {
            var details = new List<string> { PaletteEntries.Facing(thing.Angles) };
            if (thing.Patrol != 0)
                details.Add("patrols");
            if (thing.MinSkill > 0)
                details.Add($"skill {thing.MinSkill + 1} and up");
            return $"{thing.Class}, {string.Join(", ", details)}  ({value})";
        }
        return _allPaletteItems.FirstOrDefault(item => item.Entry.Value == value)?.Label ?? $"{value}  (not in the mapdefs)";
    }

    /// <summary>The map should zoom to show the whole level</summary>
    public event EventHandler? FitRequested;

    [RelayCommand]
    private void FitView() => FitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>A tool from the Tools menu, by its key letter</summary>
    [RelayCommand]
    private void PickTool(string key) => SelectToolByKey(key);

    partial void OnSelectedPlaneChanged(PlaneOption value)
    {
        Controller.CancelGesture();
        Controller.Plane = value.Plane;
        ScatterSet.Clear();     // values from another plane's palette
        OnScatterSetChanged();

        // Show the plane being edited
        switch (value.Plane)
        {
            case 0: ShowWalls = true; break;
            case 1: ShowThings = true; break;
            case MapConstants.FlatPlane: ShowFlats = true; break;
            case MapConstants.HeightPlane: ShowHeights = true; break;
            case MapConstants.TagPlane: ShowTags = true; break;
            case MapConstants.ZonePlane: ShowZones = true; break;
        }
        BuildPalette();
    }

    partial void OnSelectedPaletteItemChanged(PaletteItem? value)
    {
        // Recolor puts back the same entry with a new picture: nothing to set again
        if (value == null || _recoloring)
            return;

        Controller.Value = value.Entry.Value;
        if (value.Entry.ThingClass is { } thingClass && SelectedPlane.Plane == 1
            && _thingVariants.TryGetValue(thingClass, out var variants) && variants.Count > 1)
            ShowThingOptions(variants, variants[0]);
        else
            HasThingOptions = false;
    }

    /// <summary>The facings, skills and patrolling a thing class comes in, set to one of its codes</summary>
    private void ShowThingOptions(List<ThingVariant> variants, ThingVariant current)
    {
        _syncing = true;

        // Rebuilt only for another class: rebuilding while a drop-down is picking blanks it
        if (!ReferenceEquals(_currentVariants, variants))
        {
            _currentVariants = variants;
            FacingOptions.Clear();
            foreach (var angles in variants.Select(v => v.Angles).Distinct().Order())
                FacingOptions.Add(new ChoiceOption(angles, PaletteEntries.Facing(angles)));
            SkillOptions.Clear();
            foreach (var skill in variants.Select(v => v.MinSkill).Distinct().Order())
                SkillOptions.Add(new ChoiceOption(skill, skill == 0 ? "Every skill" : $"Skill {skill + 1} and up"));
            CanPatrol = variants.Any(v => v.Patrols) && variants.Any(v => !v.Patrols);
        }

        SelectedFacing = FacingOptions.FirstOrDefault(option => option.Value == current.Angles);
        SelectedSkill = SkillOptions.FirstOrDefault(option => option.Value == current.MinSkill);
        ThingPatrols = current.Patrols;
        HasThingOptions = true;
        Controller.Value = current.Id;
        _syncing = false;
    }

    partial void OnSelectedFacingChanged(ChoiceOption? value) => PickThingVariant();
    partial void OnSelectedSkillChanged(ChoiceOption? value) => PickThingVariant();
    partial void OnThingPatrolsChanged(bool value) => PickThingVariant();

    /// <summary>The thing code for the facing, skill and patrolling chosen (or the nearest it comes in)</summary>
    private void PickThingVariant()
    {
        if (_syncing || _currentVariants.Count == 0 || SelectedFacing == null || SelectedSkill == null)
            return;

        var variant = PaletteEntries.Closest(_currentVariants, SelectedFacing.Value, ThingPatrols, SelectedSkill.Value);
        bool exact = variant.Angles == SelectedFacing.Value && variant.Patrols == ThingPatrols && variant.MinSkill == SelectedSkill.Value;
        ShowThingOptions(_currentVariants, variant);
        Status = exact ? $"Object code {variant.Id}" : $"It doesn't come that way; the nearest is object code {variant.Id}";
    }

    partial void OnPlaneValueChanged(decimal? value)
    {
        if (HasPalette)
            return;
        var number = (ushort)Math.Clamp(value ?? 0, 0, ushort.MaxValue);
        Controller.Value = number;

        if (IsFlatsPlane && !_syncing)
        {
            _syncing = true;
            SelectedFlatFloor = FlatFloorOptions.FirstOrDefault(option => option.Value == (number & 0xff));
            SelectedFlatCeiling = FlatCeilingOptions.FirstOrDefault(option => option.Value == number >> 8);
            _syncing = false;
        }
    }

    partial void OnSelectedFlatFloorChanged(ChoiceOption? value) => PickFlats();
    partial void OnSelectedFlatCeilingChanged(ChoiceOption? value) => PickFlats();

    private void PickFlats()
    {
        if (_syncing || !IsFlatsPlane)
            return;
        _syncing = true;
        PlaneValue = ((SelectedFlatCeiling?.Value ?? 0) << 8) | (SelectedFlatFloor?.Value ?? 0);
        _syncing = false;
    }

    private static void FillFlatOptions(ObservableCollection<ChoiceOption> options, Dictionary<int, string> flats)
    {
        options.Clear();
        if (!flats.ContainsKey(0))
            options.Add(new ChoiceOption(0, "0  the level's default"));
        foreach (var (index, name) in flats.Where(flat => flat.Key is >= 0 and <= 255).OrderBy(flat => flat.Key))
            options.Add(new ChoiceOption(index, $"{index}  {name}"));
    }

    /// <summary>One more than the highest tag (or zone) the level uses</summary>
    [RelayCommand]
    private void NextUnused()
    {
        if (Document is not { } document)
            return;
        var plane = SelectedPlane.Plane;
        PlaneValue = document.Map.MapData[plane].Max() + 1;
    }

    partial void OnPaletteFilterChanged(string value) => FilterPalette();

    private void BuildPalette()
    {
        var plane = SelectedPlane.Plane;
        HasThingOptions = false;
        if (Content is { } content)
        {
            Controller.EraseValue = p => PaletteEntries.EraseValue(content.MapDefs, p);
            Controller.MakeRemap = transform => new TileRemap(content.MapDefs, transform, content.IsWallSprite);
            Controller.IsBlank = (p, value) => IsBlank(content.MapDefs, p, value);
            (Controller.Combine, Controller.Read) = plane != 0 ? (null, null)
                : SelectedPlane.Patches ? PaletteEntries.PatchLayer(content.MapDefs) : PaletteEntries.TileLayer(content.MapDefs);
            _thingVariants = PaletteEntries.ThingVariants(content.MapDefs);
            if (plane == MapConstants.FlatPlane)
            {
                _syncing = true;
                FillFlatOptions(FlatFloorOptions, content.MapDefs.Flats.Floor);
                FillFlatOptions(FlatCeilingOptions, content.MapDefs.Flats.Ceiling);
                _syncing = false;
            }
            _allPaletteItems = (SelectedPlane.Patches ? PaletteEntries.Patches(content.MapDefs) : PaletteEntries.ForPlane(content.MapDefs, plane))
                .Select(entry => new PaletteItem(entry, PaletteIcon(entry)))
                .ToList();
        }
        else
            _allPaletteItems = [];

        FilterPalette();
        if (HasPalette)
            SelectedPaletteItem = PaletteItems.FirstOrDefault(item => item.Entry.Group is "Walls" or "Patches") ?? PaletteItems.FirstOrDefault();
        else
            OnPlaneValueChanged(PlaneValue);
    }

    /// <summary>What a paste over leaves out: open floor without a patch on plane 0, and nothing (0) on the rest</summary>
    private static bool IsBlank(PFWolf.Assets.MapObjectTranslationAsset defs, int plane, ushort value)
    {
        if (plane != 0)
            return value == 0;
        int code = value & MapConstants.Plane0CodeMask;
        return value >> MapConstants.PatchShift == 0 && !defs.Doors.ContainsKey(code)
               && !(code is > 0 and <= MapConstants.MaxWallId && defs.Walls.ContainsKey(code));
    }

    private void FilterPalette()
    {
        var selected = SelectedPaletteItem;
        PaletteItems.Clear();
        foreach (var item in _allPaletteItems.Where(item => string.IsNullOrWhiteSpace(PaletteFilter)
                     || item.Label.Contains(PaletteFilter, StringComparison.OrdinalIgnoreCase)
                     || item.Group.Contains(PaletteFilter, StringComparison.OrdinalIgnoreCase)))
            PaletteItems.Add(item);
        if (selected != null && PaletteItems.Contains(selected))
            SelectedPaletteItem = selected;
    }

    /// <summary>The eyedropper's value: its palette entry, or the number on a plane without one</summary>
    private void SelectValue(ushort value)
    {
        if (!HasPalette)
        {
            PlaneValue = value;
            return;
        }

        PaletteFilter = "";

        // A thing: its class, set to this code's facing, skill and patrolling
        if (SelectedPlane.Plane == 1 && Content?.MapDefs.Things.TryGetValue(value, out var thing) == true
            && _thingVariants.TryGetValue(thing.Class, out var variants))
        {
            SelectedPaletteItem = PaletteItems.FirstOrDefault(item => item.Entry.ThingClass == thing.Class);
            if (variants.Count > 1 && variants.FirstOrDefault(v => v.Id == value) is { } variant)
                ShowThingOptions(variants, variant);
            return;
        }

        var item = PaletteItems.FirstOrDefault(item => item.Entry.Value == value);
        if (item != null)
            SelectedPaletteItem = item;
        else
            Status = $"{value} isn't in the palette (the mapdefs don't define it)";
    }

    /// <summary>A tool by its key letter, from the map canvas</summary>
    public void SelectToolByKey(string key)
    {
        if (Tools.FirstOrDefault(tool => tool.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) is { } tool)
            SelectedTool = tool;
    }

    //
    // Undo, save, new level
    //

    private bool CanUndo => Document?.CanUndo == true;
    private bool CanRedo => Document?.CanRedo == true;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        Controller.CancelGesture();
        var description = Document?.UndoDescription;
        Document?.Undo();
        Status = $"Undid {description?.ToLowerInvariant()}";
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        Controller.CancelGesture();
        var description = Document?.RedoDescription;
        Document?.Redo();
        Status = $"Redid {description?.ToLowerInvariant()}";
    }

    private bool HasDocument => Document != null;

    /// <summary>Saves the level to its mod (a folder or a pk3), asking for one the first time</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task SaveAsync()
    {
        if (Document != null)
            await SaveDocument(Document, Document.SaveFolder);
    }

    /// <summary>Saves the level to a mod picked now</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task SaveAsAsync()
    {
        if (Document != null)
            await SaveDocument(Document, null);
    }

    private async Task<bool> SaveDocument(MapDocument document, string? folder)
    {
        folder ??= await PickModToSaveIn($"A mod to save {document.AssetName.ToUpperInvariant()} in (it goes in its maps folder)");
        if (folder == null)
            return false;

        Controller.CancelGesture();
        try
        {
            var path = MapFiles.Save(document, folder);
            Status = $"Saved {path}";

            if (AddModFolder(folder))
                Status += "; it's in the mods now, so the next load reads it";
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or YamlDotNet.Core.YamlException)
        {
            Status = "Couldn't save the level.";
            Problems.Add($"Saving {document.AssetName}: {e.Message}");
            return false;
        }
    }

    private bool CanMakeMap => Content != null;

    /// <summary>A new level: a walled square of open floor, under a name picked now</summary>
    [RelayCommand(CanExecute = nameof(CanMakeMap))]
    private async Task NewMapAsync()
    {
        if (Content is not { } content || Dialogs == null)
            return;

        var name = await Dialogs.AskText("New level", "Level name (it's saved as maps/NAME.wad):", NextFreeMapName(), CheckNewMapName);
        if (name == null)
            return;

        var wall = (ushort)(content.MapDefs.Walls.Keys.Where(id => id is > 0 and <= MapConstants.MaxWallId).DefaultIfEmpty(1).Min());
        var map = MapFiles.NewMap(name.ToUpperInvariant(), wall, PaletteEntries.EraseValue(content.MapDefs, 0));
        AddNewDocument(name.ToUpperInvariant(), map);
        Status = $"New level {map.Name}: saving it into a mod lists it in the mod's game-info too (warp there with the map command)";
    }

    /// <summary>
    /// Planes from a WDC map file (.map) or an ECWolf binary map (.wad): picked ones onto the
    /// level being edited (one undo step), or a new level made of them
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanMakeMap))]
    private async Task ImportMapAsync()
    {
        if (Content is not { } content || Dialogs == null || await Dialogs.PickMapFile() is not { } path)
            return;

        IReadOnlyList<PFWolf.Loaders.WdcMap> maps;
        try
        {
            maps = MapImport.ReadFile(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Status = "Couldn't import the map.";
            Problems.Add($"Importing {Path.GetFileName(path)}: {e.Message}");
            return;
        }

        Controller.CancelGesture();
        var setup = new ImportSetup(Path.GetFileName(path), maps, Planes.Where(option => !option.Patches).ToList(), Document?.AssetName.ToUpperInvariant(),
            NextFreeMapName(), CheckNewMapName);
        if (await Dialogs.AskImport(setup) is not { } request || request.Planes.Count == 0)
            return;

        var planes = string.Join(", ", request.Planes.Select(p => p.From == p.To ? $"{p.From}" : $"{p.From}→{p.To}"));
        var from = $"{Path.GetFileName(path)} ({MapImport.Label(request.Source)})";
        if (request.NewLevelName is not { } name)
        {
            if (Document is not { } document)
                return;
            int changed = MapImport.Apply(document, request.Source, request.Planes,
                $"Import of plane{(request.Planes.Count == 1 ? "" : "s")} {planes}");
            Status = $"Imported plane{(request.Planes.Count == 1 ? "" : "s")} {planes} from {from}: {changed} tiles changed (Ctrl+Z undoes it)";
            return;
        }

        name = name.ToUpperInvariant();
        var title = string.IsNullOrWhiteSpace(request.Source.Name) ? name : request.Source.Name;
        var wall = (ushort)(content.MapDefs.Walls.Keys.Where(id => id is > 0 and <= MapConstants.MaxWallId).DefaultIfEmpty(1).Min());
        var map = MapImport.NewLevel(title, request.Source, request.Planes, wall, PaletteEntries.EraseValue(content.MapDefs, 0));
        AddNewDocument(name, map);
        Status = $"New level {name} from {from}, plane{(request.Planes.Count == 1 ? "" : "s")} {planes}: saving it into a mod lists it in the mod's game-info too";
    }

    private string? CheckNewMapName(string text)
        => MapFiles.CheckName(text) ?? (_documents.ContainsKey(text) || Content?.HasMap(text) == true ? $"There's already a level called {text}." : null);

    /// <summary>A level made in the editor, unsaved until it's written somewhere, shown now</summary>
    private void AddNewDocument(string name, PFWolf.Assets.MapAsset map)
    {
        var document = new MapDocument(name, map, isNew: true);
        document.SetProperties(new MapProperties { Name = map.Name });
        document.Changed += OnDocumentChanged;
        _documents[document.AssetName] = document;

        var item = new MapItem(document.AssetName, map.Name) { IsDirty = true };
        Maps.Add(item);
        SelectedMap = item;
    }

    private string NextFreeMapName()
    {
        for (int n = 1; n < 1000; n++)
        {
            var name = $"MAP{n:00}";
            if (!_documents.ContainsKey(name) && Content?.HasMap(name) != true)
                return name;
        }
        return "NEWMAP";
    }

    /// <summary>
    /// Asks about levels with unsaved changes before they'd be lost: saves them, lets them go,
    /// or says to stop (false)
    /// </summary>
    public async Task<bool> ResolveUnsaved(string what)
    {
        var dirty = DirtyDocuments.ToList();
        var palettes = _paletteBrowser?.DirtyDocuments.ToList() ?? [];
        var definitions = _definitions?.UnsavedNames ?? [];
        if (dirty.Count + palettes.Count + definitions.Count == 0 || Dialogs == null)
            return true;

        var names = string.Join(", ", dirty.Select(document => document.AssetName.ToUpperInvariant())
            .Concat(palettes.Select(palette => $"palette {palette.Name}"))
            .Concat(definitions));
        switch (await Dialogs.AskUnsaved($"{what} {names}. Save first?"))
        {
            case UnsavedChoice.Save:
                // The definitions first: what they save is what the levels are loaded with next
                if (definitions.Count > 0 && !await _definitions!.SaveAllAsync())
                {
                    Status = "Not everything in the definitions window could be saved: " + _definitions.Status;
                    ShowDefinitions();
                    return false;
                }
                foreach (var document in dirty)
                {
                    if (!await SaveDocument(document, document.SaveFolder))
                        return false;
                }
                foreach (var palette in palettes)
                {
                    if (!await _paletteBrowser!.SaveDocument(palette, palette.SaveFolder))
                        return false;
                }
                return true;
            case UnsavedChoice.Discard:
                return true;
            default:
                return false;
        }
    }

    //
    // Level properties and checks
    //

    /// <summary>The level's game-info entry: name, music, next level, look, shading and light zones</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task EditPropertiesAsync()
    {
        if (Document is not { } document || Content is not { } content || Dialogs == null)
            return;

        var dialog = new MapPropertiesViewModel(document.AssetName.ToUpperInvariant(), document.Properties, content.DefaultMapInfo,
            content.MusicNames, Maps.Select(map => map.Name.ToUpperInvariant()).ToList(), content.WeatherNames);
        if (!await Dialogs.EditProperties(dialog) || dialog.Build() is not { } properties)
            return;

        if (properties.ChangedKeys(document.Properties).Count > 0)
        {
            document.SetProperties(properties);
            Status = "Changed the level properties: saving the level writes them into the mod's game-info.yaml";
        }
        OnPropertyChanged(nameof(PlaneHint));
    }

    /// <summary>What the last level check found</summary>
    public ObservableCollection<LevelProblem> Checks { get; } = [];

    [ObservableProperty] private LevelProblem? _selectedCheck;

    /// <summary>A check's tile should be shown: the window scrolls the map to it</summary>
    public event EventHandler<(int X, int Y)>? GoToTile;

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void CheckLevel()
    {
        Checks.Clear();
        if (Tiles == null || Document == null)
            return;

        foreach (var problem in LevelChecks.Run(Tiles, Document.InGameInfo))
            Checks.Add(problem);
        int errors = Checks.Count(problem => problem.Severity == CheckSeverity.Error);
        Status = Checks.Count == 0
            ? $"{Document.AssetName.ToUpperInvariant()} looks fine"
            : $"{Document.AssetName.ToUpperInvariant()}: {errors} {(errors == 1 ? "error" : "errors")}, {Checks.Count - errors} {(Checks.Count - errors == 1 ? "warning" : "warnings")} (Level checks below)";
        ChecksExpanded = Checks.Count > 0;
    }

    [ObservableProperty] private bool _checksExpanded;

    partial void OnSelectedCheckChanged(LevelProblem? value)
    {
        if (value is { HasTile: true })
        {
            Controller.SelectTile(value.X, value.Y);
            GoToTile?.Invoke(this, (value.X, value.Y));
            Hover(value.X, value.Y);
        }
    }

    //
    // Play: the game started on the level being edited
    //

    /// <summary>The skills Play can start on, from game-info (1 is the easiest)</summary>
    public ObservableCollection<ChoiceOption> PlaySkills { get; } = [];

    [ObservableProperty] private ChoiceOption? _selectedPlaySkill;

    partial void OnSelectedPlaySkillChanged(ChoiceOption? value)
    {
        if (value != null)
            _settings.PlaySkill = value.Value;
    }

    private void FillPlaySkills(GameContent content)
    {
        PlaySkills.Clear();
        int index = 0;
        foreach (var (key, skill) in content.GameInfo?.Skills ?? [])
        {
            index++;
            var name = string.IsNullOrWhiteSpace(skill.Name) || skill.Name.StartsWith('$') ? key : skill.Name;
            PlaySkills.Add(new ChoiceOption(index, $"{index}  {name}"));
        }

        // The last one played, else the game's own default for --warp: the third
        SelectedPlaySkill = PlaySkills.FirstOrDefault(option => option.Value == _settings.PlaySkill)
                            ?? PlaySkills.ElementAtOrDefault(Math.Min(2, PlaySkills.Count - 1));
    }

    /// <summary>Saves the level if it needs it, then starts PFWolf on it from its player start (F5)</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private Task PlayAsync() => Play(null);

    /// <summary>The same, starting on the tile under the pointer, or else the selection's (Shift+F5)</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private Task PlayHereAsync()
    {
        (int X, int Y)? tile = _hover.X >= 0 ? _hover
            : Controller.Selection is { } selection ? (selection.Left, selection.Top)
            : null;
        return Play(tile);
    }

    private async Task Play((int X, int Y)? start)
    {
        if (Document is not { } document || Content is not { } content)
            return;

        // The game reads the level from its mod, so it has to be saved there first
        if (document.IsDirty && !await SaveDocument(document, document.SaveFolder))
        {
            Status = "Not played: the level has to be saved for the game to see the changes";
            return;
        }

        var exe = Path.Combine(GameFolder, OperatingSystem.IsWindows() ? "PFWolf.exe" : "PFWolf");
        if (!File.Exists(exe))
        {
            Problems.Add($"Play: there's no {Path.GetFileName(exe)} in {GameFolder}");
            Status = "Couldn't start the game.";
            return;
        }

        var arguments = GameLaunch.Arguments(content.PackId, document.AssetName, SelectedPlaySkill?.Value, start, Mods);
        try
        {
            GameLaunch.Start(exe, GameFolder, arguments);
            Status = $"Started PFWolf on {document.AssetName.ToUpperInvariant()}"
                     + (SelectedPlaySkill != null ? $", skill {SelectedPlaySkill.Value}" : "")
                     + (start is { } tile ? $", from {tile.X},{tile.Y}" : "");
            SaveSettings();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException)
        {
            Problems.Add($"Play: {e.Message}");
            Status = "Couldn't start the game.";
        }
    }

    //
    // Mods, hover, settings
    //

    public void AddMod(string path)
    {
        // pfwolf.pk3 is loaded under every mod already, and the editor mustn't save into it
        if (ModFiles.WhyReadOnly(path) is { } why)
        {
            Status = $"Not added: {why}";
            return;
        }
        if (!Mods.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            Mods.Add(path);
            RefreshModGames();
        }
    }

    private bool CanRemoveMod => SelectedMod != null;

    [RelayCommand(CanExecute = nameof(CanRemoveMod))]
    private void RemoveMod()
    {
        if (SelectedMod != null)
        {
            Mods.Remove(SelectedMod);
            RefreshModGames();
        }
    }

    /// <summary>
    /// Asks for a mod to save something in: one of those loaded (a folder or a pk3), or another
    /// picked now; null when none is
    /// </summary>
    private async Task<string?> PickModToSaveIn(string title)
    {
        if (Dialogs == null)
            return null;
        var path = await Dialogs.PickModToSaveIn(title, Mods.Where(mod => ModFiles.WhyReadOnly(mod) == null).ToList());
        if (path != null && ModFiles.WhyReadOnly(path) is { } why)
        {
            Status = $"Not saved: {why}";
            return null;
        }
        return path;
    }

    /// <summary>
    /// Makes a new mod as a pk3: a stand-alone game (ready to play) or a mod of one of
    /// PFWolf's games. It's added to the mods, and the editor loads it as that game.
    /// </summary>
    [RelayCommand]
    private async Task NewModAsync()
    {
        if (Dialogs == null || await Dialogs.AskNewMod(BaseGameChoices, Path.Combine(GameFolder, "mods")) is not { } request)
            return;

        try
        {
            NewMod.Create(request, request.IsStandalone ? NewMod.ReadDefaultPalette(GameFolder) : null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = "Couldn't make the mod.";
            Problems.Add($"New mod {request.Path}: {e.Message}");
            return;
        }

        AddMod(request.Path);
        var game = request.IsStandalone ? request.GameId : request.BasePack!;
        SelectedGame = GameChoices.FirstOrDefault(choice => choice.Id.Equals(game, StringComparison.OrdinalIgnoreCase)) ?? GameChoices[0];
        SaveSettings();
        Status = $"Made {request.Path}";
        if (CanLoad)
        {
            await LoadAsync();
            if (!IsLoading && Content != null)
                Status = $"Made {request.Path}; saving puts what's changed in it (the last version kept as {Path.GetFileName(request.Path)}{ModFiles.BackupExtension})";
        }
    }

    /// <summary>Shows what's on the tile under the pointer</summary>
    public void Hover(int x, int y)
    {
        _hover = (x, y);
        _hover3D = null;
        HoverInfo = Tiles == null || x < 0
            ? ""
            : $"({x}, {y})   " + string.Join("   ·   ", Tiles.Describe(x, y));
    }

    /// <summary>The 3D camera where the player starts, facing the same way (else the level's middle)</summary>
    private void PlaceCameraAtStart()
    {
        if (Tiles is not { } tiles)
            return;
        for (int y = 0; y < tiles.Height; y++)
        {
            for (int x = 0; x < tiles.Width; x++)
            {
                if (tiles.PlayerStart(x, y) is { Deathmatch: false } start)
                {
                    Camera.PlaceOn(x, y, start.Angles);
                    return;
                }
            }
        }
        Camera.PlaceOn(tiles.Width / 2, tiles.Height / 2, 90);
    }

    /// <summary>
    /// Shows what the pointer is on in the 3D view: the surface, then the tile an edit there goes
    /// to as the map does (it's also where Play from here starts)
    /// </summary>
    public void Hover3D(SurfaceHit? hit, (int X, int Y) target)
    {
        if (hit is not { } surface || Tiles == null || target.X < 0)
        {
            Hover(-1, -1);
            return;
        }

        var (x, y) = target;
        _hover = (x, y);
        _hover3D = (hit, target);
        var what = surface.Ref.Kind switch
        {
            SurfaceKind.Wall when surface.Ref.Face == Face.Top => "top of the wall",
            SurfaceKind.Wall => $"{surface.Ref.Face.ToString().ToLowerInvariant()} face",
            SurfaceKind.Door => $"door, {surface.Ref.Face.ToString().ToLowerInvariant()} side",
            SurfaceKind.Floor => "floor",
            SurfaceKind.Ceiling => "ceiling",
            SurfaceKind.PlayerStart => "player start",
            _ => "thing",
        };
        if (surface.Texture.Source is TextureSource.Texture or TextureSource.Flat)
            what += $" ({surface.Texture.Name})";
        if (target != (surface.Ref.X, surface.Ref.Y))
            what += $", editing the tile in front";
        HoverInfo = $"3D: {what}   ({x}, {y})   " + string.Join("   ·   ", Tiles.Describe(x, y));
    }

    public void SaveSettings()
    {
        _settings.GameFolder = GameFolder;
        _settings.Game = SelectedGame.Id;
        _settings.Mods = Mods.ToList();
        _settings.Save();
    }
}
