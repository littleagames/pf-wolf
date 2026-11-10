using System.Globalization;
using System.Text;
using PFWolf.Assets;
using PFWolf.Definitions;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Editor.Data;

/// <summary>
/// How a sheet of characters is cut: cells this big, left to right and down, holding
/// <paramref name="Chars"/> in order, or (with none) the characters from <paramref name="FirstChar"/> on
/// </summary>
public sealed record SheetCells(int CellWidth, int CellHeight, int FirstChar, string? Chars = null)
{
    public int Columns(int imageWidth) => Math.Max(imageWidth / Math.Max(CellWidth, 1), 0);
    public int Rows(int imageHeight) => Math.Max(imageHeight / Math.Max(CellHeight, 1), 0);

    /// <summary>How many characters the sheet holds, stopping at 255 (or the end of <see cref="Chars"/>)</summary>
    public int Count(int imageWidth, int imageHeight)
        => Math.Max(Math.Min(Columns(imageWidth) * Rows(imageHeight), string.IsNullOrEmpty(Chars) ? 256 - FirstChar : Chars.Length), 0);

    /// <summary>The character in a cell</summary>
    public int CharAt(int cell) => string.IsNullOrEmpty(Chars) ? FirstChar + cell : Chars[cell];
}

/// <summary>
/// Fonts as sheets of characters, a PNG of cells: a vga font written as one (with the fonts.yaml
/// entry that reads it as a sheet font), and one read as a vga font or a sheet font
/// </summary>
public static class FontSheets
{
    /// <summary>Where an exported sheet starts: the space (the 32 control codes before it draw nothing)</summary>
    public const int ExportFirstChar = 32;

    /// <summary>
    /// A vga font as a sheet: a cell for each of characters 32-255, 16 to a row, as wide as its
    /// widest glyph, drawn pixels white and the rest see-through; and the fonts.yaml entry that
    /// prints in it (a colorized sheet, each character moving along its own width)
    /// </summary>
    public static (byte[] Png, string Yaml) Export(FontAsset font, string name, IReadOnlyList<PaletteColor> palette)
    {
        int cellWidth = Math.Max(Enumerable.Range(ExportFirstChar, 256 - ExportFirstChar).Max(ch => (int)font.Width[ch]), 1);
        int cellHeight = Math.Max((int)font.Height, 1);
        int count = 256 - ExportFirstChar, columns = 16, rows = (count + columns - 1) / columns;
        int width = cellWidth * columns, height = cellHeight * rows;

        var white = new Palette { Colors = [.. palette] }.ClosestIndex(255, 255, 255);
        var indices = new byte[width * height];
        var opaque = new bool[width * height];
        for (int i = 0; i < count; i++)
        {
            int ch = ExportFirstChar + i, glyphWidth = font.Width[ch];
            var glyph = font.Glyph(ch);
            int left = i % columns * cellWidth, top = i / columns * cellHeight;
            for (int y = 0; y < cellHeight; y++)
                for (int x = 0; x < glyphWidth; x++)
                {
                    if (glyph[y * glyphWidth + x] == 0)
                        continue;
                    int at = (top + y) * width + left + x;
                    indices[at] = white;
                    opaque[at] = true;
                }
        }
        var png = PngFile.Write(new IndexedPicture(width, height, indices, opaque), palette);

        var yaml = new StringBuilder();
        yaml.Append(CultureInfo.InvariantCulture, $"{name}:\n  type: sheet\n  image: {name}\n  cell-width: {cellWidth}\n  cell-height: {cellHeight}\n");
        yaml.Append(CultureInfo.InvariantCulture, $"  first-char: {ExportFirstChar}\n  colorize: true\n");
        var advances = Enumerable.Range(ExportFirstChar, count).Where(ch => font.Width[ch] != cellWidth).ToList();
        if (advances.Count > 0)
        {
            yaml.Append("  advances:\n");
            foreach (var ch in advances)
                yaml.Append(CultureInfo.InvariantCulture, $"    {Quote((char)ch)}: {font.Width[ch]}\n");
        }
        return (png, yaml.ToString());
    }

    // A character as a YAML key, double-quoted (escaped where it isn't plain ASCII)
    private static string Quote(char ch)
        => ch is >= ' ' and < '\x7f' && ch is not ('"' or '\\') ? $"\"{ch}\"" : $"\"\\x{(int)ch:X2}\"";

    // Character sets sheets usually hold: 32-127, 32-126, 32-255, 0-255, 0-127
    private static readonly int[] UsualCounts = [96, 95, 224, 256, 128];

    /// <summary>
    /// The cells a sheet most likely has: of the cell sizes (4 to 64 pixels) that divide it evenly,
    /// the one holding a usual character set (32-127, 32-255, 0-255) in 16 columns if any does,
    /// else the one whose cells are most like a letter (a little taller than wide). Starting at
    /// 0 for a sheet of 256, else 32.
    /// </summary>
    public static SheetCells Guess(int width, int height)
    {
        SheetCells? best = null;
        double bestScore = double.MaxValue;
        for (int cellWidth = 4; cellWidth <= Math.Min(64, width); cellWidth++)
        {
            if (width % cellWidth != 0)
                continue;
            for (int cellHeight = 4; cellHeight <= Math.Min(64, height); cellHeight++)
            {
                if (height % cellHeight != 0)
                    continue;
                int columns = width / cellWidth, cells = columns * (height / cellHeight);
                double score = Math.Abs(cellHeight / (double)cellWidth - 1.3);
                if (UsualCounts.Any(count => count <= cells && cells < count + columns))
                    score -= 1;
                if (columns == 16)
                    score -= 0.5;
                if (columns > 32 || cells < 10)
                    score += 2;
                if (score < bestScore)
                    (best, bestScore) = (new SheetCells(cellWidth, cellHeight, 0), score);
            }
        }
        best ??= new SheetCells(Math.Max(width / 16, 1), Math.Max(height / 6, 1), 0);
        int total = best.Columns(width) * best.Rows(height);
        return best with { FirstChar = total >= 256 ? 0 : 32 };
    }

    // A pixel the font draws: one that shows (alpha half or more), isn't the see-through color and is at least so bright
    private static bool Draws(RgbaImage image, int x, int y, int threshold, (byte R, byte G, byte B)? key)
    {
        var (r, g, b, a) = image[x, y];
        return a >= 128 && (r, g, b) != key && (r * 299 + g * 587 + b * 114) / 1000 >= threshold;
    }

    /// <summary>A sheet's top-left pixel's color, the usual see-through color of a sheet with no transparency, or null when it's see-through already</summary>
    public static (byte R, byte G, byte B)? CornerColor(RgbaImage image)
        => image.Width > 0 && image.Height > 0 && image[0, 0] is var (r, g, b, a) && a >= 128 ? (r, g, b) : null;

    /// <summary>Whether any of a picture's pixels are see-through</summary>
    public static bool HasTransparency(RgbaImage image)
    {
        for (int i = 3; i < image.Pixels.Length; i += 4)
            if (image.Pixels[i] < 128)
                return true;
        return false;
    }

    /// <summary>
    /// A sheet read as a vga font: a pixel draws when it shows and is at least
    /// <paramref name="threshold"/> bright (0-255); each glyph trimmed to the columns it draws in,
    /// plus <paramref name="spacing"/>; a blank cell is no glyph, but a space, half a cell.
    /// Pixels of the <paramref name="key"/> color are see-through.
    /// </summary>
    public static FontDocument ToVgaFont(string name, RgbaImage image, SheetCells cells, int threshold, int spacing, (byte R, byte G, byte B)? key = null)
    {
        var font = FontDocument.Blank(name, Math.Clamp(cells.CellHeight, 1, 255), Math.Max(cells.CellWidth / 2, 1));
        int columns = cells.Columns(image.Width);
        for (int i = 0; i < cells.Count(image.Width, image.Height); i++)
        {
            int ch = cells.CharAt(i), left = i % columns * cells.CellWidth, top = i / columns * cells.CellHeight;
            if (ch > 255)
                continue;
            int first = -1, last = -1;
            for (int x = 0; x < cells.CellWidth; x++)
                for (int y = 0; y < cells.CellHeight; y++)
                    if (Draws(image, left + x, top + y, threshold, key))
                    {
                        if (first < 0)
                            first = x;
                        last = x;
                        break;
                    }
            if (first < 0)
                continue;       // blank: the space keeps the width it was given
            int width = Math.Min(last - first + 1 + spacing, 255), height = font.Height;
            var pixels = new byte[width * height];
            for (int y = 0; y < Math.Min(cells.CellHeight, height); y++)
                for (int x = 0; x <= last - first; x++)
                    if (Draws(image, left + first + x, top + y, threshold, key))
                        pixels[y * width + x] = 1;
            font.Paste(ch, new GlyphData(width, height, pixels));
        }
        return font;
    }

    /// <summary>A sheet font's fonts.yaml entry, its picture named <paramref name="image"/>; <paramref name="key"/> is left out when it's drawn</summary>
    public static YamlMappingNode SheetDefinition(string image, SheetCells cells, bool proportional, bool colorize, (byte R, byte G, byte B)? key = null)
    {
        var definition = new YamlMappingNode
        {
            { "type", FontSchema.Sheet },
            { "image", image },
            { "cell-width", cells.CellWidth.ToString(CultureInfo.InvariantCulture) },
            { "cell-height", cells.CellHeight.ToString(CultureInfo.InvariantCulture) },
        };
        if (string.IsNullOrEmpty(cells.Chars))
            definition.Add("first-char", cells.FirstChar.ToString(CultureInfo.InvariantCulture));
        else
            definition.Add("chars", new YamlScalarNode(cells.Chars) { Style = YamlDotNet.Core.ScalarStyle.DoubleQuoted });
        if (proportional)
            definition.Add("proportional", "true");
        if (colorize)
            definition.Add("colorize", "true");
        if (key is var (r, g, b))
            definition.Add("transparent", new YamlScalarNode($"#{r:X2}{g:X2}{b:X2}") { Style = YamlDotNet.Core.ScalarStyle.DoubleQuoted });
        return definition;
    }

    /// <summary>A picture as the game reads a PNG's (each color matched to the palette; see-through where its alpha is under half)</summary>
    public static GraphicAsset ToPicture(RgbaImage image, PaletteColor[] palette)
    {
        var match = new Palette { Colors = palette };
        var cache = new Dictionary<uint, byte>();
        var indices = new byte[image.Width * image.Height];
        var mask = new byte[image.Width * image.Height];
        bool anyClear = false;
        for (int i = 0; i < indices.Length; i++)
        {
            byte r = image.Pixels[i * 4], g = image.Pixels[i * 4 + 1], b = image.Pixels[i * 4 + 2], a = image.Pixels[i * 4 + 3];
            uint key = (uint)(r << 16 | g << 8 | b);
            if (!cache.TryGetValue(key, out var index))
                cache[key] = index = match.ClosestIndex(r, g, b);
            indices[i] = index;
            if (a >= 128)
                mask[i] = 1;
            else
                anyClear = true;
        }
        return new GraphicAsset(indices, (short)image.Width, (short)image.Height) { OpacityMask = anyClear ? mask : null };
    }
}
