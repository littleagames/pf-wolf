using NukedOPL3Sharp;
using OpenTK.Audio.OpenAL;
using SDL2;
using Wolf3D.Assets.Sounds;

namespace Wolf3D.Managers;

internal class AudioManager
{
    // Number of sound channels to use
    private const int SourceCount = 16;

    private const int MusicSampleRate = 44100;
    private const int MusicTicksPerSecond = 700;
    private const float MusicSampleGain = 3.0f;
    private const float MusicGain = 0.75f;

    // Streaming playback: how much audio (in frames) each queued OpenAL buffer holds,
    // how many buffers to prime before starting playback, and the max look-ahead depth.
    private const int MusicStreamFramesPerChunk = MusicSampleRate / 10; // 100ms per buffer
    private const int MusicStreamPrimedBuffers = 2;
    private const int MusicStreamQueueDepth = 6;

    private readonly ALDevice _device;
    private readonly ALContext _context;

    private readonly Dictionary<string, int> _buffers = [];

    // Available sound channels
    private int _nextSource;
    private readonly int[] _sources;

    private readonly int _musicSource;
    private readonly Lazy<AssetManager> _assetManager;
    private string _requestedMusicTrack = "";
    private Thread? _musicStreamThread;
    private CancellationTokenSource? _musicStreamCts;
    private bool _isPaused;
    private bool _isDisposed;

    public AudioManager(Lazy<AssetManager> assetManager)
    {
        _device = ALC.OpenDevice(null);
        if (_device == ALDevice.Null)
            throw new InvalidOperationException("OpenAL could not open an audio device.");
        _context = ALC.CreateContext(_device, (int[])null);
        if (_context == ALContext.Null || !ALC.MakeContextCurrent(_context))
            throw new InvalidOperationException("OpenAL could not create an audio context.");

        //_buffers = sounds.ToDictionary(pair => pair.Key, pair => CreateBuffer(pair.Value));
        //_musicTracks = musicTracks;

        _sources = AL.GenSources(SourceCount);
        _musicSource = AL.GenSource();
        AL.Source(_musicSource, ALSourceb.SourceRelative, true);
        foreach (var source in _sources)
        {
            AL.Source(source, ALSourceb.SourceRelative, true);
            AL.Source(source, ALSourcef.ReferenceDistance, 1.5f);
            AL.Source(source, ALSourcef.MaxDistance, 16.0f);
            AL.Source(source, ALSourcef.RolloffFactor, 0.35f);
        }

        _assetManager = assetManager;

    }

    public void Play(string name)
    {
        var assetManager = _assetManager.Value;
        var soundSeq = assetManager.Find<SoundSequenceAsset>("sound-seq");
        if (soundSeq == null)
            // not found
            return;

        SoundProfile soundProfile = null;
        for (var indirection = 0; indirection < 8; indirection++)
        {
            if (!soundSeq.SoundInfo.TryGetValue(name, out soundProfile) || soundProfile == null)
                // not found
                return;

            if (soundProfile.Random.Count > 0)
            {
                name = soundProfile.Random[Program.US_RndT() % soundProfile.Random.Count];
                continue;
            }

            if (!string.IsNullOrWhiteSpace(soundProfile.Alias))
            {
                name = soundProfile.Alias;
                continue;
            }

            break;
        }

        if (soundProfile == null)
            return;

        // Get next available sound channel
        var source = _sources[_nextSource++ % _sources.Length];

        if (!string.IsNullOrWhiteSpace(soundProfile.Digitized))
        {
            var digiSound = assetManager.Find<Wolf3dDigitizedAudio>(soundProfile.Digitized);
            if (digiSound != null)
            {
                if (!_buffers.TryGetValue(name.ToLowerInvariant(), out var buffer))
                {
                    buffer = CreateBuffer(digiSound);
                    _buffers[name.ToLowerInvariant()] = buffer;
                }

                AL.SourceStop(source);
                AL.Source(source, ALSourcei.Buffer, buffer);
                AL.SourcePlay(source);
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(soundProfile.AdLib))
        {
            var adLibSound = assetManager.Find<AdLibSound>(soundProfile.AdLib);
            if (adLibSound != null)
            {
                if (!_buffers.TryGetValue(name.ToLowerInvariant(), out var buffer))
                {
                    buffer = CreateBuffer(adLibSound);
                    _buffers[name.ToLowerInvariant()] = buffer;
                }

                AL.SourceStop(source);
                AL.Source(source, ALSourcei.Buffer, buffer);
                AL.SourcePlay(source);
                return;
            }
        }


        if (!string.IsNullOrWhiteSpace(soundProfile.PC))
        {
            var pcSound = assetManager.Find<PcSound>(soundProfile.PC);
            if (pcSound != null)
            {
                if (!_buffers.TryGetValue(name.ToLowerInvariant(), out var buffer))
                {
                    buffer = CreateBuffer(pcSound);
                    _buffers[name.ToLowerInvariant()] = buffer;
                }

                AL.SourceStop(source);
                AL.Source(source, ALSourcei.Buffer, buffer);
                AL.SourcePlay(source);
                return;
            }
        }

        // Sound not found
    }

    public void Stop(string name)
    {
        if (!_buffers.TryGetValue(name.ToLowerInvariant(), out var buffer))
            return;
        var source = _sources.FirstOrDefault(s => AL.GetSource(s, ALGetSourcei.Buffer) == buffer);
        if (source != 0)
            AL.SourceStop(source);
    }

    public void StopAll()
    {
        foreach (var source in _sources)
            AL.SourceStop(source);
    }

    public void WaitSoundDone()
    {
        foreach (var source in _sources)
        {
            AL.GetSource(source, ALGetSourcei.SourceState, out int stateInt);
            var state = (ALSourceState)stateInt;
            if (state == ALSourceState.Playing)
            {
                while (state == ALSourceState.Playing)
                {
                    SDL.SDL_Delay(5);
                    AL.GetSource(source, ALGetSourcei.SourceState, out stateInt);
                    state = (ALSourceState)stateInt;
                }
            }
        }
    }

    public bool IsAnySoundPlaying()
    {
        foreach (var source in _sources)
        {
            AL.GetSource(source, ALGetSourcei.SourceState, out int stateInt);
            var state = (ALSourceState)stateInt;
            if (state == ALSourceState.Playing)
                return true;
        }
        return false;
    }

    public bool IsPlaying(string name)
    {
        if (!_buffers.TryGetValue(name.ToLowerInvariant(), out var buffer))
            return false;
        var source = _sources.FirstOrDefault(s => AL.GetSource(s, ALGetSourcei.Buffer) == buffer);
        if (source == 0)
            return false;

        AL.GetSource(source, ALGetSourcei.SourceState, out int stateInt);

        // Cast the returned integer to the ALSourceState enum
        ALSourceState state = (ALSourceState)stateInt;
        return state == ALSourceState.Playing;
    }

    public void PlayMusic(string name)
    {
        var assetManager = _assetManager.Value;
        var imfTrack = assetManager.Find<Wolf3dImfAudio>(name);
        if (imfTrack == null)
            return;

        StopMusicStream();

        _requestedMusicTrack = name;
        AL.Source(_musicSource, ALSourcef.Gain, MusicGain);

        var cts = new CancellationTokenSource();
        _musicStreamCts = cts;
        _musicStreamThread = new Thread(() => StreamMusic(imfTrack, cts.Token)) { IsBackground = true, Name = "MusicStream" };
        _musicStreamThread.Start();
    }

    // Synthesizes the IMF track a small chunk at a time and feeds it to the music source as
    // queued OpenAL buffers, so playback can start after the first couple of chunks instead of
    // waiting for the whole (often minutes-long) track to be rendered up front.
    private void StreamMusic(Wolf3dImfAudio track, CancellationToken token)
    {
        var commands = track.Commands;
        if (commands.Count == 0 || commands.Sum(command => command.Delay) == 0)
            return;

        var chip = new Opl3Chip();
        chip.Reset(MusicSampleRate);
        chip.WriteRegister(0x01, 0x20);

        const int framesPerTick = MusicSampleRate / MusicTicksPerSecond;
        var commandIndex = 0;
        var framesRemainingInCommand = 0;
        var buffersPrimed = 0;

        while (!token.IsCancellationRequested)
        {
            var chunk = new short[MusicStreamFramesPerChunk * 2];
            var framesWritten = 0;

            while (framesWritten < MusicStreamFramesPerChunk)
            {
                while (framesRemainingInCommand == 0)
                {
                    var command = commands[commandIndex];
                    chip.WriteRegister(command.Register, command.Value);
                    framesRemainingInCommand = command.Delay * framesPerTick;
                    commandIndex++;
                    if (commandIndex >= commands.Count)
                        commandIndex = 0; // loop the track
                }

                var framesToGenerate = Math.Min(framesRemainingInCommand, MusicStreamFramesPerChunk - framesWritten);
                chip.GenerateStream(chunk.AsSpan(framesWritten * 2, framesToGenerate * 2));
                framesWritten += framesToGenerate;
                framesRemainingInCommand -= framesToGenerate;
            }

            if (token.IsCancellationRequested)
                return;

            ApplyMusicGain(chunk);

            var bufferId = AL.GenBuffer();
            AL.BufferData(bufferId, ALFormat.Stereo16, chunk, MusicSampleRate);
            AL.SourceQueueBuffers(_musicSource, 1, [bufferId]);

            // Before the source has actually started playing, a Stopped/Initial source reports
            // every queued buffer as immediately "processed" (nothing is consuming them yet), so
            // reclaiming here would delete our own priming buffers before SourcePlay ever runs.
            if (IsMusicSourceActive())
                ReclaimProcessedMusicBuffers();

            if (buffersPrimed < MusicStreamPrimedBuffers)
            {
                buffersPrimed++;
                if (buffersPrimed == MusicStreamPrimedBuffers && !_isPaused)
                    AL.SourcePlay(_musicSource);
            }

            while (!token.IsCancellationRequested)
            {
                AL.GetSource(_musicSource, ALGetSourcei.BuffersQueued, out var queuedCount);
                if (queuedCount < MusicStreamQueueDepth)
                    break;
                Thread.Sleep(20);
                if (IsMusicSourceActive())
                    ReclaimProcessedMusicBuffers();
            }
        }
    }

    private bool IsMusicSourceActive()
    {
        AL.GetSource(_musicSource, ALGetSourcei.SourceState, out var stateInt);
        var state = (ALSourceState)stateInt;
        return state is ALSourceState.Playing or ALSourceState.Paused;
    }

    private void ReclaimProcessedMusicBuffers()
    {
        AL.GetSource(_musicSource, ALGetSourcei.BuffersProcessed, out var processed);
        if (processed <= 0)
            return;
        var processedBuffers = new int[processed];
        AL.SourceUnqueueBuffers(_musicSource, processed, processedBuffers);
        AL.DeleteBuffers(processedBuffers);
    }

    // Stops and joins the streaming thread, then drains any buffers still queued on the music
    // source, so the next PlayMusic/Shutdown starts from a clean slate.
    private void StopMusicStream()
    {
        _musicStreamCts?.Cancel();
        _musicStreamThread?.Join();
        _musicStreamCts?.Dispose();
        _musicStreamCts = null;
        _musicStreamThread = null;

        AL.SourceStop(_musicSource);
        AL.GetSource(_musicSource, ALGetSourcei.BuffersQueued, out var queued);
        if (queued <= 0)
            return;
        var queuedBuffers = new int[queued];
        AL.SourceUnqueueBuffers(_musicSource, queued, queuedBuffers);
        AL.DeleteBuffers(queuedBuffers);
    }

    private static void ApplyMusicGain(Span<short> samples)
    {
        for (var index = 0; index < samples.Length; index++)
        {
            var amplified = samples[index] * MusicSampleGain;
            samples[index] = (short)Math.Clamp(amplified, short.MinValue, short.MaxValue);
        }
    }

    public void StopMusic()
    {
        StopMusicStream();
        _requestedMusicTrack = "";
    }

    /// <summary>
    /// Pauses or resumes the current music without restarting its sequence.
    /// </summary>
    public void SetPaused(bool isPaused)
    {
        if (_isDisposed)
            return;
        _isPaused = isPaused;
        if (isPaused)
            AL.SourcePause(_musicSource);
        else if (!string.IsNullOrEmpty(_requestedMusicTrack) && _musicStreamThread is { IsAlive: true })
            AL.SourcePlay(_musicSource);
    }

    public void Shutdown()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        StopMusicStream();
        AL.SourceStop(_musicSource);
        foreach (var source in _sources)
            AL.SourceStop(source);
        AL.DeleteSource(_musicSource);
        AL.DeleteSources(_sources);
        AL.DeleteBuffers(_buffers.Values.ToArray());
        ALC.MakeContextCurrent(ALContext.Null);
        ALC.DestroyContext(_context);
        ALC.CloseDevice(_device);
    }

    private static int CreateBuffer(Wolf3dDigitizedAudio sound)
    {
        var buffer = AL.GenBuffer();
        var data = sound.ToPcm16(44100);
        AL.BufferData(buffer, ALFormat.Mono16, data, 44100);
        return buffer;
    }
    private static int CreateBuffer(AdLibSound sound)
    {
        var buffer = AL.GenBuffer();
        var data = sound.ToMono8();
        AL.BufferData(buffer, ALFormat.Mono8, data, 44100);
        return buffer;
    }
    private static int CreateBuffer(PcSound sound)
    {
        var buffer = AL.GenBuffer();
        var data = sound.ToMono8();
        AL.BufferData(buffer, ALFormat.Mono8, data, 44100);
        return buffer;
    }
}
