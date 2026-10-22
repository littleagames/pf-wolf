using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;

namespace PFWolf.Editor.ViewModels;

// Blake Stone's level codes: the Level codes panel, editing a tile's code, and picking a link's target
public sealed partial class MainWindowViewModel
{
    /// <summary>The map-info codes and links on the level, for the Level codes panel</summary>
    public ObservableCollection<LevelCode> LevelCodeList { get; } = [];

    [ObservableProperty] private LevelCode? _selectedLevelCode;
    [ObservableProperty] private bool _levelCodesExpanded;

    /// <summary>Whether the game's mapdefs have map-info codes (Blake Stone's), so the panel shows</summary>
    public bool HasLevelCodes => Content?.MapDefs.MapInfo.Count > 0;

    /// <summary>Whether the mapdefs give ceiling and floor colors / flats codes, for the panel's buttons</summary>
    public bool HasColorCode => HasMapInfoKind(MapInfoCodes.CeilingFloorColors);
    public bool HasFlatsCode => HasMapInfoKind(MapInfoCodes.CeilingFloorFlats);

    private bool HasMapInfoKind(string kind)
        => Content?.MapDefs.MapInfo.Values.Any(value => value.Equals(kind, StringComparison.OrdinalIgnoreCase)) == true;

    partial void OnContentChanged(GameContent? value)
    {
        OnPropertyChanged(nameof(HasLevelCodes));
        OnPropertyChanged(nameof(HasColorCode));
        OnPropertyChanged(nameof(HasFlatsCode));
    }

    partial void OnTilesChanged(MapTiles? value)
    {
        Controller.Pairs = value;
        Controller.PickTile = null;
        RefreshLevelCodes();
        EditCodeCommand.NotifyCanExecuteChanged();
        DeleteCodeCommand.NotifyCanExecuteChanged();
        SetLevelColorsCommand.NotifyCanExecuteChanged();
        SetLevelFlatsCommand.NotifyCanExecuteChanged();
    }

    partial void OnLevelCodesExpandedChanged(bool value) => RefreshLevelCodes();

    partial void OnSelectedLevelCodeChanged(LevelCode? value)
    {
        if (value == null)
            return;
        Controller.SelectTile(value.X, value.Y);
        GoToTile?.Invoke(this, (value.X, value.Y));
        Hover(value.X, value.Y);
    }

    /// <summary>Lists the level's codes again (while the panel is open: it walks the whole level)</summary>
    private void RefreshLevelCodes()
    {
        if (!LevelCodesExpanded && LevelCodeList.Count == 0)
            return;
        var selected = SelectedLevelCode;
        LevelCodeList.Clear();
        if (Tiles is { } tiles && LevelCodesExpanded)
        {
            foreach (var code in Editing.LevelCodes.List(tiles))
                LevelCodeList.Add(code);
        }
        if (selected != null)
            _selectedLevelCode = LevelCodeList.FirstOrDefault(code => code.X == selected.X && code.Y == selected.Y);
        OnPropertyChanged(nameof(SelectedLevelCode));
    }

    /// <summary>The tile a code command works on: the code picked in the panel, else the tile under the pointer, else the selection's</summary>
    private (int X, int Y)? CodeTile()
        => SelectedLevelCode is { } code && LevelCodesExpanded ? (code.X, code.Y)
            : _hover.X >= 0 ? _hover
            : Controller.Selection is { } selection ? (selection.Left, selection.Top)
            : null;

    /// <summary>Edits the code or link on the tile (Ctrl+E): its value, floor, hint or target</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task EditCode()
    {
        if (Tiles is not { } tiles || CodeTile() is not { } tile)
            return;
        if (Editing.LevelCodes.EditForm(tiles, tile.X, tile.Y) is not { } form)
        {
            Status = $"({tile.X}, {tile.Y}) holds no map-info code or link to edit";
            return;
        }
        await RunCodeForm(form, "Edit code");
    }

    /// <summary>Takes the code or link off the tile, with its value</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void DeleteCode()
    {
        if (Tiles is not { } tiles || Document is not { } document || CodeTile() is not { } tile)
            return;
        if (Editing.LevelCodes.EditForm(tiles, tile.X, tile.Y) == null)
        {
            Status = $"({tile.X}, {tile.Y}) holds no map-info code or link";
            return;
        }
        Editing.LevelCodes.Delete(document, tiles, tile.X, tile.Y);
        Status = $"Took the code off ({tile.X}, {tile.Y})";
        RefreshLevelCodes();
    }

    /// <summary>Sets the level's ceiling and floor colors code (where it has one, else in the top row)</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private Task SetLevelColors() => SetLevelWide(MapInfoCodes.CeilingFloorColors);

    /// <summary>Sets the level's ceiling and floor flats code (where it has one, else in the top row)</summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private Task SetLevelFlats() => SetLevelWide(MapInfoCodes.CeilingFloorFlats);

    private async Task SetLevelWide(string kind)
    {
        if (Tiles is not { } tiles || LevelDefs is not { } defs)
            return;
        var high = defs.MapInfo.FirstOrDefault(code => code.Value.Equals(kind, StringComparison.OrdinalIgnoreCase)).Key;
        if (Editing.LevelCodes.PlaceFor(tiles, kind, 0, 0) is not { } place)
        {
            Status = "There's no free pair of tiles in the level's top row for the code";
            return;
        }
        var form = Editing.LevelCodes.EditForm(tiles, place.X, place.Y) ?? Editing.LevelCodes.NewForm(tiles, high, kind, place.X, place.Y);
        await RunCodeForm(form, LevelCodes.KindLabel(kind));
    }

    /// <summary>Asks the form's questions and writes the answers, or waits for the target to be clicked on the map</summary>
    private async Task RunCodeForm(CodeForm form, string description)
    {
        if (Dialogs is not { } dialogs || Document is not { } document)
            return;
        if (await dialogs.AskCode(form) is not { } answer)
            return;

        if (!answer.PickOnMap)
        {
            Editing.LevelCodes.Apply(document, form, answer.Values, description);
            Status = $"{form.Title}: done";
            RefreshLevelCodes();
            return;
        }

        Status = $"{form.Title}: click the tile it goes to (Esc: leave it as it is)";
        Controller.PickTile = (x, y) =>
        {
            var values = answer.Values.ToArray();
            values[^2] = x;
            values[^1] = y;
            Editing.LevelCodes.Apply(document, form, values, description);
            Status = $"{form.Title}: now ({x}, {y})";
            RefreshLevelCodes();
        };
    }

    /// <summary>Stops waiting for a link's target to be clicked</summary>
    private bool CancelTilePick()
    {
        if (Controller.PickTile == null)
            return false;
        Controller.PickTile = null;
        Status = "Left the code as it was";
        return true;
    }

    /// <summary>
    /// A tile code from the palette (a hint, a link, a teleporter's floor): its low byte and value
    /// asked for once, then the tools put it down with its value in the tile after it
    /// </summary>
    private async Task PickCodeEntry(PaletteEntry entry)
    {
        Controller.PairValue = null;
        if (Tiles is not { } tiles || Dialogs is not { } dialogs || tiles.MapInfoKind(entry.Value) is not { } kind)
            return;

        // The answers are worked out on a tile in from the edge, then put down wherever the tools go
        var form = Editing.LevelCodes.NewForm(tiles, entry.Value >> 8, kind, 0, 0) with { Title = LevelCodes.KindLabel(kind), PicksTile = false };
        if (await dialogs.AskCode(form) is not { } answer)
            return;
        var writes = form.Writes(answer.Values);
        Controller.Value = writes[0].Value;
        Controller.PairValue = writes.Count > 1 ? writes[1].Value : null;
        Status = $"Paint puts down {LevelCodes.KindLabel(kind)} 0x{writes[0].Value:X4}"
                 + (Controller.PairValue is { } pair ? $" with 0x{pair:X4} in the tile after it" : "");
    }
}
