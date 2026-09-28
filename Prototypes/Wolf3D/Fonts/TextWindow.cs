using Wolf3D.Exceptions;
using Wolf3D.Managers;

namespace Wolf3D.Fonts;

/// <summary>
/// An area of the screen text is printed into, with a print position that moves along as text
/// is printed, and the style it's printed in. Coordinates are in the 320x200 virtual screen.
/// </summary>
internal sealed class TextWindow
{
    private readonly GraphicManager graphics;

    public TextWindow(GraphicManager graphics, int x, int y, int width, int height, TextStyle style)
    {
        this.graphics = graphics;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Style = style;
        Home();
    }

    /// <summary>A window covering the whole screen, for text placed by setting the print position</summary>
    public static TextWindow FullScreen(GraphicManager graphics, TextStyle style) => new(graphics, 0, 0, 320, 200, style);

    /// <summary>A window from (x, y) to the bottom right of the screen: text prints from there, and newlines come back to x</summary>
    public static TextWindow At(GraphicManager graphics, int x, int y, TextStyle style) => new(graphics, x, y, 320 - x, 200 - y, style);

    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Where the next <see cref="Print(string)"/> starts</summary>
    public int PrintX { get; set; }
    public int PrintY { get; set; }

    /// <summary>What text is printed with until it's changed</summary>
    public TextStyle Style { get; set; }

    /// <summary>Moves the print position to the window's top left</summary>
    public void Home()
    {
        PrintX = X;
        PrintY = Y;
    }

    /// <summary>Fills the window with <paramref name="color"/> and moves the print position home</summary>
    public void Clear(string color)
    {
        graphics.Bar(X, Y, Width, Height, color);
        Home();
    }

    /// <summary>Width and line height of <paramref name="text"/> in the window's current font</summary>
    public void Measure(string text, out int width, out int height) => graphics.MeasureText(text, Style.Font, out width, out height);

    /// <summary>
    /// Prints at the print position, leaving it after the text. A newline goes back to the
    /// window's left edge on the next line.
    /// </summary>
    public void Print(string text)
    {
        if (text == null)
            return;

        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            Measure(lines[i], out int w, out int h);
            graphics.DrawText(PrintX, PrintY, lines[i], Style);

            if (i < lines.Length - 1)
            {
                PrintX = X;
                PrintY += h;
            }
            else
                PrintX += w;
        }
    }

    public void Print(string text, TextStyle style)
    {
        Style = style;
        Print(text);
    }

    /// <summary>Prints each line centered across the window, starting at the print position's line</summary>
    public void CPrint(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        // A trailing newline doesn't start another (empty) line
        var lines = text.Split('\n');
        int count = text.EndsWith('\n') ? lines.Length - 1 : lines.Length;
        for (int i = 0; i < count; i++)
            CPrintLine(lines[i]);
    }

    public void CPrint(string text, TextStyle style)
    {
        Style = style;
        CPrint(text);
    }

    /// <summary>Prints one line centered across the window and moves the print position to the next line</summary>
    public void CPrintLine(string line)
    {
        Measure(line, out int w, out int h);
        if (w > Width)
            throw new PfWolfGraphicException("CPrintLine() - String exceeds width: '{0}'", line);

        graphics.DrawText(X + (Width - w) / 2, PrintY, line, Style);
        PrintY += h;
    }

    /// <summary>Prints a line centered in the middle of the window, without moving the print position</summary>
    public void PrintCentered(string line) => PrintInCenter(line, X, Y, Width, Height);

    /// <summary>Prints a line centered in the given area, without moving the print position</summary>
    public void PrintInCenter(string line, int x, int y, int width, int height)
    {
        Measure(line, out int w, out int h);
        graphics.DrawText(x + (width - w) / 2, y + (height - h) / 2, line, Style);
    }
}
