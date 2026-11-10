using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets.Sounds;
using PFWolf.Editor.Audio;
using PFWolf.Editor.Audio.Opl;

namespace PFWolf.Editor.ViewModels;

/// <summary>
/// An AdLib sound as the editor changes it: its priority, instrument, octave block, and one tone a
/// tick (140 a second): an F-number's low byte, 0 for silence
/// </summary>
public sealed record AdLibDraft(ushort Priority, OplInstrument Instrument, byte Block, byte[] Tones)
{
    public const int TicksPerSecond = 140;

    public static AdLibDraft From(AdLibSound sound) => new(sound.Priority, sound.Instrument, sound.Block, sound.Tones);

    public AdLibSound ToSound() => AdLibSound.Create(Priority, Instrument, Block, Tones);

    /// <summary>A tone's frequency in this block</summary>
    public double Frequency(int tone) => OplPitch.Frequency(tone, Block);

    /// <summary>The tone (1-255) nearest a MIDI note in this block, or null when the block can't play it</summary>
    public int? ToneOf(double midiNote)
    {
        var tone = (int)Math.Round(440 * Math.Pow(2, (midiNote - 69) / 12) * (1 << (20 - Block)) / OplPitch.ChipRate);
        return tone is >= 1 and <= 255 ? tone : null;
    }
}

/// <summary>
/// The AdLib sound editor: a sound's tones drawn on a strip, a tick at a time, with its
/// instrument, block and priority, played as the game plays it, undone and redone, saved into a
/// mod as sounds/NAME.adl
/// </summary>
public sealed partial class AdLibEditorViewModel : ObservableObject, IDisposable
{
    private readonly Stack<(AdLibDraft Draft, string What, int Version)> _undo = new();
    private readonly Stack<(AdLibDraft Draft, string What, int Version)> _redo = new();
    private int _version, _lastVersion, _savedVersion;
    private readonly SoundPlayer _player = new();
    private readonly DispatcherTimer? _timer;
    private AdLibDraft? _strokeStart;
    private bool _settingInstrument;
    private DateTime _lastInstrumentEdit;

    public AdLibEditorViewModel(string name, AdLibDraft draft, bool isNew, IReadOnlyList<InstrumentSource>? library = null)
    {
        Name = name;
        Draft = draft;
        Library = library ?? [];
        if (isNew && draft.Tones.Any(tone => tone != 0))
            _savedVersion = -1;
        InstrumentEditor.Set(draft.Instrument);
        InstrumentEditor.Changed += (_, instrument) => ChangeInstrument(instrument);
        Refresh();
        if (Avalonia.Application.Current != null)
        {
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Background, (_, _) => UpdatePlayhead());
            _player.Finished += (_, _) => Dispatcher.UIThread.Post(Stopped);
        }
    }

    public string Name { get; private set; }
    public IReadOnlyList<InstrumentSource> Library { get; }

    /// <summary>The sound as it is now; changed only through <see cref="Edit"/> and strokes</summary>
    public AdLibDraft Draft { get; private set; }

    public InstrumentEditorViewModel InstrumentEditor { get; } = new(ignoresFeedback: true);

    [ObservableProperty] private int _revision;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private bool _isDirty;

    public string Title => $"AdLib sound - {Name}{(IsDirty ? " *" : "")}";

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _info = "";

    //
    // Changes, undo and redo
    //

    public void Edit(string what, Func<AdLibDraft, AdLibDraft> change)
    {
        var changed = change(Draft);
        if (changed == Draft || changed.Priority == Draft.Priority && changed.Instrument == Draft.Instrument && changed.Block == Draft.Block
            && changed.Tones.AsSpan().SequenceEqual(Draft.Tones))
            return;
        _undo.Push((Draft, what, _version));
        _redo.Clear();
        Draft = changed;
        _version = ++_lastVersion;
        Changed(what);
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
        _redo.Push((Draft, what, _version));
        (Draft, _version) = (draft, version);
        ShowInstrument();
        Changed($"Undid: {what}");
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_redo.Count == 0)
            return;
        var (draft, what, version) = _redo.Pop();
        _undo.Push((Draft, what, _version));
        (Draft, _version) = (draft, version);
        ShowInstrument();
        Changed($"Redid: {what}");
    }

    private void ShowInstrument()
    {
        _settingInstrument = true;
        InstrumentEditor.Set(Draft.Instrument);
        _settingInstrument = false;
    }

    private void Refresh()
    {
        var sounding = Draft.Tones.Count(tone => tone != 0);
        Info = $"{Draft.Tones.Length} ticks ({Draft.Tones.Length / (double)AdLibDraft.TicksPerSecond:0.00} s), {sounding} sounding; block {Draft.Block}: "
               + $"tone 1 is {Draft.Frequency(1):0.#} Hz, 255 is {Draft.Frequency(255):0} Hz";
        OnPropertyChanged(nameof(Priority));
        OnPropertyChanged(nameof(Block));
        OnPropertyChanged(nameof(Length));
        Revision++;
    }

    //
    // The sound's settings
    //

    /// <summary>Plays over a sound of a lower priority, and isn't played over one of a higher</summary>
    public decimal? Priority
    {
        get => Draft.Priority;
        set
        {
            if (value is { } priority)
                Edit($"Priority {priority}", draft => draft with { Priority = (ushort)Math.Clamp(priority, 0, ushort.MaxValue) });
        }
    }

    /// <summary>The octave block the tones play in (0-7); each up is an octave higher</summary>
    public decimal? Block
    {
        get => Draft.Block;
        set
        {
            if (value is { } block)
                Edit($"Block {block}", draft => draft with { Block = (byte)Math.Clamp(block, 0, 7) });
        }
    }

    /// <summary>How long it is, in ticks; longer adds silence, shorter cuts the end off</summary>
    public decimal? Length
    {
        get => Draft.Tones.Length;
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

    // Instrument panel edits close together are one step
    private void ChangeInstrument(OplInstrument instrument)
    {
        if (_settingInstrument)
            return;
        if (_undo.Count > 0 && _undo.Peek().What == "Changed the instrument" && DateTime.UtcNow - _lastInstrumentEdit < TimeSpan.FromSeconds(2))
        {
            Draft = Draft with { Instrument = instrument };
            _version = ++_lastVersion;
            Changed("Changed the instrument");
        }
        else
            Edit("Changed the instrument", draft => draft with { Instrument = instrument });
        _lastInstrumentEdit = DateTime.UtcNow;
    }

    /// <summary>The instrument becomes a copy of one from a game song, AdLib sound or bank</summary>
    public void CopyInstrument(InstrumentSource source)
    {
        Edit($"Instrument copied from {source.Label}", draft => draft with { Instrument = source.Instrument });
        ShowInstrument();
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
    public void BeginStroke() => _strokeStart = Draft;

    /// <summary>Sets ticks' tones as a drag goes (0 for silence), growing the sound to hold them</summary>
    public void SetTones(int from, int to, Func<int, int> toneAt)
    {
        if (to < from)
            (from, to) = (to, from);
        from = Math.Max(0, from);
        var tones = Draft.Tones.Length > to ? (byte[])Draft.Tones.Clone() : [.. Draft.Tones, .. new byte[to + 1 - Draft.Tones.Length]];
        for (var tick = from; tick <= to; tick++)
        {
            var tone = Math.Clamp(toneAt(tick), 0, 255);
            if (tone > 0 && SnapToNotes)
            {
                var note = Math.Round(OplPitch.MidiNote(Draft.Frequency(tone)));
                tone = Draft.ToneOf(note) ?? tone;
            }
            tones[tick] = (byte)tone;
        }
        Draft = Draft with { Tones = tones };
        Revision++;
    }

    /// <summary>The drag ends: what it drew is one step to undo</summary>
    public void EndStroke(string what)
    {
        if (_strokeStart is not { } start)
            return;
        _strokeStart = null;
        if (start.Tones.AsSpan().SequenceEqual(Draft.Tones))
            return;
        _undo.Push((start, what, _version));
        _redo.Clear();
        _version = ++_lastVersion;
        Changed(what);
    }

    private (int From, int To) Span => Range is { } range ? (Math.Clamp(range.From, 0, Draft.Tones.Length), Math.Clamp(range.To, 0, Draft.Tones.Length)) : (0, Draft.Tones.Length);
    private string Where => Range != null ? "the ticks picked" : "the whole sound";

    /// <summary>Every sounding tone moved by some semitones (clamped to what the block holds)</summary>
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
                    tones[tick] = (byte)Math.Clamp((int)Math.Round(tones[tick] * factor), 1, 255);
            }
            return draft with { Tones = tones };
        });
    }

    [RelayCommand] private void Up() => Transpose(1);
    [RelayCommand] private void Down() => Transpose(-1);

    /// <summary>
    /// The block moved up or down with the tones halved or doubled, so it sounds the same with
    /// finer (down) or coarser (up) steps; tones past 255 or under 1 are clamped
    /// </summary>
    public void ShiftBlock(int by)
    {
        var block = Math.Clamp(Draft.Block + by, 0, 7);
        if (block == Draft.Block)
            return;
        var factor = Math.Pow(2, Draft.Block - block);
        Edit($"Block {block}, same pitch", draft => draft with
        {
            Block = (byte)block,
            Tones = [.. draft.Tones.Select(tone => tone == 0 ? (byte)0 : (byte)Math.Clamp((int)Math.Round(tone * factor), 1, 255))],
        });
    }

    [RelayCommand] private void BlockUp() => ShiftBlock(1);
    [RelayCommand] private void BlockDown() => ShiftBlock(-1);

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
        if (to - from >= Draft.Tones.Length)
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
    public short[] ToSamples() => Draft.ToSound().ToPcm16();

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
            PlayheadTick = (int)(_player.Position.TotalSeconds * AdLibDraft.TicksPerSecond);
    }

    //
    // Saving
    //

    /// <summary>Saves the sound into a mod (its name, the .adl file): the name it's saved as, or null</summary>
    public Func<string, byte[], Task<string?>>? SaveToMod { get; set; }

    public byte[] ToAdl() => Draft.ToSound().RawData;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SaveToMod == null)
            return;
        Stop();
        if (await SaveToMod(Name, ToAdl()) is { } saved)
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
