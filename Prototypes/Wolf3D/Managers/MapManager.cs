using Wolf3D.Managers;
using Wolf3D.Mappers;

namespace Wolf3D.Managers;

public class MapConstants
{
    public const int ICONARROWS = 90;
    public const int PUSHABLETILE = 98;
    public const int EXITTILE = 99;          // at end of castle
    public const int AREATILE = 107;         // first of NUMAREAS floor tiles
    public const int NUMAREAS = 37;
    public const int ELEVATORTILE = 21;
    public const int AMBUSHTILE = 106;
    public const int ALTELEVATORTILE = 107;
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
        planestart = new Int32[MapManager.MAPPLANES];
        planelength = new UInt16[MapManager.MAPPLANES];
        name = new char[16];
    }
}

internal class MapManager
{
    internal const int MAPSHIFT = 6;
    internal const int MAPSIZE = (1 << MAPSHIFT);
    internal const int MAPAREA = MAPSIZE * MAPSIZE;

    public const int NUMMAPS = 60;
    public const int MAPPLANES = 3;


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

    private UInt16[][] mapsegs = new ushort[MAPPLANES][];
    private maptype[] mapheaderseg = new maptype[NUMMAPS];

    private const string mheadname = "maphead.";
    private const string mfilename = "gamemaps."; // maptemp
    private mapfiletype tinf;

    public MapManager()
    {

    }

    public void Init(string extension)
    {
        mapHeadFileName = $"{mheadname}{extension}";
        mapDataFileName = $"{mfilename}{extension}";
        CAL_SetupMapFile();
    }

    private string mapHeadFileName;
    private string mapDataFileName;

    internal ushort mapwidth, mapheight;
    internal byte[,] tilemap;
    internal bool[,] spotvis;
    internal Actor?[,] actorat;

    public ushort GetTile(int x, int y, int plane)
    {
        return mapsegs[0][y * mapwidth + x];
    }

    public void LoadMap(string mapName)
    {
        var mapnum = MapInfoMappings.MapAssetToIndex[mapName];
        CacheMap(mapnum);
        var mapheader = mapheaderseg[mapnum];

        mapwidth = mapheader.width;
        mapheight = mapheader.height;

#if USE_FEATUREFLAGS
    const int MXX = MAPSIZE - 1;
    
    // Read feature flags data from map corners and overwrite corners with adjacent tiles
    ffDataTopLeft     = MAPSPOT(0,   0,   0); MAPSPOT(0,   0,   0) = MAPSPOT(1,       0,       0);
    ffDataTopRight    = MAPSPOT(MXX, 0,   0); MAPSPOT(MXX, 0,   0) = MAPSPOT(MXX,     1,       0);
    ffDataBottomRight = MAPSPOT(MXX, MXX, 0); MAPSPOT(MXX, MXX, 0) = MAPSPOT(MXX - 1, MXX,     0);
    ffDataBottomLeft  = MAPSPOT(0,   MXX, 0); MAPSPOT(0,   MXX, 0) = MAPSPOT(0,       MXX - 1, 0);
#endif

        tilemap = new byte[MAPSIZE, MAPSIZE]; // wall values only
        spotvis = new bool[MAPSIZE, MAPSIZE];
        actorat = new Actor?[MAPSIZE, MAPSIZE];

        for (int y = 0; y < mapheight; y++)
        {
            for (int x = 0; x < mapwidth; x++)
            {
                int tile = MAPSPOT(x, y, 0);
                if (tile < MapConstants.AMBUSHTILE)
                {
                    // solid wall
                    tilemap[x, y] = (byte)tile;
                    actorat[x, y] = new Wall(tile);// (uint)tile;
                }
                else
                {
                    // area floor
                    tilemap[x, y] = 0;
                    actorat[x, y] = null;
                }
            }
        }
    }

    internal static bool VALIDAREA(int x) => (x) >= MapConstants.AREATILE && (x) < (MapConstants.AREATILE + MapConstants.NUMAREAS);

    internal static bool ISPOINTER(Actor? check)
    {
        return check is objstruct;
    }

    private void CAL_SetupMapFile()
    {
        int i;
        int pos;

        //
        // load maphead.ext (offsets and tileinfo for map file)
        //
        if (!File.Exists(mapHeadFileName))
            throw new PfWolfMapException("Cannot open file: {0}. File does not exist.", mapHeadFileName);

        tinf = new mapfiletype();
        using (var fs = new FileStream(mapHeadFileName, FileMode.Open, FileAccess.Read))
        using (var br = new BinaryReader(fs))
        {
            tinf.RLEWtag = br.ReadUInt16();
            for (i = 0; i < NUMMAPS; i++)
                tinf.headeroffsets[i] = br.ReadInt32();
        }

        //
        // open the data file
        //

        if (!File.Exists(mapDataFileName))
            throw new PfWolfMapException("Cannot open file: {0}. File does not exist.", mapDataFileName);

        using (var fs = new FileStream(mapDataFileName, FileMode.Open, FileAccess.Read))
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

    internal void CacheMap(int mapnum)
    {
        int pos, compressed;
        if (mapheaderseg[mapnum].width != MAPSIZE || mapheaderseg[mapnum].height != MAPSIZE)
            throw new PfWolfMapException($"CA_CacheMap: Map not {MAPSIZE}*{MAPSIZE}!");

        if (!File.Exists(mapDataFileName))
            throw new PfWolfMapException("Cannot open file: {0}. File does not exist.", mapDataFileName);

        //
        // load the planes into the allready allocated buffers
        //
        var size = MAPAREA * sizeof(ushort);
        using (FileStream fs = File.OpenRead(mapDataFileName))
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
                var buffer2seg = new ushort[expanded / sizeof(ushort)]; // might be byte[expanded]
                CAL_CarmackExpand(bufferseg.Skip(sizeof(ushort)).ToArray(), buffer2seg, expanded);
                CA_RLEWexpand(buffer2seg.Skip(1).ToArray(), out ushort[] dest, size, tinf.RLEWtag);
                mapsegs[plane] = dest;
            }
        }
    }

    internal const ushort NEARTAG = 0xa7;
    internal const ushort FARTAG = 0xa8;
    private readonly GameEngineManager gameEngineManager;

    internal void CAL_CarmackExpand(byte[] source, ushort[] dest, int length)
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

    internal void CA_RLEWexpand(ushort[] source, out ushort[] dest, int length, ushort rlewtag)
    {
        ushort value, count, i;
        dest = new ushort[length / 2];

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

    internal int MAPSPOT(int x, int y, int plane) => (mapsegs[(plane)][((y) << MAPSHIFT) + (x)]);
    internal void SetMapSpot(int x, int y, int plane, ushort value)
    {
        (mapsegs[(plane)][((y) << MAPSHIFT) + (x)]) = value;
    }
}
