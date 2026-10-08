using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Editor.Data;
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

    public MainWindowViewModel(EditorSettings settings)
    {
        _settings = settings;
        _gameFolder = settings.GameFolder;
        _selectedGame = GameChoices.FirstOrDefault(choice => choice.Id.Equals(settings.Game, StringComparison.OrdinalIgnoreCase)) ?? GameChoices[0];
        foreach (var mod in settings.Mods)
            Mods.Add(mod);

        // The shared code's warnings (an asset that can't be read, say) go to the problems list
        WarningLog.Sink = message => Dispatcher.UIThread.Post(() => Problems.Add(message));
    }

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

    public ObservableCollection<MapEntry> Maps { get; } = [];

    [ObservableProperty]
    private MapEntry? _selectedMap;

    [ObservableProperty]
    private GameContent? _content;

    [ObservableProperty]
    private ArtCache? _art;

    [ObservableProperty]
    private MapTiles? _tiles;

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

    public bool CanLoad => !IsLoading && !string.IsNullOrWhiteSpace(GameFolder);

    /// <summary>Loads the game in the game folder, with the mods, and shows its first level</summary>
    [RelayCommand(CanExecute = nameof(CanLoad))]
    public async Task LoadAsync()
    {
        IsLoading = true;
        Problems.Clear();
        Status = $"Loading {GameFolder}...";

        string folder = GameFolder, game = SelectedGame.Id;
        var mods = Mods.ToList();
        try
        {
            var content = await Task.Run(() => GameContent.Load(folder, game, mods));

            var oldArt = Art;
            Tiles = null;
            Content = content;
            Art = new ArtCache(content);
            oldArt?.Dispose();

            Maps.Clear();
            foreach (var map in content.Maps)
                Maps.Add(map);
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

    partial void OnSelectedMapChanged(MapEntry? value)
    {
        Tiles = value != null && Content?.FindMap(value.Name) is { } map ? new MapTiles(Content, map) : null;
        HoverInfo = "";
    }

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
