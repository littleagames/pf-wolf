using System.IO.Compression;
using System.Text;
using PFWolf.Loaders;

namespace PFWolf.Tests;

public class ImageDecoderTests
{
    // A PNG: IHDR, any extra chunks, then the rows (each starting with its filter byte) as one IDAT.
    // The decoder doesn't check CRCs, so they're left 0.
    private static byte[] Png(int width, int height, byte depth, byte colorType, byte[] filteredRows,
        params (string Type, byte[] Body)[] extraChunks)
    {
        using var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        void Chunk(string type, byte[] body)
        {
            stream.Write(BigEndian(body.Length));
            stream.Write(Encoding.ASCII.GetBytes(type));
            stream.Write(body);
            stream.Write(new byte[4]);
        }

        Chunk("IHDR", [.. BigEndian(width), .. BigEndian(height), depth, colorType, 0, 0, 0]);
        foreach (var (type, body) in extraChunks)
            Chunk(type, body);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(filteredRows);
        Chunk("IDAT", compressed.ToArray());
        Chunk("IEND", []);
        return stream.ToArray();
    }

    private static byte[] BigEndian(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    // A BMP with a 40-byte header: palette (B, G, R, 0 each) then the rows as given
    private static byte[] Bmp(int width, int height, short bitsPerPixel, byte[] rows, byte[]? palette = null)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        int dataOffset = 14 + 40 + (palette?.Length ?? 0);
        writer.Write(Encoding.ASCII.GetBytes("BM"));
        writer.Write(dataOffset + rows.Length);
        writer.Write(0);
        writer.Write(dataOffset);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write(bitsPerPixel);
        writer.Write(0);            // BI_RGB
        writer.Write(rows.Length);
        writer.Write(0);
        writer.Write(0);
        writer.Write(palette == null ? 0 : palette.Length / 4);
        writer.Write(0);
        if (palette != null)
            writer.Write(palette);
        writer.Write(rows);
        writer.Flush();
        return stream.ToArray();
    }

    [Test]
    public void Png_Rgba_Undoes_Each_Row_Filter()
    {
        // Arrange: 2x4 RGBA, one row per filter (none, sub, up, average), then paeth below
        byte[] rows =
        [
            0, 10, 20, 30, 255, 40, 50, 60, 255,     // none
            1, 1, 2, 3, 255, 1, 1, 1, 0,             // sub: (1,2,3,255) then +1 each
            2, 5, 5, 5, 0, 5, 5, 5, 0,               // up: the sub row + 5
            3, 10, 10, 10, 128, 0, 0, 0, 0,          // average
        ];
        var png = Png(2, 4, 8, 6, rows);

        // Act
        var image = ImageDecoder.Decode(png);

        // Assert
        Assert.That(image.Width, Is.EqualTo(2));
        Assert.That(image.Height, Is.EqualTo(4));
        Assert.That(image[0, 0], Is.EqualTo(((byte)10, (byte)20, (byte)30, (byte)255)));
        Assert.That(image[1, 0], Is.EqualTo(((byte)40, (byte)50, (byte)60, (byte)255)));
        Assert.That(image[0, 1], Is.EqualTo(((byte)1, (byte)2, (byte)3, (byte)255)));
        Assert.That(image[1, 1], Is.EqualTo(((byte)2, (byte)3, (byte)4, (byte)255)));
        Assert.That(image[0, 2], Is.EqualTo(((byte)6, (byte)7, (byte)8, (byte)255)));
        Assert.That(image[1, 2], Is.EqualTo(((byte)7, (byte)8, (byte)9, (byte)255)));
        // average: left is 0 at x=0, so above/2 + raw; then (left + above)/2
        Assert.That(image[0, 3], Is.EqualTo(((byte)13, (byte)13, (byte)14, (byte)255)));
        Assert.That(image[1, 3], Is.EqualTo(((byte)10, (byte)10, (byte)11, (byte)255)));
    }

    [Test]
    public void Png_Paeth_Filter_Picks_The_Nearest_Neighbour()
    {
        // Arrange: 2x2 gray; row 2 is paeth with zeros, so it copies its predictor
        byte[] rows =
        [
            0, 100, 200,
            4, 0, 0,    // x=0: up (100); x=1: a=100, b=200, c=100 -> p=200 -> b (200)
        ];
        var png = Png(2, 2, 8, 0, rows);

        // Act
        var image = ImageDecoder.Decode(png);

        // Assert
        Assert.That(image[0, 1].R, Is.EqualTo(100));
        Assert.That(image[1, 1].R, Is.EqualTo(200));
    }

    [Test]
    public void Png_Palette_Takes_Its_Alpha_From_tRNS()
    {
        // Arrange: 2x1, 4-bit palette indices 0 and 1; entry 0 is see-through
        var png = Png(2, 1, 4, 3, [0, 0x01],
            ("PLTE", [152, 0, 136, 10, 20, 30]),
            ("tRNS", [0]));

        // Act
        var image = ImageDecoder.Decode(png);

        // Assert
        Assert.That(image[0, 0], Is.EqualTo(((byte)152, (byte)0, (byte)136, (byte)0)));
        Assert.That(image[1, 0], Is.EqualTo(((byte)10, (byte)20, (byte)30, (byte)255)));
    }

    [Test]
    public void Png_Low_Bit_Gray_Widens_To_8_Bits()
    {
        // Arrange: 4x1 2-bit gray, 0..3
        var png = Png(4, 1, 2, 0, [0, 0b00_01_10_11]);

        // Act
        var image = ImageDecoder.Decode(png);

        // Assert
        Assert.That(Enumerable.Range(0, 4).Select(x => image[x, 0].R), Is.EqualTo(new byte[] { 0, 85, 170, 255 }));
    }

    [Test]
    public void Png_16_Bit_Rgb_Keeps_The_High_Byte_And_Matches_tRNS_At_Full_Precision()
    {
        // Arrange: 2x1 16-bit RGB; the first pixel is tRNS's color, the second differs only in a low byte
        var png = Png(2, 1, 16, 2, [0, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBD],
            ("tRNS", [0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC]));

        // Act
        var image = ImageDecoder.Decode(png);

        // Assert
        Assert.That(image[0, 0], Is.EqualTo(((byte)0x12, (byte)0x56, (byte)0x9A, (byte)0)));
        Assert.That(image[1, 0], Is.EqualTo(((byte)0x12, (byte)0x56, (byte)0x9A, (byte)255)));
    }

    [Test]
    public void Bmp_24_Bit_Is_Bottom_Up_With_Padded_Rows()
    {
        // Arrange: 1x2, rows padded to 4 bytes; the first row in the file is the bottom one
        byte[] rows = [3, 2, 1, 0, 30, 20, 10, 0];
        var bmp = Bmp(1, 2, 24, rows);

        // Act
        var image = ImageDecoder.Decode(bmp);

        // Assert
        Assert.That(image[0, 0], Is.EqualTo(((byte)10, (byte)20, (byte)30, (byte)255)));
        Assert.That(image[0, 1], Is.EqualTo(((byte)1, (byte)2, (byte)3, (byte)255)));
    }

    [Test]
    public void Bmp_8_Bit_Looks_Colors_Up_In_Its_Palette()
    {
        // Arrange: 2x1, indices 1 and 0
        var bmp = Bmp(2, 1, 8, [1, 0, 0, 0], palette: [136, 0, 152, 0, 30, 20, 10, 0]);

        // Act
        var image = ImageDecoder.Decode(bmp);

        // Assert
        Assert.That(image[0, 0], Is.EqualTo(((byte)10, (byte)20, (byte)30, (byte)255)));
        Assert.That(image[1, 0], Is.EqualTo(((byte)152, (byte)0, (byte)136, (byte)255)));
    }

    [Test]
    public void Bmp_16_Bit_Widens_5_Bit_Channels_By_Repeating_Their_Bits()
    {
        // Arrange: 1x1 X1R5G5B5 with red 21, green 7, blue 31
        ushort pixel = (21 << 10) | (7 << 5) | 31;
        var bmp = Bmp(1, 1, 16, [(byte)pixel, (byte)(pixel >> 8), 0, 0]);

        // Act
        var image = ImageDecoder.Decode(bmp);

        // Assert
        Assert.That(image[0, 0], Is.EqualTo(((byte)173, (byte)57, (byte)255, (byte)255)));
    }

    [Test]
    public void Other_Files_Are_Not_Supported()
    {
        // Act / Assert
        Assert.Throws<NotSupportedException>(() => ImageDecoder.Decode(Encoding.ASCII.GetBytes("GIF89a......")));
    }
}
