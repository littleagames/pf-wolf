using Wolf3D.Assets;
using Wolf3D.Managers;

namespace Wolf3D.Loaders;

/// <summary>
/// A GAMEMAPS/MAPHEAD pair: the game's own, or a mod's under maps/ in a pk3. MAPHEAD is the
/// RLEW tag, then each level's offset in GAMEMAPS (up to 100; 0 or -1 for no level). Each
/// level there starts with its planes' offsets and lengths, its size and a 16-character name,
/// and its planes are Carmack- then RLEW-compressed.
/// </summary>
internal class Wolf3dMapFileLoader
{
    private readonly ushort _rlewTag;
    // Null where MAPHEAD has no level
    private readonly maptype?[] _levels;
    private readonly byte[] _gameMaps;

    internal const ushort NEARTAG = 0xa7;
    internal const ushort FARTAG = 0xa8;

    // MAPHEAD's offset table holds no more than this
    private const int MaxLevels = 100;

    /// <summary>The game's own pair, from the working folder</summary>
    public static Wolf3dMapFileLoader FromFiles(string mapHeaderFile, string mapDataFile)
    {
        foreach (var file in new[] { mapHeaderFile, mapDataFile })
        {
            if (!File.Exists(file))
                throw new PfWolfMapException("Cannot open file: {0}. File does not exist.", file);
        }

        return new Wolf3dMapFileLoader(File.ReadAllBytes(mapHeaderFile), File.ReadAllBytes(mapDataFile));
    }

    public Wolf3dMapFileLoader(byte[] mapHead, byte[] gameMaps)
    {
        _gameMaps = gameMaps;
        if (mapHead.Length < sizeof(ushort))
            throw new InvalidDataException("MAPHEAD is too short");

        _rlewTag = BitConverter.ToUInt16(mapHead, 0);
        _levels = new maptype?[Math.Min(MaxLevels, (mapHead.Length - sizeof(ushort)) / sizeof(int))];

        using var br = new BinaryReader(new MemoryStream(gameMaps));
        for (int i = 0; i < _levels.Length; i++)
        {
            var pos = BitConverter.ToInt32(mapHead, sizeof(ushort) + i * sizeof(int));
            if (pos <= 0)                           // $FFFFFFFF start is a sparse map; 0 is none
                continue;
            if (pos > gameMaps.Length - (MapManager.MAPPLANES * 6 + 4 + 16))
                throw new InvalidDataException($"MAPHEAD puts level {i + 1} past the end of GAMEMAPS");

            var level = new maptype();
            br.BaseStream.Seek(pos, SeekOrigin.Begin);
            for (int p = 0; p < MapManager.MAPPLANES; p++)
                level.planestart[p] = br.ReadInt32();
            for (int p = 0; p < MapManager.MAPPLANES; p++)
                level.planelength[p] = br.ReadUInt16();
            level.width = br.ReadUInt16();
            level.height = br.ReadUInt16();
            for (int n = 0; n < 16; n++)
                level.name[n] = (char)br.ReadByte();
            _levels[i] = level;
        }
    }

    /// <summary>
    /// The levels, named by <paramref name="dataMap"/> in order (raw-data-map's maps). A level
    /// the pair doesn't have is left out, and so is one that can't be read (with a warning).
    /// </summary>
    public Dictionary<string, Asset> GetAssets(List<string> dataMap, Action<string>? warn = null)
    {
        var assets = new Dictionary<string, Asset>();
        for (int i = 0; i < dataMap.Count && i < _levels.Length; i++)
        {
            if (_levels[i] == null)
                continue;

            try
            {
                assets[dataMap[i].ToLowerInvariant()] = CacheMap(i);
            }
            catch (Exception e) when (e is PfWolfMapException or InvalidDataException or IndexOutOfRangeException or ArgumentException)
            {
                warn?.Invoke($"{dataMap[i]} can't be read, so it's left out: {e.Message}");
            }
        }

        return assets;
    }

    public MapAsset CacheMap(int mapnum)
    {
        int pos, compressed;
        var level = _levels[mapnum] ?? throw new PfWolfMapException($"There's no level {mapnum + 1}");
        if (level.width != MapManager.MAPSIZE || level.height != MapManager.MAPSIZE)
            throw new PfWolfMapException($"CA_CacheMap: Map not {MapManager.MAPSIZE}*{MapManager.MAPSIZE}!");

        //
        // load the planes into the allready allocated buffers
        //
        var size = MapManager.MAPAREA * sizeof(ushort);

        // GAMEMAPS has three planes; the height and tag planes after them are left empty
        UInt16[][] mapsegs = new ushort[MapManager.LEVELPLANES][];
        for (var plane = 0; plane < MapManager.LEVELPLANES; plane++)
            mapsegs[plane] = new ushort[MapManager.MAPAREA];

        using (var fs = new MemoryStream(_gameMaps))
        using (BinaryReader br = new BinaryReader(fs))
        {
            for (var plane = 0; plane < MapManager.MAPPLANES; plane++)
            {

                pos = level.planestart[plane];
                compressed = level.planelength[plane];

                if (compressed == 0)
                    continue; // empty plane

                if (pos < 0 || pos + compressed > _gameMaps.Length)
                    throw new InvalidDataException($"its plane {plane} runs past the end of GAMEMAPS");
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
                CA_RLEWexpand(buffer2seg.Skip(1).ToArray(), out ushort[] dest, size, _rlewTag);
                mapsegs[plane] = dest;
            }

            return new MapAsset() // TODO: Add raw data for the entire map's data like a single MAP file, which means I'll have to come up with the format here.
            {
                Width = level.width,
                Height = level.height,
                Name = new string(level.name),
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
