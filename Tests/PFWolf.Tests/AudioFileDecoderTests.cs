using System.Text;
using PFWolf.Assets.Sounds;
using PFWolf.Loaders;
using PFWolf.Managers;

namespace PFWolf.Tests;

public class AudioFileDecoderTests
{
    // A Riff Wave file: fmt, then any extra chunks, then data
    private static byte[] Wav(ushort format, ushort channels, int sampleRate, ushort bits, byte[] samples,
        (string Id, byte[] Body)[]? extraChunks = null, bool extensible = false)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(0);    // the length, filled in below
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(extensible ? 40 : 16);
        writer.Write(extensible ? (ushort)0xFFFE : format);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bits / 8);
        writer.Write((ushort)(channels * bits / 8));
        writer.Write(bits);
        if (extensible)
        {
            writer.Write((ushort)22);   // extension size
            writer.Write(bits);         // valid bits
            writer.Write(0);            // channel mask
            writer.Write(format);       // sub-format GUID, which starts with the format tag
            writer.Write(new byte[14]);
        }

        foreach (var (id, body) in extraChunks ?? [])
        {
            writer.Write(Encoding.ASCII.GetBytes(id));
            writer.Write(body.Length);
            writer.Write(body);
            if (body.Length % 2 == 1)
                writer.Write((byte)0);
        }

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(samples.Length);
        writer.Write(samples);

        writer.Flush();
        var bytes = stream.ToArray();
        BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
        return bytes;
    }

    private static byte[] Int16s(params short[] values)
    {
        var bytes = new byte[values.Length * 2];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    [Test]
    public void Detects_Formats_By_Their_Contents()
    {
        Assert.That(AudioFileDecoder.DetectFormat(Wav(1, 1, 8000, 16, Int16s(0))), Is.EqualTo(AudioFileFormat.Wav));
        Assert.That(AudioFileDecoder.DetectFormat("OggS\0\0"u8.ToArray()), Is.EqualTo(AudioFileFormat.Vorbis));
        Assert.That(AudioFileDecoder.DetectFormat("ID3\u0004"u8.ToArray()), Is.EqualTo(AudioFileFormat.Mp3));
        Assert.That(AudioFileDecoder.DetectFormat([0xFF, 0xFB, 0x90, 0x00]), Is.EqualTo(AudioFileFormat.Mp3));
        Assert.That(AudioFileDecoder.DetectFormat("hello"u8.ToArray()), Is.EqualTo(AudioFileFormat.Unknown));
    }

    [Test]
    public void Unknown_Data_Is_Refused()
    {
        Assert.Throws<InvalidDataException>(() => AudioFileDecoder.Open("not audio at all"u8.ToArray()));
    }

    [Test]
    public void Stereo_16_Bit_Wav_Is_Averaged_To_Mono()
    {
        // Act
        var (samples, sampleRate) = AudioFileDecoder.DecodeMono16(Wav(1, 2, 22050, 16, Int16s(1000, 3000, -2000, -4000)));

        // Assert
        Assert.That(sampleRate, Is.EqualTo(22050));
        Assert.That(samples, Is.EqualTo(new short[] { 2000, -3000 }).Within(1));
    }

    [Test]
    public void Eight_Bit_Wav_Is_Unsigned()
    {
        // Act
        var (samples, _) = AudioFileDecoder.DecodeMono16(Wav(1, 1, 11025, 8, [128, 255, 0]));

        // Assert: 128 is silence, 0 the bottom
        Assert.That(samples[0], Is.EqualTo(0));
        Assert.That(samples[1], Is.GreaterThan(32000));
        Assert.That(samples[2], Is.EqualTo(short.MinValue + 1).Within(1));
    }

    [Test]
    public void Twenty_Four_Bit_And_Float_Wavs_Decode()
    {
        // 24-bit: 0x400000 is half of full scale
        var (pcm24, _) = AudioFileDecoder.DecodeMono16(Wav(1, 1, 44100, 24, [0x00, 0x00, 0x40]));
        Assert.That(pcm24[0], Is.EqualTo(16384).Within(1));

        var (float32, _) = AudioFileDecoder.DecodeMono16(Wav(3, 1, 44100, 32, BitConverter.GetBytes(-0.5f)));
        Assert.That(float32[0], Is.EqualTo(-16384).Within(1));
    }

    [Test]
    public void Extensible_Wav_With_Other_Chunks_Decodes()
    {
        // An odd-sized LIST chunk before data, padded to an even length
        var wav = Wav(1, 1, 48000, 16, Int16s(1234, -1234),
            extraChunks: [("LIST", [1, 2, 3])], extensible: true);

        // Act
        var (samples, sampleRate) = AudioFileDecoder.DecodeMono16(wav);

        // Assert
        Assert.That(sampleRate, Is.EqualTo(48000));
        Assert.That(samples, Is.EqualTo(new short[] { 1234, -1234 }).Within(1));
    }

    [Test]
    public void Compressed_Wav_Is_Refused()
    {
        // ADPCM
        Assert.Throws<InvalidDataException>(() => AudioFileDecoder.Open(Wav(2, 1, 8000, 4, [0, 0])));
    }

    [Test]
    public void Music_File_Loops_From_The_Start()
    {
        // Arrange: three mono frames, played on both sides
        var music = new MusicFileAsset(Wav(1, 1, 8000, 16, Int16s(100, 200, 300)));
        using var source = new FileMusicSource(music);
        var stereo = new short[8 * 2];

        // Act
        var frames = source.Read(stereo, loop: true);

        // Assert
        Assert.That(frames, Is.EqualTo(8));
        Assert.That(source.SampleRate, Is.EqualTo(8000));
        var left = Enumerable.Range(0, 8).Select(frame => stereo[frame * 2]).ToArray();
        var right = Enumerable.Range(0, 8).Select(frame => stereo[frame * 2 + 1]).ToArray();
        Assert.That(left, Is.EqualTo(new short[] { 100, 200, 300, 100, 200, 300, 100, 200 }).Within(1));
        Assert.That(right, Is.EqualTo(left));
    }

    [Test]
    public void Music_File_Played_Once_Ends()
    {
        // Arrange
        var music = new MusicFileAsset(Wav(1, 2, 8000, 16, Int16s(1, 2, 3, 4)));
        using var source = new FileMusicSource(music);
        var stereo = new short[8 * 2];

        // Act / Assert: two frames, then nothing
        Assert.That(source.Read(stereo, loop: false), Is.EqualTo(2));
        Assert.That(source.Read(stereo, loop: false), Is.EqualTo(0));
    }

    [Test]
    public void Pk3_Sounds_And_Music_Files_Load_By_Name()
    {
        // Arrange
        var source = new MemoryAssetSource("pfwolf.pk3",
            new() { ["gamepacks/gamepack-info.yaml"] = "alpha:\n  title: Alpha\n  game-palette: pal\n" },
            new()
            {
                ["sounds/DSDROPN.wav"] = Wav(1, 1, 22050, 16, Int16s(100, 200, 300)),
                ["music/CORNER.wav"] = Wav(1, 2, 44100, 16, Int16s(1, 2, 3, 4)),
            });

        // Act
        var loader = new PfWolfPk3Loader([source], "alpha", "alpha");

        // Assert
        var sound = loader.Load<SoundFileAsset>("dsdropn");
        Assert.That(sound.SampleRate, Is.EqualTo(22050));
        Assert.That(sound.Samples, Has.Length.EqualTo(3));

        using var decoder = loader.Load<MusicFileAsset>("corner").OpenDecoder();
        Assert.That(decoder.Channels, Is.EqualTo(2));
        Assert.That(decoder.SampleRate, Is.EqualTo(44100));
    }

    // An AUDIOT sound chunk: the length, priority 0, any header bytes, then one value a tick
    private static byte[] AudiotSound(byte[] header, params byte[] ticks)
        => BitConverter.GetBytes((uint)ticks.Length).Concat(new byte[2]).Concat(header).Concat(ticks).ToArray();

    [Test]
    public void Pk3_Pc_Speaker_And_AdLib_Sounds_Load_By_Name()
    {
        // Arrange: PCDROPN replaces the game's; MYBEEP is new
        var instrument = new byte[17];      // 16 instrument bytes, then the block
        var source = new MemoryAssetSource("pfwolf.pk3",
            new() { ["gamepacks/gamepack-info.yaml"] = "alpha:\n  title: Alpha\n  game-palette: pal\n" },
            new()
            {
                ["sounds/PCDROPN.pcs"] = AudiotSound([], 40, 0, 40),
                ["sounds/MYBEEP.adl"] = AudiotSound(instrument, 0x44, 0x44),
            });

        // Act
        var loader = new PfWolfPk3Loader([source], "alpha", "alpha");

        // Assert: 315 samples a tick at 44100 Hz
        Assert.That(loader.Load<PcSound>("pcdropn").ToMono8(), Has.Length.EqualTo(3 * 315));
        Assert.That(loader.Load<AdLibSound>("mybeep").ToMono8(), Has.Length.EqualTo(2 * 315 + 44100 / 5));
        Assert.That(loader.GetAssets().Keys, Has.None.StartsWith("soundfileasset:"));
    }

    [Test]
    public void Pc_Speaker_And_AdLib_Files_Too_Short_For_Their_Length_Are_Refused()
    {
        var pc = AudiotSound([], 1, 2, 3);
        var adLib = AudiotSound(new byte[17], 1, 2, 3);

        Assert.Throws<InvalidDataException>(() => PcSound.FromFile(pc[..^1]));
        Assert.Throws<InvalidDataException>(() => AdLibSound.FromFile(adLib[..^1]));
        Assert.Throws<InvalidDataException>(() => AdLibSound.FromFile(pc));
        Assert.That(PcSound.FromFile(pc).RawData, Is.EqualTo(pc));
    }

    // IMF commands: register, value, then a 16-bit delay in 700 Hz ticks
    private static byte[] ImfCommands(params (byte Register, byte Value, ushort Delay)[] commands)
        => commands.SelectMany(command => new[] { command.Register, command.Value }
            .Concat(BitConverter.GetBytes(command.Delay))).ToArray();

    [Test]
    public void Type_1_Imf_File_Reads_Its_Length_And_Skips_The_Tag()
    {
        // Arrange: the byte length, two commands, then a tag
        var commands = ImfCommands((0xB0, 0x20, 7), (0xA0, 0x44, 700));
        var file = BitConverter.GetBytes((ushort)commands.Length).Concat(commands).Concat("tag"u8.ToArray()).ToArray();

        // Act
        var song = Wolf3dImfAudio.FromFile(file);

        // Assert
        Assert.That(song.Commands, Has.Count.EqualTo(2));
        Assert.That(song.Commands[1], Is.EqualTo(new Wolf3dImfAudio.WolfensteinMusicCommand(0xA0, 0x44, 700)));
        Assert.That(ImfMusicSource.Length(song), Is.EqualTo(TimeSpan.FromSeconds(1.01)));
    }

    [Test]
    public void Type_0_Imf_File_Is_All_Commands()
    {
        // Arrange: starts with register 0 set to 0, so no length
        var file = ImfCommands((0, 0, 0), (0xB0, 0x20, 350), (0xA0, 0x44, 350));

        // Act
        var song = Wolf3dImfAudio.FromFile(file);

        // Assert
        Assert.That(song.Commands, Has.Count.EqualTo(3));
        Assert.That(ImfMusicSource.Length(song), Is.EqualTo(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public void Imf_File_Without_Timing_Is_Refused()
    {
        Assert.Throws<InvalidDataException>(() => Wolf3dImfAudio.FromFile(ImfCommands((0, 0, 0), (0xB0, 0x20, 0))));
        Assert.Throws<InvalidDataException>(() => Wolf3dImfAudio.FromFile([1]));
    }

    [Test]
    public void Imf_Key_Off_Then_On_In_One_Tick_Strikes_The_Note_Again()
    {
        // Arrange: a note that decays to silence while held, then is keyed off and on with no
        // delay between, as the songs' drum lines do
        var song = Wolf3dImfAudio.FromFile(ImfCommands(
            (0x20, 0x01, 0), (0x23, 0x01, 0), (0x40, 0x00, 0), (0x43, 0x00, 0),
            (0x60, 0xF8, 0), (0x63, 0xF8, 0), (0x80, 0xF8, 0), (0x83, 0xF8, 0),
            (0xC0, 0x30, 0), (0xA0, 0x80, 0), (0xB0, 0x31, 350),
            (0xB0, 0x11, 0), (0xB0, 0x31, 70)));
        using var source = new ImfMusicSource(song);
        var stereo = new short[ImfMusicSource.MusicSampleRate / 2 * 2];
        source.Read(stereo, loop: false);
        var hit = new short[ImfMusicSource.MusicSampleRate / 10 * 2];

        // Act
        source.Read(hit, loop: false);

        // Assert
        Assert.That(hit.Max(sample => Math.Abs((int)sample)), Is.GreaterThan(1000));
    }

    [Test]
    public void Pk3_Imf_Music_Loads_By_Name_And_Replaces_A_Music_File_Beneath()
    {
        // Arrange: the base pk3 has CORNER as a WAV and a new IMF song; a mod gives CORNER as an IMF
        var song = ImfCommands((0, 0, 0), (0xB0, 0x20, 700));
        var basePk3 = new MemoryAssetSource("pfwolf.pk3",
            new() { ["gamepacks/gamepack-info.yaml"] = "alpha:\n  title: Alpha\n  game-palette: pal\n" },
            new()
            {
                ["music/CORNER.ogg"] = Wav(1, 2, 44100, 16, Int16s(1, 2, 3, 4)),
                ["music/NEWSONG.imf"] = song,
            });
        var mod = new MemoryAssetSource("mod.pk3", new(), new() { ["music/CORNER.wlf"] = song });

        // Act
        var loader = new PfWolfPk3Loader([basePk3], "alpha", "alpha", [mod]);

        // Assert
        Assert.That(loader.Load<Wolf3dImfAudio>("newsong").Commands, Has.Count.EqualTo(2));
        Assert.That(loader.Load<Wolf3dImfAudio>("corner").Commands, Has.Count.EqualTo(2));
        Assert.That(loader.GetAssets().Keys, Has.None.EqualTo("musicfileasset:corner"));
    }
}
