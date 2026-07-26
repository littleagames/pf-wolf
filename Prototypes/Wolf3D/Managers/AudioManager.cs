using SDL2;
using System.Runtime.InteropServices;
using Wolf3D.Assets.Sounds;
using Wolf3D.AudioPlayers;
using Wolf3D.Constants;
using Wolf3D.Mappers;
using Wolf3D.OPL;
using Wolf3D.OPL.Woody;
using static SDL2.SDL;
using static SDL2.SDL_mixer;

namespace Wolf3D.Managers;

internal enum SDMode
{
    Off,
    PC,
    AdLib
}

internal enum SMMode
{
    Off,
    AdLib
}

internal enum SDSMode
{
    Off,
    SoundBlaster
}

internal struct SoundCommon
{
    public uint length;
    public ushort priority;

    public SoundCommon()
    {
    }

    public SoundCommon(byte[] data)
    {
        length = BitConverter.ToUInt32(data, 0);
        priority = BitConverter.ToUInt16(data, 4);
    }
}

internal struct Instrument
{
    public sbyte mChar, cChar, mScale, cScale, mAttack, cAttack, mSus, cSus, mWave, cWave, nConn,

    //These are only for Muse - these bytes are really unused
    voice, mode;
    public sbyte[] unused;
    public Instrument()
    {
        unused = new sbyte[3];
    }
}
internal struct MusicGroup
{
    public ushort length;
    public ushort[] values;
    public MusicGroup()
    {
        values = new ushort[1];
    }
}

internal struct globalsoundpos
{
    public int valid;
    public int globalsoundx, globalsoundy;
}

internal class AudioManager
{
    public const int DefaultAudioBufferSize = 2048;
    public const int DefaultSampleRate = 44100;

    private int _sampleRate;
    private ImfPlayer _imfPlayer;// = new ImfPlayer(new WoodyEmulatorOpl(OPL.OplType.Opl2));
    private IdAdlPlayer _adlPlayer;// = new IdAdlPlayer(new WoodyEmulatorOpl(OPL.OplType.Opl2));
    private float _imfRefreshRateHz;// = _player.RefreshRate;    // SDL_t0FastAsmService played at 700Hz

    internal const int ORIG_SOUNDCOMMON_SIZE = 6;
    internal const int ORIG_INSTRUMENT_SIZE = 16;
    internal const int ORIG_ADLIBSOUND_SIZE = (ORIG_SOUNDCOMMON_SIZE + ORIG_INSTRUMENT_SIZE + 2);
    internal const int pcTimer = 0x42;
    internal const int pcTAccess = 0x43;
    internal const int pcSpeaker = 0x61;

    internal const int pcSpkBits = 3;

    //      Register addresses
    // Operator stuff
    internal const int alChar = 0x20;
    internal const int alScale = 0x40;
    internal const int alAttack = 0x60;
    internal const int alSus = 0x80;
    internal const int alWave = 0xe0;
    // Channel stuff
    internal const int alFreqL = 0xa0;
    internal const int alFreqH = 0xb0;
    internal const int alFeedCon = 0xc0;
    // Global stuff
    internal const int alEffects = 0xbd;

    //
    //      Sequencing stuff
    //
    internal const int sqMaxTracks = 10;

    // id_sd.c
    internal const int ORIGSAMPLERATE = 7042;

    internal struct headchunk
    {
        public byte[] RIFF;
        public uint filelenminus8;
        public byte[] WAVE;
        public byte[] fmt_;
        public uint formatlen;
        public ushort val0x0001;
        public ushort channels;
        public uint samplerate;
        public uint bytespersec;
        public ushort bytespersample;
        public ushort bitspersample;
        public headchunk()
        {
            RIFF = new byte[4];
            WAVE = new byte[4];
            fmt_ = new byte[4];
        }

        public static int size_of =>
            4 * sizeof(byte)
            + sizeof(uint)
            + 4 * sizeof(byte)
            + 4 * sizeof(byte)
            + sizeof(uint)
            + sizeof(ushort) * 2
            + sizeof(uint) * 2
            + sizeof(ushort) * 2;
        public byte[] AsBytes()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            {
                bw.Write(RIFF);
                bw.Write(filelenminus8);
                bw.Write(WAVE);
                bw.Write(fmt_);
                bw.Write(formatlen);
                bw.Write(val0x0001);
                bw.Write(channels);
                bw.Write(samplerate);
                bw.Write(bytespersec);
                bw.Write(bytespersample);
                bw.Write(bitspersample);
                return ms.ToArray();
            }
        }
    }

    internal class wavechunk
    {
        public byte[] chunkid;
        public uint chunklength;
        public wavechunk()
        {
            chunkid = new byte[4];
        }

        public static int size_of =>
            4 * sizeof(byte)
            + sizeof(uint);

        public byte[] AsBytes()
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            {
                bw.Write(chunkid);
                bw.Write(chunklength);
                return ms.ToArray();
            }
        }
    }

    internal Dictionary<string, IntPtr> SoundChunks = new Dictionary<string, IntPtr>(); //new IntPtr[STARTMUSIC - STARTDIGISOUNDS];

    internal globalsoundpos[] channelSoundPos = new globalsoundpos[SDL_mixer.MIX_CHANNELS];

    public bool PCSoundEnabled => SoundMode == SDMode.PC;
    public bool AdLibSoundEnabled => SoundMode == SDMode.AdLib;
    public bool DigiSoundEnabled => DigiMode == SDSMode.SoundBlaster;

    // Global variables
    internal bool AdLibPresent,
        SoundBlasterPresent, SBProPresent,
        SoundPositioned;
    public SDMode SoundMode { get; private set; }
    public SMMode MusicMode { get; private set; }
    public SDSMode DigiMode { get; private set; }
    //internal int SoundTable;// byte[][] SoundTable;

    int[] DigiMap = new int[AudioMappings.AudioTKeys.Count];
    List<int> DigiChannel = new List<int>(); // new int[STARTMUSIC - STARTDIGISOUNDS];

    // Internal variables
    private bool SD_Started;
    private bool nextsoundpos;
    private int SoundNumber;
    private int DigiNumber;
    private ushort SoundPriority;
    private ushort DigiPriority;
    private int LeftPosition;
    private int RightPosition;

    private ushort NumDigi;
    //private digiinfo[] DigiList;
    private bool DigiPlaying;


    // PC Sound variables
    internal volatile byte pcLastSample;
    internal volatile byte[] pcSound;
    internal volatile int pcSoundPtr;
    internal uint pcLengthLeft;

    // AdLib variables
    //internal static volatile byte[] alSound;
    //internal static sbyte alBlock;
    //internal static uint alLengthLeft;
    internal uint alTimeCount;
    //internal static Instrument alZeroInst;

    // Sequencer variables
    internal volatile bool sqActive;
    internal ushort[] sqHack;
    internal int sqHackPtr;
    internal int sqHackLen;
    internal int sqHackSeqLen;
    internal uint sqHackTime;

    private const int oplChip = 0;


    private int numreadysamples = 0;
    //private static byte[] curAlSound = [];
    //private static int curAlSoundPtr = 0;
    //private static uint curAlLengthLeft = 0;
    private int soundTimeCounter = 5;
    private int samplesPerMusicTick;

    //private Sound[] audiosegs = new Sound[NUMSNDCHUNKS];

    public AudioManager(AssetManager assetManager)
    {
        this.assetManager = assetManager;
    }

    internal void Init(int audioBufferSize, int sampleRate)
    {
        _sampleRate = sampleRate;

        int i;
        int chunksize;

        if (SD_Started)
            return;

        //
        // use a custom size audiobuffer or the largest power
        // of 2 <= the value calculated based on the samplerate
        //
        if (audioBufferSize != DefaultAudioBufferSize)
            chunksize = audioBufferSize;
        else
        {
            if (sampleRate == 0 || sampleRate > 44100)
                throw new PfWolfAudioException("Divide by zero caused by invalid samplerate!");

            chunksize = 1 << (int)Math.Log2(audioBufferSize / (44100 / sampleRate));
        }

        if (SDL_mixer.Mix_OpenAudioDevice(sampleRate, SDL.AUDIO_S16, 2, chunksize, IntPtr.Zero, SDL.SDL_AUDIO_ALLOW_FREQUENCY_CHANGE) != 0)
        {
            throw new PfWolfAudioException("Unable to open audio device: {error}", SDL_mixer.Mix_GetError());
        }

        SDL_mixer.Mix_QuerySpec(out sampleRate, out ushort format, out int channels);

        SDL_mixer.Mix_ReserveChannels(2);  // reserve player and boss weapon channels
        SDL_mixer.Mix_GroupChannels(2, SDL_mixer.MIX_CHANNELS - 1, 1); // group remaining channels

        // Init music
        var imfOpl = new WoodyEmulatorOpl(OplType.Opl2);
        imfOpl.Init(sampleRate);

        _imfPlayer = new ImfPlayer(imfOpl);
        _imfRefreshRateHz = _imfPlayer.RefreshRate;

        var adlOpl = new WoodyEmulatorOpl(OplType.Opl2);
        adlOpl.Init(sampleRate);
        _adlPlayer = new IdAdlPlayer(adlOpl);

        samplesPerMusicTick = (int)(sampleRate / _imfPlayer.RefreshRate);    // SDL_t0FastAsmService played at 700Hz

        SDL_mixer.Mix_HookMusic(SDL_IMFMusicPlayer, 0);
        SDL_mixer.Mix_ChannelFinished(SD_ChannelFinished);
        AdLibPresent = true;
        SoundBlasterPresent = true;

        alTimeCount = 0;

        // Add PC speaker sound mixer
        SDL_mixer.Mix_SetPostMix(SDL_PCMixCallback, IntPtr.Zero);

        SetSoundMode(SDMode.Off);
        SetMusicMode(SMMode.Off);

        SDL_SetupDigi();
        SD_Started = true;
    }

    private void SDL_SoundFinished()
    {
        SoundNumber = 0;
        SoundPriority = 0;
    }

    ///////////////////////////////////////////////////////////////////////////
    //
    //      SDL_PCPlaySound() - Plays the specified sound on the PC speaker
    //
    ///////////////////////////////////////////////////////////////////////////
    internal void SDL_PCPlaySound(PcSound sound)
    {
        pcLastSample = unchecked((byte)-1);
        pcLengthLeft = sound.Common.Length;
        pcSound = sound.Data;
        pcSoundPtr = 0;
    }

    ///////////////////////////////////////////////////////////////////////////
    //
    //      SDL_PCStopSound() - Stops the current sound playing on the PC Speaker
    //
    ///////////////////////////////////////////////////////////////////////////
    internal void SDL_PCStopSound()
    {
        pcSound = [];
        pcSoundPtr = 0;
    }


    ///////////////////////////////////////////////////////////////////////////
    //
    //      SDL_ShutPC() - Turns off the pc speaker
    //
    ///////////////////////////////////////////////////////////////////////////
    internal void SDL_ShutPC()
    {
        pcSound = [];
        pcSoundPtr = 0;
    }

    internal const int SQUARE_WAVE_AMP = 0x2000;
    private readonly AssetManager assetManager;
    private int current_remaining = 0;
    private int current_freq = 0;
    private int phase_offset = 0;
    private void SDL_PCMixCallback(nint udata, nint stream, int len)
    {
        unsafe
        {
            short* leftptr;
            short* rightptr;
            short this_value;
            int i;
            int nsamples;

            // Number of samples is quadrupled, because of 16-bit and stereo

            nsamples = len / 4;

            leftptr = (short*)stream;
            rightptr = ((short*)stream) + 1;

            // Fill the output buffer

            for (i = 0; i < nsamples; ++i)
            {
                // Has this sound expired? If so, retrieve the next frequency

                while (current_remaining == 0)
                {
                    phase_offset = 0;

                    // Get the next frequency to play

                    if (pcSound != null && pcSound.Length > 0 && pcSoundPtr < pcSound.Length)
                    {
                        // The PC speaker sample rate is 140Hz (see SDL_t0SlowAsmService)
                        current_remaining = _sampleRate / 140;

                        if (pcSound[pcSoundPtr] != pcLastSample)
                        {
                            pcLastSample = pcSound[pcSoundPtr];

                            if (pcLastSample != 0)
                                // The PC PIC counts down at 1.193180MHz
                                // So pwm_freq = counter_freq / reload_value
                                // reload_value = pcLastSample * 60 (see SDL_DoFX)
                                current_freq = 1193180 / (pcLastSample * 60);
                            else
                                current_freq = 0;

                        }
                        pcSoundPtr++;
                        pcLengthLeft--;
                        if (pcLengthLeft <= 0)
                        {
                            pcSound = [];
                            pcSoundPtr = 0;
                            SoundNumber = 0;
                            SoundPriority = 0;
                        }
                    }
                    else
                    {
                        current_freq = 0;
                        current_remaining = 1;
                    }
                }

                // Set the value for this sample.

                if (current_freq == 0)
                {
                    // Silence

                    this_value = 0;
                }
                else
                {
                    int frac;

                    // Determine whether we are at a peak or trough in the current
                    // sound.  Multiply by 2 so that frac % 2 will give 0 or 1
                    // depending on whether we are at a peak or trough.

                    frac = (phase_offset * current_freq * 2) / _sampleRate;

                    if ((frac % 2) == 0)
                    {
                        this_value = SQUARE_WAVE_AMP;
                    }
                    else
                    {
                        this_value = -SQUARE_WAVE_AMP;
                    }

                    ++phase_offset;
                }

                --current_remaining;

                *leftptr += this_value;
                *rightptr += this_value;

                leftptr += 2;
                rightptr += 2;
            }
        }
    }

    private void SD_ChannelFinished(int channel)
    {
        channelSoundPos[channel].valid = 0;
    }

    internal void SDL_IMFMusicPlayer(nint udata, nint stream, int len)
    {
        // len = bytes to fill; stereo 16-bit -> 4 bytes per frame
        int framesToFill = len / 4;
        unsafe
        {
            short* dst = (short*)stream; // destination pointer in shorts (interleaved stereo)
            int framesRemaining = framesToFill;

            while (framesRemaining > 0)
            {
                // Ensure we have a chunk of ready samples to copy
                if (numreadysamples == 0)
                {
                    soundTimeCounter--;
                    if (soundTimeCounter == 0)
                    {
                        soundTimeCounter = 5; // paces the sound at 140hz
                        if (!_adlPlayer.Update())
                        {
                            SoundNumber = 0;
                            SoundPriority = 0;
                        }
                    }

                    // Sequencer / player bookkeeping copied from original logic
                    if (sqActive)
                    {
                        if (sqHackTime <= alTimeCount)
                        {
                            var playing = _imfPlayer.Update();
                            if (!playing)
                            {
                                _imfPlayer.Restart();
                                alTimeCount = 0;
                                sqHackTime = 0;
                                continue;
                            }

                            uint time = (uint)Math.Round((_imfRefreshRateHz / _imfPlayer.RefreshRate));
                            sqHackTime = (alTimeCount + time);
                        }
                        alTimeCount++;
                    }

                    numreadysamples = samplesPerMusicTick;
                }

                int takeFrames = Math.Min(numreadysamples, framesRemaining);
                int takeSamples = takeFrames * 2; // shorts (left+right)

                // ReadBuffer fills short[] with interleaved stereo samples
                // Read from both OPL instances and mix
                short[] imfBuffer = new short[takeSamples];
                short[] adlBuffer = new short[takeSamples];

                _imfPlayer.Opl.ReadBuffer(imfBuffer, 0, imfBuffer.Length);
                _adlPlayer.Opl.ReadBuffer(adlBuffer, 0, adlBuffer.Length);

                // Mix both buffers into destination
                fixed (short* imfPtr = imfBuffer, adlPtr = adlBuffer)
                {
                    for (int i = 0; i < takeSamples; i++)
                    {
                        // Simple mix: average both sources to prevent clipping
                        int mixed = (imfPtr[i] + adlPtr[i]) / 2;
                        // Clamp to short range
                        dst[i] = (short)Math.Clamp(mixed, short.MinValue, short.MaxValue);
                    }
                }

                // advance destination pointer by number of shorts copied
                dst += takeSamples;
                framesRemaining -= takeFrames;
                numreadysamples -= takeFrames;
            }
        }
    }

    ///////////////////////////////////////////////////////////////////////////
    //
    //      SD_PositionSound() - Sets up a stereo imaging location for the next
    //              sound to be played. Each channel ranges from 0 to 15.
    //
    ///////////////////////////////////////////////////////////////////////////
    internal void SD_PositionSound(int leftvol, int rightvol)
    {
        LeftPosition = leftvol;
        RightPosition = rightvol;
        nextsoundpos = true;
    }

    internal int SD_PlaySound(string sound)
    {
        bool ispos;
        SoundCommon s;
        int lp, rp;

        lp = LeftPosition;
        rp = RightPosition;
        LeftPosition = 0;
        RightPosition = 0;

        ispos = nextsoundpos;
        nextsoundpos = false;

        var soundIndex = AudioMappings.AudioTKeys.IndexOf(sound);
        if (soundIndex == -1 || (DigiMode == SDSMode.Off && SoundMode == SDMode.Off))
            return 0;

        var digiSound = assetManager.GetSound(sound);
        if ((SoundMode != SDMode.Off) && digiSound == null)
        {
            Console.WriteLine($"{nameof(SD_PlaySound)}({sound}) - Sound not found.");
            return 0;
            //throw new PfWolfAudioException("SD_PlaySound({sound}) - Uncached sound", sound);
        }


        // TODO: Handle PC Sound, AdLib, or Digi
        //var sData = SoundTable[sound]; // TODO: This might need a better way to get soundtable data
        // var soundSeg = audiosegs[soundIndex + SoundTable];
        // if (soundSeg is PCSound)
        //     s = soundSeg.common;
        // else
        s = new SoundCommon(digiSound.RawData);// (SoundCommon*)SoundTable[sound];

        if ((DigiMode != SDSMode.Off) && (DigiMap[soundIndex] != -1))
        {
            //if ((DigiMode == SDSMode.PC) && (SoundMode == SDMode.PC))
            //{
            //    if (s.priority < SoundPriority)
            //        return 0;

            //    SDL_PCStopSound();

            //    SD_PlayDigitized(sound, lp, rp);
            //    SoundPositioned = ispos;
            //    SoundNumber = soundIndex;
            //    SoundPriority = s.priority;
            //}
            //else
            {
                //# ifdef NOTYET
                //                if (s->priority < DigiPriority)
                //                    return (false);
                //#endif

                int channel = SD_PlayDigitized(sound, lp, rp);
                SoundPositioned = ispos;
                DigiNumber = soundIndex;
                DigiPriority = s.priority;
                return channel + 1;
            }

            return 1;
        }

        if (SoundMode == SDMode.Off)
            return 0;

        if (s.length == 0)
            throw new PfWolfAudioException("SD_PlaySound() - Zero length sound");
        if (s.priority < SoundPriority)
            return 0;

        switch (SoundMode)
        {
            case SDMode.PC:
                //SDL_PCPlaySound((PCSound)soundSeg);
                break;
            case SDMode.AdLib:
                //curAlSound = [];
                //alSound = [];                // Tricob
                //alOut(alFreqH, 0);
               // SDL_ALPlaySound((AdLibSound)soundSeg);
                break;

            default:
                break;
        }

        SoundNumber = soundIndex;
        SoundPriority = s.priority;

        return 0;
    }

    internal void SD_StopSound()
    {
        if (DigiPlaying)
            SD_StopDigitized();

        switch (SoundMode)
        {
            case SDMode.PC:
                SDL_PCStopSound();
                break;
            case SDMode.AdLib:
                SDL_ALStopSound();
                break;

            default:
                break;
        }

        SoundPositioned = false;

        SDL_SoundFinished();
    }

    internal void SD_Shutdown()
    {
        int i;

        if (!SD_Started)
            return;

        SD_MusicOff();
        SD_StopSound();

        // TODO: Free all sound chunks in Dictionary
        //for (i = 0; i < STARTMUSIC - STARTDIGISOUNDS; i++)
        //{
        //    if (SoundChunks[i] != IntPtr.Zero)
        //        Mix_FreeChunk(SoundChunks[i]);
        //}

        //DigiList = [];

        SD_Started = false;
    }

    internal void SD_PrepareSound(string which)
    {
        try
        {
            //if (DigiList?.Length == 0)
            //   throw new PfWolfAudioException("SD_PrepareSound({which}): DigiList not initialized!", which.ToString());

            var soundAsset = assetManager.GetSound(which);
            if (soundAsset == null)
            {
                Console.WriteLine($"{nameof(SD_PrepareSound)}({which}) - Sound asset not found.");
                return;
            }

            byte[] wavebuffer = soundAsset.ToRawWav(_sampleRate);
            
            GCHandle pinnedArray = GCHandle.Alloc(wavebuffer, GCHandleType.Pinned);
            IntPtr pointer = pinnedArray.AddrOfPinnedObject();

            IntPtr temp = SDL_RWFromMem(pointer, wavebuffer.Length);
            SoundChunks[which] = Mix_LoadWAV_RW(temp, 1);
            pinnedArray.Free();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    internal int SD_GetChannelForDigi(int which)
    {
        if (DigiChannel[which] != -1) return DigiChannel[which];

        int channel = Mix_GroupAvailable(1);
        if (channel == -1) channel = Mix_GroupOldest(1);
        if (channel == -1)           // All sounds stopped in the meantime?
            return Mix_GroupAvailable(1);
        return channel;
    }

    internal void SD_SetPosition(int channel, int leftpos, int rightpos)
    {
        if ((leftpos < 0) || (leftpos > 15) || (rightpos < 0) || (rightpos > 15)
                || ((leftpos == 15) && (rightpos == 15)))
            throw new PfWolfAudioException("SD_SetPosition: Illegal position");

        switch (DigiMode)
        {
            case SDSMode.SoundBlaster:
                //            SDL_PositionSBP(leftpos,rightpos);
                Mix_SetPanning(channel, (byte)(255 - (leftpos * 28)), (byte)(255 - (rightpos * 28)));
                break;

            default:
                break;
        }
    }

    internal int SD_PlayDigitized(string which, int leftpos, int rightpos)
    {
        if (DigiMode == SDSMode.Off)
            return 0;

        //if (which >= NumDigi)
        //    throw new PfWolfAudioException($"SD_PlayDigitized: bad sound number {which}");

        int channel = SD_GetChannelForDigi(0);// which); // TODO: Get channel info for digitized sound
        SD_SetPosition(channel, leftpos, rightpos);

        DigiPlaying = true;

        IntPtr sample = SoundChunks[which];
        if (sample == IntPtr.Zero)
        {
            Console.WriteLine($"SoundChunks[{which}] is NULL!");
            return 0;
        }

        if (Mix_PlayChannel(channel, sample, 0) == -1)
        {
            throw new PfWolfAudioException("Unable to play sound {error}", Mix_GetError());
        }

        return channel;
    }

    public void SD_MusicOn()
    {
        sqActive = true;
    }

    public int SD_MusicOff()
    {
        ushort i;

        sqActive = false;
        switch (MusicMode)
        {
            case SMMode.AdLib:
                //alOut(alEffects, 0);
                //for (i = 0; i < sqMaxTracks; i++)
                //    alOut(alFreqH + i + 1, 0);
                break;

            default:
                break;
        }

        return (int)0;// (sqHackPtr - sqHack);
    }

    internal void SD_StartMusic(string song)
    {
        //int chunk = AudioMappings.MusicKeys.IndexOf(song);
        //if (chunk == -1)
        //    return;

        SD_MusicOff();

        if (MusicMode == SMMode.AdLib)
        {
            var imfChunk = assetManager.GetImf(song);
            if (imfChunk == null || imfChunk.Size == 0)
            {
                Console.WriteLine($"IMF Music Asset {song} not found.");
                return;
            }

            int chunkLen = imfChunk.Size;
            using (var ms = new MemoryStream(imfChunk.RawData))
            {
                _imfPlayer.Load(ms);
                _imfRefreshRateHz = _imfPlayer.RefreshRate;
            }

            //sqHack = audiosegs[STARTMUSIC + chunk];     // alignment is correct
            //if (*sqHack == 0) sqHackLen = sqHackSeqLen = chunkLen;
            //else sqHackLen = sqHackSeqLen = *sqHack++;
            sqHackPtr = 0;// sqHack;
            sqHackTime = 0;
            alTimeCount = 0;
            SD_MusicOn();
        }
    }

    internal void SD_ContinueMusic(string song, int startoffs)
    {
        int i;

        SD_MusicOff();

        if (MusicMode == SMMode.AdLib)
        {
          //  int chunk = AudioMappings.MusicKeys.IndexOf(song);
          //  if (chunk == -1)
          //      return;
            var imfChunk = assetManager.GetImf(song);
            if (imfChunk == null || imfChunk.Size == 0)
            {
                Console.WriteLine($"IMF Music Asset {song} not found.");
                return;
            }
            int chunkLen = imfChunk.Size;
            //sqHack = (word*)(void*)audiosegs[STARTMUSIC + chunk];     // alignment is correct
            //if (*sqHack == 0) sqHackLen = sqHackSeqLen = chunkLen;
            //else sqHackLen = sqHackSeqLen = *sqHack++;
            // sqHackPtr = sqHack;

            if (startoffs >= sqHackLen)
            {
                startoffs = 0;
            }

            // fast forward to correct position
            // (needed to reconstruct the instruments)

            for (i = 0; i < startoffs; i += 2)
            {
                byte reg = 0;// *(byte*)sqHackPtr;
                byte val = 0;// *(((byte*)sqHackPtr) + 1);
                if (reg >= 0xb1 && reg <= 0xb8) val &= 0xdf;           // disable play note flag
                else if (reg == 0xbd) val &= 0xe0;                     // disable drum flags

                //alOut(reg, val);
                sqHackPtr += 2;
                sqHackLen -= 4;
            }
            sqHackTime = 0;
            alTimeCount = 0;

            SD_MusicOn();
        }
    }

    internal void SD_FadeOutMusic()
    {
        switch (MusicMode)
        {
            case SMMode.AdLib:
                // DEBUG - quick hack to turn the music off
                SD_MusicOff();
                break;

            default:
                break;
        }
    }
    internal bool SD_MusicPlaying()
    {
        bool result;

        switch (MusicMode)
        {
            case SMMode.AdLib:
                result = sqActive;
                break;
            default:
                result = false;
                break;
        }

        return (result);
    }
    public bool SetMusicMode(SMMode mode)
    {
        bool result = false;

        SD_FadeOutMusic();
        while (SD_MusicPlaying())
            GameEngineManager.DelayMs(5);

        switch (mode)
        {
            case SMMode.Off:
                result = true;
                break;
            case SMMode.AdLib:
                if (AdLibPresent)
                    result = true;
                break;
        }

        if (result)
            MusicMode = mode;

        return (result);
    }
    public bool SetSoundMode(SDMode mode)
    {
        bool result = false;
        //ushort tableoffset;

        SD_StopSound();

        if ((mode == SDMode.AdLib) && !AdLibPresent)
            mode = SDMode.PC;

        switch (mode)
        {
            case SDMode.Off:
          //      tableoffset = (ushort)STARTADLIBSOUNDS;
                result = true;
                break;
            case SDMode.PC:
            //    tableoffset = STARTPCSOUNDS;
                result = true;
                break;
            case SDMode.AdLib:
              //  tableoffset = (ushort)STARTADLIBSOUNDS;
                if (AdLibPresent)
                    result = true;
                break;
            default:
                throw new PfWolfAudioException("SD_SetSoundMode: Invalid sound mode {mode}", mode.ToString());
        }

        // Instead of a byte[][] reference, let's just offset where the sounds start, for now.
     //   SoundTable = tableoffset;

        if (result && (mode != SoundMode))
        {
            SDL_ShutDevice();
            SoundMode = mode;
            SDL_StartDevice();
        }

        return (result);
    }
    internal void SDL_StartDevice()
    {
        switch (SoundMode)
        {
            case SDMode.AdLib:
                SDL_StartAL();
                break;

            default:
                break;
        }

        SoundNumber = 0;
        SoundPriority = 0;
    }

    internal void SDL_StartAL()
    {
        //alOut(alEffects, 0);
        //SDL_AlSetFXInst(alZeroInst);
    }

    internal void SDL_ShutDevice()
    {
        switch ((SDMode)SoundMode)
        {
            case SDMode.PC:
                SDL_ShutPC();
                break;
            case SDMode.AdLib:
                SDL_ShutAL();
                break;

            default:
                break;
        }
        SoundMode = (sbyte)SDMode.Off;
    }

    internal void SDL_ShutAL()
    {
        //alSound = [];
        //alOut(alEffects, 0);
        //alOut(alFreqH + 0, 0);
        //SDL_AlSetFXInst(alZeroInst);
    }

    public void SetDigiDevice(SDSMode mode)
    {
        bool devicenotpresent;

        if (mode == DigiMode)
            return;

        SD_StopDigitized();

        devicenotpresent = false;
        switch (mode)
        {
            case SDSMode.SoundBlaster:
                if (!SoundBlasterPresent)
                    devicenotpresent = true;
                break;

            default:
                break;
        }

        if (!devicenotpresent)
        {
            DigiMode = mode;
        }
    }

    public void SD_StopDigitized()
    {
        DigiPlaying = false;
        DigiNumber = 0;
        DigiPriority = 0;
        SoundPositioned = false;
        //if ((DigiMode == SDSMode.PC) && (SoundMode == SDMode.PC))
        //    SDL_SoundFinished();

        switch (DigiMode)
        {
            //case SDSMode.PC:
            //    SDL_PCStopSound();
            //    break;
            case SDSMode.SoundBlaster:
                Mix_HaltChannel(-1);
                break;

            default:
                break;
        }
    }

    public string SD_SoundPlaying()
    {
        // TODO: Override the sound check until implemented
        return "";

        string result = "";

        //switch (SoundMode)
        //{
        //    case SDMode.PC:
        //        result = pcSound?.Length != 0 ? 1 : 0;
        //        break;
        //    case SDMode.AdLib:
        //        result = alSound?.Length != 0 ? 1 : 0;
        //        break;

        //    default:
        //        break;
        //}

        return result; // sound index being played
    }

    internal void SD_WaitSoundDone()
    {
        //while (SD_SoundPlaying() != 0)
        //{
        //    GameEngineManager.DelayMs(5);
        //}
    }

    internal void SDL_ALStopSound()
    {
        //alSound = [];
        //alOut(alFreqH + 0, 0);
    }

    // TODO: Part of the IdAdlPlayer "SetInstrument"
    //internal static void SDL_AlSetFXInst(Instrument inst)
    //{
    //    sbyte c, m;

    //    m = 0;      // modulator cell for channel 0
    //    c = 3;      // carrier cell for channel 0

    //    alOut(m + alChar, inst.mChar);
    //    alOut(m + alScale, inst.mScale);
    //    alOut(m + alAttack, inst.mAttack);
    //    alOut(m + alSus, inst.mSus);
    //    alOut(m + alWave, inst.mWave);
    //    alOut(c + alChar, inst.cChar);
    //    alOut(c + alScale, inst.cScale);
    //    alOut(c + alAttack, inst.cAttack);
    //    alOut(c + alSus, inst.cSus);
    //    alOut(c + alWave, inst.cWave);

    //    // Note: Switch commenting on these lines for old MUSE compatibility
    //    //    alOutInIRQ(alFeedCon,inst->nConn);
    //    alOut(alFeedCon, 0);
    //}

    internal void SDL_ALPlaySound(AdLibSound sound)
    {
        SDL_ALStopSound();

        using (var ms = new MemoryStream(sound.RawData))
        {
            _adlPlayer.Load(ms);
        }

        // alLengthLeft = sound.common.length;
        //alBlock = (sbyte)(((sound.block & 7) << 2) | 0x20);
        //var inst = sound.inst;

        //if ((inst.mSus | inst.cSus) == 0)
        //{
        //    _gameEngineManager.Quit("SDL_ALPlaySound() - Bad instrument");
        //}

        // SDL_AlSetFXInst(inst);
        // alSound = sound.data;
    }

    internal void SDL_SetupDigi()
    {
        // TODO: Move this to a "raw file loader"


        for (int i = 0; i < AudioMappings.AudioTKeys.Count; i++)
        {
            DigiMap[i] = -1;
            DigiChannel.Add(-1);// = -1;
        }
    }

    internal int leftchannel, rightchannel;

    public void SetSoundChannels(int left, int right)
    {
        leftchannel = left;
        rightchannel = right;
    }

    /*
    ==========================
    =
    = SetSoundLocGlobal - Sets up globalsoundx & globalsoundy and then calls
    =       UpdateSoundLoc() to transform that into relative channel volumes. Those
    =       values are then passed to the Sound Manager so that they'll be used for
    =       the next sound played (if possible).
    =
    = JAB
    =
    ==========================
    */
    public void PlaySoundLocGlobal(string s, int gx, int gy,
        int viewx, int viewy, int viewsin, int viewcos)
    {
        SetSoundLoc(gx, gy,
                    viewx, viewy, viewsin, viewcos);
        SD_PositionSound(leftchannel, rightchannel);

        int channel = SD_PlaySound(s);
        if (channel != 0)
        {
            channelSoundPos[channel - 1].globalsoundx = gx;
            channelSoundPos[channel - 1].globalsoundy = gy;
            channelSoundPos[channel - 1].valid = 1;
        }
    }


    internal void UpdateSoundLoc(
        int viewx, int viewy, int viewsin, int viewcos)
    {
        int i;

        /*    if (SoundPositioned)
            {
                SetSoundLoc(globalsoundx,globalsoundy);
                SD_SetPosition(leftchannel,rightchannel);
            }*/

        for (i = 0; i < SDL_mixer.MIX_CHANNELS; i++)
        {
            if (channelSoundPos[i].valid != 0)
            {
                SetSoundLoc(channelSoundPos[i].globalsoundx,
                    channelSoundPos[i].globalsoundy,
                    viewx,viewy,viewsin,viewcos);
                SD_SetPosition(i, leftchannel, rightchannel);
            }
        }
    }
    /*
    ==========================
    =
    = SetSoundLoc - Given the location of an object (in terms of global
    =       coordinates, held in globalsoundx and globalsoundy), munges the values
    =       for an approximate distance from the left and right ear, and puts
    =       those values into leftchannel and rightchannel.
    =
    = JAB
    =
    ==========================
    */

    internal const int ATABLEMAX = 15;
    internal static byte[,] righttable = new byte[ATABLEMAX, ATABLEMAX * 2] {
        { 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 7, 7, 7, 6, 0, 0, 0, 0, 0, 1, 3, 5, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 7, 7, 6, 4, 0, 0, 0, 0, 0, 2, 4, 6, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 7, 6, 6, 4, 1, 0, 0, 0, 1, 2, 4, 6, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 7, 6, 5, 4, 2, 1, 0, 1, 2, 3, 5, 7, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 6, 5, 4, 3, 2, 2, 3, 3, 5, 6, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 6, 6, 5, 4, 4, 4, 4, 5, 6, 7, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 6, 6, 5, 5, 5, 6, 6, 7, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 6, 6, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8}
    };
    internal static byte[,] lefttable = new byte[ATABLEMAX, ATABLEMAX * 2] {
        { 8, 8, 8, 8, 8, 8, 8, 8, 5, 3, 1, 0, 0, 0, 0, 0, 6, 7, 7, 7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 6, 4, 2, 0, 0, 0, 0, 0, 4, 6, 7, 7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 6, 4, 2, 1, 0, 0, 0, 1, 4, 6, 6, 7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 7, 5, 3, 2, 1, 0, 1, 2, 4, 5, 6, 7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 6, 5, 3, 3, 2, 2, 3, 4, 5, 6, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 7, 6, 5, 4, 4, 4, 4, 5, 6, 6, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 7, 6, 6, 5, 5, 5, 6, 6, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 7, 7, 6, 6, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8}
    };

    internal void SetSoundLoc(int gx, int gy,
        int viewx, int viewy, int viewsin, int viewcos)
    {
        int xt, yt;
        int x, y;

        //
        // translate point to view centered coordinates
        //
        gx -= viewx;
        gy -= viewy;

        //
        // calculate newx
        //
        xt = MathUtils.FixedMul(gx, viewcos);
        yt = MathUtils.FixedMul(gy, viewsin);
        x = (xt - yt) >> MapConstants.TILESHIFT;

        //
        // calculate newy
        //
        xt = MathUtils.FixedMul(gx, viewsin);
        yt = MathUtils.FixedMul(gy, viewcos);
        y = (yt + xt) >> MapConstants.TILESHIFT;

        if (y >= ATABLEMAX)
            y = ATABLEMAX - 1;
        else if (y <= -ATABLEMAX)
            y = -ATABLEMAX;
        if (x < 0)
            x = -x;
        if (x >= ATABLEMAX)
            x = ATABLEMAX - 1;
        leftchannel = lefttable[x, y + ATABLEMAX];
        rightchannel = righttable[x, y + ATABLEMAX];
        //_audioManager.SetChannels(leftchannel,rightchannel); // TODO:

        //#if 0
        //    CenterWindow(8,1);
        //    US_PrintSigned(leftchannel);
        //    US_Print(",");
        //    US_PrintSigned(rightchannel);
        //    _videoManager.Update();
        //#endif
    }
}
