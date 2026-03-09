using SDL2;
namespace Wolf3D;

internal partial class Program
{
    internal static uint bordercolor;

    internal static void VW_WaitVBL(uint a) => VL_WaitVBL(a);
    internal static void VL_WaitVBL(uint a) => SDL.SDL_Delay((a) * 8);

    [Obsolete("These will be initialized elsewhere")]
    internal static void VL_SetVGAPlaneMode()
    {
        pixelangle = new short[_videoManager.screenWidth];
        wallheight = new short[_videoManager.screenWidth];
    }

    internal static void VL_ConvertPalette(byte[] srcpal, SDL.SDL_Color[] destpal, int numColors)
    {
        int i, s = 0;

        for (i = 0; i < numColors; i++)
        {
            destpal[i].r = (byte)(srcpal[s++] * 255 / 63);
            destpal[i].g = (byte)(srcpal[s++] * 255 / 63);
            destpal[i].b = (byte)(srcpal[s++] * 255 / 63);
            destpal[i].a = 255;// SDL.SDL_ALPHA_OPAQUE;
        }
    }

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

        for(plane = 0; plane < 4; plane++)
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
