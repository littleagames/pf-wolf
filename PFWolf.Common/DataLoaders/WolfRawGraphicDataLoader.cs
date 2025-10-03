using PFWolf.Common.Assets;
using PFWolf.Common.Compression;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PFWolf.Common.DataLoaders;

internal class WolfRawGraphicDataLoader
{
    public static Graphic Load(MemoryStream stream, int picNum, Dimension dimensions, HuffmanCompression huffman)
    {
       // using MemoryStream ms = new MemoryStream();
       // stream.CopyTo(ms);
        var rawData = stream.ToArray();


       // //var structData = ((StructPicAsset)vgaAssets[StructPicIndex]);
       // //var picNum = i - (int)(segmentStarts[2]);
       // //if (picNum < 0 || picNum >= structData.NumPics)
       // //{
       // //    throw new IndexOutOfRangeException($"Pic number {picNum} is out of range.");
       // //}

       //// var dimensions = structData.Dimensions[picNum];

       // var size = BitConverter.ToInt32(rawData.Take(sizeof(int)).ToArray());
       // var compressedData = rawData.Skip(sizeof(int)).ToArray();
       // var expandedData = huffman.Expand(compressedData); // TODO: 63999 for a 64000 image?
       // if (expandedData.Length < size)
       // {
       //     throw new Exception(
       //         $"Huffman expand didn't fill the entire array: {expandedData.Length} (expanded) < {size} (size)");
       // }
       // expandedData = expandedData.Take(size).ToArray();
       // expandedData = DeplaneData(expandedData, dimensions);

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
