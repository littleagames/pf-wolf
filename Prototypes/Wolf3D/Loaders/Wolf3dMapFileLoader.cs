using Wolf3D.Assets;
using Wolf3D.Managers;
using Wolf3D.Mappers;

namespace Wolf3D.Loaders;

internal class Wolf3dMapFileLoader
{
    struct mapfiletype
    {
        public UInt16 RLEWtag;
        //public UInt16 numplanes; // If >= 4
        public Int32[] headeroffsets;

        public mapfiletype()
        {
            headeroffsets = new Int32[MapManager.NUMMAPS]; // TODO: This will be dynamic with the file
        }
    }
    private mapfiletype tinf;

    private maptype[] mapheaderseg = new maptype[MapManager.NUMMAPS];

    internal const ushort NEARTAG = 0xa7;
    internal const ushort FARTAG = 0xa8;

    private string mapHeadFileName;
    private string mapDataFileName;

    public Wolf3dMapFileLoader(string mapHeaderFile, string mapDataFile, string extension)
    {
        mapHeadFileName = $"{mapHeaderFile}.{extension}";
        mapDataFileName = $"{mapDataFile}.{extension}";
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
            for (i = 0; i < MapManager.NUMMAPS; i++)
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

            for (i = 0; i < MapManager.NUMMAPS; i++)
            {
                pos = tinf.headeroffsets[i];
                if (pos < 0)                          // $FFFFFFFF start is a sparse map
                    continue;

                mapheaderseg[i] = new maptype();

                fs.Seek(pos, SeekOrigin.Begin);
                for (int p = 0; p < MapManager.MAPPLANES; p++)
                {
                    mapheaderseg[i].planestart[p] = br.ReadInt32();
                }
                for (int p = 0; p < MapManager.MAPPLANES; p++)
                {
                    mapheaderseg[i].planelength[p] = br.ReadUInt16();
                }
                mapheaderseg[i].width = br.ReadUInt16();
                mapheaderseg[i].height = br.ReadUInt16();
                for (int n = 0; n < 16; n++)
                    mapheaderseg[i].name[n] = (char)br.ReadByte();
            }
        }
    }

    public Dictionary<string, Asset> GetAssets(List<string> dataMap)
    {
        var assets = new Dictionary<string, Asset>();
        for(int i = 0; i < dataMap.Count; i++)
        {
            assets.Add(dataMap[i].ToLowerInvariant(), CacheMap(i));
        }

        return assets;
    }

    public MapAsset CacheMap(int mapnum)
    {
        int pos, compressed;
        if (mapheaderseg[mapnum].width != MapManager.MAPSIZE || mapheaderseg[mapnum].height != MapManager.MAPSIZE)
            throw new PfWolfMapException($"CA_CacheMap: Map not {MapManager.MAPSIZE}*{MapManager.MAPSIZE}!");

        if (!File.Exists(mapDataFileName))
            throw new PfWolfMapException("Cannot open file: {0}. File does not exist.", mapDataFileName);

        //
        // load the planes into the allready allocated buffers
        //
        var size = MapManager.MAPAREA * sizeof(ushort);

        UInt16[][] mapsegs = new ushort[MapManager.MAPPLANES][];

        using (FileStream fs = File.OpenRead(mapDataFileName))
        using (BinaryReader br = new BinaryReader(fs))
        {
            for (var plane = 0; plane < MapManager.MAPPLANES; plane++)
            {
                // allocate
                mapsegs[plane] = new ushort[MapManager.MAPAREA];

                pos = mapheaderseg[mapnum].planestart[plane];
                compressed = mapheaderseg[mapnum].planelength[plane];

                if (compressed == 0)
                    continue; // empty plane

                fs.Seek(pos, SeekOrigin.Begin);

                //var bufferseg = new byte[compressed];
                //for (int i = 0; i < bufferseg.Length; i++)
                //{
                //    bufferseg[i] = br.ReadByte();
                //}

                var bufferseg = br.ReadBytes(compressed);

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

            return new MapAsset() // TODO: Add raw data for the entire map's data like a single MAP file, which means I'll have to come up with the format here.
            {
                Width = mapheaderseg[mapnum].width,
                Height = mapheaderseg[mapnum].height,
                Name = new string(mapheaderseg[mapnum].name),
                MapData = mapsegs
            };
        }
    }

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
}
