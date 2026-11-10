using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets.Sounds;
using PFWolf.Editor.Audio;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// A sound made of one tone a tick, 140 a second, as the editor changes it: an AdLib sound (an
/// F-number's low byte in a block) or a PC speaker sound (a PIT value); 0 is silence
/// </summary>
public abstract record ToneDraft(ushort Priority, byte[] Tones)
{
    public const int TicksPerSecond = 140;

    /// <summary>A tone's frequency (1-255)</summary>
    public abstract double Frequency(int tone);

    /// <summary>The tone nearest a frequency, not clamped (it can be under 1 or past 255)</summary>
    protected abstract int NearestTone(double frequency);

    /// <summary>The tone nearest a frequency, clamped to 1-255</summary>
    public int ToneOfFrequency(double frequency) => Math.Clamp(NearestTone(frequency), 1, 255);

    /// <summary>The tone (1-255) nearest a MIDI note, or null when this sound can't play it</summary>
    public int? ToneOf(double midiNote)
    {
        var tone = NearestTone(440 * Math.Pow(2, (midiNote - 69) / 12));
        return tone is >= 1 and <= 255 ? tone : null;
    }

    /// <summary>The lowest and highest frequency the strip shows</summary>
    public abstract (double Low, double High) Shown { get; }

    /// <summary>What a tone is called ("tone", "value")</summary>
    public abstract string ToneWord { get; }

    /// <summary>The file the game reads (.adl, .pcs)</summary>
    public abstract byte[] ToFile();

    /// <summary>The sound as the game plays it: 16-bit mono at 44100 Hz</summary>
    public abstract short[] ToSamples();

    /// <summary>The same sound: every setting and every tone</summary>
    public bool SameAs(ToneDraft other)
        => this with { Tones = Array.Empty<byte>() } == other with { Tones = Array.Empty<byte>() } && Tones.AsSpan().SequenceEqual(other.Tones);
}

/// <summary>
/// What the AdLib and PC speaker sound editors share: a sound's tones drawn on a strip a tick at
/// a time, tools on the ticks picked, length and priority, undo and redo, playing as the game
/// does, saving into a mod
/// </summary>
public abstract partial class ToneSoundEditorViewModel : ObservableObject, IDisposable
{
    private readonly Stack<(ToneDraft Draft, string What, int Version)> _undo = new();
    private readonly Stack<(ToneDraft Draft, string What, int Version)> _redo = new();
    private int _version, _lastVersion, _savedVersion;
    private readonly SoundPlayer _player = new();
    private readonly DispatcherTimer? _timer;
    private ToneDraft? _strokeStart;

    protected ToneSoundEditorViewModel(string name, ToneDraft draft, bool isNew)
    {
        Name = name;
        Current = draft;
        if (isNew && draft.Tones.Any(tone => tone != 0))
            _savedVersion = -1;
        if (Avalonia.Application.Current != null)
        {
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Background, (_, _) => UpdatePlayhead());
            _player.Finished += (_, _) => Dispatcher.UIThread.Post(Stopped);
        }
    }

    public string Name { get; private set; }

    /// <summary>The sound as it is now; changed only through <see cref="Edit"/> and strokes</summary>
    public ToneDraft Current { get; private set; }

    /// <summary>"AdLib sound", "PC speaker sound"</summary>
    protected abstract string Kind { get; }

    [ObservableProperty] private int _revision;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private bool _isDirty;

    public string Title => $"{Kind} - {Name}{(IsDirty ? " *" : "")}";

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _info = "";

    //
    // Changes, undo and redo
    //

    public void Edit(string what, Func<ToneDraft, ToneDraft> change)
    {
        var changed = change(Current);
        if (changed.SameAs(Current))
            return;
        _undo.Push((Current, what, _version));
        _redo.Clear();
        Current = changed;
        _version = ++_lastVersion;
        Changed(what);
    }

    /// <summary>The last step is changed again in place (edits close together that are one step)</summary>
    protected bool AmendLast(string what, Func<ToneDraft, ToneDraft> change)
    {
        if (_undo.Count == 0 || _undo.Peek().What != what)
            return false;
        Current = change(Current);
        _version = ++_lastVersion;
        Changed(what);
        return true;
    }

    private void Changed(string what)
    {
        IsDirty = _version != _savedVersion;
        Status = what;
        Refresh();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private bool CanUndo => _undo.Count > 0;
    private bool CanRedo => _redo.Count > 0;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_undo.Count == 0)
            return;
        var (draft, what, version) = _undo.Pop();
        _redo.Push((Current, what, _version));
        (Current, _version) = (draft, version);
        Restored();
        Changed($"Undid: {what}");
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_redo.Count == 0)
            return;
        var (draft, what, version) = _redo.Pop();
        _undo.Push((Current, what, _version));
        (Current, _version) = (draft, version);
        Restored();
        Changed($"Redid: {what}");
    }

    /// <summary>An undo or redo put an older sound back: panels showing its settings catch up</summary>
    protected virtual void Restored()
    {
    }

    protected void Refresh()
    {
        var sounding = Current.Tones.Count(tone => tone != 0);
        Info = $"{Current.Tones.Length} ticks ({Current.Tones.Length / (double)ToneDraft.TicksPerSecond:0.00} s), {sounding} sounding; {Describe()}";
        OnPropertyChanged(nameof(Priority));
        OnPropertyChanged(nameof(Length));
        Refreshed();
        Revision++;
    }

    /// <summary>The end of the status line: what the tones can play</summary>
    protected abstract string Describe();

    /// <summary>The sound changed: settings shown that aren't the shared ones catch up</summary>
    protected virtual void Refreshed()
    {
    }

    //
    // The sound's settings
    //

    /// <summary>Plays over a sound of a lower priority, and isn't played over one of a higher</summary>
    public decimal? Priority
    {
        get => Current.Priority;
        set
        {
            if (value is { } priority)
                Edit($"Priority {priority}", draft => draft with { Priority = (ushort)Math.Clamp(priority, 0, ushort.MaxValue) });
        }
    }

    /// <summary>How long it is, in ticks; longer adds silence, shorter cuts the end off</summary>
    public decimal? Length
    {
        get => Current.Tones.Length;
        set
        {
            if (value is not { } length)
                return;
            var ticks = (int)Math.Clamp(length, 1, 10000);
            Edit($"Made {ticks} ticks long", draft =>
            {
                var tones = new byte[ticks];
                draft.Tones.AsSpan(0, Math.Min(ticks, draft.Tones.Length)).CopyTo(tones);
                return draft with { Tones = tones };
            });
        }
    }

    //
    // Drawing on the strip
    //

    /// <summary>Snap drawn tones to semitones</summary>
    [ObservableProperty] private bool _snapToNotes = true;

    /// <summary>Ticks picked on the strip (from, to exclusive) that the tools work on; null: all of it</summary>
    [ObservableProperty] private (int From, int To)? _range;

    partial void OnRangeChanged((int From, int To)? value) => Revision++;

    /// <summary>A drag on the strip starts: what it draws is one step</summary>
    public void BeginStroke() => _strokeStart = Current;

    /// <summary>Sets ticks' tones as a drag goes (0 for silence), growing the sound to hold them</summary>
    public void SetTones(int from, int to, Func<int, int> toneAt)
    {
        if (to < from)
            (from, to) = (to, from);
        from = Math.Max(0, from);
        var tones = Current.Tones.Length > to ? (byte[])Current.Tones.Clone() : [.. Current.Tones, .. new byte[to + 1 - Current.Tones.Length]];
        for (var tick = from; tick <= to; tick++)
        {
            var tone = Math.Clamp(toneAt(tick), 0, 255);
            if (tone > 0 && SnapToNotes)
            {
                var note = Math.Round(OplPitch.MidiNote(Current.Frequency(tone)));
                tone = Current.ToneOf(note) ?? tone;
            }
            tones[tick] = (byte)tone;
        }
        Current = Current with { Tones = tones };
        Revision++;
    }

    /// <summary>The drag ends: what it drew is one step to undo</summary>
    public void EndStroke(string what)
    {
        if (_strokeStart is not { } start)
            return;
        _strokeStart = null;
        if (start.Tones.AsSpan().SequenceEqual(Current.Tones))
            return;
        _undo.Push((start, what, _version));
        _redo.Clear();
        _version = ++_lastVersion;
        Changed(what);
    }

    private (int From, int To) Span => Range is { } range ? (Math.Clamp(range.From, 0, Current.Tones.Length), Math.Clamp(range.To, 0, Current.Tones.Length)) : (0, Current.Tones.Length);
    private string Where => Range != null ? "the ticks picked" : "the whole sound";

    /// <summary>Every sounding tone moved by some semitones (clamped to what the sound can play)</summary>
    public void Transpose(int semitones)
    {
        var (from, to) = Span;
        var factor = Math.Pow(2, semitones / 12.0);
        Edit($"Moved {Where} {semitones:+0;-0} semitones", draft =>
        {
            var tones = (byte[])draft.Tones.Clone();
            for (var tick = from; tick < to; tick++)
            {
                if (tones[tick] != 0)
                    tones[tick] = (byte)draft.ToneOfFrequency(draft.Frequency(tones[tick]) * factor);
            }
            return draft with { Tones = tones };
        });
    }

    [RelayCommand] private void Up() => Transpose(1);
    [RelayCommand] private void Down() => Transpose(-1);

    /// <summary>The ticks picked (or the whole sound) made longer or shorter by a factor, keeping their pitches</summary>
    public void Stretch(double factor)
    {
        var (from, to) = Span;
        if (to <= from || factor <= 0)
            return;
        var length = Math.Max(1, (int)Math.Round((to - from) * factor));
        Edit($"Stretched {Where} to {length} ticks", draft =>
        {
            var part = new byte[length];
            for (var tick = 0; tick < length; tick++)
                part[tick] = draft.Tones[from + Math.Min(to - from - 1, (int)(tick / factor))];
            return draft with { Tones = [.. draft.Tones[..from], .. part, .. draft.Tones[to..]] };
        });
        if (Range != null)
            Range = (from, from + length);
    }

    [RelayCommand] private void Longer() => Stretch(2);
    [RelayCommand] private void Shorter() => Stretch(0.5);

    /// <summary>The ticks picked (or all) made silent</summary>
    [RelayCommand]
    private void Silence()
    {
        var (from, to) = Span;
        Edit($"Silenced {Where}", draft =>
        {
            var tones = (byte[])draft.Tones.Clone();
            Array.Clear(tones, from, to - from);
            return draft with { Tones = tones };
        });
    }

    /// <summary>The ticks picked taken out (the rest closes up)</summary>
    [RelayCommand]
    private void DeleteRange()
    {
        if (Range == null)
            return;
        var (from, to) = Span;
        if (to - from >= Current.Tones.Length)
            return;
        Edit("Took out the ticks picked", draft => draft with { Tones = [.. draft.Tones[..from], .. draft.Tones[to..]] });
        Range = null;
    }

    //
    // Playing
    //

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayLabel))]
    private bool _isPlaying;

    [ObservableProperty] private int _playheadTick = -1;

    public string PlayLabel => IsPlaying ? "■ Stop" : "▶ Play";

    /// <summary>The sound as the game plays it: 16-bit mono at 44100 Hz</summary>
    public short[] ToSamples() => Current.ToSamples();

    [RelayCommand]
    private void Play()
    {
        if (IsPlaying)
        {
            Stop();
            return;
        }
        _player.Play(new SampleMusicSource(ToSamples(), ImfMusicSource.MusicSampleRate), loop: false);
        IsPlaying = SoundPlayer.IsAvailable;
        _timer?.Start();
    }

    public void Stop()
    {
        _player.Stop();
        Stopped();
    }

    private void Stopped()
    {
        _timer?.Stop();
        IsPlaying = false;
        PlayheadTick = -1;
    }

    private void UpdatePlayhead()
    {
        if (IsPlaying)
            PlayheadTick = (int)(_player.Position.TotalSeconds * ToneDraft.TicksPerSecond);
    }

    //
    // Saving
    //

    /// <summary>Saves the sound into a mod (its name, the file): the name it's saved as, or null</summary>
    public Func<string, byte[], Task<string?>>? SaveToMod { get; set; }

    /// <summary>The file the game reads</summary>
    public byte[] ToFile() => Current.ToFile();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SaveToMod == null)
            return;
        Stop();
        if (await SaveToMod(Name, ToFile()) is { } saved)
        {
            Name = saved;
            _savedVersion = _version;
            IsDirty = false;
            OnPropertyChanged(nameof(Title));
            Status = $"Saved {Name}";
        }
    }

    public void Dispose()
    {
        _timer?.Stop();
        _player.Dispose();
    }
}
