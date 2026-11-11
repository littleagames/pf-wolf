using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets;
using PFWolf.Definitions;
using PFWolf.Editor.Data;
using PFWolf.Editor.Definitions;
using PFWolf.Editor.Rendering;
using PFWolf.Entities.Actors;

namespace PFWolf.Editor.ViewModels;

/// <summary>A class in the actor window's list</summary>
public sealed partial class ActorItem(ActorEntry entry) : ObservableObject
{
    public ActorEntry Entry { get; } = entry;

    /// <summary>A new class made here, not saved in a mod yet</summary>
    public bool IsDraft { get; init; }

    /// <summary>Changed here, not saved</summary>
    [ObservableProperty] private bool _hasEdits;

    public string Name => Entry.Name;
    public string KindText => ActorBrowserViewModel.KindLabel(Entry.Kind);
    public bool HasProblems => Entry.Problems.Count > 0;
    public string ToolTip => HasProblems ? string.Join("\n", Entry.Problems)
        : string.IsNullOrEmpty(Entry.Own.Parent) ? KindText : $"{KindText}, built on {Entry.Own.Parent}";

    public override string ToString() => Name;
}

/// <summary>A filter of the actor list</summary>
public sealed record ActorKindOption(ActorKind? Kind, string Label, bool Problems = false)
{
    public override string ToString() => Label;
}

/// <summary>A frame of the state shown, in the timeline: as wide as its tics</summary>
public sealed partial class TimelineFrame(ActorStateFrame frame, int index) : ObservableObject
{
    public ActorStateFrame Frame { get; } = frame;
    public int Index { get; } = index;
    public string Letter => Frame.FrameLetter;
    public string Tics => Frame.HoldsForever ? "∞" : Frame.TicTime.ToString(CultureInfo.InvariantCulture);
    public bool HasAction => !string.IsNullOrWhiteSpace(Frame.Action);
    public double Width => Frame.HoldsForever ? 44 : Math.Clamp(Frame.TicTime * 3, 26, 150);

    public string ToolTip => $"{Frame.Sprite}{Frame.FrameLetter}, {(Frame.HoldsForever ? "held for good" : $"{Frame.TicTime} tics")}"
        + (Frame.Think is { } think ? $"\nthink: {think} (every tic)" : "")
        + (Frame.Action is { } action ? $"\naction: {action} (as it ends)" : "")
        + (Frame.Modifiers.Count > 0 ? $"\nmodifiers: {string.Join(", ", Frame.Modifiers)}" : "");

    /// <summary>The frame the preview is on</summary>
    [ObservableProperty] private bool _isCurrent;
}

/// <summary>A property in the Properties list: its value, and the class it comes from when that's a parent</summary>
public sealed record ActorPropertyRow(string Key, string Value, string From, bool IsOwn, string Description);

/// <summary>A speed the preview plays at</summary>
public sealed record PreviewSpeed(double Factor, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The actor window: every actordefs class as the game builds it, played as the game plays it,
/// with what it's built on, where it's defined, what names it and what's wrong with it
/// </summary>
public sealed partial class ActorBrowserViewModel : ObservableObject
{
    private GameContent? _content;
    private List<ActorItem> _all = [];
    private readonly StatePlayer _player = new() { StopAtJumps = true };
    private readonly Dictionary<string, Bitmap?> _bitmaps = new(StringComparer.OrdinalIgnoreCase);
    private double _ticsOwed;

    public ActorBrowserViewModel()
    {
        _selectedKind = Kinds[0];
        _selectedSpeed = Speeds[0];
    }

    public IReadOnlyList<ActorKindOption> Kinds { get; } =
    [
        new(null, "All"),
        new(ActorKind.Enemy, "Enemies"),
        new(ActorKind.Item, "Items"),
        new(ActorKind.Weapon, "Weapons"),
        new(ActorKind.Projectile, "Projectiles"),
        new(ActorKind.Decoration, "Decorations"),
        new(ActorKind.Player, "Players"),
        new(ActorKind.Unseen, "Unseen (markers, base classes)"),
        new(null, "With problems", Problems: true),
    ];

    public IReadOnlyList<PreviewSpeed> Speeds { get; } = [new(1, "1×"), new(0.5, "½×"), new(0.25, "¼×")];

    /// <summary>The views a turning sprite has: 1 its front, 5 its back</summary>
    public IReadOnlyList<int> Views { get; } = [1, 2, 3, 4, 5, 6, 7, 8];

    public static string KindLabel(ActorKind kind) => kind switch
    {
        ActorKind.Enemy => "enemy",
        ActorKind.Item => "item",
        ActorKind.Weapon => "weapon",
        ActorKind.Projectile => "projectile",
        ActorKind.Decoration => "decoration",
        ActorKind.Player => "player",
        _ => "unseen",
    };

    public ObservableCollection<ActorItem> Items { get; } = [];
    public ObservableCollection<string> States { get; } = [];
    public ObservableCollection<TimelineFrame> Timeline { get; } = [];
    public ObservableCollection<ActorPropertyRow> Properties { get; } = [];
    public ObservableCollection<string> Problems { get; } = [];
    public ObservableCollection<string> Notes { get; } = [];
    public ObservableCollection<ActorUse> Uses { get; } = [];
    public ObservableCollection<string> DefinedIn { get; } = [];

    [ObservableProperty] private ActorKindOption _selectedKind;
    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private string _countText = "";
    [ObservableProperty] private string _title = "Actors";
    [ObservableProperty] private string _status = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(CopyNameCommand), nameof(EditInDefinitionsCommand))]
    private ActorItem? _selectedItem;

    public bool HasSelection => SelectedItem != null;

    [ObservableProperty] private string _details = "";
    [ObservableProperty] private string _lineage = "";
    [ObservableProperty] private string _keyNumbers = "";
    [ObservableProperty] private string _flagsText = "";
    [ObservableProperty] private string _definitionYaml = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoToUseCommand))]
    private ActorUse? _selectedUse;

    // The preview
    [ObservableProperty] private string? _selectedState;
    [ObservableProperty] private int _selectedView = 1;
    [ObservableProperty] private bool _turns;
    [ObservableProperty] private bool _playing = true;
    [ObservableProperty] private PreviewSpeed _selectedSpeed;
    [ObservableProperty] private bool _followNext = true;
    [ObservableProperty] private int _zoom = 3;

    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private double _previewWidth;
    [ObservableProperty] private double _previewHeight;
    [ObservableProperty] private double _previewBoxWidth = 192;
    [ObservableProperty] private double _previewBoxHeight = 192;
    [ObservableProperty] private string _pictureText = "";
    [ObservableProperty] private string _ticText = "";
    [ObservableProperty] private string _runningText = "";
    [ObservableProperty] private string _eventText = "";
    [ObservableProperty] private string _frameText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoOnCommand))]
    private string _jumpText = "";

    public bool IsStuck => JumpText.Length > 0;

    partial void OnJumpTextChanged(string value) => OnPropertyChanged(nameof(IsStuck));

    /// <summary>The frame the preview is on, as the timeline marks it</summary>
    public ActorStateFrame? CurrentFrame => _player.Frame;

    /// <summary>The sprite the preview shows ("GARDB1"), for Show in the art window</summary>
    public string? CurrentPicture { get; private set; }

    /// <summary>A use's Go to: another class here, a definition, a script's line or a level's tile</summary>
    public event EventHandler<ActorUseTarget>? GoTo;

    /// <summary>Asks the art window to show a sprite</summary>
    public event EventHandler<string>? SpriteRequested;

    /// <summary>Asks the window to put text on the clipboard</summary>
    public event EventHandler<string>? CopyText;

    public void Load(GameContent? content)
    {
        _content = content;
        foreach (var bitmap in _bitmaps.Values)
            bitmap?.Dispose();
        _bitmaps.Clear();
        var picked = SelectedItem?.Name;
        _all = [];
        if (content != null)
        {
            LoadFields(content);
            LoadStatePickers(content);
            _sounds.Load(content);
            _all = [.. ActorCatalog.Build(content).Select(entry => new ActorItem(entry) { HasEdits = HasUnsaved(entry.Name) })];
            // New classes not saved yet stay listed
            foreach (var name in _drafts.Keys)
                _all.Add(DraftItem(content, name));
            _all.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }
        Title = content == null ? "Actors" : $"Actors - {content.Title}";
        Refilter();
        SelectedItem = Items.FirstOrDefault(item => item.Name.Equals(picked, StringComparison.Ordinal))
                       ?? Items.FirstOrDefault(item => item.Entry.Kind == ActorKind.Enemy) ?? Items.FirstOrDefault();
    }

    /// <summary>Picks a class by name (the list's filter cleared if it hides it)</summary>
    public void Show(string name)
    {
        if (_all.FirstOrDefault(item => item.Name.Equals(name, StringComparison.Ordinal)) is not { } item)
            return;
        if (!Items.Contains(item))
        {
            Filter = "";
            SelectedKind = Kinds[0];
        }
        SelectedItem = item;
    }

    partial void OnFilterChanged(string value) => Refilter();
    partial void OnSelectedKindChanged(ActorKindOption value) => Refilter();

    private void Refilter()
    {
        var picked = SelectedItem;
        Items.Clear();
        foreach (var item in _all.Where(Shows))
            Items.Add(item);
        CountText = Items.Count == _all.Count ? $"{_all.Count} classes" : $"{Items.Count} of {_all.Count} classes";
        if (picked != null && Items.Contains(picked))
            SelectedItem = picked;
    }

    private bool Shows(ActorItem item)
    {
        if (SelectedKind is { Problems: true } ? !item.HasProblems : SelectedKind?.Kind is { } kind && item.Entry.Kind != kind)
            return false;
        if (string.IsNullOrWhiteSpace(Filter))
            return true;
        var words = Filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.All(word => item.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                                 || item.Entry.Ancestors.Any(parent => parent.Contains(word, StringComparison.OrdinalIgnoreCase))
                                 || item.Entry.States.Values.Any(frame => Mentions(frame, word)));
    }

    // A state's frames name a sprite, think or action with the word
    private static bool Mentions(ActorStateFrame first, string word)
        => ActorCatalog.FramesOf(first).Any(frame => frame.Sprite.Contains(word, StringComparison.OrdinalIgnoreCase)
                                                     || frame.Think?.Contains(word, StringComparison.OrdinalIgnoreCase) == true
                                                     || frame.Action?.Contains(word, StringComparison.OrdinalIgnoreCase) == true);

    // The class shown: as edited here (its fields changed, not saved), else as the game has it
    private ActorEntry? _edited;
    private ActorEntry? Shown => _edited ?? SelectedItem?.Entry;

    partial void OnSelectedItemChanged(ActorItem? value)
    {
        OpenFields(value);
        ShowEntry(keepState: false);
    }

    // Everything about the class shown, filled in again (picked, or its fields changed)
    private void ShowEntry(bool keepState)
    {
        var state = SelectedState;
        States.Clear();
        Properties.Clear();
        Problems.Clear();
        Notes.Clear();
        Uses.Clear();
        DefinedIn.Clear();
        (Details, Lineage, KeyNumbers, FlagsText, DefinitionYaml) = ("", "", "", "", "");
        if (Shown is not { } entry || _content == null)
        {
            SelectedState = null;
            return;
        }

        var actors = _edited != null ? EditedActors(entry) : _content.Actors;
        Details = Describe(entry);
        if (ActorClasses.Engine.ContainsKey(entry.Name) && BuiltOn(entry.Name) is { Count: > 0 } reached)
            Details += $"; one of the engine's own base classes: {reached.Count} class{(reached.Count == 1 ? " is" : "es are")} built on it, and a change reaches them all";
        Lineage = string.Join(" › ", entry.Ancestors.Reverse().Append(entry.Name));

        foreach (var problem in entry.Problems)
            Problems.Add(problem);
        foreach (var note in entry.Notes)
            Notes.Add(note);
        foreach (var use in entry.Uses)
            Uses.Add(use);
        SelectedUse = null;
        foreach (var site in entry.DefinedIn)
            DefinedIn.Add(site.File);
        DefinitionYaml = entry.DefinedIn.Count > 0 ? entry.DefinedIn[^1].Yaml : "";

        if (entry.Merged is { } merged)
        {
            // The ones from its parents: its own are its fields
            foreach (var (key, value2) in merged.Properties.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                var from = entry.SourceOfProperty(key, actors) ?? entry.Name;
                if (from != entry.Name)
                    Properties.Add(new ActorPropertyRow(key, ValueText(value2), from, false,
                        ActorProperties.Find(key)?.Description ?? "The engine doesn't read it (a script may)"));
            }
            FlagsText = merged.Flags.Count == 0 ? "none"
                : string.Join(", ", merged.Flags.Order(StringComparer.OrdinalIgnoreCase)
                    .Select(flag => entry.SourceOfFlag(flag, actors) is { } from && from != entry.Name ? $"{flag} (from {from})" : flag));
            KeyNumbers = Numbers(merged);
        }

        foreach (var name in entry.States.Keys)
            States.Add(name);
        SelectedState = null;
        SelectedState = keepState && state != null && entry.States.ContainsKey(state) ? state : FirstState(entry);
        AddableProperties = PropertiesToAdd(entry);
        FillStatePickers(entry);
        UpdateFramePanel();
    }

    // Where the preview starts: Spawn, unless it stands still for good and the class has a Chase
    // (so something moves); a weapon's Ready
    private static string? FirstState(ActorEntry entry)
    {
        var states = entry.States;
        if (entry.Kind == ActorKind.Weapon && states.ContainsKey("Ready"))
            return "Ready";
        if (states.TryGetValue("Spawn", out var spawn))
            return spawn.HoldsForever && spawn.Next == spawn && states.ContainsKey("Chase") && entry.Kind == ActorKind.Enemy ? "Chase" : "Spawn";
        return states.Keys.FirstOrDefault();
    }

    private static string Describe(ActorEntry entry)
    {
        var parts = new List<string> { $"A{(KindLabel(entry.Kind) is "enemy" or "item" or "unseen" ? "n" : "")} {KindLabel(entry.Kind)}" };
        if (!string.IsNullOrEmpty(entry.Own.Parent))
            parts.Add($"built on {entry.Own.Parent}");
        parts.Add($"the engine's {entry.EngineClass}");
        if (entry.Own.Extend)
            parts.Add("a mod's change to a class before it (extend)");
        return string.Join(", ", parts);
    }

    private static string Numbers(ActorData merged)
    {
        var parts = new List<string>();
        foreach (var (key, label) in new[] { ("health", "health"), ("speed", "speed"), ("points", "points"), ("dropitem", "drops"), ("inventory.amount", "amount") })
            if (merged.Properties.TryGetValue(key, out var value))
                parts.Add($"{label} {ValueText(value)}");
        parts.Add(merged.Radius > 0 ? $"radius {merged.Radius}" : "radius: the game's own");
        return string.Join(" · ", parts);
    }

    private static string ValueText(object? value) => value switch
    {
        null => "",
        string text => text,
        IEnumerable<object> list => "[" + string.Join(", ", list.Select(ValueText)) + "]",
        bool flag => flag ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };

    //
    // The preview
    //

    // Set while the preview moves on into another state by itself, so the state list follows without starting over
    private bool _following;

    partial void OnSelectedStateChanged(string? value)
    {
        if (_following)
            return;
        Restart();
    }

    partial void OnSelectedViewChanged(int value) => DrawFrame();
    partial void OnZoomChanged(int value) => DrawFrame();

    /// <summary>Plays the state picked from its first frame</summary>
    [RelayCommand]
    private void Restart()
    {
        JumpText = "";
        EventText = "";
        _ticsOwed = 0;
        if (Shown?.States is not { } states || SelectedState == null || !states.TryGetValue(SelectedState, out var first))
        {
            Timeline.Clear();
            (PreviewImage, PictureText, TicText, RunningText, FrameText) = (null, "", "", "", "");
            return;
        }
        Report(_player.Start(first));
        FillTimeline();
        DrawFrame();
    }

    [RelayCommand]
    private void PlayPause() => Playing = !Playing;

    /// <summary>On to the next frame (playing stops)</summary>
    [RelayCommand]
    private void StepFrame()
    {
        Playing = false;
        if (_player.Frame is not { } from)
            return;
        if (IsStuck)
        {
            GoOn();
            return;
        }
        // Until it's on another frame, or this one again from the start (a frame that loops on itself)
        var events = new List<StateEvent>();
        for (int i = 0; i < 10000 && !_player.Stuck && !from.HoldsForever; i++)
        {
            events.AddRange(_player.Step());
            if (_player.Frame != from || _player.TicsLeft == from.TicTime)
                break;
        }
        Report(events);
        Moved();
    }

    /// <summary>After an action that may jump: on to the frame's next, as if it didn't</summary>
    [RelayCommand(CanExecute = nameof(IsStuck))]
    private void GoOn()
    {
        if (_player.Frame?.Next is not { } next)
            return;
        JumpText = "";
        Report(_player.Resume(next));
        Moved();
    }

    /// <summary>Plays some tics (the window's timer calls it, about 35 times a second)</summary>
    public void Tick(double tics)
    {
        if (!Playing || _player.Frame == null || IsStuck)
            return;
        _ticsOwed += tics * SelectedSpeed.Factor;
        int whole = (int)_ticsOwed;
        if (whole == 0)
            return;
        _ticsOwed -= whole;
        var before = _player.Frame;
        Report(_player.Step(whole));
        if (_player.Frame != before || _player.Stuck)
            Moved();
        else
            TicText = TicLine();
    }

    /// <summary>Shows a frame of the timeline (a click), playing stopped</summary>
    public void PickFrame(TimelineFrame frame)
    {
        Playing = false;
        JumpText = "";
        _player.Show(frame.Frame);
        Moved();
    }

    // The player is on another frame: maybe another state, whose frames the timeline then shows
    private void Moved()
    {
        if (_player.Frame is { } frame && frame.StateName != SelectedState)
        {
            if (FollowNext)
            {
                Sound([], SelectedState, frame.StateName);
                _following = true;
                SelectedState = frame.StateName;
                _following = false;
                FillTimeline();
            }
            else
            {
                Restart();
                return;
            }
        }
        DrawFrame();
    }

    private void Report(List<StateEvent> events)
    {
        Sound(events);
        if (events.Count > 0)
            EventText = "Ran: " + RunsOf(events.Select(e => e.Action));
        if (_player.Stuck && events.LastOrDefault() is { MayJump: true } jump)
            JumpText = $"{ActorCatalog.CallName(jump.Action)} may go elsewhere here (the game decides as it plays). Go on goes to the frame after it; or pick a state.";
        else if (_player.Stuck)
            JumpText = "Its 0-tic frames loop forever: the game freezes it here.";
    }

    /// <summary>Actions as they ran, one after another the same written once with how many (A_WeaponReady ×7), the last few</summary>
    public static string RunsOf(IEnumerable<string> actions)
    {
        var runs = new List<(string Action, int Count)>();
        foreach (var action in actions)
        {
            if (runs.Count > 0 && runs[^1].Action == action)
                runs[^1] = (action, runs[^1].Count + 1);
            else
                runs.Add((action, 1));
        }
        var shown = runs.TakeLast(6).Select(run => run.Count > 1 ? $"{run.Action} ×{run.Count}" : run.Action);
        return (runs.Count > 6 ? "…, " : "") + string.Join(", ", shown);
    }

    private void FillTimeline()
    {
        Timeline.Clear();
        if (Shown?.States is not { } states || SelectedState == null || !states.TryGetValue(SelectedState, out var first))
            return;
        int index = 0;
        foreach (var frame in ActorCatalog.FramesOf(first))
            Timeline.Add(new TimelineFrame(frame, index++));
    }

    /// <summary>Where the state's last frame goes: held, looping, or on to another state</summary>
    public string TimelineEnd
    {
        get
        {
            if (Timeline.Count == 0)
                return "";
            var last = Timeline[^1].Frame;
            if (last.HoldsForever)
                return "held";
            if (last.Next == Timeline[0].Frame)
                return "↺ loops";
            if (last.Next == last)
                return "↺ its last frame again (stop)";
            return last.Next != null ? $"→ {last.Next.StateName}" : "";
        }
    }

    private string TicLine() => _player.Frame is { } frame
        ? $"tic {_player.Tic}, {(frame.HoldsForever ? "held for good" : $"{_player.TicsLeft} of {frame.TicTime} left")}"
        : "";

    private void DrawFrame()
    {
        OnPropertyChanged(nameof(TimelineEnd));
        if (_content == null || _player.Frame is not { } frame)
            return;
        foreach (var item in Timeline)
            item.IsCurrent = item.Frame == frame;

        Turns = ActorCatalog.Turns(_content, frame);
        var name = ActorCatalog.PictureName(_content, frame, SelectedView, out var found);
        CurrentPicture = found ? name : null;
        PictureText = name == null ? "nothing to see (TNT1)" : found ? name : $"{name}: no such sprite";
        TicText = TicLine();
        RunningText = (frame.Think is { } think ? $"{think} every tic" : "no think")
                      + (frame.Action is { } action ? $"; {action} as it ends" : "")
                      + (frame.Bright ? "; drawn bright" : frame.LightenLevel is { } lighten ? $"; lighten({lighten})" : "");
        FrameText = $"{frame.StateName} frame {Timeline.FirstOrDefault(item => item.Frame == frame)?.Index + 1}: sprite {frame.Sprite}, letter {frame.FrameLetter}, "
                    + (frame.HoldsForever ? "held for good" : $"{frame.TicTime} tics")
                    + $"; think {frame.Think ?? "—"}; action {frame.Action ?? "—"}"
                    + (frame.Modifiers.Count > 0 ? $"; {string.Join(", ", frame.Modifiers)}" : "");

        int zoom = Math.Clamp(Zoom, 1, 6);
        PreviewImage = found && name != null ? Picture(name) : null;
        if (PreviewImage is { } bitmap)
            (PreviewWidth, PreviewHeight) = (bitmap.PixelSize.Width * zoom, bitmap.PixelSize.Height * zoom);
        else
            (PreviewWidth, PreviewHeight) = (64 * zoom, 64 * zoom);
        // A box the size of a 64-pixel sprite, or the picture's if it's bigger, so the preview doesn't jump about
        (PreviewBoxWidth, PreviewBoxHeight) = (Math.Max(64 * zoom, PreviewWidth), Math.Max(64 * zoom, PreviewHeight));
        if (!Playing)
            UpdateFramePanel();
    }

    // A sprite drawn in the game's palette, see-through where it is; none without a window (tests)
    private Bitmap? Picture(string name)
    {
        if (_bitmaps.TryGetValue(name, out var bitmap))
            return bitmap;
        if (Avalonia.Application.Current != null && _content != null && ArtCache.Indexed(_content, ArtKind.Sprite, name) is { } picture)
            bitmap = ArtCache.Render(picture, _content.Palette);
        return _bitmaps[name] = bitmap;
    }

    //
    // Doing things with the class picked
    //

    private bool HasPicked => SelectedItem != null;

    [RelayCommand(CanExecute = nameof(HasPicked))]
    private void CopyName()
    {
        if (SelectedItem is { } item)
        {
            CopyText?.Invoke(this, item.Name);
            Status = $"Copied {item.Name}";
        }
    }

    /// <summary>Opens the class in the Definitions window's Actors tab</summary>
    [RelayCommand(CanExecute = nameof(HasPicked))]
    private void EditInDefinitions()
    {
        if (SelectedItem is { IsDraft: false } item)
            GoTo?.Invoke(this, new ActorUseTarget(Kind: DefinitionKind.Actors, EntryPath: [item.Name]));
        else if (SelectedItem != null)
            Status = "It's new: save it first";
    }

    /// <summary>The sprite the preview shows, in the art window</summary>
    [RelayCommand]
    private void ShowSprite()
    {
        if (CurrentPicture is { } name)
            SpriteRequested?.Invoke(this, name);
        else
            Status = "The frame shows no sprite the game has";
    }

    private bool CanGoToUse => SelectedUse?.Target != null;

    [RelayCommand(CanExecute = nameof(CanGoToUse))]
    private void GoToUse()
    {
        if (SelectedUse?.Target is not { } target)
            return;
        if (target.Actor != null)
            Show(target.Actor);
        else
            GoTo?.Invoke(this, target);
    }
}
