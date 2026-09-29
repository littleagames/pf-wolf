using Wolf3D.Assets;
using Wolf3D.Managers;

namespace Wolf3D.Fonts;

/// <summary>
/// A font made of pictures: one per character, like the status bar numbers (FONTN*) or the
/// intermission letters (FONTL*), or cells cut from one sheet of characters. The pictures carry
/// their own colors, unless the font colorizes them in the text's color.
/// </summary>
internal sealed class GraphicFont : Font
{
    /// <summary>
    /// A character's picture: the part of <paramref name="Image"/> from (SrcX, SrcY), Width by
    /// Height; no image for a blank, like a space. <paramref name="Advance"/> is how far it moves the pen.
    /// </summary>
    public record Glyph(GraphicAsset? Image, int SrcX, int SrcY, int Width, int Height, int Advance)
    {
        /// <summary>A whole picture as the glyph</summary>
        public Glyph(GraphicAsset? pic, int advance) : this(pic, 0, 0, pic?.Width ?? 0, pic?.Height ?? 0, advance)
        {
        }
    }

    private readonly Dictionary<char, Glyph> _glyphs;
    private readonly int _height;
    private readonly int _lineHeight;
    private readonly bool _upperCase;
    private readonly bool _colorize;
    private readonly byte? _transparent;

    /// <param name="height">Glyph height; defaults to the tallest glyph</param>
    /// <param name="lineHeight">Distance between lines; defaults to <paramref name="height"/></param>
    /// <param name="spaceWidth">
    /// How far a character the font lacks moves the pen; defaults to the space glyph's advance,
    /// or the widest glyph's if there's no space
    /// </param>
    /// <param name="upperCase">Draw lower case letters with the upper case glyphs</param>
    /// <param name="colorize">Draw every pixel of a glyph in the text's color</param>
    /// <param name="transparent">A palette index that isn't drawn, so what's behind shows through</param>
    public GraphicFont(IReadOnlyDictionary<char, Glyph> glyphs, int? height = null, int? lineHeight = null,
        int? spaceWidth = null, bool upperCase = false, bool colorize = false, byte? transparent = null)
    {
        _glyphs = new Dictionary<char, Glyph>(glyphs);
        _height = height ?? (_glyphs.Count == 0 ? 0 : _glyphs.Values.Max(g => g.Height));
        _lineHeight = lineHeight ?? _height;
        _upperCase = upperCase;
        _colorize = colorize;
        _transparent = transparent;
        SpaceWidth = spaceWidth
            ?? (_glyphs.TryGetValue(' ', out var space) ? space.Advance
            : _glyphs.Count == 0 ? 0 : _glyphs.Values.Max(g => g.Advance));
    }

    public int SpaceWidth { get; }

    public override int Height => _height;

    public override int LineHeight => _lineHeight;

    public override int Advance(char ch) => FindGlyph(ch)?.Advance ?? SpaceWidth;

    protected override void DrawText(VideoManager video, int x, int y, string text, string color, bool silhouette, byte[]? rowColors)
    {
        foreach (char ch in text)
        {
            var glyph = FindGlyph(ch);
            if (glyph?.Image is GraphicAsset image)
            {
                // A gradient only shades glyphs drawn in the text's color
                video.DrawImageRegion(image.RawData, image.OpacityMask, image.Width, glyph.SrcX, glyph.SrcY,
                    glyph.Width, glyph.Height, x, y, _transparent, _colorize || silhouette ? color : null,
                    _colorize ? rowColors : null);
            }
            x += glyph?.Advance ?? SpaceWidth;
        }
    }

    private Glyph? FindGlyph(char ch)
    {
        if (_upperCase)
            ch = char.ToUpperInvariant(ch);
        return _glyphs.TryGetValue(ch, out var glyph) ? glyph : null;
    }
}
