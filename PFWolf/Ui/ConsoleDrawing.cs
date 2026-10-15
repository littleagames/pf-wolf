using System.Text;
using PFWolf.Fonts;

namespace PFWolf.Ui;

/// <summary>
/// The drop-down console drawn over the top of the screen (DrawConsole), and the view border it
/// covered put back once it's shut (RestoreBorderAfterConsole).
/// </summary>
internal sealed class ConsoleDrawing(
    VideoManager video,
    GraphicManager graphics,
    FontManager fonts,
    ConsoleManager console,
    PlayScreen playScreen,
    StatusBarDrawing statusBar)
{
    // Drop-down console layout, in the 320x200 virtual screen coordinates Bar/DrawText use. It
    // hangs from the top of the screen and reaches across all of it, however wide.
    internal const int CONSOLE_HEIGHT = 100;
    internal const int CONSOLE_MARGIN = 4;
    internal const string CONSOLE_FONT = "SmallFont";
    internal const string CONSOLE_PROMPT = "] ";

    // Whether the console panel was drawn over the last frame
    internal bool consoleDrawn;

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
    internal void RestoreBorderAfterConsole()
    {
        if (!consoleDrawn || console.IsOpen)
            return;

        consoleDrawn = false;
        playScreen.DrawPlayBorderSides();
        if (Program.TOPLINES > 0)
        {
            // the console covered the top status bar too
            statusBar.DrawTopBar();
            statusBar.DrawLevel();
            statusBar.DrawLives();
            statusBar.DrawLocation();
        }
    }

    internal void DrawConsole()
    {
        if (!console.IsOpen)
            return;

        var font = fonts.Find(CONSOLE_FONT);
        if (font == null)
            return;

        consoleDrawn = true;

        int lineHeight = font.LineHeight;

        using var _ = video.UseUiOrigin(UiAnchor.Top);
        int line = video.ScreenY(CONSOLE_HEIGHT - 1), bottom = video.ScreenY(CONSOLE_HEIGHT);
        video.BarScaledCoord(0, 0, video.screenWidth, line, "Black");
        video.BarScaledCoord(0, line, video.screenWidth, bottom - line, "Grey");

        // The text runs from the screen's left edge to its right, less the margins
        int left = video.ToLayoutX(0, roundUp: true) + CONSOLE_MARGIN;
        int textWidth = video.ToLayoutX(video.screenWidth) - left - CONSOLE_MARGIN;

        //
        // input line, scrolled sideways so the cursor stays visible
        //
        int inputY = CONSOLE_HEIGHT - 2 - lineHeight;
        string input = ConsoleSafeText(console.InputLine);
        int cursor = console.Cursor;
        int promptWidth = ConsoleTextWidth(CONSOLE_PROMPT, font);
        int inputWidth = textWidth - promptWidth - 1;

        int start = 0;
        while (start < cursor && ConsoleTextWidth(input[start..cursor], font) > inputWidth)
            start++;
        string visible = ConsoleFitText(input[start..], inputWidth, font);

        graphics.DrawText(left, inputY, CONSOLE_PROMPT, font, "White");
        graphics.DrawText(left + promptWidth, inputY, visible, font, "White");

        if ((GameEngineManager.GetTimeCount() / 20) % 2 == 0)     // blink about 3 times a second
        {
            int cursorX = left + promptWidth + ConsoleTextWidth(input[start..cursor], font);
            video.Bar(cursorX, inputY, 1, lineHeight, "White");
        }

        //
        // output, from the newest (less the scroll offset) upwards until the panel is full
        //
        int y = inputY - lineHeight;
        var scrollback = console.Scrollback;

        if (console.ScrollOffset > 0)
        {
            // Quake-style marker that there's newer output below
            graphics.DrawText(left, y, "^   ^   ^   ^   ^   ^   ^   ^", font, "Grey");
            y -= lineHeight;
        }

        for (int i = scrollback.Count - 1 - console.ScrollOffset; i >= 0 && y >= 0; i--)
        {
            string color = scrollback[i].StartsWith(CONSOLE_PROMPT) ? "White" : "HIGHLIGHT";
            var rows = ConsoleWrapText(ConsoleSafeText(scrollback[i]), textWidth, font);

            for (int r = rows.Count - 1; r >= 0 && y >= 0; r--)
            {
                graphics.DrawText(left, y, rows[r], font, color);
                y -= lineHeight;
            }
        }
    }

    /// <summary>
    /// Keeps only characters the game fonts can draw (printable ASCII); tabs become spaces and
    /// anything else becomes '?'. Console output can carry arbitrary text, e.g. asset names.
    /// </summary>
    internal static string ConsoleSafeText(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
            sb.Append(c == '\t' ? ' ' : (c >= ' ' && c <= '~') ? c : '?');
        return sb.ToString();
    }

    internal static int ConsoleTextWidth(string text, Font font) => font.Measure(text);

    /// <summary>The longest prefix of <paramref name="text"/> that fits in <paramref name="maxWidth"/>.</summary>
    internal static string ConsoleFitText(string text, int maxWidth, Font font) => text[..font.Fit(text, maxWidth)];

    /// <summary>Splits a line into rows that fit the console, breaking at spaces where possible.</summary>
    internal static List<string> ConsoleWrapText(string text, int maxWidth, Font font)
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
