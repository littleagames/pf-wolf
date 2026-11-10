using NukedOPL3Sharp;
using PFWolf.Assets.Sounds;
using Command = PFWolf.Assets.Sounds.Wolf3dImfAudio.WolfensteinMusicCommand;

namespace PFWolf.Editor.Audio.Opl;

/// <summary>
/// An IMF song played from a tick, to a tick, looping between them when asked: what the song
/// editor plays. The chip is brought to where the song is at the first tick by writing every
/// command before it (notes keyed on then sound from there), as the game would have it.
/// </summary>
public sealed class ImfRangeSource : MusicSource
{
    private const int Rate = ImfMusicSource.MusicSampleRate;
    private const int FramesPerTick = Rate / ImfMusicSource.MusicTicksPerSecond;
    private const float Gain = 3.0f;

    private readonly IReadOnlyList<Command> _commands;
    private readonly int[] _ticks;      // the tick each command is written at
    private readonly int _from, _to;
    private Opl3Chip _chip = new();
    private int _index;
    private int _tick;
    private int _framesLeftInTick;

    /// <param name="from">The tick to start at (and loop back to)</param>
    /// <param name="to">The tick to stop (or loop) at; past the song's end, its end</param>
    public ImfRangeSource(IReadOnlyList<Command> commands, int from, int to)
    {
        _commands = commands;
        _ticks = new int[commands.Count];
        var tick = 0;
        for (var i = 0; i < commands.Count; i++)
        {
            _ticks[i] = tick;
            tick += commands[i].Delay;
        }
        Length = tick;
        _to = Math.Clamp(to, 1, Math.Max(1, tick));
        _from = Math.Clamp(from, 0, _to - 1);
        Restart();
    }

    /// <summary>The song's length in ticks</summary>
    public int Length { get; }

    public override int SampleRate => Rate;

    // A fresh chip with every command before the first tick written
    private void Restart()
    {
        _chip = new Opl3Chip();
        _chip.Reset(Rate);
        _chip.WriteRegister(0x01, 0x20);
        _index = 0;
        while (_index < _commands.Count && _ticks[_index] < _from)
        {
            _chip.WriteRegister(_commands[_index].Register, _commands[_index].Value);
            _index++;
        }
        _tick = _from;
        _framesLeftInTick = 0;
    }

    public override int Read(short[] stereo, bool loop)
    {
        var frames = stereo.Length / 2;
        var written = 0;
        while (written < frames)
        {
            if (_framesLeftInTick == 0)
            {
                if (_tick >= _to)
                {
                    if (!loop)
                        break;
                    Restart();
                }
                // This tick's writes, buffered as ImfMusicSource does
                while (_index < _commands.Count && _ticks[_index] <= _tick)
                {
                    _chip.WriteRegisterBuffered(_commands[_index].Register, _commands[_index].Value);
                    _index++;
                }
                _tick++;
                _framesLeftInTick = FramesPerTick;
            }
            var count = Math.Min(_framesLeftInTick, frames - written);
            _chip.GenerateStream(stereo.AsSpan(written * 2, count * 2));
            written += count;
            _framesLeftInTick -= count;
        }

        for (var i = 0; i < written * 2; i++)
            stereo[i] = (short)Math.Clamp(stereo[i] * Gain, short.MinValue, short.MaxValue);
        return written;
    }

    /// <summary>The song from a tick to a tick as 16-bit mono at 44100 Hz (for a WAV)</summary>
    public static short[] RenderMono(IReadOnlyList<Command> commands, int from, int to)
    {
        var source = new ImfRangeSource(commands, from, to);
        var frames = (long)(source._to - source._from) * FramesPerTick;
        var stereo = new short[frames * 2];
        var read = source.Read(stereo, loop: false);
        var mono = new short[read];
        for (var i = 0; i < read; i++)
            mono[i] = (short)((stereo[i * 2] + stereo[i * 2 + 1]) / 2);
        return mono;
    }
}

/// <summary>A single note of an instrument, to hear it while drawing or setting up an instrument</summary>
public static class OplAudition
{
    /// <summary>The note held for <paramref name="seconds"/>, then let go, as 16-bit mono at 44100 Hz</summary>
    public static short[] Note(OplInstrument instrument, int fNumber, int block, byte? level, double seconds = 0.4, double release = 0.4)
    {
        const int rate = ImfMusicSource.MusicSampleRate, channel = 1;
        var chip = new Opl3Chip();
        chip.Reset(rate);
        chip.WriteRegister(0x01, 0x20);
        foreach (var (register, value) in instrument.Registers(channel))
            chip.WriteRegister(register, value);
        if (level is { } carrier)
            chip.WriteRegister(OplRegisters.CarrierLevel(channel), carrier);
        chip.WriteRegister((ushort)(0xA0 + channel), (byte)(fNumber & 0xFF));
        chip.WriteRegister((ushort)(0xB0 + channel), (byte)(OplRegisters.KeyOn | ((block & 7) << 2) | ((fNumber >> 8) & 3)));

        var held = new short[(int)(rate * seconds) * 2];
        chip.GenerateStream(held);
        chip.WriteRegister((ushort)(0xB0 + channel), (byte)(((block & 7) << 2) | ((fNumber >> 8) & 3)));
        var tail = new short[(int)(rate * release) * 2];
        chip.GenerateStream(tail);

        var mono = new short[(held.Length + tail.Length) / 2];
        for (var i = 0; i < mono.Length; i++)
        {
            var (left, right) = i * 2 < held.Length ? (held[i * 2], held[i * 2 + 1]) : (tail[i * 2 - held.Length], tail[i * 2 + 1 - held.Length]);
            mono[i] = (short)Math.Clamp((left + right) * 3 / 2, short.MinValue, short.MaxValue);
        }
        return mono;
    }
}
