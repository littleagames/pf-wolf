using SDL2;
using System.Reflection;
using System.Runtime.InteropServices;
using static SDL2.SDL;
using static SDL2.SDL_mixer;

namespace Wolf3D;

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
    PC,
    SoundBlaster
}

internal struct digiinfo
{
    public uint startpage;
    public uint length;
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

internal class PCSound : Sound
{
    public byte[] data;
    public PCSound()
    {
        data = new byte[1];
    }

    public PCSound(byte[] data)
    {
        common = new SoundCommon(data);
        this.data = new byte[common.length]; // data length - sizeof(soundcommon)??
        Buffer.BlockCopy(data, 6, this.data, 0, (int)common.length);
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

internal abstract class Sound
{
    public SoundCommon common;
}

internal class AdLibSound : Sound
{
    public Instrument inst;
    public sbyte block;
    public byte[] data;

    public AdLibSound()
    {
        common = new();
        inst = new();
        data = new byte[1];
    }

    public AdLibSound(byte[] data)
    {
        common = new SoundCommon(data);
        inst = new Instrument
        {
            mChar = (sbyte)data[6],
            cChar = (sbyte)data[7],
            mScale = (sbyte)data[8],
            cScale = (sbyte)data[9],
            mAttack = (sbyte)data[10],
            cAttack = (sbyte)data[11],
            mSus = (sbyte)data[12],
            cSus = (sbyte)data[13],
            mWave = (sbyte)data[14],
            cWave = (sbyte)data[15],
            nConn = (sbyte)data[16],
            voice = (sbyte)data[17],
            mode = (sbyte)data[18]
        };
        this.data = new byte[common.length - 12]; // data length - sizeof(soundcommon) - sizeof(instrument) - block and unused bytes
    }
}

internal partial class Program
{
    //id_sd.h
    internal static int alOut(int n, int b) => 0; // TODO:
    //internal static int alOut(int n, int b) => YM3812Write(oplChip, n, b);

    internal const int TickBase = 70;     // 70Hz per tick - used as a base for timer 0

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
    internal static uint GetTimeCount() => ((SDL.SDL_GetTicks() * 7) / 100);

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

    internal static IntPtr[] SoundChunks = new IntPtr[STARTMUSIC - STARTDIGISOUNDS];

    internal static globalsoundpos[] channelSoundPos = new globalsoundpos[SDL_mixer.MIX_CHANNELS];


    // Global variables
    internal static bool AdLibPresent,
        SoundBlasterPresent, SBProPresent,
        SoundPositioned;
    internal static SDMode SoundMode;
    internal static SMMode MusicMode;
    internal static SDSMode DigiMode;
    internal static int SoundTable;// byte[][] SoundTable;

    static int[] DigiMap = new int[(int)soundnames.LASTSOUND];
    static int[] DigiChannel = new int[STARTMUSIC - STARTDIGISOUNDS];

    // Internal variables
    private static bool SD_Started;
    private static bool nextsoundpos;
    private static int SoundNumber;
    private static int DigiNumber;
    private static ushort SoundPriority;
    private static ushort DigiPriority;
    private static int LeftPosition;
    private static int RightPosition;

    private static ushort NumDigi;
    private static digiinfo[] DigiList;
    private static bool DigiPlaying;


    // PC Sound variables
    internal static volatile byte pcLastSample;
    internal static volatile byte[] pcSound;
    internal static volatile int pcSoundPtr;
    internal static uint pcLengthLeft;

    // AdLib variables
    internal static volatile byte[] alSound;
    internal static sbyte alBlock;
    internal static uint alLengthLeft;
    internal static uint alTimeCount;
    internal static Instrument alZeroInst;

    // Sequencer variables
    internal static volatile bool sqActive;
    internal static ushort[] sqHack;
    internal static int sqHackPtr;
    internal static int sqHackLen;
    internal static int sqHackSeqLen;
    internal static uint sqHackTime;

    private const int oplChip = 0;


    private static int numreadysamples = 0;
    private static byte[] curAlSound = [];
    private static byte[] curAlSoundPtr = [];
    private static uint curAlLengthLeft = 0;
    private static int soundTimeCounter = 5;
    private static int samplesPerMusicTick;


    private static void Delay(int wolfticks)
    {
        if (wolfticks > 0)
            SDL.SDL_Delay((uint)((wolfticks * 100) / 7));
    }

    private static void SDL_SoundFinished()
    {
        SoundNumber = 0;
        SoundPriority = 0;
    }

    ///////////////////////////////////////////////////////////////////////////
    //
    //      SDL_PCPlaySound() - Plays the specified sound on the PC speaker
    //
    ///////////////////////////////////////////////////////////////////////////
    internal static void SDL_PCPlaySound(PCSound sound)
    {
        pcLastSample = unchecked((byte)-1);
        pcLengthLeft = sound.common.length;
        pcSound = sound.data;
        pcSoundPtr = 0;
    }

    ///////////////////////////////////////////////////////////////////////////
    //
    //      SDL_PCStopSound() - Stops the current sound playing on the PC Speaker
    //
    ///////////////////////////////////////////////////////////////////////////
    internal static void SDL_PCStopSound()
    {
        pcSound = [];
        pcSoundPtr = 0;
    }


    ///////////////////////////////////////////////////////////////////////////
    //
    //      SDL_ShutPC() - Turns off the pc speaker
    //
    ///////////////////////////////////////////////////////////////////////////
    internal static void SDL_ShutPC()
    {
        pcSound = [];
        pcSoundPtr = 0;
    }

    internal const int SQUARE_WAVE_AMP = 0x2000;

    static int current_remaining = 0;
    static int current_freq = 0;
    static int phase_offset = 0;
    private static void SDL_PCMixCallback(nint udata, nint stream, int len)
    {
        unsafe {
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
                    current_remaining = param_samplerate / 140;

                    if (pcSound[pcSoundPtr] != pcLastSample)
                    {
                        pcLastSample = pcSound[pcSoundPtr];
                        Console.WriteLine($"{current_remaining}: pcLastSample: {pcLastSample}");

                    if (pcLastSample != 0)
                            // The PC PIC counts down at 1.193180MHz
                            // So pwm_freq = counter_freq / reload_value
                            // reload_value = pcLastSample * 60 (see SDL_DoFX)
                            current_freq = 1193180 / (pcLastSample * 60);
                        else
                            current_freq = 0;
                        Console.WriteLine($"{current_remaining}: current_freq: {current_freq}");

                    }
                    pcSoundPtr++;
                    pcLengthLeft--;
                    Console.WriteLine($"lenLeft: {pcLengthLeft}");
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

                frac = (phase_offset * current_freq * 2) / param_samplerate;

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

    internal static void SD_Startup()
    {
        int i;
        int chunksize;

        if (SD_Started)
            return;

        //
        // use a custom size audiobuffer or the largest power
        // of 2 <= the value calculated based on the samplerate
        //
        if (param_audiobuffer != DEFAULT_AUDIO_BUFFER_SIZE)
            chunksize = param_audiobuffer;
        else
        {
            if (param_samplerate == 0 || param_samplerate > 44100)
                Quit("Divide by zero caused by invalid samplerate!");

            chunksize = 1 << (int)Math.Log2(param_audiobuffer / (44100 / param_samplerate));
        }

        //if (SDL.SDL_OpenAudioDevice(param_samplerate, AUDIO_S16, chunksize, IntPtr.Zero, SDL_AUDIO_ALLOW_FREQUENCY_CHANGE))
        if (SDL_mixer.Mix_OpenAudioDevice(param_samplerate, SDL.AUDIO_S16, 2, chunksize, IntPtr.Zero, SDL.SDL_AUDIO_ALLOW_FREQUENCY_CHANGE) != 0)
        //if (SDL_mixer.Mix_OpenAudio(frequency: param_samplerate, format: SDL.AUDIO_S16, channels: 2, chunksize) != 0)//, IntPtr.Zero, SDL.SDL_AUDIO_ALLOW_FREQUENCY_CHANGE))
        {
            Error($"Unable to open audio device: {SDL_mixer.Mix_GetError()}\n");
            return;
        }

        SDL_mixer.Mix_QuerySpec(out param_samplerate, out ushort format, out int channels);

        SDL_mixer.Mix_ReserveChannels(2);  // reserve player and boss weapon channels
        SDL_mixer.Mix_GroupChannels(2, SDL_mixer.MIX_CHANNELS - 1, 1); // group remaining channels

        // Init music

        samplesPerMusicTick = param_samplerate / 700;    // SDL_t0FastAsmService played at 700Hz

        //if (YM3812Init(1, 3579545, param_samplerate))
        //{
        //    Console.WriteLine("Unable to create virtual OPL!!");
        //}

        //for (i = 1; i < 0xf6; i++)
        //    YM3812Write(oplChip, i, 0);

        //YM3812Write(oplChip, 1, 0x20); // Set WSE=1
                                       //    YM3812Write(0,8,0); // Set CSM=0 & SEL=0		 // already set in for statement
                                       
        SDL_mixer.Mix_HookMusic(SDL_IMFMusicPlayer, 0);
        SDL_mixer.Mix_ChannelFinished(SD_ChannelFinished);
        AdLibPresent = true;
        SoundBlasterPresent = true;

        alTimeCount = 0;

        // Add PC speaker sound mixer
        SDL_mixer.Mix_SetPostMix(SDL_PCMixCallback, IntPtr.Zero);

        SD_SetSoundMode((byte)SDMode.Off);
        SD_SetMusicMode((byte)SMMode.Off);

        SDL_SetupDigi();
        SD_Started = true;
    }

    private static void SD_ChannelFinished(int channel)
    {
        channelSoundPos[channel].valid = 0;
    }

    private static void SDL_IMFMusicPlayer(nint udata, nint stream, int len)
    {
        //throw new NotImplementedException();
    }

    ///////////////////////////////////////////////////////////////////////////
    //
    //      SD_PositionSound() - Sets up a stereo imaging location for the next
    //              sound to be played. Each channel ranges from 0 to 15.
    //
    ///////////////////////////////////////////////////////////////////////////
    internal static void SD_PositionSound(int leftvol,int rightvol)
    {
        LeftPosition = leftvol;
        RightPosition = rightvol;
        nextsoundpos = true;
    }

    internal static int SD_PlaySound(int sound)
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

        if (sound == -1 || (DigiMode == SDSMode.Off && SoundMode == SDMode.Off))
            return 0;

        //var sData = SoundTable[sound]; // TODO: This might need a better way to get soundtable data
        var soundSeg = audiosegs[sound + SoundTable];
        s = soundSeg.common;//new SoundCommon(sData);// (SoundCommon*)SoundTable[sound];

        if ((SoundMode != SDMode.Off) && soundSeg == null)
            Quit("SD_PlaySound() - Uncached sound");

        if ((DigiMode != SDSMode.Off) && (DigiMap[sound] != -1))
        {
            if ((DigiMode == SDSMode.PC) && (SoundMode == SDMode.PC))
            {
                if (s.priority < SoundPriority)
                    return 0;

                SDL_PCStopSound();

                SD_PlayDigitized((ushort)DigiMap[sound], lp, rp);
                SoundPositioned = ispos;
                SoundNumber = sound;
                SoundPriority = s.priority;
            }
            else
            {
//# ifdef NOTYET
//                if (s->priority < DigiPriority)
//                    return (false);
//#endif

                int channel = SD_PlayDigitized((ushort)DigiMap[sound], lp, rp);
                SoundPositioned = ispos;
                DigiNumber = sound;
                DigiPriority = s.priority;
                return channel + 1;
            }

            return 1;
        }

        if (SoundMode == SDMode.Off)
            return 0;

        if (s.length == 0)
            Quit("SD_PlaySound() - Zero length sound");
        if (s.priority < SoundPriority)
            return 0;

        switch (SoundMode)
        {
            case SDMode.PC:
                SDL_PCPlaySound((PCSound)soundSeg);
                break;
            case SDMode.AdLib:
                curAlSound = [];
                alSound = [];                // Tricob
                alOut(alFreqH, 0);
                SDL_ALPlaySound((AdLibSound)soundSeg);
                break;

            default:
                break;
        }

        SoundNumber = sound;
        SoundPriority = s.priority;

        return 0;
    }

    internal static void SD_StopSound()
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

    internal static void SD_Shutdown()
    {
        int i;

        if (!SD_Started)
            return;

        SD_MusicOff();
        SD_StopSound();

        for (i = 0; i < STARTMUSIC - STARTDIGISOUNDS; i++)
        {
            if (SoundChunks[i] != IntPtr.Zero)
                Mix_FreeChunk(SoundChunks[i]);
        }

        DigiList = [];

        SD_Started = false;
    }

    internal static void SD_PrepareSound(int which)
    {
        int i;

        if (DigiList?.Length == 0)
            Quit($"SD_PrepareSound({which}): DigiList not initialized!");

        int page = (int)DigiList[which].startpage;
        int size = (int)DigiList[which].length;

        byte[] origsamples = PM_GetSoundPage(page, size);

        int destsamples = (int)((float)size * (float)param_samplerate
            / (float)ORIGSAMPLERATE);

        byte[] wavebuffer = new byte[headchunk.size_of + wavechunk.size_of + destsamples * 2];     // dest are 16-bit samples

        GCHandle pinnedArray = GCHandle.Alloc(wavebuffer, GCHandleType.Pinned);
        IntPtr pointer = pinnedArray.AddrOfPinnedObject();

        headchunk head = new headchunk{
            RIFF = [(byte)'R', (byte)'I', (byte)'F', (byte)'F'],
            filelenminus8 = 0,
            WAVE = [(byte)'W', (byte)'A', (byte)'V', (byte)'E'],
            fmt_ = [(byte)'f', (byte)'m', (byte)'t', (byte)' '],
            formatlen = 0x10,
            val0x0001 = 0x0001,
            channels = 1,
            samplerate = (uint)param_samplerate,
            bytespersec = (uint) (param_samplerate*2),
            bytespersample = 2,
            bitspersample = 16
        };

        wavechunk dhead = new wavechunk
        { 
            chunkid = [(byte)'d', (byte)'a', (byte)'t', (byte)'a' ],
            chunklength = (uint)(destsamples * 2)
        };
        head.filelenminus8 = (uint)(headchunk.size_of + destsamples * 2);  // (sizeof(dhead)-8 = 0)

        var headData = head.AsBytes();
        Buffer.BlockCopy(headData, 0, wavebuffer, 0, headData.Length);
        var dheadData = dhead.AsBytes();
        Buffer.BlockCopy(dheadData, 0, wavebuffer, headData.Length, dheadData.Length);

        // alignment is correct, as wavebuffer comes from malloc
        // and sizeof(headchunk) % 4 == 0 and sizeof(wavechunk) % 4 == 0
        short[] newsamples = new short[(wavebuffer.Length + headchunk.size_of
            + wavechunk.size_of) / sizeof(short)];
        float cursample = 0.0F;
        float samplestep = (float)ORIGSAMPLERATE / (float)param_samplerate;
        for (i = 0; i < destsamples; i++, cursample += samplestep)
        {
            newsamples[i] = GetSample((float)size * (float)i / (float)destsamples,
                origsamples, size);
        }

        Buffer.BlockCopy(
            src: newsamples,
            srcOffset: 0,
            dst: wavebuffer,
            dstOffset: headData.Length + dheadData.Length,
            count: wavebuffer.Length - (headData.Length + dheadData.Length));
        IntPtr temp = SDL_RWFromMem(pointer, wavebuffer.Length);
        SoundChunks[which] = Mix_LoadWAV_RW(temp, 1);
        pinnedArray.Free();
    }

    internal static short GetSample(float csample, byte[] samples, int size)
    {
        float s0 = 0, s1 = 0, s2 = 0;
        int cursample = (int)csample;
        float sf = csample - (float)cursample;

        if (cursample - 1 >= 0) s0 = (float)(samples[cursample - 1] - 128);
        s1 = (float)(samples[cursample] - 128);
        if (cursample + 1 < size) s2 = (float)(samples[cursample + 1] - 128);

        float val = s0 * sf * (sf - 1) / 2 - s1 * (sf * sf - 1) + s2 * (sf + 1) * sf / 2;
        int intval = (int)(val * 256);
        if (intval < -32768) intval = -32768;
        else if (intval > 32767) intval = 32767;
        return (short)intval;
    }

    internal static int SD_GetChannelForDigi(int which)
    {
        if (DigiChannel[which] != -1) return DigiChannel[which];

        int channel = Mix_GroupAvailable(1);
        if (channel == -1) channel = Mix_GroupOldest(1);
        if (channel == -1)           // All sounds stopped in the meantime?
            return Mix_GroupAvailable(1);
        return channel;
    }

    internal static void SD_SetPosition(int channel, int leftpos, int rightpos)
    {
        if ((leftpos < 0) || (leftpos > 15) || (rightpos < 0) || (rightpos > 15)
                || ((leftpos == 15) && (rightpos == 15)))
            Quit("SD_SetPosition: Illegal position");

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

    internal static int SD_PlayDigitized(ushort which, int leftpos, int rightpos)
    {
        if (DigiMode == SDSMode.Off)
            return 0;

        if (which >= NumDigi)
            Quit($"SD_PlayDigitized: bad sound number {which}");

        int channel = SD_GetChannelForDigi(which);
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
            Console.WriteLine($"Unable to play sound: {Mix_GetError()}");
            return 0;
        }

        return channel;
    }

    internal static void SD_MusicOn()
    {
        sqActive = true;
    }

    internal static int SD_MusicOff()
    {
        ushort i;

        sqActive = false;
        switch (MusicMode)
        {
            case SMMode.AdLib:
                alOut(alEffects, 0);
                for (i = 0; i < sqMaxTracks; i++)
                    alOut(alFreqH + i + 1, 0);
                break;

            default:
                break;
        }

        return (int)0;// (sqHackPtr - sqHack);
    }

    internal static void SD_StartMusic(musicnames chunk)
    {
        SD_MusicOff();

        if (MusicMode == SMMode.AdLib)
        {
            int chunkLen = CA_CacheAudioChunk((int)chunk);
            //sqHack = audiosegs[chunk];     // alignment is correct
            //if (*sqHack == 0) sqHackLen = sqHackSeqLen = chunkLen;
            //else sqHackLen = sqHackSeqLen = *sqHack++;
            sqHackPtr = 0;// sqHack;
            sqHackTime = 0;
            alTimeCount = 0;
            SD_MusicOn();
        }
    }

    internal static void SD_ContinueMusic(int chunk, int startoffs)
    {
        int i;

        SD_MusicOff();

        if (MusicMode == SMMode.AdLib)
        {
            int chunkLen = CA_CacheAudioChunk(chunk);
            //sqHack = (word*)(void*)audiosegs[chunk];     // alignment is correct
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

                alOut(reg, val);
                sqHackPtr += 2;
                sqHackLen -= 4;
            }
            sqHackTime = 0;
            alTimeCount = 0;

            SD_MusicOn();
        }
    }

    internal static void SD_FadeOutMusic()
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
    internal static bool SD_MusicPlaying()
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
    static bool SD_SetMusicMode(SMMode mode)
    {
        bool result = false;

        SD_FadeOutMusic();
        while (SD_MusicPlaying())
            SDL_Delay(5);

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
    static bool SD_SetSoundMode(SDMode mode)
    {
        bool result = false;
        ushort tableoffset;

        SD_StopSound();

        if ((mode == SDMode.AdLib) && !AdLibPresent)
            mode = SDMode.PC;

        switch (mode)
        {
            case SDMode.Off:
                tableoffset = STARTADLIBSOUNDS;
                result = true;
                break;
            case SDMode.PC:
                tableoffset = STARTPCSOUNDS;
                result = true;
                break;
            case SDMode.AdLib:
                tableoffset = STARTADLIBSOUNDS;
                if (AdLibPresent)
                    result = true;
                break;
            default:
                Quit($"SD_SetSoundMode: Invalid sound mode {mode}");
                return false;
        }

        // Instead of a byte[][] reference, let's just offset where the sounds start, for now.
        SoundTable = tableoffset;

        if (result && (mode != SoundMode))
        {
            SDL_ShutDevice();
            SoundMode = mode;
            SDL_StartDevice();
        }

        return (result);
    }
    internal static void SDL_StartDevice()
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

    internal static void SDL_StartAL()
    {
        alOut(alEffects, 0);
        SDL_AlSetFXInst(alZeroInst);
    }

    internal static void SDL_ShutDevice()
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

    internal static void SDL_ShutAL()
    {
        alSound = [];
        alOut(alEffects, 0);
        alOut(alFreqH + 0, 0);
        SDL_AlSetFXInst(alZeroInst);
    }

    static void SD_SetDigiDevice(SDSMode mode)
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

    static void SD_StopDigitized()
    {
        DigiPlaying = false;
        DigiNumber = 0;
        DigiPriority = 0;
        SoundPositioned = false;
        if ((DigiMode == SDSMode.PC) && (SoundMode == SDMode.PC))
            SDL_SoundFinished();

        switch (DigiMode)
        {
            case SDSMode.PC:
                SDL_PCStopSound();
                break;
            case SDSMode.SoundBlaster:
                Mix_HaltChannel(-1);
                break;

            default:
                break;
        }
    }

    static int SD_SoundPlaying()
    {
        // TODO: Override the sound check until implemented
        return 0;

        int result = 0;

        switch (SoundMode)
        {
            case SDMode.PC:
                result = pcSound?.Length != 0 ? 1 : 0;
                break;
            case SDMode.AdLib:
                result = alSound?.Length != 0 ? 1 : 0;
                break;

            default:
                break;
        }

        return result; // sound index being played
    }

    static void SD_WaitSoundDone()
    {
        //while (SD_SoundPlaying() != 0)
        //{
        //    SDL.SDL_Delay(5);
        //}
    }

    internal static void SDL_ALStopSound()
    {
        alSound = [];
        alOut(alFreqH + 0, 0);
    }

    internal static void SDL_AlSetFXInst(Instrument inst)
    {
        sbyte c, m;

        m = 0;      // modulator cell for channel 0
        c = 3;      // carrier cell for channel 0

        alOut(m + alChar, inst.mChar);
        alOut(m + alScale, inst.mScale);
        alOut(m + alAttack, inst.mAttack);
        alOut(m + alSus, inst.mSus);
        alOut(m + alWave, inst.mWave);
        alOut(c + alChar, inst.cChar);
        alOut(c + alScale, inst.cScale);
        alOut(c + alAttack, inst.cAttack);
        alOut(c + alSus, inst.cSus);
        alOut(c + alWave, inst.cWave);

        // Note: Switch commenting on these lines for old MUSE compatibility
        //    alOutInIRQ(alFeedCon,inst->nConn);
        alOut(alFeedCon, 0);
    }

    internal static void SDL_ALPlaySound(AdLibSound sound)
    {
        SDL_ALStopSound();

        alLengthLeft = sound.common.length;
        byte[] data = sound.data;
        alBlock = (sbyte)(((sound.block & 7) << 2) | 0x20);
        var inst = sound.inst;

        if ((inst.mSus | inst.cSus) == 0)
        {
            Quit("SDL_ALPlaySound() - Bad instrument");
        }

        SDL_AlSetFXInst(inst);
        alSound = data;
    }

    internal static void SDL_SetupDigi()
    {
        // TODO: read in ushort chunks
        byte[] soundInfoData = PM_GetPage(ChunksInFile - 1);
        ushort[] soundInfoPage = new ushort[soundInfoData.Length / 2];
        Buffer.BlockCopy(soundInfoData, 0, soundInfoPage, 0, soundInfoData.Length);

        NumDigi = (ushort)(PM_GetPageSize(ChunksInFile - 1) / 4);

        DigiList = new digiinfo[NumDigi];
        int i, page;

        for (i = 0; i < NumDigi; i++)
        {
            // Calculate the size of the digi from the sizes of the pages between
            // the start page and the start page of the next sound

            DigiList[i].startpage = soundInfoPage[i * 2];
            if ((int)DigiList[i].startpage >= ChunksInFile - 1)
            {
                NumDigi = (ushort)i;
                break;
            }

            int lastPage;
            if (i < NumDigi - 1)
            {
                lastPage = soundInfoPage[i * 2 + 2];
                if (lastPage == 0 || lastPage + PMSoundStart > ChunksInFile - 1) lastPage = ChunksInFile - 1;
                else lastPage += PMSoundStart;
            }
            else lastPage = ChunksInFile - 1;

            int size = 0;
            for (page = (int)(PMSoundStart + DigiList[i].startpage); page < lastPage; page++)
                size += (int)PM_GetPageSize(page);

            // Don't include padding of sound info page, if padding was added
            if (lastPage == ChunksInFile - 1 && PMSoundInfoPagePadded) size--;

            // Patch lower 16-bit of size with size from sound info page.
            // The original VSWAP contains padding which is included in the page size,
            // but not included in the 16-bit size. So we use the more precise value.
            if ((size & 0xffff0000) != 0 && (size & 0xffff) < soundInfoPage[i * 2 + 1])
                size -= 0x10000;
            size = (int)((size & 0xffff0000) | soundInfoPage[i * 2 + 1]);

            DigiList[i].length = (uint)size;
        }

        for (i = 0; i < (byte)soundnames.LASTSOUND; i++)
        {
            DigiMap[i] = -1;
            DigiChannel[i] = -1;
        }
    }
}
