using PFWolf.Fonts;

namespace PFWolf.Editor.Rendering;

/// <summary>
/// A font's characters, codes 0-255 sixteen to a row, each in a cell its glyph fits, drawn plain
/// (no shadow, outline, gradient or glow) in the text's color; cells for characters the font
/// hasn't got dark. One can be marked (the glyph editor's picked character).
/// </summary>
public sealed record CharacterGrid(IndexedPicture Picture, int CellWidth, int CellHeight, int Count)
{
    public static CharacterGrid Draw(Font font, Func<int, int, IndexedTextCanvas> canvasOf, string color, string background, int? marked = null)
    {
        int glyphWidth = Enumerable.Range(0, 256).Where(code => font.Has((char)code)).Select(code => font.Advance((char)code)).DefaultIfEmpty(4).Max();
        int cellWidth = Math.Max(glyphWidth, 4) + 2, cellHeight = Math.Max(font.Height, 4) + 2;
        var canvas = canvasOf(cellWidth * 16 + 1, cellHeight * 16 + 1);
        byte back = canvas.ResolveColor(background), missing = canvas.ResolveColor("#3C3C3C"), lines = canvas.ResolveColor("#606060");
        Array.Fill(canvas.Pixels, lines);

        int has = 0;
        for (int code = 0; code < 256; code++)
        {
            int x = code % 16 * cellWidth + 1, y = code / 16 * cellHeight + 1;
            bool present = font.Has((char)code);
            has += present ? 1 : 0;
            for (int row = 0; row < cellHeight - 1; row++)
                Array.Fill(canvas.Pixels, present ? back : missing, (y + row) * canvas.Width + x, cellWidth - 1);
            if (present)
                font.Draw(canvas, x, y, ((char)code).ToString(), color, FontShadow.None, FontGradient.None, FontOutline.None, FontGlow.None, background);
        }

        // The marked cell's border in a bright color
        if (marked is int pick and >= 0 and < 256)
        {
            byte mark = canvas.ResolveColor("#FFD040");
            int left = pick % 16 * cellWidth, top = pick / 16 * cellHeight;
            for (int i = 0; i <= cellWidth; i++)
            {
                canvas.Pixels[top * canvas.Width + left + i] = mark;
                canvas.Pixels[(top + cellHeight) * canvas.Width + left + i] = mark;
            }
            for (int i = 0; i <= cellHeight; i++)
            {
                canvas.Pixels[(top + i) * canvas.Width + left] = mark;
                canvas.Pixels[(top + i) * canvas.Width + left + cellWidth] = mark;
            }
        }
        return new CharacterGrid(new IndexedPicture(canvas.Width, canvas.Height, canvas.Pixels, null), cellWidth, cellHeight, has);
    }

    /// <summary>The character at a point of the grid (in its own pixels), or null off it</summary>
    public int? CodeAt(double x, double y)
    {
        int column = (int)Math.Floor(x / CellWidth), row = (int)Math.Floor(y / CellHeight);
        return column is >= 0 and < 16 && row is >= 0 and < 16 ? row * 16 + column : null;
    }

    /// <summary>A character's code and how it's shown ("Code 65 'A'")</summary>
    public static string Describe(int code)
    {
        var ch = (char)code;
        return ch is < ' ' or (>= '\x7f' and < '\xa0') ? $"Code {code}" : $"Code {code} '{ch}'";
    }
}
