using System.Globalization;

namespace PFWolf.Assets;

/// <summary>
/// A game pack's sprite color translations (gamepacks/{pack}/translations.yaml, a mod's
/// translations.yaml), by name: each a list of ZDoom-style ranges that recolor a sprite's
/// palette indices (see <see cref="TranslationTable"/>). An actor is drawn through one by its
/// actordefs `translation` property, a mapdefs thing's `translation:`, its faction's
/// (factions.yaml), or a player's color. A pack built on a base-pack starts from its file.
/// </summary>
public record TranslationsAsset : Asset
{
    public TranslationsAsset()
    {
    }

    public TranslationsAsset(Dictionary<string, List<string>> translations)
    {
        foreach (var (name, ranges) in translations)
            Translations[name] = ranges ?? [];
    }

    public Dictionary<string, List<string>> Translations { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Builds a translation: a table of 256 palette indices, what each index of a sprite is drawn
/// as. Every index starts as itself; each range then recolors some, from the index's own color
/// in the palette (not what an earlier range made of it). A range is ZDoom's:
///   "208:223=144:159"            those indices onto these, spread evenly (either way round)
///   "208:223=[255,0,0]:[64,0,0]" onto a gradient between two colors (0-255 each), the
///                                nearest palette color to each step; "#FF0000:#400000" too
///   "208:223=%[0,0,0]:[2,0,0]"   desaturated: each index's own lightness picks the color
///                                between the two (each 0 to 2, 1 being full), keeping its
///                                shading whatever color it was
/// One index on its own ("15=31") is a range of one.
/// </summary>
public static class TranslationTable
{
    /// <summary>The table that leaves every color as it is</summary>
    public static byte[] Identity() => Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

    /// <summary>
    /// The table the ranges make against the palette; a range that can't be read is left out,
    /// with a warning (naming <paramref name="name"/>)
    /// </summary>
    public static byte[] Build(IEnumerable<string> ranges, IReadOnlyList<PaletteColor> palette, string name, List<string> warnings)
    {
        var table = Identity();
        foreach (var range in ranges)
        {
            try
            {
                Apply(table, range, palette);
            }
            catch (FormatException e)
            {
                warnings.Add($"translations.yaml: {name}'s \"{range}\" is left out: {e.Message}");
            }
        }
        return table;
    }

    private static void Apply(byte[] table, string range, IReadOnlyList<PaletteColor> palette)
    {
        var parts = range.Split('=', 2);
        if (parts.Length != 2)
            throw new FormatException("it needs from=to, e.g. 208:223=144:159");

        var (first, last) = ParseIndexRange(parts[0]);
        var to = parts[1].Trim();
        if (to.StartsWith('%'))
        {
            var (low, high) = ParseColorPair(to[1..], scale: true);
            for (int i = Math.Min(first, last); i <= Math.Min(Math.Max(first, last), palette.Count - 1); i++)
            {
                var c = palette[i];
                double light = (0.299 * c.Red + 0.587 * c.Green + 0.114 * c.Blue) / 255;
                table[i] = Nearest(palette, Lerp(low, high, light));
            }
            return;
        }

        if (to.Contains('[') || to.Contains('#'))
        {
            var (start, end) = ParseColorPair(to, scale: false);
            Spread(first, last, t => table[Step(first, last, t)] = Nearest(palette, Lerp(start, end, t)));
            return;
        }

        var (toFirst, toLast) = ParseIndexRange(to);
        Spread(first, last, t => table[Step(first, last, t)] = (byte)Math.Round(toFirst + (toLast - toFirst) * t));
    }

    // Calls back for each index of first..last (whichever way round) with how far along it is, 0 to 1
    private static void Spread(int first, int last, Action<double> each)
    {
        int count = Math.Abs(last - first);
        for (int step = 0; step <= count; step++)
            each(count == 0 ? 0 : step / (double)count);
    }

    // The index how far (0-1) along first..last
    private static int Step(int first, int last, double t) => (int)Math.Round(first + (last - first) * t);

    private static (int First, int Last) ParseIndexRange(string text)
    {
        var ends = text.Split(':');
        if (ends.Length > 2)
            throw new FormatException($"\"{text}\" isn't an index or a range of them (first:last)");
        int first = ParseIndex(ends[0]);
        return (first, ends.Length == 2 ? ParseIndex(ends[1]) : first);
    }

    private static int ParseIndex(string text) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index is >= 0 and <= 255
            ? index
            : throw new FormatException($"\"{text.Trim()}\" isn't a palette index (0-255)");

    // Two colors, [r,g,b]:[r,g,b] or #RRGGBB:#RRGGBB, as 0-255 each (scale: a desaturated
    // range's 0-2, times 255)
    private static ((double R, double G, double B) Start, (double R, double G, double B) End) ParseColorPair(string text, bool scale)
    {
        var colors = new List<(double, double, double)>();
        int at = 0;
        while (at < text.Length)
        {
            char c = text[at];
            if (c == '[')
            {
                int close = text.IndexOf(']', at);
                if (close < 0)
                    throw new FormatException($"\"{text}\" has a [ with no ]");
                var values = text[(at + 1)..close].Split(',').Select(v => double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
                    ? n : throw new FormatException($"\"{v.Trim()}\" isn't a number")).ToArray();
                if (values.Length != 3)
                    throw new FormatException($"[{text[(at + 1)..close]}] isn't three numbers");
                double factor = scale ? 255 : 1;
                colors.Add((values[0] * factor, values[1] * factor, values[2] * factor));
                at = close + 1;
            }
            else if (c == '#' && at + 7 <= text.Length
                && int.TryParse(text.AsSpan(at + 1, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            {
                colors.Add(((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF));
                at += 7;
            }
            else if (c is ':' or ' ')
                at++;
            else
                throw new FormatException($"\"{text}\" isn't two colors ([r,g,b]:[r,g,b] or #RRGGBB:#RRGGBB)");
        }

        return colors.Count == 2 ? (colors[0], colors[1]) : throw new FormatException($"\"{text}\" isn't two colors");
    }

    private static (double R, double G, double B) Lerp((double R, double G, double B) a, (double R, double G, double B) b, double t) =>
        (a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);

    /// <summary>The palette index nearest a color (components clamped to 0-255)</summary>
    public static byte Nearest(IReadOnlyList<PaletteColor> palette, (double R, double G, double B) color)
    {
        double r = Math.Clamp(color.R, 0, 255), g = Math.Clamp(color.G, 0, 255), b = Math.Clamp(color.B, 0, 255);
        int best = 0;
        double bestDistance = double.MaxValue;
        for (int i = 0; i < palette.Count; i++)
        {
            double dr = r - palette[i].Red, dg = g - palette[i].Green, db = b - palette[i].Blue;
            double distance = dr * dr + dg * dg + db * db;
            if (distance < bestDistance)
                (best, bestDistance) = (i, distance);
        }
        return (byte)best;
    }
}
