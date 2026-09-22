using System.Diagnostics;
using Wolf3D.Assets;

namespace Wolf3D.Loaders;

internal class Wolf3dVgaFileLoader
{
    private struct pictabletype
    {
        public short width;
        public short height;
    }

    private struct huffnode
    {
        public ushort bit0, bit1; // 0-255 is a character, > is a pointer to a node
    }

    // Structural layout of every VGAGRAPH file: STRUCTPIC is always the first chunk,
    // and the font chunks always start immediately after it.
    private const int StructPic = 0;
    private const int StartFont = 1;

    private int[] grstarts = null!;
    private byte[][] grsegs = null!;
    private huffnode[] grhuffman = new huffnode[255];
    private pictabletype[] pictable = null!;

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
    public Wolf3dVgaFileLoader(string vgaHeadFile, string vgaGraphFile, string vgaDictFile, string extension, int numFonts)
    {
        this.numFonts = numFonts;

        var fname = $"{vgaDictFile}.{extension}";
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
        fname = $"{vgaHeadFile}.{extension}";

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
        fname = $"{vgaGraphFile}.{extension}";

        if (!File.Exists(fname))
        {
            throw new PfWolfGraphicException("Cannot open file: {0}. File does not exist.", fname);
        }

        using (FileStream fs = File.OpenRead(fname))
        using (BinaryReader br = new BinaryReader(fs))
        {
            CAL_GetGrChunkLength(fs, br, StructPic);
            byte[] compseg = br.ReadBytes(chunkcomplen);

            // The STRUCTPIC chunk's decompressed length (chunkexplen) tells us exactly how
            // many pics the file describes, so NUMPICS never needs to be hard-coded.
            numPics = chunkexplen / (sizeof(ushort) * 2);
            var dest = CAL_HuffExpand(compseg, chunkexplen, grhuffman);
            pictable = StructHelpers.BytesToStructArray<pictabletype>(dest);

            CA_CacheGrChunks(fs, br);
        }
    }
    private void CAL_GetGrChunkLength(FileStream fs, BinaryReader br, int chunk)
    {
        fs.Seek(GRFILEPOS(chunk), SeekOrigin.Begin);
        chunkexplen = br.ReadInt32();
        chunkcomplen = GRFILEPOS(chunk + 1) - GRFILEPOS(chunk) - 4;
    }

    private int GRFILEPOS(int idx)
    {
        Debug.Assert(idx < grstarts.Length);
        return grstarts[idx];
    }

    internal void CA_CacheGrChunks(FileStream fs, BinaryReader br)
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

            if (chunk >= StartPics && chunk < StartExterns)
                CAL_DeplaneGrChunk(chunk);
        }
    }

    internal void CAL_ExpandGrChunk(int chunk, byte[] source)
    {
        if (chunk == StartTile8)
        {
            // The tile8 chunk has no explicit length prefix, so its decompressed size can't be
            // read up front. Instead, decode until the compressed bitstream itself runs out;
            // whatever came out is the real data, packed as fixed 8x8 (64-byte) tiles.
            const int BLOCK = 64;

            var tileData = CAL_HuffExpandUntilSourceExhausted(source, grhuffman);
            numTile8 = tileData.Length / BLOCK;

            if (numTile8 * BLOCK != tileData.Length)
                throw new PfWolfGraphicException($"Tile8 chunk decompressed to {tileData.Length} bytes, which is not a whole number of {BLOCK}-byte tiles.");

            grsegs[chunk] = tileData;
            return;
        }

        //
        // everything else has an explicit size longword
        //
        var expanded = BitConverter.ToInt32(source, 0);
        grsegs[chunk] = CAL_HuffExpand(source.Skip(sizeof(int)).ToArray(), expanded, grhuffman);
    }

    private void CAL_DeplaneGrChunk(int chunk)
    {
        int i;
        short width, height;

        if (chunk == StartTile8)
        {
            width = height = 8;
            for (i = 0; i < numTile8; i++)
            {
                var offset = i * (width * height);
                var dest = VL_DePlaneVGA(grsegs[chunk].Skip(offset).ToArray(), width, height);
                Buffer.BlockCopy(dest, 0, grsegs[chunk], offset, width * height);
            }
        }
        else
        {
            width = pictable[chunk - StartPics].width;
            height = pictable[chunk - StartPics].height;

            grsegs[chunk] = VL_DePlaneVGA(grsegs[chunk], width, height);
        }
    }

    private static byte[] VL_DePlaneVGA(byte[] source, int width, int height)
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

    private static byte[] CAL_HuffExpand(byte[] source, int length, huffnode[] hufftable)
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

    // Same Huffman walk as CAL_HuffExpand, but for the one chunk (tile8) with no explicit
    // decompressed length: decode symbols until the compressed bitstream itself runs out,
    // discarding whatever partial symbol was in progress when the input ended.
    private static byte[] CAL_HuffExpandUntilSourceExhausted(byte[] source, huffnode[] hufftable)
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

    public Dictionary<string, Asset> GetAssets(List<string> dataMap)
    {
        var assets = new Dictionary<string, Asset>();
        int i = 1; // skip STRUCTPIC
        // fonts
        for (int h = 0; h < numFonts; h++, i++)
        {
            var data = grsegs[StartFont + h];
            var asset = new FontAsset(data);
            assets[dataMap[i].ToLowerInvariant()] = asset;
        }

        // graphic chunks
        for (int j = 0; j < numPics; j++,i++)
        {
            var data = grsegs[StartPics + j];
            var asset = new GraphicAsset(data, pictable[j].width, pictable[j].height);
            assets[dataMap[i].ToLowerInvariant()] = asset;
        }

        // Tile8
        var tile8Data = grsegs[StartTile8];
        var tile8Asset = new Tile8Asset(tile8Data);
        assets[dataMap[i].ToLowerInvariant()] = tile8Asset;
        i++;

        // Screens
        i += 2; // skip for now

        // Extern "text"
        var helpTextData = grsegs[i];
        var helpTextAsset = new TextAsset(helpTextData);
        assets[dataMap[i].ToLowerInvariant()] = helpTextAsset;
        i++;

        // Extern Demos
        for (int k = 0; k < 4; k++, i++) {
            var data = grsegs[i];
            var asset = new DemoAsset(data);
            assets[dataMap[i].ToLowerInvariant()] = asset;
        }

        // Extern "text" again
        for (int l = 0; l < 6; l++, i++)
        {
            var data = grsegs[i];
            var asset = new TextAsset(helpTextData);
            assets[dataMap[i].ToLowerInvariant()] = asset;
        }
            return assets;
    }
}
