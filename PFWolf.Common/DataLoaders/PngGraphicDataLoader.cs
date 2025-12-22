using PFWolf.Common.Assets;
using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PFWolf.Common.DataLoaders;

public class PngGraphicDataLoader
{
    public static Graphic Load(MemoryStream stream, Palette sourcePalette)
    {
        // Using SixLabors.ImageSharp for cross-platform PNG support
        stream.Position = 0;
        using var image = Image.Load<Rgba32>(stream);
        int width = image.Width;
        int height = image.Height;
        var indexedData = new byte[width * height];

        // Prepare palette color array for fast lookup
        var paletteColors = sourcePalette.Colors;
        int paletteSize = paletteColors.Length;

        // Helper to find closest palette index
        int FindClosestPaletteIndex(Rgba32 color)
        {
            int minDist = int.MaxValue;
            int minIdx = 0;
            for (int i = 0; i < paletteSize; i++)
            {
                var p = paletteColors[i];
                int dr = color.R - p.Red;
                int dg = color.G - p.Green;
                int db = color.B - p.Blue;
                int da = color.A - p.Alpha;
                int dist = dr * dr + dg * dg + db * db + da * da;
                if (dist < minDist)
                {
                    minDist = dist;
                    minIdx = i;
                }
            }
            return minIdx;
        }

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    var color = row[x];
                    int idx = FindClosestPaletteIndex(color);
                    indexedData[y * width + x] = (byte)idx;
                }
            }
        });

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
                    offset = new Point(x, y);
                }
                stream.Position += 4; // Skip CRC
                break;
            }
            else
            {
                stream.Position += chunkLen + 4; // Skip chunk data + CRC
            }
        }

        return new Graphic
        {
            Data = indexedData,
            Size = new Dimension(width, height),
            Offset = offset
        };
    }
}
