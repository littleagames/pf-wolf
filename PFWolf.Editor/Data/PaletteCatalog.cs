using System.Globalization;
using System.Text;
using PFWolf.Assets;
using PFWolf.Loaders;

namespace PFWolf.Editor.Data;

/// <summary>Something drawn in a palette: the game itself, a title or end screen, a movie. Picture is what it shows, when there's one.</summary>
public sealed record PaletteUse(string Label, string? Picture = null)
{
    public override string ToString() => Label;
}

/// <summary>One palette in the game's assets, where it came from and what's drawn in it</summary>
public sealed record PaletteInfo(string Name, PaletteColor[] Colors, IReadOnlyList<AssetOrigin> Origins, IReadOnlyList<PaletteUse> Uses, bool IsGamePalette)
{
    /// <summary>The file or pack the palette in use comes from</summary>
    public string Source => Origins.LastOrDefault(origin => origin.Action != AssetOrigin.LeftOut)?.Source ?? "";

    public override string ToString() => Name;
}

/// <summary>
/// Every palette the game's assets hold (pk3 palettes/ files, and the data files' own: Spear's
/// TITLEPAL and END*PAL, Blake's extras), with what game-info and the movies draw in each, and
/// the palette files the editor reads and writes
/// </summary>
public static class PaletteCatalog
{
    public const int ColorCount = 256;

    public static List<PaletteInfo> Build(GameContent content)
    {
        var gamePalette = GamePaletteName(content);
        var uses = Uses(content, gamePalette);
        var entries = new List<PaletteInfo>();
        foreach (var name in content.Assets.AssetNames.Where(content.Assets.Exists<Palette>).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (content.Find<Palette>(name) is not { Colors.Length: > 0 } palette)
                continue;

            // A palette is always 256 colors here, so every index can be shown and edited
            var colors = new PaletteColor[ColorCount];
            Array.Copy(palette.Colors, colors, Math.Min(ColorCount, palette.Colors.Length));
            var display = name.ToUpperInvariant();
            entries.Add(new PaletteInfo(display, colors, ArtCatalog.Origins(content, name, nameof(Palette)),
                uses.GetValueOrDefault(display, []), display.Equals(gamePalette, StringComparison.OrdinalIgnoreCase)));
        }

        // The game palette first, the rest by name
        return entries.OrderByDescending(entry => entry.IsGamePalette).ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>The palette the game draws everything in (gamepack-info's game-palette), upper-cased; null when there's none</summary>
    public static string? GamePaletteName(GameContent content)
    {
        try
        {
            return content.Assets.GetGamePaletteName().ToUpperInvariant();
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or NullReferenceException)
        {
            return null;
        }
    }

    /// <summary>Palettes by name: the game palette, and the screens and movies game-info and movies.yaml draw in their own</summary>
    public static Dictionary<string, List<PaletteUse>> Uses(GameContent content, string? gamePalette)
    {
        var uses = new Dictionary<string, List<PaletteUse>>(StringComparer.OrdinalIgnoreCase);
        void Add(string? name, PaletteUse use)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            if (!uses.TryGetValue(name, out var list))
                uses[name] = list = [];
            if (!list.Contains(use))
                list.Add(use);
        }

        Add(gamePalette, new PaletteUse("The game palette: every texture, sprite and picture drawn without its own"));

        var info = content.GameInfo;
        if (info != null)
        {
            Add(info.TitlePalette, new PaletteUse("The title screen", info.TitlePics.FirstOrDefault()));
            void Screens(IEnumerable<TitleScreenInfo> screens, string label)
            {
                int number = 1;
                foreach (var screen in screens)
                {
                    var what = screen.Pic ?? (screen.Title ? info.TitlePics.FirstOrDefault() : null) ?? screen.Movie;
                    Add(screen.Palette, new PaletteUse(what != null ? $"{label} {number} ({what})" : $"{label} {number}", screen.Pic ?? (screen.Title ? info.TitlePics.FirstOrDefault() : null)));
                    number++;
                }
            }
            Screens(info.Intro, "Intro screen");
            Screens(info.TitleLoop, "Title loop screen");

            foreach (var (number, cluster) in info.Clusters.OrderBy(cluster => cluster.Key))
            {
                foreach (var screen in cluster.EndScreens)
                    Add(screen.Palette, new PaletteUse($"Cluster {number}'s end screen {screen.Pic}", screen.Pic));
            }
        }

        if (content.Find<MoviesAsset>($"{content.PackId}/movies") is { } movies)
        {
            foreach (var (name, movie) in movies.Movies.OrderBy(movie => movie.Key, StringComparer.OrdinalIgnoreCase))
                Add(movie.Palette, new PaletteUse($"Movie {name}"));
        }

        return uses;
    }

    //
    // Editing
    //

    /// <summary>Colors <paramref name="from"/> to <paramref name="to"/> (either way round) blended evenly from the first one to the last</summary>
    public static void Gradient(PaletteColor[] colors, int from, int to)
    {
        if (from > to)
            (from, to) = (to, from);
        if (to - from < 2)
            return;

        var (first, last) = (colors[from], colors[to]);
        for (int i = from + 1; i < to; i++)
        {
            double t = (i - from) / (double)(to - from);
            colors[i] = new PaletteColor(Blend(first.Red, last.Red), Blend(first.Green, last.Green), Blend(first.Blue, last.Blue));

            byte Blend(byte a, byte b) => (byte)Math.Round(a + (b - a) * t);
        }
    }

    /// <summary>The index of the color nearest this one (as the engine matches #RRGGBB colors)</summary>
    public static int Closest(IReadOnlyList<PaletteColor> colors, byte red, byte green, byte blue)
    {
        int best = 0, bestDistance = int.MaxValue;
        for (int i = 0; i < colors.Count; i++)
        {
            int dr = colors[i].Red - red, dg = colors[i].Green - green, db = colors[i].Blue - blue;
            int distance = dr * dr + dg * dg + db * db;
            if (distance < bestDistance)
                (best, bestDistance) = (i, distance);
        }
        return best;
    }

    /// <summary>"#RRGGBB" for a color</summary>
    public static string Hex(PaletteColor color) => $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";

    /// <summary>Reads "#RRGGBB", "RRGGBB" or "#RGB"; false for anything else</summary>
    public static bool TryParseHex(string text, out PaletteColor color)
    {
        color = default;
        var hex = text.Trim().TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(digit => $"{digit}{digit}"));
        if (hex.Length != 6 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return false;
        color = new PaletteColor((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }

    //
    // Files
    //

    /// <summary>The kinds of palette file the editor reads and writes</summary>
    public enum FileFormat
    {
        /// <summary>768 bytes, red green blue for each color, 0-255: what pk3 palettes/ hold</summary>
        Raw,
        /// <summary>Paint Shop Pro's text palette ("JASC-PAL"), which most paint programs read</summary>
        Jasc,
        /// <summary>GIMP's text palette (.gpl)</summary>
        Gimp,
    }

    /// <summary>A palette as a pk3's palettes/NAME.pal holds it: 256 red, green, blue triples, 0-255</summary>
    public static byte[] ToRaw(IReadOnlyList<PaletteColor> colors)
    {
        var data = new byte[ColorCount * 3];
        for (int i = 0; i < ColorCount && i < colors.Count; i++)
        {
            data[i * 3] = colors[i].Red;
            data[i * 3 + 1] = colors[i].Green;
            data[i * 3 + 2] = colors[i].Blue;
        }
        return data;
    }

    public static byte[] Write(IReadOnlyList<PaletteColor> colors, FileFormat format, string name)
    {
        switch (format)
        {
            case FileFormat.Jasc:
            {
                var text = new StringBuilder("JASC-PAL\r\n0100\r\n256\r\n");
                foreach (var color in colors.Take(ColorCount))
                    text.Append(CultureInfo.InvariantCulture, $"{color.Red} {color.Green} {color.Blue}\r\n");
                return Encoding.ASCII.GetBytes(text.ToString());
            }
            case FileFormat.Gimp:
            {
                var text = new StringBuilder($"GIMP Palette\nName: {name}\nColumns: 16\n#\n");
                int index = 0;
                foreach (var color in colors.Take(ColorCount))
                    text.Append(CultureInfo.InvariantCulture, $"{color.Red,3} {color.Green,3} {color.Blue,3}\tIndex {index++}\n");
                return Encoding.ASCII.GetBytes(text.ToString());
            }
            default:
                return ToRaw(colors);
        }
    }

    /// <summary>
    /// Reads a palette file: a raw 768-byte palette (6-bit VGA values, 0-63, when no value is over
    /// 63) or Adobe .act, a JASC-PAL or a GIMP palette. Fewer than 256 colors fill the start; returns the colors
    /// and a note on how the file was read, or throws InvalidDataException.
    /// </summary>
    public static (PaletteColor[] Colors, string How) Read(byte[] data)
    {
        var text = data.Length > 0 && data.Length < 64 * 1024 ? Encoding.ASCII.GetString(data) : "";
        if (text.StartsWith("JASC-PAL"))
        {
            var lines = Lines(text).Skip(3);
            return (FromTriples(lines, "JASC-PAL"), "JASC-PAL");
        }
        if (text.StartsWith("GIMP Palette"))
        {
            // Header lines (Name:, Columns:) and comments come before the colors
            var lines = Lines(text).Skip(1).Where(line => !line.StartsWith('#') && !line.Contains(':'));
            return (FromTriples(lines, "GIMP palette"), "GIMP palette");
        }

        // An Adobe .act is a raw palette with a color count and transparent index after it
        if (data.Length == ColorCount * 3 + 4)
            data = data[..(ColorCount * 3)];
        if (data.Length != ColorCount * 3)
            throw new InvalidDataException($"It isn't a palette the editor reads: a raw palette is {ColorCount * 3} bytes (this is {data.Length}), else a JASC-PAL or GIMP palette");

        var colors = new PaletteColor[ColorCount];
        bool vga = data.All(value => value <= 63);
        for (int i = 0; i < ColorCount; i++)
        {
            colors[i] = vga
                ? new PaletteColor(FromVga(data[i * 3]), FromVga(data[i * 3 + 1]), FromVga(data[i * 3 + 2]))
                : new PaletteColor(data[i * 3], data[i * 3 + 1], data[i * 3 + 2]);
        }
        return (colors, vga ? "raw, 6-bit VGA values (0-63) scaled to 0-255" : "raw");

        static IEnumerable<string> Lines(string text)
            => text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0);
    }

    /// <summary>A 6-bit VGA DAC value (0-63) as 0-255, as the engine reads the data files' palettes</summary>
    public static byte FromVga(byte value) => (byte)(Math.Min(value, (byte)63) * 255 / 63);

    /// <summary>A 0-255 value as the VGA DAC's 6 bits (0-63)</summary>
    public static int ToVga(byte value) => (int)Math.Round(value * 63 / 255.0);

    private static PaletteColor[] FromTriples(IEnumerable<string> lines, string what)
    {
        var colors = new PaletteColor[ColorCount];
        int count = 0;
        foreach (var line in lines)
        {
            if (count == ColorCount)
                break;
            var parts = line.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3
                || !byte.TryParse(parts[0], CultureInfo.InvariantCulture, out var red)
                || !byte.TryParse(parts[1], CultureInfo.InvariantCulture, out var green)
                || !byte.TryParse(parts[2], CultureInfo.InvariantCulture, out var blue))
                throw new InvalidDataException($"The {what} has a line that isn't three numbers 0-255: \"{line}\"");
            colors[count++] = new PaletteColor(red, green, blue);
        }
        if (count == 0)
            throw new InvalidDataException($"The {what} has no colors");
        for (int i = count; i < ColorCount; i++)
            colors[i] = new PaletteColor(0, 0, 0);
        return colors;
    }

    /// <summary>
    /// Writes a palette to palettes/name.pal in the mod (a folder or a pk3), where the game reads
    /// it in place of the palette of that name. Returns where it went.
    /// </summary>
    public static string Save(string modPath, string name, IReadOnlyList<PaletteColor> colors)
        => Editing.ModFiles.Write(modPath, ($"palettes/{name.ToLowerInvariant()}.pal", ToRaw(colors)));
}
