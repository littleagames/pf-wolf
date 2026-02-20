using System.Diagnostics;
using System.Runtime.InteropServices;

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

    // id_ca.c
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct huffnode
    {
        public ushort bit0, bit1; // 0-255 is a character, > is a pointer to a node
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
    static byte[][] grsegs = new byte[NUMCHUNKS][];

    internal static string extension = string.Empty;
    internal static string graphext = string.Empty;
    internal static string audioext = string.Empty;
    internal const string gheadname = "vgahead.";
    internal const string gfilename = "vgagraph.";
    internal const string gdictname = "vgadict.";
    internal const string mheadname = "maphead.";
    internal const string mfilename = "gamemaps."; // maptemp.
    internal const string aheadname = "audiohed.";
    internal const string afilename = "audiot.";


    internal static int[] grstarts = new int[NUMCHUNKS + 1];
    internal static int[] audiostarts;

    internal static huffnode[] grhuffman = new huffnode[255];

    internal static FileStream audiofile = null!;

    static int chunkcomplen, chunkexplen;

    static sbyte oldsoundmode;

    static mapfiletype tinf;

    static void CA_Startup()
    {
        CAL_SetupMapFile();
        CAL_SetupGrFile();
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

    internal static void CAL_SetupGrFile()
    {
        var fname = $"{gdictname}{extension}";
        if (!File.Exists(fname))
        {
            CA_CannotOpen(fname);
            return;
        }

        using (FileStream fs =  File.OpenRead(fname))
            using (BinaryReader br = new BinaryReader(fs))
        {
            for(int i = 0; i < 255; i++)
            {
                grhuffman[i].bit0 = br.ReadUInt16();
                grhuffman[i].bit1 = br.ReadUInt16();
            }
        }

        //
        // load the data offsets from ???head.ext
        //
        fname = $"{gheadname}{extension}";

        if (!File.Exists(fname))
        {
            CA_CannotOpen(fname);
            return;
        }

        using (FileStream fs = File.OpenRead(fname))
        using (BinaryReader br = new BinaryReader(fs))
        {
            long headersize = fs.Length;

            int expectedsize = grstarts.Length;

            if (!param_ignorenumchunks && headersize / 3 != expectedsize)
                Quit($@"Wolf4SDL was not compiled for these data files:
{fname} contains a wrong number of offsets ({headersize / 3} instead of {expectedsize})!
        
Please check whether you are using the right executable!        
(For mod developers: perhaps you forgot to update NUMCHUNKS?)");

            byte[] data = new byte[grstarts.Length * sizeof(int) * 3];
            data = br.ReadBytes(data.Length);

            for (int i = 0,  dOffs = 0; i < grstarts.Length; i++, dOffs += 3)
            {
                int val = data[0+dOffs] | (data[1+dOffs] << 8) | (data[2+dOffs] << 16);
                grstarts[i] = (val == 0x00FFFFFF ? -1 : val);
            }
        }

        //
        // Open the graphics file
        //
        fname = $"{gfilename}{extension}";

        if (!File.Exists(fname))
        {
            CA_CannotOpen(fname);
            return;
        }

        using (FileStream fs = File.OpenRead(fname))
        using (BinaryReader br = new BinaryReader(fs))
        {
            pictable = new pictabletype[NUMPICS];
            CAL_GetGrChunkLength(fs, br, STRUCTPIC);
            byte[] compseg = new byte[chunkcomplen];
            compseg = br.ReadBytes(chunkcomplen);
            var dest = CAL_HuffExpand(compseg, NUMPICS * sizeof(ushort)*2, grhuffman);
            pictable = StructHelpers.BytesToStructArray<pictabletype>(dest);

            CA_CacheGrChunks(fs, br);
        }
    }

    internal static void CAL_GetGrChunkLength(FileStream fs, BinaryReader br, int chunk)
    {
        fs.Seek(GRFILEPOS(chunk), SeekOrigin.Begin);
        chunkexplen = br.ReadInt32();
        chunkcomplen = GRFILEPOS(chunk + 1) - GRFILEPOS(chunk) - 4;
    }

    static int GRFILEPOS(int idx)
    {
        Debug.Assert(idx < grstarts.Length);
        return grstarts[idx];
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
            Quit($"Unable to open {fname}. Data empty.");
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

        if (oldsoundmode != (sbyte)SDMode.Off)
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

        oldsoundmode = (sbyte)SoundMode;

        switch ((SDMode)SoundMode)
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

    internal static byte[] CAL_HuffExpand(byte[] source, int length, huffnode[] hufftable)
    {
        if (length == 0 || source.Length == 0)
        {
            Quit("CAL_HuffExpand: length or dest is null!");
            return [];
        }

        byte[] dest = new byte[length];

        var headptr = 254; // head node is always node 254

        int written = 0;

        var end = length;

        var sourceIndex = 0;
        var destIndex = 0;

        byte val = source[sourceIndex++];

        byte mask = 1;

        ushort nodeval;
        var huffptr = headptr;
        while (true)
        {
            if ((val & mask) == 0)
                nodeval = hufftable[huffptr].bit0;
            else
                nodeval = hufftable[huffptr].bit1;

            if (mask == 0x80)
            {
                val = source[sourceIndex++];
                mask = 1;
            }
            else
                mask <<= 1;

            if (nodeval < 256)
            {
                dest[destIndex++] = (byte)nodeval;
                written++;
                huffptr = headptr;
                if (destIndex >= end) break;
            }
            else
            {
                huffptr = (nodeval - 256);
            }
        }

        return dest;
    }

    internal static void CA_CacheGrChunks(FileStream fs, BinaryReader br)
    {
        int pos, compressed;
        byte[] bufferseg;
        int[] source;
        int chunk, next;
        int sourceIndex = 0;

        for (chunk = STRUCTPIC + 1; chunk < NUMCHUNKS; chunk++)
        {
            if (grsegs[chunk]?.Length > 0)
                continue; // already in memory

            //
            // load the chunk info a buffer
            //
            pos = GRFILEPOS(chunk);

            if (pos < 0) // $FFFFFFFF start is a sparse tile
                continue;

            next = chunk + 1;

            while (GRFILEPOS(next) == -1) // skip past any sparse tiles
                next++;

            compressed = GRFILEPOS(next) - pos;

            fs.Seek(pos, SeekOrigin.Begin);
            bufferseg = new byte[compressed];
            //sourceIndex = buffersegIndex; // Or just set index = 0;

            for (int i = 0; i < bufferseg.Length; i++)
            {
                bufferseg[i] = br.ReadByte();
            }

            CAL_ExpandGrChunk(chunk, bufferseg);

            if (chunk >= STARTPICS && chunk < STARTEXTERNS)
                CAL_DeplaneGrChunk(chunk);
        }
    }

    internal static void CAL_ExpandGrChunk(int chunk, byte[] source)
    {
        int expanded;
        var sourceIndex = 0;

        if (chunk >= STARTTILE8 && chunk < STARTEXTERNS)
        {
            //
            // expanded sizes of tile8/16/32 are implicit
            //
            const int BLOCK = 64;
            const int MASKBLOCK = 128;

            if (chunk < STARTTILE8M)          // tile 8s are all in one chunk!
                expanded = BLOCK * NUMTILE8;
            else if (chunk < STARTTILE16)
                expanded = MASKBLOCK * NUMTILE8M;
            else if (chunk < STARTTILE16M)    // all other tiles are one/chunk
                expanded = BLOCK * 4;
            else if (chunk < STARTTILE32)
                expanded = MASKBLOCK * 4;
            else if (chunk < STARTTILE32M)
                expanded = BLOCK * 16;
            else
                expanded = MASKBLOCK * 16;
        }
        else
        {
            //
            // everything else has an explicit size longword
            //
            expanded = BitConverter.ToInt32(source, sourceIndex);
            sourceIndex += sizeof(int);
        }

        //
        // allocate final space and decompress it
        //
        grsegs[chunk] = new byte[expanded];
        grsegs[chunk] = CAL_HuffExpand (source.Skip(sourceIndex).ToArray(), expanded, grhuffman);
    }

    internal static void CAL_DeplaneGrChunk(int chunk)
    {
        int i;
        short width, height;

        if (chunk == STARTTILE8)
        {
            width = height = 8;
            for (i = 0; i < NUMTILE8; i++)
            {
                var offset = i * (width * height);
                var dest = VL_DePlaneVGA(grsegs[chunk].Skip(offset).ToArray(), width, height);
                Buffer.BlockCopy(dest, 0, grsegs[chunk], offset, width * height);
            }
        }
        else
        {
            width = pictable[chunk - STARTPICS].width;
            height = pictable[chunk - STARTPICS].height;

            grsegs[chunk] = VL_DePlaneVGA(grsegs[chunk], width, height);
        }
    }

    internal static void CA_CacheMap(int mapnum)
    {
        int pos, compressed;
        if (mapheaderseg[mapnum].width != MAPSIZE || mapheaderseg[mapnum].height != MAPSIZE)
            Quit($"CA_CacheMap: Map not {MAPSIZE}*{MAPSIZE}!");

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
            Quit($"Error writing file {filename}: {ioEx.Message}");
            return;
        }
    }

    internal static void CA_LoadFile(string filename, ref byte[] data) { throw new NotImplementedException(); }

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

        sound.data = br.ReadBytes(size - ORIG_ADLIBSOUND_SIZE + 1).Select(x => (sbyte)x).ToArray();

        audiosegs[chunk] = sound;// new byte[0];// sound; // TODO: This could be of various class types, may need to do this differently
    }
}
