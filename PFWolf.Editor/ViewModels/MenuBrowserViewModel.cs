using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Editor.Data;
using PFWolf.Editor.Rendering;
using PFWolf.Loaders;
using PFWolf.Menus;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.ViewModels;

/// <summary>A menu as the menu window lists it</summary>
public sealed class MenuListItem(MenuEntry entry, int problems, string packId)
{
    public MenuEntry Entry { get; } = entry;
    public string Name => Entry.Name;
    public int ProblemCount { get; } = problems;
    public bool HasProblems => ProblemCount > 0;

    /// <summary>Where it's from, in a word or two</summary>
    public string Where => Entry.IsModsOwn ? "a mod's own"
        : Entry.FromMod ? "changed by a mod"
        : Entry.IsPackOwn ? $"{packId}'s own"
        : "";

    /// <summary>Nothing in this game gets to it: shown faded</summary>
    public double Opacity => Entry.Reached ? 1 : 0.5;

    public string ToolTip => $"{Entry.Name}  ({string.Join(", then ", Entry.Layers.Select(layer => layer.Label))})"
        + (Entry.Reached ? "" : "\nNothing in this game opens it")
        + (Entry.Uses.Count > 0 ? "\n" + string.Join("\n", Entry.Uses) : "")
        + (HasProblems ? $"\n{ProblemCount} problem{(ProblemCount == 1 ? "" : "s")}" : "");

    public override string ToString() => Name;
}

/// <summary>
/// The menu window: every menudefs menu of the loaded game, drawn as the game draws it with what
/// the game's code adds made up, the files it's made from (to change in the Definitions window's
/// YAML tab, which shows it again as it's typed), what opens it, and what's wrong with it
/// </summary>
public sealed partial class MenuBrowserViewModel : ObservableObject, IDisposable
{
    private GameContent? _content;
    private List<MenuListItem> _all = [];
    private HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
    private MenuBuild? _build;
    // Each menu-items / components entry's line in the merged YAML (from 1), in the order the menu reads them
    private List<int> _itemLines = [], _componentLines = [];

    public MenuBrowserViewModel()
    {
        Preview.PartPicked += (_, part) => SelectPart(part);
        Preview.PartOpened += (_, part) => OpenPart(part);
    }

    public MenuPreviewModel Preview { get; } = new();

    public ObservableCollection<MenuListItem> Items { get; } = [];
    public ObservableCollection<MenuLayer> Layers { get; } = [];
    public ObservableCollection<MenuUse> Uses { get; } = [];
    public ObservableCollection<string> Problems { get; } = [];

    /// <summary>Which menus are listed: 0 all, 1 those the game gets to, 2 the pack's own, 3 the mods', 4 those with problems</summary>
    public static IReadOnlyList<string> ShowChoices { get; } = ["All", "The game gets to", "The pack's own", "Changed or added by mods", "With problems"];

    [ObservableProperty] private int _showIndex;
    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private string _countText = "";
    [ObservableProperty] private string _title = "Menus";
    [ObservableProperty] private string _status = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private MenuListItem? _selectedItem;

    /// <summary>The menu as its files merge, as YAML (to read: change it in its files)</summary>
    [ObservableProperty] private string _mergedYaml = "";

    /// <summary>What the menu is ("Wolf3D's main menu, the shared one") and how it's laid out</summary>
    [ObservableProperty] private string _details = "";

    /// <summary>What the game's code draws on it, which the preview makes up</summary>
    [ObservableProperty] private string _codeNote = "";

    public bool HasSelection => SelectedItem != null;
    public bool HasUses => Uses.Count > 0;
    public bool HasProblems => Problems.Count > 0;
    public bool HasCodeNote => CodeNote.Length > 0;

    /// <summary>Asks the editor to open a file in the Definitions window's YAML tab, to change it there</summary>
    public event EventHandler<MenuLayer>? EditFileRequested;

    /// <summary>Asks the editor to show a picture in the art window</summary>
    public event EventHandler<string>? PictureRequested;

    /// <summary>Asks the window to select lines of the merged YAML (from 1) and show them</summary>
    public event EventHandler<(int First, int Last)>? SelectYamlLines;

    /// <summary>Lists the menus of a newly loaded game, keeping the one picked if it's still there</summary>
    public void Load(GameContent? content)
    {
        _content = content;
        var picked = SelectedItem?.Name;
        Status = "";
        if (content == null)
        {
            _all = [];
            _names = new(StringComparer.OrdinalIgnoreCase);
            Title = "Menus";
        }
        else
        {
            var entries = MenuCatalog.Build(content);
            _names = new(entries.Select(entry => entry.Name), StringComparer.OrdinalIgnoreCase);
            _all = [.. entries.Select(entry => new MenuListItem(entry,
                MenuCatalog.Problems(content, MenuCatalog.Merge(content, entry), _names).Count, content.PackId))];
            Title = $"Menus - {content.Title}";
        }

        Refilter();
        SelectedItem = picked != null ? Items.FirstOrDefault(item => item.Name.Equals(picked, StringComparison.OrdinalIgnoreCase)) : null;
        SelectedItem ??= Items.FirstOrDefault(item => item.Name == "main-menu") ?? Items.FirstOrDefault();
        if (SelectedItem == null)
            Preview.Show(content, "", null);
    }

    /// <summary>The same game in new colors: the menu is drawn again</summary>
    public void Recolor() => Preview.Recolor();

    /// <summary>Shows a menu by name</summary>
    public bool Show(string name)
    {
        var found = _all.FirstOrDefault(item => item.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (found == null)
        {
            Status = $"There's no menu {name}";
            return false;
        }
        if (!Items.Contains(found))
        {
            Filter = "";
            ShowIndex = 0;
        }
        SelectedItem = found;
        return true;
    }

    partial void OnFilterChanged(string value) => Refilter();
    partial void OnShowIndexChanged(int value) => Refilter();

    private void Refilter()
    {
        var selected = SelectedItem;
        var words = Filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Items.Clear();
        foreach (var item in _all.Where(item => ShowIndex switch
                 {
                     1 => item.Entry.Reached,
                     2 => item.Entry.IsPackOwn,
                     3 => item.Entry.FromMod,
                     4 => item.HasProblems,
                     _ => true,
                 } && words.All(word => item.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                                        || item.Where.Contains(word, StringComparison.OrdinalIgnoreCase)
                                        || item.Entry.Layers.Any(layer => layer.Label.Contains(word, StringComparison.OrdinalIgnoreCase)))))
            Items.Add(item);
        int problems = _all.Count(item => item.HasProblems && item.Entry.Reached);
        CountText = (Items.Count == _all.Count ? $"{Items.Count} menus" : $"{Items.Count} of {_all.Count} menus")
                    + (problems > 0 ? $", {problems} the game gets to with problems" : "");
        SelectedItem = selected != null && Items.Contains(selected) ? selected : Items.FirstOrDefault();
    }

    partial void OnSelectedItemChanged(MenuListItem? value)
    {
        Layers.Clear();
        Uses.Clear();
        Problems.Clear();
        _build = null;
        if (value == null || _content == null)
        {
            MergedYaml = "";
            Details = "";
            CodeNote = "";
            Preview.Show(_content, "", null);
            NotifyLists();
            return;
        }

        var entry = value.Entry;
        foreach (var layer in entry.Layers)
            Layers.Add(layer);
        foreach (var use in entry.Uses)
            Uses.Add(use);

        _build = MenuCatalog.Merge(_content, entry);
        foreach (var problem in MenuCatalog.Problems(_content, _build, _names))
            Problems.Add(problem);
        MergedYaml = _build.Tree != null ? YamlTree.ToText(_build.Tree) : "";
        IndexYaml();

        var menu = _build.Menu;
        Details = (entry.IsPackOwn ? $"{_content.PackId}'s own (menudefs/{_content.PackId}/{entry.Name}.yaml), in place of the shared one"
                      : entry.IsModsOwn ? "A mod's own menu: a CP_Menu item opens it"
                      : "The shared menu (menudefs/), every pack's that hasn't its own")
                  + (entry.Reached ? "" : ". Nothing in this game opens it.")
                  + (menu == null ? "" : $"\n{Layout(menu)}");
        CodeNote = MenuSamples.CodeDrawn(entry.Name) ?? "";
        Preview.Show(_content, entry.Name, menu);
        NotifyLists();
    }

    private void NotifyLists()
    {
        OnPropertyChanged(nameof(HasUses));
        OnPropertyChanged(nameof(HasProblems));
        OnPropertyChanged(nameof(HasCodeNote));
    }

    private static string Layout(MenuMetadata menu)
    {
        var parts = new List<string> { $"Rows from {menu.Position.X}, {menu.Position.Y}" + (menu.Indent != 0 ? $", text {menu.Indent} further right" : "") };
        parts.Add($"{menu.RowHeight ?? 13} pixels apart");
        if (menu.ItemsSource != null)
            parts.Add($"made from {MenuNames.ItemsSources.GetValueOrDefault(menu.ItemsSource, menu.ItemsSource)}");
        if (menu.VisibleRows is > 0)
            parts.Add($"{menu.VisibleRows} shown at once");
        if (menu.FitItems != null)
            parts.Add($"fitted to the number of rows (laid out for {menu.FitItems.Rows})");
        if (menu.Cursor != null)
            parts.Add("a highlight bar for a cursor");
        if (!string.IsNullOrEmpty(menu.Font))
            parts.Add($"in {menu.Font}");
        if (!string.IsNullOrEmpty(menu.Music))
            parts.Add($"music {menu.Music}");
        return string.Join(", ", parts);
    }

    // Each entry's line in the merged YAML, for the ones the menu reads (an unknown type's left out, as the game leaves it out)
    private void IndexYaml()
    {
        _itemLines = [];
        _componentLines = [];
        if (MergedYaml.Length == 0)
            return;
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(MergedYaml));
            if (stream.Documents.FirstOrDefault()?.RootNode is not YamlMappingNode root)
                return;
            _componentLines = Lines(root, "components", ComponentTypes);
            _itemLines = Lines(root, "menu-items", ItemTypes);
        }
        catch (YamlDotNet.Core.YamlException)
        {
        }
    }

    private static readonly HashSet<string> ComponentTypes = new(["Background", "Window", "Fill", "Graphic", "Stripe", "Label"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ItemTypes = new(["MenuSwitcher", "ToggleMenuItem", "ChoiceMenuItem", "SliderMenuItem",
        "MusicMenuItem", "ControlMenuItem", "HeaderMenuItem", "BlankMenuItem"], StringComparer.OrdinalIgnoreCase);

    private static List<int> Lines(YamlMappingNode root, string key, HashSet<string> types)
    {
        var lines = new List<int>();
        if (!root.Children.TryGetValue(new YamlScalarNode(key), out var node) || node is not YamlSequenceNode list)
            return lines;
        foreach (var entry in list.Children.OfType<YamlMappingNode>())
            if (entry.Children.TryGetValue(new YamlScalarNode("type"), out var type) && type is YamlScalarNode { Value: { } name } && types.Contains(name))
                lines.Add((int)entry.Start.Line);
        return lines;
    }

    // A part picked on the picture: its lines in the merged YAML
    private void SelectPart(MenuPart part)
    {
        if (_build?.Menu is not { } menu || _content == null)
            return;
        int index = part.Kind switch
        {
            MenuPartKind.Component => part.Index,
            MenuPartKind.Row when menu.ItemsSource == null => ItemIndexOfRow(menu, part.Index),
            _ => -1,
        };
        var lines = part.Kind == MenuPartKind.Component ? _componentLines : _itemLines;
        if (index >= 0 && index < lines.Count)
        {
            int last = index + 1 < lines.Count ? lines[index + 1] - 1 : NextKeyLine(lines[index]);
            SelectYamlLines?.Invoke(this, (lines[index], last));
        }
    }

    // The menu-items entry a row comes from: the row-th in the running pack
    private int ItemIndexOfRow(MenuMetadata menu, int row)
    {
        int shown = -1;
        for (int i = 0; i < menu.MenuItems.Count; i++)
        {
            if (MenuCatalog.InPack(_content!, menu.MenuItems[i].GamePacks) && ++shown == row)
                return i;
        }
        return -1;
    }

    // The last line of the entry starting at this line: up to the next line at the top level (or the end)
    private int NextKeyLine(int start)
    {
        var lines = MergedYaml.Split('\n');
        int at = start;
        while (at < lines.Length && (lines[at].Length == 0 || char.IsWhiteSpace(lines[at][0]) || lines[at].StartsWith('-')))
            at++;
        return Math.Max(start, at);
    }

    // A part double-clicked: the menu an item opens, a picture in the art window
    private void OpenPart(MenuPart part)
    {
        if (_build?.Menu is not { } menu || _content == null)
            return;
        if (part.Kind == MenuPartKind.Row && menu.ItemsSource == null && ItemIndexOfRow(menu, part.Index) is >= 0 and var index
            && menu.MenuItems[index] is MenuSwitcher item)
        {
            if (item.Action == MenuNames.SubMenuAction && !string.IsNullOrWhiteSpace(item.Menu))
                Show(item.Menu);
            else if (item.Action != null && MenuNames.ActionMenus.TryGetValue(item.Action, out var menus) && menus.FirstOrDefault(_names.Contains) is { } opened)
                Show(opened);
            return;
        }
        if (part.Kind == MenuPartKind.Component && menu.Components[part.Index] is Graphic { Name.Length: > 0 } graphic)
            PictureRequested?.Invoke(this, graphic.Name);
        else if (part.Kind == MenuPartKind.Picture && part.Label.IndexOf('(') is >= 0 and var open)
            PictureRequested?.Invoke(this, part.Label[(open + 1)..].TrimEnd(')'));
    }

    /// <summary>Goes to what opens the menu: the menu with the item</summary>
    [RelayCommand]
    private void GoToUse(MenuUse? use)
    {
        if (use?.Menu != null)
            Show(use.Menu);
    }

    /// <summary>Opens one of the menu's files in the Definitions window's YAML tab, where it's changed (and shown as it's typed)</summary>
    [RelayCommand]
    private void EditLayer(MenuLayer? layer)
    {
        if (layer != null)
            EditFileRequested?.Invoke(this, layer);
    }

    public void Dispose() => Preview.Dispose();
}
