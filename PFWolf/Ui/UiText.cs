using PFWolf.Fonts;

namespace PFWolf.Ui;

/// <summary>The fonts the menus and screens use</summary>
internal static class UiFonts
{
    internal const string SMALL_FONT = "SmallFont";
    internal const string LARGE_FONT = "LargeFont";

    /// <summary>Control panel menu items are printed in this</summary>
    internal const string MENU_FONT = LARGE_FONT;

    /// <summary>Control panel text in <paramref name="color"/> on the menu background</summary>
    internal static TextStyle MenuStyle(string color) => new(MENU_FONT, color);
}

/// <summary>Text on the 320x200 screen: placing it, measuring it, and windows to print it in</summary>
internal sealed class UiText(GraphicManager graphics, FontManager fonts)
{
    /// <summary>Text printed from (x, y), coming back to x after a newline</summary>
    internal TextWindow TextAt(int x, int y, TextStyle style) => TextWindow.At(graphics, x, y, style);

    /// <summary>Lines centered between x and x + width, the first at y</summary>
    internal TextWindow CenteredText(int x, int width, int y, TextStyle style) => new(graphics, x, y, width, 200 - y, style);

    internal void MeasureText(string text, string font, out int width, out int height) => graphics.MeasureText(text, font, out width, out height);

    internal int TextWidth(string text, string font)
    {
        MeasureText(text, font, out int width, out _);
        return width;
    }

    /// <summary>Cuts text short (to one character at least) to fit a width in a font.</summary>
    internal string FitText(string text, int width, string font)
    {
        var f = fonts.Find(font);
        if (f == null || text.Length == 0)
            return text;

        return text[..Math.Max(1, f.Fit(text, width))];
    }

    /// <summary>
    /// Draws a white window with a TILE8 frame, in 8 pixel tiles, and returns it to print into
    /// </summary>
    internal TextWindow DrawWindow(int x, int y, int w, int h, TextStyle style)
    {
        int i, sx, sy, sw, sh;

        var window = new TextWindow(graphics, x * 8, y * 8, w * 8, h * 8, style);

        sx = (x - 1) * 8;
        sy = (y - 1) * 8;
        sw = (w + 1) * 8;
        sh = (h + 1) * 8;

        window.Clear("White");

        graphics.DrawTile8(sx, sy, 0);
        graphics.DrawTile8(sx, sy + sh, 5);
        for (i = sx + 8; i <= sx + sw - 8; i += 8) {
            graphics.DrawTile8(i, sy, 1);
            graphics.DrawTile8(i, sy + sh, 6);
        }
        graphics.DrawTile8(i, sy, 2);
        graphics.DrawTile8(i, sy + sh, 7);

        for (i = sy + 8; i <= sy + sh - 8; i += 8) {
            graphics.DrawTile8(sx, i, 3);
            graphics.DrawTile8(sx + sw, i, 4);
        }

        return window;
    }

    private bool _xoricursor_status = false;
    internal void XORICursor(int x, int y, string s, int cursor, TextStyle style)
    {
        int w = TextWidth(s, style.Font);

        // Blinks by drawing the cursor glyph in the text color, then in the background color;
        // without a shadow, outline or glow, which would reach past the line and wear away what's there
        style = style with { Shadow = FontShadow.None, Outline = FontOutline.None, Glow = FontGlow.None };
        graphics.DrawText(x + w - 1, y, "\x80", (_xoricursor_status ^= true) ? style : style.Inverted);
    }

    /// <summary>Black text on the white of a <see cref="CenterWindow"/></summary>
    internal static readonly TextStyle PromptStyle = new(UiFonts.SMALL_FONT, "Black", "White");

    /// <summary>A framed window of w by h tiles, centered in the play view</summary>
    internal TextWindow CenterWindow(int w, int h, TextStyle style)
        => DrawWindow(((GameConstants.MAXX / 8) - w) / 2, ((GameConstants.MAXY / 8) - h) / 2, w, h, style);
}