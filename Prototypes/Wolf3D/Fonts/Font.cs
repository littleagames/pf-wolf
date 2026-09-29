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

    /// <summary>
    /// Shading of the text's color from the top of the glyphs to the bottom; none by default.
    /// Only for glyphs drawn in the text's color (vga fonts, and colorized picture fonts).
    /// </summary>
    public FontGradient? Gradient { get; set; }

    /// <summary>A border in one color around each glyph; none by default</summary>
    public FontOutline? Outline { get; set; }

    /// <summary>A halo round the glyphs fading out into the background; none by default</summary>
    public FontGlow? Glow { get; set; }

    /// <summary>
    /// Draws a line of text, with the font's own shadow, outline, gradient and glow; a glow fades
    /// into black, as there's no style saying what's behind the text
    /// </summary>
    public void Draw(VideoManager video, int x, int y, string text, string color)
        => Draw(video, x, y, text, color, Shadow, Gradient, Outline, Glow, "Black");

    /// <summary>
    /// Draws a line of text with <paramref name="glow"/> and <paramref name="shadow"/> behind it,
    /// <paramref name="outline"/> around it and shaded by <paramref name="gradient"/>, in place
    /// of the font's own; none for null. The glow fades into <paramref name="background"/> and,
    /// like the shadow, spreads from the outlined text. None of them change how far characters
    /// move along, so they spread into the gaps between them.
    /// </summary>
    public void Draw(VideoManager video, int x, int y, string text, string color, FontShadow? shadow, FontGradient? gradient,
        FontOutline? outline = null, FontGlow? glow = null, string background = "Black")
    {
        bool outlined = outline != null && outline != FontOutline.None;
        var outlineOffsets = outlined ? outline!.Offsets() : [];

        // Outermost ring first, so each pixel ends up the color of the ring nearest the text
        if (glow != null && glow != FontGlow.None)
        {
            var ringColors = video.GetGlowColors(glow.Color ?? color, background, glow.Radius, glow.Strength);
            var rings = glow.Rings(outlined ? outline!.Thickness : 0);
            for (int ring = rings.Count - 1; ring >= 0; ring--)
                foreach (var (dx, dy) in rings[ring])
                    DrawText(video, x + dx, y + dy, text, ringColors[ring], silhouette: true, rowColors: null);
        }

        if (shadow != null && shadow != FontShadow.None)
        {
            DrawText(video, x + shadow.X, y + shadow.Y, text, shadow.Color, silhouette: true, rowColors: null);
            foreach (var (dx, dy) in outlineOffsets)
                DrawText(video, x + shadow.X + dx, y + shadow.Y + dy, text, shadow.Color, silhouette: true, rowColors: null);
        }

        // Every glyph's outline goes down before any glyph, so one doesn't cover its neighbor
        foreach (var (dx, dy) in outlineOffsets)
            DrawText(video, x + dx, y + dy, text, outline!.Color, silhouette: true, rowColors: null);

        byte[]? rowColors = gradient != null && gradient != FontGradient.None
            ? video.GetGradient(color, Height, gradient.Top, gradient.Bottom)
            : null;
        DrawText(video, x, y, text, color, silhouette: false, rowColors);
    }

    /// <param name="silhouette">Draw every pixel of the glyphs in <paramref name="color"/>, even for a font whose glyphs have their own colors</param>
    /// <param name="rowColors">For glyphs drawn in the text's color, a palette index for each row from the top, in place of it</param>
    protected abstract void DrawText(VideoManager video, int x, int y, string text, string color, bool silhouette, byte[]? rowColors);

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

/// <summary>
/// A border around text: <paramref name="Thickness"/> pixels of <paramref name="Color"/> on
/// every side, including the corners when <paramref name="Diagonals"/> (otherwise only straight
/// up, down, left and right, for a rounder look)
/// </summary>
internal record FontOutline(string Color, int Thickness = 1, bool Diagonals = true)
{
    /// <summary>For a <see cref="TextStyle"/>: no outline, even if the font has one</summary>
    public static readonly FontOutline None = new("", 0);

    /// <summary>One pixel of black all round</summary>
    public static readonly FontOutline Default = new("Black");

    /// <summary>Where the text is drawn again, in the outline color, to make the outline</summary>
    public List<(int X, int Y)> Offsets()
    {
        var offsets = new List<(int, int)>();
        for (int dy = -Thickness; dy <= Thickness; dy++)
            for (int dx = -Thickness; dx <= Thickness; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                if (!Diagonals && Math.Abs(dx) + Math.Abs(dy) > Thickness)
                    continue;
                offsets.Add((dx, dy));
            }
        return offsets;
    }
}

/// <summary>
/// A halo round text: <paramref name="Radius"/> rings of pixels, the nearest in the glow color
/// at <paramref name="Strength"/> percent over the background, each further one fading more
/// toward the background. No <paramref name="Color"/> glows in the text's own color.
/// </summary>
internal record FontGlow(string? Color = null, int Radius = 2, int Strength = 50)
{
    /// <summary>For a <see cref="TextStyle"/>: no glow, even if the font has one</summary>
    public static readonly FontGlow None = new(null, 0, 0);

    /// <summary>Two rings in the text's color, from 50% strength</summary>
    public static readonly FontGlow Default = new();

    /// <summary>
    /// For each ring, nearest first, where the text is drawn again to make it: the offsets
    /// <paramref name="reach"/> plus the ring's number of pixels away (rounded), so a glow round
    /// an outline starts outside it
    /// </summary>
    public List<List<(int X, int Y)>> Rings(int reach)
    {
        var rings = new List<List<(int, int)>>();
        for (int ring = 1; ring <= Radius; ring++)
        {
            int distance = reach + ring;
            var offsets = new List<(int, int)>();
            for (int dy = -distance; dy <= distance; dy++)
                for (int dx = -distance; dx <= distance; dx++)
                    if ((int)Math.Round(Math.Sqrt(dx * dx + dy * dy)) == distance)
                        offsets.Add((dx, dy));
            rings.Add(offsets);
        }
        return rings;
    }
}

/// <summary>
/// Shading of the text's color down the glyphs: <paramref name="Top"/> percent at the top row
/// to <paramref name="Bottom"/> percent at the bottom, positive toward white and negative
/// toward black, blending in between
/// </summary>
internal record FontGradient(int Top, int Bottom)
{
    /// <summary>For a <see cref="TextStyle"/>: the plain color, even if the font has a gradient</summary>
    public static readonly FontGradient None = new(0, 0);

    /// <summary>40% lighter at the top, 40% darker at the bottom</summary>
    public static readonly FontGradient Default = new(40, -40);
}
