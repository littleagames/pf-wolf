namespace Wolf3D;

internal partial class Program
{
    // id_ca.h
    internal const int NUMMAPS = 60;
    internal const int MAPPLANES = 3;
    internal static void UNCACHEAUDIOCHUNK(int chunk)
    {
        if (audiosegs[chunk] != null) 
        { 
            audiosegs[chunk] = null!;
        }
    }

    struct maptype
    {
        public int[] planestart;
        public UInt16[] planelength;
        public UInt16 width;
        public UInt16 height;
        public char[] name;

        public maptype()
        {
            planestart = new Int32[MAPPLANES];
            planelength = new UInt16[MAPPLANES];
            name = new char[16];
        }
    }

    struct mapfiletype
    {
        public UInt16 RLEWtag;
        //public UInt16 numplanes; // If >= 4
        public Int32[] headeroffsets;

        public mapfiletype()
        {
            headeroffsets = new Int32[NUMMAPS];
        }
    }

    static UInt16[][] mapsegs = new ushort[MAPPLANES][];
    static maptype[] mapheaderseg = new maptype[NUMMAPS];
    static Sound[] audiosegs = new Sound[NUMSNDCHUNKS];

    internal static string extension = string.Empty;
    internal static string graphext = string.Empty;
    internal static string audioext = string.Empty;
    internal const string mheadname = "maphead.";
    internal const string mfilename = "gamemaps."; // maptemp.
    internal const string aheadname = "audiohed.";
    internal const string afilename = "audiot.";


    internal static int[] audiostarts;


    internal static FileStream audiofile = null!;


    static SDMode oldsoundmode;

    static mapfiletype tinf;

    static void CA_Startup()
    {
        CAL_SetupMapFile();
        CAL_SetupAudioFile();
    }

    internal static void CA_Shutdown()
    {
        audiofile?.Close();
        // Do nothing else
        // GC will handle uncaching, and removing all array data
    }

    static void CAL_SetupMapFile()
    {
        int i;
        int pos;

        //
        // load maphead.ext (offsets and tileinfo for map file)
        //
        var fname = $"{mheadname}{extension}";
        if (!File.Exists(fname))
            CA_CannotOpen(fname);

        tinf = new mapfiletype();
        using (var fs = new FileStream(fname, FileMode.Open, FileAccess.Read))
        using (var br = new BinaryReader(fs))
        {
            tinf.RLEWtag = br.ReadUInt16();
            for (i = 0; i < NUMMAPS; i++)
                tinf.headeroffsets[i] = br.ReadInt32();
        }

        //
        // open the data file
        //
        fname = $"{mfilename}{extension}";

        if (!File.Exists(fname))
            CA_CannotOpen(fname);

        using (var fs = new FileStream(fname, FileMode.Open, FileAccess.Read))
        using (var br = new BinaryReader(fs))
        {
            //
            // load all map header
            //

            for (i = 0; i < NUMMAPS; i++)
            {
                pos = tinf.headeroffsets[i];
                if (pos < 0)                          // $FFFFFFFF start is a sparse map
                    continue;

                mapheaderseg[i] = new maptype();

                fs.Seek(pos, SeekOrigin.Begin);
                for (int p = 0; p < MAPPLANES; p++)
                {
                    mapheaderseg[i].planestart[p] = br.ReadInt32();
                }
                for (int p = 0; p < MAPPLANES; p++)
                {
                    mapheaderseg[i].planelength[p] = br.ReadUInt16();
                }
                mapheaderseg[i].width = br.ReadUInt16();
                mapheaderseg[i].height = br.ReadUInt16();
                for (int n = 0; n < 16; n++)
                    mapheaderseg[i].name[n] = (char)br.ReadByte();
            }
        }

        //
        // allocate space for 3 64*64 planes
        //

        for (i = 0; i < MAPPLANES; i++)
            mapsegs[i] = new ushort[MAPAREA];
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

    internal static void CA_CacheMap(int mapnum)
    {
        int pos, compressed;
        if (mapheaderseg[mapnum].width != MAPSIZE || mapheaderseg[mapnum].height != MAPSIZE)
            _gameEngineManager.Quit($"CA_CacheMap: Map not {MAPSIZE}*{MAPSIZE}!");

        string fname = $"{mfilename}{extension}";

        if (!File.Exists(fname))
            CA_CannotOpen(fname);

        //
        // load the planes into the allready allocated buffers
        //
        var size = MAPAREA * sizeof(ushort);
        using (FileStream fs = File.OpenRead(fname))
        using (BinaryReader br = new BinaryReader(fs))
        {
            for (var plane = 0; plane < MAPPLANES; plane++)
            {
                pos = mapheaderseg[mapnum].planestart[plane];
                compressed = mapheaderseg[mapnum].planelength[plane];

                if (compressed == 0)
                    continue; // empty plane

                //dest = mapsegs[plane]; // pointer to location to store

                fs.Seek(pos, SeekOrigin.Begin);

                var bufferseg = new byte[compressed];
                //sourceIndex = buffersegIndex; // Or just set index = 0;
                for (int i = 0; i < bufferseg.Length; i++)
                {
                    bufferseg[i] = br.ReadByte();
                }


                //
                // unhuffman, then unRLEW
                // The huffman'd chunk has a two byte expanded length first
                // The resulting RLEW chunk also does, even though it's not really
                // needed
                //
                var expanded = BitConverter.ToUInt16(bufferseg);
                var buffer2seg = new ushort[expanded/sizeof(ushort)]; // might be byte[expanded]
                CAL_CarmackExpand(bufferseg.Skip(sizeof(ushort)).ToArray(), buffer2seg, expanded);
                CA_RLEWexpand(buffer2seg.Skip(1).ToArray(), out ushort[] dest, size, tinf.RLEWtag);
                mapsegs[plane] = dest;
            }
        }
    }

    internal const ushort NEARTAG = 0xa7;
    internal const ushort FARTAG = 0xa8;

    internal static void CAL_CarmackExpand(byte[] source, ushort[] dest, int length)
    {
        ushort ch, chhigh, count, offset;
        int inptr = 0, outptr = 0, copyptr = 0;

        length /= 2;

        while (length > 0)
        {
            ch = BitConverter.ToUInt16(source, inptr);
            inptr += 2;
            chhigh = (ushort)(ch >> 8);
            if (chhigh == NEARTAG)
            {
                count = (ushort)(ch & 0xff);
                if (count == 0)
                {
                    ch |= source[inptr++];
                    dest[outptr++] = ch;
                    length--;
                }
                else
                {
                    offset = source[inptr++];
                    copyptr = outptr - offset;
                    length -= count;
                    if (length < 0) return;
                    while (count-- != 0)
                        dest[outptr++] = dest[copyptr++];
                }
            }
            else if (chhigh == FARTAG)
            {
                count = (ushort)(ch & 0xff);
                if (count == 0)
                {
                    ch |= source[inptr++];
                    dest[outptr++] = ch;
                    length--;
                }
                else
                {
                    offset = BitConverter.ToUInt16(source, inptr);
                    inptr += 2;
                    copyptr = offset;
                    length -= count;
                    if (length < 0) return;
                    while (count-- != 0)
                        dest[outptr++] = dest[copyptr++];
                }
            }
            else
            {
                dest[outptr++] = ch;
                length--;
            }
        }
    }

    internal static void CA_RLEWexpand(ushort[] source, out ushort[] dest, int length, ushort rlewtag)
    {
        ushort value, count, i;
        dest = new ushort[length/2];
        
        int sourceIndex = 0, destIndex = 0, endIndex = length / 2;

        //
        // expand it
        //
        do
        {
            value = source[sourceIndex++];
            if (value != rlewtag)
                //
                // uncompressed
                //
                dest[destIndex++] = value;
            else
            {
                //
                // compressed string
                //
                count = source[sourceIndex++];
                value = source[sourceIndex++];
                for (i = 1; i <= count; i++)
                    dest[destIndex++] = value;
            }

        } while (destIndex < endIndex);
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
