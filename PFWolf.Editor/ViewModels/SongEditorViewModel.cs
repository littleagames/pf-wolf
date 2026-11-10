using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets.Sounds;
using PFWolf.Editor.Audio;
using PFWolf.Editor.Audio.Opl;

namespace PFWolf.Editor.ViewModels;

/// <summary>One of the song's nine OPL channels in the song editor: its color, and whether it's heard and shown</summary>
public sealed partial class SongChannelItem(int channel, Action changed) : ObservableObject
{
    public static readonly Color[] Colors =
    [
        Color.FromRgb(0x9a, 0x9a, 0xa4), Color.FromRgb(0xe8, 0x6a, 0x5a), Color.FromRgb(0xe8, 0xb4, 0x4a),
        Color.FromRgb(0xb8, 0xd8, 0x50), Color.FromRgb(0x50, 0xc8, 0x78), Color.FromRgb(0x48, 0xc4, 0xd0),
        Color.FromRgb(0x5a, 0x8c, 0xe8), Color.FromRgb(0xa0, 0x6c, 0xe0), Color.FromRgb(0xe0, 0x6c, 0xb8),
    ];

    public int Channel { get; } = channel;
    public IBrush Brush { get; } = new SolidColorBrush(Colors[channel]);
    public string Label => Channel == 0 ? "Channel 0 (DOS Wolf3D's sound effects)" : $"Channel {Channel}";

    [ObservableProperty] private bool _isMuted;
    [ObservableProperty] private bool _isSolo;
    [ObservableProperty] private int _notes;

    partial void OnIsMutedChanged(bool value) => changed();
    partial void OnIsSoloChanged(bool value) => changed();

    public override string ToString() => Label;
}

/// <summary>An instrument of the song, as the lists show it</summary>
public sealed record SongInstrumentItem(int Index, string Label)
{
    public override string ToString() => Label;
}

/// <summary>An instrument to copy from: a game song's, an AdLib sound's or a bank's</summary>
public sealed record InstrumentSource(string Label, OplInstrument Instrument)
{
    public override string ToString() => Label;
}

public enum PianoTool
{
    /// <summary>A click on nothing puts a note there (drag for its length)</summary>
    Draw,
    /// <summary>A drag on nothing picks the notes in the box</summary>
    Select,
}

/// <summary>
/// The song editor: an IMF song as notes on a piano roll, its channels and instruments, played
/// from the cursor, changed with undo and redo, saved into a mod as music/NAME.imf
/// </summary>
public sealed partial class SongEditorViewModel : ObservableObject, IDisposable
{
    // Each with the version of the song it is: the song's unchanged since it was saved (or opened)
    // while its version is the one saved
    private readonly Stack<(ImfSong Song, string What, int Version)> _undo = new();
    private readonly Stack<(ImfSong Song, string What, int Version)> _redo = new();
    private int _version, _lastVersion, _savedVersion;
    private readonly SoundPlayer _player = new();
    private readonly SoundPlayer _audition = new();
    private readonly DispatcherTimer? _timer;
    private (int From, int To, bool Loop)? _playing;
    private DateTime _lastInstrumentEdit;
    private int? _lastInstrumentEdited;
    private bool _settingSelection;

    public SongEditorViewModel(string name, ImfSong song, bool isNew, IReadOnlyList<InstrumentSource>? library = null)
    {
        Name = name;
        Song = song;
        IsNew = isNew;
        Library = library ?? [];
        _ticksPerRow = ImfSongEdits.GuessTicksPerRow(song);
        for (var channel = 0; channel < OplRegisters.Channels; channel++)
            Channels.Add(new SongChannelItem(channel, () => Revision++));
        _currentChannel = Channels[song.ChannelsUsed.FirstOrDefault(channel => channel > 0, 1)];
        InstrumentEditor.Changed += (_, instrument) => ChangeInstrument(instrument);
        RefreshLists();
        CurrentInstrument = Instruments.FirstOrDefault();
        // A new song with notes already (from Import MIDI) isn't in a mod yet
        if (isNew && song.Notes.Count > 0)
            _savedVersion = -1;
        IsDirty = _version != _savedVersion;

        // Only with a UI to run it (tests have none)
        if (Avalonia.Application.Current != null)
        {
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Background, (_, _) => UpdatePlayhead());
            _player.Finished += (_, _) => Dispatcher.UIThread.Post(Stopped);
        }
    }

    public string Name { get; private set; }
    public bool IsNew { get; private set; }
    public IReadOnlyList<InstrumentSource> Library { get; }

    /// <summary>The song as it is now; changed only through <see cref="Edit"/></summary>
    public ImfSong Song { get; private set; }

    /// <summary>Goes up with every change to what's drawn (the song, the channels shown)</summary>
    [ObservableProperty] private int _revision;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private bool _isDirty;

    public string Title => $"Song editor - {Name}{(IsDirty ? " *" : "")}";

    /// <summary>What was last done, for the bar at the bottom</summary>
    [ObservableProperty] private string _status = "";

    [ObservableProperty] private string _info = "";

    //
    // Changes, undo and redo
    //

    /// <summary>Changes the song as one step that can be undone</summary>
    public void Edit(string what, Action<ImfSong> change)
    {
        _undo.Push((Song.Clone(), what, _version));
        _redo.Clear();
        change(Song);
        _version = ++_lastVersion;
        Changed(what);
    }

    private void Changed(string what)
    {
        IsDirty = _version != _savedVersion;
        Status = what;
        RefreshLists();
        Revision++;
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
        var (song, what, version) = _undo.Pop();
        _redo.Push((Song, what, _version));
        Song = song;
        _version = version;
        _lastInstrumentEdited = null;
        Selection = Selection.Where(Song.Notes.Contains).ToList();
        Changed($"Undid: {what}");
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_redo.Count == 0)
            return;
        var (song, what, version) = _redo.Pop();
        _undo.Push((Song, what, _version));
        Song = song;
        _version = version;
        _lastInstrumentEdited = null;
        Selection = Selection.Where(Song.Notes.Contains).ToList();
        Changed($"Redid: {what}");
    }

    //
    // Channels and instruments
    //

    public ObservableCollection<SongChannelItem> Channels { get; } = [];

    /// <summary>The channel new notes go on</summary>
    [ObservableProperty] private SongChannelItem _currentChannel;

    /// <summary>Whether a channel is heard and drawn bright: solo ones if any are, else those not muted</summary>
    public bool IsShown(int channel)
        => Channels.Any(item => item.IsSolo) ? Channels[channel].IsSolo : !Channels[channel].IsMuted;

    public ObservableCollection<SongInstrumentItem> Instruments { get; } = [];

    /// <summary>The instrument new notes are given, and the one the instrument panel edits</summary>
    [ObservableProperty] private SongInstrumentItem? _currentInstrument;

    public InstrumentEditorViewModel InstrumentEditor { get; } = new();

    partial void OnCurrentInstrumentChanged(SongInstrumentItem? value)
    {
        if (value != null && value.Index < Song.Instruments.Count)
            InstrumentEditor.Set(Song.Instruments[value.Index]);
        _lastInstrumentEdited = null;
    }

    // Edits made in the instrument panel: those to one instrument close together are one step
    private void ChangeInstrument(OplInstrument instrument)
    {
        if (CurrentInstrument is not { } current)
            return;
        var what = $"Changed instrument {current.Index + 1}";
        if (_lastInstrumentEdited == current.Index && DateTime.UtcNow - _lastInstrumentEdit < TimeSpan.FromSeconds(2) && _undo.Count > 0)
        {
            ImfSongEdits.ReplaceInstrument(Song, current.Index, instrument);
            _version = ++_lastVersion;
            Changed(what);
        }
        else
            Edit(what, song => ImfSongEdits.ReplaceInstrument(song, current.Index, instrument));
        _lastInstrumentEdited = current.Index;
        _lastInstrumentEdit = DateTime.UtcNow;
    }

    /// <summary>A new instrument, a copy of the one picked, to change</summary>
    [RelayCommand]
    private void NewInstrument()
    {
        var copy = CurrentInstrument is { } current && current.Index < Song.Instruments.Count ? Song.Instruments[current.Index] : OplBank.BuiltIn.Melodic(0).Instrument;
        Edit("New instrument", song => song.Instruments.Add(copy));
        CurrentInstrument = Instruments.LastOrDefault();
    }

    /// <summary>The instrument picked becomes a copy of one from a game song, AdLib sound or bank</summary>
    public void CopyInstrument(InstrumentSource source)
    {
        if (CurrentInstrument is not { } current)
            return;
        Edit($"Instrument {current.Index + 1} copied from {source.Label}", song => ImfSongEdits.ReplaceInstrument(song, current.Index, source.Instrument));
        InstrumentEditor.Set(source.Instrument);
    }

    /// <summary>Plays a note (middle C, or the note picked) with the instrument being edited</summary>
    [RelayCommand]
    private void TryInstrument()
    {
        var note = Selection.FirstOrDefault() ?? ImfSongEdits.NoteOf(1, 0, 1, 60);
        Audition(InstrumentEditor.ToInstrument(), note.FNumber, note.Block, null);
    }

    private void RefreshLists()
    {
        for (var index = 0; index < Song.Instruments.Count; index++)
        {
            var uses = Song.PatchChanges.Count(change => change.Instrument == index);
            var item = new SongInstrumentItem(index, $"Instrument {index + 1}  ({uses} use{(uses == 1 ? "" : "s")})");
            if (index < Instruments.Count)
            {
                if (Instruments[index] != item)
                {
                    var wasCurrent = CurrentInstrument?.Index == index;
                    Instruments[index] = item;
                    if (wasCurrent)
                        CurrentInstrument = item;
                }
            }
            else
                Instruments.Add(item);
        }
        while (Instruments.Count > Song.Instruments.Count)
            Instruments.RemoveAt(Instruments.Count - 1);
        if (CurrentInstrument is { } current && current.Index < Song.Instruments.Count)
            InstrumentEditor.Set(Song.Instruments[current.Index]);

        foreach (var channel in Channels)
            channel.Notes = Song.Notes.Count(note => note.Channel == channel.Channel);

        var commands = ImfSongWriter.Write(Song).Count;
        var lines = new List<string>
        {
            $"{Song.Notes.Count} notes, {Song.Duration:m\\:ss\\.f} ({Song.Length} ticks) before it loops; {commands} IMF commands",
        };
        if (commands > Wolf3dImfAudio.MaxType1Commands)
            lines.Add($"More than {Wolf3dImfAudio.MaxType1Commands} commands: saved as a type 0 .imf, which PFWolf plays but DOS Wolf3D's AUDIOT can't hold");
        if (Song.Notes.Any(note => note.Channel == 0))
            lines.Add("Channel 0 has notes: PFWolf plays them, but in DOS Wolf3D the AdLib sound effects take it over");
        Info = string.Join("\n", lines);
        OnPropertyChanged(nameof(KeptWrites));
        OnPropertyChanged(nameof(BeatsPerMinute));
    }

    //
    // The piano roll: grid, zoom, tool, selection
    //

    /// <summary>Ticks (700 a second) a grid row is: where notes snap</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BeatsPerMinute))]
    private double _ticksPerRow;

    /// <summary>Rows a beat (a brighter line every beat)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BeatsPerMinute))]
    private int _rowsPerBeat = 4;

    public string BeatsPerMinute => TicksPerRow > 0 && RowsPerBeat > 0
        ? $"{60.0 * ImfSong.TicksPerSecond / (TicksPerRow * RowsPerBeat):0.#} beats a minute" : "";

    partial void OnTicksPerRowChanged(double value)
    {
        if (value < 1)
            TicksPerRow = 1;
        Revision++;
    }

    partial void OnRowsPerBeatChanged(int value)
    {
        if (value < 1)
            RowsPerBeat = 1;
        Revision++;
    }

    /// <summary>Pixels a tick: the horizontal zoom</summary>
    [ObservableProperty] private double _pixelsPerTick = 0.25;

    [RelayCommand]
    private void ZoomIn() => PixelsPerTick = Math.Min(8, PixelsPerTick * 1.5);

    [RelayCommand]
    private void ZoomOut() => PixelsPerTick = Math.Max(0.01, PixelsPerTick / 1.5);

    [ObservableProperty] private PianoTool _tool = PianoTool.Draw;

    public bool IsDrawTool
    {
        get => Tool == PianoTool.Draw;
        set => Tool = value ? PianoTool.Draw : PianoTool.Select;
    }

    partial void OnToolChanged(PianoTool value) => OnPropertyChanged(nameof(IsDrawTool));

    /// <summary>The notes picked</summary>
    public IReadOnlyList<ImfNote> Selection
    {
        get => _selection;
        set
        {
            _selection = value;
            OnPropertyChanged();
            ShowSelection();
            Revision++;
        }
    }
    private IReadOnlyList<ImfNote> _selection = [];

    [ObservableProperty] private string _selectionText = "Nothing picked";
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private decimal? _selectionVolume;
    [ObservableProperty] private SongInstrumentItem? _selectionInstrument;

    private void ShowSelection()
    {
        _settingSelection = true;
        HasSelection = _selection.Count > 0;
        if (_selection.Count == 0)
        {
            SelectionText = "Nothing picked";
            SelectionVolume = null;
            SelectionInstrument = null;
        }
        else
        {
            var first = _selection[0];
            var (note, cents) = first.NearestNote;
            var bends = Song.PitchChangesOf(first).Count();
            SelectionText = _selection.Count == 1
                ? $"{ImfSongEdits.NoteName(note)}{(cents != 0 ? $" {cents:+0;-0} cents" : "")} on channel {first.Channel}, from tick {first.Start} for {first.Length?.ToString() ?? "the rest"} ticks"
                  + (bends > 0 ? $"; its pitch changes {bends} time{(bends == 1 ? "" : "s")} as it sounds" : "")
                : $"{_selection.Count} notes picked";
            SelectionVolume = 63 - (EffectiveLevel(first) & 0x3F);
            var instrument = ImfSongEdits.InstrumentAt(Song, first.Channel, first.Start);
            SelectionInstrument = Instruments.FirstOrDefault(item => item.Index == instrument);
        }
        _settingSelection = false;
        DeleteSelectionCommand.NotifyCanExecuteChanged();
    }

    // The carrier level a note plays at: its own, or what its instrument has
    private byte EffectiveLevel(ImfNote note)
        => note.Volume ?? (ImfSongEdits.InstrumentAt(Song, note.Channel, note.Start) is { } index && index < Song.Instruments.Count
            ? Song.Instruments[index].Carrier.Level : (byte)0);

    partial void OnSelectionVolumeChanged(decimal? value)
    {
        if (_settingSelection || value is not { } volume || _selection.Count == 0)
            return;
        var loudness = (int)Math.Clamp(volume, 0, 63);
        var picked = _selection.ToList();
        List<ImfNote> changed = [];
        Edit($"Volume {loudness} on {picked.Count} note{(picked.Count == 1 ? "" : "s")}", song =>
            changed = picked.Select(note => ImfSongEdits.SetVolume(song, [note], (byte)((EffectiveLevel(note) & 0xC0) | (63 - loudness)))[0]).ToList());
        _selection = changed;
        Revision++;
    }

    partial void OnSelectionInstrumentChanged(SongInstrumentItem? value)
    {
        if (_settingSelection || value == null || _selection.Count == 0)
            return;
        var picked = _selection.ToList();
        Edit($"{value.Label.Split("  ")[0]} on {picked.Count} note{(picked.Count == 1 ? "" : "s")}", song =>
        {
            foreach (var note in picked.OrderBy(note => note.Start))
                ImfSongEdits.SetInstrument(song, note, value.Index);
        });
    }

    /// <summary>Puts a note down where the piano roll was clicked, on the current channel with the current instrument</summary>
    public ImfNote AddNote(int tick, int midiNote, int length)
    {
        var instrument = CurrentInstrument?.Index ?? 0;
        if (Song.Instruments.Count == 0)
            Song.Instruments.Add(OplBank.BuiltIn.Melodic(0).Instrument);
        var note = ImfSongEdits.NoteOf(CurrentChannel.Channel, Math.Max(0, tick), Math.Max(1, length), midiNote);
        ImfNote added = note;
        var before = Song.Notes.Count;
        Edit($"Added {ImfSongEdits.NoteName(midiNote)} on channel {note.Channel}", song => added = ImfSongEdits.AddNote(song, note, instrument));
        if (before + 1 - Song.Notes.Count is > 0 and var lost)
            Status += $" (it took out {lost} note{(lost == 1 ? "" : "s")} it landed on: a channel plays one at a time; Undo puts them back)";
        Selection = [added];
        AuditionNote(added);
        return added;
    }

    /// <summary>The notes picked moved by ticks and semitones</summary>
    public void MoveSelection(int ticks, int semitones)
    {
        if (_selection.Count == 0 || ticks == 0 && semitones == 0)
            return;
        var picked = _selection.ToList();
        List<ImfNote> moved = [];
        var before = Song.Notes.Count;
        Edit($"Moved {picked.Count} note{(picked.Count == 1 ? "" : "s")}", song => moved = ImfSongEdits.MoveNotes(song, picked, ticks, semitones));
        if (before - Song.Notes.Count is > 0 and var lost)
            Status += $" (they took out {lost} note{(lost == 1 ? "" : "s")} they landed on; Undo puts them back)";
        Selection = moved;
        if (semitones != 0 && moved.Count > 0)
            AuditionNote(moved[0]);
    }

    /// <summary>The notes picked moved to another channel</summary>
    public void MoveSelectionToChannel(int channel)
    {
        if (_selection.Count == 0)
            return;
        var picked = _selection.ToList();
        List<ImfNote> moved = [];
        Edit($"Moved {picked.Count} note{(picked.Count == 1 ? "" : "s")} to channel {channel}", song => moved = ImfSongEdits.MoveNotes(song, picked, 0, 0, channel));
        Selection = moved;
    }

    public void ResizeNote(ImfNote note, int length)
    {
        if (note.Length == length)
            return;
        ImfNote resized = note;
        Edit($"Made {ImfSongEdits.NoteName(note.NearestNote.Note)} {length} ticks long", song => resized = ImfSongEdits.Resize(song, note, length));
        Selection = [resized];
    }

    private bool CanDelete => _selection.Count > 0;

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void DeleteSelection()
    {
        var picked = _selection.ToList();
        Edit($"Deleted {picked.Count} note{(picked.Count == 1 ? "" : "s")}", song => ImfSongEdits.RemoveNotes(song, picked));
        Selection = [];
    }

    [RelayCommand]
    private void SelectAll() => Selection = Song.Notes.Where(note => IsShown(note.Channel)).ToList();

    /// <summary>Plays a note as the song would: its instrument and volume</summary>
    public void AuditionNote(ImfNote note)
    {
        var index = ImfSongEdits.InstrumentAt(Song, note.Channel, note.Start) ?? CurrentInstrument?.Index ?? 0;
        if (index < Song.Instruments.Count)
            Audition(Song.Instruments[index], note.FNumber, note.Block, note.Volume);
    }

    private void Audition(OplInstrument instrument, int fNumber, int block, byte? level)
    {
        if (!SoundPlayer.IsAvailable || IsPlaying)
            return;
        _audition.Play(new SampleMusicSource(OplAudition.Note(instrument, fNumber, block, level, 0.3, 0.3), ImfMusicSource.MusicSampleRate), loop: false);
    }

    /// <summary>The song's loop point (its length), in ticks; never before the last note ends</summary>
    public decimal? SongLength
    {
        get => Song.Length;
        set
        {
            if (value is not { } length)
                return;
            var lastEnd = Song.Notes.Select(note => note.End ?? 0).DefaultIfEmpty(0).Max();
            var ticks = Math.Max((int)length, Math.Max(1, lastEnd));
            if (ticks != Song.Length)
                Edit($"Song made {ticks} ticks long", song => song.Length = ticks);
            OnPropertyChanged();
        }
    }

    //
    // Playing
    //

    /// <summary>Where play starts from, in ticks (set by clicking the ruler)</summary>
    [ObservableProperty] private int _cursorTick;

    /// <summary>Where playing is, in ticks; -1 when it isn't</summary>
    [ObservableProperty] private int _playheadTick = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayLabel))]
    private bool _isPlaying;

    public string PlayLabel => IsPlaying ? "■ Stop" : "▶ Play";

    [ObservableProperty] private bool _loop;

    /// <summary>The loop region, in ticks, or null for the whole song</summary>
    [ObservableProperty] private (int From, int To)? _loopRegion;

    partial void OnCursorTickChanged(int value) => Revision++;
    partial void OnLoopRegionChanged((int From, int To)? value) => Revision++;

    [RelayCommand]
    private void LoopSelection()
    {
        if (_selection.Count == 0)
            return;
        LoopRegion = (_selection.Min(note => note.Start), _selection.Max(note => note.End ?? Song.Length));
        Loop = true;
    }

    [RelayCommand]
    private void ClearLoop() => LoopRegion = null;

    [RelayCommand]
    private void Play()
    {
        if (IsPlaying)
        {
            Stop();
            return;
        }
        var commands = ImfSongWriter.Write(Muted(Song));
        var (from, to) = LoopRegion is { } region && Loop ? region : (Math.Min(CursorTick, Math.Max(0, Song.Length - 1)), Song.Length);
        _audition.Stop();
        _playing = (from, to, Loop);
        _player.Play(new ImfRangeSource(commands, from, to), Loop);
        IsPlaying = SoundPlayer.IsAvailable;
        _timer?.Start();
    }

    // The song with the channels not shown left out, as it's heard while editing
    private ImfSong Muted(ImfSong song)
    {
        if (Channels.All(channel => IsShown(channel.Channel)))
            return song;
        var heard = song.Clone();
        ImfSongEdits.RemoveNotes(heard, heard.Notes.Where(note => !IsShown(note.Channel)));
        return heard;
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
        _playing = null;
    }

    private void UpdatePlayhead()
    {
        if (_playing is not { } playing)
            return;
        var ticks = (int)(_player.Position.TotalSeconds * ImfSong.TicksPerSecond);
        var span = Math.Max(1, playing.To - playing.From);
        PlayheadTick = playing.From + (playing.Loop ? ticks % span : Math.Min(ticks, span));
    }

    //
    // The song's other writes and its commands
    //

    /// <summary>The writes kept as they are (the start, pitches set while nothing sounds), for the raw view</summary>
    public IEnumerable<string> KeptWrites => Song.Writes.Select(write => $"{write.Tick,7}   {write.Register:X2} = {write.Value:X2}   {RegisterName(write.Register)}");

    /// <summary>The song as the IMF commands it's saved as, for the raw view</summary>
    public IReadOnlyList<string> CommandLines()
    {
        var lines = new List<string>();
        var tick = 0;
        foreach (var command in ImfSongWriter.Write(Song))
        {
            lines.Add($"{tick,7}   {command.Register:X2} = {command.Value:X2}   wait {command.Delay,-5}  {RegisterName(command.Register)}");
            tick += command.Delay;
        }
        return lines;
    }

    /// <summary>What an OPL2 register is</summary>
    public static string RegisterName(byte register)
    {
        if (OplRegisters.OperatorOf(register) is { } op)
        {
            var part = (register & 0xE0) switch
            {
                0x20 => "tremolo/vibrato/sustain/KSR/multiplier",
                0x40 => "key scale/level",
                0x60 => "attack/decay",
                0x80 => "sustain/release",
                _ => "waveform",
            };
            return $"channel {op.Channel} {(op.IsCarrier ? "carrier" : "modulator")} {part}";
        }
        if (OplRegisters.ChannelOf(register, 0xA0) is { } a)
            return $"channel {a} F-number (low)";
        if (OplRegisters.ChannelOf(register, 0xB0) is { } b)
            return $"channel {b} key on/block/F-number (high)";
        if (OplRegisters.ChannelOf(register, 0xC0) is { } c)
            return $"channel {c} feedback/connection";
        return register switch
        {
            0x00 => "test (unused)",
            0x01 => "waveform select enable",
            0x08 => "CSM/note select",
            0xBD => "tremolo/vibrato depth, rhythm mode",
            _ => "",
        };
    }

    //
    // Saving
    //

    /// <summary>Saves the song into a mod (its name, the .imf file): the name it's saved as, or null when it isn't</summary>
    public Func<string, byte[], Task<string?>>? SaveToMod { get; set; }

    /// <summary>The song as the .imf file it's saved as</summary>
    public byte[] ToImf() => Wolf3dImfAudio.ToFile(ImfSongWriter.Write(Song), Name);

    /// <summary>The song once through as 16-bit mono at 44100 Hz</summary>
    public short[] ToSamples() => ImfRangeSource.RenderMono(ImfSongWriter.Write(Song), 0, Song.Length);

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SaveToMod == null)
            return;
        Stop();
        if (await SaveToMod(Name, ToImf()) is { } saved)
        {
            Name = saved;
            IsNew = false;
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
        _audition.Dispose();
    }
}
