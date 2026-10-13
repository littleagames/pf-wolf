using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Editor.Definitions;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.ViewModels;

/// <summary>A definition as a tab lists it</summary>
public sealed partial class EntryItem : ObservableObject
{
    public EntryItem(DefinitionEntry entry, bool isSet, string? description, string detail = "", Avalonia.Media.Imaging.Bitmap? icon = null)
    {
        Entry = entry;
        IsSet = isSet;
        Description = description;
        Detail = detail;
        Icon = icon;
    }

    public DefinitionEntry Entry { get; }
    public string Name => Entry.Name;
    public string Group => Entry.Group;

    /// <summary>What it is, beside its name: a thing's class, a wall's texture</summary>
    public string Detail { get; }

    /// <summary>Its picture (a wall's texture, a thing's sprite), or null</summary>
    public Avalonia.Media.Imaging.Bitmap? Icon { get; }

    public bool HasIcon => Icon != null;

    /// <summary>The game has it (a game-info section can be listed without being set)</summary>
    public bool IsSet { get; }

    public string? Description { get; }

    /// <summary>It's been changed here and not saved</summary>
    [ObservableProperty] private bool _hasEdits;

    public double Opacity => IsSet ? 1 : 0.55;

    public string ToolTip => (Description ?? YamlNodes.Describe(Entry.Path)) + (IsSet ? "" : "\nNot set: the game uses its default");

    public string Key => EntryEditorViewModel.KeyOf(Entry.Path);
}

/// <summary>A kind of definition New… can make: a mapdefs table (walls, doors, things...)</summary>
public sealed record NewEntryChoice(string Key, string? Description);

/// <summary>A file with a part in a definition, as the tab lists it</summary>
public sealed record OriginItem(DefinitionFile File, string Label, string Part)
{
    public string ToolTip => $"{File.Label}\n{Part}. Edit YAML opens the file at the definition.";
}

/// <summary>
/// A tab of the definitions window: the definitions of one kind (game-info's sections, the
/// releases, the mapdefs entries), and the one picked as fields. Edits are kept for each
/// definition until they're saved or reverted; saving writes just what changed, into the mod
/// picked (pfwolf.pk3 is read-only).
/// </summary>
public sealed partial class EntryEditorViewModel : ObservableObject
{
    private readonly DefinitionsViewModel _owner;
    private List<EntryItem> _all = [];
    // Definitions opened, by key: their fields, and the value they were opened with
    private readonly Dictionary<string, (FieldNode? Root, YamlNode? Original)> _open = [];
    private DefinitionSuggestions _suggestions = new(null);
    private FieldContext _context;

    public EntryEditorViewModel(DefinitionsViewModel owner, DefinitionKind kind)
    {
        _owner = owner;
        Kind = kind;
        NewChoices = kind == DefinitionKind.MapDefs ? MapDefsTables() : [];
        _context = MakeContext();
        owner.DefinitionsChanged += (_, _) => Invalidate(keepEdits: true);
        owner.Reloaded += (_, _) =>
        {
            // The old game's pictures are gone
            _all = [];
            Items.Clear();
            _suggestions = new DefinitionSuggestions(owner.Content);
            _context = MakeContext();
            Invalidate(keepEdits: false);
        };
    }

    // Listed again when it's next shown (each tab reads its files only once it's looked at)
    private bool _stale = true, _staleKeepsEdits = true, _active;

    private void Invalidate(bool keepEdits)
    {
        _stale = true;
        _staleKeepsEdits &= keepEdits;
        if (_active)
            Activate();
    }

    /// <summary>The tab is shown: it lists its definitions, if they've changed since</summary>
    public void Activate()
    {
        _active = true;
        if (!_stale)
            return;
        var keepEdits = _staleKeepsEdits;
        _stale = false;
        _staleKeepsEdits = true;
        Refresh(keepEdits);
    }

    /// <summary>The tab is hidden</summary>
    public void Deactivate() => _active = false;

    private FieldContext MakeContext() => new()
    {
        Suggestions = hint => _suggestions.For(hint),
        Changed = OnFieldChanged,
        AskKey = (title, prompt, check) => _owner.AskText?.Invoke(title, prompt, "", check) ?? Task.FromResult<string?>(null),
    };

    public DefinitionKind Kind { get; }
    public string Title => DefinitionKinds.Title(Kind);

    public ObservableCollection<EntryItem> Items { get; } = [];

    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private string _countText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(DeleteEntryCommand), nameof(TakeOutCommand))]
    private EntryItem? _selectedItem;

    /// <summary>The definition's fields, shown as a tree</summary>
    public ObservableCollection<FieldNode> Rows { get; } = [];

    public ObservableCollection<OriginItem> Origins { get; } = [];

    /// <summary>For a class: what it has from its parents (and what places it on a map)</summary>
    public ObservableCollection<string> Inherited { get; } = [];

    public bool HasInherited => Inherited.Count > 0;

    [ObservableProperty] private string _heading = "";
    [ObservableProperty] private string _subheading = "";
    [ObservableProperty] private string _description = "";

    /// <summary>What's wrong with the fields as they are (a word where a number goes)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _error = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(RevertCommand))]
    private bool _isChanged;

    /// <summary>The definition is to be taken out when it's saved</summary>
    [ObservableProperty] private bool _isDeleted;

    public bool HasSelection => SelectedItem != null;
    public bool HasError => Error.Length > 0;
    public bool CanAddEntries => Kind is DefinitionKind.GamePackInfo or DefinitionKind.MapDefs or DefinitionKind.Actors or DefinitionKind.Animations;

    /// <summary>What New… can make, when it's asked (mapdefs: its tables of entries, walls, doors, things...); empty when there's one kind</summary>
    public IReadOnlyList<NewEntryChoice> NewChoices { get; }

    public bool HasNewChoices => NewChoices.Count > 0;

    /// <summary>New… makes its one kind straight away</summary>
    public bool CanAddWithoutChoice => CanAddEntries && !HasNewChoices;

    public static string KeyOf(IReadOnlyList<string> path) => string.Join("\u001f", path);

    //
    // Listing
    //

    /// <summary>Lists the definitions again: the game loaded again, or something saved</summary>
    public void Refresh(bool keepEdits)
    {
        var picked = SelectedItem?.Key;
        if (!keepEdits)
            _open.Clear();
        else
        {
            // Saved or unchanged: opened again from the files
            foreach (var key in _open.Where(open => !Changed(open.Value)).Select(open => open.Key).ToList())
                _open.Remove(key);
        }

        _all = ListEntries();
        foreach (var item in _all)
            item.HasEdits = _open.ContainsKey(item.Key);
        Refilter();
        SelectedItem = _all.FirstOrDefault(item => item.Key == picked) is { } again && Items.Contains(again) ? again : Items.FirstOrDefault();
        if (SelectedItem != null && SelectedItem.Key == picked)
            Open(SelectedItem);
    }

    private List<EntryItem> ListEntries()
    {
        var set = _owner.Set(Kind);
        if (set == null)
            return [];
        if (DefinitionSchemas.Sections(Kind) is { } sections)
        {
            // Every section the game reads, set or not, then any other key the files have
            var merged = set.Merged;
            var items = sections.Fields
                .Select(field => new EntryItem(new DefinitionEntry([field.Key], field.Key, ""), YamlNodes.Get(merged, [field.Key]) != null, field.Description))
                .ToList();
            foreach (var (key, _) in merged.Children)
            {
                if (key is YamlScalarNode { Value: { } name } && sections.Field(name) == null)
                    items.Add(new EntryItem(new DefinitionEntry([name], name, ""), true, "Not a key the game reads"));
            }
            return items;
        }

        var entries = set.Entries(baseOnly: false).Select(entry =>
        {
            var value = set.ValueOf(entry.Path, baseOnly: false);
            var (detail, icon) = Describe(entry, value);
            return new EntryItem(entry, true, DescriptionOf(entry), detail, icon);
        }).ToList();

        // pfwolf.pk3's that a mod takes out (!remove): listed still, so that can be taken back out of the mod
        var listed = entries.Select(item => item.Key).ToHashSet();
        foreach (var entry in set.Entries(baseOnly: true).Where(entry => !listed.Contains(KeyOf(entry.Path))))
        {
            var (_, icon) = Describe(entry, set.ValueOf(entry.Path, baseOnly: true));
            entries.Add(new EntryItem(entry, false,
                "Taken out by a mod (written !remove). \"Take out the mod's change\" brings pfwolf.pk3's back.", "taken out by a mod", icon));
        }
        return entries;
    }

    // What an entry is, in a word, and its picture: a thing's class and sprite, a wall's texture
    private (string Detail, Avalonia.Media.Imaging.Bitmap? Icon) Describe(DefinitionEntry entry, YamlNode? value)
    {
        string? Field(params string[] names) => names.Select(name => (YamlNodes.Get(value, [name]) as YamlScalarNode)?.Value)
            .FirstOrDefault(text => !string.IsNullOrEmpty(text));

        switch (Kind)
        {
            case DefinitionKind.Actors:
                return (Field("parent") is { } parent ? $"← {parent}" : "", _owner.Art?.Invoke(FieldHint.Class, entry.Name));
            case DefinitionKind.GamePackInfo:
                return (Field("title") ?? "", null);
            case DefinitionKind.MapDefs when entry.Path.Count > 1:
                if (Field("class") is { } thing)
                    return (thing, _owner.Art?.Invoke(FieldHint.Class, thing));
                if (Field("north", "east", "south", "west", "texture") is { } texture)
                    return (texture, _owner.Art?.Invoke(FieldHint.Texture, texture));
                return (Field("action", "shape") ?? (value is YamlScalarNode scalar ? scalar.Value ?? "" : ""), null);
            case DefinitionKind.Animations:
            {
                // "3 frames, loops", its first frame as the picture; one the game left out says so
                var frames = (YamlNodes.Get(value, ["frames"]) as YamlSequenceNode)?.Children ?? [];
                var names = frames.Select(frame => frame is YamlScalarNode s ? s.Value : (YamlNodes.Get(frame, ["name"]) as YamlScalarNode)?.Value)
                    .Where(name => !string.IsNullOrEmpty(name)).ToList();
                bool once = names.Count > 0 && names[^1]!.Equals(AnimationDefinition.Stop, StringComparison.OrdinalIgnoreCase);
                int count = names.Count - (once ? 1 : 0);
                var detail = $"{count} frame{(count == 1 ? "" : "s")}, {(once ? "plays once" : "loops")}";
                if (_owner.Content?.Assets.AnimationProblems.ContainsKey(entry.Name) == true)
                    detail = "⚠ " + detail;
                return (detail, count > 0 ? _owner.Art?.Invoke(FieldHint.Texture, names[0]!) : null);
            }
            default:
                return ("", null);
        }
    }

    private string? DescriptionOf(DefinitionEntry entry) => Kind switch
    {
        DefinitionKind.MapDefs => DefinitionSchemas.MapDefs.Field(entry.Path[0])?.Description,
        // What the game made of it when it loaded
        DefinitionKind.Animations => _owner.Content?.Assets.AnimationProblems.GetValueOrDefault(entry.Name) is { } problem
            ? $"When the game loaded: {problem}"
            : null,
        _ => null,
    };

    partial void OnFilterChanged(string value) => Refilter();

    private void Refilter()
    {
        var selected = SelectedItem;
        var words = Filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Items.Clear();
        foreach (var item in _all.Where(item => words.All(word => item.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                                                                 || item.Group.Contains(word, StringComparison.OrdinalIgnoreCase)
                                                                 || item.Detail.Contains(word, StringComparison.OrdinalIgnoreCase))))
            Items.Add(item);
        CountText = Items.Count == _all.Count ? $"{Items.Count} {Noun}" : $"{Items.Count} of {_all.Count} {Noun}";
        if (selected != null && Items.Contains(selected))
            SelectedItem = selected;
    }

    private string Noun => Kind switch
    {
        DefinitionKind.GameInfo or DefinitionKind.Factions => "sections",
        DefinitionKind.GamePackInfo => "games",
        DefinitionKind.Actors => "classes",
        DefinitionKind.Animations => "animations",
        _ => "entries",
    };

    //
    // The definition picked
    //

    partial void OnSelectedItemChanged(EntryItem? value) => Open(value);

    private void Open(EntryItem? item)
    {
        Rows.Clear();
        Origins.Clear();
        Inherited.Clear();
        OnPropertyChanged(nameof(HasInherited));
        Error = "";
        IsChanged = false;
        IsDeleted = false;
        Heading = "";
        Subheading = "";
        Description = "";
        var set = _owner.Set(Kind);
        if (item == null || set == null)
            return;

        var path = item.Entry.Path;
        if (!_open.TryGetValue(item.Key, out var open))
        {
            var value = set.ValueOf(path, baseOnly: false);
            var baseValue = set.ValueOf(path, baseOnly: true);
            var root = new FieldNode(SchemaFor(path), path[^1], value is null ? null : YamlNodes.Clone(value), baseValue, null, _context,
                isEntry: !DefinitionKinds.IsSections(Kind));
            root.EnsureChildren();
            HideFieldsThatDoNotApply(root, path);
            // One the game doesn't have (taken out by a mod) starts as its empty fields, which isn't a change
            open = (root, value ?? root.Build([]));
            _open[item.Key] = open;
        }

        if (open.Root == null)
            IsDeleted = true;
        else if (open.Root.IsGroup && open.Root.Schema?.Type == FieldType.Record)
        {
            foreach (var row in open.Root.Children)
                Rows.Add(row);
        }
        else if (open.Root.IsGroup)
        {
            // A list or dictionary as itself, opened, so it can be added to (end-strings, maps...)
            open.Root.IsExpanded = true;
            Rows.Add(open.Root);
        }
        else
            Rows.Add(open.Root);

        Heading = Kind switch
        {
            DefinitionKind.MapDefs => $"{path[0]} {path[^1]}",
            _ => item.Name,
        };
        Description = item.Description ?? "";
        var defining = set.FilesDefining(path);
        Subheading = defining.Count == 0
            ? (DefinitionKinds.IsSections(Kind) && !item.IsSet ? "Not set: the game uses its default" : "Not saved yet")
            : defining.Count == 1 ? $"From {defining[0].Label}" : $"From {defining[^1].Label}, over {defining.Count - 1} more";
        for (int i = 0; i < defining.Count; i++)
        {
            var file = defining[i];
            Origins.Add(new OriginItem(file, file.Label, i == defining.Count - 1 ? "Its say is the last, so it's what the game uses (or merges last)" : "Laid over by the files after it"));
        }
        if (Kind == DefinitionKind.Actors)
            FillInherited(item.Name, set.Merged);
        UpdateChanged();
    }

    private FieldSchema SchemaFor(IReadOnlyList<string> path) => Kind switch
    {
        DefinitionKind.GameInfo or DefinitionKind.Factions => DefinitionSchemas.Sections(Kind)!.Field(path[0]) ?? FieldSchema.For(typeof(object), path[0]),
        DefinitionKind.GamePackInfo => DefinitionSchemas.GamePack,
        DefinitionKind.MapDefs => DefinitionSchemas.MapDefs.Field(path[0]) is { } table
            ? path.Count > 1 && table.Type == FieldType.Dictionary ? table.Item! : table
            : FieldSchema.For(typeof(object), path[0]),
        DefinitionKind.Animations => DefinitionSchemas.Animation,
        _ => DefinitionSchemas.Actor,
    };

    // A class's flags, properties and states from its parents that it doesn't set itself, nearest
    // parent first, and the map things that place it
    private void FillInherited(string className, YamlMappingNode merged)
    {
        static IEnumerable<string> Keys(YamlNode? node)
            => (node as YamlMappingNode)?.Children.Keys.OfType<YamlScalarNode>().Select(key => key.Value ?? "") ?? [];
        static IEnumerable<string> Flags(YamlNode? node)
            => (node as YamlSequenceNode)?.Children.OfType<YamlScalarNode>().Select(flag => flag.Value ?? "") ?? [];

        var own = YamlNodes.Get(merged, [className]);
        var flags = new HashSet<string>(Flags(YamlNodes.Get(own, ["flags"])), StringComparer.OrdinalIgnoreCase);
        var properties = new HashSet<string>(Keys(YamlNodes.Get(own, ["properties"])), StringComparer.OrdinalIgnoreCase);
        var states = new HashSet<string>(Keys(YamlNodes.Get(own, ["states"])), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.Ordinal) { className };

        var parent = (YamlNodes.Get(own, ["parent"]) as YamlScalarNode)?.Value;
        while (!string.IsNullOrEmpty(parent) && seen.Add(parent) && YamlNodes.Get(merged, [parent]) is YamlMappingNode parentClass)
        {
            var newFlags = Flags(YamlNodes.Get(parentClass, ["flags"])).Where(flags.Add).ToList();
            if (newFlags.Count > 0)
                Inherited.Add($"{parent}: flags {string.Join(", ", newFlags)}");
            if (YamlNodes.Get(parentClass, ["properties"]) is YamlMappingNode parentProperties)
            {
                foreach (var (key, value) in parentProperties.Children)
                {
                    if (key is YamlScalarNode { Value: { } name } && properties.Add(name))
                        Inherited.Add($"{parent}: {name} = {YamlText.Inline(value)}");
                }
            }
            var newStates = Keys(YamlNodes.Get(parentClass, ["states"])).Where(states.Add).ToList();
            if (newStates.Count > 0)
                Inherited.Add($"{parent}: states {string.Join(", ", newStates)}");
            parent = (YamlNodes.Get(parentClass, ["parent"]) as YamlScalarNode)?.Value;
        }

        if (_owner.Content?.MapDefs.Things.Where(thing => thing.Value.Class.Equals(className, StringComparison.OrdinalIgnoreCase))
                .Select(thing => thing.Key).Order().ToList() is { Count: > 0 } placedBy)
            Inherited.Add($"Placed by map things {string.Join(", ", placedBy.Take(24))}{(placedBy.Count > 24 ? $" and {placedBy.Count - 24} more" : "")}");
        OnPropertyChanged(nameof(HasInherited));
    }

    // Walls and doors are read as one type, and a wall's switch as a patch's, whose fields say
    // which they're for ("Doors only: ...", "Patches only: ...")
    private void HideFieldsThatDoNotApply(FieldNode root, IReadOnlyList<string> path)
    {
        if (Kind != DefinitionKind.MapDefs || path.Count < 2)
            return;
        string[] others = path[0].ToLowerInvariant() switch
        {
            "walls" => ["Doors only", "Patches only"],
            "doors" => ["Walls only", "Patches only"],
            _ => [],
        };
        if (others.Length > 0)
            root.HideUnset(field => others.Any(other => field.Description?.StartsWith(other, StringComparison.OrdinalIgnoreCase) == true));
    }

    private void OnFieldChanged() => UpdateChanged();

    private bool Changed((FieldNode? Root, YamlNode? Original) open)
        => open.Root == null ? open.Original != null : !YamlNodes.Equal(open.Root.Build([]), open.Original);

    private void UpdateChanged()
    {
        if (SelectedItem == null || !_open.TryGetValue(SelectedItem.Key, out var open))
            return;
        var errors = new List<string>();
        var built = open.Root?.Build(errors);
        IsChanged = open.Root == null ? open.Original != null : !YamlNodes.Equal(built, open.Original);
        Error = string.Join("\n", errors);
        SelectedItem.HasEdits = IsChanged;
    }

    //
    // Commands
    //

    [RelayCommand(CanExecute = nameof(IsChanged))]
    private void Revert()
    {
        if (SelectedItem == null)
            return;
        _open.Remove(SelectedItem.Key);
        SelectedItem.HasEdits = false;
        Open(SelectedItem);
    }

    /// <summary>Saves the definition into the mod picked (pfwolf.pk3 is read-only)</summary>
    [RelayCommand(CanExecute = nameof(IsChanged))]
    private async Task SaveAsync()
    {
        if (SelectedItem is { } item)
            await SaveEntry(item.Key);
    }

    /// <summary>The definitions changed here and not saved, as they're named to a person ("Actors: Guard")</summary>
    public IReadOnlyList<string> UnsavedNames
        => _open.Where(open => Changed(open.Value)).Select(open => $"{Title}: {YamlNodes.Describe(open.Key.Split('\u001f'))}").ToList();

    /// <summary>Saves every definition changed here; false (with what's wrong shown) when one isn't saved</summary>
    public async Task<bool> SaveAllAsync()
    {
        foreach (var key in _open.Where(open => Changed(open.Value)).Select(open => open.Key).ToList())
        {
            if (!await SaveEntry(key))
                return false;
        }
        return true;
    }

    // Saves one definition's edits; false when they aren't saved
    private async Task<bool> SaveEntry(string key)
    {
        if (_owner.Set(Kind) is not { } set || _owner.Content is not { } content || !_open.TryGetValue(key, out var open))
            return false;
        var path = key.Split('\u001f');
        var name = YamlNodes.Describe(path);

        void Fail(string message)
        {
            if (SelectedItem?.Key == key)
                Error = message;
            _owner.Status = $"{Title}: {name} isn't saved. {message}";
        }

        var errors = new List<string>();
        var edited = open.Root?.Build(errors);
        if (errors.Count > 0)
        {
            Fail("Not saved: " + string.Join("\n", errors));
            return false;
        }

        if (await _owner.ModToSaveIn() is not { } mod)
            return false;

        try
        {
            var write = set.PlanWrite(path, edited, mod);
            var places = DefinitionSaver.Save(write);
            _open.Remove(key);
            if (_all.FirstOrDefault(item => item.Key == key) is { } item)
                item.HasEdits = false;
            _owner.FileSaved(mod, places, write.Notes);
            return true;
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Fail($"Not saved: {e.Message}");
            return false;
        }
    }

    private bool CanDelete => SelectedItem != null && !DefinitionKinds.IsSections(Kind);

    /// <summary>Marks the definition to be taken out when it's saved</summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void DeleteEntry()
    {
        if (SelectedItem is not { } item || !_open.TryGetValue(item.Key, out var open))
            return;
        _open[item.Key] = (null, open.Original);
        Open(item);
    }

    private bool CanTakeOut => SelectedItem != null && _owner.SaveTarget is { } target
                               && _owner.Set(Kind)?.ModChanges(SelectedItem.Entry.Path, target.Path) == true;

    /// <summary>Takes the mod's own say in the definition out, so it's what pfwolf.pk3 (and the mods before) make it</summary>
    [RelayCommand(CanExecute = nameof(CanTakeOut))]
    private void TakeOut()
    {
        if (SelectedItem is not { } item || _owner.Set(Kind) is not { } set || _owner.Content is not { } content || _owner.SaveTarget is not { } target)
            return;
        try
        {
            var write = set.PlanTakeOut(item.Entry.Path, target.Path);
            var places = DefinitionSaver.Save(write);
            _open.Remove(item.Key);
            item.HasEdits = false;
            _owner.FileSaved(target.Path, places, write.Notes);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Error = $"Not changed: {e.Message}";
        }
    }

    // The mapdefs tables New… can add an entry to: those of numbered entries with fields (walls, doors, things...)
    private static List<NewEntryChoice> MapDefsTables()
        => DefinitionSchemas.MapDefs.Fields
            .Where(table => table.Type == FieldType.Dictionary && table.Item?.Type == FieldType.Record)
            .Select(table => new NewEntryChoice(table.Key, table.Description))
            .ToList();

    /// <summary>
    /// Starts a new definition (a game, a mapdefs entry in the table picked from New…'s list, a class),
    /// saved like any other
    /// </summary>
    [RelayCommand]
    private async Task NewEntryAsync(string? choice)
    {
        if (!CanAddEntries || _owner.Set(Kind) is not { } set || _owner.AskText == null)
            return;
        var merged = set.Merged;

        IReadOnlyList<string> path;
        YamlNode value;
        switch (Kind)
        {
            case DefinitionKind.MapDefs:
            {
                var table = choice ?? SelectedItem?.Entry.Path[0] ?? "things";
                if (NewChoices.FirstOrDefault(known => known.Key.Equals(table, StringComparison.OrdinalIgnoreCase)) is not { } picked)
                {
                    _owner.Status = $"{table} isn't a table of entries: pick walls, doors, things... from New…";
                    return;
                }
                // Named as the files name it, so it's listed with the table's others
                table = merged.Children.Keys.OfType<YamlScalarNode>().Select(key => key.Value)
                    .FirstOrDefault(name => picked.Key.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? picked.Key;
                var used = (YamlNodes.Get(merged, [table]) as YamlMappingNode)?.Children.Keys.OfType<YamlScalarNode>()
                    .Select(key => int.TryParse(key.Value, out var id) ? id : -1).ToHashSet() ?? [];
                var free = FreeId(table, used);
                var id = await _owner.AskText($"New {table} entry", $"Its number (the map value that means it):", free.ToString(CultureInfo.InvariantCulture),
                    candidate => !int.TryParse(candidate.Trim(), out var number) || number < 0 ? "It's a number, 0 or more"
                        : used.Contains(number) ? $"There's a {table} {number} already" : null);
                if (id == null)
                    return;
                path = [table, id.Trim()];
                value = new YamlMappingNode();
                break;
            }
            case DefinitionKind.GamePackInfo:
            {
                var id = await _owner.AskText("New game", "Its id (what --game names it), lower case with dashes:", "my-game",
                    candidate => candidate.Trim().Length == 0 || candidate.Trim().Any(c => !(char.IsLetterOrDigit(c) || c == '-'))
                        ? "Letters, digits and dashes"
                        : YamlNodes.Get(merged, [candidate.Trim()]) != null ? $"There's a {candidate.Trim()} already" : null);
                if (id == null)
                    return;
                path = [id.Trim()];
                value = new YamlMappingNode { { "title", id.Trim() }, { "base-pack", "standalone" } };
                break;
            }
            case DefinitionKind.Animations:
            {
                var name = await _owner.AskText("New animation", "Its name (as walls, flats and pictures use it; a texture's own name animates it everywhere):", "",
                    candidate => candidate.Trim().Length == 0 || candidate.Trim().Any(c => char.IsWhiteSpace(c) || c is '/' or '\\' or ':')
                        ? "A name without spaces, slashes or colons"
                        : YamlNodes.Get(merged, [candidate.Trim()]) != null ? $"There's a {candidate.Trim()} already" : null);
                if (name == null)
                    return;
                path = [name.Trim().ToUpperInvariant()];
                value = new YamlMappingNode
                {
                    { "tics", AnimationDefinition.DefaultTics.ToString(CultureInfo.InvariantCulture) },
                    { "frames", new YamlSequenceNode() },
                };
                break;
            }
            default:
            {
                var name = await _owner.AskText("New class", "Its name (as mapdefs and other classes name it):", "",
                    candidate => candidate.Trim().Length == 0 || candidate.Trim().Any(c => !(char.IsLetterOrDigit(c) || c == '_'))
                        ? "Letters, digits and _"
                        : YamlNodes.Get(merged, [candidate.Trim()]) != null ? $"There's a {candidate.Trim()} already" : null);
                if (name == null)
                    return;
                var parent = SelectedItem?.Name;
                path = [name.Trim()];
                value = parent != null ? new YamlMappingNode { { "parent", parent } } : new YamlMappingNode();
                break;
            }
        }

        var entry = new DefinitionEntry(path, path[^1], Kind == DefinitionKind.MapDefs ? path[0] : "new");
        var item = new EntryItem(entry, true, DescriptionOf(entry));
        var root = new FieldNode(SchemaFor(path), path[^1], value, null, null, _context, isEntry: true);
        root.EnsureChildren();
        HideFieldsThatDoNotApply(root, path);
        _open[item.Key] = (root, null);
        // A mapdefs entry goes with its table's (after its last), not at the end of the list
        var lastOfTable = Kind == DefinitionKind.MapDefs
            ? _all.FindLastIndex(other => other.Group.Equals(entry.Group, StringComparison.OrdinalIgnoreCase))
            : -1;
        _all.Insert(lastOfTable >= 0 ? lastOfTable + 1 : _all.Count, item);
        Filter = "";
        Refilter();
        SelectedItem = item;
        _owner.Status = $"{YamlNodes.Describe(path)} is new: fill it in and save it";
    }

    // The first number a table doesn't use: walls go from 1 to 63, the rest from the table's lowest up
    private static int FreeId(string table, HashSet<int> used)
    {
        int start = table.Equals("walls", StringComparison.OrdinalIgnoreCase) ? 1 : used.Count > 0 ? used.Where(id => id >= 0).DefaultIfEmpty(1).Min() : 1;
        int id = start;
        while (used.Contains(id))
            id++;
        return id;
    }

    /// <summary>The right-click menu's Copy to mod: a game is copied as a new game of the mod's, built on it</summary>
    public string CopyToModHeader => Kind == DefinitionKind.GamePackInfo ? "Copy to mod as a new game…" : "Copy to mod";

    public string CopyToModToolTip => Kind == DefinitionKind.GamePackInfo
        ? "A mod can't change pfwolf.pk3's games, but it can add one built on it: it plays that game's data files and definitions, with the mod's over them. Asks for its id; a game the mod has already is just opened."
        : "Copies the whole definition, as the game has it now, into the mod picked to save into, and opens it there in the YAML tab to change (a mod with its own of it already just opens that)";

    /// <summary>
    /// Copies the definition picked whole, as the game has it, into the mod picked to save into
    /// (its file of the kind: game-info.yaml, actordefs/actors.yaml, mapdefs/TABLE.yaml), and
    /// opens it there in the YAML tab to change. One the mod has its own of already is just opened.
    /// </summary>
    [RelayCommand]
    private async Task CopyToModAsync()
    {
        if (SelectedItem is not { } item || _owner.Set(Kind) is not { } set || _owner.Content is not { } content)
            return;
        if (await _owner.ModToSaveIn() is not { } mod)
            return;

        var path = item.Entry.Path;
        if (set.ModFileDefining(path, mod) is { } existing)
        {
            _owner.Status = $"{existing.SourceName} has its own {YamlNodes.Describe(path)} already: here it is";
            _owner.ShowYaml(existing, path);
            return;
        }

        if (Kind == DefinitionKind.GamePackInfo)
        {
            await CopyGameToModAsync(set, path[0], mod);
            return;
        }

        try
        {
            var write = set.PlanCopyToMod(path, mod);
            var places = DefinitionSaver.Save(write);
            _owner.FileSaved(mod, places, write.Notes);
            _owner.ShowYaml(write.File.WithText(write.NewText), path);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            _owner.Status = $"Not copied: {e.Message}";
        }
    }

    /// <summary>
    /// A game added to the mod's gamepack-info.yaml, built on the one picked (base-pack), by an id
    /// asked for: it has that game's data files, definitions and look, and the mod's files over them
    /// </summary>
    private async Task CopyGameToModAsync(DefinitionSet set, string baseId, string mod)
    {
        if (_owner.AskText == null || _owner.Content is not { } content)
            return;

        var taken = set.Merged.Children.Keys.OfType<YamlScalarNode>().Select(key => key.Value ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var suggested = $"{baseId}-mod";
        for (int n = 2; taken.Contains(suggested); n++)
            suggested = $"{baseId}-mod{n}";

        var id = await _owner.AskText("Copy to mod as a new game",
            $"A mod can't change {baseId}, but it can add a game built on it. The new game's id (what --game names it):", suggested,
            candidate =>
            {
                var trimmed = candidate.Trim();
                if (trimmed.Length == 0 || trimmed.Any(c => !(char.IsLetterOrDigit(c) || c == '-')))
                    return "Letters, digits and dashes";
                return taken.Contains(trimmed) ? $"There's a game {trimmed} already" : null;
            });
        if (id == null)
            return;
        id = id.Trim().ToLowerInvariant();

        var title = (YamlNodes.Get(set.Merged, [baseId, "title"]) as YamlScalarNode)?.Value ?? baseId;
        var game = new YamlMappingNode { { "title", $"{title} ({id})" }, { "base-pack", baseId } };
        try
        {
            var write = set.PlanWrite([id], game, mod);
            var places = DefinitionSaver.Save(write);
            _owner.FileSaved(mod, places, [$"{id} plays {baseId}'s data files and definitions with this mod's over them: pick it in the editor's games once it's loaded again, or run PFWolf with --game {id}"]);
            _owner.ShowYaml(write.File.WithText(write.NewText), [id]);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            _owner.Status = $"Not copied: {e.Message}";
        }
    }

    /// <summary>Picks a definition by its key path; false when there's none</summary>
    public bool Select(IReadOnlyList<string> path)
    {
        var key = KeyOf(path);
        var item = _all.FirstOrDefault(candidate => candidate.Key == key)
                   ?? _all.FirstOrDefault(candidate => candidate.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (item == null)
            return false;
        if (!Items.Contains(item))
            Filter = "";
        SelectedItem = item;
        return true;
    }

    /// <summary>The field picked in the tree (a state's frame gone to from a script)</summary>
    [ObservableProperty] private FieldNode? _selectedRow;

    /// <summary>
    /// Opens the shown definition's fields down a path of keys (states, Fire, 2: a list item by its
    /// number from 0) and picks the last; as far as it gets when a key isn't there. False when
    /// the first isn't.
    /// </summary>
    public bool SelectField(IReadOnlyList<string> fieldPath)
    {
        if (fieldPath.Count == 0)
            return true;
        FieldNode? node = null;
        IEnumerable<FieldNode> level = Rows;
        foreach (var key in fieldPath)
        {
            var next = level.FirstOrDefault(row => row.Key == key)
                       ?? level.FirstOrDefault(row => row.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (next == null)
                break;
            node = next;
            node.EnsureChildren();
            node.IsExpanded = true;
            level = node.Children;
        }
        if (node == null)
            return false;
        // (it's opened too: a frame shows its action and think)
        SelectedRow = node;
        return true;
    }

    /// <summary>Opens a file the definition comes from in the YAML tab, at the definition</summary>
    [RelayCommand]
    private void EditYaml(OriginItem? origin)
    {
        if (origin != null && SelectedItem != null)
            _owner.ShowYaml(origin.File, SelectedItem.Entry.Path);
    }
}

/// <summary>The schemas of the definitions' types, made once</summary>
public static class DefinitionSchemas
{
    public static FieldSchema GameInfo { get; } = FieldSchema.For(typeof(GameInfoAsset));
    public static FieldSchema Factions { get; } = FieldSchema.For(typeof(FactionsAsset));

    /// <summary>The record a sections kind's file is (game-info, factions), or null for one of entries</summary>
    public static FieldSchema? Sections(DefinitionKind kind) => kind switch
    {
        DefinitionKind.GameInfo => GameInfo,
        DefinitionKind.Factions => Factions,
        _ => null,
    };
    public static FieldSchema GamePack { get; } = FieldSchema.For(typeof(PFWolf.Assets.GamePack));
    public static FieldSchema MapDefs { get; } = FieldSchema.For(typeof(MapObjectTranslationAsset));
    public static FieldSchema Actor { get; } = ActorSchema.Build();
    public static FieldSchema Animation { get; } = AnimationSchema.Build();
}
