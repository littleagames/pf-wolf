using Wolf3D.Managers;

namespace Wolf3D.Fonts;

/// <summary>
/// Something text is drawn with: a Wolf3D VGAGRAPH font (<see cref="VgaFont"/>) or a set of
/// pictures, one per character (<see cref="GraphicFont"/>). Coordinates are in the 320x200
/// virtual screen. A font draws a single line; newlines are up to the caller (<see cref="TextWindow"/>).
/// </summary>
internal abstract class Font
{
    /// <summary>Height of the glyphs</summary>
    public abstract int Height { get; }

    /// <summary>How far down the next line of text starts</summary>
    public virtual int LineHeight => Height;

    /// <summary>
    /// How far across the next character starts after <paramref name="ch"/>. A character the
    /// font doesn't have isn't drawn, but still takes up room so text doesn't run together.
    /// </summary>
    public abstract int Advance(char ch);

    public abstract void Draw(VideoManager video, int x, int y, string text, string color);

    /// <summary>Width of a line of text</summary>
    public int Measure(string text)
    {
        int width = 0;
        foreach (char ch in text)
            width += Advance(ch);
        return width;
    }

    /// <summary>How many characters from the start of <paramref name="text"/> fit in <paramref name="maxWidth"/></summary>
    public int Fit(string text, int maxWidth)
    {
        int width = 0, length = 0;
        while (length < text.Length && width + Advance(text[length]) <= maxWidth)
            width += Advance(text[length++]);
        return length;
    }
}
