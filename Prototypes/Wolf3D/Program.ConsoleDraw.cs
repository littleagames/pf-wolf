using System.Text;
using Wolf3D.Assets;

namespace Wolf3D;

internal partial class Program
{
    // Drop-down console layout, in the 320x200 virtual screen coordinates Bar/DrawPropString use.
    const int CONSOLE_HEIGHT = 100;
    const int CONSOLE_MARGIN = 4;
    const int CONSOLE_TEXTWIDTH = 320 - (CONSOLE_MARGIN * 2);
    const string CONSOLE_FONT = "SmallFont";
    const string CONSOLE_PROMPT = "] ";

    // Whether the console panel was drawn over the last frame
    static bool consoleDrawn;

    /// <summary>
    /// Draws the console over the top of the screen: output lines (word-wrapped, newest at the
    /// bottom) above the input line and its blinking cursor. Called each frame from ThreeDRefresh
    /// after the 3D view is drawn and before the screen is presented.
    /// </summary>
    /// <summary>
    /// The console panel covers the view border, which (unlike the 3D view) isn't redrawn every
    /// frame, so this repaints it the first frame after the console closes. Called from
    /// ThreeDRefresh before anything else is drawn over the border (the fps counter).
    /// </summary>
    internal static void RestoreBorderAfterConsole()
    {
        if (!consoleDrawn || _consoleManager.IsOpen)
            return;

        consoleDrawn = false;
        DrawPlayBorderSides();
    }

    internal static void DrawConsole()
    {
        if (!_consoleManager.IsOpen)
            return;

        var font = _assetManager.Find<FontAsset>(CONSOLE_FONT);
        if (font == null)
            return;

        consoleDrawn = true;

        int lineHeight = font.Height;

        _videoManager.Bar(0, 0, 320, CONSOLE_HEIGHT - 1, "Black");
        _videoManager.Bar(0, CONSOLE_HEIGHT - 1, 320, 1, "Grey");

        //
        // input line, scrolled sideways so the cursor stays visible
        //
        int inputY = CONSOLE_HEIGHT - 2 - lineHeight;
        string input = ConsoleSafeText(_consoleManager.InputLine);
        int cursor = _consoleManager.Cursor;
        int promptWidth = ConsoleTextWidth(CONSOLE_PROMPT, font);
        int inputWidth = CONSOLE_TEXTWIDTH - promptWidth - 1;

        int start = 0;
        while (start < cursor && ConsoleTextWidth(input[start..cursor], font) > inputWidth)
            start++;
        string visible = ConsoleFitText(input[start..], inputWidth, font);

        _videoManager.DrawPropString(CONSOLE_MARGIN, inputY, CONSOLE_PROMPT, "White", font);
        _videoManager.DrawPropString(CONSOLE_MARGIN + promptWidth, inputY, visible, "White", font);

        if ((GameEngineManager.GetTimeCount() / 20) % 2 == 0)     // blink about 3 times a second
        {
            int cursorX = CONSOLE_MARGIN + promptWidth + ConsoleTextWidth(input[start..cursor], font);
            _videoManager.Bar(cursorX, inputY, 1, lineHeight, "White");
        }

        //
        // output, from the newest (less the scroll offset) upwards until the panel is full
        //
        int y = inputY - lineHeight;
        var scrollback = _consoleManager.Scrollback;

        if (_consoleManager.ScrollOffset > 0)
        {
            // Quake-style marker that there's newer output below
            _videoManager.DrawPropString(CONSOLE_MARGIN, y, "^   ^   ^   ^   ^   ^   ^   ^", "Grey", font);
            y -= lineHeight;
        }

        for (int i = scrollback.Count - 1 - _consoleManager.ScrollOffset; i >= 0 && y >= 0; i--)
        {
            string color = scrollback[i].StartsWith(CONSOLE_PROMPT) ? "White" : "HIGHLIGHT";
            var rows = ConsoleWrapText(ConsoleSafeText(scrollback[i]), CONSOLE_TEXTWIDTH, font);

            for (int r = rows.Count - 1; r >= 0 && y >= 0; r--)
            {
                _videoManager.DrawPropString(CONSOLE_MARGIN, y, rows[r], color, font);
                y -= lineHeight;
            }
        }
    }

    /// <summary>
    /// Keeps only characters the game fonts can draw (printable ASCII); tabs become spaces and
    /// anything else becomes '?'. Console output can carry arbitrary text, e.g. asset names.
    /// </summary>
    static string ConsoleSafeText(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
            sb.Append(c == '\t' ? ' ' : (c >= ' ' && c <= '~') ? c : '?');
        return sb.ToString();
    }

    static int ConsoleTextWidth(string text, FontAsset font)
    {
        GraphicManager.MeasureString(text, out ushort width, out _, font);
        return width;
    }

    /// <summary>The longest prefix of <paramref name="text"/> that fits in <paramref name="maxWidth"/>.</summary>
    static string ConsoleFitText(string text, int maxWidth, FontAsset font)
    {
        int width = 0, length = 0;
        while (length < text.Length && width + font.Width[text[length]] <= maxWidth)
            width += font.Width[text[length++]];
        return text[..length];
    }

    /// <summary>Splits a line into rows that fit the console, breaking at spaces where possible.</summary>
    static List<string> ConsoleWrapText(string text, int maxWidth, FontAsset font)
    {
        var rows = new List<string>();

        while (ConsoleTextWidth(text, font) > maxWidth)
        {
            string fit = ConsoleFitText(text, maxWidth, font);
            if (fit.Length == 0)
                fit = text[..1];    // a single glyph wider than the console; never loop forever

            int space = fit.LastIndexOf(' ');
            if (space > 0 && fit.Length < text.Length)
                fit = fit[..space];

            rows.Add(fit);
            text = text[fit.Length..].TrimStart(' ');
        }

        rows.Add(text);
        return rows;
    }
}
