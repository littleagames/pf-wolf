namespace PFWolf.Common.Assets;

public record Palette : Asset
{
    public PaletteColor[] Colors { get; set; } = [];
    public Palette(byte[] data)
    {
        Type = AssetType.Palette;
        Colors = new PaletteColor[256];
        if (data.Length != 768) // 256*3 bytes
        {
            throw new ArgumentException("Invalid SDL palette data");
        }

        var paletteColors = new PaletteColor[256];
        for (int i = 0; i < 256; i++)
        {
            paletteColors[i] = new PaletteColor
            {
                Red = data[(i * 3)],
                Green = data[(i * 3) + 1],
                Blue = data[(i * 3) + 2],
                Alpha = 255
            };
        }

        Colors = paletteColors;
    }
}

public struct PaletteColor
{
    public PaletteColor(byte r, byte g, byte b)
    {
        Red = r;
        Green = g;
        Blue = b;
        Alpha = 255;
    }

    public PaletteColor(byte r, byte g, byte b, byte a)
    {
        Red = r;
        Green = g;
        Blue = b;
        Alpha = a;
    }

    public byte Red { get; set; } = 0;
    public byte Green { get; set; } = 0;
    public byte Blue { get; set; } = 0;
    public byte Alpha { get; set; } = 255;
    public readonly ulong ARGB => (uint)(Alpha << 32) | (byte)(Red << 16) | (byte)(Green << 8) | Blue;
    public readonly uint RGB => (uint)(Red << 16) | (byte)(Green << 8) | Blue;
}