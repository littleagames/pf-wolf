using NLayer;
using NVorbis;

namespace PFWolf.Assets.Sounds;

/// <summary>
/// Reads an audio file (WAV, OGG Vorbis or MP3) as interleaved float samples from -1 to 1.
/// The format is told by the file's contents, not its name.
/// </summary>
public abstract class AudioFileDecoder : IDisposable
{
    public abstract int SampleRate { get; }
    public abstract int Channels { get; }

    /// <summary>Reads up to <paramref name="count"/> samples (frames times channels); 0 at the end</summary>
    public abstract int Read(float[] buffer, int offset, int count);

    public virtual void Dispose() { }

    public static AudioFileDecoder Open(byte[] data)
    {
        AudioFileDecoder decoder = DetectFormat(data) switch
        {
            AudioFileFormat.Wav => new WavDecoder(data),
            AudioFileFormat.Vorbis => new VorbisDecoder(data),
            AudioFileFormat.Mp3 => new Mp3Decoder(data),
            _ => throw new InvalidDataException("not a WAV, OGG Vorbis or MP3 file"),
        };
        if (decoder.SampleRate <= 0 || decoder.Channels <= 0)
        {
            decoder.Dispose();
            throw new InvalidDataException($"unplayable audio ({decoder.Channels} channels at {decoder.SampleRate} Hz)");
        }
        return decoder;
    }

    public static AudioFileFormat DetectFormat(byte[] data)
    {
        if (data.Length >= 12 && Matches(data, 0, "RIFF") && Matches(data, 8, "WAVE"))
            return AudioFileFormat.Wav;
        if (data.Length >= 4 && Matches(data, 0, "OggS"))
            return AudioFileFormat.Vorbis;
        // An ID3 tag, or straight into an MPEG audio frame (11 sync bits)
        if (data.Length >= 3 && Matches(data, 0, "ID3"))
            return AudioFileFormat.Mp3;
        if (data.Length >= 2 && data[0] == 0xFF && (data[1] & 0xE0) == 0xE0)
            return AudioFileFormat.Mp3;
        return AudioFileFormat.Unknown;
    }

    private static bool Matches(byte[] data, int offset, string tag)
    {
        for (var index = 0; index < tag.Length; index++)
            if (data[offset + index] != tag[index])
                return false;
        return true;
    }

    /// <summary>
    /// The whole file as 16-bit mono at its own sample rate: channels are averaged, since OpenAL
    /// only places mono sounds in the world
    /// </summary>
    public static (short[] Samples, int SampleRate) DecodeMono16(byte[] data)
    {
        using var decoder = Open(data);
        var channels = decoder.Channels;
        var samples = new List<short>();
        var buffer = new float[4096 * channels];
        int read;
        while ((read = decoder.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var frame = 0; frame + channels <= read; frame += channels)
            {
                var sum = 0.0f;
                for (var channel = 0; channel < channels; channel++)
                    sum += buffer[frame + channel];
                samples.Add(ToPcm16(sum / channels));
            }
        }
        if (samples.Count == 0)
            throw new InvalidDataException("the file has no sound in it");
        return (samples.ToArray(), decoder.SampleRate);
    }

    public static short ToPcm16(float sample)
        => (short)Math.Clamp(MathF.Round(sample * 32767.0f), short.MinValue, short.MaxValue);
}

public enum AudioFileFormat
{
    Unknown,
    Wav,
    Vorbis,
    Mp3,
}

public sealed class VorbisDecoder : AudioFileDecoder
{
    private readonly VorbisReader _reader;

    public VorbisDecoder(byte[] data)
    {
        _reader = new VorbisReader(new MemoryStream(data, writable: false), closeOnDispose: true);
    }

    public override int SampleRate => _reader.SampleRate;
    public override int Channels => _reader.Channels;
    public override int Read(float[] buffer, int offset, int count) => _reader.ReadSamples(buffer, offset, count);
    public override void Dispose() => _reader.Dispose();
}

public sealed class Mp3Decoder : AudioFileDecoder
{
    private readonly MpegFile _file;

    public Mp3Decoder(byte[] data)
    {
        _file = new MpegFile(new MemoryStream(data, writable: false));
    }

    public override int SampleRate => _file.SampleRate;
    public override int Channels => _file.Channels;
    public override int Read(float[] buffer, int offset, int count) => _file.ReadSamples(buffer, offset, count);
    public override void Dispose() => _file.Dispose();
}

/// <summary>
/// RIFF WAVE: integer PCM of 8 (unsigned), 16, 24 or 32 bits, or 32/64-bit float, plain or
/// WAVE_FORMAT_EXTENSIBLE. Other chunks (LIST, cue, fact) are skipped.
/// </summary>
public sealed class WavDecoder : AudioFileDecoder
{
    private const ushort FormatPcm = 0x0001;
    private const ushort FormatFloat = 0x0003;
    private const ushort FormatExtensible = 0xFFFE;

    private readonly byte[] _data;
    private readonly int _dataStart;
    private readonly int _dataEnd;
    private readonly int _bytesPerSample;
    private readonly bool _isFloat;
    private int _position;

    public override int SampleRate { get; }
    public override int Channels { get; }

    public WavDecoder(byte[] data)
    {
        _data = data;
        var format = (ushort)0;
        var bitsPerSample = 0;
        var foundFormat = false;
        var dataStart = -1;
        var dataEnd = -1;

        var offset = 12;
        while (offset + 8 <= data.Length)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(data, offset, 4);
            var chunkSize = BitConverter.ToUInt32(data, offset + 4);
            var body = offset + 8;
            // Some writers leave the size at 0 or 0xFFFFFFFF for a stream; take what's there
            var available = data.Length - body;
            var size = chunkSize > available ? available : (int)chunkSize;

            if (chunkId == "fmt " && size >= 16)
            {
                format = BitConverter.ToUInt16(data, body);
                Channels = BitConverter.ToUInt16(data, body + 2);
                SampleRate = (int)BitConverter.ToUInt32(data, body + 4);
                bitsPerSample = BitConverter.ToUInt16(data, body + 14);
                // The extensible header's sub-format GUID starts with the real format tag
                if (format == FormatExtensible && size >= 26)
                    format = BitConverter.ToUInt16(data, body + 24);
                foundFormat = true;
            }
            else if (chunkId == "data")
            {
                dataStart = body;
                dataEnd = body + (chunkSize == 0 ? available : size);
                if (foundFormat)
                    break;
            }

            offset = body + size + (size & 1);   // chunks are padded to an even length
        }

        if (!foundFormat)
            throw new InvalidDataException("WAV file has no fmt chunk");
        if (dataStart < 0)
            throw new InvalidDataException("WAV file has no data chunk");

        _isFloat = format == FormatFloat;
        if (format != FormatPcm && !_isFloat)
            throw new InvalidDataException($"WAV format 0x{format:X4} isn't supported (PCM or float only)");
        if (_isFloat ? bitsPerSample is not (32 or 64) : bitsPerSample is not (8 or 16 or 24 or 32))
            throw new InvalidDataException($"{bitsPerSample}-bit {(_isFloat ? "float" : "PCM")} WAV isn't supported");

        _bytesPerSample = bitsPerSample / 8;
        _dataStart = dataStart;
        // Whole frames only
        var frameSize = _bytesPerSample * Math.Max(1, (int)Channels);
        _dataEnd = dataStart + (dataEnd - dataStart) / frameSize * frameSize;
        _position = _dataStart;
    }

    public override int Read(float[] buffer, int offset, int count)
    {
        var read = 0;
        while (read < count && _position + _bytesPerSample <= _dataEnd)
        {
            buffer[offset + read++] = ReadSample(_position);
            _position += _bytesPerSample;
        }
        return read;
    }

    private float ReadSample(int at)
    {
        if (_isFloat)
            return _bytesPerSample == 4 ? BitConverter.ToSingle(_data, at) : (float)BitConverter.ToDouble(_data, at);

        return _bytesPerSample switch
        {
            1 => (_data[at] - 128) / 128.0f,
            2 => BitConverter.ToInt16(_data, at) / 32768.0f,
            3 => ((_data[at] << 8) | (_data[at + 1] << 16) | (_data[at + 2] << 24)) / 2147483648.0f,
            _ => BitConverter.ToInt32(_data, at) / 2147483648.0f,
        };
    }
}
