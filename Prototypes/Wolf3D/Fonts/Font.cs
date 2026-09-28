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

    /// <summary>A copy of the text drawn behind it, offset, in one color; none by default</summary>
    public FontShadow? Shadow { get; set; }

    /// <summary>Draws a line of text, with its shadow first if the font has one</summary>
    public void Draw(VideoManager video, int x, int y, string text, string color) => Draw(video, x, y, text, color, Shadow);

    /// <summary>Draws a line of text with <paramref name="shadow"/> behind it in place of the font's own; none for null</summary>
    public void Draw(VideoManager video, int x, int y, string text, string color, FontShadow? shadow)
    {
        if (shadow != null && shadow != FontShadow.None)
            DrawText(video, x + shadow.X, y + shadow.Y, text, shadow.Color, silhouette: true);
        DrawText(video, x, y, text, color, silhouette: false);
    }

    /// <param name="silhouette">Draw every pixel of the glyphs in <paramref name="color"/>, even for a font whose glyphs have their own colors</param>
    protected abstract void DrawText(VideoManager video, int x, int y, string text, string color, bool silhouette);

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

/// <summary>A text shadow: how far right and down of the text it sits, and its color</summary>
internal record FontShadow(int X, int Y, string Color)
{
    /// <summary>For a <see cref="TextStyle"/>: no shadow, even if the font has one</summary>
    public static readonly FontShadow None = new(0, 0, "");

    /// <summary>One pixel right and down, in black</summary>
    public static readonly FontShadow Default = new(1, 1, "Black");
}
