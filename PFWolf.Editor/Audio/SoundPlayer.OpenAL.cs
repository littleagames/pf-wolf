using OpenTK.Audio.OpenAL;
using PFWolf.Assets.Sounds;

namespace PFWolf.Editor.Audio;

// Playing through OpenAL, off Windows: the same 100ms chunks as winmm, queued on one source
public sealed partial class SoundPlayer
{
    // Buffers queued before playback starts
    private const int OpenALPrimedBuffers = 2;

    // The device and its context, opened the first time a sound plays (or IsAvailable is asked)
    // and kept until the editor closes; the context is current for the whole process. False when
    // OpenAL couldn't be started.
    private static readonly Lazy<bool> OpenALDevice = new(OpenOpenALDevice);

    // The source playing (0 when none) and the frames each of its queued buffers holds; with _lock held
    private int _alSource;
    private readonly Dictionary<int, int> _alQueued = [];

    private static bool OpenOpenALDevice()
    {
        ALDevice device;
        try
        {
            device = ALC.OpenDevice(null);
        }
        catch (Exception e) when (e is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            WarningLog.Write($"Sound can't play: OpenAL couldn't be loaded ({e.Message}). "
                + "On Linux, install OpenAL Soft (libopenal1 on Debian/Ubuntu, openal-soft on Fedora and Arch).");
            return false;
        }

        if (device == ALDevice.Null)
        {
            WarningLog.Write("Sound can't play: OpenAL couldn't open an audio device (no sound output, or it's in use / disabled).");
            return false;
        }

        var context = ALC.CreateContext(device, (int[]?)null!);
        if (context == ALContext.Null || !ALC.MakeContextCurrent(context))
        {
            WarningLog.Write($"Sound can't play: OpenAL couldn't create an audio context ({ALC.GetError(device)}).");
            if (context != ALContext.Null)
                ALC.DestroyContext(context);
            ALC.CloseDevice(device);
            return false;
        }
        return true;
    }

    // Through OpenAL; returns true when the source ran out and everything queued played
    private bool StreamOpenAL(MusicSource source, bool loop, CancellationToken token)
    {
        if (!OpenALDevice.Value)
            return false;

        var sampleRate = source.SampleRate;
        var framesPerChunk = Math.Max(1, sampleRate / 10);
        var alSource = AL.GenSource();
        AL.Source(alSource, ALSourceb.SourceRelative, true);
        lock (_lock)
        {
            _alSource = alSource;
            _alQueued.Clear();
            _sampleRate = sampleRate;
            _framesOffset = 0;
        }

        var started = false;
        var ended = false;
        var finished = false;
        try
        {
            while (!token.IsCancellationRequested)
            {
                // (Before it starts, a source reports everything queued as played: take back only after)
                if (started && IsPlayingOpenAL(alSource))
                    ReclaimOpenAL(alSource);

                if (ended)
                {
                    // It's started on the last chunk, so stopped now means it played out
                    if (!started || !IsPlayingOpenAL(alSource))
                    {
                        finished = true;
                        break;
                    }
                    Thread.Sleep(10);
                    continue;
                }

                int queued;
                lock (_lock)
                    queued = _alQueued.Count;
                if (queued >= BufferCount)
                {
                    Thread.Sleep(10);
                    continue;
                }

                var chunk = new short[framesPerChunk * 2];
                var frames = source.Read(chunk, loop);
                if (frames < framesPerChunk)
                    ended = true;
                if (frames > 0)
                {
                    if (frames < framesPerChunk)
                        Array.Resize(ref chunk, frames * 2);
                    ApplyVolume(chunk, frames * 2, Volume);
                    var buffer = AL.GenBuffer();
                    AL.BufferData(buffer, ALFormat.Stereo16, chunk, sampleRate);
                    lock (_lock)
                    {
                        AL.SourceQueueBuffers(alSource, 1, [buffer]);
                        _alQueued[buffer] = frames;
                        queued = _alQueued.Count;
                    }
                }

                // Starts once a couple of chunks are queued (or a short sound has ended); later, a
                // source that ran dry (a slow chunk) stopped, so starts it again on what's queued
                if (queued > 0 && (started ? !IsPlayingOpenAL(alSource) : queued >= OpenALPrimedBuffers || ended))
                {
                    AL.SourcePlay(alSource);
                    started = true;
                }
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
                _framesOffset += finished ? _alQueued.Values.Sum() : OpenALPosition();
                _alSource = 0;
                _alQueued.Clear();
            }
            AL.SourceStop(alSource);
            AL.GetSource(alSource, ALGetSourcei.BuffersQueued, out var left);
            if (left > 0)
            {
                var buffers = new int[left];
                AL.SourceUnqueueBuffers(alSource, left, buffers);
                AL.DeleteBuffers(buffers);
            }
            AL.DeleteSource(alSource);
        }
        return finished;
    }

    private static bool IsPlayingOpenAL(int alSource)
    {
        AL.GetSource(alSource, ALGetSourcei.SourceState, out var state);
        return (ALSourceState)state == ALSourceState.Playing;
    }

    // Takes back the buffers that have played, counting their frames as played
    private void ReclaimOpenAL(int alSource)
    {
        AL.GetSource(alSource, ALGetSourcei.BuffersProcessed, out var processed);
        if (processed <= 0)
            return;

        var buffers = new int[processed];
        lock (_lock)
        {
            AL.SourceUnqueueBuffers(alSource, processed, buffers);
            foreach (var buffer in buffers)
                _framesOffset += _alQueued.Remove(buffer, out var frames) ? frames : 0;
        }
        AL.DeleteBuffers(buffers);
    }

    // The frames played of the buffers still queued; call with _lock held
    private long OpenALPosition()
    {
        if (_alSource == 0)
            return 0;
        AL.GetSource(_alSource, ALGetSourcei.SampleOffset, out var offset);
        return offset;
    }
}
