using PFWolf.Common.Assets;

namespace PFWolf.Common.DataLoaders;

internal class BmpGraphicDataLoader
{
    public static Graphic Load(MemoryStream stream)
    {
        var rawData = stream.ToArray();

        // BMP header parsing
        if (rawData.Length < 54 || rawData[0] != 'B' || rawData[1] != 'M')
            throw new ArgumentException("Not a valid BMP file.");

        int dataOffset = BitConverter.ToInt32(rawData, 10);
        int width = BitConverter.ToInt32(rawData, 18);
        int height = BitConverter.ToInt32(rawData, 22);
        short bitsPerPixel = BitConverter.ToInt16(rawData, 28);

        byte[] pixelData = [];
        switch (bitsPerPixel)
        {
            case 8:
                pixelData = Bits8(width, height, dataOffset, rawData);
                break;
            case 24:
                pixelData = Bits24(width, height, dataOffset, rawData);
                break;
            default:
                throw new NotSupportedException($"Currently does not support {bitsPerPixel}-bit format");
        }


        return new Graphic
        {
            Data = pixelData,
            Size = new Dimension(width, Math.Abs(height)),
            Offset = Point.Zero
        };
    }

    public static byte[] Bits24(int width, int height, int dataOffset, byte[] rawData)
    {
        var bitsPerPixel = 24;
        int rowSize = ((bitsPerPixel * width + 31) / 32) * 4;
        int pixelArraySize = rowSize * Math.Abs(height);

        byte[] pixelData = new byte[width * Math.Abs(height) * 3];

        // BMP stores pixels bottom-up
        for (int y = 0; y < Math.Abs(height); y++)
        {
            int bmpRow = (height > 0 ? Math.Abs(height) - 1 - y : y);
            int bmpRowStart = dataOffset + bmpRow * rowSize;
            int pixelRowStart = y * width * 3;
            Array.Copy(rawData, bmpRowStart, pixelData, pixelRowStart, width * 3);
        }

        return pixelData;
    }

    public static byte[] Bits8(int width, int height, int dataOffset, byte[] rawData)
    {
        // Palette: 256 entries, 4 bytes each (B,G,R,0)
        int paletteSize = 256 * 4;
        int paletteOffset = 54;
        byte[] palette = new byte[paletteSize];
        Array.Copy(rawData, paletteOffset, palette, 0, paletteSize);

        int rowSize = ((width + 3) / 4) * 4; // Each row is padded to 4 bytes
        byte[] pixelData = new byte[width * Math.Abs(height)];

        // BMP stores pixels bottom-up
        for (int y = 0; y < Math.Abs(height); y++)
        {
            int bmpRow = (height > 0 ? Math.Abs(height) - 1 - y : y);
            int bmpRowStart = dataOffset + bmpRow * rowSize;
            int pixelRowStart = y * width;
            Array.Copy(rawData, bmpRowStart, pixelData, pixelRowStart, width);
        }

        return pixelData;
    }
}
