using PFWolf.Assets.Sounds;

namespace PFWolf.Editor.Audio.Opl;

/// <summary>How a MIDI file is turned into an IMF song</summary>
public sealed record MidiImportOptions
{
    public OplBank Bank { get; init; } = OplBank.BuiltIn;

    /// <summary>The OPL channels the notes are given out to: 1-8, as Wolf3D's songs (DOS Wolf3D plays AdLib sounds on channel 0)</summary>
    public IReadOnlyList<int> Channels { get; init; } = [1, 2, 3, 4, 5, 6, 7, 8];

    /// <summary>Semitones every note but the drums is moved</summary>
    public int Transpose { get; init; }

    /// <summary>MIDI channels (0-15) left out</summary>
    public IReadOnlySet<int> Muted { get; init; } = new HashSet<int>();

    /// <summary>Whether channel 10's drums are played (on the same channels as the rest)</summary>
    public bool Drums { get; init; } = true;
}

/// <summary>What an import made of the file, and what it had to leave out</summary>
public sealed record MidiImportReport(int Notes, int Imported, int CutShort, int OutOfRange, int NoDrum, int BendsSkipped, int Commands, TimeSpan Length,
    IReadOnlyList<int> ChannelsUsed)
{
    public bool TooLongForAudiot => Commands > Wolf3dImfAudio.MaxType1Commands;

    public override string ToString()
    {
        var lines = new List<string>
        {
            $"{Imported} of {Notes} notes, {Length:m\\:ss} long, on OPL channels {string.Join(", ", ChannelsUsed)}; {Commands} IMF commands",
        };
        if (CutShort > 0)
            lines.Add($"{CutShort} notes cut short: more were sounding at once than there are channels");
        if (OutOfRange > 0)
            lines.Add($"{OutOfRange} notes too high or low for the chip, left out");
        if (NoDrum > 0)
            lines.Add($"{NoDrum} drum hits the bank has no drum for, left out");
        if (BendsSkipped > 0)
            lines.Add($"{BendsSkipped} pitch bends during notes not played (a note keeps the bend it starts with)");
        if (TooLongForAudiot)
            lines.Add($"More than {Wolf3dImfAudio.MaxType1Commands} commands: it's saved as a type 0 .imf, which PFWolf plays but DOS Wolf3D's AUDIOT can't hold");
        return string.Join("\n", lines);
    }
}

/// <summary>
/// A MIDI file as an <see cref="ImfSong"/>: each note given to a free OPL channel (one already
/// set to its instrument first; when none is free, the one whose note started longest ago is
/// cut short), its instrument from the bank, its volume from velocity, channel volume and
/// expression, its pitch from its key, the transpose, the instrument's offset and the bend it
/// starts with. The song starts as Muse's do: 00, BD and 08 cleared, every channel used keyed
/// off and given its first instrument; it loops where the file ends.
/// </summary>
public static class MidiImport
{
    public static (ImfSong Song, MidiImportReport Report) ToSong(MidiFile midi, MidiImportOptions options)
    {
        var song = new ImfSong();
        // Channel 0 last: it's DOS Wolf3D's sound effects' channel, so only used when the rest are busy
        var channels = options.Channels.Where(channel => channel is >= 0 and < OplRegisters.Channels).Distinct()
            .OrderBy(channel => channel == 0 ? OplRegisters.Channels : channel).ToArray();
        if (channels.Length == 0)
            throw new ArgumentException("There are no channels to play on", nameof(options));

        // What each OPL channel is doing: its instrument now and the note it's playing
        var instrumentOn = new int?[OplRegisters.Channels];
        var playing = new int?[OplRegisters.Channels];   // index in song.Notes
        var firstInstrument = new int?[OplRegisters.Channels];
        int imported = 0, cutShort = 0, outOfRange = 0, noDrum = 0, considered = 0;

        foreach (var note in midi.Notes)
        {
            if (options.Muted.Contains(note.Channel) || note.IsDrum && !options.Drums)
                continue;
            considered++;

            var entry = note.IsDrum ? options.Bank.Drum(note.Key) : options.Bank.Melodic(note.Program);
            if (entry == null)
            {
                noDrum++;
                continue;
            }

            var key = entry.FixedNote ?? note.Key + entry.NoteOffset + (note.IsDrum ? 0 : options.Transpose);
            var pitch = OplPitch.FromMidiNote(key + (entry.FixedNote == null ? note.Bend : 0));
            var start = (int)Math.Round(note.Start * ImfSong.TicksPerSecond);
            var end = Math.Max(start + 1, (int)Math.Round(note.End * ImfSong.TicksPerSecond));
            if (pitch is not { } chip || OplPitch.Frequency(chip.FNumber, chip.Block) < 8)
            {
                outOfRange++;
                continue;
            }

            var instrument = song.InstrumentIndex(entry.Instrument);
            var channel = PickChannel(start, instrument);
            if (playing[channel] is { } playingIndex && song.Notes[playingIndex].End > start)
            {
                var cut = song.Notes[playingIndex];
                song.Notes[playingIndex] = cut with { Length = Math.Max(0, start - cut.Start) };
                cutShort++;
            }

            if (instrumentOn[channel] != instrument)
            {
                if (firstInstrument[channel] == null)
                    firstInstrument[channel] = instrument;
                else
                    song.PatchChanges.Add(new ImfPatchChange(start, channel, instrument));
                instrumentOn[channel] = instrument;
            }

            song.Notes.Add(new ImfNote(channel, start, end - start, chip.FNumber, chip.Block,
                Volume(entry.Instrument, note.Velocity, note.Volume, note.Expression)));
            playing[channel] = song.Notes.Count - 1;
            imported++;
        }

        // Zero-length notes (cut at once) are dropped; the start sets every channel used
        song.Notes.RemoveAll(note => note.Length == 0);
        song.Writes.AddRange([new ImfWrite(0, 0x00, 0x00), new ImfWrite(0, 0xBD, 0x00), new ImfWrite(0, 0x08, 0x00)]);
        foreach (var channel in channels.Where(channel => firstInstrument[channel] != null))
        {
            song.Writes.Add(new ImfWrite(0, (byte)(0xB0 + channel), 0));
            song.PatchChanges.Insert(0, new ImfPatchChange(0, channel, firstInstrument[channel]!.Value));
        }
        song.PatchChanges.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.Channel.CompareTo(b.Channel));

        var lastEnd = song.Notes.Count > 0 ? song.Notes.Max(note => note.End ?? 0) : 0;
        song.Length = Math.Max(lastEnd, (int)Math.Round(midi.Length * ImfSong.TicksPerSecond));

        var commands = ImfSongWriter.Write(song).Count;
        var report = new MidiImportReport(considered, song.Notes.Count, cutShort, outOfRange, noDrum, midi.BendsSkipped, commands, song.Duration,
            [.. song.ChannelsUsed]);
        return (song, report);

        int PickChannel(int start, int instrument)
        {
            bool Free(int channel) => playing[channel] is not { } index || song.Notes[index].End <= start;
            // A free channel already set to the instrument, else the one free longest, else the one whose note started first
            var free = channels.Where(Free).ToList();
            if (free.Where(channel => instrumentOn[channel] == instrument).Cast<int?>().FirstOrDefault() is { } same)
                return same;
            if (free.Count > 0)
                return free.OrderBy(channel => playing[channel] is { } index ? song.Notes[index].End ?? 0 : -1).First();
            return channels.OrderBy(channel => song.Notes[playing[channel]!.Value].Start).First();
        }
    }

    /// <summary>
    /// The carrier's 0x40 register for a note: the instrument's key scale level and level, made
    /// quieter by velocity, channel volume and expression (40·log10 of each, as most MIDI synths;
    /// the chip's level goes in 0.75 dB steps)
    /// </summary>
    public static byte Volume(OplInstrument instrument, int velocity, int volume, int expression)
    {
        var level = instrument.Carrier.Level;
        double Decibels(int value) => value <= 0 ? 96 : -40 * Math.Log10(Math.Min(value, 127) / 127.0);
        var quieter = (int)Math.Round((Decibels(velocity) + Decibels(volume) + Decibels(expression)) / 0.75);
        var total = Math.Min(0x3F, (level & 0x3F) + quieter);
        return (byte)((level & 0xC0) | total);
    }
}
