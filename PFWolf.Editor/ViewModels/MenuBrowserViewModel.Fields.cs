using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Definitions;
using PFWolf.Editor.Data;
using PFWolf.Editor.Definitions;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;
using PFWolf.Loaders;
using PFWolf.Menus;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// The menu window's fields: a menu's menudefs keys, components and rows edited in the
/// Definitions window's rows, drawn in the preview as they change, undone and redone, saved into
/// a mod as just what it changes (Editing/MenuSaver); new menus
/// </summary>
public sealed partial class MenuBrowserViewModel
{
    /// <summary>A menu being edited: its fields, what it was opened as (menudefs' form), and its steps</summary>
    private sealed class MenuEdit(FieldNode root, YamlMappingNode? original, YamlMappingNode reference)
    {
        public FieldNode Root { get; set; } = root;

        /// <summary>The menu as its files make it; null for a new menu not saved yet</summary>
        public YamlMappingNode? Original { get; } = original;

        /// <summary>What it started as: params keep this one's order</summary>
        public YamlMappingNode Reference { get; } = reference;

        public List<YamlNode?> Undo { get; } = [];
        public List<YamlNode?> Redo { get; } = [];
        public YamlNode? Last { get; set; }
        public DateTime LastStep { get; set; }
    }

    private static readonly FieldSchema Schema = MenuSchema.Build();

    // Menus opened for editing, by name, kept until saved or reverted
    private readonly Dictionary<string, MenuEdit> _edits = new(StringComparer.OrdinalIgnoreCase);
    // New menus not saved yet, by name: what they started as
    private readonly Dictionary<string, YamlMappingNode> _drafts = new(StringComparer.OrdinalIgnoreCase);
    private DefinitionSuggestions _suggestions = new(null);
    private FieldContext? _context;
    private bool _rebuilding;

    /// <summary>The picked menu's fields</summary>
    public ObservableCollection<FieldNode> FieldRows { get; } = [];

    /// <summary>The field picked (a part picked on the picture picks its entry)</summary>
    [ObservableProperty] private FieldNode? _selectedField;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveMenuCommand), nameof(RevertMenuCommand))]
    private bool _isMenuChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFieldError))]
    private string _fieldError = "";

    public bool HasFieldError => FieldError.Length > 0;

    /// <summary>Asks for a line of text (title, prompt, initial, check); null when cancelled</summary>
    public Func<string, string, string, Func<string, string?>, Task<string?>>? AskText { get; set; }

    /// <summary>Asks for a mod (a folder or a pk3) to save in; null when none is picked</summary>
    public Func<string, Task<string?>>? PickMod { get; set; }

    /// <summary>Asks a yes-or-no question (title, message, the yes button's label)</summary>
    public Func<string, string, string, Task<bool>>? Confirm { get; set; }

    /// <summary>Adds a mod saved into to the editor's mods (so the game loads it)</summary>
    public Action<string>? AddMod { get; set; }

    /// <summary>Starts the game on a menu (its name), as saved: false, with why in Status, when it can't</summary>
    public Func<string, bool>? StartGameOn { get; set; }

    /// <summary>A menu went into a mod (the editor loads the game again)</summary>
    public event EventHandler<string>? Saved;

    /// <summary>The menus changed here and not saved, as they're named to a person ("menu options")</summary>
    public IReadOnlyList<string> UnsavedNames => [.. _edits.Where(edit => Changed(edit.Value)).Select(edit => $"menu {edit.Key}")];

    private static YamlMappingNode? Menudefs(MenuEdit edit)
        => edit.Root.Build([]) is YamlMappingNode built ? MenuEditing.FromEditable(built, edit.Reference) : null;

    private static bool Changed(MenuEdit edit) => edit.Original == null || !YamlNodes.Equal(Menudefs(edit), edit.Original);

    // Read again as the game loads: what the fields can offer; saved or unchanged menus open again from the files
    private void LoadFields(GameContent? content)
    {
        _suggestions = new MenuSuggestions(content, () => _names);
        _context = new FieldContext
        {
            Suggestions = hint => _suggestions.For(hint),
            Changed = OnFieldsChanged,
            AskKey = (title, prompt, check) => AskText?.Invoke(title, prompt, "", check) ?? Task.FromResult<string?>(null),
        };
        foreach (var name in _edits.Where(edit => !Changed(edit.Value)).Select(edit => edit.Key).ToList())
            _edits.Remove(name);
        foreach (var name in _drafts.Keys.Where(name => _names.Contains(name)).ToList())
            _drafts.Remove(name);
    }

    // The fields of the menu picked, opened (or as left) for editing
    private void OpenFields(MenuListItem? item)
    {
        FieldRows.Clear();
        FieldError = "";
        IsMenuChanged = false;
        SelectedField = null;
        if (item == null || _content == null || _context == null)
            return;

        if (!_edits.TryGetValue(item.Name, out var edit))
        {
            var original = _drafts.TryGetValue(item.Name, out var draft) ? null : _build?.Tree;
            var start = original ?? draft ?? new YamlMappingNode();
            edit = new MenuEdit(NewRoot(item.Entry, start), original, start) { Last = MenuEditing.ToEditable(start) };
            _edits[item.Name] = edit;
        }
        foreach (var row in edit.Root.Children)
            FieldRows.Add(row);
        UpdateFields(edit);
    }

    // The fields of a menu (menudefs' form), each component's params only its type's (unless set)
    private FieldNode NewRoot(MenuEntry entry, YamlMappingNode menu)
    {
        var baseLayers = entry.Layers.Where(layer => layer.IsBase).ToList();
        var baseTree = baseLayers.Count > 0 && _content != null ? MenuCatalog.Merge(_content, baseLayers).Tree : null;
        var root = new FieldNode(Schema, entry.Name, MenuEditing.ToEditable(menu), baseTree == null ? null : MenuEditing.ToEditable(baseTree),
            null, _context!, isEntry: true);
        root.EnsureChildren();
        HideParams(root);
        return root;
    }

    // Each component's params: the ones its type doesn't read left out, unless they're set
    private static void HideParams(FieldNode root)
    {
        var components = root.Children.FirstOrDefault(child => child.Key == "components");
        if (components == null)
            return;
        components.EnsureChildren();
        foreach (var component in components.Children)
        {
            component.EnsureChildren();
            var type = component.Children.FirstOrDefault(child => child.Key == "type")?.Text;
            component.Children.FirstOrDefault(child => child.Key == "params")?.HideUnset(field => !MenuSchema.ParamAppliesTo(field.Key, type));
        }
    }

    // The components' types, to see when one changes (its params are shown again for it)
    private static string Types(YamlNode? editable)
        => YamlNodes.Get(editable, ["components"]) is YamlSequenceNode list
            ? string.Join(",", list.Children.Select(component => (YamlNodes.Get(component, ["type"]) as YamlScalarNode)?.Value ?? ""))
            : "";

    private void OnFieldsChanged()
    {
        if (_rebuilding || SelectedItem is not { } item || !_edits.TryGetValue(item.Name, out var edit))
            return;

        var built = edit.Root.Build([]);
        if (!YamlNodes.Equal(built, edit.Last))
        {
            // Typing in a field is one step, not one a letter
            if (edit.Undo.Count == 0 || DateTime.UtcNow - edit.LastStep > TimeSpan.FromSeconds(1))
                edit.Undo.Add(edit.Last == null ? null : YamlNodes.Clone(edit.Last));
            edit.Redo.Clear();
            edit.LastStep = DateTime.UtcNow;
            bool retyped = Types(built) != Types(edit.Last);
            edit.Last = built == null ? null : YamlNodes.Clone(built);
            if (retyped)
            {
                Later(() => ShowFields(edit, built));
                return;
            }
        }
        UpdateFields(edit);
    }

    // Runs after the field that's changing has finished (rows can't be swapped under it)
    private static void Later(Action action)
    {
        if (Avalonia.Application.Current != null)
            Dispatcher.UIThread.Post(action);
        else
            action();
    }

    // The fields made again from an editable value (an undo, a redo, a component's new type)
    private void ShowFields(MenuEdit edit, YamlNode? editable)
    {
        if (SelectedItem is not { } item)
            return;
        _rebuilding = true;
        try
        {
            var menu = editable is YamlMappingNode mapping ? MenuEditing.FromEditable(mapping, edit.Reference) : new YamlMappingNode();
            edit.Root = NewRoot(item.Entry, menu);
            edit.Last = editable == null ? null : YamlNodes.Clone(editable);
            FieldRows.Clear();
            foreach (var row in edit.Root.Children)
                FieldRows.Add(row);
        }
        finally
        {
            _rebuilding = false;
        }
        UpdateFields(edit);
    }

    // What's changed, what's wrong, and the preview drawn with the fields as they are
    private void UpdateFields(MenuEdit edit)
    {
        if (_content == null || SelectedItem is not { } item)
            return;
        var errors = new List<string>();
        var built = edit.Root.Build(errors) as YamlMappingNode;
        IsMenuChanged = Changed(edit);
        item.HasEdits = IsMenuChanged;
        UndoMenuCommand.NotifyCanExecuteChanged();
        RedoMenuCommand.NotifyCanExecuteChanged();

        var tree = built == null ? new YamlMappingNode() : MenuEditing.FromEditable(built, edit.Reference);
        var warnings = new List<string>();
        MenuAsset? asset = null;
        MenuMetadata? menu = null;
        try
        {
            asset = YamlDataEntryLoader.Deserialize<MenuAsset>(YamlTree.ToText(tree));
            menu = MenuMetadata.BuildFromAsset(asset, warnings.Add);
        }
        catch (Exception e) when (e is YamlDotNet.Core.YamlException or InvalidOperationException or FormatException or ArgumentException)
        {
            errors.Add($"It doesn't read as a menu: {(e is YamlDotNet.Core.YamlException yaml ? DefinitionKinds.Describe(yaml) : e.Message)}");
        }

        if (IsMenuChanged || edit.Original == null)
        {
            // The edited menu: its problems, its YAML, its picture
            _build = new MenuBuild(tree, asset, menu, null, warnings);
            Problems.Clear();
            foreach (var problem in MenuCatalog.Problems(_content, _build, _names))
                Problems.Add(problem);
            MergedYaml = YamlTree.ToText(tree);
            IndexYaml();
            OnPropertyChanged(nameof(HasProblems));
        }
        FieldError = string.Join("\n", errors);
        LoadGameMenu(item);
        ShowInPreview(item.Name, menu);
    }

    /// <summary>Picks the field of a part picked on the picture (a component's, a row's entry), opening the way to it</summary>
    private void PickField(MenuPart part)
    {
        if (_build?.Menu is not { } menu || SelectedItem == null || !_edits.TryGetValue(SelectedItem.Name, out var edit))
            return;
        var (key, index) = part.Kind switch
        {
            MenuPartKind.Component => ("components", part.Index),
            MenuPartKind.Row when menu.ItemsSource == null => ("menu-items", ItemIndexOfRow(menu, part.Index)),
            _ => ("", -1),
        };
        if (index < 0 || edit.Root.Children.FirstOrDefault(child => child.Key == key) is not { } list)
            return;

        // The menu's index counts the entries it reads: their place among all of the list's
        var types = key == "components" ? ComponentTypes : ItemTypes;
        list.EnsureChildren();
        var read = list.Children.Where(child => types.Contains(child.Children.FirstOrDefault(field => field.Key == "type")?.Text ?? "")).ToList();
        if (index >= read.Count)
            return;
        list.IsExpanded = true;
        read[index].IsExpanded = true;
        SelectedField = read[index];
    }

    private bool CanUndoMenu => SelectedItem != null && _edits.TryGetValue(SelectedItem.Name, out var edit) && edit.Undo.Count > 0;
    private bool CanRedoMenu => SelectedItem != null && _edits.TryGetValue(SelectedItem.Name, out var edit) && edit.Redo.Count > 0;

    [RelayCommand(CanExecute = nameof(CanUndoMenu))]
    private void UndoMenu()
    {
        if (SelectedItem == null || !_edits.TryGetValue(SelectedItem.Name, out var edit) || edit.Undo.Count == 0)
            return;
        edit.Redo.Add(edit.Root.Build([]));
        var value = edit.Undo[^1];
        edit.Undo.RemoveAt(edit.Undo.Count - 1);
        edit.LastStep = DateTime.MinValue;
        ShowFields(edit, value);
        Status = "Undone";
    }

    [RelayCommand(CanExecute = nameof(CanRedoMenu))]
    private void RedoMenu()
    {
        if (SelectedItem == null || !_edits.TryGetValue(SelectedItem.Name, out var edit) || edit.Redo.Count == 0)
            return;
        edit.Undo.Add(edit.Root.Build([]));
        var value = edit.Redo[^1];
        edit.Redo.RemoveAt(edit.Redo.Count - 1);
        edit.LastStep = DateTime.MinValue;
        ShowFields(edit, value);
        Status = "Redone";
    }

    /// <summary>Back to the menu as its files make it</summary>
    [RelayCommand(CanExecute = nameof(IsMenuChanged))]
    private void RevertMenu()
    {
        if (SelectedItem is not { } item)
            return;
        _edits.Remove(item.Name);
        item.HasEdits = false;
        if (_drafts.ContainsKey(item.Name))
            _edits.Remove(item.Name);
        OnSelectedItemChanged(item);
        Status = $"{item.Name} is as {(item.IsDraft ? "it started" : "its files make it")}";
    }

    /// <summary>Saves the picked menu into a mod: the last mod with its own file of it, else one picked</summary>
    [RelayCommand(CanExecute = nameof(IsMenuChanged))]
    private async Task SaveMenuAsync()
    {
        if (SelectedItem is { } item)
            await SaveMenuAsync(item.Name);
    }

    /// <summary>Saves every menu changed here; false (with what's wrong in Status) when one isn't</summary>
    public async Task<bool> SaveAllAsync()
    {
        foreach (var name in _edits.Where(edit => Changed(edit.Value)).Select(edit => edit.Key).ToList())
            if (!await SaveMenuAsync(name, reload: false))
                return false;
        return true;
    }

    private async Task<bool> SaveMenuAsync(string name, bool reload = true)
    {
        if (_content is not { } content || !_edits.TryGetValue(name, out var edit))
            return false;
        var errors = new List<string>();
        if (edit.Root.Build(errors) is not YamlMappingNode built || errors.Count > 0)
        {
            Status = $"{name} isn't saved: {string.Join(" ", errors)}";
            return false;
        }
        var menu = MenuEditing.FromEditable(built, edit.Reference);
        var layers = _all.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Entry.Layers ?? [];
        var mod = layers.LastOrDefault(layer => !layer.IsBase)?.ModPath
                  ?? (PickMod == null ? null : await PickMod($"Save the menu {name} into (pfwolf.pk3 is read-only)"));
        if (mod == null)
            return false;

        try
        {
            var write = MenuSaver.Plan(content, name, layers, menu, mod);
            // A list written whole stops taking in pfwolf.pk3's later changes to it: asked first
            if (Confirm != null && write.Notes.Any(note => note.Contains("!replace"))
                && !await Confirm($"Save {name}", string.Join("\n\n", write.Notes) + "\n\nSave it like that?", "Save"))
            {
                Status = $"{name} isn't saved: give the entries you changed ids, then save";
                return false;
            }
            var place = MenuSaver.Save(write);
            _edits.Remove(name);
            Status = $"Saved {name} in {place}" + (write.Notes.Count > 0 ? ". " + string.Join(" ", write.Notes) : "");
            AddMod?.Invoke(mod);
            if (reload)
                Saved?.Invoke(this, mod);
            return true;
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Status = $"{name} isn't saved: {e.Message}";
            return false;
        }
    }

    /// <summary>
    /// Starts the game on the menu picked (the menu command): as it's saved, so a changed menu is
    /// saved first if that's wanted; a new one has to be
    /// </summary>
    [RelayCommand]
    private async Task TestInGameAsync()
    {
        if (SelectedItem is not { } item || StartGameOn == null)
            return;
        if (IsMenuChanged || item.IsDraft)
        {
            bool save = item.IsDraft || Confirm == null
                        || await Confirm($"Test {item.Name} in the game", $"The game shows {item.Name} as it's saved. Save it first?", "Save");
            if (save && !await SaveMenuAsync(item.Name))
                return;
        }
        if (StartGameOn(item.Name))
            Status = $"Started PFWolf on {item.Name}: Esc goes back to the main menu";
    }

    /// <summary>
    /// A new menu, not saved yet: a copy of the one picked, or one window and a Back row; and,
    /// if asked, a row on another menu that opens it (CP_Menu)
    /// </summary>
    [RelayCommand]
    private async Task NewMenuAsync()
    {
        if (AskText == null || _content == null)
            return;
        var taken = _all.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = await AskText(SelectedItem == null ? "New menu" : $"New menu (a copy of {SelectedItem.Name}, or blank)",
            "Its name (a CP_Menu item's menu: names it; letters, digits and -). Start it with + for a blank one (+my-menu):", "",
            candidate => NewNameProblem(candidate.Trim().TrimStart('+'), taken));
        if (name == null)
            return;
        bool blank = name.Trim().StartsWith('+') || SelectedItem == null;
        name = name.Trim().TrimStart('+').ToLowerInvariant();

        YamlMappingNode start;
        if (!blank && SelectedItem is { } source && (_edits.TryGetValue(source.Name, out var sourceEdit) ? Menudefs(sourceEdit) : _build?.Tree) is { } copy)
            start = (YamlMappingNode)YamlNodes.Clone(copy);
        else
            start = YamlTree.Parse("""
                position: { x: 76, y: 70 }
                indent: 24
                components:
                  - id: background
                    type: Background
                    params:
                      - color: BORDCOLOR
                  - id: window
                    type: Window
                    params:
                      - x: 68
                      - y: 64
                      - width: 178
                      - height: 32
                menu-items:
                  - id: back
                    type: MenuSwitcher
                    text: Back
                """)!;
        _drafts[name] = start;

        var opener = await AskText($"Open {name} from", "A menu to add a row to that opens it (main-menu, options), or nothing for none:", "",
            candidate => candidate.Trim().Length == 0 || taken.Contains(candidate.Trim()) ? null : $"There's no menu {candidate.Trim()}");
        if (!string.IsNullOrWhiteSpace(opener))
            AddOpener(opener.Trim(), name);

        var entry = new MenuEntry(name, name, [], [], Reached: !string.IsNullOrWhiteSpace(opener));
        var item = new MenuListItem(entry, 0, _content.PackId) { IsDraft = true };
        _all.Add(item);
        _all.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        _names.Add(name);
        Filter = "";
        ShowIndex = 0;
        Refilter();
        SelectedItem = item;
        Status = $"{name} is new: Save puts it in a mod's menudefs/"
                 + (string.IsNullOrWhiteSpace(opener) ? "; nothing opens it yet (a CP_Menu row with menu: " + name + ")" : $"; save {opener.Trim()} too, which has a row that opens it");
    }

    private static string? NewNameProblem(string name, HashSet<string> taken)
    {
        if (name.Length == 0)
            return "It needs a name";
        if (name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            return "Letters, digits and - only";
        return taken.Contains(name) ? $"There's a menu {name} already" : null;
    }

    // A row on another menu (an edit of it, not saved) that opens the new one
    private void AddOpener(string openerName, string menuName)
    {
        if (_all.FirstOrDefault(item => item.Name.Equals(openerName, StringComparison.OrdinalIgnoreCase)) is not { } opener || _content == null)
            return;
        if (!_edits.TryGetValue(opener.Name, out var edit))
        {
            var original = MenuCatalog.Merge(_content, opener.Entry).Tree ?? new YamlMappingNode();
            edit = new MenuEdit(NewRoot(opener.Entry, original), original, original) { Last = MenuEditing.ToEditable(original) };
            _edits[opener.Name] = edit;
        }
        var editable = edit.Root.Build([]) as YamlMappingNode ?? new YamlMappingNode();
        if (YamlNodes.Get(editable, ["menu-items"]) is not YamlSequenceNode items)
            editable.Add("menu-items", items = new YamlSequenceNode());
        items.Add(new YamlMappingNode
        {
            { "id", menuName }, { "type", "MenuSwitcher" }, { "text", menuName }, { "action", MenuNames.SubMenuAction }, { "menu", menuName },
        });
        edit.Undo.Add(edit.Last == null ? null : YamlNodes.Clone(edit.Last));
        edit.Root = NewRoot(opener.Entry, MenuEditing.FromEditable(editable, edit.Reference));
        edit.Last = YamlNodes.Clone(editable);
        opener.HasEdits = true;
    }

}

/// <summary>What a menu's fields offer: the game's names, and its menus and texts</summary>
internal sealed class MenuSuggestions(GameContent? content, Func<IEnumerable<string>> menus) : DefinitionSuggestions(content)
{
    public override IReadOnlyList<string> For(FieldHint hint) => hint switch
    {
        FieldHint.Menu => [.. menus().Order(StringComparer.OrdinalIgnoreCase)],
        FieldHint.Article => content == null ? [] : [.. content.Assets.AssetNames.Where(name => !name.Contains('/') && content.Assets.Exists<TextAsset>(name))
            .Select(name => name.ToUpperInvariant()).Order(StringComparer.Ordinal)],
        _ => base.For(hint),
    };
}
