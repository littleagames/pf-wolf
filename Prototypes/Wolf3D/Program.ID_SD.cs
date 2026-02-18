using SDL2;

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

internal struct PCSound
{
    public SoundCommon common;
    public sbyte[] data;
    public PCSound()
    {
        data = new sbyte[1];
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

internal struct AdLibSound
{
    public SoundCommon common;
    public Instrument inst;
    public sbyte block;
    public sbyte[] data;

    public AdLibSound()
    {
        common = new();
        inst = new();
        data = new sbyte[1];
    }
}

internal partial class Program
{
    //id_sd.h
    internal const int ORIG_SOUNDCOMMON_SIZE = 6;
    internal const int ORIG_INSTRUMENT_SIZE = 16;
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

    internal static int ORIG_ADLIBSOUND_SIZE = (ORIG_SOUNDCOMMON_SIZE + ORIG_INSTRUMENT_SIZE + 2);


    internal const int TickBase = 70;     // 70Hz per tick - used as a base for timer 0

    // Global variables
    internal static bool AdLibPresent,
        SoundBlasterPresent, SBProPresent,
        SoundPositioned;
    internal static byte SoundMode;
    internal static byte MusicMode;
    internal static byte DigiMode;

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


    // Global variables
    internal static uint GetTimeCount() => ((SDL.SDL_GetTicks() * 7) / 100);

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

        // TODO:
        //if (Mix_OpenAudioDevice(param_samplerate, AUDIO_S16, 2, chunksize, NULL, SDL_AUDIO_ALLOW_FREQUENCY_CHANGE))
        //{
        //    snprintf(str, sizeof(str), "Unable to open audio device: %s\n", Mix_GetError());
        //    Error(str);
        //    return;
        //}

        //Mix_QuerySpec(&param_samplerate, NULL, NULL);

        //Mix_ReserveChannels(2);  // reserve player and boss weapon channels
        //Mix_GroupChannels(2, MIX_CHANNELS - 1, 1); // group remaining channels

        //// Init music

        //samplesPerMusicTick = param_samplerate / 700;    // SDL_t0FastAsmService played at 700Hz

        //if (YM3812Init(1, 3579545, param_samplerate))
        //{
        //    printf("Unable to create virtual OPL!!\n");
        //}

        //for (i = 1; i < 0xf6; i++)
        //    YM3812Write(oplChip, i, 0);

        //YM3812Write(oplChip, 1, 0x20); // Set WSE=1
        //                               //    YM3812Write(0,8,0); // Set CSM=0 & SEL=0		 // already set in for statement

        //Mix_HookMusic(SDL_IMFMusicPlayer, 0);
        //Mix_ChannelFinished(SD_ChannelFinished);
        //AdLibPresent = true;
        //SoundBlasterPresent = true;

        //alTimeCount = 0;

        //// Add PC speaker sound mixer
        //Mix_SetPostMix(SDL_PCMixCallback, NULL);

        SD_SetSoundMode((byte)SDMode.Off);
        SD_SetMusicMode((byte)SMMode.Off);

        SDL_SetupDigi();
        SD_Started = true;
    }

    internal static void SD_PlaySound(int sound)
    {

    }

    internal static void SD_StopSound()
    {
        // TODO:
    }

    internal static void SD_Shutdown()
    {
        if (!SD_Started)
            return;
        SD_Started = false;
    }

    internal static void SD_PrepareSound(int which)
    {

    }

    internal static int SD_PlayDigitized(ushort which, int leftpos, int rightpos)
    {
        if (DigiMode == 0)
            return 0;

        return 0;
    }

    internal static void SD_MusicOn()
    {
        
    }

    internal static void SD_MusicOff()
    {
    }

    static bool SD_SetMusicMode(byte mode)
    {
        bool result = false;

        return result;
    }
    static bool SD_SetSoundMode(byte mode)
    {
        bool result = false;

        return result;
    }
    static void SD_SetDigiDevice(byte mode)
    {
    }

    static void SD_StopDigitized()
    {
        // TODO:
    }

    static int SD_SoundPlaying()
    {
        byte result = 0;

        switch (SoundMode)
        {
            //case SDMode.PC:
            //    result = pcSound ? true : false;
            //    break;
            //case SDMode.AdLib:
            //    result = alSound ? true : false;
            //    break;

            default:
                break;
        }

        if (result > 0)
            return 1; // sound index being played
        else
            return 0;
    }

    static void SD_WaitSoundDone()
    {
        while (SD_SoundPlaying() != 0)
        {
            SDL.SDL_Delay(5);
        }
    }

    internal static void SDL_SetupDigi()
    {
        // TODO: read in ushort chunks
        byte[] soundInfoData = PM_GetPage(ChunksInFile - 1);
        //ushort soundInfoPage = PM_GetPage(ChunksInFile - 1);
        NumDigi = 0;// (word)PM_GetPageSize(ChunksInFile - 1) / 4;
        DigiList = new digiinfo[NumDigi];
    }
}
