using PFWolf.Assets;
using System;

namespace PFWolf.Loaders;

internal class PngGraphicDataLoader
{
    private const byte AlphaOpaqueThreshold = 128;

    internal static GraphicAsset Load(MemoryStream stream, Palette sourcePalette)
    {
        stream.Position = 0;
        var image = ImageDecoder.Decode(stream);
        int width = image.Width;
        int height = image.Height;
        var pixels = image.Pixels;
        var indexedData = new byte[width * height];

        // Prepare palette color array for fast lookup
        var paletteColors = sourcePalette.Colors;
        int paletteSize = paletteColors.Length;

        // Helper to find closest palette index
        int FindClosestPaletteIndex(int r, int g, int b, int a)
        {
            int minDist = int.MaxValue;
            int minIdx = 0;
            for (int i = 0; i < paletteSize; i++)
            {
                var p = paletteColors[i];
                int dr = r - p.Red;
                int dg = g - p.Green;
                int db = b - p.Blue;
                int da = a - p.Alpha;
                int dist = dr * dr + dg * dg + db * db + da * da;
                if (dist < minDist)
                {
                    minDist = dist;
                    minIdx = i;
                }
            }
            return minIdx;
        }

        var opacityMask = new byte[width * height];
        bool anyClear = false;

        for (int i = 0; i < width * height; i++)
        {
            int alpha = pixels[i * 4 + 3];
            indexedData[i] = (byte)FindClosestPaletteIndex(pixels[i * 4], pixels[i * 4 + 1], pixels[i * 4 + 2], alpha);
            if (alpha < AlphaOpaqueThreshold)
                anyClear = true;
            else
                opacityMask[i] = 1;
        }

        // Try to extract grAb chunk (offset)
        Point offset = Point.Zero;
        stream.Position = 8; // Skip PNG signature
        while (stream.Position < stream.Length)
        {
            // Read chunk length and type
            Span<byte> buf = stackalloc byte[8];
            if (stream.Read(buf) != 8) break;
            int chunkLen = (buf[0] << 24) | (buf[1] << 16) | (buf[2] << 8) | buf[3];
            string chunkType = System.Text.Encoding.ASCII.GetString(buf.Slice(4, 4));

            if (chunkType == "grAb" && chunkLen == 8)
            {
                Span<byte> grAbBuf = stackalloc byte[8];
                if (stream.Read(grAbBuf) == 8)
                {
                    int x = (grAbBuf[0] << 24) | (grAbBuf[1] << 16) | (grAbBuf[2] << 8) | grAbBuf[3];
                    int y = (grAbBuf[4] << 24) | (grAbBuf[5] << 16) | (grAbBuf[6] << 8) | grAbBuf[7];
                    offset = new Point { x = x, y = y };
                }
                stream.Position += 4; // Skip CRC
                break;
            }
            else
            {
                stream.Position += chunkLen + 4; // Skip chunk data + CRC
            }
        }

        return new GraphicAsset(indexedData, (short)width, (short)height)//, offset);
        {
            OpacityMask = anyClear ? opacityMask : null,
            //Data = indexedData,
            //Size = new Dimension(width, height),
            //Offset = offset
        };
    }
}
