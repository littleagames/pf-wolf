using SDL2;
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

    internal void Read(BinaryReader br)
    {
        name = new string(br.ReadChars(MaxHighName + 1));
        score = br.ReadInt32();
        completed = br.ReadUInt16();
        episode = br.ReadUInt16();
    }

    internal void Write(BinaryWriter bw)
    {
        var len = MaxHighName + 1;
        bw.Write(name.ToFixedArray(len), 0, len);
        bw.Write(score);
        bw.Write(completed);
        bw.Write(episode);
    }
}

struct SaveGame
{
    char[] signature;
    //short[] oldtest;
    bool present;
    char[] name;

    public SaveGame()
    {
        signature = new char[4];
        name = new char[Program.MaxGameName + 1];
    }
}

// Record used to save & restore screen windows
struct WindowRec
{
    public int x, y, w, h, px, py;
}

internal partial class Program
{
    internal static ushort PrintX, PrintY;
    internal static ushort WindowX, WindowY, WindowW, WindowH;

    static bool US_Started;

    internal static SaveGame[] Games = new SaveGame[MaxSaveGames];
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

    internal const int MaxGameName = 32;
    internal const int MaxSaveGames = 6;

    internal const int MaxString = 128;

    internal static void US_HomeWindow()
    {
        PrintX = WindowX;
        PrintY = WindowY;
    }

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


    internal static void USL_MeasureString(string s, out ushort w, out ushort h) => VW_MeasurePropString(s, out w, out h);
    internal static void USL_DrawString(string s) => VWB_DrawPropString(s);
    internal static void US_Print(string sorg)
    {
        ushort w, h;
        if (sorg == null)
            return;

        int index = 0;
        int len = sorg.Length;

        while (index < len)
        {
            // Find end of segment (up to next newline or end of string)
            int se = index;
            while (se < len && sorg[se] != '\n')
                se++;

            // Extract segment
            string segment = sorg.Substring(index, se - index);

            // Measure and draw
            USL_MeasureString(segment, out w, out h);
            px = PrintX;
            py = PrintY;
            USL_DrawString(segment);

            // Advance index
            index = se;
            if (index < len && sorg[index] == '\n')
            {
                // consume newline
                index++;

                // move to start of next line
                PrintX = WindowX;
                PrintY += h;
            }
            else
            {
                // continue on same line
                PrintX += w;
            }
        }
    }

    internal static void US_CPrint(string sorg)
    {
        if (sorg == null)
            return;

        int len = sorg.Length;
        int index = 0;

        while (index < len)
        {
            int se = index;
            while (se < len && sorg[se] != '\n')
                se++;

            // substring for current line (may be empty)
            string segment = sorg.Substring(index, se - index);
            US_CPrintLine(segment);

            // advance index past the processed part
            index = se;

            // if we're at a newline, skip it and continue to next line
            if (index < len && sorg[index] == '\n')
                index++;
        }
    }

    internal static void USL_PrintInCenter(string s, Rect r)
    {
        ushort w, h,
                rw, rh;

        USL_MeasureString(s, out w, out h);
        rw = (ushort)(r.lr.x - r.ul.x);
        rh = (ushort)(r.lr.y - r.ul.y);

        px = r.ul.x + ((rw - w) / 2);
        py = r.ul.y + ((rh - h) / 2);
        USL_DrawString(s);
    }

    internal static void US_PrintCentered(string s)
    {
        Rect r = new Rect();

        r.ul.x = WindowX;
        r.ul.y = WindowY;
        r.lr.x = r.ul.x + WindowW;
        r.lr.y = r.ul.y + WindowH;

        USL_PrintInCenter(s, r);
    }

    internal static void US_CPrintLine(string s)
    {
        ushort w, h;

        USL_MeasureString(s, out w, out h);

        if (w > WindowW)
            Quit("US_CPrintLine() - String exceeds width");
        px = WindowX + ((WindowW - w) / 2);
        py = PrintY;
        USL_DrawString(s);
        PrintY += h;
    }

    internal static bool US_LineInput(int x, int y, ref string buf, string def, bool escok, int maxchars, int maxwidth)
    {
        bool redraw,
                    cursorvis, cursormoved,
                    done, result = false, checkkey;
        int sc;
        string s, olds;
        //char[] s = new char[MaxString], olds = new char[MaxString];
        int cursor, len;
        ushort i,
                    w, h,
                    temp;
        uint curtime, lasttime, lastdirtime, lastbuttontime, lastdirmovetime;
        ControlInfo ci;
        byte lastdir = (byte)Direction.dir_None;

        if (!string.IsNullOrEmpty(def))
            s = def;
        else
            s = "";

        olds = "";
        cursor = s.Length;

        cursormoved = redraw = true;

        cursorvis = done = false;
        lasttime = lastdirtime = lastdirmovetime = GetTimeCount();
        lastbuttontime = lasttime + TickBase / 4;   // 250 ms => first button press accepted after 500 ms
        LastScan = (int)ScanCodes.sc_None;

        IN_ClearTextInput();
        while (!done)
        {
            ReadAnyControl(out ci);

            if (cursorvis)
                USL_XORICursor(x, y, new string(s), (ushort)cursor);

            sc = LastScan;
            LastScan = (int)ScanCodes.sc_None;

            checkkey = true;
            curtime = GetTimeCount();

            // After each direction change accept the next change after 250 ms and then everz 125 ms
            if (ci.dir != lastdir || (curtime - lastdirtime > TickBase / 4 && curtime - lastdirmovetime > TickBase / 8))
            {
                if (ci.dir != lastdir)
                {
                    lastdir = ci.dir;
                    lastdirtime = curtime;
                }
                lastdirmovetime = curtime;

                switch ((Direction)ci.dir)
                {
                    case Direction.dir_West:
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
                    case Direction.dir_East:
                        if (cursor >= MaxString - 1) break;

                        if (s.Length == cursor)
                        {
                            USL_MeasureString(new string(s), out w, out h);
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

                    case Direction.dir_North:
                        {
                            if (string.IsNullOrEmpty(s) || s[cursor] == 0)
                            {
                                USL_MeasureString(new string(s), out w, out h);
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

                    case Direction.dir_South:
                        {
                            if (string.IsNullOrEmpty(s) || s[cursor] == 0)
                            {
                                USL_MeasureString(new string(s), out w, out h);
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

            if ((int)(curtime - lastbuttontime) > TickBase / 4)   // 250 ms
            {
                if (ci.button0 != 0)             // acts as return
                {
                    buf = s; //snprintf(buf, maxchars + 1, "%s", s);
                    done = true;
                    result = true;
                    checkkey = false;
                }
                if (ci.button1 != 0 && escok)    // acts as escape
                {
                    done = true;
                    result = false;
                    checkkey = false;
                }
                if (ci.button2 != 0)             // acts as backspace
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
                    case (byte)ScanCodes.sc_LeftArrow:
                        if (cursor != 0)
                            cursor--;
                        cursormoved = true;
                        break;
                    case (byte)ScanCodes.sc_RightArrow:
                        if (s[cursor] != 0)
                            cursor++;
                        cursormoved = true;
                        break;
                    case (byte)ScanCodes.sc_Home:
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
                    case (byte)ScanCodes.sc_End:
                        cursor = s.Length;
                        cursormoved = true;
                        break;

                    case (byte)ScanCodes.sc_Return:
                        buf = s;
                        done = true;
                        result = true;
                        break;
                    case (byte)ScanCodes.sc_Escape:
                        if (escok)
                        {
                            done = true;
                            result = false;
                        }
                        break;

                    case (byte)ScanCodes.sc_BackSpace:
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

                    case (byte)ScanCodes.sc_Delete:
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

                //for (text = textinput; *text; text++)
                for(int t = 0; t < textinput.Length && textinput[t] != '\0'; t++)
                {
                    char txt = textinput[t];
                    //len = (int)strlen(s);
                    USL_MeasureString(new string(s), out w, out h);

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

                IN_ClearTextInput();
            }

            if (redraw)
            {
                px = x;
                py = y;
                temp = fontcolor;
                fontcolor = backcolor;
                USL_DrawString(new string(olds));
                fontcolor = (byte)temp;
                olds = s;

                px = x;
                py = y;
                USL_DrawString(new string(s));

                redraw = false;
            }

            if (cursormoved)
            {
                cursorvis = false;
                lasttime = curtime - TickBase;

                cursormoved = false;
            }
            if (curtime - lasttime > TickBase / 2)    // 500 ms
            {
                lasttime = curtime;

                cursorvis ^= true;
            }
            else SDL.SDL_Delay(5);
            if (cursorvis)
                USL_XORICursor(x, y, new string(s), (ushort)cursor);

            VW_UpdateScreen();
        }

        if (cursorvis)
            USL_XORICursor(x, y, new string(s), (ushort)cursor);
        if (!result)
        {
            px = x;
            py = y;
            USL_DrawString(new string (olds));
        }
        VW_UpdateScreen();

        IN_ClearKeysDown();
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

    internal static void US_ClearWindow()
    {
        VWB_Bar(WindowX, WindowY, WindowW, WindowH, WHITE);
        PrintX = WindowX;
        PrintY = WindowY;
    }

    internal static void US_DrawWindow(ushort x, ushort y, ushort w, ushort h)
    {
        ushort i,
                sx, sy, sw, sh;

        WindowX = (ushort)(x * 8);
        WindowY = (ushort)(y * 8);
        WindowW = (ushort)(w * 8);
        WindowH = (ushort)(h * 8);

        PrintX = WindowX;
        PrintY = WindowY;

        sx = (ushort)((x - 1) * 8);
        sy = (ushort)((y - 1) * 8);
        sw = (ushort)((w + 1) * 8);
        sh = (ushort)((h + 1) * 8);

        US_ClearWindow();

        VWB_DrawTile8(sx, sy, 0);
        VWB_DrawTile8(sx, sy + sh, 5);
        for (i = (ushort)(sx + 8); i <= sx + sw - 8; i += 8) {
            VWB_DrawTile8(i, sy, 1);
            VWB_DrawTile8(i, sy + sh, 6);
        }
        VWB_DrawTile8(i, sy, 2);
        VWB_DrawTile8(i, sy + sh, 7);

        for (i = (ushort)(sy + 8); i <= sy + sh - 8; i += 8) {
            VWB_DrawTile8(sx, i, 3);
            VWB_DrawTile8(sx + sw, i, 4);
        }
    }
    ///////////////////////////////////////////////////////////////////////////
    //
    //	US_CenterWindow() - Generates a window of a given width & height in the
    //		middle of the screen
    //
    ///////////////////////////////////////////////////////////////////////////
    internal static void US_CenterWindow(ushort w, ushort h)
    {
        US_DrawWindow((ushort)(((MaxX / 8) - w) / 2), (ushort)(((MaxY / 8) - h) / 2), w, h);
    }

    private static bool _xoricursor_status = false;
    internal static void USL_XORICursor(int x, int y, string s, ushort cursor)
    {
        string buf;
        int temp;
        ushort w, h;

        buf = s;
        // buf[cursor] = '\0'; // not necessary for C# strings
        USL_MeasureString(buf, out w, out h);

        px = x + w - 1;
        py = y;
        if (_xoricursor_status ^= true)
            USL_DrawString("\x80");
        else
        {
            temp = fontcolor;
            fontcolor = backcolor;
            USL_DrawString("\x80");
            fontcolor = (byte)temp;
        }
    }

    internal static int US_RndT()
    {
        rndindex = (rndindex + 1) & 0xff;
        return rndtable[rndindex];
    }
}
