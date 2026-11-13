using PFWolf.Assets.Sounds;
using PFWolf.Editor.Audio.Opl;

namespace PFWolf.Tests;

/// <summary>Reading MIDI files, GENMIDI banks, and MIDI turned into IMF songs</summary>
public class MidiImportTests
{
    // A MIDI file built by hand: tracks of (delta, bytes) events, each ended with End of Track
    private static byte[] Midi(int format, int division, params (int Delta, byte[] Bytes)[][] tracks)
    {
        var file = new List<byte>();
        file.AddRange("MThd"u8.ToArray());
        file.AddRange(BigEndian(6, 4));
        file.AddRange(BigEndian(format, 2));
        file.AddRange(BigEndian(tracks.Length, 2));
        file.AddRange(BigEndian(division, 2));
        foreach (var track in tracks)
        {
            var body = new List<byte>();
            foreach (var (delta, bytes) in track.Append((0, [0xFF, 0x2F, 0x00])))
            {
                body.AddRange(VariableLength(delta));
                body.AddRange(bytes);
            }
            file.AddRange("MTrk"u8.ToArray());
            file.AddRange(BigEndian(body.Count, 4));
            file.AddRange(body);
        }
        return [.. file];
    }

    private static byte[] BigEndian(int value, int bytes) => Enumerable.Range(0, bytes).Select(i => (byte)(value >> (8 * (bytes - 1 - i)))).ToArray();

    private static byte[] VariableLength(int value)
    {
        var bytes = new List<byte> { (byte)(value & 0x7F) };
        for (value >>= 7; value > 0; value >>= 7)
            bytes.Insert(0, (byte)(0x80 | (value & 0x7F)));
        return [.. bytes];
    }

    private static (int, byte[]) On(int delta, int key, int velocity = 100, int channel = 0) => (delta, [(byte)(0x90 | channel), (byte)key, (byte)velocity]);
    private static (int, byte[]) Off(int delta, int key, int channel = 0) => (delta, [(byte)(0x80 | channel), (byte)key, 0]);
    private static (int, byte[]) Tempo(int delta, int microsecondsPerQuarter) => (delta, [0xFF, 0x51, 0x03, .. BigEndian(microsecondsPerQuarter, 3)]);

    [Test]
    public void Notes_Are_Timed_By_The_Tempo_Changes()
    {
        // 480 ticks a quarter; 120 bpm, then 240 bpm from tick 960
        var midi = MidiFile.Read(Midi(0, 480, [On(480, 69), Off(480, 69), Tempo(0, 250000), On(480, 72), Off(480, 72)]));

        Assert.That(midi.Notes, Has.Count.EqualTo(2));
        Assert.That((midi.Notes[0].Start, midi.Notes[0].End), Is.EqualTo((0.5, 1.0)));
        Assert.That(midi.Notes[1].Start, Is.EqualTo(1.25).Within(1e-9));
        Assert.That(midi.Notes[1].End, Is.EqualTo(1.5).Within(1e-9));
        Assert.That(midi.Length, Is.EqualTo(1.5).Within(1e-9));
    }

    [Test]
    public void Running_Status_And_Velocity_0_As_Note_Off()
    {
        // 90 3C 64, then 3C 00 (running status: a note-on of velocity 0 is a note-off), then 40 64, 40 00
        var midi = MidiFile.Read(Midi(0, 96, [On(0, 60), (96, [60, 0]), (0, [64, 100]), (96, [64, 0])]));
        Assert.That(midi.Notes.Select(note => (note.Key, note.Start, note.End)), Is.EqualTo(new[] { (60, 0.0, 0.5), (64, 0.5, 1.0) }));
    }

    [Test]
    public void Type_1_Tracks_Play_Together_With_Their_Programs_And_Volumes()
    {
        var midi = MidiFile.Read(Midi(1, 96,
            [Tempo(0, 500000)],
            [(0, [0xC1, 33]), (0, [0xB1, 7, 64]), On(0, 40, channel: 1), Off(192, 40, channel: 1)],
            [On(96, 76, channel: 2), Off(96, 76, channel: 2)]));

        Assert.That(midi.Notes, Has.Count.EqualTo(2));
        var bass = midi.Notes.Single(note => note.Channel == 1);
        Assert.That((bass.Program, bass.Volume, bass.Start, bass.End), Is.EqualTo((33, 64, 0.0, 1.0)));
        Assert.That(midi.Notes.Single(note => note.Channel == 2).Start, Is.EqualTo(0.5));
    }

    [Test]
    public void Quantized_Notes_Snap_To_The_Grid()
    {
        // A sixteenth at 96 a quarter is 24 ticks: 22 snaps to 24, 50 to 48
        var midi = MidiFile.Read(Midi(0, 96, [On(22, 60), Off(28, 60)]), quantizeDivisor: 16);
        Assert.That(midi.Notes[0].Start, Is.EqualTo(24 / 192.0).Within(1e-9));
        Assert.That(midi.Notes[0].End, Is.EqualTo(48 / 192.0).Within(1e-9));
    }

    [Test]
    public void A_Note_Never_Let_Go_Ends_With_The_File_And_Bends_Are_Kept_As_Notes_Start()
    {
        // Bend up 2 semitones (the default range) before the note, then a bend during it
        var midi = MidiFile.Read(Midi(0, 96, [(0, [0xE0, 0x7F, 0x7F]), On(0, 60), (48, [0xE0, 0x00, 0x40]), (48, [0xFF, 0x01, 0x00])]));
        Assert.That(midi.Notes[0].End, Is.EqualTo(0.5));
        Assert.That(midi.Notes[0].Bend, Is.EqualTo(2).Within(0.001));
        Assert.That(midi.BendsSkipped, Is.EqualTo(1));
    }

    [Test]
    public void Not_A_Midi_File_Says_Why()
    {
        Assert.Throws<InvalidDataException>(() => MidiFile.Read("RIFF0000"u8.ToArray()));
        var cutOff = Midi(0, 96, [On(0, 60), Off(96, 60)]);
        Assert.Throws<InvalidDataException>(() => MidiFile.Read(cutOff[..^5]));
    }

    [Test]
    public void Imported_Notes_Get_Channels_Instruments_Pitches_And_Volumes()
    {
        var midi = MidiFile.Read(Midi(0, 480, [(0, [0xC0, 0]), On(0, 69, 127), On(240, 73, 64), Off(240, 69), Off(480, 73)]));
        var (song, report) = MidiImport.ToSong(midi, new MidiImportOptions());

        Assert.That(song.Notes, Has.Count.EqualTo(2));
        var a = song.Notes[0];
        Assert.That(a.Channel, Is.EqualTo(1));
        Assert.That((a.Start, a.Length), Is.EqualTo((0, 350)));
        Assert.That(a.Frequency, Is.EqualTo(440).Within(1));
        var piano = OplBank.BuiltIn.Melodic(0).Instrument;
        Assert.That(a.Volume, Is.EqualTo(MidiImport.Volume(piano, 127, 100, 127)));

        var cSharp = song.Notes[1];
        Assert.That(cSharp.Channel, Is.EqualTo(2), "the first channel is still playing");
        Assert.That(cSharp.Start, Is.EqualTo(175));
        Assert.That(cSharp.NearestNote.Note, Is.EqualTo(73));
        Assert.That(cSharp.Volume!.Value & 0x3F, Is.GreaterThan(a.Volume!.Value & 0x3F), "quieter");

        Assert.That(song.PatchChanges, Is.EqualTo(new[] { new ImfPatchChange(0, 1, 0), new ImfPatchChange(0, 2, 0) }));
        Assert.That(song.Length, Is.EqualTo(700));
        Assert.That(report.Imported, Is.EqualTo(2));
        Assert.That(report.ChannelsUsed, Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void More_Notes_At_Once_Than_Channels_Cut_The_Oldest_Short()
    {
        // Three notes struck a little apart, held together; two channels to play them on
        var events = new List<(int, byte[])>();
        for (var i = 0; i < 3; i++)
            events.Add(On(i == 0 ? 0 : 10, 60 + i));
        for (var i = 0; i < 3; i++)
            events.Add(Off(i == 0 ? 500 : 0, 60 + i));
        var (song, report) = MidiImport.ToSong(MidiFile.Read(Midi(0, 96, [.. events])), new MidiImportOptions { Channels = [3, 4] });

        Assert.That(report.CutShort, Is.EqualTo(1));
        Assert.That(song.Notes.Select(note => note.Channel).Distinct().Order(), Is.EqualTo(new[] { 3, 4 }));
        var first = song.Notes.Single(note => note.NearestNote.Note == 60);
        var third = song.Notes.Single(note => note.NearestNote.Note == 62);
        Assert.That(third.Channel, Is.EqualTo(first.Channel), "the third note takes the first one's channel");
        Assert.That(first.End, Is.EqualTo(third.Start));
    }

    [Test]
    public void Drums_Play_Their_Fixed_Notes_And_Muted_Channels_Are_Left_Out()
    {
        var file = Midi(0, 96, [On(0, 36, channel: 9), Off(48, 36, channel: 9), On(0, 60, channel: 3), Off(48, 60, channel: 3)]);
        var (song, _) = MidiImport.ToSong(MidiFile.Read(file), new MidiImportOptions { Muted = new HashSet<int> { 3 } });

        var kick = song.Notes.Single();
        Assert.That(kick.NearestNote.Note, Is.EqualTo(OplBank.BuiltIn.Drum(36)!.FixedNote));
        Assert.That(song.Instruments[song.PatchChanges.Single().Instrument], Is.EqualTo(OplBank.BuiltIn.Drum(36)!.Instrument));

        var (noDrums, report) = MidiImport.ToSong(MidiFile.Read(file), new MidiImportOptions { Drums = false });
        Assert.That(noDrums.Notes.Single().NearestNote.Note, Is.EqualTo(60));
        Assert.That(report.Notes, Is.EqualTo(1));
    }

    [Test]
    public void An_Imported_Song_Plays_Its_Notes()
    {
        var midi = MidiFile.Read(Midi(0, 96, [(0, [0xC0, 40]), On(0, 60), Off(96, 60), (0, [0xC0, 0]), On(0, 64), Off(96, 64), On(0, 67), Off(96, 67)]));
        var (song, report) = MidiImport.ToSong(midi, new MidiImportOptions { Transpose = 12, Channels = [1] });
        var commands = ImfSongWriter.Write(song);
        var playback = ImfPlayback.Of(commands);

        Assert.That(playback.Ticks.Values.Sum(tick => tick.Strikes.Length), Is.EqualTo(3));
        Assert.That(report.Commands, Is.EqualTo(commands.Count));
        Assert.That(song.Notes[0].NearestNote.Note, Is.EqualTo(72), "transposed an octave");
        // Two instruments on one channel: the second is changed to as its note starts
        Assert.That(song.PatchChanges.Select(change => change.Tick), Is.EqualTo(new[] { 0, 350 }));

        // And plays on the chip without complaint
        using var source = new ImfMusicSource(Wolf3dImfAudio.FromFile(Wolf3dImfAudio.ToFile(commands)));
        var stereo = new short[44100 * 2];
        Assert.That(source.Read(stereo, loop: false), Is.GreaterThan(0));
        Assert.That(stereo.Any(sample => sample != 0), Is.True);
    }

    [Test]
    public void The_Import_Dialog_Rebuilds_As_Its_Options_Change_And_Saves_The_Song()
    {
        var file = Midi(0, 96, [On(0, 60), On(0, 36, channel: 9), Off(96, 60), Off(0, 36, channel: 9), On(0, 64, channel: 1), Off(96, 64, channel: 1)]);
        // (A path in this system's own form: off Windows, '\' isn't a separator)
        using var import = new PFWolf.Editor.ViewModels.MidiImportViewModel(Path.Combine(Path.GetTempPath(), "music", "My Song.mid"), file);

        Assert.That(import.SuggestedName, Is.EqualTo("MY_SONG"));
        Assert.That(import.MidiChannels.Select(channel => channel.Channel), Is.EqualTo(new[] { 0, 1, 9 }));
        Assert.That(import.Channels.Where(channel => channel.IsOn).Select(channel => channel.Channel), Is.EqualTo(Enumerable.Range(1, 8)));
        Assert.That(import.Report, Does.StartWith("3 of 3 notes"));
        var before = import.Imf;
        Assert.That(before, Is.Not.Null);

        import.MidiChannels.Single(channel => channel.Channel == 9).IsOn = false;
        Assert.That(import.Report, Does.StartWith("2 of 2 notes"));
        import.Channels[0].IsOn = true;
        Assert.That(import.Report, Does.Contain("Channel 0 is on"));
        Assert.That(import.Report, Does.Contain("on OPL channels 1;"), "channel 0 only once the others are busy");
        import.Transpose = 12;
        Assert.That(ImfSongReader.Read(Wolf3dImfAudio.FromFile(import.Imf!)).Notes[0].NearestNote.Note, Is.EqualTo(72));

        (string Name, byte[] Data)? saved = null;
        import.Save = (name, data) => { saved = (name, data); return Task.FromResult(true); };
        var closed = false;
        import.Saved += (_, _) => closed = true;
        import.SaveSongCommand.Execute(null);
        Assert.That(saved?.Name, Is.EqualTo("MY_SONG"));
        Assert.That(saved?.Data, Is.EqualTo(import.Imf));
        Assert.That(closed, Is.True);

        Assert.Throws<InvalidDataException>(() => new PFWolf.Editor.ViewModels.MidiImportViewModel("x.mid", [1, 2, 3]));
    }

    [Test]
    public void Volume_Is_Quieter_With_Velocity()
    {
        var instrument = new OplInstrument(default, new OplOperator(0, 0x45, 0, 0, 0), 0);
        Assert.That(MidiImport.Volume(instrument, 127, 127, 127), Is.EqualTo(0x45), "full: the instrument's own level, key scale kept");
        // 64 of 127 is 11.9 dB down: 16 steps of 0.75 dB
        Assert.That(MidiImport.Volume(instrument, 64, 127, 127), Is.EqualTo(0x40 | (0x05 + 16)));
        Assert.That(MidiImport.Volume(instrument, 1, 127, 127), Is.EqualTo(0x7F), "no quieter than the chip goes");
    }

    [Test]
    public void Genmidi_Bank_Gives_Programs_And_Drums()
    {
        var data = new byte[8 + 175 * 36 + 175 * 32];
        "#OPL_II#"u8.CopyTo(data);
        void Instrument(int index, ushort flags, byte fixedNote, short noteOffset, string name)
        {
            var at = 8 + index * 36;
            BitConverter.TryWriteBytes(data.AsSpan(at), flags);
            data[at + 3] = fixedNote;
            // modulator: tremolo 21, attack F2, sustain 53, wave 1, scale 40, level 0F; feedback 0E
            new byte[] { 0x21, 0xF2, 0x53, 0x01, 0x40, 0x0F, 0x0E, 0x01, 0xF3, 0x74, 0x00, 0x80, 0x02, 0x00 }.CopyTo(data, at + 4);
            BitConverter.TryWriteBytes(data.AsSpan(at + 4 + 14), noteOffset);
            System.Text.Encoding.ASCII.GetBytes(name).CopyTo(data, 8 + 175 * 36 + index * 32);
        }
        Instrument(0, 0, 0, -12, "Acoustic Grand");
        Instrument(128, 1, 35, 0, "Kick");

        var bank = OplBank.FromOp2(data, "test");
        var piano = bank.Melodic(0);
        Assert.That(piano.Name, Is.EqualTo("Acoustic Grand"));
        Assert.That(piano.NoteOffset, Is.EqualTo(-12));
        Assert.That(piano.Instrument, Is.EqualTo(new OplInstrument(new OplOperator(0x21, 0x4F, 0xF2, 0x53, 0x01), new OplOperator(0x01, 0x82, 0xF3, 0x74, 0x00), 0x0E)));
        Assert.That(bank.Drum(35), Is.EqualTo(new OplBankInstrument("Kick", piano.Instrument, 0, 35)));
        Assert.Throws<InvalidDataException>(() => OplBank.FromOp2(new byte[100], "bad"));
    }
}
