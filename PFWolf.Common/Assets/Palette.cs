namespace PFWolf.Common.Assets;

public record Palette : Asset
{
    public PaletteColor[] Colors { get; set; } = [];
    public Palette()
    {
    }

    public void MakeRemap(PaletteColor[] palette, ref byte[] paletteMap, int paletteSize)
    {
        if (palette == null || paletteMap == null || paletteSize <= 0 || palette.Length < paletteSize || Colors == null || Colors.Length < paletteSize)
            throw new ArgumentException("Invalid palette or paletteMap or paletteSize.");

        for (int i = 0; i < paletteSize; i++)
        {
            int bestIndex = 0;
            int bestDistance = int.MaxValue;
            var src = palette[i];

            for (int j = 0; j < Colors.Length; j++)
            {
                var dst = Colors[j];
                int dr = src.Red - dst.Red;
                int dg = src.Green - dst.Green;
                int db = src.Blue - dst.Blue;
                int da = src.Alpha - dst.Alpha;
                int distance = dr * dr + dg * dg + db * db + da * da;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = j;
                }
            }
            paletteMap[i] = (byte)bestIndex;
        }
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