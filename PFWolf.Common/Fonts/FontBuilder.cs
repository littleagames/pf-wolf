using System.Globalization;
using System.Text.RegularExpressions;
using PFWolf.Assets;

namespace PFWolf.Fonts;

/// <summary>
/// Makes a font from its fonts.yaml definition, or, with none, from the Wolf3D font of its name
/// (a VGAGRAPH font chunk or a file in a pk3's fonts/ folder), as the game does: the game's
/// FontManager and the editor's font window both build them here.
/// </summary>
/// <param name="findFont">A Wolf3D font asset by name, or null</param>
/// <param name="findPicture">A picture by name, or null</param>
/// <param name="resolveColor">A color name, #RRGGBB or palette index as its palette index (for transparent)</param>
/// <param name="warn">Where problems with a definition are reported</param>
public sealed class FontBuilder(Func<string, FontAsset?> findFont, Func<string, GraphicAsset?> findPicture,
    Func<string, byte> resolveColor, Action<string> warn)
{
    /// <summary>The character the typing cursor is drawn as (Wolf3D's cursor block)</summary>
    public const char CursorChar = '\x80';

    /// <summary>
    /// The font named <paramref name="name"/>: built from <paramref name="definition"/> with its
    /// shadow, gradient, outline and glow, or the vga font of that name when there's no
    /// definition; null when there's nothing to build it from
    /// </summary>
    public Font? Build(string name, FontDefinition? definition)
    {
        var font = definition != null ? BuildType(name, definition) : FindVgaFont(name);
        if (font == null)
            return null;

        if (definition?.Shadow is { } shadow)
            font.Shadow = new FontShadow(shadow.X, shadow.Y, shadow.Color);
        if (definition?.Gradient is { } gradient)
            font.Gradient = new FontGradient(gradient.Top, gradient.Bottom);
        if (definition?.Outline is { } outline)
            font.Outline = new FontOutline(outline.Color, Math.Max(outline.Thickness, 1), outline.Diagonals);
        if (definition?.Glow is { } glow)
            font.Glow = new FontGlow(glow.Color, Math.Max(glow.Radius, 1), Math.Clamp(glow.Strength, 0, 100));
        return font;
    }

    private Font? BuildType(string name, FontDefinition definition)
    {
        switch (definition.Type.ToLowerInvariant())
        {
            case "vga":
                return FindVgaFont(string.IsNullOrEmpty(definition.Source) ? name : definition.Source, definition.UpperCase, definition.Cursor);

            case "graphic":
                return BuildGraphicFont(name, definition);

            case "sheet":
                return BuildSheetFont(name, definition);

            default:
                warn($"Font '{name}' has an unknown type '{definition.Type}' (vga, graphic or sheet)");
                return null;
        }
    }

    /// <summary>
    /// A vga font asset with lower case letters on the upper case glyphs (Blake Stone's small
    /// font has only capitals), and the typing cursor (character 128) on another character's
    /// glyph (Blake Stone's "@")
    /// </summary>
    public static FontAsset Adjusted(FontAsset asset, bool upperCase, string? cursor)
    {
        if (!upperCase && string.IsNullOrEmpty(cursor))
            return asset;
        var location = (short[])asset.Location.Clone();
        var width = (byte[])asset.Width.Clone();
        if (upperCase)
            for (char ch = 'a'; ch <= 'z'; ch++)
            {
                location[ch] = location[char.ToUpperInvariant(ch)];
                width[ch] = width[char.ToUpperInvariant(ch)];
            }
        if (!string.IsNullOrEmpty(cursor) && cursor[0] < 256)
        {
            location[CursorChar] = location[cursor[0]];
            width[CursorChar] = width[cursor[0]];
        }
        return asset with { Location = location, Width = width };
    }

    private VgaFont? FindVgaFont(string source, bool upperCase = false, string? cursor = null)
        => findFont(source) is { } asset ? new VgaFont(Adjusted(asset, upperCase, cursor)) : null;

    private byte? TransparentIndex(FontDefinition definition)
        => string.IsNullOrEmpty(definition.Transparent) ? null : resolveColor(definition.Transparent);

    private static readonly Regex CodePlaceholder = new(@"\{code(?::([^}]*))?\}");

    /// <summary>
    /// The picture name for each character of a graphic font: the pattern's for its chars, then
    /// the glyphs listed one by one, which win (a key that isn't one character is reported and skipped)
    /// </summary>
    public static Dictionary<char, string> GlyphPictures(FontDefinition definition, Action<string>? warn = null, string name = "")
    {
        var pics = new Dictionary<char, string>();
        if (!string.IsNullOrEmpty(definition.Pattern))
        {
            foreach (char ch in definition.Chars ?? "")
                pics[ch] = CodePlaceholder.Replace(definition.Pattern,
                    m => ((int)ch).ToString(m.Groups[1].Success ? m.Groups[1].Value : "", CultureInfo.InvariantCulture));
        }
        foreach (var (key, pic) in definition.Glyphs)
        {
            if (key.Length == 1)
                pics[key[0]] = pic;
            else
                warn?.Invoke($"Font '{name}': glyph key '{key}' must be a single character");
        }
        return pics;
    }

    private GraphicFont BuildGraphicFont(string name, FontDefinition definition)
    {
        var pics = GlyphPictures(definition, warn, name);

        var glyphs = new Dictionary<char, GraphicFont.Glyph>();
        var missing = new List<string>();
        foreach (var (ch, picName) in pics)
        {
            var pic = findPicture(picName);
            if (pic == null)
            {
                missing.Add(picName);
                continue;
            }
            glyphs[ch] = new GraphicFont.Glyph(pic, AdvanceFor(definition, ch) ?? definition.CellWidth ?? pic.Width);
        }

        // A character with an advance but no picture is a blank of that width
        foreach (var (key, advance) in definition.Advances)
        {
            if (key.Length == 1 && !glyphs.ContainsKey(key[0]))
                glyphs[key[0]] = new GraphicFont.Glyph(null, advance);
        }

        if (missing.Count > 0)
            warn($"Font '{name}': no picture for {string.Join(", ", missing)}");

        return new GraphicFont(glyphs, definition.Height, definition.LineHeight,
            definition.SpaceWidth ?? (glyphs.ContainsKey(' ') ? null : definition.CellWidth), definition.UpperCase,
            definition.Colorize, TransparentIndex(definition));
    }

    /// <summary>
    /// A font cut from one picture: a grid of CellWidth by CellHeight cells, left to right and
    /// down, holding Chars (or FirstChar onwards). A proportional sheet trims each glyph to the
    /// columns it draws in; a blank cell there is a gap, and only a space keeps one.
    /// </summary>
    private GraphicFont? BuildSheetFont(string name, FontDefinition definition)
    {
        if (string.IsNullOrEmpty(definition.Image) || definition.CellWidth is not int cellWidth || cellWidth <= 0)
        {
            warn($"Font '{name}': a sheet needs an image and a cell-width");
            return null;
        }

        var image = findPicture(definition.Image);
        if (image == null)
        {
            warn($"Font '{name}': no picture '{definition.Image}'");
            return null;
        }

        int cellHeight = definition.CellHeight ?? cellWidth;
        int columns = image.Width / cellWidth;
        int cells = columns * (image.Height / cellHeight);
        string chars = definition.Chars
            ?? new string(Enumerable.Range(0, cells).Select(i => (char)(definition.FirstChar + i)).ToArray());

        byte? transparent = TransparentIndex(definition);
        bool Shows(int x, int y)
        {
            int i = y * image.Width + x;
            return (image.OpacityMask == null || image.OpacityMask[i] != 0) && image.RawData[i] != transparent;
        }

        var glyphs = new Dictionary<char, GraphicFont.Glyph>();
        for (int cell = 0; cell < Math.Min(chars.Length, cells); cell++)
        {
            char ch = chars[cell];
            int sx = (cell % columns) * cellWidth;
            int sy = (cell / columns) * cellHeight;
            int? advance = AdvanceFor(definition, ch);

            if (!definition.Proportional)
            {
                glyphs[ch] = new GraphicFont.Glyph(image, sx, sy, cellWidth, cellHeight, advance ?? cellWidth);
                continue;
            }

            // The columns of the cell with something in them
            int first = -1, last = -1;
            for (int x = 0; x < cellWidth; x++)
            {
                for (int y = 0; y < cellHeight; y++)
                {
                    if (!Shows(sx + x, sy + y))
                        continue;
                    if (first < 0)
                        first = x;
                    last = x;
                    break;
                }
            }

            if (first < 0)
            {
                if (ch == ' ')
                    glyphs[ch] = new GraphicFont.Glyph(null, advance ?? definition.SpaceWidth ?? cellWidth / 2);
                continue;
            }

            int width = last - first + 1;
            glyphs[ch] = new GraphicFont.Glyph(image, sx + first, sy, width, cellHeight, advance ?? width + definition.Spacing);
        }

        return new GraphicFont(glyphs, definition.Height ?? cellHeight, definition.LineHeight, definition.SpaceWidth,
            definition.UpperCase, definition.Colorize, transparent);
    }

    private static int? AdvanceFor(FontDefinition definition, char ch)
        => definition.Advances.TryGetValue(ch.ToString(), out int advance) ? advance : null;
}
