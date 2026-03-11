namespace Wolf3D;

internal partial class Program
{
    // id_ca.h
    internal static void UNCACHEAUDIOCHUNK(int chunk)
    {
        if (audiosegs[chunk] != null) 
        { 
            audiosegs[chunk] = null!;
        }
    }

    static Sound[] audiosegs = new Sound[NUMSNDCHUNKS];

    internal static string extension = string.Empty;
    internal static string audioext = string.Empty;
    internal const string aheadname = "audiohed.";
    internal const string afilename = "audiot.";


    internal static int[] audiostarts;


    internal static FileStream audiofile = null!;


    static SDMode oldsoundmode;


    static void CA_Startup()
    {
        CAL_SetupAudioFile();
    }

    internal static void CA_Shutdown()
    {
        audiofile?.Close();
        // Do nothing else
        // GC will handle uncaching, and removing all array data
    }



    internal static void CAL_SetupAudioFile()
    {
        //
        // load audiohed.ext (offsets for audio file)
        //
        var fname = $"{aheadname}{extension}";

        if (!File.Exists(fname))
        {
            CA_CannotOpen(fname);
            return;
        }

        // CA_LoadFile (doing this block instead of passing a pointer back out)
        var data = File.ReadAllBytes(fname);
        if (data.Length == 0 )
        {
            _gameEngineManager.Quit($"Unable to open {fname}. Data empty.");
            return;
        }

        audiostarts = new int[data.Length / sizeof(int)];

        Buffer.BlockCopy(data, 0, audiostarts, 0, data.Length);

        //
        // open the data file
        //
        if (!File.Exists(fname))
        {
            CA_CannotOpen(fname);
            return;
        }

        fname = $"{afilename}{extension}";
        audiofile = File.OpenRead(fname);
    }

    internal static void CA_LoadAllSounds()
    {
        uint start = 0, i;

        if (oldsoundmode != SDMode.Off)
        {
            switch ((SDMode)oldsoundmode)
            {
                case SDMode.PC:
                    start = STARTPCSOUNDS;
                    break;
                case SDMode.AdLib:
                    start = STARTADLIBSOUNDS;
                    break;
            }

            for (i = 0; i < NUMSOUNDS; i++, start++)
                UNCACHEAUDIOCHUNK((int)start);
        }

        oldsoundmode = SoundMode;

        switch (SoundMode)
        {
            case SDMode.Off:
                start = STARTADLIBSOUNDS;   // needed for priorities...
                break;
            case SDMode.PC:
                start = STARTPCSOUNDS;
                break;
            case SDMode.AdLib:
                start = STARTADLIBSOUNDS;
                break;
        }

        if (start == STARTADLIBSOUNDS)
        {
            for (i = 0; i < NUMSOUNDS; i++, start++)
                CA_CacheAdlibSoundChunk((int)start);
        }
        else
        {
            for (i = 0; i < NUMSOUNDS; i++, start++)
                CA_CacheAudioChunk((int)start);
        }
    }



    internal static void CA_WriteFile(string filename, byte[] data, int length)
    {
        try
        {
            using FileStream fs = File.Create(filename);
            using BinaryWriter br = new BinaryWriter(fs);
            {
                br.Write(data);
            }
        }
        catch (FileNotFoundException fnfEx)
        {
            CA_CannotOpen(filename);
            return;
        }
        catch (IOException ioEx)
        {
            _gameEngineManager.Quit($"Error writing file {filename}: {ioEx.Message}");
            return;
        }
    }

    internal static int CA_CacheAudioChunk (int chunk)
    {
        int pos = audiostarts[chunk];
        int size = audiostarts[chunk + 1] - pos;

        if (audiosegs[chunk] != null)
            return size;                        // already in memory

        //audiosegs[chunk] = new byte[size];
        BinaryReader br = new BinaryReader(audiofile);
        audiofile.Seek(pos, SeekOrigin.Begin);
        audiosegs[chunk] = new PCSound(br.ReadBytes(size));

        return size;
    }
    internal static int CA_CacheMusicChunk(musicnames chunk)
    {
        int pos = audiostarts[(int)chunk];
        int size = audiostarts[(int)chunk + 1] - pos;

        if (audiosegs[(int)chunk] != null)
            return size;                        // already in memory

        //audiosegs[chunk] = new byte[size];
        BinaryReader br = new BinaryReader(audiofile);
        audiofile.Seek(pos, SeekOrigin.Begin);
        audiosegs[(int)chunk] = new ImfMusic(br.ReadBytes(size));

        return size;
    }

    internal static void CA_CacheAdlibSoundChunk(int chunk)
    {
        int pos = audiostarts[chunk];
        int size = audiostarts[chunk + 1] - pos;
        if (audiosegs[chunk] != null)
            return;

        BinaryReader br = new BinaryReader(audiofile);
        audiofile.Seek(pos, SeekOrigin.Begin);
        //byte[] bufferseg = new byte[ORIG_ADLIBSOUND_SIZE - 1];
        //bufferseg = br.ReadBytes(ORIG_ADLIBSOUND_SIZE - 1);

        AdLibSound sound = new AdLibSound();// [size]; //size + sizeof(*sound) - ORIG_ADLIBSOUND_SIZE
        sound.common.length = br.ReadUInt32();
        sound.common.priority = br.ReadUInt16();

        sound.inst.mChar = br.ReadSByte();
        sound.inst.cChar = br.ReadSByte();
        sound.inst.mScale = br.ReadSByte();
        sound.inst.cScale = br.ReadSByte();
        sound.inst.mAttack = br.ReadSByte();
        sound.inst.cAttack = br.ReadSByte();
        sound.inst.mSus = br.ReadSByte();
        sound.inst.cSus = br.ReadSByte();
        sound.inst.mWave = br.ReadSByte();
        sound.inst.cWave = br.ReadSByte();
        sound.inst.nConn = br.ReadSByte();
        sound.inst.voice = br.ReadSByte();
        sound.inst.mode = br.ReadSByte();
        sound.inst.unused[0] = br.ReadSByte();
        sound.inst.unused[1] = br.ReadSByte();
        sound.inst.unused[2] = br.ReadSByte();
        sound.block = br.ReadSByte();

        sound.data = br.ReadBytes(size - ORIG_ADLIBSOUND_SIZE + 1).ToArray();

        audiosegs[chunk] = sound;
    }
}
