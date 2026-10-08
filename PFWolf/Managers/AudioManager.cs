using NukedOPL3Sharp;
using OpenTK.Audio.OpenAL;
using SDL2;
using System.Runtime.InteropServices;
using PFWolf.Assets;
using PFWolf.Assets.Sounds;

namespace PFWolf.Managers;

internal class AudioManager
{
    // Number of sound channels to use
    private const int SourceCount = 16;

    // IMF music is boosted as it's synthesized (ImfMusicSource), then brought back down here
    private const float MusicGain = 0.75f;

    // Streaming playback: how many buffers (100ms each) to prime before starting playback,
    // and the max look-ahead depth.
    private const int MusicStreamPrimedBuffers = 2;
    private const int MusicStreamQueueDepth = 6;

    private readonly ALDevice _device;
    private readonly ALContext _context;

    // Keyed by variant ("digi:NAME", "adlib:NAME", "pc:NAME"), since the same sound can play
    // from a different device once one is switched off.
    private readonly Dictionary<string, int> _buffers = [];
    // The running pack's own sound sequences, found on first use (empty when it has none)
    private SoundSequenceAsset? _packSoundSeq;

    // The buffer each sound name last played, for Stop and IsPlaying.
    private readonly Dictionary<string, int> _lastBufferForSound = [];

    private bool _pcSoundEnabled = true;
    private bool _adLibSoundEnabled = true;
    private bool _digitizedSoundEnabled = true;
    private bool _musicEnabled = true;

    /// <summary>Whether sounds may fall back to their PC speaker variant.</summary>
    public bool PcSoundEnabled
    {
        get => _pcSoundEnabled;
        set => SetSoundDevice(ref _pcSoundEnabled, value);
    }

    /// <summary>Whether sounds may fall back to their AdLib variant.</summary>
    public bool AdLibSoundEnabled
    {
        get => _adLibSoundEnabled;
        set => SetSoundDevice(ref _adLibSoundEnabled, value);
    }

    /// <summary>Whether sounds may play their digitized variant.</summary>
    public bool DigitizedSoundEnabled
    {
        get => _digitizedSoundEnabled;
        set => SetSoundDevice(ref _digitizedSoundEnabled, value);
    }

    /// <summary>
    /// Whether music plays. While off, <see cref="PlayMusic"/> still records the track, and
    /// switching music back on starts it.
    /// </summary>
    public bool MusicEnabled
    {
        get => _musicEnabled;
        set
        {
            if (_musicEnabled == value)
                return;
            _musicEnabled = value;
            if (!value)
                StopMusicStream();
            else if (!string.IsNullOrEmpty(_requestedMusicTrack))
                PlayMusic(_requestedMusicTrack);
        }
    }

    /// <summary>The top of the <see cref="SoundVolume"/> and <see cref="MusicVolume"/> scale.</summary>
    public const int MaxVolume = 10;

    private int _soundVolume = MaxVolume;
    private int _musicVolume = MaxVolume;

    /// <summary>Sound effects volume, 0 (silent) to <see cref="MaxVolume"/>.</summary>
    public int SoundVolume
    {
        get => _soundVolume;
        set
        {
            _soundVolume = Math.Clamp(value, 0, MaxVolume);
            if (!IsActive)
                return;
            var gain = VolumeGain(_soundVolume);
            foreach (var source in _sources)
                AL.Source(source, ALSourcef.Gain, gain);
        }
    }

    /// <summary>Music volume, 0 (silent) to <see cref="MaxVolume"/>. Applies to the playing track at once.</summary>
    public int MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Math.Clamp(value, 0, MaxVolume);
            if (IsActive)
                AL.Source(_musicSource, ALSourcef.Gain, _musicSourceGain * VolumeGain(_musicVolume));
        }
    }

    // The playing track's own gain, under the volume setting (see PlayMusic)
    private float _musicSourceGain = MusicGain;

    // Loudness is heard roughly logarithmically, so a squared curve makes each step sound
    // about as big as the last; a straight line would bunch the audible change at the bottom.
    private static float VolumeGain(int volume)
    {
        var fraction = volume / (float)MaxVolume;
        return fraction * fraction;
    }

    // Switching a device off silences whatever it's playing now.
    private void SetSoundDevice(ref bool enabled, bool value)
    {
        if (enabled == value)
            return;
        enabled = value;
        if (!value)
            StopAll();
    }

    // Available sound channels
    private int _nextSource;
    private readonly int[] _sources;

    private readonly int _musicSource;
    private readonly Lazy<AssetManager> _assetManager;
    private string _requestedMusicTrack = "";

    /// <summary>The track last started with <see cref="PlayMusic"/> (playing or paused), or "" if none.</summary>
    public string CurrentMusicTrack => _requestedMusicTrack;

    private Thread? _musicStreamThread;
    private CancellationTokenSource? _musicStreamCts;
    private bool _isPaused;
    private bool _isDisposed;

    // False when OpenAL couldn't be started: the game runs silent and every call here does nothing
    private readonly bool _isAvailable;

    /// <summary>Whether sound is working; false when OpenAL couldn't be started (the game runs silent).</summary>
    public bool IsAvailable => _isAvailable;

    private bool IsActive => _isAvailable && !_isDisposed;

    public AudioManager(Lazy<AssetManager> assetManager)
    {
        _assetManager = assetManager;
        _sources = [];

        var failure = TryOpenDevice(out _device, out _context);
        if (failure != null)
        {
            // Startup output is shown on the signon screen
            Console.WriteLine($"Sound is off: {failure}");
            return;
        }
        _isAvailable = true;

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
    }

    private const string OpenALFileName = "OpenAL32.dll";

    // Opens the default device and makes a context current; on failure, returns why (null when it worked)
    private static string? TryOpenDevice(out ALDevice device, out ALContext context)
    {
        device = ALDevice.Null;
        context = ALContext.Null;
        try
        {
            device = ALC.OpenDevice(null);
        }
        catch (Exception e) when (e is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            return DescribeLibraryFailure(e);
        }

        if (device == ALDevice.Null)
            return "OpenAL couldn't open an audio device (no sound output, or it's in use / disabled).";

        context = ALC.CreateContext(device, (int[])null);
        if (context == ALContext.Null || !ALC.MakeContextCurrent(context))
        {
            var error = ALC.GetError(device);
            if (context != ALContext.Null)
                ALC.DestroyContext(context);
            ALC.CloseDevice(device);
            device = ALDevice.Null;
            context = ALContext.Null;
            return $"OpenAL couldn't create an audio context ({error}).";
        }
        return null;
    }

    // Why OpenAL32.dll didn't load. OpenTK only says it couldn't, so load the bundled copy
    // directly to get the OS's reason, and compare its architecture with this process's.
    private static string DescribeLibraryFailure(Exception e)
    {
        var lines = new List<string> { $"{OpenALFileName} couldn't be loaded ({e.GetType().Name})." };
        var process = RuntimeInformation.ProcessArchitecture;
        lines.Add($"  Process: {process}, OS: {RuntimeInformation.OSArchitecture}");

        var path = Path.Combine(AppContext.BaseDirectory, OpenALFileName);
        if (!File.Exists(path))
        {
            lines.Add($"  {path} is missing (deleted, or quarantined by antivirus?)");
            return string.Join(Environment.NewLine, lines);
        }

        var dllArchitecture = PortableExecutableArchitecture(path);
        lines.Add($"  {path}: {new FileInfo(path).Length:N0} bytes, {dllArchitecture ?? "not a readable DLL"}");
        if (dllArchitecture != null && !string.Equals(dllArchitecture, process.ToString(), StringComparison.OrdinalIgnoreCase))
            lines.Add($"  This {process} process can't load a DLL built for {dllArchitecture}: replace it with the {process} build of OpenAL Soft");

        try
        {
            NativeLibrary.Free(NativeLibrary.Load(path));
            lines.Add("  Loading it directly works, so OpenTK looked somewhere else for it.");
        }
        catch (Exception loadError)
        {
            // Carries the OS's reason, e.g. "not a valid Win32 application" or "Access is denied"
            lines.Add($"  {loadError.Message}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    // The CPU a PE file (DLL/EXE) was built for, read from its COFF header; null when it isn't one
    private static string? PortableExecutableArchitecture(string path)
    {
        try
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.ReadUInt16() != 0x5A4D) // "MZ"
                return null;
            reader.BaseStream.Position = 0x3C;
            reader.BaseStream.Position = reader.ReadInt32();
            if (reader.ReadUInt32() != 0x00004550) // "PE\0\0"
                return null;
            return reader.ReadUInt16() switch
            {
                0x014C => "X86",
                0x8664 => "X64",
                0xAA64 => "Arm64",
                0x01C4 => "Arm",
                var machine => $"machine 0x{machine:X4}"
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Plays a sound at the listener, with no distance attenuation or panning.
    /// </summary>
    public void Play(string name) => Play(name, null);

    /// <summary>
    /// Plays a sound at a world position (in tiles), attenuated and panned relative to the
    /// listener set by <see cref="SetListener"/>.
    /// </summary>
    public void PlayAt(string name, float x, float y) => Play(name, (x, y));

    /// <summary>
    /// Moves the OpenAL listener to the player's world position (in tiles) and facing.
    /// Game space is x east / y south with angle 0 east and 90 north; this maps game x to
    /// AL +X and game y to AL +Z, so facing north looks down AL -Z with east on the right.
    /// </summary>
    public void SetListener(float x, float y, float angleDegrees)
    {
        if (!IsActive)
            return;
        var radians = angleDegrees * MathF.PI / 180.0f;
        AL.Listener(ALListener3f.Position, x, 0.0f, y);
        float[] orientation = [MathF.Cos(radians), 0.0f, -MathF.Sin(radians), 0.0f, 1.0f, 0.0f];
        AL.Listener(ALListenerfv.Orientation, orientation);
    }

    private void Play(string name, (float X, float Y)? position)
    {
        if (!IsActive)
            return;
        var requestedName = name;
        var soundSeq = _assetManager.Value.Find<SoundSequenceAsset>("sound-seq");
        // The running pack's own sounds (gamepacks/<pack>/sound-seq.yaml) win over the shared ones
        var packSeq = _packSoundSeq ??= _assetManager.Value.Exists<SoundSequenceAsset>($"{_assetManager.Value.GamePackId}/sound-seq")
            ? _assetManager.Value.FindInGamePack<SoundSequenceAsset>("sound-seq")
            : new SoundSequenceAsset();
        if (soundSeq == null && packSeq.SoundInfo.Count == 0)
            // not found
            return;

        SoundProfile? soundProfile = null;
        for (var indirection = 0; indirection < 8; indirection++)
        {
            if (!(packSeq.SoundInfo.TryGetValue(name, out soundProfile) || soundSeq?.SoundInfo.TryGetValue(name, out soundProfile) == true)
                || soundProfile == null)
            {
                // A sound file or digitized sound of that name plays without a sound-seq entry
                if (!_assetManager.Value.Exists<SoundFileAsset>(name) && !_assetManager.Value.Exists<Wolf3dDigitizedAudio>(name))
                    return;
                soundProfile = new SoundProfile { Digitized = name };
                break;
            }

            if (soundProfile.Random.Count > 0)
            {
                name = soundProfile.Random[Program.SoundRandom() % soundProfile.Random.Count];
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

        // Best variant first, skipping devices that are switched off. With none left, it's silent.
        var buffer = (_digitizedSoundEnabled ? FindDigitizedBuffer(soundProfile.Digitized) : null)
            ?? (_adLibSoundEnabled ? FindBuffer<AdLibSound>("adlib", soundProfile.AdLib, CreateBuffer) : null)
            ?? (_pcSoundEnabled ? FindBuffer<PcSound>("pc", soundProfile.PC, CreateBuffer) : null);
        if (buffer is not int playBuffer)
            return;

        // Get next available sound channel
        var source = _sources[_nextSource++ % _sources.Length];
        _lastBufferForSound[requestedName.ToLowerInvariant()] = playBuffer;
        StartSource(source, playBuffer, position);
    }

    // A pk3's sound file (sounds/NAME.ogg) wins over the game's own digitized sound of that name
    private int? FindDigitizedBuffer(string? assetName)
        => !string.IsNullOrWhiteSpace(assetName) && _assetManager.Value.Exists<SoundFileAsset>(assetName)
            ? FindBuffer<SoundFileAsset>("file", assetName, CreateBuffer)
            : FindBuffer<Wolf3dDigitizedAudio>("digi", assetName, CreateBuffer);

    // The cached buffer for one variant of a sound, created on first use; null when the sound
    // has no such variant.
    private int? FindBuffer<T>(string device, string? assetName, Func<T, int> createBuffer) where T : Asset
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return null;

        var key = $"{device}:{assetName.ToLowerInvariant()}";
        if (_buffers.TryGetValue(key, out var buffer))
            return buffer;

        var sound = _assetManager.Value.Find<T>(assetName);
        if (sound == null)
            return null;

        buffer = createBuffer(sound);
        _buffers[key] = buffer;
        return buffer;
    }

    // Sources are recycled round-robin, so each play must reset positioning: positional sounds
    // live in world space, everything else sits on the listener (relative, at the origin).
    private static void StartSource(int source, int buffer, (float X, float Y)? position)
    {
        AL.SourceStop(source);
        AL.Source(source, ALSourcei.Buffer, buffer);
        if (position is { } pos)
        {
            AL.Source(source, ALSourceb.SourceRelative, false);
            AL.Source(source, ALSource3f.Position, pos.X, 0.0f, pos.Y);
        }
        else
        {
            AL.Source(source, ALSourceb.SourceRelative, true);
            AL.Source(source, ALSource3f.Position, 0.0f, 0.0f, 0.0f);
        }
        AL.SourcePlay(source);
    }

    public void Stop(string name)
    {
        if (!IsActive || !_lastBufferForSound.TryGetValue(name.ToLowerInvariant(), out var buffer))
            return;
        var source = _sources.FirstOrDefault(s => AL.GetSource(s, ALGetSourcei.Buffer) == buffer);
        if (source != 0)
            AL.SourceStop(source);
    }

    public void StopAll()
    {
        if (!IsActive)
            return;
        foreach (var source in _sources)
            AL.SourceStop(source);
    }

    public void WaitSoundDone()
    {
        if (!IsActive)
            return;
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
        if (!IsActive)
            return false;
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
        if (!IsActive || !_lastBufferForSound.TryGetValue(name.ToLowerInvariant(), out var buffer))
            return false;
        var source = _sources.FirstOrDefault(s => AL.GetSource(s, ALGetSourcei.Buffer) == buffer);
        if (source == 0)
            return false;

        AL.GetSource(source, ALGetSourcei.SourceState, out int stateInt);

        // Cast the returned integer to the ALSourceState enum
        ALSourceState state = (ALSourceState)stateInt;
        return state == ALSourceState.Playing;
    }

    /// <param name="loop">false plays the track once (Blake Stone's Apogee fanfare); see IsMusicPlaying</param>
    public void PlayMusic(string name, bool loop = true)
    {
        // A pk3's music file (music/NAME.ogg) wins over the game's own IMF song of that name
        var assetManager = _assetManager.Value;
        var musicFile = !string.IsNullOrWhiteSpace(name) && assetManager.Exists<MusicFileAsset>(name)
            ? assetManager.Find<MusicFileAsset>(name)
            : null;
        var imfTrack = musicFile == null ? assetManager.Find<Wolf3dImfAudio>(name) : null;
        if (musicFile == null && imfTrack == null)
            return;

        StopMusicStream();

        _requestedMusicTrack = name;
        _isPaused = false; // A deliberate request for new music always plays, even if a prior unrelated pause was never lifted.
        if (!_musicEnabled || !IsActive)
            return; // remembered, and started when music is switched back on

        // OPL output is quiet and boosted (MusicSampleGain), then brought back down here; a
        // recording is already mastered, so it plays as it is
        _musicSourceGain = musicFile != null ? 1.0f : MusicGain;
        AL.Source(_musicSource, ALSourcef.Gain, _musicSourceGain * VolumeGain(_musicVolume));

        var cts = new CancellationTokenSource();
        _musicStreamCts = cts;
        _musicStreamThread = new Thread(() =>
        {
            MusicSource source;
            try
            {
                source = musicFile != null ? new FileMusicSource(musicFile) : new ImfMusicSource(imfTrack!);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Music '{name}' can't be played: {e.Message}");
                return;
            }
            using (source)
                StreamMusic(source, loop, cts.Token);
        }) { IsBackground = true, Name = "MusicStream" };
        _musicStreamThread.Start();
    }

    /// <summary>Whether there's a song of this name, as a music file or an IMF song</summary>
    public bool HasMusic(string name)
        => _assetManager.Value.Exists<MusicFileAsset>(name) || _assetManager.Value.Exists<Wolf3dImfAudio>(name);

    /// <summary>
    /// Whether music is still sounding: false once a track played without looping has finished,
    /// or when music is off or stopped
    /// </summary>
    public bool IsMusicPlaying
    {
        get
        {
            if (!IsActive || !_musicEnabled || string.IsNullOrEmpty(_requestedMusicTrack))
                return false;
            if (_musicStreamThread?.IsAlive == true)
                return true;
            AL.GetSource(_musicSource, ALGetSourcei.SourceState, out var stateInt);
            return (ALSourceState)stateInt is ALSourceState.Playing or ALSourceState.Paused;
        }
    }

    // Renders the track a small chunk at a time and feeds it to the music source as queued
    // OpenAL buffers, so playback can start after the first couple of chunks instead of waiting
    // for the whole (often minutes-long) track to be rendered up front. A track that doesn't
    // loop stops being fed at its end; what's queued plays out.
    private void StreamMusic(MusicSource source, bool loop, CancellationToken token)
    {
        var sampleRate = source.SampleRate;
        // 100ms per buffer
        var framesPerChunk = Math.Max(1, sampleRate / 10);
        var buffersPrimed = 0;
        var ended = false;

        while (!token.IsCancellationRequested && !ended)
        {
            var chunk = new short[framesPerChunk * 2];
            var framesWritten = source.Read(chunk, loop);
            ended = framesWritten < framesPerChunk;

            if (token.IsCancellationRequested)
                return;

            if (framesWritten > 0)
            {
                if (framesWritten < framesPerChunk)
                    Array.Resize(ref chunk, framesWritten * 2);

                var bufferId = AL.GenBuffer();
                AL.BufferData(bufferId, ALFormat.Stereo16, chunk, sampleRate);
                AL.SourceQueueBuffers(_musicSource, 1, [bufferId]);
            }

            // Before the source has actually started playing, a Stopped/Initial source reports
            // every queued buffer as immediately "processed" (nothing is consuming them yet), so
            // reclaiming here would delete our own priming buffers before SourcePlay ever runs.
            if (IsMusicSourceActive())
                ReclaimProcessedMusicBuffers();

            if (buffersPrimed < MusicStreamPrimedBuffers)
            {
                buffersPrimed++;
                // A short track that ended before priming plays what it has
                if ((buffersPrimed == MusicStreamPrimedBuffers || ended) && !_isPaused)
                    AL.SourcePlay(_musicSource);
            }
            else if (!_isPaused && framesWritten > 0 && !IsMusicSourceActive())
            {
                // Starved (decoding fell behind, or a long hitch): OpenAL stops a source that
                // runs out of buffers, so start it again on what's now queued
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

        if (!_isAvailable)
            return;
        AL.SourceStop(_musicSource);
        AL.GetSource(_musicSource, ALGetSourcei.BuffersQueued, out var queued);
        if (queued <= 0)
            return;
        var queuedBuffers = new int[queued];
        AL.SourceUnqueueBuffers(_musicSource, queued, queuedBuffers);
        AL.DeleteBuffers(queuedBuffers);
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
        if (!IsActive)
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
        if (!_isAvailable)
            return;
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
    private static int CreateBuffer(SoundFileAsset sound)
    {
        var buffer = AL.GenBuffer();
        AL.BufferData(buffer, ALFormat.Mono16, sound.Samples, sound.SampleRate);
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
