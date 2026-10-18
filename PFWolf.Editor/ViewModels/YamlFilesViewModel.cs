using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Editor.Definitions;
using PFWolf.Loaders;
using PFWolf.Managers;

namespace PFWolf.Editor.ViewModels;

/// <summary>A YAML file in pfwolf.pk3 or a mod, as the YAML tab lists it</summary>
public sealed partial class YamlFileItem : ObservableObject
{
    public YamlFileItem(string? modPath, string entryPath, string sourceName)
    {
        ModPath = modPath;
        EntryPath = entryPath;
        SourceName = sourceName;
        What = YamlFileTypes.Describe(entryPath, isMod: modPath != null);
    }

    /// <summary>The mod it's in, or null for pfwolf.pk3's own</summary>
    public string? ModPath { get; }
    public string EntryPath { get; }
    public string SourceName { get; }
    public bool IsBase => ModPath == null;

    /// <summary>What the game reads it as ("actor classes"), or null for a file it doesn't read</summary>
    public string? What { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(ToolTip))]
    private string? _error;

    public bool HasError => Error != null;

    public string ToolTip => $"{SourceName}: {EntryPath}" + (What != null ? $"\n{char.ToUpperInvariant(What[0])}{What[1..]}" : "\nNot a file the game reads")
                             + (Error != null ? $"\n\n{Error}" : "");

    public bool Is(string? modPath, string entryPath)
        => DefinitionFile.SameMod(ModPath, modPath) && EntryPath.Equals(entryPath, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The YAML tab: every YAML file in pfwolf.pk3 and the mods, each checked as the game reads it,
/// and one at a time as text to fix or change. pfwolf.pk3's own are read-only (Copy to mod makes a mod's own).
/// </summary>
public sealed partial class YamlFilesViewModel : ObservableObject
{
    private readonly DefinitionsViewModel _owner;
    private List<YamlFileItem> _all = [];
    // Each file's text as read (or saved), so it isn't read again
    private readonly Dictionary<YamlFileItem, string> _texts = [];
    private string _original = "";
    // Set while the text shown is swapped for another file's, so it isn't taken for an edit
    private bool _loadingSource;
    private readonly DispatcherTimer _checkTimer;
    private int _refreshes;

    public YamlFilesViewModel(DefinitionsViewModel owner)
    {
        _owner = owner;
        _checkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _checkTimer.Tick += (_, _) =>
        {
            _checkTimer.Stop();
            CheckSource();
        };
        owner.DefinitionsChanged += (_, _) => Refresh();
    }

    public ObservableCollection<YamlFileItem> Items { get; } = [];

    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private bool _showBase = true;
    [ObservableProperty] private bool _onlyProblems;
    [ObservableProperty] private string _countText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(IsReadOnly), nameof(ReadOnlyText), nameof(FileTitle), nameof(FileWhat),
        nameof(CanCopyToMod), nameof(CopyToModText), nameof(IsBaseFile))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(DeleteCommand))]
    private YamlFileItem? _selectedItem;

    /// <summary>The file's text, as changed in the box</summary>
    [ObservableProperty] private string _source = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(RevertCommand))]
    private bool _isChanged;

    /// <summary>What's wrong with the text as it stands, or empty</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    private string _problem = "";

    /// <summary>The line (from 1) the problem is on, or 0</summary>
    [ObservableProperty] private int _problemLine;

    /// <summary>A note about the file</summary>
    [ObservableProperty] private string _note = "";

    /// <summary>Where the caret is ("Line 12, column 4"), which the window keeps up to date</summary>
    [ObservableProperty] private string _caretText = "";

    public bool HasSelection => SelectedItem != null;
    public bool HasProblem => Problem.Length > 0;
    /// <summary>pfwolf.pk3's own files are read-only: a mod changes them</summary>
    public bool IsReadOnly => SelectedItem is { IsBase: true };

    public string ReadOnlyText => !IsReadOnly ? ""
        : CanCopyToMod ? "pfwolf.pk3's own file, which is read-only: change what it defines with the other tabs, or Copy to mod for a mod's own to change"
        : $"pfwolf.pk3's own file, which is read-only. It can't be copied to a mod: {LowerFirst(CopyToModText.TrimEnd('.'))}.";

    private static string LowerFirst(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

    public string FileTitle => SelectedItem == null ? "" : $"{SelectedItem.SourceName}: {SelectedItem.EntryPath}";

    public string FileWhat => SelectedItem == null ? ""
        : SelectedItem.What is { } what ? $"The game reads it as {what}" + (SelectedItem.IsBase ? "" : $" (as {YamlFileTypes.LoadedPath(SelectedItem.EntryPath, true, _owner.Content?.PackId ?? "pack")} would be)")
        : "The game doesn't read this file";

    /// <summary>Asks the window to put the caret at the start of a line (from 1) and show it</summary>
    public event EventHandler<int>? GoToLineRequested;

    /// <summary>Lists the files again (the game was loaded again, or something was saved), keeping the one picked</summary>
    public void Refresh()
    {
        var picked = SelectedItem;
        var keepEdit = IsChanged ? Source : null;
        _texts.Clear();
        lock (_sources)
            _sources.Clear();
        _all = [];
        var content = _owner.Content;
        if (content != null && !string.IsNullOrEmpty(content.GameFolder))
        {
            var basePk3 = Path.Combine(content.GameFolder, AssetManager.BasePk3FileName);
            if (File.Exists(basePk3))
                _all.AddRange(new Pk3AssetSource(basePk3).EntryPaths.Where(YamlFileTypes.IsYaml).Order(StringComparer.OrdinalIgnoreCase)
                    .Select(path => new YamlFileItem(null, path, AssetManager.BasePk3FileName)));
            foreach (var mod in content.Assets.LoadedMods.Select(mod => mod.FullPath)
                         .Concat(_owner.SaveTargets.Select(target => target.Path))
                         .DistinctBy(path => Path.GetFullPath(path).TrimEnd('\\', '/').ToLowerInvariant()))
            {
                if (ModSource.TryOpen(mod, [])?.Source is not { } source)
                    continue;
                _all.AddRange(source.EntryPaths.Where(YamlFileTypes.IsYaml).Order(StringComparer.OrdinalIgnoreCase)
                    .Select(path => new YamlFileItem(mod, path, Path.GetFileName(mod.TrimEnd('\\', '/')))));
            }
        }

        Refilter();
        var again = picked == null ? null : _all.FirstOrDefault(item => item.Is(picked.ModPath, picked.EntryPath));
        SelectedItem = again != null && Items.Contains(again) ? again : Items.FirstOrDefault(item => !item.IsBase) ?? Items.FirstOrDefault();
        if (keepEdit != null && SelectedItem == again)
            Source = keepEdit;
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(ReadOnlyText));
        SaveCommand.NotifyCanExecuteChanged();
        CheckAll();
    }

    // Checks every file as the game reads it, away from the window, marking the ones that don't read
    private void CheckAll()
    {
        int refresh = ++_refreshes;
        var items = _all.ToList();
        Task.Run(() =>
        {
            foreach (var item in items)
            {
                if (refresh != _refreshes)
                    return;
                var text = ReadText(item);
                var error = text == null ? "It can't be read" : YamlFileTypes.Check(item.EntryPath, !item.IsBase, text);
                Dispatcher.UIThread.Post(() =>
                {
                    if (refresh != _refreshes)
                        return;
                    if (text != null)
                        _texts.TryAdd(item, text);
                    item.Error = error;
                    if (OnlyProblems)
                        Refilter();
                });
            }
        });
    }

    // pfwolf.pk3 (under "") and the mods, opened once each time the files are listed
    private readonly Dictionary<string, IAssetSource?> _sources = new(StringComparer.OrdinalIgnoreCase);

    private string? ReadText(YamlFileItem item)
    {
        try
        {
            IAssetSource? source;
            lock (_sources)
            {
                var key = item.ModPath ?? "";
                if (!_sources.TryGetValue(key, out source))
                {
                    _sources[key] = source = item.IsBase
                        ? new Pk3AssetSource(Path.Combine(_owner.Content!.GameFolder, AssetManager.BasePk3FileName))
                        : ModSource.TryOpen(item.ModPath!, [])?.Source;
                }
            }
            return source == null ? null : YamlDataEntryLoader.ReadText(source.Open(item.EntryPath));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or NullReferenceException)
        {
            return null;
        }
    }

    partial void OnFilterChanged(string value) => Refilter();
    partial void OnShowBaseChanged(bool value) => Refilter();
    partial void OnOnlyProblemsChanged(bool value) => Refilter();

    private void Refilter()
    {
        var selected = SelectedItem;
        var words = Filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Items.Clear();
        foreach (var item in _all.Where(item => (ShowBase || !item.IsBase) && (!OnlyProblems || item.HasError || item == selected)
                                                && words.All(word => item.EntryPath.Contains(word, StringComparison.OrdinalIgnoreCase)
                                                                     || item.SourceName.Contains(word, StringComparison.OrdinalIgnoreCase)
                                                                     || (item.What?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false))))
            Items.Add(item);
        int problems = _all.Count(item => item.HasError);
        CountText = (Items.Count == _all.Count ? $"{Items.Count} files" : $"{Items.Count} of {_all.Count} files")
                    + (problems > 0 ? $", {problems} with problems" : "");
        if (selected != null && Items.Contains(selected))
            SelectedItem = selected;
    }

    partial void OnSelectedItemChanged(YamlFileItem? value)
    {
        _original = "";
        Note = "";
        if (value != null)
        {
            if (!_texts.TryGetValue(value, out var text))
            {
                text = ReadText(value) ?? "";
                _texts[value] = text;
            }
            _original = text.TrimStart('﻿');
        }

        _loadingSource = true;
        try
        {
            Source = _original;
        }
        finally
        {
            _loadingSource = false;
        }
        IsChanged = false;
        CheckSource();
    }

    partial void OnSourceChanged(string value)
    {
        if (_loadingSource)
            return;
        IsChanged = value != _original;
        _checkTimer.Stop();
        _checkTimer.Start();
    }

    // What's wrong with the text in the box, and on which line
    private void CheckSource()
    {
        if (SelectedItem is not { } item)
        {
            Problem = "";
            ProblemLine = 0;
            return;
        }

        var error = YamlFileTypes.Check(item.EntryPath, !item.IsBase, Source);
        Problem = error ?? "";
        ProblemLine = error != null && error.StartsWith("Line ") && int.TryParse(error[5..].Split(',', ':')[0], out var line) ? line : 0;
        if (!IsChanged)
            item.Error = error;
    }

    /// <summary>Shows a file, with the caret on the line of a key path in it</summary>
    public void Show(string? modPath, string entryPath, IReadOnlyList<string> path)
    {
        var item = _all.FirstOrDefault(candidate => candidate.Is(modPath, entryPath));
        if (item == null)
            return;
        if (!Items.Contains(item))
        {
            Filter = "";
            ShowBase = true;
            OnlyProblems = false;
        }
        SelectedItem = item;
        if (YamlFileTypes.LineOf(Source, path) is > 0 and var line)
            GoToLineRequested?.Invoke(this, line);
    }

    [RelayCommand]
    private void GoToProblem()
    {
        if (ProblemLine > 0)
            GoToLineRequested?.Invoke(this, ProblemLine);
    }

    [RelayCommand(CanExecute = nameof(IsChanged))]
    private void Revert() => Source = _original;

    public bool IsBaseFile => SelectedItem is { IsBase: true };

    /// <summary>pfwolf.pk3's file picked, which a mod can have its own of</summary>
    public bool CanCopyToMod => SelectedItem is { IsBase: true } item && YamlFileTypes.ModPathFor(item.EntryPath, out _) != null;

    /// <summary>Why pfwolf.pk3's file picked can't be copied to a mod, or empty</summary>
    public string CopyToModText
    {
        get
        {
            if (SelectedItem is not { IsBase: true } item)
                return "";
            return YamlFileTypes.ModPathFor(item.EntryPath, out var why) is { } modPath
                ? $"Makes {modPath} in the mod picked to save into, laid over this file key by key"
                : why;
        }
    }

    /// <summary>
    /// Makes a mod's own of pfwolf.pk3's file picked, where the mod keeps it ("whole": a copy of
    /// it to trim down; else just a note), and opens it. One the mod has already is opened as it is.
    /// </summary>
    [RelayCommand]
    private async Task CopyToModAsync(string? whole)
    {
        if (SelectedItem is not { IsBase: true } item || _owner.Content is not { } content)
            return;
        if (YamlFileTypes.ModPathFor(item.EntryPath, out var why) is not { } entryPath)
        {
            _owner.Status = why;
            return;
        }
        if (await _owner.ModToSaveIn() is not { } mod)
            return;

        try
        {
            if (Editing.ModFiles.ReadText(mod, entryPath) != null)
            {
                _owner.Status = $"{Path.GetFileName(mod.TrimEnd('\\', '/'))} has its own {entryPath} already: here it is";
                Show(mod, entryPath, []);
                return;
            }

            var text = YamlFileTypes.ModCopyText(item.EntryPath, _original, whole == "whole");
            var file = new DefinitionFile(mod, entryPath, "", "", isNew: true);
            var places = DefinitionSaver.SaveText(file, text);
            _owner.FileSaved(mod, places, []);
            Show(mod, entryPath, []);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _owner.Status = $"Couldn't make {entryPath}: {e.Message}";
        }
    }

    /// <summary>A mod's file picked (pfwolf.pk3's own are read-only)</summary>
    private bool CanDelete => SelectedItem is { IsBase: false };

    /// <summary>Takes the mod's file picked out of its mod</summary>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (SelectedItem is not { ModPath: { } mod } item || _owner.Confirm == null)
            return;
        var after = item.What != null ? $"The game won't read its {item.What} any more." : "The game doesn't read it.";
        if (!await _owner.Confirm($"Delete {item.EntryPath}", Editing.ModFiles.DeleteQuestion(mod, item.EntryPath, after), "Delete"))
            return;
        try
        {
            Editing.ModFiles.Delete(mod, item.EntryPath);
            IsChanged = false;
            _owner.FileDeleted($"{item.SourceName}: {item.EntryPath}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _owner.Status = $"Couldn't delete {item.EntryPath}: {e.Message}";
        }
    }

    private bool CanSave => SelectedItem != null && IsChanged && !IsReadOnly;

    /// <summary>Saves the file into its mod (pfwolf.pk3's own are read-only)</summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() => SaveChanges();

    /// <summary>The file changed in the box and not saved, as it's named to a person, or null</summary>
    public string? UnsavedName => SelectedItem != null && IsChanged ? $"YAML: {FileTitle}" : null;

    /// <summary>Saves the file in the box; false (with why in the status) when it isn't saved</summary>
    public bool SaveChanges()
    {
        if (SelectedItem is not { } item || IsReadOnly)
            return false;

        var error = YamlFileTypes.Check(item.EntryPath, !item.IsBase, Source);
        try
        {
            var file = new DefinitionFile(item.ModPath, item.EntryPath, "", _original);
            var text = Source;
            var places = DefinitionSaver.SaveText(file, text);
            _original = text;
            _texts[item] = text;
            IsChanged = false;
            item.Error = error;
            _owner.FileSaved(item.ModPath, places, error != null ? [$"It still has a problem, so the game leaves it out: {error}"] : []);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _owner.Status = $"Couldn't save {item.EntryPath}: {e.Message}";
            return false;
        }
    }
}
