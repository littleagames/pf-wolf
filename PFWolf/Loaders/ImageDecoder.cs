using System.IO.Compression;
using System.Numerics;

namespace PFWolf.Loaders;

/// <summary>A decoded picture: 4 bytes a pixel (R, G, B, A), rows top to bottom</summary>
internal sealed class RgbaImage(int width, int height, byte[] pixels)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public byte[] Pixels { get; } = pixels;

    public (byte R, byte G, byte B, byte A) this[int x, int y]
    {
        get
        {
            int i = (y * Width + x) * 4;
            return (Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
        }
    }
}

/// <summary>
/// Reads PNG and BMP pictures into RGBA. PNG: every color type and bit depth, Adam7 interlacing,
/// tRNS transparency. BMP: 1/4/8/16/24/32-bit, uncompressed or bitfields (no RLE).
/// </summary>
internal static class ImageDecoder
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    internal static RgbaImage Decode(Stream stream)
    {
        if (stream is MemoryStream memory)
            return Decode(memory.ToArray());
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return Decode(copy.ToArray());
    }

    internal static RgbaImage Decode(string path) => Decode(File.ReadAllBytes(path));

    internal static RgbaImage Decode(byte[] data)
    {
        if (data.AsSpan().StartsWith(PngSignature))
            return DecodePng(data);
        if (data.Length >= 2 && data[0] == 'B' && data[1] == 'M')
            return DecodeBmp(data);
        throw new NotSupportedException("it isn't a PNG or BMP picture");
    }

    #region PNG

    private static RgbaImage DecodePng(byte[] data)
    {
        int width = 0, height = 0, depth = 0, colorType = 0, interlace = 0;
        byte[]? palette = null;     // RGBA per entry
        byte[]? trns = null;
        using var compressed = new MemoryStream();

        int pos = PngSignature.Length;
        while (pos + 8 <= data.Length)
        {
            int length = ReadBigEndian(data, pos);
            string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            int body = pos + 8;
            if (length < 0 || body + length > data.Length)
                throw new InvalidDataException($"PNG chunk {type} runs past the end of the file");

            switch (type)
            {
                case "IHDR":
                    width = ReadBigEndian(data, body);
                    height = ReadBigEndian(data, body + 4);
                    depth = data[body + 8];
                    colorType = data[body + 9];
                    interlace = data[body + 12];
                    break;
                case "PLTE":
                    palette = new byte[256 * 4];
                    for (int i = 0; i < length / 3 && i < 256; i++)
                    {
                        palette[i * 4] = data[body + i * 3];
                        palette[i * 4 + 1] = data[body + i * 3 + 1];
                        palette[i * 4 + 2] = data[body + i * 3 + 2];
                        palette[i * 4 + 3] = 255;
                    }
                    break;
                case "tRNS":
                    trns = data.AsSpan(body, length).ToArray();
                    break;
                case "IDAT":
                    compressed.Write(data, body, length);
                    break;
            }

            if (type == "IEND")
                break;
            pos = body + length + 4; // skip the CRC
        }

        if (width <= 0 || height <= 0)
            throw new InvalidDataException("PNG has no IHDR");

        int channels = colorType switch
        {
            0 => 1, // gray
            2 => 3, // RGB
            3 => 1, // palette
            4 => 2, // gray + alpha
            6 => 4, // RGBA
            _ => throw new NotSupportedException($"PNG color type {colorType}")
        };
        if (colorType == 3)
        {
            palette ??= new byte[256 * 4];
            if (trns != null)
                for (int i = 0; i < trns.Length && i < 256; i++)
                    palette[i * 4 + 3] = trns[i];
        }

        compressed.Position = 0;
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress))
            zlib.CopyTo(inflated);
        var raw = inflated.GetBuffer();
        int rawLength = (int)inflated.Length;

        var pixels = new byte[width * height * 4];
        int bitsPerPixel = channels * depth;
        int filterStride = Math.Max(1, bitsPerPixel / 8);
        int offset = 0;

        // Adam7 passes (x start, y start, x step, y step); not interlaced is one pass over everything
        (int X, int Y, int DX, int DY)[] passes = interlace == 1
            ? [(0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)]
            : [(0, 0, 1, 1)];

        foreach (var pass in passes)
        {
            int passWidth = (width - pass.X + pass.DX - 1) / pass.DX;
            int passHeight = (height - pass.Y + pass.DY - 1) / pass.DY;
            if (passWidth <= 0 || passHeight <= 0)
                continue;

            int rowBytes = (passWidth * bitsPerPixel + 7) / 8;
            var previous = new byte[rowBytes];
            var row = new byte[rowBytes];

            for (int py = 0; py < passHeight; py++)
            {
                if (offset + 1 + rowBytes > rawLength)
                    throw new InvalidDataException("PNG image data is cut short");
                byte filter = raw[offset];
                Array.Copy(raw, offset + 1, row, 0, rowBytes);
                offset += 1 + rowBytes;
                Unfilter(filter, row, previous, filterStride);

                int y = pass.Y + py * pass.DY;
                for (int px = 0; px < passWidth; px++)
                {
                    int x = pass.X + px * pass.DX;
                    WritePngPixel(row, px, channels, depth, colorType, palette, trns, pixels, (y * width + x) * 4);
                }

                (previous, row) = (row, previous);
            }
        }

        return new RgbaImage(width, height, pixels);
    }

    private static void Unfilter(byte filter, byte[] row, byte[] previous, int stride)
    {
        switch (filter)
        {
            case 0:
                break;
            case 1: // sub
                for (int i = stride; i < row.Length; i++)
                    row[i] += row[i - stride];
                break;
            case 2: // up
                for (int i = 0; i < row.Length; i++)
                    row[i] += previous[i];
                break;
            case 3: // average
                for (int i = 0; i < row.Length; i++)
                {
                    int left = i >= stride ? row[i - stride] : 0;
                    row[i] += (byte)((left + previous[i]) >> 1);
                }
                break;
            case 4: // paeth
                for (int i = 0; i < row.Length; i++)
                {
                    int a = i >= stride ? row[i - stride] : 0;
                    int b = previous[i];
                    int c = i >= stride ? previous[i - stride] : 0;
                    int p = a + b - c;
                    int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                    row[i] += (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
                }
                break;
            default:
                throw new InvalidDataException($"PNG row filter {filter}");
        }
    }

    private static void WritePngPixel(byte[] row, int index, int channels, int depth, int colorType,
        byte[]? palette, byte[]? trns, byte[] pixels, int dest)
    {
        // Raw samples at full precision (tRNS compares against those)
        Span<int> sample = stackalloc int[4];
        for (int c = 0; c < channels; c++)
            sample[c] = ReadSample(row, (index * channels + c) * depth, depth);

        switch (colorType)
        {
            case 3:
                Array.Copy(palette!, sample[0] * 4, pixels, dest, 4);
                return;
            case 0:
            case 4:
            {
                byte gray = To8Bit(sample[0], depth);
                pixels[dest] = pixels[dest + 1] = pixels[dest + 2] = gray;
                if (colorType == 4)
                    pixels[dest + 3] = To8Bit(sample[1], depth);
                else
                    pixels[dest + 3] = (byte)(trns is { Length: >= 2 } && sample[0] == ReadBigEndian16(trns, 0) ? 0 : 255);
                return;
            }
            default: // 2, 6
                pixels[dest] = To8Bit(sample[0], depth);
                pixels[dest + 1] = To8Bit(sample[1], depth);
                pixels[dest + 2] = To8Bit(sample[2], depth);
                if (colorType == 6)
                    pixels[dest + 3] = To8Bit(sample[3], depth);
                else
                    pixels[dest + 3] = (byte)(trns is { Length: >= 6 }
                        && sample[0] == ReadBigEndian16(trns, 0)
                        && sample[1] == ReadBigEndian16(trns, 2)
                        && sample[2] == ReadBigEndian16(trns, 4) ? 0 : 255);
                return;
        }
    }

    private static int ReadSample(byte[] row, int bitOffset, int depth) => depth switch
    {
        8 => row[bitOffset >> 3],
        16 => (row[bitOffset >> 3] << 8) | row[(bitOffset >> 3) + 1],
        _ => (row[bitOffset >> 3] >> (8 - depth - (bitOffset & 7))) & ((1 << depth) - 1)
    };

    private static byte To8Bit(int sample, int depth) => depth switch
    {
        8 => (byte)sample,
        16 => (byte)(sample >> 8),
        _ => (byte)(sample * 255 / ((1 << depth) - 1))
    };

    private static int ReadBigEndian(byte[] data, int pos) =>
        (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];

    private static int ReadBigEndian16(byte[] data, int pos) => (data[pos] << 8) | data[pos + 1];

    #endregion

    #region BMP

    private static RgbaImage DecodeBmp(byte[] data)
    {
        int dataOffset = BitConverter.ToInt32(data, 10);
        int headerSize = BitConverter.ToInt32(data, 14);
        int width, height, bitsPerPixel, compression = 0, colorsUsed = 0, paletteEntrySize = 4;

        if (headerSize == 12) // BITMAPCOREHEADER
        {
            width = BitConverter.ToInt16(data, 18);
            height = BitConverter.ToInt16(data, 20);
            bitsPerPixel = BitConverter.ToInt16(data, 24);
            paletteEntrySize = 3;
        }
        else
        {
            width = BitConverter.ToInt32(data, 18);
            height = BitConverter.ToInt32(data, 22);
            bitsPerPixel = BitConverter.ToInt16(data, 28);
            compression = BitConverter.ToInt32(data, 30);
            colorsUsed = BitConverter.ToInt32(data, 46);
        }

        // BI_RGB = 0, BI_BITFIELDS = 3, BI_ALPHABITFIELDS = 6
        if (compression != 0 && compression != 3 && compression != 6)
            throw new NotSupportedException($"BMP compression {compression}");

        bool bottomUp = height > 0;
        height = Math.Abs(height);
        if (width <= 0 || height == 0)
            throw new InvalidDataException("BMP has no size");

        // Palette for 8 bits and fewer
        byte[]? palette = null;
        if (bitsPerPixel <= 8)
        {
            int count = colorsUsed > 0 ? Math.Min(colorsUsed, 256) : 1 << bitsPerPixel;
            int paletteStart = 14 + headerSize;
            palette = new byte[256 * 4];
            for (int i = 0; i < count && paletteStart + i * paletteEntrySize + 2 < data.Length; i++)
            {
                int p = paletteStart + i * paletteEntrySize;
                palette[i * 4] = data[p + 2];
                palette[i * 4 + 1] = data[p + 1];
                palette[i * 4 + 2] = data[p];
                palette[i * 4 + 3] = 255;
            }
        }

        // Channel masks for 16 and 32 bits
        uint redMask = 0, greenMask = 0, blueMask = 0, alphaMask = 0;
        if (bitsPerPixel == 16 || bitsPerPixel == 32)
        {
            if (compression == 3 || compression == 6)
            {
                // In a V4/V5 header, or just after a 40-byte one
                redMask = BitConverter.ToUInt32(data, 54);
                greenMask = BitConverter.ToUInt32(data, 58);
                blueMask = BitConverter.ToUInt32(data, 62);
                if (compression == 6 || headerSize >= 56)
                    alphaMask = BitConverter.ToUInt32(data, 66);
            }
            else if (bitsPerPixel == 16)
                (redMask, greenMask, blueMask) = (0x7C00, 0x03E0, 0x001F);
            else
                (redMask, greenMask, blueMask) = (0x00FF0000, 0x0000FF00, 0x000000FF);
        }

        int stride = ((bitsPerPixel * width + 31) / 32) * 4;
        if (dataOffset + (long)stride * height > data.Length)
            throw new InvalidDataException("BMP pixel data is cut short");

        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            int rowStart = dataOffset + (bottomUp ? height - 1 - y : y) * stride;
            for (int x = 0; x < width; x++)
            {
                int dest = (y * width + x) * 4;
                switch (bitsPerPixel)
                {
                    case 1:
                    case 2:
                    case 4:
                    case 8:
                    {
                        int bit = x * bitsPerPixel;
                        int index = (data[rowStart + (bit >> 3)] >> (8 - bitsPerPixel - (bit & 7))) & ((1 << bitsPerPixel) - 1);
                        Array.Copy(palette!, index * 4, pixels, dest, 4);
                        break;
                    }
                    case 24:
                    {
                        int p = rowStart + x * 3;
                        pixels[dest] = data[p + 2];
                        pixels[dest + 1] = data[p + 1];
                        pixels[dest + 2] = data[p];
                        pixels[dest + 3] = 255;
                        break;
                    }
                    case 16:
                    case 32:
                    {
                        uint value = bitsPerPixel == 16
                            ? BitConverter.ToUInt16(data, rowStart + x * 2)
                            : BitConverter.ToUInt32(data, rowStart + x * 4);
                        pixels[dest] = FromMask(value, redMask);
                        pixels[dest + 1] = FromMask(value, greenMask);
                        pixels[dest + 2] = FromMask(value, blueMask);
                        pixels[dest + 3] = alphaMask == 0 ? (byte)255 : FromMask(value, alphaMask);
                        break;
                    }
                    default:
                        throw new NotSupportedException($"{bitsPerPixel}-bit BMP");
                }
            }
        }

        return new RgbaImage(width, height, pixels);
    }

    private static byte FromMask(uint value, uint mask)
    {
        if (mask == 0)
            return 0;
        int shift = BitOperations.TrailingZeroCount(mask);
        int bits = BitOperations.PopCount(mask);
        uint channel = (value & mask) >> shift;
        if (bits >= 8)
            return (byte)(channel >> (bits - 8));

        // Widen to 8 bits by repeating the channel's bits (5-bit 21 -> 10101101), as ImageSharp did
        uint result = 0;
        int filled = 0;
        for (; filled < 8; filled += bits)
            result = (result << bits) | channel;
        return (byte)(result >> (filled - 8));
    }

    #endregion
}
