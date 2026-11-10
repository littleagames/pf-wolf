using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PFWolf.Assets.Sounds;
using PFWolf.Editor.Audio;
using PFWolf.Editor.Audio.Opl;

namespace PFWolf.Editor.ViewModels;

/// <summary>One of the OPL's nine channels, and whether the import gives notes to it</summary>
public sealed partial class OplChannelOption(int channel, bool isOn, Action changed) : ObservableObject
{
    public int Channel { get; } = channel;
    public string Label => Channel == 0 ? "0 (DOS Wolf3D's sound effects)" : Channel.ToString();

    [ObservableProperty] private bool _isOn = isOn;
    partial void OnIsOnChanged(bool value) => changed();
}

/// <summary>A MIDI channel of the file, how many notes it has, and whether it's imported</summary>
public sealed partial class MidiChannelOption(int channel, string label, Action changed) : ObservableObject
{
    public int Channel { get; } = channel;
    public string Label { get; } = label;

    [ObservableProperty] private bool _isOn = true;
    partial void OnIsOnChanged(bool value) => changed();
}

/// <summary>A note grid to snap the file's notes to</summary>
public sealed record QuantizeOption(int Divisor, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// The Import MIDI dialog: a MIDI file made into an IMF song as the options say, with what it
/// made of it, to hear and to save into a mod as music/NAME.imf
/// </summary>
public sealed partial class MidiImportViewModel : ObservableObject, IDisposable
{
    private readonly byte[] _data;
    private readonly SoundPlayer _player = new();
    private bool _building;

    /// <summary>Saves the song (its suggested name, the .imf file): true once it's in a mod</summary>
    public Func<string, byte[], Task<bool>>? Save { get; set; }

    public MidiImportViewModel(string fileName, byte[] data)
    {
        _data = data;
        FileName = Path.GetFileName(fileName);
        SuggestedName = new string(Path.GetFileNameWithoutExtension(fileName)
            .Select(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' ? char.ToUpperInvariant(c) : '_').ToArray());

        // Throws (InvalidDataException) for a file that isn't MIDI, before the dialog opens
        var midi = MidiFile.Read(data);
        Title = midi.Title;
        _building = true;
        for (var channel = 0; channel < OplRegisters.Channels; channel++)
            Channels.Add(new OplChannelOption(channel, channel > 0, Rebuild));
        foreach (var group in midi.Notes.GroupBy(note => note.Channel).OrderBy(group => group.Key))
        {
            var programs = group.Key == MidiFile.DrumChannel ? "drums"
                : string.Join(", ", group.Select(note => note.Program).Distinct().Order().Take(4).Select(program => $"program {program + 1}"));
            MidiChannels.Add(new MidiChannelOption(group.Key, $"Channel {group.Key + 1}: {group.Count()} notes ({programs})", Rebuild));
        }
        Banks.Add(OplBank.BuiltIn);
        _selectedBank = OplBank.BuiltIn;
        _selectedQuantize = Quantizes[0];
        _player.Finished += (_, _) => Dispatcher.UIThread.Post(() => IsPlaying = false);
        _building = false;
        Rebuild();
    }

    public string FileName { get; }
    public string? Title { get; }
    public string SuggestedName { get; }

    public ObservableCollection<OplChannelOption> Channels { get; } = [];
    public ObservableCollection<MidiChannelOption> MidiChannels { get; } = [];
    public ObservableCollection<OplBank> Banks { get; } = [];

    public IReadOnlyList<QuantizeOption> Quantizes { get; } =
    [
        new(0, "Off: as the file has them"),
        new(8, "Eighth notes"),
        new(16, "Sixteenth notes"),
        new(32, "Thirty-second notes"),
    ];

    [ObservableProperty] private OplBank _selectedBank;
    [ObservableProperty] private QuantizeOption _selectedQuantize;
    [ObservableProperty] private decimal? _transpose = 0;
    [ObservableProperty] private string _report = "";
    [ObservableProperty] private string _status = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayLabel))]
    private bool _isPlaying;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand), nameof(SaveSongCommand), nameof(EditSongCommand))]
    private byte[]? _imf;

    public string PlayLabel => IsPlaying ? "■ Stop" : "▶ Play";

    partial void OnSelectedBankChanged(OplBank value) => Rebuild();
    partial void OnSelectedQuantizeChanged(QuantizeOption value) => Rebuild();
    partial void OnTransposeChanged(decimal? value) => Rebuild();

    /// <summary>A DMX GENMIDI bank (.op2) to take the instruments from</summary>
    public void LoadBank(string fileName, byte[] data)
    {
        try
        {
            var bank = OplBank.FromOp2(data, Path.GetFileName(fileName));
            Banks.Add(bank);
            SelectedBank = bank;
            Status = $"Instruments from {bank.Name}";
        }
        catch (InvalidDataException e)
        {
            Status = $"{Path.GetFileName(fileName)} can't be used: {e.Message}";
        }
    }

    // The song again, as the options are now (what's playing stops)
    private void Rebuild()
    {
        if (_building)
            return;
        Stop();
        try
        {
            var midi = MidiFile.Read(_data, SelectedQuantize.Divisor);
            var options = new MidiImportOptions
            {
                Bank = SelectedBank,
                Channels = Channels.Where(channel => channel.IsOn).Select(channel => channel.Channel).ToList(),
                Transpose = (int)(Transpose ?? 0),
                Muted = MidiChannels.Where(channel => !channel.IsOn).Select(channel => channel.Channel).ToHashSet(),
            };
            var (song, report) = MidiImport.ToSong(midi, options);
            _song = song;
            Imf =Wolf3dImfAudio.ToFile(ImfSongWriter.Write(song), Title ?? SuggestedName, remarks: $"Imported from {FileName}");
            Report = report.ToString() + (Channels.Any(channel => channel.Channel == 0 && channel.IsOn)
                ? "\nChannel 0 is on: PFWolf plays it, but in DOS Wolf3D the AdLib sound effects take it over" : "");
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException)
        {
            Imf = null;
            Report = $"It can't be made into a song: {e.Message}";
        }
    }

    private bool HasSong => Imf != null;

    /// <summary>Plays the song as it'll be saved, or stops it</summary>
    [RelayCommand(CanExecute = nameof(HasSong))]
    private void Play()
    {
        if (IsPlaying)
        {
            Stop();
            return;
        }
        if (Imf == null)
            return;
        _player.Play(new ImfMusicSource(Wolf3dImfAudio.FromFile(Imf)), loop: false);
        IsPlaying = SoundPlayer.IsAvailable;
    }

    public void Stop()
    {
        _player.Stop();
        IsPlaying = false;
    }

    /// <summary>Raised once the song's in a mod (or opened in the song editor), for the dialog to close</summary>
    public event EventHandler? Saved;

    /// <summary>Opens a song in the song editor (its suggested name, the song)</summary>
    public Action<string, ImfSong>? Edit { get; set; }

    private ImfSong? _song;

    /// <summary>Opens the song as it's made now in the song editor, to change before saving</summary>
    [RelayCommand(CanExecute = nameof(HasSong))]
    private void EditSong()
    {
        if (_song == null || Edit == null)
            return;
        Stop();
        Edit(SuggestedName, _song.Clone());
        Saved?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(HasSong))]
    private async Task SaveSongAsync()
    {
        if (Imf == null || Save == null)
            return;
        Stop();
        if (await Save(SuggestedName, Imf))
            Saved?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _player.Dispose();
}
