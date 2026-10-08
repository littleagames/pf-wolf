using System.Runtime.InteropServices;
using PFWolf.Assets.Sounds;

namespace PFWolf.Editor.Audio;

/// <summary>
/// Plays one sound or song at a time through the Windows wave-out device (winmm), fed a chunk at
/// a time from a <see cref="MusicSource"/> on a thread of its own. Starting another stops the
/// one playing. Elsewhere than Windows it's silent (<see cref="IsAvailable"/> is false).
/// </summary>
public sealed class SoundPlayer : IDisposable
{
    // 100ms per buffer, a few queued so a slow chunk (an MP3 frame, a busy OPL) doesn't gap
    private const int BufferCount = 4;

    private readonly object _lock = new();
    private Thread? _thread;
    private CancellationTokenSource? _cancel;
    private int _playId;
    private long _framesOffset;
    private int _sampleRate = 1;
    private IntPtr _device;

    public static bool IsAvailable => OperatingSystem.IsWindows();

    /// <summary>Sound volume, 0 to 1</summary>
    public float Volume { get; set; } = 0.8f;

    /// <summary>Whether something is playing</summary>
    public bool IsPlaying => _thread is { IsAlive: true };

    /// <summary>How far into the sound or song playback is (counting every loop)</summary>
    public TimeSpan Position
    {
        get
        {
            lock (_lock)
            {
                var frames = _framesOffset + DevicePosition();
                return TimeSpan.FromSeconds(frames / (double)_sampleRate);
            }
        }
    }

    /// <summary>Playback finished on its own (not stopped), on the player's thread</summary>
    public event EventHandler? Finished;

    /// <summary>Plays <paramref name="source"/>, which the player disposes of when it's done with it</summary>
    public void Play(MusicSource source, bool loop)
    {
        Stop();
        if (!IsAvailable)
        {
            source.Dispose();
            return;
        }

        var cancel = new CancellationTokenSource();
        _cancel = cancel;
        var id = ++_playId;
        _thread = new Thread(() =>
        {
            using (source)
            {
                var finished = Stream(source, loop, cancel.Token);
                if (finished && id == _playId)
                    Finished?.Invoke(this, EventArgs.Empty);
            }
        }) { IsBackground = true, Name = "EditorSound" };
        _thread.Start();
    }

    public void Stop()
    {
        _cancel?.Cancel();
        _thread?.Join();
        _cancel?.Dispose();
        _cancel = null;
        _thread = null;
    }

    public void Dispose() => Stop();

    // Returns true when the source ran out and everything queued played
    private bool Stream(MusicSource source, bool loop, CancellationToken token)
    {
        var sampleRate = source.SampleRate;
        var format = new WaveFormat
        {
            FormatTag = 1, // PCM
            Channels = 2,
            SamplesPerSec = sampleRate,
            AvgBytesPerSec = sampleRate * 4,
            BlockAlign = 4,
            BitsPerSample = 16,
        };
        if (waveOutOpen(out var device, WaveMapper, ref format, IntPtr.Zero, IntPtr.Zero, 0) != 0)
            return false;

        lock (_lock)
        {
            _device = device;
            _sampleRate = sampleRate;
            _framesOffset = 0;
        }

        var framesPerChunk = Math.Max(1, sampleRate / 10);
        var chunk = new short[framesPerChunk * 2];
        var headerSize = Marshal.SizeOf<WaveHeader>();
        var flagsOffset = (int)Marshal.OffsetOf<WaveHeader>(nameof(WaveHeader.Flags));
        var headers = new IntPtr[BufferCount];
        var data = new IntPtr[BufferCount];
        for (var index = 0; index < BufferCount; index++)
        {
            data[index] = Marshal.AllocHGlobal(chunk.Length * 2);
            headers[index] = Marshal.AllocHGlobal(headerSize);
            // Starts "done", so every buffer is free to fill
            Marshal.StructureToPtr(new WaveHeader { Flags = WhdrDone }, headers[index], false);
        }

        var ended = false;
        var finished = false;
        try
        {
            var next = 0;
            while (!token.IsCancellationRequested)
            {
                var header = headers[next];
                if ((Marshal.ReadInt32(header, flagsOffset) & WhdrDone) == 0)
                {
                    // Still playing; once the source has ended, this is waiting for the last ones
                    Thread.Sleep(10);
                    continue;
                }

                if ((Marshal.ReadInt32(header, flagsOffset) & WhdrPrepared) != 0)
                    waveOutUnprepareHeader(device, header, headerSize);

                if (ended)
                {
                    // Every buffer back means what was queued has played out
                    if (headers.All(other => (Marshal.ReadInt32(other, flagsOffset) & WhdrDone) != 0))
                    {
                        finished = true;
                        break;
                    }
                    next = (next + 1) % BufferCount;
                    continue;
                }

                var frames = source.Read(chunk, loop);
                if (frames < framesPerChunk)
                    ended = true;
                if (frames == 0)
                    continue;

                ApplyVolume(chunk, frames * 2, Volume);
                Marshal.Copy(chunk, 0, data[next], frames * 2);
                Marshal.StructureToPtr(new WaveHeader { Data = data[next], BufferLength = frames * 4 }, header, false);
                waveOutPrepareHeader(device, header, headerSize);
                waveOutWrite(device, header, headerSize);
                next = (next + 1) % BufferCount;
            }
        }
        catch (Exception e)
        {
            WarningLog.Write($"Playing the sound stopped: {e.Message}");
        }
        finally
        {
            lock (_lock)
            {
                _framesOffset += DevicePosition();
                _device = IntPtr.Zero;
            }
            waveOutReset(device);
            foreach (var header in headers)
            {
                if ((Marshal.ReadInt32(header, flagsOffset) & WhdrPrepared) != 0)
                    waveOutUnprepareHeader(device, header, headerSize);
                Marshal.FreeHGlobal(header);
            }
            foreach (var buffer in data)
                Marshal.FreeHGlobal(buffer);
            waveOutClose(device);
        }
        return finished;
    }

    // The frames the device has played since it opened; call with _lock held
    private long DevicePosition()
    {
        if (_device == IntPtr.Zero)
            return 0;
        var time = new MmTime { Type = TimeSamples };
        if (waveOutGetPosition(_device, ref time, Marshal.SizeOf<MmTime>()) != 0 || time.Type != TimeSamples)
            return 0;
        // The counter is 32-bit: it wraps after about 27 hours at 44.1kHz, longer than anyone listens
        return time.Value;
    }

    private static void ApplyVolume(short[] samples, int count, float volume)
    {
        if (volume >= 0.999f)
            return;
        // Heard roughly logarithmically, as the game's volume setting is
        var gain = volume * volume;
        for (var index = 0; index < count; index++)
            samples[index] = (short)(samples[index] * gain);
    }

    //
    // winmm
    //

    private const uint WaveMapper = unchecked((uint)-1);
    private const int WhdrDone = 0x01;
    private const int WhdrPrepared = 0x02;
    private const uint TimeSamples = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public short FormatTag;
        public short Channels;
        public int SamplesPerSec;
        public int AvgBytesPerSec;
        public short BlockAlign;
        public short BitsPerSample;
        public short Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public int BufferLength;
        public int BytesRecorded;
        public IntPtr User;
        public int Flags;
        public int Loops;
        public IntPtr Next;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MmTime
    {
        public uint Type;
        public uint Value;
        // The rest of the union (SMPTE time is 8 bytes)
        public uint Padding;
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutOpen(out IntPtr device, uint deviceId, ref WaveFormat format, IntPtr callback, IntPtr instance, int flags);

    [DllImport("winmm.dll")]
    private static extern int waveOutPrepareHeader(IntPtr device, IntPtr header, int size);

    [DllImport("winmm.dll")]
    private static extern int waveOutUnprepareHeader(IntPtr device, IntPtr header, int size);

    [DllImport("winmm.dll")]
    private static extern int waveOutWrite(IntPtr device, IntPtr header, int size);

    [DllImport("winmm.dll")]
    private static extern int waveOutReset(IntPtr device);

    [DllImport("winmm.dll")]
    private static extern int waveOutClose(IntPtr device);

    [DllImport("winmm.dll")]
    private static extern int waveOutGetPosition(IntPtr device, ref MmTime time, int size);
}
