using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Constants;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;
using PFWolf.Exceptions;

namespace PFWolf.Editor.ViewModels;

/// <summary>A game the editor can open: a game pack id, or empty to pick by the data files</summary>
public sealed record GameChoice(string Id, string Label)
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

    public MainWindowViewModel(EditorSettings settings)
    {
        _settings = settings;
        _gameFolder = settings.GameFolder;
        _selectedGame = GameChoices.FirstOrDefault(choice => choice.Id.Equals(settings.Game, StringComparison.OrdinalIgnoreCase)) ?? GameChoices[0];
        _selectedTool = Tools[0];
        _selectedPlane = Planes[0];
        foreach (var mod in settings.Mods)
            Mods.Add(mod);

        Controller.ValuePicked += (_, value) => SelectValue(value);

        // The shared code's warnings (an asset that can't be read, say) go to the problems list
        WarningLog.Sink = message => Dispatcher.UIThread.Post(() => Problems.Add(message));
    }

    /// <summary>Answers the editor's questions; the window sets it</summary>
    public IEditorDialogs? Dialogs { get; set; }

    public ToolController Controller { get; } = new();

    /// <summary>Wolf3D and Spear for now; Blake Stone's games come later</summary>
    public IReadOnlyList<GameChoice> GameChoices { get; } =
    [
        new("", "By the data files"),
        new("wolf3d", "Wolfenstein 3D"),
        new("wolf3d-apogee", "Wolfenstein 3D (Apogee)"),
        new("wolf3d-shareware", "Wolfenstein 3D (shareware)"),
        new("spear", "Spear of Destiny"),
        new("spear-demo", "Spear of Destiny (demo)"),
    ];

    public IReadOnlyList<ToolOption> Tools { get; } =
    [
        new(EditTool.Paint, "Paint", "P"),
        new(EditTool.Line, "Line", "L"),
        new(EditTool.Rectangle, "Rectangle", "R"),
        new(EditTool.Fill, "Fill", "F"),
        new(EditTool.Pick, "Pick", "I"),
        new(EditTool.Select, "Select", "S"),
    ];

    public IReadOnlyList<PlaneOption> Planes { get; } =
    [
        new(0, "Walls, doors and floors (plane 0)"),
        new(1, "Things (plane 1)"),
        new(MapConstants.FLATPLANE, "Flats (plane 2)"),
        new(MapConstants.HEIGHTPLANE, "Heights (plane 3)"),
        new(MapConstants.TAGPLANE, "Tags (plane 4)"),
        new(MapConstants.ZONEPLANE, "Light zones (plane 5)"),
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
    [NotifyCanExecuteChangedFor(nameof(NewMapCommand))]
    private GameContent? _content;

    [ObservableProperty]
    private ArtCache? _art;

    [ObservableProperty]
    private MapTiles? _tiles;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyCanExecuteChangedFor(nameof(UndoCommand), nameof(RedoCommand), nameof(SaveCommand), nameof(SaveAsCommand))]
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

    // Which layers the map shows
    [ObservableProperty] private bool _showWalls = true;
    [ObservableProperty] private bool _showThings = true;
    [ObservableProperty] private bool _showFlats;
    [ObservableProperty] private bool _showHeights;
    [ObservableProperty] private bool _showTags;
    [ObservableProperty] private bool _showZones;
    [ObservableProperty] private bool _showGrid = true;

    // The tools
    [ObservableProperty] private ToolOption _selectedTool;
    [ObservableProperty] private bool _outline;
    [ObservableProperty] private bool _copyAllPlanes = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPalette), nameof(IsFlatsPlane), nameof(HasNextUnused), nameof(PlaneHint))]
    private PlaneOption _selectedPlane;

    public bool HasPalette => PaletteEntries.HasPalette(SelectedPlane.Plane);

    public ObservableCollection<PaletteItem> PaletteItems { get; } = [];

    [ObservableProperty] private PaletteItem? _selectedPaletteItem;
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

    public bool IsFlatsPlane => SelectedPlane.Plane == MapConstants.FLATPLANE;
    public bool HasNextUnused => SelectedPlane.Plane is MapConstants.TAGPLANE or MapConstants.ZONEPLANE;

    /// <summary>What a plane's number means</summary>
    public string PlaneHint => SelectedPlane.Plane switch
    {
        MapConstants.FLATPLANE => "Ceiling × 256 + floor: the mapdefs flats indices. 0 is the level's default floor and ceiling.",
        MapConstants.HEIGHTPLANE => $"The wall's height in stories, 1 to 8. 0 uses the level's height ({LevelWallHeight}). Taller than 1 on open floor makes an arch.",
        MapConstants.TAGPLANE => "A switch acts on the doors, walls and things that share its tag. 0 is no tag.",
        MapConstants.ZONEPLANE => "The level's light zones (Level properties); 0 is lit as the level.",
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
        Problems.Clear();
        Status = $"Loading {GameFolder}...";

        string folder = GameFolder, game = SelectedGame.Id;
        var mods = Mods.ToList();
        try
        {
            var content = await Task.Run(() => GameContent.Load(folder, game, mods));

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
            BuildPalette();
            SelectedMap = Maps.FirstOrDefault();

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
        }
    }

    partial void OnSelectedMapChanged(MapItem? value)
    {
        Document = value != null ? OpenDocument(value.Name) : null;
        Controller.Document = Document;
        var document = Document;
        Tiles = document != null && Content != null ? new MapTiles(Content, document.Map, () => document.Properties) : null;
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
        document = new MapDocument(name, map, properties: info != null ? MapProperties.From(info) : null) { SaveFolder = Content.ModFolderOf(name) };
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
        if (_hover.X >= 0)
            Hover(_hover.X, _hover.Y);
    }

    //
    // Tools and palette
    //

    partial void OnSelectedToolChanged(ToolOption value) => Controller.Tool = value.Tool;
    partial void OnOutlineChanged(bool value) => Controller.Outline = value;
    partial void OnCopyAllPlanesChanged(bool value) => Controller.CopyAllPlanes = value;

    partial void OnSelectedPlaneChanged(PlaneOption value)
    {
        Controller.CancelGesture();
        Controller.Plane = value.Plane;

        // Show the plane being edited
        switch (value.Plane)
        {
            case 0: ShowWalls = true; break;
            case 1: ShowThings = true; break;
            case MapConstants.FLATPLANE: ShowFlats = true; break;
            case MapConstants.HEIGHTPLANE: ShowHeights = true; break;
            case MapConstants.TAGPLANE: ShowTags = true; break;
            case MapConstants.ZONEPLANE: ShowZones = true; break;
        }
        BuildPalette();
    }

    partial void OnSelectedPaletteItemChanged(PaletteItem? value)
    {
        if (value == null)
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
            _thingVariants = PaletteEntries.ThingVariants(content.MapDefs);
            if (plane == MapConstants.FLATPLANE)
            {
                _syncing = true;
                FillFlatOptions(FlatFloorOptions, content.MapDefs.Flats.Floor);
                FillFlatOptions(FlatCeilingOptions, content.MapDefs.Flats.Ceiling);
                _syncing = false;
            }
            _allPaletteItems = PaletteEntries.ForPlane(content.MapDefs, plane)
                .Select(entry => new PaletteItem(entry,
                    entry.Texture != null ? Art?.Texture(entry.Texture) : entry.ThingClass != null ? Art?.ThingSprite(entry.ThingClass) : null))
                .ToList();
        }
        else
            _allPaletteItems = [];

        FilterPalette();
        if (HasPalette)
            SelectedPaletteItem = PaletteItems.FirstOrDefault(item => item.Entry.Group == "Walls") ?? PaletteItems.FirstOrDefault();
        else
            OnPlaneValueChanged(PlaneValue);
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

    /// <summary>Saves the level to its mod folder, asking for one the first time</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task SaveAsync()
    {
        if (Document != null)
            await SaveDocument(Document, Document.SaveFolder);
    }

    /// <summary>Saves the level to a mod folder picked now</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task SaveAsAsync()
    {
        if (Document != null)
            await SaveDocument(Document, null);
    }

    private async Task<bool> SaveDocument(MapDocument document, string? folder)
    {
        folder ??= Dialogs == null ? null : await Dialogs.PickModFolder($"A mod folder to save {document.AssetName.ToUpperInvariant()} in (it goes in its maps folder)");
        if (folder == null)
            return false;

        Controller.CancelGesture();
        try
        {
            var path = MapFiles.Save(document, folder);
            Status = $"Saved {path}";

            // The game only sees the level with the mod loaded
            if (!Mods.Any(mod => Path.GetFullPath(mod).TrimEnd('\\', '/').Equals(Path.GetFullPath(folder).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)))
            {
                Mods.Add(folder);
                SaveSettings();
                Status += "; its folder is in the mods now, so the next load reads it";
            }
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

        var name = await Dialogs.AskText("New level", "Level name (it's saved as maps/NAME.wad):", NextFreeMapName(),
            text => MapFiles.CheckName(text) ?? (_documents.ContainsKey(text) || content.HasMap(text) ? $"There's already a level called {text}." : null));
        if (name == null)
            return;

        var wall = (ushort)(content.MapDefs.Walls.Keys.Where(id => id is > 0 and <= MapConstants.MAXWALLID).DefaultIfEmpty(1).Min());
        var map = MapFiles.NewMap(name.ToUpperInvariant(), wall, PaletteEntries.EraseValue(content.MapDefs, 0));
        // Unsaved until it's written somewhere
        var document = new MapDocument(name.ToUpperInvariant(), map, isNew: true);
        document.SetProperties(new MapProperties { Name = map.Name });
        document.Changed += OnDocumentChanged;
        _documents[document.AssetName] = document;

        var item = new MapItem(document.AssetName, map.Name) { IsDirty = true };
        Maps.Add(item);
        SelectedMap = item;
        Status = $"New level {map.Name}: saving it into a mod folder lists it in the mod's game-info too (warp there with the map command)";
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
        if (dirty.Count == 0 || Dialogs == null)
            return true;

        var names = string.Join(", ", dirty.Select(document => document.AssetName.ToUpperInvariant()));
        switch (await Dialogs.AskUnsaved($"{what} {names}. Save first?"))
        {
            case UnsavedChoice.Save:
                foreach (var document in dirty)
                {
                    if (!await SaveDocument(document, document.SaveFolder))
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
            content.MusicNames, Maps.Select(map => map.Name.ToUpperInvariant()).ToList());
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
    // Mods, hover, settings
    //

    public void AddMod(string path)
    {
        if (!Mods.Contains(path, StringComparer.OrdinalIgnoreCase))
            Mods.Add(path);
    }

    private bool CanRemoveMod => SelectedMod != null;

    [RelayCommand(CanExecute = nameof(CanRemoveMod))]
    private void RemoveMod()
    {
        if (SelectedMod != null)
            Mods.Remove(SelectedMod);
    }

    /// <summary>Shows what's on the tile under the pointer</summary>
    public void Hover(int x, int y)
    {
        _hover = (x, y);
        HoverInfo = Tiles == null || x < 0
            ? ""
            : $"({x}, {y})   " + string.Join("   ·   ", Tiles.Describe(x, y));
    }

    public void SaveSettings()
    {
        _settings.GameFolder = GameFolder;
        _settings.Game = SelectedGame.Id;
        _settings.Mods = Mods.ToList();
        _settings.Save();
    }
}
