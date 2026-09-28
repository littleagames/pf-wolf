using Wolf3D.Assets;
using Wolf3D.Managers;

namespace Wolf3D.Fonts;

/// <summary>
/// A font made of pictures, one per character, like the status bar numbers (FONTN*) or the
/// intermission letters (FONTL*). The pictures carry their own colors, so the text color is
/// ignored.
/// </summary>
internal sealed class GraphicFont : Font
{
    /// <summary>A character's picture (none for a blank, like a space) and how far it moves the pen</summary>
    public record Glyph(GraphicAsset? Pic, int Advance);

    private readonly Dictionary<char, Glyph> _glyphs;
    private readonly int _height;
    private readonly int _lineHeight;
    private readonly bool _upperCase;

    /// <param name="height">Glyph height; defaults to the tallest picture</param>
    /// <param name="lineHeight">Distance between lines; defaults to <paramref name="height"/></param>
    /// <param name="spaceWidth">
    /// How far a character the font lacks moves the pen; defaults to the space glyph's advance,
    /// or the widest glyph's if there's no space
    /// </param>
    /// <param name="upperCase">Draw lower case letters with the upper case glyphs</param>
    public GraphicFont(IReadOnlyDictionary<char, Glyph> glyphs, int? height = null, int? lineHeight = null,
        int? spaceWidth = null, bool upperCase = false)
    {
        _glyphs = new Dictionary<char, Glyph>(glyphs);
        _height = height ?? (_glyphs.Count == 0 ? 0 : _glyphs.Values.Max(g => g.Pic?.Height ?? 0));
        _lineHeight = lineHeight ?? _height;
        _upperCase = upperCase;
        SpaceWidth = spaceWidth
            ?? (_glyphs.TryGetValue(' ', out var space) ? space.Advance
            : _glyphs.Count == 0 ? 0 : _glyphs.Values.Max(g => g.Advance));
    }

    public int SpaceWidth { get; }

    public override int Height => _height;

    public override int LineHeight => _lineHeight;

    public override int Advance(char ch) => FindGlyph(ch)?.Advance ?? SpaceWidth;

    public override void Draw(VideoManager video, int x, int y, string text, string color)
    {
        foreach (char ch in text)
        {
            var glyph = FindGlyph(ch);
            if (glyph?.Pic is GraphicAsset pic)
                video.MemToScreen(pic.RawData, pic.Width, pic.Height, x, y);
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
