using NukedOPL3Sharp;

namespace PFWolf.Assets.Sounds;

/// <summary>
/// Music (or a sound) as 16-bit stereo, read a chunk at a time: the engine streams it to OpenAL,
/// the editor to its own player
/// </summary>
public abstract class MusicSource : IDisposable
{
    public abstract int SampleRate { get; }

    /// <summary>
    /// Fills <paramref name="stereo"/> (interleaved left/right) and returns the frames written,
    /// fewer than it holds only at the end of a track that doesn't loop
    /// </summary>
    public abstract int Read(short[] stereo, bool loop);

    public virtual void Dispose() { }
}

/// <summary>An IMF song, synthesized on an emulated OPL3</summary>
public sealed class ImfMusicSource : MusicSource
{
    public const int MusicSampleRate = 44100;
    public const int MusicTicksPerSecond = 700;
    // OPL output is quiet, so it's boosted here (and the engine brings it back down with its source gain)
    private const float MusicSampleGain = 3.0f;

    private readonly IReadOnlyList<Wolf3dImfAudio.WolfensteinMusicCommand> _commands;
    private readonly Opl3Chip _chip = new();
    private int _commandIndex;
    private int _framesRemainingInCommand;

    public ImfMusicSource(Wolf3dImfAudio track)
    {
        _commands = track.Commands;
        if (_commands.Count == 0 || _commands.Sum(command => command.Delay) == 0)
            throw new InvalidDataException("the song is empty");
        _chip.Reset(MusicSampleRate);
        _chip.WriteRegister(0x01, 0x20);
    }

    public override int SampleRate => MusicSampleRate;

    /// <summary>How long the song plays before it loops</summary>
    public static TimeSpan Length(Wolf3dImfAudio track)
        => TimeSpan.FromSeconds(track.Commands.Sum(command => (double)command.Delay) / MusicTicksPerSecond);

    public override int Read(short[] stereo, bool loop)
    {
        const int framesPerTick = MusicSampleRate / MusicTicksPerSecond;
        var frames = stereo.Length / 2;
        var framesWritten = 0;
        while (framesWritten < frames)
        {
            while (_framesRemainingInCommand == 0)
            {
                if (_commandIndex >= _commands.Count)
                {
                    if (!loop)
                        return Finish(stereo, framesWritten);
                    _commandIndex = 0;   // loop the track
                }
                var command = _commands[_commandIndex];
                _chip.WriteRegister(command.Register, command.Value);
                _framesRemainingInCommand = command.Delay * framesPerTick;
                _commandIndex++;
            }

            var framesToGenerate = Math.Min(_framesRemainingInCommand, frames - framesWritten);
            _chip.GenerateStream(stereo.AsSpan(framesWritten * 2, framesToGenerate * 2));
            framesWritten += framesToGenerate;
            _framesRemainingInCommand -= framesToGenerate;
        }
        return Finish(stereo, framesWritten);
    }

    private static int Finish(short[] stereo, int framesWritten)
    {
        var samples = stereo.AsSpan(0, framesWritten * 2);
        for (var index = 0; index < samples.Length; index++)
        {
            var amplified = samples[index] * MusicSampleGain;
            samples[index] = (short)Math.Clamp(amplified, short.MinValue, short.MaxValue);
        }
        return framesWritten;
    }
}

/// <summary>
/// An OGG, MP3 or WAV song, decoded as it plays. Mono is played on both sides; past two
/// channels, only the front left and right are kept.
/// </summary>
public sealed class FileMusicSource : MusicSource
{
    private readonly MusicFileAsset _file;
    private AudioFileDecoder _decoder;
    private float[] _samples = [];

    public FileMusicSource(MusicFileAsset file)
    {
        _file = file;
        _decoder = file.OpenDecoder();
    }

    public override int SampleRate => _decoder.SampleRate;

    public override int Read(short[] stereo, bool loop)
    {
        var channels = _decoder.Channels;
        var frames = stereo.Length / 2;
        if (_samples.Length < frames * channels)
            _samples = new float[frames * channels];

        var framesWritten = 0;
        var restarted = false;
        while (framesWritten < frames)
        {
            var read = _decoder.Read(_samples, 0, (frames - framesWritten) * channels) / channels;
            if (read == 0)
            {
                // Reopened rather than seeked, which every format supports alike. A file
                // that gives nothing right after being reopened has nothing to loop.
                if (!loop || restarted)
                    break;
                _decoder.Dispose();
                _decoder = _file.OpenDecoder();
                restarted = true;
                continue;
            }
            restarted = false;

            for (var frame = 0; frame < read; frame++)
            {
                var left = _samples[frame * channels];
                var right = channels > 1 ? _samples[frame * channels + 1] : left;
                stereo[(framesWritten + frame) * 2] = AudioFileDecoder.ToPcm16(left);
                stereo[(framesWritten + frame) * 2 + 1] = AudioFileDecoder.ToPcm16(right);
            }
            framesWritten += read;
        }
        return framesWritten;
    }

    public override void Dispose() => _decoder.Dispose();
}

/// <summary>A sound already decoded to 16-bit mono, played on both sides, from <paramref name="start"/> samples in</summary>
public sealed class SampleMusicSource(short[] samples, int sampleRate, int start = 0) : MusicSource
{
    private int _position = Math.Clamp(start, 0, samples.Length);

    public override int SampleRate => sampleRate;

    public override int Read(short[] stereo, bool loop)
    {
        var frames = stereo.Length / 2;
        var framesWritten = 0;
        while (framesWritten < frames)
        {
            if (_position >= samples.Length)
            {
                if (!loop || samples.Length == 0)
                    break;
                _position = 0;
            }
            var sample = samples[_position++];
            stereo[framesWritten * 2] = sample;
            stereo[framesWritten * 2 + 1] = sample;
            framesWritten++;
        }
        return framesWritten;
    }
}
