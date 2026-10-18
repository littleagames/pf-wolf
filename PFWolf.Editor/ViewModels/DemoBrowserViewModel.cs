using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets.Demos;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;
using PFWolf.Managers;

namespace PFWolf.Editor.ViewModels;

/// <summary>A demo as the browser lists it</summary>
public sealed class DemoItem(DemoEntry entry, bool replaced)
{
    public DemoEntry Entry { get; } = entry;
    public string Name => Entry.Name;
    public string MapText => Entry.Demo.Data.Length >= DemoFile.HeaderSize ? Entry.Demo.MapName : "?";
    public string LengthText => DemoBrowserViewModel.Time(Entry.Demo.Duration);
    public string SourceText => Entry.SourceText + (replaced ? " (replaced)" : "");
    public double Opacity => replaced ? 0.55 : 1;

    public string ToolTip => Entry.Source switch
    {
        DemoSource.Game => replaced ? $"The game's {Name}: the recorded DEMO{Entry.Number}.dmo plays in its place" : $"The game's {Name}",
        DemoSource.Mod => $"From {Entry.Mod?.ModName}: {Entry.Mod?.EntryPath}"
                          + (Entry.Number == null ? $". Not one of the title loop's (DEMO0-9): playdemo {Name} plays it"
                              : replaced ? $". The recorded DEMO{Entry.Number}.dmo plays in its place" : ""),
        DemoSource.Recorded => $"Recorded with the game (recorddemo): {Entry.FilePath}. Plays in place of the game's DEMO{Entry.Number}",
        _ => Entry.FilePath ?? Name,
    } + (Entry.Demo.Problems.Count > 0 ? "\n" + string.Join("\n", Entry.Demo.Problems) : "");
}

/// <summary>An event in the trace, as the list shows it</summary>
public sealed record DemoEventItem(TraceEvent Event)
{
    public string TimeText => DemoBrowserViewModel.Time(DemoFile.TimeOf(Event.Frame), tenths: true);
    public string Text => Event.Text;
    public Avalonia.Media.IBrush Brush => DemoTimeline.EventBrush(Event.Kind);
}

/// <summary>How fast the replay plays</summary>
public sealed record DemoSpeedOption(double Speed, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The demo browser: the game's demos and recorded ones (and .dmo files opened), each one's
/// controls along a timeline, and, once the game has played it (demotrace), the level with the
/// player's path, everyone's moves and what happened, frame by frame
/// </summary>
public sealed partial class DemoBrowserViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private double _playedFrames;
    private readonly List<DemoEntry> _opened = [];
    private readonly Dictionary<DemoEntry, DemoTrace> _traces = [];
    private CancellationTokenSource? _tracing;
    private GameContent? _content;

    public DemoBrowserViewModel()
    {
        _speed = Speeds[2];
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Render, (_, _) => Tick());
    }

    /// <summary>What the game is run with to play a demo: its program, its folder and the mods; null when there's no game</summary>
    public Func<(string Exe, string GameFolder, IReadOnlyList<string> Mods)?>? GameToRun { get; set; }

    public ObservableCollection<DemoItem> Items { get; } = [];
    public ObservableCollection<DemoEventItem> Events { get; } = [];

    public IReadOnlyList<DemoSpeedOption> Speeds { get; } =
    [
        new(0.25, "¼×"), new(0.5, "½×"), new(1, "1×"), new(2, "2×"), new(4, "4×"), new(8, "8×"),
    ];

    /// <summary>Draws the trace over the level</summary>
    public DemoOverlay Overlay { get; } = new();

    [ObservableProperty] private string _title = "Demos";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _details = "";
    [ObservableProperty] private string _traceStatus = "";
    [ObservableProperty] private string _frameText = "";
    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private string _playerText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(TraceCommand), nameof(PlayCommand), nameof(StepCommand), nameof(GoToEndCommand), nameof(WatchCommand), nameof(AddToModCommand))]
    private DemoItem? _selectedItem;

    /// <summary>The demo picked, for the timeline</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastFrame))]
    private DemoFile? _demo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTrace))]
    private DemoTrace? _trace;

    /// <summary>The demo's level, read-only, under the trace</summary>
    [ObservableProperty] private MapTiles? _tiles;
    [ObservableProperty] private ArtCache? _art;

    [ObservableProperty] private int _frame;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TraceCommand))]
    [NotifyPropertyChangedFor(nameof(TraceLabel))]
    private bool _isTracing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayLabel))]
    private bool _isPlaying;

    [ObservableProperty] private DemoSpeedOption _speed;
    [ObservableProperty] private bool _followPlayer = true;
    [ObservableProperty] private bool _showPath = true;
    [ObservableProperty] private bool _showGhosts = true;
    [ObservableProperty] private bool _showEvents = true;
    [ObservableProperty] private DemoEventItem? _selectedEvent;

    public bool HasSelection => SelectedItem != null;
    public bool HasTrace => Trace != null;
    public int LastFrame => Math.Max(0, (Demo?.Frames.Count ?? 1) - 1);
    public string PlayLabel => IsPlaying ? "❚❚ Pause" : "▶ Play";
    public string TraceLabel => IsTracing ? "Tracing…" : "Trace again";

    /// <summary>The frame shown changed (the view keeps the player in sight)</summary>
    public event EventHandler? FrameShown;

    partial void OnShowPathChanged(bool value) => Overlay.ShowPath = value;
    partial void OnShowGhostsChanged(bool value) => Overlay.ShowGhosts = value;
    partial void OnShowEventsChanged(bool value) => Overlay.ShowEvents = value;
    partial void OnArtChanged(ArtCache? value)
    {
        Overlay.Art = value;
        Overlay.ForgetPictures();
    }

    partial void OnSelectedEventChanged(DemoEventItem? value)
    {
        if (value != null)
        {
            Pause();
            Frame = value.Event.Frame;
        }
    }

    /// <summary>Lists the demos of a newly loaded game, keeping the one picked if it's still there</summary>
    public void Load(GameContent? content, ArtCache? art)
    {
        Pause();
        CancelTrace();
        _content = content;
        _traces.Clear();
        Art = art;
        var picked = SelectedItem?.Entry;

        var entries = new List<DemoEntry>();
        if (content != null)
        {
            entries.AddRange(DemoCatalog.Build(content, GameTypes.DemosFolder(content.Game)));
            Title = $"Demos - {content.Title}";
        }
        else
            Title = "Demos";
        entries.AddRange(_opened);

        Items.Clear();
        foreach (var entry in entries)
            Items.Add(new DemoItem(entry, DemoCatalog.IsReplaced(entry, entries)));
        Status = content == null ? "Load a game to see its demos"
            : Items.Count == 0 ? "This game has no demos: record one in the game (recorddemo MAP01), or open a .dmo"
            : $"{Items.Count} demos";

        SelectedItem = Items.FirstOrDefault(item => picked != null && item.Entry.Name == picked.Name && item.Entry.Source == picked.Source)
            ?? Items.FirstOrDefault();
    }

    /// <summary>The pictures came again in new colors</summary>
    public void Recolor(ArtCache art) => Art = art;

    /// <summary>Lists a .dmo file and picks it</summary>
    public void Open(string path)
    {
        if (DemoCatalog.Read(path) is not { } entry)
        {
            Status = $"Couldn't read {path}";
            return;
        }
        _opened.RemoveAll(other => string.Equals(other.FilePath, path, StringComparison.OrdinalIgnoreCase));
        _opened.Add(entry);
        var item = new DemoItem(entry, false);
        Items.Add(item);
        SelectedItem = item;
        Status = $"Opened {path}";
    }

    partial void OnSelectedItemChanged(DemoItem? value)
    {
        Pause();
        CancelTrace();
        Events.Clear();
        Trace = null;
        Overlay.Trace = null;
        Frame = 0;
        Demo = value?.Entry.Demo;
        TraceStatus = "";

        if (value == null || _content == null)
        {
            Tiles = null;
            Details = "";
            ShowFrame();
            return;
        }

        var demo = value.Entry.Demo;
        var map = demo.Data.Length >= DemoFile.HeaderSize ? _content.FindMap(demo.MapName) : null;
        Tiles = map != null ? new MapTiles(_content, map) : null;
        Overlay.Tiles = Tiles;
        var title = map != null && _content.Maps.FirstOrDefault(m => m.Name.Equals(demo.MapName, StringComparison.OrdinalIgnoreCase)) is { } entry
            && !string.IsNullOrWhiteSpace(entry.Title) ? $" ({entry.Title})" : "";
        Details = $"{demo.MapName}{title}, {demo.Frames.Count} frames, {Time(demo.Duration, tenths: true)}, {demo.Data.Length} bytes"
            + (map == null && demo.Data.Length >= DemoFile.HeaderSize ? $"\nThe game has no {demo.MapName}: it won't play" : "")
            + (demo.Problems.Count > 0 ? "\n" + string.Join("\n", demo.Problems) : "");
        ShowFrame();

        if (_traces.TryGetValue(value.Entry, out var known))
            ShowTrace(known);
        else if (map != null && demo.IsPlayable)
            _ = TraceAsync();
    }

    private bool CanTrace => SelectedItem != null && !IsTracing;

    /// <summary>Has the game play the demo and write down what happened, then shows it</summary>
    [RelayCommand(CanExecute = nameof(CanTrace))]
    private async Task TraceAsync()
    {
        if (SelectedItem?.Entry is not { } entry || _content == null)
            return;
        if (GameToRun?.Invoke() is not { } game || !File.Exists(game.Exe))
        {
            TraceStatus = "There's no PFWolf program in the game folder to play the demo with";
            return;
        }

        CancelTrace();
        var cancel = _tracing = new CancellationTokenSource();
        IsTracing = true;
        TraceStatus = "The game is playing the demo…";
        try
        {
            var trace = await DemoTraceRunner.RunAsync(game.Exe, game.GameFolder, _content.PackId, game.Mods, entry.Demo.Data, cancel.Token);
            _traces[entry] = trace;
            if (SelectedItem?.Entry == entry)
                ShowTrace(trace);
        }
        catch (OperationCanceledException)
        {
            // Another demo was picked
        }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException or IOException
                                      or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            if (SelectedItem?.Entry == entry)
                TraceStatus = $"Couldn't trace it: {e.Message}";
        }
        finally
        {
            if (_tracing == cancel)
            {
                _tracing = null;
                IsTracing = false;
            }
            cancel.Dispose();
        }
    }

    /// <summary>Asks for a mod (a folder or a pk3) to save in; null when none is picked</summary>
    public Func<string, Task<string?>>? PickMod { get; set; }

    /// <summary>Asks for a line of text (title, prompt, initial, check); null when cancelled</summary>
    public Func<string, string, string, Func<string, string?>, Task<string?>>? AskText { get; set; }

    /// <summary>A demo went into a mod (the editor adds the mod and loads the game again)</summary>
    public event EventHandler<string>? Saved;

    /// <summary>What's wrong with a name for a demo in a mod's demos/, or null when it will do</summary>
    public static string? CheckDemoName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Give it a name";
        if (name.IndexOfAny([.. Path.GetInvalidFileNameChars(), ' ', '/', '\\']) >= 0)
            return "A name is one word, of letters, digits, - and _";
        return null;
    }

    /// <summary>
    /// Saves the demo picked into a mod as demos/NAME.dmo: DEMO0-9 take that place in the title
    /// loop (in place of the game's own of the number), any other name is for playdemo
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanWatch))]
    private async Task AddToModAsync()
    {
        if (SelectedItem?.Entry is not { } entry || _content == null || AskText == null || PickMod == null)
            return;

        // The first title loop number free, else what it's called
        var taken = Items.Where(item => item.Entry.Source is DemoSource.Game or DemoSource.Mod).Select(item => item.Entry.Number).ToHashSet();
        var free = Enumerable.Range(0, DemoCatalog.MaxDemos).Cast<int?>().FirstOrDefault(number => !taken.Contains(number));
        var suggested = free is { } number ? $"DEMO{number}" : Path.GetFileNameWithoutExtension(entry.Name);
        var name = await AskText("Add to a mod",
            "Its name in the mod's demos folder. DEMO0 to DEMO9 play in the title loop (in place of the game's own of that number); any other name plays with playdemo NAME:",
            suggested, candidate => CheckDemoName(candidate.Trim()));
        if (string.IsNullOrWhiteSpace(name))
            return;
        name = name.Trim();
        var folder = await PickMod($"A mod to put {name} in (it goes in its demos folder)");
        if (folder == null)
            return;

        try
        {
            var path = ModFiles.Write(folder, ($"demos/{name}.dmo", entry.Demo.Data));
            Status = $"Saved {path}";
            Saved?.Invoke(this, folder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Status = $"Couldn't save {name}: {e.Message}";
        }
    }

    private bool CanWatch => SelectedItem?.Entry.Demo.IsPlayable == true;

    /// <summary>
    /// Starts PFWolf (in its window, with the mods) playing the demo picked: the very data shown
    /// here, saved to a temporary file, so a recorded demo or a mod's plays as listed
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanWatch))]
    private void Watch()
    {
        if (SelectedItem?.Entry is not { } entry || _content == null)
            return;
        if (GameToRun?.Invoke() is not { } game || !File.Exists(game.Exe))
        {
            Status = "There's no PFWolf program in the game folder to play the demo with";
            return;
        }

        try
        {
            var folder = Path.Combine(Path.GetTempPath(), "pfwolf-editor-demos");
            Directory.CreateDirectory(folder);
            var name = string.Join("_", Path.GetFileNameWithoutExtension(entry.Name).Split(Path.GetInvalidFileNameChars()));
            var path = Path.Combine(folder, $"{name}.dmo");
            File.WriteAllBytes(path, entry.Demo.Data);
            GameLaunch.Start(game.Exe, game.GameFolder, GameLaunch.DemoArguments(_content.PackId, path, game.Mods));
            Status = $"Started PFWolf playing {entry.Name}";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            Status = $"Couldn't start the game: {e.Message}";
        }
    }

    private void CancelTrace()
    {
        _tracing?.Cancel();
        _tracing = null;
        IsTracing = false;
    }

    private void ShowTrace(DemoTrace trace)
    {
        Trace = trace;
        Overlay.Trace = trace;
        Events.Clear();
        foreach (var e in trace.Events)
            Events.Add(new DemoEventItem(e));

        int kills = trace.Events.Count(e => e.Kind == TraceEventKind.Kill);
        int pickups = trace.Events.Count(e => e.Kind == TraceEventKind.Pickup);
        var mismatch = Demo != null && trace.FrameCount != Demo.Frames.Count ? $" (the game played {trace.FrameCount} of {Demo.Frames.Count} frames)" : "";
        TraceStatus = $"Ended: {EndedText(trace.EndedHow)}. {kills} kills, {pickups} pickups, final score {(trace.FrameCount > 0 ? trace.Player[^1].Score : 0)}{mismatch}";
        ShowFrame();
    }

    private static string EndedText(string how) => how switch
    {
        "out-of-data" => "the demo ran out",
        "died" => "the player died",
        "completed" => "the level was finished",
        "victorious" => "the game was won",
        "secretlevel" => "went to the secret level",
        _ => how,
    };

    partial void OnFrameChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, LastFrame);
        if (clamped != value)
        {
            Frame = clamped;
            return;
        }
        Overlay.Frame = value;
        ShowFrame();
    }

    // The readouts for the frame shown
    private void ShowFrame()
    {
        if (Demo is not { Frames.Count: > 0 } demo)
        {
            FrameText = InputText = PlayerText = "";
            return;
        }

        FrameText = $"{Time(DemoFile.TimeOf(Frame), tenths: true)} / {Time(demo.Duration, tenths: true)}   frame {Frame + 1} of {demo.Frames.Count}";
        var input = demo.Frames[Frame];
        var held = Enumerable.Range(0, DemoFile.ButtonNames.Length).Where(b => (input.Buttons & 1 << b) != 0).Select(b => DemoFile.ButtonNames[b]);
        InputText = $"Turn {input.Turn,4}   Move {input.Move,4}   {string.Join(", ", held)}";

        if (Trace is { FrameCount: > 0 } trace)
        {
            var p = trace.Player[Math.Min(Frame, trace.FrameCount - 1)];
            PlayerText = string.Create(CultureInfo.InvariantCulture,
                $"Health {p.Health}   Score {p.Score}   {p.Weapon}   at {p.X:0.0}, {p.Y:0.0} facing {p.Angle}°");
        }
        else
            PlayerText = "";
        FrameShown?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Where the player is at the frame shown, in tiles; null before the trace</summary>
    public (double X, double Y)? PlayerSpot
        => Trace is { FrameCount: > 0 } trace ? (trace.Player[Math.Min(Frame, trace.FrameCount - 1)].X, trace.Player[Math.Min(Frame, trace.FrameCount - 1)].Y) : null;

    private bool HasFrames => Demo is { Frames.Count: > 0 };

    [RelayCommand(CanExecute = nameof(HasFrames))]
    private void Play()
    {
        if (IsPlaying)
        {
            Pause();
            return;
        }
        if (Frame >= LastFrame)
            Frame = 0;
        IsPlaying = true;
        _playedFrames = Frame;
        _clock.Restart();
        _timer.Start();
    }

    public void Pause()
    {
        _timer.Stop();
        _clock.Stop();
        IsPlaying = false;
    }

    // As the game plays it: a frame every DemoTics tics of 70 a second, times the speed
    private void Tick()
    {
        var elapsed = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        _playedFrames += elapsed * DemoFile.TicsPerSecond / DemoFile.TicsPerFrame * Speed.Speed;
        var next = (int)_playedFrames;
        if (next >= LastFrame)
        {
            Frame = LastFrame;
            Pause();
        }
        else if (next != Frame)
            Frame = next;
    }

    /// <summary>Moves by so many frames (back when negative; a button's parameter is text)</summary>
    [RelayCommand(CanExecute = nameof(HasFrames))]
    private void Step(string? frames)
    {
        Pause();
        if (int.TryParse(frames, NumberStyles.Integer, CultureInfo.InvariantCulture, out var by))
            Frame = Math.Clamp(Frame + by, 0, LastFrame);
    }

    [RelayCommand(CanExecute = nameof(HasFrames))]
    private void GoToEnd()
    {
        Pause();
        Frame = LastFrame;
    }

    public static string Time(TimeSpan time, bool tenths = false)
        => tenths ? $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds / 100}" : $"{(int)time.TotalMinutes}:{time.Seconds:00}";

    public void Dispose()
    {
        _timer.Stop();
        CancelTrace();
    }
}
