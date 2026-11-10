using PFWolf.Assets.Sounds;
using PFWolf.Editor.Audio.Opl;
using Command = PFWolf.Assets.Sounds.Wolf3dImfAudio.WolfensteinMusicCommand;

namespace PFWolf.Tests;

/// <summary>The OPL editor's core: instruments, the IMF / AdLib / PC speaker writers and the IMF song model</summary>
public class ImfSongTests
{
    private static readonly OplInstrument Piano = new(new OplOperator(0x01, 0x4F, 0xF1, 0x53, 0x00), new OplOperator(0x11, 0x00, 0xF2, 0x74, 0x01), 0x06);

    [Test]
    public void Instrument_Round_Trips_Through_AdLib_Bytes_And_Registers()
    {
        var bytes = new byte[OplInstrument.AdLibBytes];
        Piano.ToAdLibBytes(bytes);
        Assert.That(bytes, Is.EqualTo(new byte[] { 0x01, 0x11, 0x4F, 0x00, 0xF1, 0xF2, 0x53, 0x74, 0x00, 0x01, 0x06 }));
        Assert.That(OplInstrument.FromAdLibBytes(bytes), Is.EqualTo(Piano));

        var registers = new byte[256];
        foreach (var (register, value) in Piano.Registers(4))
            registers[register] = value;
        // Channel 4: modulator slot 9, carrier slot 12
        Assert.That(registers[0x29], Is.EqualTo(0x01));
        Assert.That(registers[0x4C], Is.EqualTo(0x00));
        Assert.That(registers[0xEC], Is.EqualTo(0x01));
        Assert.That(registers[0xC4], Is.EqualTo(0x06));
        Assert.That(OplInstrument.FromRegisters(registers, 4), Is.EqualTo(Piano));
    }

    [TestCase((byte)0x20, 0, false)]
    [TestCase((byte)0x23, 0, true)]
    [TestCase((byte)0x4C, 4, true)]
    [TestCase((byte)0x51, 7, false)]
    [TestCase((byte)0xF5, 8, true)]
    public void Operator_Registers_Belong_To_Their_Channel(byte register, int channel, bool isCarrier)
        => Assert.That(OplRegisters.OperatorOf(register), Is.EqualTo((channel, isCarrier)));

    [TestCase((byte)0x26)]
    [TestCase((byte)0xA0)]
    [TestCase((byte)0xBD)]
    public void Other_Registers_Belong_To_No_Operator(byte register) => Assert.That(OplRegisters.OperatorOf(register), Is.Null);

    [Test]
    public void Pitch_Of_A_440()
    {
        var (fNumber, block) = OplPitch.FromMidiNote(69)!.Value;
        Assert.That(OplPitch.Frequency(fNumber, block), Is.EqualTo(440).Within(0.5));
        Assert.That(OplPitch.MidiNote(OplPitch.Frequency(fNumber, block)), Is.EqualTo(69).Within(0.01));
        // Wolf3D's songs' A1=8B B1=36: F-number 0x28B in block 5, 988 Hz, B5
        Assert.That(new ImfNote(1, 0, null, 0x28B, 5).NearestNote, Is.EqualTo((83, 0)));
    }

    [Test]
    public void Imf_File_Type_1_Has_Its_Length_And_Tag()
    {
        Command[] commands = [new(0x00, 0x00, 0), new(0xA1, 0x8B, 0), new(0xB1, 0x36, 70)];
        var file = Wolf3dImfAudio.ToFile(commands, "Test song");
        Assert.That(BitConverter.ToUInt16(file, 0), Is.EqualTo(12));
        Assert.That(file[14], Is.EqualTo(0x1A));
        Assert.That(Wolf3dImfAudio.FromFile(file).Commands, Is.EqualTo(commands));

        var untagged = Wolf3dImfAudio.ToFile(commands);
        Assert.That(untagged, Has.Length.EqualTo(14));
    }

    [Test]
    public void Imf_File_Past_What_Type_1_Holds_Is_Type_0()
    {
        var commands = Enumerable.Range(0, Wolf3dImfAudio.MaxType1Commands + 1).Select(_ => new Command(0x00, 0x00, 1)).ToArray();
        var file = Wolf3dImfAudio.ToFile(commands, "ignored");
        Assert.That(file, Has.Length.EqualTo(commands.Length * 4));
        Assert.That(Wolf3dImfAudio.FromFile(file).Commands, Has.Count.EqualTo(commands.Length));
    }

    [Test]
    public void AdLib_Sound_Round_Trips()
    {
        var sound = AdLibSound.Create(50, Piano, 4, [0x80, 0x90, 0, 0xA0]);
        var read = AdLibSound.FromFile(sound.RawData);
        Assert.That(read.Priority, Is.EqualTo(50));
        Assert.That(read.Instrument, Is.EqualTo(Piano));
        Assert.That(read.Block, Is.EqualTo(4));
        Assert.That(read.Tones, Is.EqualTo(new byte[] { 0x80, 0x90, 0, 0xA0 }));
        Assert.That(read.ToPcm16(), Has.Length.EqualTo(4 * 315 + 44100 / 5));
    }

    [Test]
    public void Pc_Sound_Round_Trips()
    {
        var sound = PcSound.Create(20, [40, 0, 60]);
        var read = PcSound.FromFile(sound.RawData);
        Assert.That(read.Priority, Is.EqualTo(20));
        Assert.That(read.Values, Is.EqualTo(new byte[] { 40, 0, 60 }));
    }

    // A Muse-like song: the start sets channel 1's instrument and keys everything off, then a
    // note, a louder note struck again in one tick, a key-off and a pitch set ahead of the last note
    private static Command[] MuseLikeSong()
    {
        var commands = new List<Command> { new(0x00, 0x00, 0), new(0xBD, 0x00, 0), new(0x08, 0x00, 0) };
        commands.AddRange(Piano.Registers(1).Select(write => new Command(write.Register, write.Value, 0)));
        for (var channel = 0; channel < 9; channel++)
            commands.Add(new Command((byte)(0xB0 + channel), 0, channel == 8 ? (ushort)5 : (ushort)0));
        commands.AddRange([
            new(0xA1, 0x8B, 0), new(0xB1, 0x36, 100),            // tick 5: note on
            new(0xB1, 0x16, 0), new(0x44, 0x10, 0), new(0xB1, 0x36, 50), // tick 105: off and on again, louder
            new(0xB1, 0x16, 20),                                 // tick 155: off
            new(0xA1, 0x65, 0), new(0xB1, 0x13, 30),             // tick 175: pitch set ahead, keyed off
            new(0xB1, 0x33, 40),                                 // tick 205: on
            new(0xB1, 0x13, 0),                                  // tick 245: off, just as it loops
        ]);
        return [.. commands];
    }

    [Test]
    public void Reads_Notes_Instruments_And_Volumes()
    {
        var song = ImfSongReader.Read(MuseLikeSong());

        Assert.That(song.Length, Is.EqualTo(245));
        Assert.That(song.Notes, Is.EqualTo(new[]
        {
            new ImfNote(1, 5, 100, 0x28B, 5),
            new ImfNote(1, 105, 50, 0x28B, 5, 0x10),
            new ImfNote(1, 205, 40, 0x365, 4),
        }));
        Assert.That(song.Instruments, Is.EqualTo(new[] { Piano }));
        Assert.That(song.PatchChanges, Is.EqualTo(new[] { new ImfPatchChange(0, 1, 0) }));
        Assert.That(song.VolumeChanges, Is.Empty);
        // The start's 00, BD, 08 and keys off, and the pitch set ahead of the last note
        Assert.That(song.Writes.Select(write => (write.Tick, write.Register)), Is.EqualTo(new[]
        {
            (0, (byte)0x00), (0, (byte)0xBD), (0, (byte)0x08),
            (0, (byte)0xB0), (0, (byte)0xB1), (0, (byte)0xB2), (0, (byte)0xB3), (0, (byte)0xB4),
            (0, (byte)0xB5), (0, (byte)0xB6), (0, (byte)0xB7), (0, (byte)0xB8),
            (175, (byte)0xA1), (175, (byte)0xB1),
        }));
    }

    [Test]
    public void Written_Again_It_Plays_The_Same()
    {
        var original = MuseLikeSong();
        var written = ImfSongWriter.Write(ImfSongReader.Read(original));
        ImfPlayback.AssertSame(ImfPlayback.Of(original), ImfPlayback.Of(written), "Muse-like song");
    }

    [Test]
    public void A_New_Song_Is_Written_With_Its_Notes()
    {
        var song = new ImfSong { Length = 140 };
        var piano = song.InstrumentIndex(Piano);
        song.PatchChanges.Add(new ImfPatchChange(0, 2, piano));
        var (fNumber, block) = OplPitch.FromMidiNote(60)!.Value;
        song.Notes.Add(new ImfNote(2, 0, 70, fNumber, block, 0x08));

        var commands = ImfSongWriter.Write(song);
        var playback = ImfPlayback.Of(commands);
        Assert.That(commands.Sum(command => command.Delay), Is.EqualTo(140));
        Assert.That(playback.Ticks[0].Strikes, Is.EqualTo(new[] { 2 }));
        Assert.That(playback.Ticks[0].Registers[0x45], Is.EqualTo(0x08), "channel 2's carrier level is the note's volume");
        Assert.That(playback.Ticks[70].Registers[0xB2] & OplRegisters.KeyOn, Is.Zero, "keyed off after 70 ticks");
        // Everything the instrument needs is written at the start, zeros too
        Assert.That(commands.Any(command => command.Register == 0xE2), Is.True);
    }

    [Test]
    public void A_Gap_Longer_Than_A_Delay_Holds_Is_Split()
    {
        var song = new ImfSong { Length = 70000 };
        song.Notes.Add(new ImfNote(1, 0, 69000, 0x200, 4));
        var commands = ImfSongWriter.Write(song);
        Assert.That(commands.Sum(command => (long)command.Delay), Is.EqualTo(70000));
        Assert.That(ImfPlayback.Of(commands).Ticks.ContainsKey(69000), Is.True);
    }
}

/// <summary>
/// What IMF commands do to the chip, tick by tick: every register at the tick's end and which
/// channels were keyed on in it (from off, or off and on again)
/// </summary>
public sealed record ImfPlayback(SortedDictionary<int, (byte[] Registers, int[] Strikes)> Ticks, int Length)
{
    public static ImfPlayback Of(IReadOnlyList<Command> commands)
    {
        var ticks = new SortedDictionary<int, (byte[], int[])>();
        var registers = new byte[256];
        var strikes = new SortedSet<int>();
        var tick = 0;
        var any = false;
        foreach (var (register, value, delay) in commands)
        {
            if (register is >= 0xB0 and <= 0xB8 && (registers[register] & 0x20) == 0 && (value & 0x20) != 0)
                strikes.Add(register - 0xB0);
            registers[register] = value;
            any = true;
            if (delay > 0)
            {
                ticks[tick] = ((byte[])registers.Clone(), strikes.ToArray());
                strikes.Clear();
                any = false;
                tick += delay;
            }
        }
        if (any)
            ticks[tick] = ((byte[])registers.Clone(), strikes.ToArray());
        return new ImfPlayback(ticks, tick);
    }

    /// <summary>The same notes struck with the chip set the same at every tick something happens in either</summary>
    public static void AssertSame(ImfPlayback expected, ImfPlayback actual, string what)
    {
        Assert.That(actual.Length, Is.EqualTo(expected.Length), $"{what}: length");
        byte[] expectedRegisters = new byte[256], actualRegisters = new byte[256];
        foreach (var tick in expected.Ticks.Keys.Union(actual.Ticks.Keys).Order())
        {
            int[] expectedStrikes = [], actualStrikes = [];
            if (expected.Ticks.TryGetValue(tick, out var e))
                (expectedRegisters, expectedStrikes) = e;
            if (actual.Ticks.TryGetValue(tick, out var a))
                (actualRegisters, actualStrikes) = a;
            if (!expectedStrikes.SequenceEqual(actualStrikes))
                Assert.Fail($"{what}: at tick {tick} channels [{string.Join(",", expectedStrikes)}] should be struck, not [{string.Join(",", actualStrikes)}]");
            for (var register = 0; register < 256; register++)
            {
                if (expectedRegisters[register] != actualRegisters[register])
                    Assert.Fail($"{what}: at tick {tick} register {register:X2} should be {expectedRegisters[register]:X2}, not {actualRegisters[register]:X2}");
            }
        }
    }
}
