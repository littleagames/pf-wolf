namespace PFWolf.Fonts;

/// <summary>
/// What a <see cref="TextWindow"/> prints on: the game's screen (its GraphicManager) or a
/// picture in the editor. Coordinates are the game's layout pixels (its 320x200 virtual screen);
/// colors are names from the game pack's colors.yaml, #RRGGBB or a palette index.
/// </summary>
public interface ITextSurface
{
    /// <summary>Fills a rectangle</summary>
    void Bar(int x, int y, int width, int height, string color);

    /// <summary>Draws one line of text; a missing font draws nothing</summary>
    void DrawText(int x, int y, string text, TextStyle style);

    /// <summary>Width and line height of one line of text; zero for a missing font</summary>
    void MeasureText(string text, string fontName, out int width, out int height);
}

/// <summary>Drawing text in a <see cref="TextStyle"/>: the style's effects over the font's own</summary>
public static class TextDrawing
{
    /// <summary>Draws one line of <paramref name="text"/> in <paramref name="font"/> as the style has it</summary>
    public static void Draw(ITextCanvas canvas, Font font, int x, int y, string text, TextStyle style)
    {
        var shadow = style.Shadow ?? font.Shadow;
        if (shadow != null && shadow != FontShadow.None && style.ShadowColor != null)
            shadow = shadow with { Color = style.ShadowColor };

        var outline = style.Outline ?? font.Outline;
        if (outline != null && outline != FontOutline.None && style.OutlineColor != null)
            outline = outline with { Color = style.OutlineColor };

        // A glow color draws the rings flat, fading from that color into itself
        var glow = style.Glow ?? font.Glow;
        var glowBackground = style.Background;
        if (glow != null && glow != FontGlow.None && style.GlowColor != null)
        {
            glow = glow with { Color = style.GlowColor };
            glowBackground = style.GlowColor;
        }

        font.Draw(canvas, x, y, text, style.Color, shadow, style.Gradient ?? font.Gradient, outline, glow, glowBackground);
    }
}
