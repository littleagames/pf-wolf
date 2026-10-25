using PFWolf.Assets;
using PFWolf.Extensions;

namespace PFWolf.Ui;

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

/*
=============================================================================

                            TEXT PRESENTER

JAM Productions' text presenter, as Blake Stone uses it for its briefings, elevator messages
and message boxes (bstone jm_tp.cpp). A script is text with ^ codes, two letters and a fixed
number of hex digits (^FC3a sets the color, ^CE centers the line, ^EP ends a page, ^XX the
script). Lines word wrap within the region; a line of nothing but codes takes no room.
Fonts, shapes, animations (^AN, run while a page waits) and sounds come from
gamepacks/{pack}/presenter.yaml. Not done: display strings (^DS) and music by number (^PM),
which none of Blake Stone's texts use.

=============================================================================
*/

internal sealed class TextPresenter(AssetManager assets, InputManager input, AudioManager audio,
    VideoManager video, GraphicManager graphics, FontManager fonts, MenuDrawing draw, MenuInput menuInput)
{
    const char ReturnCode = '\r';
    const char Control = '^';
    const int Margin = 1;
    const int CursorSaves = 8;

    const int center = 0x0001, boxshape = 0x0004, shadowText = 0x0008, presenting = 0x0010,
        startOfLine = 0x0020, upReleased = 0x0040, downReleased = 0x0080, shadowPic = 0x0400,
        clearScBack = 0x0800;

    PresenterAsset? presenterAsset;
    PresenterAsset Presenter =>
        presenterAsset ??= assets.FindInGamePack<PresenterAsset>("presenter") ?? new PresenterAsset();

    PresenterInfo tp = new();
    int tpflags;
    int tpbg, tplt, tpdk, tpsh;
    int tpxl, tpyl, tpxh, tpyh;
    int tpcurx, tpcury, tplastx, tplasty;
    int tppx, tppy;
    string tptext = "";
    int tppos;
    int tpfontnumber;
    Fonts.Font? tpfont;
    int tpfontcolor;
    bool tpjustifyright;
    readonly int[] tpsavex = new int[CursorSaves + 1], tpsavey = new int[CursorSaves + 1];
    readonly int[] tppagex = new int[2], tppagey = new int[2];

    char TpAt(int i) => i >= 0 && i < tptext.Length ? tptext[i] : '\0';

    internal Fonts.Font? PresenterFont(int number)
    {
        var names = Presenter.Fonts;
        return names != null && number >= 0 && number < names.Count ? fonts.Find(names[number]) : null;
    }

    int TpWidth(char ch) => tpfont?.Advance(ch) ?? 0;
    int TpFontHeight => tpfont?.Height ?? 8;
    int TpShadowed => (tpflags & shadowText) != 0 ? 1 : 0;

    /// <summary>
    /// A VGAGRAPH text as a presenter script: up to and including its ^XX, the end marker. Null
    /// if there's no such text.
    /// </summary>
    internal string? PresenterScript(string textName)
    {
        if (assets.Find<TextAsset>(textName) is not { } asset)
        {
            Console.WriteLine($"Presenter text \"{textName}\" wasn't found");
            return null;
        }

        var text = System.Text.Encoding.Latin1.GetString(asset.RawData);
        int end = text.IndexOf("^XX", StringComparison.Ordinal);
        return end < 0 ? text + "^XX" : text[..(end + 3)];
    }

    /// <summary>Finds the script's pages: each ^EP starts one (the one after the last isn't a page)</summary>
    internal void TpInitScript(PresenterInfo pi)
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

            if (tptext[pos++] == Control && TpAt(pos) == 'E' && TpAt(pos + 1) == 'P')
            {
                pos += 2;
                pi.Pages.Add(pos);
            }
        }

        if (pi.Pages.Count > 1)
            pi.Pages.RemoveAt(pi.Pages.Count - 1);
    }

    /// <summary>A line starting with a semicolon is a comment: how far to skip past it, or 0</summary>
    int TpLineCommented(int pos)
    {
        if (TpAt(pos) != ';' || TpAt(pos - 2) != ReturnCode)
            return 0;

        int s = pos;
        while (s < tptext.Length && tptext[s] != ReturnCode)
            s++;
        return s + 2 - pos;
    }

    /// <summary>Presents a script (RunPresenter): prints it into its region, page by page</summary>
    internal void RunPresenter(PresenterInfo pi)
    {
        tp = pi;
        if (pi.Pages.Count == 0)
            TpInitScript(pi);

        tptext = pi.Script;
        tpbg = pi.Background;
        tplt = pi.Light;
        tpdk = pi.Dark;
        tpsh = pi.Shadow;
        tpxl = pi.X1 + Margin;
        tpyl = pi.Y1 + Margin;
        tpxh = pi.X2 - Margin;
        tpyh = pi.Y2 - Margin;
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
        tpanims.Clear();

        tpfontnumber = pi.Font;
        tpfont = PresenterFont(tpfontnumber);
        tpflags = presenting | startOfLine;
        if (TpAt(tppos) == Control)
            TpHandleCodes();

        // The info line under the region
        if (pi.InfoLine != null)
        {
            var oldFont = tpfont;
            var oldColor = tpfontcolor;
            tpfont = PresenterFont(pi.InfoFont);
            tpfontcolor = pi.InfoColor;

            tppx = tpxl;
            tppy = tpyh + Margin + 1;
            video.Bar(tpxl - Margin, tppy, tpxh - tpxl + 1 + Margin * 2, 8, tpbg.ToString());
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
            video.Bar(tpxl - Margin, tpyl - Margin, tpxh - tpxl + 1 + Margin * 2, tpyh - tpyl + 1 + Margin * 2, tpbg.ToString());

        while ((tpflags & presenting) != 0 && tppos <= tptext.Length)
        {
            if (TpAt(tppos) == Control)
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
    void TpWrapText()
    {
        tpflags &= ~startOfLine;

        int skip = TpLineCommented(tppos);
        if (skip != 0)
        {
            tppos += skip;
            return;
        }

        // Up to the right margin, the end, a line break or a code
        int scanx = tpcurx;
        int scan = tppos;
        while (scanx + TpWidth(TpAt(scan)) <= tpxh && TpAt(scan) != '\0' && TpAt(scan) != ReturnCode && TpAt(scan) != Control)
            scanx += TpWidth(TpAt(scan++));

        bool newlineOnly = false;
        // Past the right margin: back to a space
        if (scanx + TpWidth(TpAt(scan)) > tpxh)
        {
            int lastx = scanx, last = scan;
            while (scan != tppos && TpAt(scan) != ' ' && TpAt(scan) != ReturnCode)
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
            if (tpjustifyright && (tpflags & center) == 0 && tpfont != null)
            {
                tpcurx = tpxh - tpfont.Measure(line) + 1;
                if (tpcurx < tpxl)
                    tpcurx = tpxl;
            }

            tppx = tpcurx;
            tppy = tpcury;
            if (TpAt(tppos) != ReturnCode)
            {
                if (tp.PrintDelay > 0)
                    TpSlowPrint(line, tp.PrintDelay);
                else
                    TpPrint(line);
            }
            tppos = scan;
        }

        tpflags &= ~center;

        // A wrapped line's space, and the line break
        if (TpAt(tppos) == ' ' && TpAt(tppos + 1) != ' ')
            tppos++;
        if (TpAt(tppos) == ReturnCode)
            tppos += TpAt(tppos + 1) == '\n' ? 2 : 1;

        // Codes don't move to the next line
        if (TpAt(scan) != Control && TpAt(scan) != '\0')
        {
            tpcurx = tpxl;
            if (tp.Flags.HasFlag(PresenterFlags.ScrollRegion) && tpcury + TpFontHeight * 2 > tpyh)
            {
                // The next line won't fit: the region scrolls up a line, and it goes where the
                // last one was
                int line = TpFontHeight + TpShadowed;
                video.ScreenToScreen(tpxl, tpyl + line, tpxh - tpxl + 1, tpyh - tpyl + 1 - line, tpxl, tpyl);
                video.Bar(tpcurx, tpcury, tpxh - tpxl + 1 + Margin * 2, tpyh - tpcury + 1, tpbg.ToString());
                if (tpcury + TpFontHeight > tpyh)
                    tpcury = tpyh - TpFontHeight + 1 - TpShadowed;
            }
            else if (tp.CustomLineHeight > 0)
                tpcury += tp.CustomLineHeight + TpShadowed;
            else
                tpcury += TpFontHeight + TpShadowed;
        }
    }

    int TpValue(int pos, int digits)
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
    internal static int TpCodeDigits(string code) => code switch
    {
        "SX" or "RX" or "SY" or "RY" or "FN" or "ST" or "BX" or "SP" or "SB" => 1,
        "FC" or "BC" or "SC" or "LC" or "DC" or "AX" or "AY" or "HC" or "AN" or "DS" or "PM" or "PS" or "BG" => 2,
        "LM" or "RM" or "PX" or "PY" or "SH" => 3,
        _ => 0,
    };

    void TpHandleCodes()
    {
        if (TpAt(tppos - 2) == ReturnCode && TpAt(tppos - 1) == '\n')
            tpflags |= startOfLine;

        while (TpAt(tppos) == Control && (tpflags & presenting) != 0)
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
                        while (TpAt(s) != '\0' && TpAt(s) != ReturnCode)
                        {
                            if (TpAt(s) == Control)
                            {
                                var sub = $"{TpAt(s + 1)}{TpAt(s + 2)}";
                                int digits = TpCodeDigits(sub);
                                if (sub == "SH")
                                    length += TpBoxAroundShape(-1, -1, ShapePic(TpValueAt(s + 3, 3)));
                                else if (sub == "AN")
                                    length += TpBoxAroundShape(-1, -1, AnimationFrames(TpValueAt(s + 3, 2)).FirstOrDefault());
                                s += 3 + digits;
                            }
                            else
                                length += TpWidth(TpAt(s++));
                        }
                        // Too long to center: JAM's unsigned arithmetic puts it past the right
                        // margin, so it starts the next line from the left instead
                        int room = tpxh - tpcurx + 1 - length;
                        tpcurx = room >= 0 ? tpcurx + room / 2 : tpxh + 1;
                        tpflags |= center;
                        break;
                    }

                case "SH":
                    TpDrawShape(tpcurx, tpcury, ShapePic(value));
                    break;

                case "AN":
                    // Its first frame, and it's animated where it stands while the page waits (TpAnimatePage)
                    if (Presenter.Animations.TryGetValue(value, out var animation))
                    {
                        var frames = AnimationFrames(value);
                        tpanims.Add(new TpAnim(animation, frames, tpcurx, tpcury, tpbg));
                        TpDrawShape(tpcurx, tpcury, frames.FirstOrDefault());
                    }
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
                    tpsavex[CursorSaves] = tpcurx;
                    tpsavey[CursorSaves] = tpcury;
                    break;
                case "RL":
                    tpcurx = tpsavex[CursorSaves];
                    tpcury = tpsavey[CursorSaves];
                    break;
                case "SX":
                    tpsavex[Math.Min(value, CursorSaves)] = tpcurx;
                    break;
                case "RX":
                    tpcurx = tpsavex[Math.Min(value, CursorSaves)];
                    break;
                case "SY":
                    tpsavey[Math.Min(value, CursorSaves)] = tpcury;
                    break;
                case "RY":
                    tpcury = tpsavey[Math.Min(value, CursorSaves)];
                    break;

                case "FN":
                    tpfontnumber = value;
                    tpfont = PresenterFont(value);
                    break;

                case "ST":
                    tpflags = value != 0 ? tpflags | shadowText : tpflags & ~shadowText;
                    break;
                case "SP":
                    tpflags = value != 0 ? tpflags | shadowPic : tpflags & ~shadowPic;
                    break;
                case "BX":
                    tpflags = value != 0 ? tpflags | boxshape : tpflags & ~boxshape;
                    break;
                case "SB":
                    tpflags = value != 0 ? tpflags | clearScBack : tpflags & ~clearScBack;
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
                    tpxl = tp.X1 + Margin;
                    tpyl = tp.Y1 + Margin;
                    tpxh = tp.X2 - Margin;
                    tpyh = tp.Y2 - Margin;
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
                    audio.Play("bs/term_beep");
                    break;

                case "PS":
                    if (Presenter.Sounds.TryGetValue(value, out var sound))
                        audio.Play(sound);
                    break;

                case "PA":
                    video.Update();
                    GameEngineManager.DelayTics(30);
                    break;

                case "MO":
                    TpMore();
                    break;

                case "EP":
                    TpEndPage();
                    break;

                case "XX":
                    tpflags &= ~presenting;
                    video.Update();
                    break;

                // ^ZZ, ^HI, and those not done (^DS, ^PM): nothing
            }
        }

        if (TpAt(tppos) == ReturnCode && TpAt(tppos + 1) == '\n' && (tpflags & startOfLine) != 0)
            tppos += 2;
    }

    int TpValueAt(int pos, int digits)
    {
        int old = tppos;
        int value = TpValue(pos, digits);
        tppos = old;
        return value;
    }

    /// <summary>
    /// A shape ready to draw: row-major pixels, which of them show (null: all), and whether it's
    /// a picture (which snaps to 8 pixels across)
    /// </summary>
    sealed record TpShape(byte[] Pixels, byte[]? Mask, int Width, int Height, bool IsPic);

    readonly Dictionary<int, TpShape?> tpShapes = [];

    /// <summary>What presenter.yaml gives shape <paramref name="number"/>: a picture, sprite or wall; null if none</summary>
    TpShape? ShapePic(int number)
    {
        if (tpShapes.TryGetValue(number, out var cached))
            return cached;

        TpShape? found = null;
        if (Presenter.Shapes.TryGetValue(number, out var shape))
        {
            if (!string.IsNullOrEmpty(shape.Pic) && assets.Find<GraphicAsset>(shape.Pic) is { } pic)
                found = new TpShape(pic.RawData, null, pic.Width, pic.Height, true);
            else if (!string.IsNullOrEmpty(shape.Sprite))
                found = SpriteShape(shape.Sprite);
            else if (!string.IsNullOrEmpty(shape.Wall) && assets.Find<TextureAsset>(shape.Wall) is { } wall)
            {
                // Walls are kept column by column
                var story = wall.BottomStory();
                const int size = TextureAsset.StorySize;
                var rows = new byte[size * size];
                for (int x = 0; x < size; x++)
                    for (int y = 0; y < size; y++)
                        rows[y * size + x] = story[x * size + y];
                found = new TpShape(rows, null, size, size, false);
            }
        }

        tpShapes[number] = found;
        return found;
    }

    /// <summary>A sprite as a shape, or null if there's no such sprite</summary>
    TpShape? SpriteShape(string name) =>
        assets.Find<SpriteAsset>(name) is { } sprite ? new TpShape(sprite.RawData, sprite.OpacityMask, sprite.Width, sprite.Height, false) : null;

    readonly Dictionary<int, List<TpShape?>> tpAnimFrames = [];

    /// <summary>The frames of presenter.yaml's animation <paramref name="number"/>: none if it has no such animation</summary>
    List<TpShape?> AnimationFrames(int number)
    {
        if (!tpAnimFrames.TryGetValue(number, out var frames))
            tpAnimFrames[number] = frames = Presenter.Animations.TryGetValue(number, out var animation)
                ? animation.Sprites.Select(SpriteShape).ToList() : [];
        return frames;
    }

    /// <summary>A ^AN animation on the page: where it's drawn, the window's color behind it then, and its frame</summary>
    sealed class TpAnim(PresenterAnimation animation, List<TpShape?> frames, int x, int y, int background)
    {
        public readonly PresenterAnimation Animation = animation;
        public readonly List<TpShape?> Frames = frames;
        public readonly int X = x, Y = y, Background = background;
        public int Frame, Step = 1, Delay;
    }

    readonly List<TpAnim> tpanims = [];
    uint tpanimTime;

    /// <summary>
    /// Moves the page's animations on by the tics since the last call, each to its next frame once
    /// its frame's had its tics (bstone's TP_AnimatePage). True if any was drawn.
    /// </summary>
    bool TpAnimatePage()
    {
        uint now = GameEngineManager.GetTimeCount();
        int tics = (int)(now - tpanimTime);
        tpanimTime = now;
        if (tics <= 0)
            return false;

        bool drawn = false;
        foreach (var anim in tpanims)
        {
            if (anim.Frames.Count == 0 || (anim.Delay += tics) < anim.Animation.Tics)
                continue;

            anim.Delay = 0;
            anim.Frame += anim.Step;
            if (anim.Frame >= anim.Frames.Count || anim.Frame < 0)
            {
                if (anim.Animation.Rebound)
                {
                    anim.Step = -anim.Step;
                    anim.Frame = Math.Clamp(anim.Frame + anim.Step, 0, anim.Frames.Count - 1);
                }
                else
                    anim.Frame = 0;
            }

            // The last frame wiped (a sprite's see-through parts would show it), then the next
            if (anim.Frames[anim.Frame] is not { } shape)
                continue;
            video.Bar(anim.X, anim.Y, shape.Width, shape.Height, anim.Background.ToString());
            int curx = tpcurx;
            TpDrawShape(anim.X, anim.Y, shape);
            tpcurx = curx;
            drawn = true;
        }
        return drawn;
    }

    /// <summary>Draws a shape at the print position (pictures snap to 8 pixels across) and moves past it</summary>
    void TpDrawShape(int x, int y, TpShape? shape)
    {
        if (shape == null)
            return;

        if (shape.IsPic)
            x = (x + 7) & ~7;
        int width = TpBoxAroundShape(x, y, shape);
        if (shape.Mask == null)
            video.MemToScreen(shape.Pixels, shape.Width, shape.Height, x, y);
        else
        {
            if ((tpflags & clearScBack) != 0)
                video.Bar(x, y, shape.Width, shape.Height, tpbg.ToString());
            video.DrawImageRegion(shape.Pixels, shape.Mask, shape.Width, 0, 0, shape.Width, shape.Height, x, y);
        }
        tpcurx += width;
    }

    /// <summary>A shape's width, with its box and shadow if they're on; draws them unless x is -1</summary>
    int TpBoxAroundShape(int x1, int y1, TpShape? shape)
    {
        if (shape == null)
            return 0;

        int x2 = x1 + shape.Width - 1, y2 = y1 + shape.Height - 1;
        if ((tpflags & boxshape) != 0)
        {
            x1--;
            x2++;
            y1--;
            y2++;
            if (x1 >= 0 && y1 >= 0)
            {
                video.HorizontalLine(x1, x2, y1, tplt.ToString());
                video.HorizontalLine(x1, x2, y2, tpdk.ToString());
                video.VerticalLine(y1, y2, x1, tplt.ToString());
                video.VerticalLine(y1, y2, x2, tpdk.ToString());
            }
        }

        if ((tpflags & shadowPic) != 0)
        {
            x2++;
            y2++;
            if (x1 >= 0 && y1 >= 0)
            {
                video.HorizontalLine(x1 + 1, x2, y2, tpsh.ToString());
                video.VerticalLine(y1 + 1, y2, x2, tpsh.ToString());
            }
        }

        return x2 - x1 + 1;
    }

    /// <summary>Prints at (px, py), moving px past it (with its shadow, when shadowed text is on)</summary>
    void TpPrint(string text)
    {
        tplastx = tpcurx;
        tplasty = tpcury;

        if ((tpflags & shadowText) != 0)
            TpShadowPrint(text, tpfontcolor == tpbg ? tpbg : tpsh);
        else
            TpDraw(text, tpfontcolor);

        tpcurx = tppx;
        tpcury = tppy;
    }

    /// <summary>Text with a shadow one pixel right and down (bstone's ShPrint)</summary>
    void TpShadowPrint(string text, int shadowColor)
    {
        int x = tppx;
        tppx = x + 1;
        tppy++;
        TpDraw(text, shadowColor);
        tppx = x;
        tppy--;
        TpDraw(text, tpfontcolor);
    }

    void TpDraw(string text, int color)
    {
        if (tpfont == null || text.Length == 0)
            return;
        graphics.DrawText(tppx, tppy, text, tpfont, color.ToString());
        tppx += tpfont.Measure(text);
    }

    /// <summary>Prints a character at a time, delay tics apart; a key (if abortable) prints the rest at once</summary>
    void TpSlowPrint(string text, int delay)
    {
        for (int i = 0; i < text.Length; i++)
        {
            TpPrint(text[i].ToString());
            tppx = tpcurx;
            video.Update();

            if (tp.Flags.HasFlag(PresenterFlags.TermSound) && text[i] != ' ' && !string.IsNullOrEmpty(tp.TypeSound))
                audio.Play(tp.TypeSound);

            input.ClearLastKey();
            var start = GameEngineManager.GetTimeCount();
            while (GameEngineManager.GetTimeCount() - start < delay)
            {
                GameEngineManager.DelayMs(5);
                input.ProcessEvents();
                if (tp.Flags.HasFlag(PresenterFlags.Abortable) && input.GetLastKeyPressed() != ScanCodes.None)
                {
                    TpPrint(text[(i + 1)..]);
                    video.Update();
                    tpflags &= ~presenting;
                    return;
                }
            }
        }
    }

    /// <summary>^MO: prints &lt;MORE&gt;, waits for anything, and clears the line</summary>
    void TpMore()
    {
        tppx = tpcurx;
        tppy = tpcury;
        TpPrint("<MORE>");
        video.Update();

        input.ClearKeysDown();
        input.Ack();
        tp.Escaped = input.GetLastKeyPressed() == ScanCodes.Escape;
        if (tp.Escaped)
            tpflags &= ~presenting;

        tpcurx = tpxl;
        video.Bar(tpcurx, tpcury, tpxh - tpxl + 1 + Margin * 2, TpFontHeight + TpShadowed, tpbg.ToString());
    }

    /// <summary>^EP: shows the page and waits to go to the one before or after, or to finish</summary>
    void TpEndPage()
    {
        video.Update();
        if (video.ScreenFaded)
            video.FadeIn();

        input.ClearKeysDown();
        tpflags |= upReleased | downReleased;
        tpanimTime = GameEngineManager.GetTimeCount();
        while (true)
        {
            GameEngineManager.DelayMs(5);
            if (TpAnimatePage())
                video.Update();
            menuInput.ReadAnyControl(out var ci);
            var dir = ci.Dir;
            if (input.IsKeyDown(ScanCodes.PgUp))
                dir = Direction.North;
            else if (input.IsKeyDown(ScanCodes.PgDn))
                dir = Direction.South;

            if (tp.Flags.HasFlag(PresenterFlags.Continue)
                && (ci.Button0 || input.IsKeyDown(ScanCodes.Space) || input.IsKeyDown(ScanCodes.Enter)))
            {
                tp.Escaped = false;
                tpflags &= ~presenting;
                break;
            }

            if (ci.Button1 || input.IsKeyDown(ScanCodes.Escape))
            {
                tp.Escaped = true;
                tpflags &= ~presenting;
                break;
            }

            if ((dir == Direction.North || dir == Direction.West) && tp.PageNum > 0)
            {
                if ((tpflags & upReleased) != 0)
                {
                    tp.PageNum--;
                    tpflags &= ~upReleased;
                    break;
                }
            }
            else
            {
                tpflags |= upReleased;
                if ((dir == Direction.South || dir == Direction.East) && tp.PageNum < tp.Pages.Count - 1)
                {
                    if ((tpflags & downReleased) != 0)
                    {
                        tp.PageNum++;
                        tpflags &= ~downReleased;
                        break;
                    }
                }
                else
                    tpflags |= downReleased;
            }
        }

        tpanims.Clear();        // the next page starts its own
        if ((tpflags & presenting) == 0)
        {
            input.ClearKeysDown();
            return;
        }

        tpcurx = tpxl;
        tpcury = tpyl;
        if (tpcury + TpFontHeight > tpyh)
            tpcury = tpyh - TpFontHeight;
        tppos = tp.Pages[tp.PageNum];

        if (TpAt(tppos) == Control)
        {
            TpHandleCodes();
            tpflags &= ~startOfLine;
        }
        video.Bar(tpxl, tpyl, tpxh - tpxl + 1, tpyh - tpyl + 1, tpbg.ToString());
        TpPrintPageNumber();

        // A held key doesn't flip page after page
        while (input.IsKeyDown(ScanCodes.UpArrow) || input.IsKeyDown(ScanCodes.DownArrow)
            || input.IsKeyDown(ScanCodes.PgUp) || input.IsKeyDown(ScanCodes.PgDn)
            || input.IsKeyDown(ScanCodes.LeftArrow) || input.IsKeyDown(ScanCodes.RightArrow))
        {
            GameEngineManager.DelayMs(5);
            input.ProcessEvents();
        }
    }

    void TpPrintPageNumber()
    {
        if (!tp.Flags.HasFlag(PresenterFlags.ShowPages) || tp.PageX < 0)
            return;

        var oldFont = tpfont;
        var oldColor = tpfontcolor;
        tpfont = PresenterFont(tp.InfoFont);
        tpfontcolor = tp.InfoColor;

        tppx = tppagex[0];
        tppy = tppagey[0];
        video.Bar(tppx, tppy, 12, 7, tpbg.ToString());
        TpShadowPrint($"{tp.PageNum + 1,2}", tpsh);

        tppx = tppagex[1];
        tppy = tppagey[1];
        video.Bar(tppx, tppy, 12, 7, tpbg.ToString());
        TpShadowPrint($"{tp.Pages.Count,2}", tpsh);

        tpfont = oldFont;
        tpfontcolor = oldColor;
    }

    /*
    =============================================================================

                            THE STATUS BAR'S INFO AREA

    A message in Blake Stone's info area can be a little script of its own (bstone's
    DrawInfoArea): ^SH draws a shape and ^AN starts an animation, a sprite shrunk into a box
    (BoxSize square, on the background color) with the text going on to its right; ^FC sets
    the text color, ^BG the box's, ^LM the left margin (fff: here) and ^DM puts it back. Text
    is printed a line at a time, with a shadow, a line break (\r or \n) starting the next line
    at the left margin. One animation runs while the message is up (TickInfoScript).

    =============================================================================
    */

    /// <summary>A shape's box in the info area, each way</summary>
    const int InfoBoxSize = 37;

    // The info area message's animation: what it is, where, and when it next moves on
    TpAnim? infoAnim;
    uint infoAnimTime;

    /// <summary>Whether a message is an info area script: it has ^ codes</summary>
    internal static bool IsInfoScript(string text) =>
        text.Length > 3 && text.IndexOf(Control) is var i && i >= 0 && i + 2 < text.Length && char.IsLetter(text[i + 1]);

    /// <summary>An info area script's words alone, its lines joined by spaces (for the console's history)</summary>
    internal static string InfoScriptText(string script)
    {
        var text = new System.Text.StringBuilder();
        for (int pos = 0; pos < script.Length; pos++)
        {
            if (script[pos] == Control && pos + 2 < script.Length)
            {
                var code = $"{char.ToUpperInvariant(script[pos + 1])}{char.ToUpperInvariant(script[pos + 2])}";
                pos += 2 + TpCodeDigits(code);
                continue;
            }
            text.Append(script[pos]);
        }
        return string.Join(" ", text.ToString().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>
    /// Draws an info area script at (x, y): text in font, color and shadow (palette indices),
    /// shapes on background
    /// </summary>
    internal void DrawInfoScript(string script, int x, int y, Fonts.Font font, int color, int shadow, int background)
    {
        infoAnim = null;
        int left = x, cx = x, cy = y, fontColor = color, back = background;
        int lineHeight = font.Height;
        int pos = 0;
        while (pos < script.Length)
        {
            if (script[pos] == Control && pos + 2 < script.Length)
            {
                var code = $"{char.ToUpperInvariant(script[pos + 1])}{char.ToUpperInvariant(script[pos + 2])}";
                int digits = TpCodeDigits(code);
                int value = 0;
                for (int i = 0; i < digits && pos + 3 + i < script.Length; i++)
                    value = value << 4 | (Uri.IsHexDigit(script[pos + 3 + i]) ? Uri.FromHex(script[pos + 3 + i]) : 0);
                pos += 3 + digits;

                switch (code)
                {
                    case "AN":
                        if (Presenter.Animations.TryGetValue(value, out var animation))
                        {
                            var frames = AnimationFrames(value);
                            infoAnim = new TpAnim(animation, frames, cx, cy, back);
                            infoAnimTime = GameEngineManager.GetTimeCount();
                            DrawInfoShape(cx, cy, frames.FirstOrDefault(), back);
                            cx += InfoBoxSize;
                            left = cx;
                        }
                        break;
                    case "SH":
                        if (ShapePic(value) is { } shape)
                        {
                            cx += DrawInfoShape(cx, cy, shape, back);
                            left = cx;
                        }
                        break;
                    case "FC": fontColor = value; break;
                    case "BG": back = value; break;
                    case "DM": left = x; break;
                    case "LM": left = value == 0xfff ? cx : value; break;
                }
                continue;
            }

            // A line, up to a break or a code
            int end = pos;
            while (end < script.Length && script[end] is not ('\r' or '\n' or Control))
                end++;
            var line = script[pos..end];
            // bstone's shadow, then the text: plain, without the font's own shadow on either
            if (line.Length > 0)
            {
                font.Draw(video, cx + 1, cy + 1, line, shadow.ToString(), Fonts.FontShadow.None, font.Gradient);
                font.Draw(video, cx, cy, line, fontColor.ToString(), Fonts.FontShadow.None, font.Gradient);
            }
            int printed = font.Measure(line);
            pos = end;

            // a line break (\r\n is one), or a space after the line, is passed over
            bool lineBreak = pos < script.Length && script[pos] is '\r' or '\n';
            if (pos < script.Length && script[pos] == '\r' && pos + 1 < script.Length && script[pos + 1] == '\n')
                pos++;
            if (pos < script.Length && script[pos] is ' ' or '\r' or '\n')
                pos++;

            // A code goes on from the end of the text; anything else starts the next line
            if (lineBreak || pos >= script.Length || script[pos] != Control)
            {
                cx = left;
                cy += lineHeight;
            }
            else
                cx += printed;
        }
    }

    /// <summary>Takes the info area's animation away (a message without one has replaced it)</summary>
    internal void ClearInfoScript() => infoAnim = null;

    /// <summary>
    /// Moves the info area's animation on to its next frame once its frame's had its tics
    /// (bstone's AnimatePage); true when it drew one
    /// </summary>
    internal bool TickInfoScript()
    {
        if (infoAnim is not { Frames.Count: > 0 } anim)
            return false;
        uint now = GameEngineManager.GetTimeCount();
        anim.Delay += (int)(now - infoAnimTime);
        infoAnimTime = now;
        if (anim.Delay < anim.Animation.Tics)
            return false;

        anim.Delay = 0;
        anim.Frame = (anim.Frame + 1) % anim.Frames.Count;
        DrawInfoShape(anim.X, anim.Y, anim.Frames[anim.Frame], anim.Background);
        return true;
    }

    /// <summary>
    /// Draws a shape in the info area: a sprite or wall shrunk into its box on the background, a
    /// picture at its own size (8 pixels across). Its width.
    /// </summary>
    int DrawInfoShape(int x, int y, TpShape? shape, int background)
    {
        if (shape == null)
            return 0;
        if (shape.IsPic)
        {
            x = (x + 7) & ~7;
            video.MemToScreen(shape.Pixels, shape.Width, shape.Height, x, y);
            return shape.Width;
        }

        // Shrunk to the box, as bstone's vid_draw_ui_sprite at 37
        const int size = InfoBoxSize;
        var pixels = new byte[size * size];
        var mask = shape.Mask == null ? null : new byte[size * size];
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                int src = j * shape.Height / size * shape.Width + i * shape.Width / size;
                pixels[j * size + i] = shape.Pixels[src];
                if (mask != null)
                    mask[j * size + i] = shape.Mask![src];
            }
        video.Bar(x, y, size, size, background.ToString());
        video.MemToScreen(pixels, size, size, x, y + 1, mask);
        return size;
    }

    /*
    =============================================================================

                            SCREENS ON THE PRESENTER

    =============================================================================
    */

    /// <summary>
    /// Presents a script in presenter.yaml's message box (bstone's BMAmsg), its lines centered up
    /// and down in it. Nothing if the pack has no message box.
    /// </summary>
    internal void PresenterMessageBox(string script)
    {
        if (Presenter.MessageBox is not { } box)
            return;

        int x2 = box.X + 7, y2 = box.Y + 4, w2 = box.Width - 14, h2 = box.Height - 8;
        draw.BevelBox(box.X, box.Y, box.Width, box.Height, box.Hi, box.Med, box.Lo);
        draw.BevelBox(x2, y2, w2, h2, box.Lo, box.Med, box.Hi);

        var font = PresenterFont(box.Font);
        int lines = 1 + script.Count(c => c == ReturnCode);
        int height = (font?.Height ?? 8) * lines + 1 + Margin * 2;

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
        RunPresenter(pi);
    }

    /// <summary>
    /// Shows a briefing (a VGAGRAPH text) in presenter.yaml's briefing window, page by page,
    /// until it's finished or left. Returns whether it was left with Esc.
    /// </summary>
    internal bool ShowBriefing(string textName) => ShowTextPages(textName, fromMenu: false);

    /// <summary>
    /// game-info's lose-screen (bstone's LoseScreen): its picture, and its text typed out a
    /// letter at a time into its window, scrolling as it fills, until a key
    /// </summary>
    internal void ShowLoseScreen(LoseScreenInfo lose)
    {
        if (PresenterScript(lose.Script) is not { } script)
            return;

        video.FadeOut();
        audio.StopMusic();
        video.ClearScreen(0);
        if (!string.IsNullOrEmpty(lose.Pic))
            graphics.DrawPic(lose.Pic, 0, 0);
        video.Update();
        video.FadeIn();

        RunPresenter(new PresenterInfo
        {
            Flags = PresenterFlags.UseCurrent | PresenterFlags.ShowCursor | PresenterFlags.ScrollRegion
                | PresenterFlags.Continue | PresenterFlags.TermSound | PresenterFlags.Abortable,
            Script = script,
            X1 = lose.X1,
            Y1 = lose.Y1,
            X2 = lose.X2,
            Y2 = lose.Y2,
            Font = lose.Font,
            FontColor = lose.Color,
            Background = lose.Background,
            Light = lose.Light,
            Dark = lose.Dark,
            Shadow = lose.Shadow,
            PrintDelay = lose.PrintDelay,
            TypeSound = lose.TypeSound,
        });

        video.FadeOut();
        input.ClearKeysDown();
    }

    /// <summary>
    /// Presenter text in the briefing window, page by page. From the menu only Esc leaves it;
    /// a briefing is also finished with Enter. Returns whether it was left with Esc.
    /// </summary>
    internal bool ShowTextPages(string textName, bool fromMenu)
    {
        if (Presenter.Briefing is not { } briefing || PresenterScript(textName) is not { } script)
            return false;

        video.FadeOut();
        video.ClearScreen(0);
        foreach (var pic in briefing.Pics)
            graphics.DrawPic(pic.Pic, pic.X, pic.Y);

        var music = fromMenu ? briefing.PagesMusic : briefing.Music;
        if (!string.IsNullOrEmpty(music))
            audio.PlayMusic(music);

        var language = assets.GetText("en-us");
        var infoLine = fromMenu ? briefing.PagesInfoLine ?? briefing.InfoLine : briefing.InfoLine;
        var pi = new PresenterInfo
        {
            Flags = PresenterFlags.ShowPages | (fromMenu ? PresenterFlags.None : PresenterFlags.Continue),
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
            InfoLine = infoLine.ToLanguageText(language),
            InfoFont = briefing.InfoFont,
            InfoColor = briefing.InfoColor,
            PageX = briefing.PageX ?? -1,
            PageY = briefing.PageY,
        };
        RunPresenter(pi);

        video.FadeOut();
        input.ClearKeysDown();
        return pi.Escaped;
    }

    /// <summary>A title screen's presenter text, printed into its window</summary>
    internal void DrawTitleText(TitleTextInfo text)
    {
        if (PresenterScript(text.Script) is not { } script)
            return;

        RunPresenter(new PresenterInfo
        {
            Flags = PresenterFlags.CacheNoGfx,
            Script = script,
            X1 = text.X1,
            Y1 = text.Y1,
            X2 = text.X2,
            Y2 = text.Y2,
            Font = text.Font,
            FontColor = text.Color,
            Background = text.Background,
            Light = text.Light,
            Dark = text.Dark,
            Shadow = text.Shadow,
        });
    }
}
