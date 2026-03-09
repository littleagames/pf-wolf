using System.Runtime.InteropServices;

namespace Wolf3D;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct pictabletype
{
    public short width;
    public short height;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
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

internal partial class Program
{
    internal static int WHITE = 15;			// graphics mode independant colors
    internal static int BLACK		= 0;
    internal static int FIRSTCOLOR	= 1;
    internal static int SECONDCOLOR	= 12;
    internal static int F_WHITE		= 15;
    internal static int F_BLACK		= 0;
    internal static int F_FIRSTCOLOR= 1;
    internal static int F_SECONDCOLOR = 12;

    static pictabletype[] pictable;
    static int px, py;
    static byte fontcolor, backcolor;
    static int fontnumber;

    internal static byte[] VL_DePlaneVGA(byte[] source, int width, int height)
    {
        int x, y, plane;
        ushort size, pwidth;

        size = (ushort)(width * height);

        if ((width & 3) != 0)
        {
            Quit("DePlaneVGA: width not divisible by 4!");
            return source;
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

    internal static void SETFONTCOLOR(byte f, byte b)
    {
        fontcolor = f;
        backcolor = b;
    }

    internal static void VWB_DrawTile8(int x, int y, int tile)
    {
        _videoManager.MemToScreen(grsegs[STARTTILE8].Skip(tile * 64).ToArray(), 8, 8, x, y);
    }

    internal static void VWB_DrawPic(int x, int y, graphicnums chunknum)
    {
        int picnum = (int)(chunknum - STARTPICS);
        int width, height;

        x &= ~7;

        width = pictable[picnum].width;
        height = pictable[picnum].height;

        _videoManager.MemToScreen(grsegs[(int)chunknum], width, height, x, y);
    }

    internal static void VWB_DrawPicScaledCoord(int scx, int scy, int chunknum)
    {
        int picnum = chunknum - STARTPICS;
        short width, height;

        width = pictable[picnum].width;
        height = pictable[picnum].height;

        _videoManager.MemToScreenScaledCoord(grsegs[chunknum], width, height, scx, scy);
    }

    internal static void VW_MeasurePropString (string text, out ushort width, out ushort height)
    {
        var data = grsegs[STARTFONT + fontnumber];

        int dataIndex = 0;
        fontstruct font = FontHelper.GetFont(data);
        // ignoring the rest of the data (we don't need it here)
        VWL_MeasureString(text, out width, out height, font);
    }

    internal static void VWL_MeasureString (string text, out ushort width, out ushort height, fontstruct font)
    {
        width = 0;
        int i;
        height = (ushort)font.height;
        for (i = 0; i < text.Length; i++)
        {
            width += font.width[text[i]]; // proportional width
        }
    }
}
