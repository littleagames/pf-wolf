using System.Diagnostics;
using PFWolf.Assets;

namespace PFWolf.Loaders;

public class Wolf3dVgaFileLoader
{
    private struct PicTableEntry
    {
        public short width;
        public short height;
    }

    private struct HuffNode
    {
        public ushort bit0, bit1; // 0-255 is a character, > is a pointer to a node
    }

    // Structural layout of every VGAGRAPH file: STRUCTPIC is always the first chunk,
    // and the font chunks always start immediately after it.
    private const int StructPic = 0;
    private const int StartFont = 1;

    private int[] grstarts = null!;
    private byte[][] grsegs = null!;
    private HuffNode[] grhuffman = new HuffNode[255];
    private PicTableEntry[] pictable = null!;

    private int chunkcomplen, chunkexplen;
    private int numChunks;
    private int numPics;
    private readonly int numFonts;
    private int numTile8;

    // Everything past the pic table is derived from the counts above, not read from the file:
    // masked pics/sprites/tile16/tile32 are never populated by any known VGAGRAPH file, and
    // tile8s are always packed into a single chunk immediately after the pics.
    private int StartPics => StartFont + numFonts;
    private int StartTile8 => StartPics + numPics;
    private int StartExterns => StartTile8 + 1;

    /// <param name="numFonts">Number of font chunks in the file. Not derivable from the file itself: a font chunk's decompressed
    /// size includes its variable-length glyph bitmap data, so there's no fixed size or other signature that reliably
    /// distinguishes it from a pic. For WL6/WL1/SOD this is 2.</param>
    public Wolf3dVgaFileLoader(string vgaHeadFile, string vgaGraphFile, string vgaDictFile, int numFonts)
    {
        this.numFonts = numFonts;

        var fname = vgaDictFile;
        if (!File.Exists(fname))
        {
            throw new PfWolfGraphicException("Cannot open file: {0}. File does not exist.", fname);
        }

        using (FileStream fs = File.OpenRead(fname))
        using (BinaryReader br = new BinaryReader(fs))
        {
            for (int i = 0; i < 255; i++)
            {
                grhuffman[i].bit0 = br.ReadUInt16();
                grhuffman[i].bit1 = br.ReadUInt16();
            }
        }

        //
        // load the data offsets from ???head.ext
        //
        fname = vgaHeadFile;

        if (!File.Exists(fname))
        {
            throw new PfWolfGraphicException("Cannot open file: {0}. File does not exist.", fname);
        }

        using (FileStream fs = File.OpenRead(fname))
        using (BinaryReader br = new BinaryReader(fs))
        {
            long headersize = fs.Length;

            // Each chunk offset is a 3-byte value; the head file holds one entry per chunk
            // plus a trailing sentinel offset used to compute the last chunk's length.
            if (headersize % 3 != 0)
                throw new PfWolfGraphicException($"Cannot open file: {fname}. File size ({headersize}) is not a multiple of 3.");

            int offsetCount = (int)(headersize / 3);
            numChunks = offsetCount - 1;
            grstarts = new int[offsetCount];
            grsegs = new byte[numChunks][];

            byte[] data = br.ReadBytes(offsetCount * 3);

            for (int i = 0, dOffs = 0; i < grstarts.Length; i++, dOffs += 3)
            {
                int val = data[0 + dOffs] | (data[1 + dOffs] << 8) | (data[2 + dOffs] << 16);
                grstarts[i] = (val == 0x00FFFFFF ? -1 : val);
            }
        }

        //
        // Open the graphics file
        //
        fname = vgaGraphFile;

        if (!File.Exists(fname))
        {
            throw new PfWolfGraphicException("Cannot open file: {0}. File does not exist.", fname);
        }

        using (FileStream fs = File.OpenRead(fname))
        using (BinaryReader br = new BinaryReader(fs))
        {
            GetGrChunkLength(fs, br, StructPic);
            byte[] compseg = br.ReadBytes(chunkcomplen);

            // The STRUCTPIC chunk's decompressed length (chunkexplen) tells us exactly how
            // many pics the file describes, so NUMPICS never needs to be hard-coded.
            numPics = chunkexplen / (sizeof(ushort) * 2);
            var dest = HuffExpand(compseg, chunkexplen, grhuffman);
            pictable = StructHelpers.BytesToStructArray<PicTableEntry>(dest);

            CacheGraphicChunks(fs, br);
        }
    }
    private void GetGrChunkLength(FileStream fs, BinaryReader br, int chunk)
    {
        fs.Seek(GrFilePos(chunk), SeekOrigin.Begin);
        chunkexplen = br.ReadInt32();
        chunkcomplen = GrFilePos(chunk + 1) - GrFilePos(chunk) - 4;
    }

    private int GrFilePos(int idx)
    {
        Debug.Assert(idx < grstarts.Length);
        return grstarts[idx];
    }

    internal void CacheGraphicChunks(FileStream fs, BinaryReader br)
    {
        int pos, compressed;
        byte[] bufferseg;
        int[] source;
        int chunk, next;
        int sourceIndex = 0;

        for (chunk = StructPic + 1; chunk < numChunks; chunk++)
        {
            if (grsegs[chunk]?.Length > 0)
                continue; // already in memory

            //
            // load the chunk info a buffer
            //
            pos = GrFilePos(chunk);

            if (pos < 0) // $FFFFFFFF start is a sparse tile
                continue;

            next = chunk + 1;

            while (GrFilePos(next) == -1) // skip past any sparse tiles
                next++;

            compressed = GrFilePos(next) - pos;

            fs.Seek(pos, SeekOrigin.Begin);
            bufferseg = new byte[compressed];
            //sourceIndex = buffersegIndex; // Or just set index = 0;

            for (int i = 0; i < bufferseg.Length; i++)
            {
                bufferseg[i] = br.ReadByte();
            }

            ExpandGrChunk(chunk, bufferseg);

            if (chunk >= StartPics && chunk < StartExterns)
                DeplaneGrChunk(chunk);
        }
    }

    internal void ExpandGrChunk(int chunk, byte[] source)
    {
        if (chunk == StartTile8)
        {
            // The tile8 chunk has no explicit length prefix, so its decompressed size can't be
            // read up front. Instead, decode until the compressed bitstream itself runs out;
            // whatever came out is the real data, packed as fixed 8x8 (64-byte) tiles.
            const int BLOCK = 64;

            // The last byte's unused bits (up to 7) can decode as a few stray symbols past the
            // tiles (Blake Stone's 35 tiles come out 3 bytes long), so those are dropped.
            var tileData = HuffExpandUntilSourceExhausted(source, grhuffman);
            numTile8 = tileData.Length / BLOCK;

            if (tileData.Length - numTile8 * BLOCK >= 8)
                throw new PfWolfGraphicException($"Tile8 chunk decompressed to {tileData.Length} bytes, which is not a whole number of {BLOCK}-byte tiles.");

            grsegs[chunk] = tileData.Length == numTile8 * BLOCK ? tileData : tileData[..(numTile8 * BLOCK)];
            return;
        }

        //
        // everything else has an explicit size longword
        //
        var expanded = BitConverter.ToInt32(source, 0);
        grsegs[chunk] = HuffExpand(source.Skip(sizeof(int)).ToArray(), expanded, grhuffman);
    }

    private void DeplaneGrChunk(int chunk)
    {
        int i;
        short width, height;

        if (chunk == StartTile8)
        {
            width = height = 8;
            for (i = 0; i < numTile8; i++)
            {
                var offset = i * (width * height);
                var dest = DeplaneVga(grsegs[chunk].Skip(offset).ToArray(), width, height);
                Buffer.BlockCopy(dest, 0, grsegs[chunk], offset, width * height);
            }
        }
        else
        {
            width = pictable[chunk - StartPics].width;
            height = pictable[chunk - StartPics].height;

            grsegs[chunk] = DeplaneVga(grsegs[chunk], width, height);
        }
    }

    private static byte[] DeplaneVga(byte[] source, int width, int height)
    {
        int x, y, plane;
        ushort size, pwidth;

        size = (ushort)(width * height);

        if ((width & 3) != 0)
        {
            throw new PfWolfGraphicException("DePlaneVGA: width not divisible by 4!");
            //return source;
        }

        var temp = new byte[size];

        //
        // munge pic into the temp buffer
        //

        var srcline = 0;
        pwidth = (ushort)(width >> 2);

        for (plane = 0; plane < 4; plane++)
        {
            var destIndex = 0;
            for (y = 0; y < height; y++)
            {
                for (x = 0; x < pwidth; x++)
                    temp[destIndex + ((x << 2) + plane)] = source[srcline++];

                destIndex += width;
            }
        }

        //
        // copy the temp buffer back into the original source
        //
        return temp;
        //Array.Copy(temp, source, size);
    }

    private static byte[] HuffExpand(byte[] source, int length, HuffNode[] hufftable)
    {
        if (length == 0 || source.Length == 0)
        {
            throw new PfWolfGraphicException("CAL_HuffExpand: length or dest is null!");
        }

        byte[] dest = new byte[length];

        var headptr = 254; // head node is always node 254

        int written = 0;

        var end = length;

        var sourceIndex = 0;
        var destIndex = 0;

        byte val = source[sourceIndex++];

        byte mask = 1;
        bool sourceEnded = false;

        ushort nodeval;
        var huffptr = headptr;
        while (true)
        {
            if (sourceEnded)
                throw new PfWolfGraphicException($"CAL_HuffExpand: compressed data ran out after {destIndex} of {length} bytes.");

            if ((val & mask) == 0)
                nodeval = hufftable[huffptr].bit0;
            else
                nodeval = hufftable[huffptr].bit1;

            if (mask == 0x80)
            {
                // The next byte is fetched before knowing whether this bit finished the last
                // symbol, so a chunk whose data ends exactly on a byte boundary (SOD's ENDSCR4
                // and ENDSCR9) would read one past its end. id's C code got away with that
                // over-read; only fetch when there's a byte left.
                if (sourceIndex < source.Length)
                    val = source[sourceIndex++];
                else
                    sourceEnded = true;
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

    // Same Huffman walk as HuffExpand, but for the one chunk (tile8) with no explicit
    // decompressed length: decode symbols until the compressed bitstream itself runs out,
    // discarding whatever partial symbol was in progress when the input ended.
    private static byte[] HuffExpandUntilSourceExhausted(byte[] source, HuffNode[] hufftable)
    {
        if (source.Length == 0)
        {
            throw new PfWolfGraphicException("CAL_HuffExpand: source is empty!");
        }

        var dest = new List<byte>();

        var headptr = 254; // head node is always node 254
        var sourceIndex = 0;

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
                if (sourceIndex >= source.Length)
                    break; // no more compressed bits left to resolve this symbol

                val = source[sourceIndex++];
                mask = 1;
            }
            else
                mask <<= 1;

            if (nodeval < 256)
            {
                dest.Add((byte)nodeval);
                huffptr = headptr;
            }
            else
            {
                huffptr = (nodeval - 256);
            }
        }

        return dest.ToArray();
    }

    /// <param name="warn">Told when the file has a different number of pics or externs than the names</param>
    public Dictionary<string, Asset> GetAssets(List<string> dataMap, Action<string>? warn = null)
    {
        var assets = new Dictionary<string, Asset>();

        // The map's names line up with the file's chunks only up to its pics: a file with pics
        // added or taken out (WDC, ChaosEdit) moves the tile8 and externs, so those go by where
        // the map's Tile8 is
        var mapTile8 = dataMap.FindIndex(name => name.Equals("Tile8", StringComparison.OrdinalIgnoreCase));
        var mapPics = mapTile8 >= 0 ? mapTile8 - StartPics : Math.Max(0, dataMap.Count - StartPics);
        var fontNames = new DataMapNames(dataMap, "fonts", "FONT");
        var picNames = new DataMapNames(dataMap.Skip(StartPics).Take(mapPics).ToList(), "pics", "PIC");
        var externNames = new DataMapNames(mapTile8 >= 0 ? dataMap[(mapTile8 + 1)..] : [], "externs", "EXTERN");

        // fonts
        for (int h = 0; h < numFonts; h++)
        {
            var data = grsegs[StartFont + h];
            if (data == null)
                continue;
            assets[fontNames.NameAt(StartFont + h, h).ToLowerInvariant()] = new FontAsset(data);
        }

        // graphic chunks
        for (int j = 0; j < numPics; j++)
        {
            var data = grsegs[StartPics + j];
            if (data == null)
                continue;
            assets[picNames.NameAt(j).ToLowerInvariant()] = new GraphicAsset(data, pictable[j].width, pictable[j].height);
        }

        // Tile8
        if (StartTile8 < numChunks && grsegs[StartTile8] != null)
            assets[(mapTile8 >= 0 ? dataMap[mapTile8] : "Tile8").ToLowerInvariant()] = new Tile8Asset(grsegs[StartTile8]);

        // Externs: which ones a release has, and in what order, differs (Wolf3D: screens, help text,
        // demos, end texts; Spear: screens, palettes, demos, end text), so go by their data map names:
        // Demo0-3 are demos, *PAL are palettes, and the rest is text. One the map has no name for
        // is read as text.
        for (int chunk = StartExterns, e = 0; chunk < numChunks; chunk++, e++)
        {
            var data = grsegs[chunk];
            if (data == null)
                continue;
            var name = externNames.NameAt(e);
            if (IsSkippedExtern(name))
                continue;

            try
            {
                if (name.StartsWith("Demo", StringComparison.OrdinalIgnoreCase))
                    assets[name.ToLowerInvariant()] = new DemoAsset(data);
                else if (name.EndsWith("PAL", StringComparison.OrdinalIgnoreCase))
                    assets[name.ToLowerInvariant()] = ToPalette(data);
                else
                    assets[name.ToLowerInvariant()] = new TextAsset(data);
            }
            catch (PfWolfGraphicException ex)
            {
                warn?.Invoke($"{name} can't be read, so it's left out: {ex.Message}");
            }
        }

        fontNames.Warn(warn);
        picNames.Warn(warn);
        externNames.Warn(warn);
        return assets;
    }

    /// <summary>
    /// Spear's palette chunks (TITLEPAL, END1PAL...): 256 Rgb triples of 6-bit VGA values (0-63)
    /// </summary>
    private static Palette ToPalette(byte[] data)
    {
        const int PaletteBytes = 256 * 3;
        if (data.Length != PaletteBytes)
            throw new PfWolfGraphicException($"Palette chunk is {data.Length} bytes, expected {PaletteBytes}.");

        var colors = new PaletteColor[256];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = new PaletteColor(To8Bit(data[i * 3]), To8Bit(data[i * 3 + 1]), To8Bit(data[i * 3 + 2]));

        return new Palette { Colors = colors };

        static byte To8Bit(byte vgaValue) => (byte)(vgaValue * 255 / 63);
    }

    /// <summary>
    /// Externs not loaded (yet): the order/error text screens
    /// </summary>
    private static bool IsSkippedExtern(string name)
    {
        return name.Equals("OrderScreen", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ErrorScreen", StringComparison.OrdinalIgnoreCase);
    }
}
