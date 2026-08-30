using System.Diagnostics;
using Wolf3D.Assets;
using Wolf3D.Mappers;

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

    private int[] grstarts = new int[GraphicConstants.NUMCHUNKS + 1];
    private byte[][] grsegs = new byte[GraphicConstants.NUMCHUNKS][];
    private huffnode[] grhuffman = new huffnode[255];
    private pictabletype[] pictable;

    private int chunkcomplen, chunkexplen;

    public Wolf3dVgaFileLoader(string vgaHeadFile, string vgaGraphFile, string vgaDictFile, string extension)
    {
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

            int expectedsize = grstarts.Length;

            if (headersize / 3 != expectedsize)
                throw new PfWolfGraphicException($@"Wolf4SDL was not compiled for these data files:
{fname} contains a wrong number of offsets ({headersize / 3} instead of {expectedsize})!
        
Please check whether you are using the right executable!        
(For mod developers: perhaps you forgot to update NUMCHUNKS?)");

            byte[] data = new byte[grstarts.Length * sizeof(int) * 3];
            data = br.ReadBytes(data.Length);

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
            pictable = new pictabletype[GraphicConstants.NUMPICS];
            CAL_GetGrChunkLength(fs, br, GraphicConstants.STRUCTPIC);
            byte[] compseg = new byte[chunkcomplen];
            compseg = br.ReadBytes(chunkcomplen);
            var dest = CAL_HuffExpand(compseg, GraphicConstants.NUMPICS * sizeof(ushort) * 2, grhuffman);
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

        for (chunk = GraphicConstants.STRUCTPIC + 1; chunk < GraphicConstants.NUMCHUNKS; chunk++)
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

            if (chunk >= GraphicConstants.STARTPICS && chunk < GraphicConstants.STARTEXTERNS)
                CAL_DeplaneGrChunk(chunk);
        }
    }

    internal void CAL_ExpandGrChunk(int chunk, byte[] source)
    {
        int expanded;
        var sourceIndex = 0;

        if (chunk >= GraphicConstants.STARTTILE8 && chunk < GraphicConstants.STARTEXTERNS)
        {
            //
            // expanded sizes of tile8/16/32 are implicit
            //
            const int BLOCK = 64;
            const int MASKBLOCK = 128;

            if (chunk < GraphicConstants.STARTTILE8M)          // tile 8s are all in one chunk!
                expanded = BLOCK * GraphicConstants.NUMTILE8;
            else if (chunk < GraphicConstants.STARTTILE16)
                expanded = MASKBLOCK * GraphicConstants.NUMTILE8M;
            else if (chunk < GraphicConstants.STARTTILE16M)    // all other tiles are one/chunk
                expanded = BLOCK * 4;
            else if (chunk < GraphicConstants.STARTTILE32)
                expanded = MASKBLOCK * 4;
            else if (chunk < GraphicConstants.STARTTILE32M)
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
        grsegs[chunk] = CAL_HuffExpand(source.Skip(sourceIndex).ToArray(), expanded, grhuffman);
    }

    private void CAL_DeplaneGrChunk(int chunk)
    {
        int i;
        short width, height;

        if (chunk == GraphicConstants.STARTTILE8)
        {
            width = height = 8;
            for (i = 0; i < GraphicConstants.NUMTILE8; i++)
            {
                var offset = i * (width * height);
                var dest = VL_DePlaneVGA(grsegs[chunk].Skip(offset).ToArray(), width, height);
                Buffer.BlockCopy(dest, 0, grsegs[chunk], offset, width * height);
            }
        }
        else
        {
            width = pictable[chunk - GraphicConstants.STARTPICS].width;
            height = pictable[chunk - GraphicConstants.STARTPICS].height;

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

    public Dictionary<string, Asset> GetAssets(List<string> dataMap)
    {
        var assets = new Dictionary<string, Asset>();
        int i = 1; // skip STRUCTPIC
        // fonts
        for (int h = 0; h < GraphicConstants.NUMFONT; h++, i++)
        {
            var data = grsegs[GraphicConstants.STARTFONT + h];
            var asset = new FontAsset(data);
            assets[dataMap[i].ToLowerInvariant()] = asset;
        }

        // graphic chunks
        for (int j = 0; j < GraphicConstants.NUMPICS; j++,i++)
        {
            var data = grsegs[GraphicConstants.STARTPICS + j];
            var asset = new GraphicAsset(data, pictable[j].width, pictable[j].height);
            assets[dataMap[i].ToLowerInvariant()] = asset;
        }

        // Tile8
        var tile8Data = grsegs[GraphicConstants.STARTTILE8];
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
