using PFWolf.Assets;

namespace PFWolf.Shading;

/// <summary>
/// The light rows (shading tables) for one palette, built on first use and kept: the game's
/// VideoManager has one for the game palette, the editor one per game it shows. Every row shares
/// one cache of nearest-color matches, so rows built mid-level (for changing lights) mostly find
/// their colors there.
/// </summary>
public sealed class LightRowBuilder
{
    private readonly PaletteColor[] _palette;
    private readonly bool[] _skipped;
    private readonly Dictionary<(int Level, int Levels, int FadeSteps, int Fade, int Tint), byte[]> _rows = [];
    private readonly Dictionary<int, byte> _matches = [];
    private (double L, double A, double B)[]? _paletteLab;

    /// <summary>
    /// Rows for these 256 colors (copied). No shaded color is matched to an index in
    /// <paramref name="skip"/>: the palette-cycles indices (Blake Stone's 240-254), whose colors
    /// move, so a faded wall would flash with them (a dark green turning bright green far off).
    /// </summary>
    public LightRowBuilder(IReadOnlyList<PaletteColor> palette, IEnumerable<int>? skip = null)
    {
        _palette = [.. palette];
        _skipped = new bool[_palette.Length];
        foreach (int i in skip ?? [])
            if (i >= 0 && i < _skipped.Length)
                _skipped[i] = true;
        if (_skipped.All(s => s))
            Array.Clear(_skipped);      // nothing left to match: skip none
    }

    /// <summary>
    /// The shading tables for one light level: <paramref name="fadeSteps"/> palette remaps of 256
    /// entries each, one after another. Every color is lit: dimmed toward black to
    /// <paramref name="level"/>/(<paramref name="levels"/> - 1) of itself and multiplied by the
    /// tint (#FFFFFF for none). Then it's mixed toward the fade color by
    /// step/(<paramref name="fadeSteps"/> - 1) for the step'th remap, and matched to the closest
    /// palette entry.
    /// </summary>
    public byte[] Row(int level, int levels, int fadeSteps, byte fadeR, byte fadeG, byte fadeB,
        byte tintR = 255, byte tintG = 255, byte tintB = 255)
    {
        int tint = tintR << 16 | tintG << 8 | tintB;
        var key = (level, levels, fadeSteps, fadeR << 16 | fadeG << 8 | fadeB, tint);
        if (_rows.TryGetValue(key, out var row))
            return row;

        float light = level / (float)(levels - 1);
        float lightR = light * tintR / 255f, lightG = light * tintG / 255f, lightB = light * tintB / 255f;
        bool untouched = level == levels - 1 && tint == 0xFFFFFF;
        static byte Mix(float from, byte to, float amount) => (byte)Math.Clamp(from + (to - from) * amount + 0.5f, 0, 255);

        row = new byte[fadeSteps * 256];
        var matches = _matches;
        for (int step = 0; step < fadeSteps; step++)
        {
            float fade = step / (float)(fadeSteps - 1);
            for (int i = 0; i < 256; i++)
            {
                if (untouched && step == 0)
                {
                    row[i] = (byte)i;           // keep the index, not just its color
                    continue;
                }

                var c = _palette[i];
                byte r = Mix(c.Red * lightR, fadeR, fade), g = Mix(c.Green * lightG, fadeG, fade), b = Mix(c.Blue * lightB, fadeB, fade);
                int rgb = r << 16 | g << 8 | b;
                if (!matches.TryGetValue(rgb, out var index))
                    matches[rgb] = index = ClosestByLab(r, g, b);
                row[step * 256 + i] = index;
            }
        }

        _rows[key] = row;
        return row;
    }

    /// <summary>
    /// The palette entry closest to a color as the eye sees it (in CIELAB): a faded or dimmed
    /// color keeps its hue where the palette allows, instead of jumping to one that's merely
    /// near in Rgb (a greyed blue to a purple). Skipped indices are never picked.
    /// </summary>
    public byte ClosestByLab(byte r, byte g, byte b)
    {
        _paletteLab ??= _palette.Select(c => ToLab(c.Red, c.Green, c.Blue)).ToArray();

        var (l, a, bb) = ToLab(r, g, b);
        byte closestIndex = 0;
        double closestDistance = double.MaxValue;

        for (int i = 0; i < _paletteLab.Length; i++)
        {
            if (_skipped[i])
                continue;
            var p = _paletteLab[i];
            double dl = l - p.L, da = a - p.A, db = bb - p.B;
            double distance = dl * dl + da * da + db * db;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = (byte)i;
            }
        }

        return closestIndex;
    }

    static (double L, double A, double B) ToLab(byte r, byte g, byte b)
    {
        static double Linear(byte c)
        {
            double v = c / 255.0;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        static double F(double t) => t > 216.0 / 24389 ? Math.Cbrt(t) : (24389.0 / 27 * t + 16) / 116;

        double lr = Linear(r), lg = Linear(g), lb = Linear(b);
        double x = (0.4124 * lr + 0.3576 * lg + 0.1805 * lb) / 0.95047;
        double y = 0.2126 * lr + 0.7152 * lg + 0.0722 * lb;
        double z = (0.0193 * lr + 0.1192 * lg + 0.9505 * lb) / 1.08883;
        double fx = F(x), fy = F(y), fz = F(z);
        return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }
}
