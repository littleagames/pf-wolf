using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Definitions;
using PFWolf.Editor.Data;
using PFWolf.Editor.Definitions;
using PFWolf.Fonts;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// The font window's style editing: a font's fonts.yaml fields (the Definitions window's rows),
/// drawn in the preview as they're changed, undone and redone, saved into a mod as just what
/// changed; new fonts and copies; a mod's change taken out
/// </summary>
public sealed partial class FontBrowserViewModel
{
    /// <summary>A font being edited: its fields, what it was opened as, and its steps</summary>
    private sealed class StyleEdit(FieldNode root, YamlNode? original)
    {
        public FieldNode Root { get; set; } = root;

        /// <summary>The definition as the files have it; null for a new font not saved yet</summary>
        public YamlNode? Original { get; } = original;

        public List<YamlNode?> Undo { get; } = [];
        public List<YamlNode?> Redo { get; } = [];

        /// <summary>The fields as last built: what an undo step goes back from</summary>
        public YamlNode? Last { get; set; }

        public DateTime LastStep { get; set; }
    }

    // Fonts opened for editing, by name, kept until saved or reverted
    private readonly Dictionary<string, StyleEdit> _edits = new(StringComparer.OrdinalIgnoreCase);
    // New fonts and copies not saved yet, by name: what they started as
    private readonly Dictionary<string, YamlNode> _drafts = new(StringComparer.OrdinalIgnoreCase);
    private DefinitionSet? _fontSet;
    private DefinitionSuggestions _suggestions = new(null);
    private FieldContext? _context;
    private Font? _editedFont;
    private bool _rebuilding;

    /// <summary>The picked font's fonts.yaml fields, as the Definitions window shows them</summary>
    public ObservableCollection<FieldNode> StyleRows { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveStyleCommand), nameof(RevertStyleCommand))]
    private bool _isStyleChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStyleError))]
    private string _styleError = "";

    public bool HasStyleError => StyleError.Length > 0;

    [ObservableProperty] private string _styleSource = "";

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

    /// <summary>The fonts changed here and not saved, as they're named to a person ("font HudMessage")</summary>
    public IReadOnlyList<string> UnsavedNames => [.. _edits.Where(edit => Changed(edit.Value)).Select(edit => $"font {edit.Key}")];

    private static bool Changed(StyleEdit edit) => !YamlNodes.Equal(edit.Root.Build([]), edit.Original);

    // Read again as the game loads: the fonts.yaml files, and what the fields can offer
    private void LoadStyles(GameContent content)
    {
        _fontSet = DefinitionSet.Load(content, DefinitionKind.Fonts);
        _suggestions = new DefinitionSuggestions(content);
        _context = new FieldContext
        {
            Suggestions = hint => _suggestions.For(hint),
            Changed = OnStyleChanged,
            AskKey = (title, prompt, check) => AskText?.Invoke(title, prompt, "", check) ?? Task.FromResult<string?>(null),
        };

        // Saved or unchanged: opened again from the files; changed ones keep their fields
        foreach (var name in _edits.Where(edit => !Changed(edit.Value)).Select(edit => edit.Key).ToList())
            _edits.Remove(name);
        foreach (var name in _drafts.Keys.Where(name => _fontSet.ValueOf([name], baseOnly: false) != null).ToList())
            _drafts.Remove(name);
    }

    // A font only this window has (new, not saved): listed with the game's
    private FontItem DraftItem(GameContent content, string name)
    {
        var value = _edits.TryGetValue(name, out var edit) ? edit.Root.Build([]) ?? _drafts[name] : _drafts[name];
        var definition = Read(value, out _);
        var entry = new FontEntry(name, definition?.Type?.ToLowerInvariant() ?? "vga", definition ?? new FontDefinition(), [],
            definition?.Type is null or "vga" ? (string.IsNullOrEmpty(definition?.Source) ? name : definition.Source) : null, [], []);
        var problems = new List<string> { "New: not saved in a mod yet" };
        var font = FontCatalog.BuildFont(content, entry, Canvas(1, 1), problems);
        return new FontItem(entry, font, problems) { IsDraft = true };
    }

    // A font's fields as the game reads them, or null (with why) when they don't read
    private static FontDefinition? Read(YamlNode? value, out string? problem)
    {
        problem = null;
        if (value is not YamlMappingNode mapping)
            return value == null ? null : new FontDefinition();
        try
        {
            return YamlDataEntryLoader.DeserializeQuietly<FontDefinition>(YamlTree.ToText(mapping));
        }
        catch (Exception e) when (e is YamlDotNet.Core.YamlException or InvalidOperationException or FormatException)
        {
            problem = e is YamlDotNet.Core.YamlException yaml ? DefinitionKinds.Describe(yaml) : e.Message;
            return null;
        }
    }

    // The fields of the font picked, opened (or as left) for editing
    private void OpenStyle(FontItem? item)
    {
        StyleRows.Clear();
        StyleError = "";
        IsStyleChanged = false;
        StyleSource = "";
        _editedFont = null;
        if (item == null || _fontSet == null || _context == null)
            return;

        var name = item.Name;
        if (!_edits.TryGetValue(name, out var edit))
        {
            var value = _fontSet.ValueOf([name], baseOnly: false) ?? _drafts.GetValueOrDefault(name);
            // A font asset nothing defines starts as the vga font of its own glyphs
            var start = value ?? new YamlMappingNode { { "type", FontSchema.Vga } };
            var original = _drafts.ContainsKey(name) ? null : value ?? YamlNodes.Clone(start);
            edit = new StyleEdit(NewRoot(name, start), original) { Last = YamlNodes.Clone(start) };
            _edits[name] = edit;
        }

        foreach (var row in edit.Root.Children)
            StyleRows.Add(row);
        var defining = _fontSet.FilesDefining([name]);
        StyleSource = defining.Count == 0 ? "Not saved in a mod yet"
            : defining.Count == 1 ? $"From {defining[0].Label}" : $"From {defining[^1].Label}, over {defining.Count - 1} more";
        UpdateStyle(edit, rebuildFont: Changed(edit));
    }

    // The fields of a value, with the ones its type doesn't read left out (unless they're set)
    private FieldNode NewRoot(string name, YamlNode? value)
    {
        var root = new FieldNode(DefinitionSchemas.Font, name, value == null ? null : YamlNodes.Clone(value),
            _fontSet?.ValueOf([name], baseOnly: true), null, _context!, isEntry: true);
        root.EnsureChildren();
        var type = (YamlNodes.Get(value, ["type"]) as YamlScalarNode)?.Value;
        root.HideUnset(field => !FontSchema.AppliesTo(field.Key, type));
        return root;
    }

    private void OnStyleChanged()
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

            // Another type reads other fields: shown again for it
            var typeBefore = (YamlNodes.Get(edit.Last, ["type"]) as YamlScalarNode)?.Value;
            var typeNow = (YamlNodes.Get(built, ["type"]) as YamlScalarNode)?.Value;
            edit.Last = built == null ? null : YamlNodes.Clone(built);
            if (!string.Equals(typeBefore, typeNow, StringComparison.OrdinalIgnoreCase))
            {
                Later(() => ShowFields(edit, built));
                return;
            }
        }
        UpdateStyle(edit, rebuildFont: true);
    }

    // Runs after the field that's changing has finished (rows can't be swapped under it)
    private static void Later(Action action)
    {
        if (Avalonia.Application.Current != null)
            Dispatcher.UIThread.Post(action);
        else
            action();
    }

    // The fields made again from a value (an undo, a redo, a new type)
    private void ShowFields(StyleEdit edit, YamlNode? value)
    {
        _rebuilding = true;
        try
        {
            edit.Root = NewRoot(SelectedItem?.Name ?? "", value);
            edit.Last = value == null ? null : YamlNodes.Clone(value);
            StyleRows.Clear();
            foreach (var row in edit.Root.Children)
                StyleRows.Add(row);
        }
        finally
        {
            _rebuilding = false;
        }
        UpdateStyle(edit, rebuildFont: true);
    }

    // What's changed, what's wrong, and the preview drawn with the fields as they are
    private void UpdateStyle(StyleEdit edit, bool rebuildFont)
    {
        var errors = new List<string>();
        var built = edit.Root.Build(errors);
        IsStyleChanged = !YamlNodes.Equal(built, edit.Original);
        if (SelectedItem != null)
            SelectedItem.HasEdits = IsStyleChanged;
        UndoStyleCommand.NotifyCanExecuteChanged();
        RedoStyleCommand.NotifyCanExecuteChanged();
        TakeOutStyleCommand.NotifyCanExecuteChanged();

        if (rebuildFont && _content != null && SelectedItem is { } item)
        {
            var definition = Read(built, out var problem);
            if (problem != null)
                errors.Add($"It doesn't read as a font: {problem}");
            var problems = new List<string>();
            var entry = item.Entry with { Definition = definition, Type = definition?.Type?.ToLowerInvariant() ?? item.Entry.Type };
            _editedFont = definition == null ? null : FontCatalog.BuildFont(_content, entry, Canvas(1, 1), problems);
            errors.AddRange(problems);
        }
        StyleError = string.Join("\n", errors);
        Redraw();
    }

    /// <summary>The font the preview draws: as edited here, or as the game has it</summary>
    private Font? PreviewFont => _editedFont ?? SelectedItem?.Font;

    private bool CanUndoStyle => SelectedItem != null && _edits.TryGetValue(SelectedItem.Name, out var edit) && edit.Undo.Count > 0;
    private bool CanRedoStyle => SelectedItem != null && _edits.TryGetValue(SelectedItem.Name, out var edit) && edit.Redo.Count > 0;

    [RelayCommand(CanExecute = nameof(CanUndoStyle))]
    private void UndoStyle()
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

    [RelayCommand(CanExecute = nameof(CanRedoStyle))]
    private void RedoStyle()
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

    /// <summary>Back to the font as the files have it</summary>
    [RelayCommand(CanExecute = nameof(IsStyleChanged))]
    private void RevertStyle()
    {
        if (SelectedItem is not { } item)
            return;
        _edits.Remove(item.Name);
        item.HasEdits = false;
        OpenStyle(item);
        Status = $"{item.Name} is as the files have it";
    }

    /// <summary>Saves the picked font into a mod: its own mod when one has it, else one picked</summary>
    [RelayCommand(CanExecute = nameof(IsStyleChanged))]
    private async Task SaveStyleAsync()
    {
        if (SelectedItem is { } item)
            await SaveFontAsync(item.Name);
    }

    /// <summary>Saves every font changed here; false (with what's wrong in Status) when one isn't</summary>
    public async Task<bool> SaveAllAsync()
    {
        foreach (var name in _edits.Where(edit => Changed(edit.Value)).Select(edit => edit.Key).ToList())
        {
            if (!await SaveFontAsync(name, reload: false))
                return false;
        }
        return true;
    }

    // The loaded mod whose fonts.yaml has its own say in the font, the last one, or null
    private string? ModDefining(string name)
        => _fontSet?.FilesDefining([name]).LastOrDefault(file => !file.IsBase)?.ModPath;

    // reload: the game's loaded again after (not when saving everything before a load anyway)
    private async Task<bool> SaveFontAsync(string name, bool reload = true)
    {
        if (_fontSet is not { } set || !_edits.TryGetValue(name, out var edit))
            return false;
        var errors = new List<string>();
        var built = edit.Root.Build(errors);
        if (errors.Count > 0)
        {
            Status = $"{name} isn't saved: {string.Join(" ", errors)}";
            return false;
        }
        var mod = ModDefining(name) ?? (PickMod == null ? null : await PickMod($"Save the font {name} into"));
        if (mod == null)
            return false;

        try
        {
            var write = set.PlanWrite([name], built, mod);
            var places = DefinitionSaver.Save(write);
            _edits.Remove(name);
            _drafts.Remove(name);
            Status = $"Saved {name} in {string.Join(", ", places)}" + (write.Notes.Count > 0 ? ". " + string.Join(" ", write.Notes) : "");
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

    private bool CanTakeOutStyle => SelectedItem is { IsDraft: false } item && ModDefining(item.Name) != null;

    /// <summary>Takes the last mod's say in the font out of its fonts.yaml, so it's what the game and the mods before make it</summary>
    [RelayCommand(CanExecute = nameof(CanTakeOutStyle))]
    private async Task TakeOutStyleAsync()
    {
        if (SelectedItem is not { } item || _fontSet is not { } set || ModDefining(item.Name) is not { } mod)
            return;
        var modName = Path.GetFileName(mod.TrimEnd('\\', '/'));
        var after = set.FilesDefining([item.Name]).Any(file => file.IsBase) ? "the game's own comes back" : "the font is gone from the game";
        if (Confirm != null && !await Confirm($"Take out {item.Name}", $"Take {modName}'s {item.Name} out of its fonts.yaml? Then {after}.", "Take out"))
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

    /// <summary>A new font, not saved yet: a copy of the one picked, or a vga font of the small font's letters</summary>
    [RelayCommand]
    private async Task NewFontAsync()
    {
        YamlNode start = SelectedItem is { } item && _edits.TryGetValue(item.Name, out var edit) && edit.Root.Build([]) is { } built
            ? YamlNodes.Clone(built)
            : new YamlMappingNode { { "type", FontSchema.Vga }, { "source", FontNames.Small } };
        // A font asset nothing defines: the copy is made from its glyphs
        if (SelectedItem is { Entry.IsDefined: false } asset)
            start = new YamlMappingNode { { "type", FontSchema.Vga }, { "source", asset.Name } };
        // A vga font's source defaults to its own name, which the copy hasn't got: it's named
        else if (SelectedItem is { Entry.SourceName: { } source } && start is YamlMappingNode copy && YamlNodes.Get(copy, ["source"]) == null
                 && FontSchema.AppliesTo("source", (YamlNodes.Get(copy, ["type"]) as YamlScalarNode)?.Value))
            copy.Add("source", source);
        await StartDraftAsync(SelectedItem == null ? "New font" : $"New font from {SelectedItem.Name}", start);
    }

    private async Task StartDraftAsync(string title, YamlNode start)
    {
        if (AskText == null || _content == null)
            return;
        var taken = _all.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = await AskText(title, "Its name (as menus, messages, the status bar and scripts name it):", "",
            candidate => FontCatalog.NameProblem(candidate, taken.Contains));
        if (name == null)
            return;
        name = name.Trim();
        _drafts[name] = start;
        var item = DraftItem(_content, name);
        _all.Add(item);
        _all.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        Filter = "";
        SelectedType = Types[0];
        Refilter();
        SelectedItem = item;
        Status = $"{name} is new: Save puts it in a mod";
    }

    /// <summary>Opens the picked font in the Definitions window's Fonts tab (its files, its YAML)</summary>
    [RelayCommand]
    private void EditInDefinitions()
    {
        if (SelectedItem is { IsDraft: false } item)
            GoTo?.Invoke(this, new FontUseTarget(DefinitionKind.Fonts, [item.Name]));
    }
}
