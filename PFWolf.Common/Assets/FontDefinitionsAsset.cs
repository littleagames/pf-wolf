namespace PFWolf.Assets;

/// <summary>
/// A game pack's fonts (gamepacks/{pack}/fonts.yaml), by name. A pack that builds on a
/// base-pack starts from its fonts; the pack's own definitions replace them name by name.
/// </summary>
public record FontDefinitionsAsset : Asset
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
}

/// <summary>A font's shadow in fonts.yaml; `shadow: {}` is one pixel right and down, in black</summary>
public class ShadowDefinition
{
    /// <summary>How far right of the text the shadow sits (negative for left)</summary>
    public int X { get; set; } = 1;

    /// <summary>How far down from the text the shadow sits (negative for up)</summary>
    public int Y { get; set; } = 1;

    /// <summary>A color name, #RRGGBB or palette index</summary>
    public string Color { get; set; } = "Black";
}

/// <summary>
/// A font's gradient in fonts.yaml: percent toward white (positive) or black (negative) at the
/// top and bottom rows of the glyphs. `gradient: {}` is 40% lighter to 40% darker.
/// </summary>
public class GradientDefinition
{
    /// <summary>Percent toward white (positive) or black (negative) at the glyphs' top row</summary>
    public int Top { get; set; } = 40;

    /// <summary>Percent toward white (positive) or black (negative) at the glyphs' bottom row</summary>
    public int Bottom { get; set; } = -40;
}

/// <summary>A font's outline in fonts.yaml; `outline: {}` is one pixel of black all round</summary>
public class OutlineDefinition
{
    /// <summary>A color name, #RRGGBB or palette index</summary>
    public string Color { get; set; } = "Black";

    /// <summary>How many pixels wide</summary>
    public int Thickness { get; set; } = 1;

    /// <summary>Fill the corners too; false leaves them out, for a rounder look</summary>
    public bool Diagonals { get; set; } = true;
}

/// <summary>A font's glow in fonts.yaml; `glow: {}` is two rings in the text's color, from 50%</summary>
public class GlowDefinition
{
    /// <summary>A color name, #RRGGBB or palette index; left out, the text's own color</summary>
    public string? Color { get; set; }

    /// <summary>How many pixels (rings) it spreads out</summary>
    public int Radius { get; set; } = 2;

    /// <summary>Percent of the glow color in the nearest ring; the rest fade toward the background</summary>
    public int Strength { get; set; } = 50;
}

/// <summary>
/// One font in fonts.yaml. Shadows, outlines and glows reach past the letters and paint over
/// whatever is next to the text, such as the border of a tight box round it: keep them small on
/// fonts used in boxes. They're drawn as the glyphs' silhouettes, so a picture glyph needs
/// see-through pixels (a PNG's, or transparent) or it casts a filled box.
/// </summary>
public class FontDefinition
{
    /// <summary>
    /// "vga": a Wolf3D font (one-bit glyphs drawn in the text's color), a VGAGRAPH font chunk or a
    /// font file in the pk3's fonts/ folder.
    /// "graphic": a picture for each character, drawn in the picture's own colors.
    /// "sheet": one picture with the characters in a grid of cells.
    /// </summary>
    public string Type { get; set; } = "vga";

    /// <summary>
    /// vga: the font chunk's name in raw-data-map, or a font file's name in fonts/ (Wolf3D's font
    /// format, or WDC's .wsf); defaults to the font's own name. A file in fonts/ named like a
    /// chunk (SmallFont.fnt) replaces the chunk.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>Any type: a copy of the text drawn behind it, offset, in one color</summary>
    public ShadowDefinition? Shadow { get; set; }

    /// <summary>
    /// vga, or colorized graphic and sheet: the text's color shaded lighter to darker down the glyphs
    /// </summary>
    public GradientDefinition? Gradient { get; set; }

    /// <summary>
    /// Any type: a border in one color around each glyph. Characters don't move further apart,
    /// so outlines fill the gaps between them.
    /// </summary>
    public OutlineDefinition? Outline { get; set; }

    /// <summary>
    /// Any type: a halo round the glyphs fading into the background color behind the text (the
    /// text style's, such as BKGDCOLOR in menus). It's drawn in solid colors, so over a picture it
    /// covers it rather than showing it through.
    /// </summary>
    public GlowDefinition? Glow { get; set; }

    /// <summary>sheet: the picture holding the characters</summary>
    public string? Image { get; set; }

    /// <summary>sheet: height of a cell in the picture; defaults to <see cref="CellWidth"/></summary>
    public int? CellHeight { get; set; }

    /// <summary>
    /// sheet: the character in the first cell, when there's no <see cref="Chars"/>; the rest
    /// follow in order, left to right and down. Defaults to 32, a space.
    /// </summary>
    public int FirstChar { get; set; } = 32;

    /// <summary>
    /// sheet: each character moves along by its own width plus spacing, not the cell's. A blank
    /// cell is left out, except a space, which moves along space-width (else half a cell).
    /// </summary>
    public bool Proportional { get; set; }

    /// <summary>sheet, proportional: the gap after each character; defaults to 1</summary>
    public int Spacing { get; set; } = 1;

    /// <summary>graphic, sheet: draw the glyphs in the text's color instead of their own</summary>
    public bool Colorize { get; set; }

    /// <summary>
    /// graphic, sheet: a color (name, #RRGGBB or palette index) left out when drawing, so what's
    /// behind the text shows through. A PNG's see-through pixels always are.
    /// </summary>
    public string? Transparent { get; set; }

    /// <summary>
    /// graphic: the picture name for each of <see cref="Chars"/>, with {code} (or {code:000}
    /// for a padded number) standing for the character's code, e.g. "FONTN{code:000}"
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// graphic: the characters <see cref="Pattern"/> makes glyphs for.
    /// sheet: the character in each cell, left to right and down.
    /// </summary>
    public string? Chars { get; set; }

    /// <summary>graphic: a picture for a character, as well as or instead of the pattern's</summary>
    public Dictionary<string, string> Glyphs { get; set; } = [];

    /// <summary>
    /// graphic, sheet: how far a character moves along, where it isn't the usual. In a graphic
    /// font, a character given here with no picture is a blank that wide.
    /// </summary>
    public Dictionary<string, int> Advances { get; set; } = [];

    /// <summary>
    /// graphic: how far every character moves along; defaults to each picture's width.
    /// sheet (required): width of a cell in the picture, and how far a character moves along.
    /// </summary>
    public int? CellWidth { get; set; }

    /// <summary>graphic, sheet: glyph height; defaults to the tallest picture (a sheet's, the cell height)</summary>
    public int? Height { get; set; }

    /// <summary>graphic, sheet: distance between lines; defaults to the height</summary>
    public int? LineHeight { get; set; }

    /// <summary>graphic, sheet: how far a character without a glyph moves along; defaults to the space's, or the cell width</summary>
    public int? SpaceWidth { get; set; }

    /// <summary>Any type: lower case letters use the upper case glyphs (Blake Stone's small font has only capitals)</summary>
    public bool UpperCase { get; set; }
}
