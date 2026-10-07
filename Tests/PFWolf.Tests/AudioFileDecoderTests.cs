using System.Text;
using PFWolf.Assets.Sounds;
using PFWolf.Loaders;
using PFWolf.Managers;

namespace PFWolf.Tests;

public class AudioFileDecoderTests
{
    // A RIFF WAVE file: fmt, then any extra chunks, then data
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
        using var source = new AudioManager.FileMusicSource(music);
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
        using var source = new AudioManager.FileMusicSource(music);
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
}
