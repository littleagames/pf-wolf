namespace Wolf3D.Assets;

/// <summary>
/// A game pack's fonts (gamepacks/{pack}/fonts.yaml), by name. A pack that builds on a
/// base-pack starts from its fonts; the pack's own definitions replace them name by name.
/// </summary>
internal record FontDefinitionsAsset : Asset
{
    public FontDefinitionsAsset()
    {
    }

    public FontDefinitionsAsset(Dictionary<string, FontDefinition> fonts)
    {
        foreach (var (name, font) in fonts)
            Fonts[name] = font;
    }

    public Dictionary<string, FontDefinition> Fonts { get; } = new(StringComparer.OrdinalIgnoreCase);

    public override void Merge(Asset other)
    {
        if (other is FontDefinitionsAsset otherAsset)
        {
            foreach (var (name, font) in otherAsset.Fonts)
                Fonts[name] = font;
        }
    }
}

/// <summary>One font in fonts.yaml</summary>
internal class FontDefinition
{
    /// <summary>
    /// "vga": a Wolf3D VGAGRAPH font chunk (one-bit glyphs drawn in the text's color).
    /// "graphic": a picture for each character, drawn in the picture's own colors.
    /// </summary>
    public string Type { get; set; } = "vga";

    /// <summary>vga: the font chunk's name in raw-data-map; defaults to the font's own name</summary>
    public string? Source { get; set; }

    /// <summary>
    /// graphic: the picture name for each of <see cref="Chars"/>, with {code} (or {code:000}
    /// for a padded number) standing for the character's code, e.g. "FONTN{code:000}"
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>graphic: the characters <see cref="Pattern"/> makes glyphs for</summary>
    public string? Chars { get; set; }

    /// <summary>graphic: a picture for a character, as well as or instead of the pattern's</summary>
    public Dictionary<string, string> Glyphs { get; set; } = [];

    /// <summary>graphic: how far a character moves along, where it isn't the cell width</summary>
    public Dictionary<string, int> Advances { get; set; } = [];

    /// <summary>graphic: how far every character moves along; defaults to each picture's width</summary>
    public int? CellWidth { get; set; }

    /// <summary>graphic: glyph height; defaults to the tallest picture</summary>
    public int? Height { get; set; }

    /// <summary>graphic: distance between lines; defaults to the height</summary>
    public int? LineHeight { get; set; }

    /// <summary>graphic: how far a character without a glyph moves along; defaults to the space's, or the cell width</summary>
    public int? SpaceWidth { get; set; }

    /// <summary>graphic: lower case letters use the upper case glyphs</summary>
    public bool UpperCase { get; set; }
}
