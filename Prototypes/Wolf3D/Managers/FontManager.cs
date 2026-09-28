using System.Globalization;
using System.Text.RegularExpressions;
using Wolf3D.Assets;
using Wolf3D.Fonts;

namespace Wolf3D.Managers;

/// <summary>
/// Finds fonts by name: the game pack's font definitions (gamepacks/{pack}/fonts.yaml) first,
/// then any font registered in code, then a VGAGRAPH font chunk of that name, so "SmallFont"
/// and "LargeFont" work without a definition. Each font is built once, the first time it's used.
/// </summary>
internal class FontManager
{
    public FontManager(Lazy<AssetManager> assetManager)
    {
        this.assetManager = assetManager;
    }

    private readonly Lazy<AssetManager> assetManager;
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

        font = Definitions?.Fonts.TryGetValue(name, out var definition) == true
            ? Build(name, definition)
            : FindVgaFont(name);
        if (font == null)
            return null;

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

            default:
                Console.WriteLine($"Font '{name}' has an unknown type '{definition.Type}' (vga or graphic)");
                return null;
        }
    }

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
            var pic = assetManager.Value.Exists<GraphicAsset>(picName) ? assetManager.Value.Find<GraphicAsset>(picName) : null;
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
            definition.SpaceWidth ?? (glyphs.ContainsKey(' ') ? null : definition.CellWidth), definition.UpperCase);
    }

    private static int? AdvanceFor(FontDefinition definition, char ch)
        => definition.Advances.TryGetValue(ch.ToString(), out int advance) ? advance : null;
}
