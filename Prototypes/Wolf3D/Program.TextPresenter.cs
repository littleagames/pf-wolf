using Wolf3D.Assets;
using Wolf3D.Extensions;
using Wolf3D.Managers;

namespace Wolf3D;

/// <summary>How a <see cref="PresenterInfo"/> is presented (JAM's TPF_ flags)</summary>
[Flags]
internal enum PresenterFlags
{
    None = 0,

    /// <summary>The script draws no shapes, so nothing is looked up for them</summary>
    CacheNoGfx = 0x2,

    /// <summary>Enter, space or fire at a page end finishes it (Esc always does, marked as escaped)</summary>
    Continue = 0x4,

    /// <summary>Prints over what's there from the region's corner, without clearing the region first</summary>
    UseCurrent = 0x8,

    ShowCursor = 0x10,

    /// <summary>A line past the region's bottom scrolls the region up</summary>
    ScrollRegion = 0x20,

    /// <summary>"PAGE n OF m" by the info line</summary>
    ShowPages = 0x40,

    /// <summary>Slow printing ticks with the terminal sound</summary>
    TermSound = 0x80,

    /// <summary>A key pressed during slow printing ends it</summary>
    Abortable = 0x100,
}

/// <summary>
/// A script to present and where (JAM's PresenterInfo): the region from (X1, Y1) to (X2, Y2),
/// its colors (palette indices) and the font number it starts in.
/// </summary>
internal sealed class PresenterInfo
{
    public PresenterFlags Flags;
    public string Script = "";
    public int X1, Y1, X2, Y2;
    public int Font;
    public int FontColor;
    public int Background, Light, Dark, Shadow;

    /// <summary>The print position to start from with <see cref="PresenterFlags.UseCurrent"/>; -1 for the region's corner</summary>
    public int CurX = -1, CurY = -1;

    /// <summary>Tics between characters; 0 prints a line at once</summary>
    public int PrintDelay;

    /// <summary>A line printed under the region, and the font number and color it's in</summary>
    public string? InfoLine;
    public int InfoFont, InfoColor;

    /// <summary>Where "PAGE n OF m" goes, with <see cref="PresenterFlags.ShowPages"/></summary>
    public int PageX = -1, PageY;

    /// <summary>Line spacing in place of the font's height; 0 for the font's</summary>
    public int CustomLineHeight;

    /// <summary>Played for each character slow printed, with <see cref="PresenterFlags.TermSound"/></summary>
    public string? TypeSound;

    public int HighlightColor, SavedFontColor;
    internal List<int> Pages = [];
    internal int PageNum;

    /// <summary>Whether it was left with Esc</summary>
    public bool Escaped;
}

internal partial class Program
{
    /*
    =============================================================================

                                TEXT PRESENTER

    JAM Productions' text presenter, as Blake Stone uses it for its briefings, elevator messages
    and message boxes (bstone jm_tp.cpp). A script is text with ^ codes, two letters and a fixed
    number of hex digits (^FC3a sets the color, ^CE centers the line, ^EP ends a page, ^XX the
    script). Lines word wrap within the region; a line of nothing but codes takes no room.
    Fonts and shapes come from gamepacks/{pack}/presenter.yaml. Not done: animations (^AN),
    display strings (^DS), music and sounds by number (^PM, ^PS) and scaled sprite or wall shapes.

    =============================================================================
    */

    const char TP_RETURN = '\r';
    const char TP_CONTROL = '^';
    const int TP_MARGIN = 1;
    const int TP_CURSOR_SAVES = 8;

    const int fl_center = 0x0001, fl_boxshape = 0x0004, fl_shadowtext = 0x0008, fl_presenting = 0x0010,
        fl_startofline = 0x0020, fl_upreleased = 0x0040, fl_dnreleased = 0x0080, fl_shadowpic = 0x0400,
        fl_clearscback = 0x0800;

    static PresenterAsset? presenterAsset;
    static PresenterAsset Presenter =>
        presenterAsset ??= _assetManager.FindInGamePack<PresenterAsset>("presenter") ?? new PresenterAsset();

    static PresenterInfo tp = new();
    static int tpflags;
    static int tpbg, tplt, tpdk, tpsh;
    static int tpxl, tpyl, tpxh, tpyh;
    static int tpcurx, tpcury, tplastx, tplasty;
    static int tppx, tppy;
    static string tptext = "";
    static int tppos;
    static int tpfontnumber;
    static Fonts.Font? tpfont;
    static int tpfontcolor;
    static bool tpjustifyright;
    static readonly int[] tpsavex = new int[TP_CURSOR_SAVES + 1], tpsavey = new int[TP_CURSOR_SAVES + 1];
    static readonly int[] tppagex = new int[2], tppagey = new int[2];

    static char TpAt(int i) => i >= 0 && i < tptext.Length ? tptext[i] : '\0';

    static Fonts.Font? PresenterFont(int number)
    {
        var fonts = Presenter.Fonts;
        return fonts != null && number >= 0 && number < fonts.Count ? _fontManager.Find(fonts[number]) : null;
    }

    static int TpWidth(char ch) => tpfont?.Advance(ch) ?? 0;
    static int TpFontHeight => tpfont?.Height ?? 8;
    static int TpShadowed => (tpflags & fl_shadowtext) != 0 ? 1 : 0;

    /// <summary>
    /// A VGAGRAPH text as a presenter script: up to and including its ^XX, the end marker. Null
    /// if there's no such text.
    /// </summary>
    internal static string? PresenterScript(string textName)
    {
        if (_assetManager.Find<TextAsset>(textName) is not { } asset)
        {
            Console.WriteLine($"Presenter text \"{textName}\" wasn't found");
            return null;
        }

        var text = System.Text.Encoding.Latin1.GetString(asset.RawData);
        int end = text.IndexOf("^XX", StringComparison.Ordinal);
        return end < 0 ? text + "^XX" : text[..(end + 3)];
    }

    /// <summary>Finds the script's pages: each ^EP starts one (the one after the last isn't a page)</summary>
    static void TpInitScript(PresenterInfo pi)
    {
        pi.Pages = [0];
        tptext = pi.Script;
        int pos = 0;
        while (pos < tptext.Length)
        {
            int skip;
            while ((skip = TpLineCommented(pos)) != 0)
                pos += skip;
            if (pos >= tptext.Length)
                break;

            if (tptext[pos++] == TP_CONTROL && TpAt(pos) == 'E' && TpAt(pos + 1) == 'P')
            {
                pos += 2;
                pi.Pages.Add(pos);
            }
        }

        if (pi.Pages.Count > 1)
            pi.Pages.RemoveAt(pi.Pages.Count - 1);
    }

    /// <summary>A line starting with a semicolon is a comment: how far to skip past it, or 0</summary>
    static int TpLineCommented(int pos)
    {
        if (TpAt(pos) != ';' || TpAt(pos - 2) != TP_RETURN)
            return 0;

        int s = pos;
        while (s < tptext.Length && tptext[s] != TP_RETURN)
            s++;
        return s + 2 - pos;
    }

    /// <summary>Presents a script (TP_Presenter): prints it into its region, page by page</summary>
    internal static void TP_Presenter(PresenterInfo pi)
    {
        tp = pi;
        if (pi.Pages.Count == 0)
            TpInitScript(pi);

        tptext = pi.Script;
        tpbg = pi.Background;
        tplt = pi.Light;
        tpdk = pi.Dark;
        tpsh = pi.Shadow;
        tpxl = pi.X1 + TP_MARGIN;
        tpyl = pi.Y1 + TP_MARGIN;
        tpxh = pi.X2 - TP_MARGIN;
        tpyh = pi.Y2 - TP_MARGIN;
        tpfontcolor = pi.FontColor;
        tpjustifyright = false;
        pi.Escaped = false;

        if (pi.Flags.HasFlag(PresenterFlags.UseCurrent) && pi.CurX != -1 && pi.CurY != -1)
        {
            if (pi.Flags.HasFlag(PresenterFlags.ShowCursor))
            {
                tpcurx = tppx;
                tpcury = tppy;
            }
            else
            {
                tpcurx = pi.CurX;
                tpcury = pi.CurY;
            }
        }
        else
        {
            tpcurx = tpxl;
            tpcury = tpyl;
        }

        tppos = 0;
        pi.PageNum = 0;

        tpfontnumber = pi.Font;
        tpfont = PresenterFont(tpfontnumber);
        tpflags = fl_presenting | fl_startofline;
        if (TpAt(tppos) == TP_CONTROL)
            TpHandleCodes();

        // The info line under the region
        if (pi.InfoLine != null)
        {
            var oldFont = tpfont;
            var oldColor = tpfontcolor;
            tpfont = PresenterFont(pi.InfoFont);
            tpfontcolor = pi.InfoColor;

            tppx = tpxl;
            tppy = tpyh + TP_MARGIN + 1;
            _videoManager.Bar(tpxl - TP_MARGIN, tppy, tpxh - tpxl + 1 + TP_MARGIN * 2, 8, tpbg.ToString());
            TpShadowPrint(pi.InfoLine, tpsh);

            if (pi.Flags.HasFlag(PresenterFlags.ShowPages) && pi.PageX >= 0)
            {
                tppx = pi.PageX;
                tppy = pi.PageY;
                TpShadowPrint("PAGE ", tpsh);
                tppagex[0] = tppx;
                tppagey[0] = tppy;
                TpShadowPrint("   OF ", tpsh);
                tppagex[1] = tppx;
                tppagey[1] = tppy;
                TpPrintPageNumber();
            }

            tpfontcolor = oldColor;
            tpfont = oldFont;
        }

        tpfont = PresenterFont(tpfontnumber);
        if (!pi.Flags.HasFlag(PresenterFlags.UseCurrent))
            _videoManager.Bar(tpxl - TP_MARGIN, tpyl - TP_MARGIN, tpxh - tpxl + 1 + TP_MARGIN * 2, tpyh - tpyl + 1 + TP_MARGIN * 2, tpbg.ToString());

        while ((tpflags & fl_presenting) != 0 && tppos <= tptext.Length)
        {
            if (TpAt(tppos) == TP_CONTROL)
                TpHandleCodes();
            else if (tppos >= tptext.Length)
                break;              // a script with no ^XX ends with its text
            else
                TpWrapText();
        }

        pi.CurX = tppx = tpcurx;
        pi.CurY = tppy = tpcury;
    }

    /// <summary>Prints as much of the line as fits, and moves to the next line where it should</summary>
    static void TpWrapText()
    {
        tpflags &= ~fl_startofline;

        int skip = TpLineCommented(tppos);
        if (skip != 0)
        {
            tppos += skip;
            return;
        }

        // Up to the right margin, the end, a line break or a code
        int scanx = tpcurx;
        int scan = tppos;
        while (scanx + TpWidth(TpAt(scan)) <= tpxh && TpAt(scan) != '\0' && TpAt(scan) != TP_RETURN && TpAt(scan) != TP_CONTROL)
            scanx += TpWidth(TpAt(scan++));

        bool newlineOnly = false;
        // Past the right margin: back to a space
        if (scanx + TpWidth(TpAt(scan)) > tpxh)
        {
            int lastx = scanx, last = scan;
            while (scan != tppos && TpAt(scan) != ' ' && TpAt(scan) != TP_RETURN)
                scanx -= TpWidth(TpAt(scan--));

            if (scan == tppos)
            {
                if (tpcurx != tpxl)
                    newlineOnly = true;     // nothing fits after what's on the line: start the next
                else
                {
                    scan = last;            // one word too long for a line: split it
                    scanx = lastx;
                }
            }
        }

        if (!newlineOnly)
        {
            var line = tptext[tppos..Math.Min(scan, tptext.Length)];
            if (tpjustifyright && (tpflags & fl_center) == 0 && tpfont != null)
            {
                tpcurx = tpxh - tpfont.Measure(line) + 1;
                if (tpcurx < tpxl)
                    tpcurx = tpxl;
            }

            tppx = tpcurx;
            tppy = tpcury;
            if (TpAt(tppos) != TP_RETURN)
            {
                if (tp.PrintDelay > 0)
                    TpSlowPrint(line, tp.PrintDelay);
                else
                    TpPrint(line);
            }
            tppos = scan;
        }

        tpflags &= ~fl_center;

        // A wrapped line's space, and the line break
        if (TpAt(tppos) == ' ' && TpAt(tppos + 1) != ' ')
            tppos++;
        if (TpAt(tppos) == TP_RETURN)
            tppos += TpAt(tppos + 1) == '\n' ? 2 : 1;

        // Codes don't move to the next line
        if (TpAt(scan) != TP_CONTROL && TpAt(scan) != '\0')
        {
            tpcurx = tpxl;
            if (tp.Flags.HasFlag(PresenterFlags.ScrollRegion) && tpcury + TpFontHeight * 2 > tpyh)
            {
                // No scrolling yet: start again at the top of a cleared region
                _videoManager.Bar(tpxl, tpyl, tpxh - tpxl + 1, tpyh - tpyl + 1, tpbg.ToString());
                tpcury = tpyl;
            }
            else if (tp.CustomLineHeight > 0)
                tpcury += tp.CustomLineHeight + TpShadowed;
            else
                tpcury += TpFontHeight + TpShadowed;
        }
    }

    static int TpValue(int pos, int digits)
    {
        int value = 0;
        for (int i = 0; i < digits; i++)
        {
            char ch = TpAt(pos + i);
            int nybble = ch >= '0' && ch <= '9' ? ch - '0'
                : ch >= 'a' && ch <= 'f' ? ch - 'a' + 10
                : ch >= 'A' && ch <= 'F' ? ch - 'A' + 10
                : 0;
            value = value << 4 | nybble;
        }
        return value;
    }

    /// <summary>How many hex digits a code takes</summary>
    static int TpCodeDigits(string code) => code switch
    {
        "SX" or "RX" or "SY" or "RY" or "FN" or "ST" or "BX" or "SP" or "SB" => 1,
        "FC" or "BC" or "SC" or "LC" or "DC" or "AX" or "AY" or "HC" or "AN" or "DS" or "PM" or "PS" => 2,
        "LM" or "RM" or "PX" or "PY" or "SH" => 3,
        _ => 0,
    };

    static void TpHandleCodes()
    {
        if (TpAt(tppos - 2) == TP_RETURN && TpAt(tppos - 1) == '\n')
            tpflags |= fl_startofline;

        while (TpAt(tppos) == TP_CONTROL && (tpflags & fl_presenting) != 0)
        {
            var code = $"{TpAt(tppos + 1)}{TpAt(tppos + 2)}";
            tppos += 3;
            int value = TpValue(tppos, TpCodeDigits(code));
            tppos += TpCodeDigits(code);

            switch (code)
            {
                case "CE":
                    {
                        // The rest of the line's width, codes and all, centered between the margins
                        int length = 0;
                        int s = tppos;
                        while (TpAt(s) != '\0' && TpAt(s) != TP_RETURN)
                        {
                            if (TpAt(s) == TP_CONTROL)
                            {
                                var sub = $"{TpAt(s + 1)}{TpAt(s + 2)}";
                                int digits = TpCodeDigits(sub);
                                if (sub == "SH")
                                    length += TpBoxAroundShape(-1, -1, ShapePic(TpValueAt(s + 3, 3)));
                                s += 3 + digits;
                            }
                            else
                                length += TpWidth(TpAt(s++));
                        }
                        tpcurx += (tpxh - tpcurx + 1 - length) / 2;
                        tpflags |= fl_center;
                        break;
                    }

                case "SH":
                    TpDrawShape(tpcurx, tpcury, ShapePic(value));
                    break;

                case "HC":
                    tp.HighlightColor = value;
                    break;
                case "HO":
                    tp.SavedFontColor = tpfontcolor;
                    tpfontcolor = tp.HighlightColor;
                    break;
                case "HF":
                    tpfontcolor = tp.SavedFontColor;
                    break;

                case "AX":
                    tpcurx += (sbyte)value;
                    break;
                case "AY":
                    tpcury += (sbyte)value;
                    break;

                case "FC":
                    tpfontcolor = value;
                    break;
                case "SC":
                    tpsh = value;
                    break;
                case "LC":
                    tplt = value;
                    break;
                case "DC":
                    tpdk = value;
                    break;
                case "BC":
                    tpbg = value;
                    break;

                case "SL":
                    tpsavex[TP_CURSOR_SAVES] = tpcurx;
                    tpsavey[TP_CURSOR_SAVES] = tpcury;
                    break;
                case "RL":
                    tpcurx = tpsavex[TP_CURSOR_SAVES];
                    tpcury = tpsavey[TP_CURSOR_SAVES];
                    break;
                case "SX":
                    tpsavex[Math.Min(value, TP_CURSOR_SAVES)] = tpcurx;
                    break;
                case "RX":
                    tpcurx = tpsavex[Math.Min(value, TP_CURSOR_SAVES)];
                    break;
                case "SY":
                    tpsavey[Math.Min(value, TP_CURSOR_SAVES)] = tpcury;
                    break;
                case "RY":
                    tpcury = tpsavey[Math.Min(value, TP_CURSOR_SAVES)];
                    break;

                case "FN":
                    tpfontnumber = value;
                    tpfont = PresenterFont(value);
                    break;

                case "ST":
                    tpflags = value != 0 ? tpflags | fl_shadowtext : tpflags & ~fl_shadowtext;
                    break;
                case "SP":
                    tpflags = value != 0 ? tpflags | fl_shadowpic : tpflags & ~fl_shadowpic;
                    break;
                case "BX":
                    tpflags = value != 0 ? tpflags | fl_boxshape : tpflags & ~fl_boxshape;
                    break;
                case "SB":
                    tpflags = value != 0 ? tpflags | fl_clearscback : tpflags & ~fl_clearscback;
                    break;

                case "LM":
                    tpxl = value == 0xfff ? tpcurx : value;
                    if (tpcurx < tpxl)
                        tpcurx = tpxl;
                    break;
                case "RM":
                    tpxh = value == 0xfff ? tpcurx : value;
                    break;
                case "DM":
                    tpxl = tp.X1 + TP_MARGIN;
                    tpyl = tp.Y1 + TP_MARGIN;
                    tpxh = tp.X2 - TP_MARGIN;
                    tpyh = tp.Y2 - TP_MARGIN;
                    break;

                case "PX":
                    tpcurx = value;
                    break;
                case "PY":
                    tpcury = value;
                    break;

                case "LJ":
                    tpjustifyright = false;
                    break;
                case "RJ":
                    tpjustifyright = true;
                    break;

                case "BE":
                    _audioManager.Play("bs/term_beep");
                    break;

                case "PA":
                    _videoManager.Update();
                    GameEngineManager.DelayTics(30);
                    break;

                case "MO":
                    TpMore();
                    break;

                case "EP":
                    TpEndPage();
                    break;

                case "XX":
                    tpflags &= ~fl_presenting;
                    _videoManager.Update();
                    break;

                // ^ZZ, ^HI, and those not done (^AN, ^DS, ^PM, ^PS): nothing
            }
        }

        if (TpAt(tppos) == TP_RETURN && TpAt(tppos + 1) == '\n' && (tpflags & fl_startofline) != 0)
            tppos += 2;
    }

    static int TpValueAt(int pos, int digits)
    {
        int old = tppos;
        int value = TpValue(pos, digits);
        tppos = old;
        return value;
    }

    /// <summary>The picture presenter.yaml gives shape <paramref name="number"/>, or null</summary>
    static GraphicAsset? ShapePic(int number) =>
        Presenter.Shapes.TryGetValue(number, out var shape) && !string.IsNullOrEmpty(shape.Pic)
            ? _assetManager.Find<GraphicAsset>(shape.Pic)
            : null;

    /// <summary>Draws a shape at the print position (pictures snap to 8 pixels across) and moves past it</summary>
    static void TpDrawShape(int x, int y, GraphicAsset? pic)
    {
        if (pic == null)
            return;

        x = (x + 7) & ~7;
        int width = TpBoxAroundShape(x, y, pic);
        _videoManager.MemToScreen(pic.RawData, pic.Width, pic.Height, x, y);
        tpcurx += width;
    }

    /// <summary>A shape's width, with its box and shadow if they're on; draws them unless x is -1</summary>
    static int TpBoxAroundShape(int x1, int y1, GraphicAsset? pic)
    {
        if (pic == null)
            return 0;

        int x2 = x1 + pic.Width - 1, y2 = y1 + pic.Height - 1;
        if ((tpflags & fl_boxshape) != 0)
        {
            x1--;
            x2++;
            y1--;
            y2++;
            if (x1 >= 0 && y1 >= 0)
            {
                _videoManager.HorizontalLine(x1, x2, y1, tplt.ToString());
                _videoManager.HorizontalLine(x1, x2, y2, tpdk.ToString());
                _videoManager.VerticalLine(y1, y2, x1, tplt.ToString());
                _videoManager.VerticalLine(y1, y2, x2, tpdk.ToString());
            }
        }

        if ((tpflags & fl_shadowpic) != 0)
        {
            x2++;
            y2++;
            if (x1 >= 0 && y1 >= 0)
            {
                _videoManager.HorizontalLine(x1 + 1, x2, y2, tpsh.ToString());
                _videoManager.VerticalLine(y1 + 1, y2, x2, tpsh.ToString());
            }
        }

        return x2 - x1 + 1;
    }

    /// <summary>Prints at (px, py), moving px past it (with its shadow, when shadowed text is on)</summary>
    static void TpPrint(string text)
    {
        tplastx = tpcurx;
        tplasty = tpcury;

        if ((tpflags & fl_shadowtext) != 0)
            TpShadowPrint(text, tpfontcolor == tpbg ? tpbg : tpsh);
        else
            TpDraw(text, tpfontcolor);

        tpcurx = tppx;
        tpcury = tppy;
    }

    /// <summary>Text with a shadow one pixel right and down (bstone's ShPrint)</summary>
    static void TpShadowPrint(string text, int shadowColor)
    {
        int x = tppx;
        tppx = x + 1;
        tppy++;
        TpDraw(text, shadowColor);
        tppx = x;
        tppy--;
        TpDraw(text, tpfontcolor);
    }

    static void TpDraw(string text, int color)
    {
        if (tpfont == null || text.Length == 0)
            return;
        _graphicManager.DrawText(tppx, tppy, text, tpfont, color.ToString());
        tppx += tpfont.Measure(text);
    }

    /// <summary>Prints a character at a time, delay tics apart; a key (if abortable) prints the rest at once</summary>
    static void TpSlowPrint(string text, int delay)
    {
        for (int i = 0; i < text.Length; i++)
        {
            TpPrint(text[i].ToString());
            tppx = tpcurx;
            _videoManager.Update();

            if (tp.Flags.HasFlag(PresenterFlags.TermSound) && text[i] != ' ' && !string.IsNullOrEmpty(tp.TypeSound))
                _audioManager.Play(tp.TypeSound);

            _inputManager.ClearLastKey();
            var start = GameEngineManager.GetTimeCount();
            while (GameEngineManager.GetTimeCount() - start < delay)
            {
                GameEngineManager.DelayMs(5);
                _inputManager.ProcessEvents();
                if (tp.Flags.HasFlag(PresenterFlags.Abortable) && _inputManager.GetLastKeyPressed() != ScanCodes.sc_None)
                {
                    TpPrint(text[(i + 1)..]);
                    _videoManager.Update();
                    tpflags &= ~fl_presenting;
                    return;
                }
            }
        }
    }

    /// <summary>^MO: prints &lt;MORE&gt;, waits for anything, and clears the line</summary>
    static void TpMore()
    {
        tppx = tpcurx;
        tppy = tpcury;
        TpPrint("<MORE>");
        _videoManager.Update();

        _inputManager.ClearKeysDown();
        _inputManager.Ack();
        tp.Escaped = _inputManager.GetLastKeyPressed() == ScanCodes.sc_Escape;
        if (tp.Escaped)
            tpflags &= ~fl_presenting;

        tpcurx = tpxl;
        _videoManager.Bar(tpcurx, tpcury, tpxh - tpxl + 1 + TP_MARGIN * 2, TpFontHeight + TpShadowed, tpbg.ToString());
    }

    /// <summary>^EP: shows the page and waits to go to the one before or after, or to finish</summary>
    static void TpEndPage()
    {
        _videoManager.Update();
        if (_videoManager.screenfaded)
            _videoManager.FadeIn();

        _inputManager.ClearKeysDown();
        tpflags |= fl_upreleased | fl_dnreleased;
        while (true)
        {
            GameEngineManager.DelayMs(5);
            ReadAnyControl(out var ci);
            var dir = ci.dir;
            if (_inputManager.IsKeyDown(ScanCodes.sc_PgUp))
                dir = Direction.North;
            else if (_inputManager.IsKeyDown(ScanCodes.sc_PgDn))
                dir = Direction.South;

            if (tp.Flags.HasFlag(PresenterFlags.Continue)
                && (ci.button0 || _inputManager.IsKeyDown(ScanCodes.sc_Space) || _inputManager.IsKeyDown(ScanCodes.sc_Enter)))
            {
                tp.Escaped = false;
                tpflags &= ~fl_presenting;
                break;
            }

            if (ci.button1 || _inputManager.IsKeyDown(ScanCodes.sc_Escape))
            {
                tp.Escaped = true;
                tpflags &= ~fl_presenting;
                break;
            }

            if ((dir == Direction.North || dir == Direction.West) && tp.PageNum > 0)
            {
                if ((tpflags & fl_upreleased) != 0)
                {
                    tp.PageNum--;
                    tpflags &= ~fl_upreleased;
                    break;
                }
            }
            else
            {
                tpflags |= fl_upreleased;
                if ((dir == Direction.South || dir == Direction.East) && tp.PageNum < tp.Pages.Count - 1)
                {
                    if ((tpflags & fl_dnreleased) != 0)
                    {
                        tp.PageNum++;
                        tpflags &= ~fl_dnreleased;
                        break;
                    }
                }
                else
                    tpflags |= fl_dnreleased;
            }
        }

        if ((tpflags & fl_presenting) == 0)
        {
            _inputManager.ClearKeysDown();
            return;
        }

        tpcurx = tpxl;
        tpcury = tpyl;
        if (tpcury + TpFontHeight > tpyh)
            tpcury = tpyh - TpFontHeight;
        tppos = tp.Pages[tp.PageNum];

        if (TpAt(tppos) == TP_CONTROL)
        {
            TpHandleCodes();
            tpflags &= ~fl_startofline;
        }
        _videoManager.Bar(tpxl, tpyl, tpxh - tpxl + 1, tpyh - tpyl + 1, tpbg.ToString());
        TpPrintPageNumber();

        // A held key doesn't flip page after page
        while (_inputManager.IsKeyDown(ScanCodes.sc_UpArrow) || _inputManager.IsKeyDown(ScanCodes.sc_DownArrow)
            || _inputManager.IsKeyDown(ScanCodes.sc_PgUp) || _inputManager.IsKeyDown(ScanCodes.sc_PgDn)
            || _inputManager.IsKeyDown(ScanCodes.sc_LeftArrow) || _inputManager.IsKeyDown(ScanCodes.sc_RightArrow))
        {
            GameEngineManager.DelayMs(5);
            _inputManager.ProcessEvents();
        }
    }

    static void TpPrintPageNumber()
    {
        if (!tp.Flags.HasFlag(PresenterFlags.ShowPages) || tp.PageX < 0)
            return;

        var oldFont = tpfont;
        var oldColor = tpfontcolor;
        tpfont = PresenterFont(tp.InfoFont);
        tpfontcolor = tp.InfoColor;

        tppx = tppagex[0];
        tppy = tppagey[0];
        _videoManager.Bar(tppx, tppy, 12, 7, tpbg.ToString());
        TpShadowPrint($"{tp.PageNum + 1,2}", tpsh);

        tppx = tppagex[1];
        tppy = tppagey[1];
        _videoManager.Bar(tppx, tppy, 12, 7, tpbg.ToString());
        TpShadowPrint($"{tp.Pages.Count,2}", tpsh);

        tpfont = oldFont;
        tpfontcolor = oldColor;
    }

    /*
    =============================================================================

                            SCREENS ON THE PRESENTER

    =============================================================================
    */

    /// <summary>A bevelled box: the face, its light top and left edges and dark bottom and right ones</summary>
    internal static void BevelBox(int x, int y, int width, int height, int hi, int med, int lo)
    {
        int x2 = x + width - 1, y2 = y + height - 1;
        _videoManager.Bar(x, y, width, height, med.ToString());
        _videoManager.HorizontalLine(x, x2, y, hi.ToString());
        _videoManager.HorizontalLine(x, x2, y2, lo.ToString());
        _videoManager.VerticalLine(y, y2, x, hi.ToString());
        _videoManager.VerticalLine(y, y2, x2, lo.ToString());
        _videoManager.Bar(x, y2, 1, 1, (med + 1).ToString());
        _videoManager.Bar(x2, y, 1, 1, (med + 1).ToString());
    }

    /// <summary>
    /// Presents a script in presenter.yaml's message box (bstone's BMAmsg), its lines centered up
    /// and down in it. Nothing if the pack has no message box.
    /// </summary>
    internal static void PresenterMessageBox(string script)
    {
        if (Presenter.MessageBox is not { } box)
            return;

        int x2 = box.X + 7, y2 = box.Y + 4, w2 = box.Width - 14, h2 = box.Height - 8;
        BevelBox(box.X, box.Y, box.Width, box.Height, box.Hi, box.Med, box.Lo);
        BevelBox(x2, y2, w2, h2, box.Lo, box.Med, box.Hi);

        var font = PresenterFont(box.Font);
        int lines = 1 + script.Count(c => c == TP_RETURN);
        int height = (font?.Height ?? 8) * lines + 1 + TP_MARGIN * 2;

        var pi = new PresenterInfo
        {
            Flags = PresenterFlags.CacheNoGfx,
            Script = script,
            X1 = x2 + 1,
            Y1 = y2 + (h2 - height) / 2,
            Font = box.Font,
            FontColor = box.TextColor,
            Background = box.Med,
            Light = box.Hi,
            Shadow = box.Lo,
            Dark = box.Lo,
        };
        pi.X2 = pi.X1 + w2 - 3;
        pi.Y2 = pi.Y1 + height - 1;
        TP_Presenter(pi);
    }

    /// <summary>
    /// Shows a briefing (a VGAGRAPH text) in presenter.yaml's briefing window, page by page,
    /// until it's finished or left. Returns whether it was left with Esc.
    /// </summary>
    internal static bool ShowBriefing(string textName)
    {
        if (Presenter.Briefing is not { } briefing || PresenterScript(textName) is not { } script)
            return false;

        _videoManager.FadeOut();
        _videoManager.ClearScreen(0);
        foreach (var pic in briefing.Pics)
            _graphicManager.DrawPic(pic.Pic, pic.X, pic.Y);

        if (!string.IsNullOrEmpty(briefing.Music))
            StartCPMusic(briefing.Music);

        var language = _assetManager.GetText("en-us");
        var pi = new PresenterInfo
        {
            Flags = PresenterFlags.ShowPages | PresenterFlags.Continue,
            Script = script,
            X1 = briefing.X1,
            Y1 = briefing.Y1,
            X2 = briefing.X2,
            Y2 = briefing.Y2,
            Light = briefing.Light,
            Background = briefing.Background,
            Dark = briefing.Dark,
            Shadow = briefing.Shadow,
            Font = briefing.Font,
            InfoLine = briefing.InfoLine.ToLanguageText(language),
            InfoFont = briefing.InfoFont,
            InfoColor = briefing.InfoColor,
            PageX = briefing.PageX ?? -1,
            PageY = briefing.PageY,
        };
        TP_Presenter(pi);

        _videoManager.FadeOut();
        _inputManager.ClearKeysDown();
        return pi.Escaped;
    }
}
