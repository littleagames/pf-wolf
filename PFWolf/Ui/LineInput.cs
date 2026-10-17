using PFWolf.Fonts;

namespace PFWolf.Ui;

/// <summary>Reads the keyboard, mouse and controller as the menus do (GameHost.ReadAnyControl)</summary>
internal delegate void ReadControl(out ControlInfo ci);

/// <summary>
/// A line of text typed in: a save's name, a high score's. A controller's up and down pick
/// letters (RotateChar), left and right move along, and its buttons are enter, escape and
/// backspace.
/// </summary>
internal sealed class LineInput(GraphicManager graphics, InputManager input, VideoManager video, UiText ui, ReadControl readAnyControl)
{
    internal const int MaxString = 128;
    /// <summary>
    /// Lets a line of text be typed in at (x, y), in <paramref name="style"/>; its background
    /// color is what erases the old text and blinks the cursor
    /// </summary>
    internal bool Run(int x, int y, ref string buf, string def, bool escok, int maxchars, int maxwidth, TextStyle style)
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
        input.ClearLastKey();

        input.ClearTextInput();
        while (!done)
        {
            readAnyControl(out ci);

            if (cursorvis)
                ui.XORICursor(x, y, s, cursor, style);

            sc = input.GetLastKeyPressed();
            input.ClearLastKey();

            checkkey = true;
            curtime = GameEngineManager.GetTimeCount();

            // After each direction change accept the next change after 250 ms and then everz 125 ms
            if (ci.Dir != lastdir || (curtime - lastdirtime > Timing.TickBase / 4 && curtime - lastdirmovetime > Timing.TickBase / 8))
            {
                if (ci.Dir != lastdir)
                {
                    lastdir = ci.Dir;
                    lastdirtime = curtime;
                }
                lastdirmovetime = curtime;

                switch ((Direction)ci.Dir)
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
                            ui.MeasureText(s, style.Font, out w, out h);
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
                                ui.MeasureText(s, style.Font, out w, out h);
                                if (s.Length >= maxchars || (maxwidth != 0 && w >= maxwidth))
                                    break;
                                s += ' ';
                            }
                            var cs = s.ToCharArray();
                            cs[cursor] = RotateChar(s[cursor], 1);
                            s = new string(cs);
                        }
                        redraw = true;
                        checkkey = false;
                        break;

                    case Direction.South:
                        {
                            if (string.IsNullOrEmpty(s) || s[cursor] == 0)
                            {
                                ui.MeasureText(s, style.Font, out w, out h);
                                if (s.Length >= maxchars || (maxwidth != 0 && w >= maxwidth))
                                    break;
                                s += ' ';
                            }
                            var cs = s.ToCharArray();
                            cs[cursor] = RotateChar(s[cursor], -1);
                            s = new string(cs);
                            redraw = true;
                            checkkey = false;
                        }
                        break;
                }
            }

            if ((int)(curtime - lastbuttontime) > Timing.TickBase / 4)   // 250 ms
            {
                if (ci.Button0)             // acts as return
                {
                    buf = s; //snprintf(buf, maxchars + 1, "%s", s);
                    done = true;
                    result = true;
                    checkkey = false;
                }
                if (ci.Button1 && escok)    // acts as escape
                {
                    done = true;
                    result = false;
                    checkkey = false;
                }
                if (ci.Button2)             // acts as backspace
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
                    case ScanCodes.LeftArrow:
                        if (cursor != 0)
                            cursor--;
                        cursormoved = true;
                        break;
                    case ScanCodes.RightArrow:
                        if (s[cursor] != 0)
                            cursor++;
                        cursormoved = true;
                        break;
                    case ScanCodes.Home:
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
                    case ScanCodes.End:
                        cursor = s.Length;
                        cursormoved = true;
                        break;

                    case ScanCodes.Return:
                        buf = s;
                        done = true;
                        result = true;
                        break;
                    case ScanCodes.Escape:
                        if (escok)
                        {
                            done = true;
                            result = false;
                        }
                        break;

                    case ScanCodes.BackSpace:
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

                    case ScanCodes.Delete:
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
                var textinput = input.GetTextInput();
                //for (text = TextInput; *text; text++)
                for (int t = 0; t < textinput.Length && textinput[t] != '\0'; t++)
                {
                    char txt = textinput[t];
                    //len = (int)strlen(s);
                    ui.MeasureText(s, style.Font, out w, out h);

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

                input.ClearTextInput();
            }

            if (redraw)
            {
                graphics.DrawText(x, y, olds, style.Inverted);
                olds = s;
                graphics.DrawText(x, y, s, style);

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
                ui.XORICursor(x, y, s, cursor, style);

            video.Update();
        }

        if (cursorvis)
            ui.XORICursor(x, y, s, cursor, style);
        if (!result)
            graphics.DrawText(x, y, olds, style);
        video.Update();

        input.ClearKeysDown();
        return result;
    }

    /// <summary>The next or previous character a controller's up or down picks</summary>
    internal static char RotateChar(char ch, int dir)
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
}
