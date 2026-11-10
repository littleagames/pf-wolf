using PFWolf.Assets.Sounds;

namespace PFWolf.Editor.Audio.Opl;

/// <summary>
/// A note on a channel: keyed on at <see cref="Start"/> (a tick, 700 a second) and off
/// <see cref="Length"/> ticks later (null: it sounds to the end of the song). Its pitch is kept
/// as the chip has it, so a song read in is written out the same; <see cref="Volume"/> is the
/// carrier's 0x40 register set as it starts (null: as it was).
/// </summary>
public sealed record ImfNote(int Channel, int Start, int? Length, int FNumber, int Block, byte? Volume = null)
{
    /// <summary>The tick it's keyed off, or null when it sounds to the end</summary>
    public int? End => Length is { } length ? Start + length : null;

    /// <summary>The B0-B8 value that keys it on</summary>
    public byte KeyOnValue => (byte)(OplRegisters.KeyOn | ((Block & 7) << 2) | ((FNumber >> 8) & 3));

    /// <summary>The A0-A8 value: the F-number's low byte</summary>
    public byte FNumberLow => (byte)(FNumber & 0xFF);

    public double Frequency => OplPitch.Frequency(FNumber, Block);

    /// <summary>The MIDI note it's nearest (60 = middle C) and how many cents off it is</summary>
    public (int Note, int Cents) NearestNote
    {
        get
        {
            var exact = OplPitch.MidiNote(Frequency);
            var note = (int)Math.Round(exact);
            return (note, (int)Math.Round((exact - note) * 100));
        }
    }
}

/// <summary>A channel's instrument (an index into <see cref="ImfSong.Instruments"/>) set from this tick on</summary>
public sealed record ImfPatchChange(int Tick, int Channel, int Instrument);

/// <summary>A channel's volume (its carrier's 0x40 register) set at a tick, apart from a note starting</summary>
public sealed record ImfVolumeChange(int Tick, int Channel, byte Level);

/// <summary>A register write the song model doesn't read as anything else, kept at its tick</summary>
public sealed record ImfWrite(int Tick, byte Register, byte Value);

/// <summary>
/// An IMF song as notes: what the song editor edits. <see cref="ImfSongReader"/> reads one from
/// IMF commands and <see cref="ImfSongWriter"/> writes it back; a song read and written again
/// strikes the same notes with the chip set the same, tick by tick.
/// </summary>
public sealed class ImfSong
{
    public const int TicksPerSecond = ImfMusicSource.MusicTicksPerSecond;

    public List<OplInstrument> Instruments { get; } = [];
    public List<ImfNote> Notes { get; } = [];
    public List<ImfPatchChange> PatchChanges { get; } = [];
    public List<ImfVolumeChange> VolumeChanges { get; } = [];

    /// <summary>Writes that aren't notes, instruments or volumes (the start's 00, BD and 08, a pitch set while no note sounds), in order</summary>
    public List<ImfWrite> Writes { get; } = [];

    /// <summary>How long it plays before it loops, in ticks; writes may be at this tick too, just before it loops</summary>
    public int Length { get; set; }

    public TimeSpan Duration => TimeSpan.FromSeconds(Length / (double)TicksPerSecond);

    /// <summary>The instrument a song's list has like this one, added to it when there's none</summary>
    public int InstrumentIndex(OplInstrument instrument)
    {
        var index = Instruments.IndexOf(instrument);
        if (index >= 0)
            return index;
        Instruments.Add(instrument);
        return Instruments.Count - 1;
    }

    /// <summary>The channels that play a note</summary>
    public IEnumerable<int> ChannelsUsed => Notes.Select(note => note.Channel).Distinct().Order();
}
