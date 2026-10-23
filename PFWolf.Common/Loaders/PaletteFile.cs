using System.Globalization;
using System.Text;
using PFWolf.Assets;

namespace PFWolf.Loaders;

/// <summary>
/// Palette files a modder may have: a raw 768-byte palette (as pk3 palettes/ hold, or 6-bit VGA
/// values as a classic mod's GAMEPAL data), Adobe .act, Paint Shop Pro's JASC-PAL or GIMP's .gpl
/// </summary>
public static class PaletteFile
{
    public const int ColorCount = 256;

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

    /// <summary>Where a mod's palette of this name goes: palettes/name.pal, read in place of the game's of that name</summary>
    public static string EntryPath(string name) => $"palettes/{name.ToLowerInvariant()}.pal";

    /// <summary>A 6-bit VGA DAC value (0-63) as 0-255, as the engine reads the data files' palettes</summary>
    public static byte FromVga(byte value) => (byte)(Math.Min(value, (byte)63) * 255 / 63);

    /// <summary>Whether a file's extension is one a palette file may have (.pal, .act, .gpl)</summary>
    public static bool HasPaletteExtension(string path)
        => Path.GetExtension(path).ToLowerInvariant() is ".pal" or ".act" or ".gpl";

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
}
