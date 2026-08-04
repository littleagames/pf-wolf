using OpenTK.Audio.OpenAL;
using SDL2;
using Wolf3D.Assets.Sounds;
using Wolf3D.Mappers;

namespace Wolf3D.Managers;

internal class AudioManager
{
    // Number of sound channels to use
    private const int SourceCount = 16;

    private const int MusicSampleRate = 44100;
    private const int MusicTicksPerSecond = 700;
    private const float MusicSampleGain = 3.0f;
    private const float MusicGain = 0.75f;

    private readonly ALDevice _device;
    private readonly ALContext _context;

    private readonly Dictionary<string, int> _buffers = new();
    private readonly IReadOnlyList<Wolf3dImfAudio> _musicTracks;
    private readonly Dictionary<int, int> _musicBuffers = [];

    // Available sound channels
    private int _nextSource;
    private readonly int[] _sources;

    private readonly int _musicSource;
    private readonly Lazy<AssetManager> _assetManager;
    private int _requestedMusicTrack = -1;
    private int _musicRequestId;
    private double _musicFade;
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
        AL.Source(_musicSource, ALSourceb.Looping, true);
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
        if (!AudioMappings.SoundMappingKeys.TryGetValue(name.ToLowerInvariant(), out var soundProfile))
            return;

        var assetManager = _assetManager.Value;
        // Get next available sound channel
        var source = _sources[_nextSource++ % _sources.Length];

        var digiSound = assetManager.GetDigitizedSound(soundProfile.Digitized);
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

        // TODO: Support adlib and pc sound playback
        //var adLibSound = assetManager.GetAdLib(soundProfile.AdLib);
        //var pcSound = assetManager.GetPcSound(soundProfile.PC);
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
        var imfTrack = assetManager.GetImf(name);
        if (imfTrack == null)
            return;
    }

    public void StopMusic()
    {
        AL.SourceStop(_musicSource);
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
        else if (_requestedMusicTrack >= 0 && _musicBuffers.ContainsKey(_requestedMusicTrack))
            AL.SourcePlay(_musicSource);
    }

    public void Shutdown()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        AL.SourceStop(_musicSource);
        foreach (var source in _sources)
            AL.SourceStop(source);
        AL.DeleteSource(_musicSource);
        AL.DeleteSources(_sources);
        AL.DeleteBuffers(_buffers.Values.ToArray());
        AL.DeleteBuffers(_musicBuffers.Values.ToArray());
        ALC.MakeContextCurrent(ALContext.Null);
        ALC.DestroyContext(_context);
        ALC.CloseDevice(_device);
    }

    private static int CreateBuffer(Wolf3dDigitizedAudio sound)
    {
        var buffer = AL.GenBuffer();
        var data = sound.ToRawWav(44100);
        AL.BufferData(buffer, ALFormat.Mono16, data, 44100);
        return buffer;
    }
}
