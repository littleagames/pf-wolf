namespace PFWolf.Fonts;

/// <summary>
/// The palette work behind a font's gradient and glow, shared by the game's screen and the
/// editor's pictures so both pick the same colors
/// </summary>
public static class FontColors
{
    /// <summary>
    /// A palette index for each of <paramref name="rows"/> rows of text, shading
    /// <paramref name="color"/> from <paramref name="top"/> percent at the first row to
    /// <paramref name="bottom"/> percent at the last: positive toward white, negative toward black.
    /// Each row is matched to the closest palette entry by <paramref name="nearest"/>.
    /// </summary>
    public static byte[] Gradient((byte R, byte G, byte B) color, int rows, int top, int bottom, Func<byte, byte, byte, byte> nearest)
    {
        rows = Math.Max(rows, 1);
        static byte Shade(byte value, float amount)
            => (byte)Math.Clamp(amount >= 0 ? value + (255 - value) * amount : value * (1 + amount), 0, 255);

        var table = new byte[rows];
        for (int i = 0; i < rows; i++)
        {
            float t = rows > 1 ? i / (float)(rows - 1) : 0.5f;
            float amount = (top + (bottom - top) * t) / 100f;
            table[i] = nearest(Shade(color.R, amount), Shade(color.G, amount), Shade(color.B, amount));
        }
        return table;
    }

    /// <summary>
    /// The palette index of each of <paramref name="radius"/> glow rings, nearest the text first:
    /// the glow color mixed into the background at <paramref name="strength"/> percent for the
    /// first, fading evenly for the rest
    /// </summary>
    public static byte[] GlowRings((byte R, byte G, byte B) glow, (byte R, byte G, byte B) background, int radius, int strength,
        Func<byte, byte, byte, byte> nearest)
    {
        radius = Math.Max(radius, 1);
        static byte Mix(byte from, byte to, float amount) => (byte)Math.Clamp(from + (to - from) * amount, 0, 255);

        var rings = new byte[radius];
        for (int ring = 0; ring < radius; ring++)
        {
            float amount = strength / 100f * (1 - ring / (float)radius);
            rings[ring] = nearest(Mix(background.R, glow.R, amount), Mix(background.G, glow.G, amount), Mix(background.B, glow.B, amount));
        }
        return rings;
    }
}
