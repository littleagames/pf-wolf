using SDL2;
using Wolf3D.Fonts;
using static Wolf3D.Program;

namespace Wolf3D;

internal class HighScore
{
    public string name;
    public int score;
    public ushort completed, episode;

    public HighScore()
    {
        name = "";
    }

    // The name is a fixed, zero-padded run of Latin-1 bytes. (Written as chars it was UTF-8,
    // where an accented letter takes two bytes and shifts everything after it.)
    internal void Read(BinaryReader br)
    {
        var nameBytes = br.ReadBytes(MaxHighName + 1);
        if (nameBytes.Length != MaxHighName + 1)
            throw new EndOfStreamException();
        name = System.Text.Encoding.Latin1.GetString(nameBytes).TrimEnd('\0');
        score = br.ReadInt32();
        completed = br.ReadUInt16();
        episode = br.ReadUInt16();
    }

    internal void Write(BinaryWriter bw)
    {
        var nameBytes = new byte[MaxHighName + 1];
        System.Text.Encoding.Latin1.GetBytes(name, 0, Math.Min(name.Length, MaxHighName), nameBytes, 0);
        bw.Write(nameBytes);
        bw.Write(score);
        bw.Write(completed);
        bw.Write(episode);
    }
}

internal partial class Program
{
    static bool US_Started;

    internal static HighScore[] Scores = new HighScore[MaxScores]
    {
        new HighScore {name = "id software-'92", score = 10000,completed = 1},
        new HighScore {name = "Adrian Carmack", score = 10000,completed = 1},
        new HighScore {name = "John Carmack", score = 10000,completed = 1},
        new HighScore {name = "Kevin Cloud", score = 10000,completed = 1},
        new HighScore {name = "Tom Hall", score = 10000,completed = 1},
        new HighScore {name = "John Romero", score = 10000,completed = 1},
        new HighScore {name = "Jay Wilbur", score = 10000,completed = 1},
    };

    internal const int MaxX = 320;
    internal const int MaxY = 200;

    internal const int MaxHelpLines = 500;

    internal const int MaxHighName = 57;
    internal const int MaxScores = 7;

    internal const int MaxGameName = 32;    // a save's name, as typed in the save menu, is one less

    internal const int MaxString = 128;

    static int rndindex = 0;


    static byte[] rndtable = {
      0,   8, 109, 220, 222, 241, 149, 107,  75, 248, 254, 140,  16,  66,
     74,  21, 211,  47,  80, 242, 154,  27, 205, 128, 161,  89,  77,  36,
     95, 110,  85,  48, 212, 140, 211, 249,  22,  79, 200,  50,  28, 188,
     52, 140, 202, 120,  68, 145,  62,  70, 184, 190,  91, 197, 152, 224,
    149, 104,  25, 178, 252, 182, 202, 182, 141, 197,   4,  81, 181, 242,
    145,  42,  39, 227, 156, 198, 225, 193, 219,  93, 122, 175, 249,   0,
    175, 143,  70, 239,  46, 246, 163,  53, 163, 109, 168, 135,   2, 235,
     25,  92,  20, 145, 138,  77,  69, 166,  78, 176, 173, 212, 166, 113,
     94, 161,  41,  50, 239,  49, 111, 164,  70,  60,   2,  37, 171,  75,
    136, 156,  11,  56,  42, 146, 138, 229,  73, 146,  77,  61,  98, 196,
    135, 106,  63, 197, 195,  86,  96, 203, 113, 101, 170, 247, 181, 113,
     80, 250, 108,   7, 255, 237, 129, 226,  79, 107, 112, 166, 103, 241,
     24, 223, 239, 120, 198,  58,  60,  82, 128,   3, 184,  66, 143, 224,
    145, 224,  81, 206, 163,  45,  63,  90, 168, 114,  59,  33, 159,  95,
     28, 139, 123,  98, 125, 196,  15,  70, 194, 253,  54,  14, 109, 226,
     71,  17, 161,  93, 186,  87, 244, 138,  20,  52, 123, 251,  26,  36,
     17,  46,  52, 231, 232,  76,  31, 221,  84,  37, 216, 165, 212, 106,
    197, 242,  98,  43,  39, 175, 254, 145, 190,  84, 118, 222, 187, 136,
    120, 163, 236, 249 };

    internal static void US_Startup()
    {
        if (US_Started)
            return;

        US_InitRndT(true);

        US_Started = true;
    }

    internal static void US_Shutdown()
    {
        if (!US_Started)
            return;

        US_Started = false;
    }

    internal static void US_InitRndT(bool randomize)
    {
        if (randomize)
            rndindex = (int)((SDL.SDL_GetTicks() >> 4) & 0xff);
        else
            rndindex = 0;
    }


    /// <summary>Text printed from (x, y), coming back to x after a newline</summary>
    internal static TextWindow TextAt(int x, int y, TextStyle style) => TextWindow.At(_graphicManager, x, y, style);

    /// <summary>Lines centered between x and x + width, the first at y</summary>
    internal static TextWindow CenteredText(int x, int width, int y, TextStyle style) => new(_graphicManager, x, y, width, 200 - y, style);

    internal static void MeasureText(string text, string font, out int width, out int height) => _graphicManager.MeasureText(text, font, out width, out height);

    internal static int TextWidth(string text, string font)
    {
        MeasureText(text, font, out int width, out _);
        return width;
    }

    /// <summary>Cuts text short (to one character at least) to fit a width in a font.</summary>
    internal static string FitText(string text, int width, string font)
    {
        var f = _fontManager.Find(font);
        if (f == null || text.Length == 0)
            return text;

        return text[..Math.Max(1, f.Fit(text, width))];
    }

    /// <summary>
    /// Lets a line of text be typed in at (x, y), in <paramref name="style"/>; its background
    /// color is what erases the old text and blinks the cursor
    /// </summary>
    internal static bool US_LineInput(int x, int y, ref string buf, string def, bool escok, int maxchars, int maxwidth, TextStyle style)
    {
        bool redraw,
                    cursorvis, cursormoved,
                    done, result = false, checkkey;
        ScanCodes sc;
        string s, olds;
        //char[] s = new char[MaxString], olds = new char[MaxString];
        int cursor, w, h;
        uint curtime, lasttime, lastdirtime, lastbuttontime, lastdirmovetime;
        ControlInfo ci;
        Direction lastdir = Direction.None;

        if (!string.IsNullOrEmpty(def))
            s = def;
        else
            s = "";

        olds = "";
        cursor = s.Length;

        cursormoved = redraw = true;

        cursorvis = done = false;
        lasttime = lastdirtime = lastdirmovetime = GameEngineManager.GetTimeCount();
        lastbuttontime = lasttime + Timing.TickBase / 4;   // 250 ms => first button press accepted after 500 ms
        _inputManager.ClearLastKey();

        _inputManager.ClearTextInput();
        while (!done)
        {
            ReadAnyControl(out ci);

            if (cursorvis)
                USL_XORICursor(x, y, s, cursor, style);

            sc = _inputManager.GetLastKeyPressed();
            _inputManager.ClearLastKey();

            checkkey = true;
            curtime = GameEngineManager.GetTimeCount();

            // After each direction change accept the next change after 250 ms and then everz 125 ms
            if (ci.dir != lastdir || (curtime - lastdirtime > Timing.TickBase / 4 && curtime - lastdirmovetime > Timing.TickBase / 8))
            {
                if (ci.dir != lastdir)
                {
                    lastdir = ci.dir;
                    lastdirtime = curtime;
                }
                lastdirmovetime = curtime;

                switch ((Direction)ci.dir)
                {
                    case Direction.West:
                        if (cursor != 0)
                        {
                            // Remove trailing whitespace if cursor is at end of string
                            //if (s[cursor] == ' ' && s[cursor + 1] == 0)
                            //    s[cursor] = (char)0;
                            s = s.TrimEnd();
                            cursor--;
                        }
                        cursormoved = true;
                        checkkey = false;
                        break;
                    case Direction.East:
                        if (cursor >= MaxString - 1) break;

                        if (s.Length == cursor)
                        {
                            MeasureText(s, style.Font, out w, out h);
                            if (s.Length >= maxchars || (maxwidth != 0 && w >= maxwidth))
                                break;

                            s += ' ';
                            //s[cursor] = ' ';
                            //s[cursor + 1] = (char)0;
                        }
                        cursor++;
                        cursormoved = true;
                        checkkey = false;
                        break;

                    case Direction.North:
                        {
                            if (string.IsNullOrEmpty(s) || s[cursor] == 0)
                            {
                                MeasureText(s, style.Font, out w, out h);
                                if (s.Length >= maxchars || (maxwidth != 0 && w >= maxwidth))
                                    break;
                                s += ' ';
                            }
                            var cs = s.ToCharArray();
                            cs[cursor] = USL_RotateChar(s[cursor], 1);
                            s = new string(cs);
                        }
                        redraw = true;
                        checkkey = false;
                        break;

                    case Direction.South:
                        {
                            if (string.IsNullOrEmpty(s) || s[cursor] == 0)
                            {
                                MeasureText(s, style.Font, out w, out h);
                                if (s.Length >= maxchars || (maxwidth != 0 && w >= maxwidth))
                                    break;
                                s += ' ';
                            }
                            var cs = s.ToCharArray();
                            cs[cursor] = USL_RotateChar(s[cursor], -1);
                            s = new string(cs);
                            redraw = true;
                            checkkey = false;
                        }
                        break;
                }
            }

            if ((int)(curtime - lastbuttontime) > Timing.TickBase / 4)   // 250 ms
            {
                if (ci.button0)             // acts as return
                {
                    buf = s; //snprintf(buf, maxchars + 1, "%s", s);
                    done = true;
                    result = true;
                    checkkey = false;
                }
                if (ci.button1 && escok)    // acts as escape
                {
                    done = true;
                    result = false;
                    checkkey = false;
                }
                if (ci.button2)             // acts as backspace
                {
                    lastbuttontime = curtime;
                    if (cursor != 0)
                    {
                        s = s.Remove(--cursor, 1);
                        //s = s.Substring(0, cursor - 1) + s.Substring(cursor, s.Length - cursor);
                        // TODO: split? or substrings
                        // Need to shift all elements ahead of cursor -1?
                        // String.Remove(index);
                        //len = new string(s).Length + (--cursor) + 1;
                        //memmove(&s[cursor], &s[cursor + 1], len);
                        redraw = true;
                    }
                    cursormoved = true;
                    checkkey = false;
                }
            }

            if (checkkey)
            {
                switch (sc)
                {
                    case ScanCodes.sc_LeftArrow:
                        if (cursor != 0)
                            cursor--;
                        cursormoved = true;
                        break;
                    case ScanCodes.sc_RightArrow:
                        if (s[cursor] != 0)
                            cursor++;
                        cursormoved = true;
                        break;
                    case ScanCodes.sc_Home:
                        if (cursor > 0)
                        {
                            cursor--;

                            //
                            // delete trailing whitespace
                            //
                            s.TrimEnd();
                            //while (cursor >= 0 && s[cursor] == ' ' && s[cursor + 1] == '\0')
                            //    s[cursor--] = '\0';

                            cursor = 0;
                        }
                        cursormoved = true;
                        break;
                    case ScanCodes.sc_End:
                        cursor = s.Length;
                        cursormoved = true;
                        break;

                    case ScanCodes.sc_Return:
                        buf = s;
                        done = true;
                        result = true;
                        break;
                    case ScanCodes.sc_Escape:
                        if (escok)
                        {
                            done = true;
                            result = false;
                        }
                        break;

                    case ScanCodes.sc_BackSpace:
                        if (cursor != 0)
                        {
                            //s = s.Substring(0, Math.Max(0, cursor - 1)) + s.Substring(cursor, s.Length - cursor);
                            s = s.Remove(--cursor, 1);
                            //s.Remove(cursor, 1);
                            //len = strlen(&s[--cursor]) + 1;
                            //memmove(&s[cursor], &s[cursor + 1], len);
                            redraw = true;
                        }
                        cursormoved = true;
                        break;

                    case ScanCodes.sc_Delete:
                        if (s[cursor] != 0)
                        {
                            s = s.Substring(0, cursor) + s.Substring(cursor + 1, s.Length - cursor + 1);
                            s = s.Remove(cursor, 1);
                            //s.Remove(cursor, 1);
                            //len = strlen(&s[cursor]) + 1;
                            //memmove(&s[cursor], &s[cursor + 1], len);
                            redraw = true;
                        }
                        cursormoved = true;
                        break;
                }
                var textinput = _inputManager.GetTextInput();
                //for (text = textinput; *text; text++)
                for (int t = 0; t < textinput.Length && textinput[t] != '\0'; t++)
                {
                    char txt = textinput[t];
                    //len = (int)strlen(s);
                    MeasureText(s, style.Font, out w, out h);

                    if (!char.IsControl(txt) && (s.Length < MaxString - 1) && ((maxchars == 0) || (s.Length < maxchars))
                        && ((maxwidth == 0) || (w < maxwidth)))
                    {
                        //for (i = (ushort)(s.Length + 1); i > cursor; i--)
                        //    s = s.Substring(0, i - 1) + s.Substring(i, s.Length - i); 
                        //s[i] = s[i - 1];
                        //s.Append(txt);
                        s += txt;
                        cursor++;
                        redraw = true;
                    }
                }

                _inputManager.ClearTextInput();
            }

            if (redraw)
            {
                _graphicManager.DrawText(x, y, olds, style.Inverted);
                olds = s;
                _graphicManager.DrawText(x, y, s, style);

                redraw = false;
            }

            if (cursormoved)
            {
                cursorvis = false;
                lasttime = curtime - Timing.TickBase;

                cursormoved = false;
            }
            if (curtime - lasttime > Timing.TickBase / 2)    // 500 ms
            {
                lasttime = curtime;

                cursorvis ^= true;
            }
            else GameEngineManager.DelayMs(5);
            if (cursorvis)
                USL_XORICursor(x, y, s, cursor, style);

            _videoManager.Update();
        }

        if (cursorvis)
            USL_XORICursor(x, y, s, cursor, style);
        if (!result)
            _graphicManager.DrawText(x, y, olds, style);
        _videoManager.Update();

        _inputManager.ClearKeysDown();
        return result;
    }
    internal static char USL_RotateChar(char ch, int dir)
    {
        const string charSet = " ABCDEFGHIJKLMNOPQRSTUVWXYZ.,-!?0123456789";
        int numChars = charSet.Length;
        int i;
        for (i = 0; i < numChars; i++)
        {
            if (ch == charSet[i]) break;
        }

        if (i == numChars) i = 0;

        i += dir;
        if (i < 0) i = numChars - 1;
        else if (i >= numChars) i = 0;
        return charSet[i];
    }

    /// <summary>
    /// Draws a white window with a TILE8 frame, in 8 pixel tiles, and returns it to print into
    /// </summary>
    internal static TextWindow US_DrawWindow(int x, int y, int w, int h, TextStyle style)
    {
        int i, sx, sy, sw, sh;

        var window = new TextWindow(_graphicManager, x * 8, y * 8, w * 8, h * 8, style);

        sx = (x - 1) * 8;
        sy = (y - 1) * 8;
        sw = (w + 1) * 8;
        sh = (h + 1) * 8;

        window.Clear("White");

        _graphicManager.DrawTile8(sx, sy, 0);
        _graphicManager.DrawTile8(sx, sy + sh, 5);
        for (i = sx + 8; i <= sx + sw - 8; i += 8) {
            _graphicManager.DrawTile8(i, sy, 1);
            _graphicManager.DrawTile8(i, sy + sh, 6);
        }
        _graphicManager.DrawTile8(i, sy, 2);
        _graphicManager.DrawTile8(i, sy + sh, 7);

        for (i = sy + 8; i <= sy + sh - 8; i += 8) {
            _graphicManager.DrawTile8(sx, i, 3);
            _graphicManager.DrawTile8(sx + sw, i, 4);
        }

        return window;
    }

    private static bool _xoricursor_status = false;
    internal static void USL_XORICursor(int x, int y, string s, int cursor, TextStyle style)
    {
        int w = TextWidth(s, style.Font);

        // Blinks by drawing the cursor glyph in the text color, then in the background color;
        // without a shadow, which would reach below the line and wear away what's there
        style = style with { Shadow = FontShadow.None };
        _graphicManager.DrawText(x + w - 1, y, "\x80", (_xoricursor_status ^= true) ? style : style.Inverted);
    }

    internal static int US_RndT()
    {
        rndindex = (rndindex + 1) & 0xff;
        return rndtable[rndindex];
    }
}
