using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Definitions;
using PFWolf.Editor.Data;
using PFWolf.Editor.Definitions;
using PFWolf.Entities.Actors;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.ViewModels;

/// <summary>A property that can be given to the class picked, for Add property</summary>
public sealed record PropertyChoice(string Name, string Description)
{
    public override string ToString() => Name;
}

/// <summary>
/// The actor window's field editing: a class's parent, radius, flags and properties (the
/// Definitions window's rows, its properties each with what it does), played and checked as they're
/// changed, undone and redone, saved into a mod as just what changed; new classes, copied or built
/// on the one picked, with mapdefs things to place them
/// </summary>
public sealed partial class ActorBrowserViewModel
{
    /// <summary>A class being edited: its fields, what it was opened as, and its steps</summary>
    private sealed class ClassEdit(FieldNode root, YamlNode? original)
    {
        public FieldNode Root { get; set; } = root;

        /// <summary>The class as the files have it; null for a new class not saved yet</summary>
        public YamlNode? Original { get; } = original;

        public List<YamlNode?> Undo { get; } = [];
        public List<YamlNode?> Redo { get; } = [];

        /// <summary>The fields as last built: what an undo step goes back from</summary>
        public YamlNode? Last { get; set; }

        public DateTime LastStep { get; set; }
    }

    /// <summary>The actordefs fields, with each property the engine reads as a field of its own (what it does, its default, names to pick)</summary>
    public static FieldSchema ClassSchema { get; } = FieldSchema.Record("class", DefinitionSchemas.Actor.Description,
        [.. DefinitionSchemas.Actor.Fields.Select(field => field.Key == "properties" ? ActorProperties.Schema() : field)]);

    // The rows the field list shows: states are the timeline's, extend only when set
    private static readonly HashSet<string> RowsNotShown = new(StringComparer.OrdinalIgnoreCase) { "states" };

    // Classes opened for editing, by name, kept until saved or reverted
    private readonly Dictionary<string, ClassEdit> _edits = new(StringComparer.Ordinal);
    // New classes not saved yet, by name: what they started as, and the mapdefs things placing them
    private readonly Dictionary<string, YamlNode> _drafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(int Id, YamlMappingNode Thing)>> _draftThings = new(StringComparer.Ordinal);
    private DefinitionSet? _classSet;
    private DefinitionSuggestions _suggestions = new(null);
    private FieldContext? _context;
    private HashSet<string> _scriptActions = [];
    private bool _rebuilding;

    /// <summary>The picked class's own fields (parent, radius, flags, properties)</summary>
    public ObservableCollection<FieldNode> FieldRows { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveClassCommand), nameof(RevertClassCommand))]
    private bool _isChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFieldError))]
    private string _fieldError = "";

    public bool HasFieldError => FieldError.Length > 0;

    [ObservableProperty] private string _fieldSource = "";

    /// <summary>Properties the picked class could be given (its kind reads them) and hasn't got</summary>
    [ObservableProperty] private IReadOnlyList<PropertyChoice> _addableProperties = [];

    /// <summary>The property picked to add: shown in the fields, empty, to fill in</summary>
    [ObservableProperty] private PropertyChoice? _propertyToAdd;

    /// <summary>Asks for a line of text (title, prompt, initial, check); null when cancelled</summary>
    public Func<string, string, string, Func<string, string?>, Task<string?>>? AskText { get; set; }

    /// <summary>Asks for a mod (a folder or a pk3) to save in; null when none is picked</summary>
    public Func<string, Task<string?>>? PickMod { get; set; }

    /// <summary>Asks a yes-or-no question (title, message, the yes button's label)</summary>
    public Func<string, string, string, Task<bool>>? Confirm { get; set; }

    /// <summary>Adds a mod saved into to the editor's mods (so the game loads it)</summary>
    public Action<string>? AddMod { get; set; }

    /// <summary>Something went into a mod (the editor loads the game again)</summary>
    public event EventHandler<string>? Saved;

    /// <summary>The classes changed here and not saved, as they're named to a person ("actor Guard")</summary>
    public IReadOnlyList<string> UnsavedNames => [.. _edits.Where(edit => Changed(edit.Value)).Select(edit => $"actor {edit.Key}")];

    private static bool Changed(ClassEdit edit) => !YamlNodes.Equal(edit.Root.Build([]), edit.Original);

    private bool HasUnsaved(string name) => _edits.TryGetValue(name, out var edit) && Changed(edit);

    // Read again as the game loads: the actordefs files, and what the fields can offer
    private void LoadFields(GameContent content)
    {
        _classSet = DefinitionSet.Load(content, DefinitionKind.Actors);
        _suggestions = new DefinitionSuggestions(content);
        _scriptActions = ActorCatalog.LuaActions(content);
        _context = new FieldContext
        {
            Suggestions = hint => _suggestions.For(hint),
            Changed = OnFieldsChanged,
            AskKey = (title, prompt, check) => AskText?.Invoke(title, prompt, "", check) ?? Task.FromResult<string?>(null),
        };

        // Saved or unchanged: opened again from the files; changed ones keep their fields
        foreach (var name in _edits.Where(edit => !Changed(edit.Value)).Select(edit => edit.Key).ToList())
            _edits.Remove(name);
        foreach (var name in _drafts.Keys.Where(name => _classSet.ValueOf([name], baseOnly: false) != null).ToList())
        {
            _drafts.Remove(name);
            _draftThings.Remove(name);
        }
    }

    // A class only this window has (new, not saved): listed with the game's
    private ActorItem DraftItem(GameContent content, string name)
    {
        var value = _edits.TryGetValue(name, out var edit) ? edit.Root.Build([]) ?? _drafts[name] : _drafts[name];
        var own = Read(name, value, out _) ?? new ActorData();
        var entry = ActorCatalog.Rebuild(content, name, own, null, _scriptActions);
        entry.Notes.Insert(0, "New: not saved in a mod yet");
        return new ActorItem(entry) { IsDraft = true, HasEdits = true };
    }

    // A class's fields as the game reads them, or null (with why) when they don't read
    private static ActorData? Read(string name, YamlNode? value, out string? problem)
    {
        problem = null;
        if (value is not YamlMappingNode mapping)
            return value == null ? null : new ActorData();
        try
        {
            var text = YamlTree.ToText(new YamlMappingNode { { name, mapping } });
            return YamlDataEntryLoader.DeserializeQuietly<Dictionary<string, ActorData>>(text).GetValueOrDefault(name) ?? new ActorData();
        }
        catch (Exception e) when (e is YamlDotNet.Core.YamlException or InvalidOperationException or FormatException)
        {
            problem = e is YamlDotNet.Core.YamlException yaml ? DefinitionKinds.Describe(yaml) : e.Message;
            return null;
        }
    }

    // The fields of the class picked, opened (or as left) for editing, and the class as edited
    private void OpenFields(ActorItem? item)
    {
        FieldRows.Clear();
        FieldError = "";
        IsChanged = false;
        FieldSource = "";
        _edited = null;
        PropertyToAdd = null;
        if (item == null || _classSet == null || _context == null || _content == null)
            return;

        var name = item.Name;
        if (!_edits.TryGetValue(name, out var edit))
        {
            var value = _classSet.ValueOf([name], baseOnly: false) ?? _drafts.GetValueOrDefault(name) ?? new YamlMappingNode();
            var original = _drafts.ContainsKey(name) ? null : value;
            edit = new ClassEdit(NewRoot(name, value), original) { Last = YamlNodes.Clone(value) };
            _edits[name] = edit;
        }
        ShowRows(edit);

        var defining = _classSet.FilesDefining([name]);
        FieldSource = defining.Count == 0 ? "Not saved in a mod yet"
            : defining.Count == 1 ? $"From {defining[0].Label}" : $"From {defining[^1].Label}, over {defining.Count - 1} more";
        UpdateFields(edit, show: false);
    }

    private void ShowRows(ClassEdit edit)
    {
        FieldRows.Clear();
        foreach (var row in edit.Root.Children.Where(row => !RowsNotShown.Contains(row.Key)))
            FieldRows.Add(row);
    }

    // The fields of a value: its properties only those it sets (Add property gives it others)
    private FieldNode NewRoot(string name, YamlNode? value)
    {
        var root = new FieldNode(ClassSchema, name, value == null ? null : YamlNodes.Clone(value),
            _classSet?.ValueOf([name], baseOnly: true), null, _context!, isEntry: true);
        root.EnsureChildren();
        root.HideUnset(field => field.Key == "extend");
        if (root.Children.FirstOrDefault(child => child.Key == "properties") is { } properties)
        {
            properties.HideUnset(_ => true);
            properties.IsExpanded = true;
        }
        return root;
    }

    private void OnFieldsChanged()
    {
        if (_rebuilding || SelectedItem is not { } item || !_edits.TryGetValue(item.Name, out var edit))
            return;

        var built = edit.Root.Build([]);
        // A step to undo; typing in a field is one step, not one a letter
        if (!YamlNodes.Equal(built, edit.Last))
        {
            if (edit.Undo.Count == 0 || DateTime.UtcNow - edit.LastStep > TimeSpan.FromSeconds(1))
                edit.Undo.Add(edit.Last == null ? null : YamlNodes.Clone(edit.Last));
            edit.Redo.Clear();
            edit.LastStep = DateTime.UtcNow;
            edit.Last = built == null ? null : YamlNodes.Clone(built);
        }
        // After the field that's changing has finished: the panels are filled again
        Later(() => UpdateFields(edit, show: true));
    }

    // Runs after the field that's changing has finished (rows can't be swapped under it)
    private static void Later(Action action)
    {
        if (Avalonia.Application.Current != null)
            Dispatcher.UIThread.Post(action);
        else
            action();
    }

    // The fields made again from a value (an undo, a redo)
    private void ShowFields(ClassEdit edit, YamlNode? value)
    {
        _rebuilding = true;
        try
        {
            edit.Root = NewRoot(SelectedItem?.Name ?? "", value);
            edit.Last = value == null ? null : YamlNodes.Clone(value);
            ShowRows(edit);
        }
        finally
        {
            _rebuilding = false;
        }
        UpdateFields(edit, show: true);
    }

    // What's changed, what's wrong, and the class built again as the fields have it
    private void UpdateFields(ClassEdit edit, bool show)
    {
        if (SelectedItem is not { } item || _content == null)
            return;
        var errors = new List<string>();
        var built = edit.Root.Build(errors);
        IsChanged = !YamlNodes.Equal(built, edit.Original);
        item.HasEdits = IsChanged || item.IsDraft;
        UndoClassCommand.NotifyCanExecuteChanged();
        RedoClassCommand.NotifyCanExecuteChanged();
        TakeOutClassCommand.NotifyCanExecuteChanged();

        var own = Read(item.Name, built, out var problem);
        if (problem != null)
            errors.Add($"It doesn't read as a class: {problem}");
        _edited = own != null && (IsChanged || item.IsDraft)
            ? ActorCatalog.Rebuild(_content, item.Name, own, item.IsDraft ? null : item.Entry, _scriptActions)
            : null;
        FieldError = string.Join("\n", errors);
        if (show)
            ShowEntry(keepState: true);
    }

    // The game's classes with the one shown as edited (for where each property comes from)
    private IReadOnlyDictionary<string, ActorData> EditedActors(ActorEntry entry)
    {
        var actors = _content!.Actors.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        actors[entry.Name] = entry.Own;
        return actors;
    }

    // The properties its kind reads that it doesn't set itself (a parent's may be set: given here, it's its own)
    private static IReadOnlyList<PropertyChoice> PropertiesToAdd(ActorEntry entry)
        => [.. ActorProperties.All
            .Where(property => !property.IsPattern && !entry.Own.Properties.ContainsKey(property.Name)
                               && (property.Needs == null || ActorClasses.IsA(entry.EngineClass, property.Needs)))
            .OrderBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
            .Select(property => new PropertyChoice(property.Name, property.Description))];

    partial void OnPropertyToAddChanged(PropertyChoice? value)
    {
        if (value == null || SelectedItem is not { } item || !_edits.TryGetValue(item.Name, out var edit))
            return;
        if (edit.Root.Children.FirstOrDefault(child => child.Key == "properties") is { } properties)
        {
            properties.IsExpanded = true;
            properties.ShowField(value.Name);
            Status = $"{value.Name}: fill it in under properties (left empty, it's not given)";
        }
        Later(() => PropertyToAdd = null);
    }

    private bool CanUndoClass => SelectedItem != null && _edits.TryGetValue(SelectedItem.Name, out var edit) && edit.Undo.Count > 0;
    private bool CanRedoClass => SelectedItem != null && _edits.TryGetValue(SelectedItem.Name, out var edit) && edit.Redo.Count > 0;

    [RelayCommand(CanExecute = nameof(CanUndoClass))]
    private void UndoClass()
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

    [RelayCommand(CanExecute = nameof(CanRedoClass))]
    private void RedoClass()
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

    /// <summary>Back to the class as the files have it</summary>
    [RelayCommand(CanExecute = nameof(IsChanged))]
    private void RevertClass()
    {
        if (SelectedItem is not { } item)
            return;
        _edits.Remove(item.Name);
        item.HasEdits = item.IsDraft;
        OpenFields(item);
        ShowEntry(keepState: true);
        Status = $"{item.Name} is as the files have it";
    }

    /// <summary>Saves the picked class into a mod: its own mod when one has it, else one picked</summary>
    [RelayCommand(CanExecute = nameof(IsChanged))]
    private async Task SaveClassAsync()
    {
        if (SelectedItem is { } item)
            await SaveOneAsync(item.Name);
    }

    /// <summary>Saves every class changed here; false (with what's wrong in Status) when one isn't</summary>
    public async Task<bool> SaveAllAsync()
    {
        foreach (var name in _edits.Where(edit => Changed(edit.Value)).Select(edit => edit.Key).ToList())
        {
            if (!await SaveOneAsync(name, reload: false))
                return false;
        }
        return true;
    }

    // The loaded mod whose actordefs have their own say in the class, the last one, or null
    private string? ModDefining(string name)
        => _classSet?.FilesDefining([name]).LastOrDefault(file => !file.IsBase)?.ModPath;

    // reload: the game's loaded again after (not when saving everything before a load anyway)
    private async Task<bool> SaveOneAsync(string name, bool reload = true)
    {
        if (_classSet is not { } set || !_edits.TryGetValue(name, out var edit) || _content == null)
            return false;
        var errors = new List<string>();
        var built = edit.Root.Build(errors);
        if (errors.Count > 0)
        {
            Status = $"{name} isn't saved: {string.Join(" ", errors)}";
            return false;
        }
        var mod = ModDefining(name) ?? (PickMod == null ? null : await PickMod($"Save the actor {name} into"));
        if (mod == null)
            return false;

        try
        {
            var write = set.PlanWrite([name], built, mod);
            var places = DefinitionSaver.Save(write).ToList();
            // A new class's map things go in with it
            if (_draftThings.TryGetValue(name, out var things) && things.Count > 0)
                places.AddRange(SaveThings(things, mod));
            _edits.Remove(name);
            _drafts.Remove(name);
            _draftThings.Remove(name);
            Status = $"Saved {name} in {string.Join(", ", places.Distinct())}" + (write.Notes.Count > 0 ? ". " + string.Join(" ", write.Notes) : "");
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

    // The mapdefs things written as one change to the things table, so they go in one file together
    private IEnumerable<string> SaveThings(List<(int Id, YamlMappingNode Thing)> things, string mod)
    {
        var mapdefs = DefinitionSet.Load(_content!, DefinitionKind.MapDefs);
        var table = YamlNodes.Clone(mapdefs.ValueOf(["things"], baseOnly: false) ?? new YamlMappingNode()) as YamlMappingNode ?? new YamlMappingNode();
        foreach (var (id, thing) in things)
            table.Children[new YamlScalarNode(id.ToString(CultureInfo.InvariantCulture))] = thing;
        return DefinitionSaver.Save(mapdefs.PlanWrite(["things"], table, mod));
    }

    private bool CanTakeOutClass => SelectedItem is { IsDraft: false } item && ModDefining(item.Name) != null;

    /// <summary>Takes the last mod's say in the class out of its actordefs, so it's what the game and the mods before make it</summary>
    [RelayCommand(CanExecute = nameof(CanTakeOutClass))]
    private async Task TakeOutClassAsync()
    {
        if (SelectedItem is not { } item || _classSet is not { } set || ModDefining(item.Name) is not { } mod)
            return;
        var modName = Path.GetFileName(mod.TrimEnd('\\', '/'));
        var after = set.FilesDefining([item.Name]).Any(file => file.IsBase) ? "the game's own comes back" : "the class is gone from the game";
        if (Confirm != null && !await Confirm($"Take out {item.Name}", $"Take {modName}'s {item.Name} out of its actordefs? Then {after}.", "Take out"))
            return;
        try
        {
            var places = DefinitionSaver.Save(set.PlanTakeOut([item.Name], mod));
            _edits.Remove(item.Name);
            Status = $"Took {item.Name} out of {string.Join(", ", places)}";
            AddMod?.Invoke(mod);
            Saved?.Invoke(this, mod);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Status = $"Not taken out: {e.Message}";
        }
    }

    /// <summary>A new class written as the picked one is (its own fields, the same parent): changing the picked one later leaves it be</summary>
    [RelayCommand(CanExecute = nameof(HasPicked))]
    private async Task NewCopyAsync()
    {
        if (SelectedItem is not { } item || !_edits.TryGetValue(item.Name, out var edit))
            return;
        var start = YamlNodes.Clone(edit.Root.Build([]) ?? new YamlMappingNode());
        await StartDraftAsync($"Copy {item.Name} as a new actor", start, item.Entry.Kind);
    }

    /// <summary>A new class built on the picked one (parent: it): it has all the picked one has, and changes what's set on it</summary>
    [RelayCommand(CanExecute = nameof(HasPicked))]
    private async Task NewChildAsync()
    {
        if (SelectedItem is not { } item)
            return;
        await StartDraftAsync($"New actor built on {item.Name}", new YamlMappingNode { { "parent", item.Name } }, item.Entry.Kind);
    }

    private async Task StartDraftAsync(string title, YamlNode start, ActorKind kind)
    {
        if (AskText == null || _content == null)
            return;
        var taken = _all.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = await AskText(title, "Its name (as mapdefs, other classes and scripts name it):", "",
            candidate => NameProblem(candidate, taken.Contains));
        if (name == null)
            return;
        name = name.Trim();

        // Map things to place it with: eight for an enemy (facing each way, standing and patrolling)
        int count = kind == ActorKind.Enemy ? 8 : 1;
        var free = FirstFreeThings(count);
        var answer = await AskText(title,
            count > 1
                ? $"The first of {count} mapdefs thing numbers to place it with (facing east, north, west, south; standing, then patrolling), or nothing for none:"
                : "A mapdefs thing number to place it with, or nothing for none:",
            free.ToString(CultureInfo.InvariantCulture), candidate => ThingsProblem(candidate, count));
        if (answer == null)
            return;

        _drafts[name] = start;
        if (int.TryParse(answer.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var first))
            _draftThings[name] = [.. Enumerable.Range(0, count).Select(i => (first + i, Thing(name, count > 1 ? i % 4 * 90 : 0, count > 1 && i >= 4)))];

        var item = DraftItem(_content, name);
        _all.Add(item);
        _all.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        Filter = "";
        SelectedKind = Kinds[0];
        Refilter();
        SelectedItem = item;
        Status = $"{name} is new: Save puts it in a mod" + (_draftThings.TryGetValue(name, out var things)
            ? $", with mapdefs things {ActorCatalog.Ranges([.. things.Select(thing => thing.Id)])}" : "");
    }

    private static YamlMappingNode Thing(string className, int angles, bool patrol)
    {
        var thing = new YamlMappingNode { { "class", className }, { "angles", angles.ToString(CultureInfo.InvariantCulture) } };
        if (patrol)
            thing.Add("patrol", "1");
        thing.Style = YamlDotNet.Core.Events.MappingStyle.Flow;
        return thing;
    }

    /// <summary>What's wrong with a name for a new class (none, a space or a quote in it, taken), or null when it'll do</summary>
    public static string? NameProblem(string candidate, Func<string, bool> taken)
    {
        var name = candidate.Trim();
        if (name.Length == 0 || name.Any(c => char.IsWhiteSpace(c) || c is ':' or '/' or '\\' or '#' or '"' or '\'' or '(' or ')' or ','))
            return "A name without spaces, colons, slashes, quotes, brackets, commas or #";
        return taken(name) ? $"There's a class {name} already" : null;
    }

    // Thing numbers the mapdefs and the new classes here already use
    private HashSet<int> UsedThings()
        => [.. _content?.MapDefs.Things.Keys ?? Enumerable.Empty<int>(), .. _draftThings.Values.SelectMany(things => things.Select(thing => thing.Id))];

    // The first of a run of free thing numbers, past the ones in use
    private int FirstFreeThings(int count)
    {
        var used = UsedThings();
        for (int id = 1000; ; id++)
            if (Enumerable.Range(id, count).All(n => !used.Contains(n)))
                return id;
    }

    private string? ThingsProblem(string candidate, int count)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return null;
        if (!int.TryParse(candidate.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var first) || first < 1 || first + count - 1 > ushort.MaxValue)
            return "A whole number from 1 (a value of the level's object plane), or nothing";
        var used = UsedThings();
        var taken = Enumerable.Range(first, count).Where(used.Contains).ToList();
        return taken.Count > 0 ? $"Taken already: {ActorCatalog.Ranges(taken)}" : null;
    }
}
