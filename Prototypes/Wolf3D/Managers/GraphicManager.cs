using System.Diagnostics;
using Wolf3D.Entities;
using Wolf3D.Mappers;

namespace Wolf3D.Managers;

internal struct pictabletype
{
    public short width;
    public short height;
}

internal struct fontstruct
{
    public short height;
    public short[] location;
    public byte[] width;

    public fontstruct()
    {
        location = new short[256];
        width = new byte[256];
    }
}

internal class GraphicManager
{
    internal struct huffnode
    {
        public ushort bit0, bit1; // 0-255 is a character, > is a pointer to a node
    }

    public GraphicManager(VideoManager videoManager)
    {
        this.videoManager = videoManager;
    }

    private pictabletype[] pictable;

    private int chunkcomplen, chunkexplen;

    private byte[][] grsegs = new byte[GraphicConstants.NUMCHUNKS][];
    private huffnode[] grhuffman = new huffnode[255];
    private int[] grstarts = new int[GraphicConstants.NUMCHUNKS + 1];
    private const string gheadname = "vgahead.";
    private const string gfilename = "vgagraph.";
    private const string gdictname = "vgadict.";
    private readonly VideoManager videoManager;

    private bool ignorenumchunks = false;

    public void Init(string extension, bool ignorenumchunks)
    {
        this.ignorenumchunks = ignorenumchunks;
        CAL_SetupGrFile(extension);
    }

    public fontstruct GetFont(int fontnumber)
    {
        var data = grsegs[GraphicConstants.STARTFONT + fontnumber];
        return FontHelper.GetFont(data);
    }

    public fontstruct GetFont(string fontName)
    {
        throw new NotImplementedException("Once the string-valued data extraction is implemented.");
    }

    public void DrawPropString(int px, int py, string s, byte fontcolor, int fontnumber)
    {
        videoManager.DrawPropString(px, py, s, fontcolor, grsegs[GraphicConstants.STARTFONT + fontnumber]);
    }

    public byte[] GetDemo(int demonumber)
    {
        string[] dems = { "demo0", "demo1", "demo2", "demo3" };
        return grsegs[GraphicsMappings.GraphicKeys.IndexOf(dems[demonumber])];
    }

    public string GetText(string textName)
    {
        string? foundKey = GraphicsMappings.GraphicKeys.FirstOrDefault(x => x.ToLowerInvariant().Equals(textName.ToLowerInvariant()));
        if (foundKey != null)
        {
            var foundchunk = GraphicsMappings.GraphicKeys.IndexOf(foundKey);
            if (foundchunk != -1)
            {
                return new string(
                    System.Text.Encoding.ASCII.GetString(
                        grsegs[(int)foundchunk]).ToCharArray());
            }
        }

        return "";
    }

    public void DrawTile8(int x, int y, int tile)
    {
        videoManager.MemToScreen(grsegs[GraphicConstants.STARTTILE8].Skip(tile * 64).ToArray(), 8, 8, x, y);
    }

    [Obsolete("This should evntually come with the picture's Graphic component.")]
    public pictabletype GetPicMetadata(string picName)
    {
        var chunknum = GraphicsMappings.GraphicKeys.IndexOf(picName);
        if (chunknum < 0 || chunknum - GraphicConstants.STARTPICS >= pictable.Length)
        {
            return new();
            //throw new PfWolfGraphicException($"GetPicMetadata: No metadata found for picture name '{picName}'!");
        }

        return pictable[(int)chunknum - GraphicConstants.STARTPICS];
    }

    public void DrawComponent(MenuComponent component)
    {
        if (component is Background bkgd)
        {
            videoManager.Bar(0, 0, 320, 200, bkgd.Color);
        }
        else if (component is Graphic gfx)
        {
            if (string.IsNullOrEmpty(gfx.Asset))
                return;

            string? foundKey = GraphicsMappings.GraphicKeys.FirstOrDefault(x => x.ToLowerInvariant().Equals(gfx.Asset.ToLowerInvariant()));
            if (foundKey != null)
            {
                var foundchunk = GraphicsMappings.GraphicKeys.IndexOf(foundKey);
                if (foundchunk != -1)
                {
                    int picnum = (int)(foundchunk - GraphicConstants.STARTPICS);
                    int width, height;

                    width = pictable[picnum].width;
                    height = pictable[picnum].height;

                    if (gfx.OrientationX == HorizontalOrientation.Center)
                        gfx.X = 160 - width / 2;
                    else if (gfx.OrientationX == HorizontalOrientation.Right)
                        gfx.X = 320 - width;

                    if (gfx.OrientationY == VerticalOrientation.Center)
                        gfx.Y = 100 - height / 2;
                    if (gfx.OrientationY == VerticalOrientation.Bottom)
                        gfx.Y = 200 - height;

                    DrawPic(gfx.X, gfx.Y, foundchunk);
                }
            }
        }
        else if (component is Stripe stripe)
        {
            videoManager.Bar(0, stripe.Y, 320, 24, stripe.BackingColor);
            videoManager.HorizontalLine(0, 319, stripe.Y + 22, stripe.LineColor);
        }
        else if (component is Window window)
        {
            videoManager.Bar(window.X, window.Y, window.Width, window.Height, 0x2d);
            DrawOutline(window.X, window.Y, window.Width, window.Height, 0x23, 0x2b);
        }
    }

    private void DrawOutline(int x, int y, int w, int h, int color1, int color2)
    {
        videoManager.HorizontalLine(x, x + w, y, color2);
        videoManager.VerticalLine(y, y + h, x, color2);
        videoManager.HorizontalLine(x, x + w, y + h, color1);
        videoManager.VerticalLine(y, y + h, x + w, color1);
    }

    public void DrawPic(string graphicName, int x, int y)
    {
        if (string.IsNullOrEmpty(graphicName))
            return;
        if (graphicName.ToLowerInvariant() == "signon".ToLowerInvariant())
        {
            // TODO: Convert to "Graphic"
            pictabletype t = new();
            t.width = 320;
            t.height = 200;
            videoManager.MemToScreen(Signon.signon, t.width, t.height, x, y);
            return;
        }

        string? foundKey = GraphicsMappings.GraphicKeys.FirstOrDefault(x => x.ToLowerInvariant().Equals(graphicName.ToLowerInvariant()));
        if (foundKey != null)
        {
            var foundchunk = GraphicsMappings.GraphicKeys.IndexOf(foundKey);
            if (foundchunk != -1)
            {
                DrawPic(x,y,foundchunk);
            }
        }
    }

    private void DrawPic(int x, int y, int chunknum)
    {
        int picnum = (int)(chunknum - GraphicConstants.STARTPICS);
        int width, height;

        x &= ~7;

        width = pictable[picnum].width;
        height = pictable[picnum].height;

        videoManager.MemToScreen(grsegs[(int)chunknum], width, height, x, y);
    }

    public void DrawPicScaledCoord(int scx, int scy, int chunknum)
    {
        int picnum = chunknum - GraphicConstants.STARTPICS;
        short width, height;

        width = pictable[picnum].width;
        height = pictable[picnum].height;

        videoManager.MemToScreenScaledCoord(grsegs[chunknum], width, height, scx, scy);
    }

    public void MeasurePropString(string text, int fontnumber, out ushort width, out ushort height)
    {
        var data = grsegs[GraphicConstants.STARTFONT + fontnumber];

        int dataIndex = 0;
        fontstruct font = FontHelper.GetFont(data);
        // ignoring the rest of the data (we don't need it here)
        MeasureString(text, out width, out height, font);
    }

    public void MeasureString(string text, out ushort width, out ushort height, fontstruct font)
    {
        width = 0;
        int i;
        height = (ushort)font.height;
        for (i = 0; i < text.Length; i++)
        {
            width += font.width[text[i]]; // proportional width
        }
    }

    private void CAL_SetupGrFile(string extension)
    {
        var fname = $"{gdictname}{extension}";
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
        fname = $"{gheadname}{extension}";

        if (!File.Exists(fname))
        {
            throw new PfWolfGraphicException("Cannot open file: {0}. File does not exist.", fname);
        }

        using (FileStream fs = File.OpenRead(fname))
        using (BinaryReader br = new BinaryReader(fs))
        {
            long headersize = fs.Length;

            int expectedsize = grstarts.Length;

            if (!ignorenumchunks && headersize / 3 != expectedsize)
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
        fname = $"{gfilename}{extension}";

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

    internal static byte[] CAL_HuffExpand(byte[] source, int length, huffnode[] hufftable)
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

}
