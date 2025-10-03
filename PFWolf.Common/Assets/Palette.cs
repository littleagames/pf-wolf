namespace PFWolf.Common.Assets;

public record Palette : Asset
{
    public PaletteColor[] Colors { get; set; } = [];
    public Palette()
    {
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