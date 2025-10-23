using PFWolf.Common.Assets;

namespace PFWolf.Common.DataLoaders;

internal class WolfRawFontDataLoader
{
    public static Font Load(MemoryStream stream)
    {
        var rawData = stream.ToArray();

        var fontChars = new List<FontCharacter>();
        var location = GetLocations(rawData);
        var widths = GetWidths(rawData);
        var height = GetHeight(rawData);
        for (var ascii = 0; ascii < 256; ascii++)
        {
            byte[] fontData = rawData.Skip(location[ascii]).Take(sizeof(byte) * widths[ascii] * height)
                .ToArray();

            fontChars.Add(new FontCharacter
            {
                Height = height,
                Width = widths[ascii],
                Data = fontData,
            });
        }

        return new Font(fontChars);
    }

    private static short GetHeight(byte[] data)
    {
        var height = BitConverter.ToInt16(data.Take(2).ToArray());
        if (height > 255)
        {
            throw new InvalidDataException("Font height is too large.");
        }

        return height;
    }

    private static short[] GetLocations(byte[] data)
    {
        short[] location = new short[256];
        Buffer.BlockCopy(data, sizeof(short), location, 0, sizeof(short) * 256);
        if (location.ToList().Any(l => l > data.Length))
        {
            throw new InvalidDataException("Invalid font data while reading the location data.");
        }

        return location;
    }

    private static byte[] GetWidths(byte[] data)
    {
        byte[] widths = new byte[256];
        var offset = sizeof(short) + (sizeof(short) * 256);
        Buffer.BlockCopy(data, offset, widths, 0, sizeof(byte) * 256);

        if (widths.ToList().Any(l => l > data.Length))
        {
            throw new InvalidDataException("Invalid font data while reading the widths data.");
        }

        return widths;
    }
}
