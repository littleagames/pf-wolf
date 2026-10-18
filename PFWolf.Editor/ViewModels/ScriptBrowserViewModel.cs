using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Editor.Data;

namespace PFWolf.Editor.ViewModels;

/// <summary>A script as the script browser lists it</summary>
public sealed class ScriptListItem(ScriptEntry entry)
{
    public ScriptEntry Entry { get; } = entry;
    public string Name => Entry.Name;

    /// <summary>Where it comes from, for under its name</summary>
    public string SourceText => Entry.Source;

    public string ToolTip => $"{Entry.EntryPath} in {Entry.Source}" + (Entry.Origins.Count > 1 ? $"\n{string.Join("\n", Entry.Origins)}" : "");
}

/// <summary>
/// The script browser: every Lua script the game runs (scripts/*.lua in pfwolf.pk3 and the mods),
/// in the order it runs them, with the actions and hooks each gives, which actordefs states call
/// those actions, and what's wrong with it, checked as it's typed. A script can be changed, or a
/// new one made, and saved to a mod's scripts folder; the game runs it the next time it starts.
/// </summary>
public sealed partial class ScriptBrowserViewModel : ObservableObject
{
    private List<ScriptListItem> _all = [];
    private GameContent? _content;
    private string _original = "";
    // Set while the text shown is swapped for another's, so it isn't taken for an edit
    private bool _loadingSource;
    // Scripts saved, by name: the text, the mod and the path in it. The game loaded still has the
    // old ones until it's loaded again
    private readonly Dictionary<string, (string Text, string Folder, string EntryPath)> _saved = new(StringComparer.OrdinalIgnoreCase);
    // Each action's callers in actordefs, and the scripts that define it (in the order they run)
    private Dictionary<string, List<CallerSite>> _callers = [];
    private Dictionary<string, List<string>> _definedIn = [];

    /// <summary>Asks for a mod (a folder or a pk3) to save in; null when none is picked</summary>
    public Func<string, Task<string?>>? PickMod { get; set; }

    /// <summary>Asks for a line of text (title, prompt, initial, check); null when cancelled</summary>
    public Func<string, string, string, Func<string, string?>, Task<string?>>? AskText { get; set; }

    /// <summary>Asks a yes-or-no question (title, message, the yes button's label)</summary>
    public Func<string, string, string, Task<bool>>? Confirm { get; set; }

    /// <summary>A script was saved into this mod (the editor adds it to the mods)</summary>
    public event EventHandler<string>? Saved;

    /// <summary>A script was taken out of its mod (the editor loads the game again)</summary>
    public event EventHandler? Deleted;

    /// <summary>Asks the window to select a line of the script (1 for the first): a problem or an action picked</summary>
    public event EventHandler<int>? SelectLine;

    public ObservableCollection<ScriptListItem> Items { get; } = [];
    public ObservableCollection<string> Origins { get; } = [];
    public ObservableCollection<ScriptActionRow> Actions { get; } = [];
    public ObservableCollection<ScriptItem> Hooks { get; } = [];
    public ObservableCollection<ScriptProblem> Problems { get; } = [];

    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private string _countText = "";
    [ObservableProperty] private string _title = "Scripts";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _summary = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(SaveToCommand), nameof(DeleteCommand))]
    private ScriptListItem? _selectedItem;

    /// <summary>The script shown, as changed in the box</summary>
    [ObservableProperty] private string _source = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevertCommand))]
    private bool _isChanged;

    public bool HasSelection => SelectedItem != null;
    public bool HasActions => Actions.Count > 0;
    public bool HasHooks => Hooks.Count > 0;
    public bool HasProblems => Problems.Count > 0;
    public bool HasNoScripts => _all.Count == 0;

    /// <summary>Lists the scripts of a newly loaded game, keeping the one picked if it's still there</summary>
    public void Load(GameContent? content)
    {
        _content = content;
        var picked = SelectedItem?.Name;
        // What was saved is in the game now, as long as its mod is loaded; what was new and unsaved is gone
        _saved.Clear();
        _drafts.Clear();
        _back.Clear();
        Status = "";

        if (content == null)
        {
            _all = [];
            _callers = [];
            _completion = CompletionSources.Empty;
            Title = "Scripts";
        }
        else
        {
            _all = ScriptCatalog.Build(content).Select(entry => new ScriptListItem(entry)).ToList();
            _callers = ScriptCatalog.CallerSites(content);
            _completion = CompletionSources.From(content);
            Title = $"Scripts - {content.Title}";
        }
        FindDefinitions();
        OnPropertyChanged(nameof(HasNoScripts));

        Refilter();
        SelectedItem = picked != null ? Items.FirstOrDefault(item => item.Name.Equals(picked, StringComparison.OrdinalIgnoreCase)) : null;
        SelectedItem ??= Items.FirstOrDefault();
    }

    // Which scripts define each action, in the order they run (the last one is the game's)
    private void FindDefinitions()
    {
        _definedIn = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var item in _all)
        {
            var text = TextOf(item.Name);
            foreach (var action in ScriptCatalog.Check(text, item.Name).Actions.Select(action => action.Name).Distinct())
            {
                if (!_definedIn.TryGetValue(action, out var list))
                    _definedIn[action] = list = [];
                list.Add(item.Name);
            }
        }
    }

    private string TextOf(string name)
        => _saved.TryGetValue(name, out var saved) ? saved.Text : _content == null ? "" : ScriptCatalog.Read(_content, name) ?? "";

    partial void OnFilterChanged(string value) => Refilter();

    private void Refilter()
    {
        var selected = SelectedItem;
        var words = Filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Items.Clear();
        foreach (var item in _all.Where(item => words.All(word => item.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                     || item.Entry.Source.Contains(word, StringComparison.OrdinalIgnoreCase)
                     || _definedIn.Any(defined => defined.Value.Contains(item.Name) && defined.Key.Contains(word, StringComparison.OrdinalIgnoreCase)))))
            Items.Add(item);
        CountText = Items.Count == _all.Count ? $"{Items.Count} script{(Items.Count == 1 ? "" : "s")}" : $"{Items.Count} of {_all.Count} scripts";
        SelectedItem = selected != null && Items.Contains(selected) ? selected : Items.FirstOrDefault();
    }

    // Scripts changed and not saved, by name, kept while another is shown (picking another script
    // in the list, or going to a definition in one)
    private readonly Dictionary<string, string> _drafts = new(StringComparer.OrdinalIgnoreCase);

    partial void OnSelectedItemChanging(ScriptListItem? oldValue, ScriptListItem? newValue)
    {
        if (oldValue != null && IsChanged)
            _drafts[oldValue.Name] = Source;
    }

    partial void OnSelectedItemChanged(ScriptListItem? value)
    {
        Origins.Clear();
        if (value != null)
            foreach (var origin in value.Entry.Origins)
                Origins.Add(origin);

        _original = value == null ? "" : TextOf(value.Name);
        var draft = value != null && _drafts.Remove(value.Name, out var kept) ? kept : null;
        _loadingSource = true;
        try
        {
            Source = draft ?? _original;
        }
        finally
        {
            _loadingSource = false;
        }
        IsChanged = draft != null && draft != _original;
        Recheck();
    }

    /// <summary>Whether a script has changes not saved, shown or not</summary>
    public bool HasUnsavedChanges => IsChanged || _drafts.Count > 0;

    //
    // Go to callers
    //

    /// <summary>Asks the editor to show an actordefs state that calls an action, in the Definitions window</summary>
    public event EventHandler<CallerSite>? ShowCaller;

    /// <summary>Shows an actordefs state that calls an action, in the Definitions window (at the frame that calls it)</summary>
    [RelayCommand]
    private void GoToCaller(CallerSite? caller)
    {
        if (caller == null)
            return;
        ShowCaller?.Invoke(this, caller);
        Status = $"{caller.Label}, frame {caller.Frame + 1}: in the Definitions window";
    }

    /// <summary>
    /// The actordefs states that call the action at <paramref name="column"/> of <paramref name="line"/>
    /// (Shift+F12), with its name; null, with the status saying why, when there's none to go to
    /// </summary>
    public (string Name, IReadOnlyList<CallerSite> Callers)? CallersAt(string line, int column)
    {
        if (ScriptNavigation.NameAt(line, column) is not { } name || !name.StartsWith("A_", StringComparison.Ordinal) && !name.StartsWith("T_", StringComparison.Ordinal))
        {
            Status = "Put the caret on an action's name (A_..., T_...) to find the actordefs states that call it";
            return null;
        }
        var callers = _callers.GetValueOrDefault(name, []);
        if (callers.Count == 0)
        {
            Status = $"No actordefs state calls {name}";
            return null;
        }
        return (name, callers);
    }

    //
    // Go to definition
    //

    // Where go-to-definition came from, to go back to (Alt+Left): the script and line
    private readonly Stack<(string Script, int Line)> _back = new();

    /// <summary>
    /// Goes to where the name at <paramref name="column"/> of <paramref name="line"/> is defined (F12,
    /// Ctrl+click): an action's script function, the one the game runs, in whichever script that
    /// is; another function's in this script. <paramref name="fromLine"/> is the line it's asked
    /// from, to come back to. The status says what happened.
    /// </summary>
    public bool GoToDefinition(string line, int column, int fromLine)
    {
        if (SelectedItem is not { } current)
            return false;
        if (ScriptNavigation.NameAt(line, column) is not { } name)
        {
            Status = "Put the caret on an action's name (A_..., T_...) or a function's to go to its definition";
            return false;
        }

        var scripts = _all.Select(item => new ScriptSource(item.Name, item.Entry.Order,
            item == current ? Source : _drafts.TryGetValue(item.Name, out var draft) ? draft : TextOf(item.Name))).ToList();
        if (ScriptNavigation.Find(name, current.Name, scripts, out var why) is not { } target)
        {
            Status = why;
            return false;
        }

        _back.Push((current.Name, fromLine));
        GoTo(target.Script, target.Line);
        Status = target.Note ?? $"{name}: {target.Script}, line {target.Line} (Alt+Left goes back)";
        return true;
    }

    /// <summary>Back to where the last go-to-definition came from (Alt+Left)</summary>
    public bool GoBack()
    {
        if (!_back.TryPop(out var from))
            return false;
        GoTo(from.Script, from.Line);
        Status = "";
        return true;
    }

    // Shows a script (keeping the one shown's changes) and selects a line of it
    private void GoTo(string script, int line)
    {
        if (!SelectedItem?.Name.Equals(script, StringComparison.OrdinalIgnoreCase) ?? true)
        {
            var item = _all.FirstOrDefault(i => i.Name.Equals(script, StringComparison.OrdinalIgnoreCase));
            if (item == null)
                return;
            if (!Items.Contains(item))
                Filter = "";
            SelectedItem = item;
        }
        SelectLine?.Invoke(this, line);
    }

    partial void OnSourceChanged(string value)
    {
        if (_loadingSource)
            return;
        IsChanged = value != _original;
        Recheck();
    }

    // Checks the script as it stands in the box
    private void Recheck()
    {
        Actions.Clear();
        Hooks.Clear();
        Problems.Clear();
        if (SelectedItem is not { } item)
        {
            Summary = "";
            NotifyLists();
            return;
        }

        var report = ScriptCatalog.Check(Source, item.Entry.EntryPath);
        foreach (var action in report.Actions)
        {
            var callers = _callers.GetValueOrDefault(action.Name, []);
            var others = _definedIn.GetValueOrDefault(action.Name, []).Where(name => !name.Equals(item.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            Actions.Add(new ScriptActionRow(action.Name, action.Line, callers, Overridden(item, others)));
        }
        foreach (var hook in report.Hooks)
            Hooks.Add(hook);
        foreach (var problem in report.Problems)
            Problems.Add(problem);

        int errors = report.Problems.Count(problem => problem.IsError);
        int warnings = report.Problems.Count - errors;
        Summary = (errors > 0 ? "Doesn't compile: the game leaves it out" : "Compiles")
            + $"; {report.Actions.Count} action{(report.Actions.Count == 1 ? "" : "s")}, {report.Hooks.Count} hook{(report.Hooks.Count == 1 ? "" : "s")}"
            + (warnings > 0 ? $", {warnings} warning{(warnings == 1 ? "" : "s")}" : "");
        NotifyLists();
    }

    // Another script that defines the same action: the one that runs last is the game's
    private string? Overridden(ScriptListItem item, List<string> others)
    {
        if (others.Count == 0)
            return null;
        var order = _all.ToDictionary(i => i.Name, i => i.Entry.Order, StringComparer.OrdinalIgnoreCase);
        int mine = item.Entry.Order;
        var later = others.Where(name => order.GetValueOrDefault(name) > mine).ToList();
        return later.Count > 0
            ? $"{string.Join(", ", later)} defines it too, and runs later: the game uses that one"
            : $"{string.Join(", ", others)} defines it too, but this one runs later: the game uses this one";
    }

    private void NotifyLists()
    {
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(HasHooks));
        OnPropertyChanged(nameof(HasProblems));
    }

    // The game's names the autocomplete offers (actor classes, states...)
    private CompletionSources _completion = CompletionSources.Empty;

    /// <summary>
    /// What the autocomplete offers with the caret after <paramref name="before"/> (its line up to
    /// it): the game API, Lua, the actions there are (this script's as it stands too), and in
    /// strings the game's names; null for nothing. <paramref name="asked"/>: Ctrl+Space.
    /// </summary>
    public CompletionContext? Complete(string before, bool asked)
        => ScriptCompletion.Find(before, _completion, Actions.Select(action => action.Name), asked);

    /// <summary>
    /// What the word at <paramref name="column"/> (from 0) of <paramref name="line"/> is, for the
    /// tooltip over it; null when it's nothing of the game's or Lua's
    /// </summary>
    public HoverInfo? Describe(string line, int column)
    {
        var info = ScriptHover.Describe(line, column, _completion, Actions.Select(action => new ScriptItem(action.Name, action.Line)));
        // An action a script defines can be gone to
        if (info is { Kind: "action" } && (_definedIn.ContainsKey(info.Signature) || Actions.Any(action => action.Name == info.Signature)))
            info = info with { Summary = info.Summary + ". F12 or Ctrl+click goes to its definition" };
        return info;
    }

    /// <summary>Goes to a line of the script: a problem, action or hook picked</summary>
    public void GoTo(int line) => SelectLine?.Invoke(this, line);

    /// <summary>Back to the script as the game has it (or as it was last saved)</summary>
    [RelayCommand(CanExecute = nameof(IsChanged))]
    private void Revert() => Source = _original;

    //
    // Saving
    //

    /// <summary>Saves the script to its mod: the one it comes from or was saved to (asking for one the first time)</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task SaveAsync()
    {
        if (SelectedItem is { } item)
        {
            var (folder, entryPath) = SaveTargetOf(item);
            await SaveScript(item, folder, entryPath);
        }
    }

    /// <summary>Saves the script to a mod picked now</summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task SaveToAsync()
    {
        if (SelectedItem is { } item)
            await SaveScript(item, null, null);
    }

    // The mod and path a script was saved to, else those it comes from (no mod for pfwolf.pk3's)
    private (string? Folder, string? EntryPath) SaveTargetOf(ScriptListItem item)
        => _saved.TryGetValue(item.Name, out var saved) ? (saved.Folder, saved.EntryPath)
            : (_content?.ModPathOf(item.Name, nameof(ScriptAsset)), item.Entry.EntryPath);

    /// <summary>Saves a script to {folder}/scripts/..., asking for a folder when there's none; false when it isn't saved</summary>
    public async Task<bool> SaveScript(ScriptListItem item, string? folder, string? entryPath)
    {
        var from = SaveTargetOf(item);
        folder ??= PickMod == null ? null : await PickMod($"A mod to save {item.Name}.lua in (it goes in its scripts folder)");
        if (folder == null)
            return false;
        // Back where it came from in its own mod; otherwise scripts/NAME.lua
        entryPath ??= from.Folder != null && Path.GetFullPath(from.Folder) == Path.GetFullPath(folder) && from.EntryPath != null
            ? from.EntryPath
            : ScriptCatalog.NewEntryPath(item.Name);

        var text = Source;
        try
        {
            var path = ScriptCatalog.Save(folder, entryPath, text);
            _saved[item.Name] = (text, folder, entryPath);
            DeleteCommand.NotifyCanExecuteChanged();
            _original = text;
            IsChanged = false;
            FindDefinitions();
            Recheck();
            Status = $"Saved {path}. The game runs scripts as it starts: Play (F5) to try it"
                + (from.Folder == null && item.Entry.Source.Length > 0 && item.Entry.Source != "new"
                    ? $"; this {item.Name}.lua is used in place of {item.Entry.Source}'s"
                    : "");
            Saved?.Invoke(this, folder);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = $"Couldn't save {item.Name}: {e.Message}";
            return false;
        }
    }

    // A script made here and never saved: in no mod yet
    private bool IsUnsavedNew(ScriptListItem item) => item.Entry.Source == "new" && !_saved.ContainsKey(item.Name);

    /// <summary>A script picked that's a mod's (pfwolf.pk3's are read-only), or one new here</summary>
    private bool CanDelete => SelectedItem is { } item && (IsUnsavedNew(item) || SaveTargetOf(item).Folder != null);

    /// <summary>Takes the script picked out of its mod (one new here and never saved is just dropped)</summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (SelectedItem is not { } item)
            return;
        if (IsUnsavedNew(item))
        {
            _all.Remove(item);
            _drafts.Remove(item.Name);
            IsChanged = false;
            OnPropertyChanged(nameof(HasNoScripts));
            Refilter();
            Status = $"Dropped {item.Name}.lua, which was never saved";
            return;
        }

        var (folder, entryPath) = SaveTargetOf(item);
        if (folder == null || entryPath == null || Confirm == null)
            return;
        var after = item.Entry.Origins.Count > 1 ? $"The {item.Name}.lua it was used in place of runs again."
            : Actions.Any(action => action.HasCallers) ? "Actordefs states call its actions, and will find nothing to run."
            : "Nothing calls its actions.";
        if (!await Confirm($"Delete {item.Name}.lua", Editing.ModFiles.DeleteQuestion(folder, entryPath, after), "Delete"))
            return;
        try
        {
            Editing.ModFiles.Delete(folder, entryPath);
            _saved.Remove(item.Name);
            _drafts.Remove(item.Name);
            Status = $"Deleted {entryPath} from {Path.GetFileName(folder.TrimEnd('\\', '/'))}";
            Deleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = $"Couldn't delete {item.Name}: {e.Message}";
        }
    }

    /// <summary>Starts a new script, by a name asked for, from one to fill in: saving it adds it to a mod</summary>
    [RelayCommand]
    private async Task NewScriptAsync()
    {
        if (_content is not { } content || AskText == null)
            return;

        var name = await AskText("New script", "Its name, which is its file in scripts/ (name.lua):",
            "", candidate => ScriptCatalog.CheckNewName(content, candidate.Trim())
                ?? (_all.Any(item => item.Name.Equals(candidate.Trim(), StringComparison.OrdinalIgnoreCase)) ? $"There's a {candidate.Trim().ToLowerInvariant()} already" : null));
        if (string.IsNullOrWhiteSpace(name))
            return;

        name = name.Trim().ToLowerInvariant();
        var entry = new ScriptEntry(name, (_all.Count == 0 ? 0 : _all.Max(item => item.Entry.Order)) + 1, ["new: not saved yet"])
        {
            Source = "new",
            EntryPath = ScriptCatalog.NewEntryPath(name),
        };
        var item = new ScriptListItem(entry);
        _all.Add(item);
        OnPropertyChanged(nameof(HasNoScripts));
        Filter = "";
        Refilter();
        SelectedItem = item;
        Source = ScriptCatalog.NewScript;
        Status = $"{name}.lua is new: Save puts it in a mod's scripts folder";
    }
}

/// <summary>An action a script defines, with the actordefs states that call it</summary>
public sealed record ScriptActionRow(string Name, int Line, IReadOnlyList<CallerSite> Callers, string? Overridden)
{
    public string CallersText => Callers.Count == 0
        ? "No actordefs state calls it"
        : $"Called by {string.Join(", ", Callers.Select(caller => caller.Label))}";

    public bool HasCallers => Callers.Count > 0;

    public bool IsOverridden => Overridden != null;
}
