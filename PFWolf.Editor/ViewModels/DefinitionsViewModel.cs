using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Editor.Data;
using PFWolf.Editor.Definitions;

namespace PFWolf.Editor.ViewModels;

/// <summary>Where the definitions window saves: a mod (a folder or a pk3), shown by its name</summary>
public sealed record SaveTarget(string Path)
{
    public string Label => $"{System.IO.Path.GetFileName(Path.TrimEnd('\\', '/'))}  ({(Editing.ModFiles.IsArchive(Path) ? "pk3" : "folder")})";
    public override string ToString() => Label;
}

/// <summary>
/// The definitions window: the game's game-info, game packs, actor classes and map definitions,
/// each in fields, and every YAML file as text. Changes are saved into a mod as just what they
/// change: pfwolf.pk3's own files are read-only.
/// </summary>
public sealed partial class DefinitionsViewModel : ObservableObject
{
    private readonly Dictionary<DefinitionKind, DefinitionSet> _sets = [];

    public DefinitionsViewModel()
    {
        Files = new YamlFilesViewModel(this);
        GameInfo = new EntryEditorViewModel(this, DefinitionKind.GameInfo);
        GamePacks = new EntryEditorViewModel(this, DefinitionKind.GamePackInfo);
        Actors = new EntryEditorViewModel(this, DefinitionKind.Actors);
        MapDefs = new EntryEditorViewModel(this, DefinitionKind.MapDefs);
        Animations = new EntryEditorViewModel(this, DefinitionKind.Animations);
    }

    /// <summary>Asks for a mod (a folder or a pk3) to save in; null when none is picked</summary>
    public Func<string, Task<string?>>? PickMod { get; set; }

    /// <summary>Asks for a line of text (title, prompt, initial, check); null when cancelled</summary>
    public Func<string, string, string, Func<string, string?>, Task<string?>>? AskText { get; set; }

    /// <summary>A picture for a name: a texture's, or a class's sprite (FieldHint.Class); null when there's none</summary>
    public Func<FieldHint, string, Avalonia.Media.Imaging.Bitmap?>? Art { get; set; }

    public EntryEditorViewModel GameInfo { get; }
    public EntryEditorViewModel GamePacks { get; }
    public EntryEditorViewModel Actors { get; }
    public EntryEditorViewModel MapDefs { get; }
    public EntryEditorViewModel Animations { get; }

    /// <summary>Something was saved into this mod (the editor adds it to the mods)</summary>
    public event EventHandler<string>? SavedToMod;

    /// <summary>Asks the editor to load the game again, so the levels see what was saved</summary>
    public event EventHandler? ReloadRequested;

    /// <summary>Asks the window to show the YAML tab</summary>
    public event EventHandler? ShowFilesRequested;

    public GameContent? Content { get; private set; }

    public YamlFilesViewModel Files { get; }

    /// <summary>The mods loaded, which changes can be saved into</summary>
    public ObservableCollection<SaveTarget> SaveTargets { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetText))]
    private SaveTarget? _saveTarget;

    [ObservableProperty] private string _title = "Definitions";
    [ObservableProperty] private string _status = "";

    /// <summary>Something's been saved that the levels, palette and game loaded don't have until it's loaded again</summary>
    [ObservableProperty] private bool _needsReload;

    public string TargetText => SaveTarget != null
        ? $"Saving into {SaveTarget.Path} (pfwolf.pk3 is read-only)"
        : "Saving into a mod picked when it's saved (pfwolf.pk3 is read-only)";

    /// <summary>A game was loaded (or loaded again, with what was saved)</summary>
    public void Load(GameContent? content, IReadOnlyList<string> mods)
    {
        Content = content;
        _sets.Clear();
        NeedsReload = false;
        Status = "";
        Title = content == null ? "Definitions" : $"Definitions - {content.Title}";
        SetMods(mods);
        Files.Refresh();
        Reloaded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The game was loaded again: the tabs list what it has now</summary>
    public event EventHandler? Reloaded;

    /// <summary>The editor's mods changed: the ones changes can be saved into</summary>
    public void SetMods(IReadOnlyList<string> mods)
    {
        var picked = SaveTarget?.Path;
        SaveTargets.Clear();
        foreach (var mod in mods.Where(mod => Editing.ModFiles.WhyReadOnly(mod) == null))
            SaveTargets.Add(new SaveTarget(mod));
        SaveTarget = SaveTargets.FirstOrDefault(target => DefinitionFile.SameMod(target.Path, picked)) ?? SaveTargets.LastOrDefault();
    }

    /// <summary>A kind's files as they are now (read again after a save)</summary>
    public DefinitionSet? Set(DefinitionKind kind)
    {
        if (Content == null || string.IsNullOrEmpty(Content.GameFolder))
            return null;
        if (!_sets.TryGetValue(kind, out var set))
            _sets[kind] = set = DefinitionSet.Load(Content, kind);
        return set;
    }

    /// <summary>Picks another mod to save into</summary>
    [RelayCommand]
    private async Task PickSaveTargetAsync()
    {
        if (PickMod == null || await PickMod("A mod to save definitions in") is not { } path)
            return;
        if (Editing.ModFiles.WhyReadOnly(path) is { } why)
        {
            Status = $"Not picked: {why}.";
            return;
        }
        if (SaveTargets.FirstOrDefault(target => DefinitionFile.SameMod(target.Path, path)) is not { } target)
            SaveTargets.Add(target = new SaveTarget(path));
        SaveTarget = target;
    }

    /// <summary>The mod changes go into, asking for one when none is picked; null when none is</summary>
    public async Task<string?> ModToSaveIn()
    {
        if (SaveTarget == null)
            await PickSaveTargetAsync();
        return SaveTarget?.Path;
    }

    [RelayCommand]
    private void Reload() => ReloadRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// A file was saved: the kinds read it again, and the editor's told (a mod it hadn't loaded is
    /// added to its mods)
    /// </summary>
    public void FileSaved(string? modPath, IReadOnlyList<string> places, IEnumerable<string> notes)
    {
        _sets.Clear();
        NeedsReload = true;
        Status = $"Saved {string.Join(" and ", places)}." + string.Concat(notes.Select(note => " " + note + "."));
        if (modPath != null)
            SavedToMod?.Invoke(this, modPath);
        DefinitionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The definitions were saved: the tabs show them again</summary>
    public event EventHandler? DefinitionsChanged;

    private IEnumerable<EntryEditorViewModel> Editors => [GameInfo, Actors, MapDefs, Animations, GamePacks];

    /// <summary>
    /// What's changed here and not saved (definitions in the tabs, the file in the YAML tab), as
    /// it's named to a person: loading the game again, or closing the editor, would lose it
    /// </summary>
    public IReadOnlyList<string> UnsavedNames
        => [.. Editors.SelectMany(editor => editor.UnsavedNames), .. Files.UnsavedName is { } file ? [file] : Array.Empty<string>()];

    /// <summary>Saves everything changed here; false when something isn't saved (the status says why)</summary>
    public async Task<bool> SaveAllAsync()
    {
        foreach (var editor in Editors)
        {
            if (!await editor.SaveAllAsync())
                return false;
        }
        return Files.UnsavedName == null || Files.SaveChanges();
    }

    /// <summary>Asks the window to show a kind's tab</summary>
    public event EventHandler<DefinitionKind>? ShowTabRequested;

    /// <summary>Shows a definition in its tab</summary>
    public void ShowEntry(DefinitionKind kind, IReadOnlyList<string> path)
    {
        var editor = kind switch
        {
            DefinitionKind.GameInfo => GameInfo,
            DefinitionKind.GamePackInfo => GamePacks,
            DefinitionKind.Actors => Actors,
            DefinitionKind.Animations => Animations,
            _ => MapDefs,
        };
        ShowTabRequested?.Invoke(this, kind);
        editor.Activate();
        if (!editor.Select(path))
            Status = $"There's no {YamlNodes.Describe(path)} in the {DefinitionKinds.Title(kind).ToLowerInvariant()}";
    }

    /// <summary>Shows a file in the YAML tab, at a definition's line</summary>
    public void ShowYaml(DefinitionFile file, IReadOnlyList<string> path)
    {
        Files.Show(file.ModPath, file.EntryPath, path);
        ShowFilesRequested?.Invoke(this, EventArgs.Empty);
    }
}
