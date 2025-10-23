using PFWolf.Common.Assets;

namespace PFWolf.Common.DataLoaders;

internal class WolfRawGraphicDataLoader
{
    public static Graphic Load(MemoryStream stream, Dimension dimensions)
    {
        var rawData = stream.ToArray();
        // For graphic
        rawData = DeplaneData(rawData, dimensions);

        return new Graphic
        {
            Data = rawData,
            Dimensions = dimensions
        };
    }

    private static byte[] DeplaneData(byte[] source, Dimension dimensions)
    {
        int width = dimensions.Width;
        int height = dimensions.Height;

        int x, y, plane;
        ushort size, pwidth;
        byte[] temp, dest, srcline;

        size = (ushort)(width * height);

        if ((width & 3) != 0)
            throw new Exception("DeplaneData: width not divisible by 4!");

        temp = new byte[size];// SafeMalloc(size);

        //
        // munge pic into the temp buffer
        //
        srcline = source;
        var srcLineIndex = 0;
        pwidth = (ushort)(width >> 2); // width/4

        for (plane = 0; plane < 4; plane++)
        {
            dest = temp;

            for (y = 0; y < height; y++)
            {
                for (x = 0; x < pwidth; x++)
                {
                    if (srcLineIndex >= srcline.Length)
                        continue;
                    dest[width * y + (x << 2) + plane] = srcline[srcLineIndex++];
                }
            }
        }

        //
        // copy the temp buffer back into the original source
        //
        //Array.Copy(temp, source, size);

        return temp;
    }
}
