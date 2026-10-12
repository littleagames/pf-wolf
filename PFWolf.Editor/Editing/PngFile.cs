using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using PFWolf.Assets;
using PFWolf.Editor.Rendering;

namespace PFWolf.Editor.Editing;

/// <summary>
/// Writes pictures as PNG files the game loads: 8-bit, in the game palette, see-through pixels
/// clear (tRNS), and a sprite's offset in a grAb chunk (as ZDoom and SLADE write it)
/// </summary>
public static class PngFile
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool IsPng(byte[] data) => data.AsSpan().StartsWith(Signature);

    /// <summary>
    /// A picture as a PNG in this palette. Its colors stay the palette's indices (paint programs
    /// keep them); see-through pixels take a color no other pixel uses, made clear. A picture that
    /// uses all 256 and has see-through pixels too is written as RGBA.
    /// </summary>
    public static byte[] Write(IndexedPicture picture, IReadOnlyList<PaletteColor> palette, (int X, int Y)? offset = null)
    {
        int width = picture.Width, height = picture.Height;
        bool Shows(int i) => picture.Opaque == null || i < picture.Opaque.Length && picture.Opaque[i];

        var used = new bool[256];
        bool anyClear = false;
        for (int i = 0; i < width * height; i++)
        {
            if (Shows(i))
                used[picture.Indices[i]] = true;
            else
                anyClear = true;
        }

        // 255 is Wolf3D's own see-through color, so it's the first choice
        int clear = !anyClear ? -1 : !used[255] ? 255 : Array.IndexOf(used, false);
        using var output = new MemoryStream();
        output.Write(Signature);

        if (anyClear && clear < 0)
        {
            WriteChunk(output, "IHDR", Header(width, height, colorType: 6));
            if (offset is { } rgbaOffset)
                WriteChunk(output, "grAb", Grab(rgbaOffset.X, rgbaOffset.Y));
            WriteChunk(output, "IDAT", Compress(width * 4, height, (x, y, row, at) =>
            {
                int i = y * width + x;
                var color = ColorOf(palette, picture.Indices[i]);
                row[at] = color.Red;
                row[at + 1] = color.Green;
                row[at + 2] = color.Blue;
                row[at + 3] = Shows(i) ? (byte)255 : (byte)0;
            }, bytesPerPixel: 4));
        }
        else
        {
            WriteChunk(output, "IHDR", Header(width, height, colorType: 3));
            var plte = new byte[256 * 3];
            for (int i = 0; i < 256; i++)
            {
                var color = ColorOf(palette, (byte)i);
                plte[i * 3] = color.Red;
                plte[i * 3 + 1] = color.Green;
                plte[i * 3 + 2] = color.Blue;
            }
            WriteChunk(output, "PLTE", plte);
            if (clear >= 0)
            {
                var trns = Enumerable.Repeat((byte)255, clear + 1).ToArray();
                trns[clear] = 0;
                WriteChunk(output, "tRNS", trns);
            }
            if (offset is { } indexedOffset)
                WriteChunk(output, "grAb", Grab(indexedOffset.X, indexedOffset.Y));
            WriteChunk(output, "IDAT", Compress(width, height, (x, y, row, at) =>
            {
                int i = y * width + x;
                row[at] = Shows(i) ? picture.Indices[i] : (byte)clear;
            }, bytesPerPixel: 1));
        }

        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    /// <summary>A PNG's offset (its grAb chunk), or null when it has none</summary>
    public static (int X, int Y)? ReadOffset(byte[] png)
    {
        foreach (var (type, start, length) in Chunks(png))
        {
            if (type == "grAb" && length == 8)
                return (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(start)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(start + 4)));
        }
        return null;
    }

    /// <summary>The same PNG with this offset (its grAb chunk replaced, or added after the header)</summary>
    public static byte[] WithOffset(byte[] png, int x, int y)
    {
        if (!IsPng(png))
            throw new InvalidDataException("it isn't a PNG");

        using var output = new MemoryStream();
        output.Write(Signature);
        foreach (var (type, start, length) in Chunks(png))
        {
            if (type == "grAb")
                continue;
            // The chunk as it was: its length, type, data and CRC
            output.Write(png, start - 8, length + 12);
            if (type == "IHDR")
                WriteChunk(output, "grAb", Grab(x, y));
        }
        return output.ToArray();
    }

    // Each chunk's type, where its data starts and how long it is
    private static IEnumerable<(string Type, int Start, int Length)> Chunks(byte[] png)
    {
        int pos = Signature.Length;
        while (pos + 8 <= png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos));
            var type = Encoding.ASCII.GetString(png, pos + 4, 4);
            if (length < 0 || pos + 12 + length > png.Length)
                yield break;
            yield return (type, pos + 8, length);
            if (type == "IEND")
                yield break;
            pos += 12 + length;
        }
    }

    private static PaletteColor ColorOf(IReadOnlyList<PaletteColor> palette, byte index)
        => index < palette.Count ? palette[index] : new PaletteColor(0, 0, 0);

    private static byte[] Header(int width, int height, byte colorType)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;          // bits a sample
        header[9] = colorType;  // 3 palette, 6 RGBA
        return header;          // deflate, adaptive filtering, not interlaced
    }

    private static byte[] Grab(int x, int y)
    {
        var data = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(data, x);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4), y);
        return data;
    }

    // The rows, each with filter 0 (none) before it, deflated with a zlib header
    private static byte[] Compress(int rowBytes, int height, Action<int, int, byte[], int> pixel, int bytesPerPixel)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            var row = new byte[rowBytes + 1];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < rowBytes / bytesPerPixel; x++)
                    pixel(x, y, row, 1 + x * bytesPerPixel);
                zlib.Write(row);
            }
        }
        return compressed.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        output.Write(number);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc(typeBytes, data));
        output.Write(number);
    }

    private static readonly uint[] CrcTable = MakeCrcTable();

    private static uint[] MakeCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc(byte[] type, byte[] data)
    {
        uint c = 0xFFFFFFFF;
        foreach (var b in type)
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        foreach (var b in data)
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFF;
    }
}
