using System.Globalization;
using System.Text.RegularExpressions;
using Wolf3D.Assets;
using Wolf3D.Fonts;

namespace Wolf3D.Managers;

/// <summary>
/// Finds fonts by name: the game pack's font definitions (gamepacks/{pack}/fonts.yaml) first,
/// then any font registered in code, then a Wolf3D font of that name (a VGAGRAPH font chunk or
/// a file in the pk3's fonts/ folder), so "SmallFont" and "LargeFont" work without a
/// definition. Each font is built once, the first time it's used.
/// </summary>
internal class FontManager
{
    public FontManager(Lazy<AssetManager> assetManager, VideoManager videoManager)
    {
        this.assetManager = assetManager;
        this.videoManager = videoManager;
    }

    private readonly Lazy<AssetManager> assetManager;
    private readonly VideoManager videoManager;
    private readonly Dictionary<string, Font> fonts = new(StringComparer.OrdinalIgnoreCase);
    private FontDefinitionsAsset? definitions;
    private bool definitionsRead;

    public void Register(string name, Font font) => fonts[name] = font;

    public Font? Find(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (fonts.TryGetValue(name, out var font))
            return font;

        FontDefinition? definition = null;
        font = Definitions?.Fonts.TryGetValue(name, out definition) == true
            ? Build(name, definition)
            : FindVgaFont(name);
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

        fonts[name] = font;
        return font;
    }

    private FontDefinitionsAsset? Definitions
    {
        get
        {
            if (!definitionsRead)
            {
                definitionsRead = true;
                if (assetManager.Value.Exists<FontDefinitionsAsset>($"{GamePackId}/fonts"))
                    definitions = assetManager.Value.FindInGamePack<FontDefinitionsAsset>("fonts");
            }
            return definitions;
        }
    }

    private string GamePackId => assetManager.Value.GamePackId;

    private VgaFont? FindVgaFont(string source)
    {
        var asset = assetManager.Value.Find<FontAsset>(source);
        return asset == null ? null : new VgaFont(asset);
    }

    private Font? Build(string name, FontDefinition definition)
    {
        switch (definition.Type.ToLowerInvariant())
        {
            case "vga":
                return FindVgaFont(string.IsNullOrEmpty(definition.Source) ? name : definition.Source);

            case "graphic":
                return BuildGraphicFont(name, definition);

            case "sheet":
                return BuildSheetFont(name, definition);

            default:
                Console.WriteLine($"Font '{name}' has an unknown type '{definition.Type}' (vga, graphic or sheet)");
                return null;
        }
    }

    private byte? TransparentIndex(FontDefinition definition)
        => string.IsNullOrEmpty(definition.Transparent) ? null : videoManager.ResolveColor(definition.Transparent);

    private GraphicAsset? FindPicture(string picName)
        => assetManager.Value.Exists<GraphicAsset>(picName) ? assetManager.Value.Find<GraphicAsset>(picName) : null;

    private static readonly Regex CodePlaceholder = new(@"\{code(?::([^}]*))?\}");

    private GraphicFont BuildGraphicFont(string name, FontDefinition definition)
    {
        // Pattern first, then the glyphs listed one by one, which win
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
                Console.WriteLine($"Font '{name}': glyph key '{key}' must be a single character");
        }

        var glyphs = new Dictionary<char, GraphicFont.Glyph>();
        var missing = new List<string>();
        foreach (var (ch, picName) in pics)
        {
            var pic = FindPicture(picName);
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
            Console.WriteLine($"Font '{name}': no picture for {string.Join(", ", missing)}");

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
            Console.WriteLine($"Font '{name}': a sheet needs an image and a cell-width");
            return null;
        }

        var image = FindPicture(definition.Image);
        if (image == null)
        {
            Console.WriteLine($"Font '{name}': no picture '{definition.Image}'");
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
