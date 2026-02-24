using SDL2;
using System;

namespace Wolf3D;

internal partial class Program
{

    /*
    =============================================================================

                                                     LOCAL CONSTANTS

    =============================================================================
    */
    private const int BACKCOLOR = 0x11;


    private const int WORDLIMIT = 80;
    private const int FONTHEIGHT = 10;
    private const int TOPMARGIN = 16;
    private const int BOTTOMMARGIN = 32;
    private const int LEFTMARGIN = 16;
    private const int RIGHTMARGIN = 16;
    private const int PICMARGIN = 8;
    private const int TEXTROWS = ((200 - TOPMARGIN - BOTTOMMARGIN) / FONTHEIGHT);
    private const int SPACEWIDTH = 7;
    private const int SCREENPIXWIDTH = 320;
    private const int SCREENMID = (SCREENPIXWIDTH / 2);

    /*
    =============================================================================

                                    LOCAL VARIABLES

    =============================================================================
    */

    static int pagenum;
    static int numpages;

    static uint[] leftmargin = new uint[TEXTROWS];
    static uint[] rightmargin = new uint[TEXTROWS];
    static string text = "";
    static int textIndex = 0;
    static uint rowon;

    static int picx;
    static int picy;
    static int picnum;
    static int picdelay;
    static bool layoutdone;

    static int endextern = (int)graphicnums.T_ENDART1;
    static int helpextern = (int)graphicnums.T_HELPART;

    internal static string helpfilename = "HELPART.";
    internal static string endfilename = "ENDART1.";

    internal static void HelpScreens()
    {
        int artnum;
        //string text;
        artnum = helpextern;
        //text = (char*)grsegs[artnum];
        text = new string(System.Text.Encoding.ASCII.GetString(grsegs[artnum]).ToCharArray());
        ShowArticle(text);
        VW_FadeOut();

        FreeMusic();
    }

    internal static void EndText()
    {
        int artnum;
        string text;
        ClearMemory();

        artnum = endextern + gamestate.episode;
        text = new string(System.Text.Encoding.ASCII.GetString(grsegs[artnum]).ToCharArray());

        ShowArticle(text);

        VW_FadeOut();
        SETFONTCOLOR(0, 15);
        IN_ClearKeysDown();
        IN_CenterMouse();

        FreeMusic();
    }

    internal static void ShowArticle(string article)
    {
        uint oldfontnumber;
        bool newpage, firstpage;
        ControlInfo ci;

        text = article;
        oldfontnumber = (uint)fontnumber;
        fontnumber = 0;
        VWB_Bar(0, 0, 320, 200, BACKCOLOR);
        CacheLayout();

        newpage = true;
        firstpage = true;
        do
        {
            if (newpage)
            {
                newpage = false;
                PageLayout(true);
                VW_UpdateScreen();
                if (firstpage)
                {
                    VL_FadeIn(0, 255, gamepal, 10);
                    firstpage = false;
                }
            }
            SDL.SDL_Delay(5);

            LastScan = 0;
            ReadAnyControl(out ci);
            byte dir = ci.dir;
            switch (dir)
            {
                case (byte)Direction.dir_North:
                case (byte)Direction.dir_South:
                    break;

                default:
                    if (ci.button0 != 0) dir = (byte)Direction.dir_South;
                    switch (LastScan)
                    {
                        case (int)ScanCodes.sc_UpArrow:
                        case (int)ScanCodes.sc_PgUp:
                        case (int)ScanCodes.sc_LeftArrow:
                            dir = (byte)Direction.dir_North;
                            break;

                        case (int)ScanCodes.sc_Enter:
                        case (int)ScanCodes.sc_DownArrow:
                        case (int)ScanCodes.sc_PgDn:
                        case (int)ScanCodes.sc_RightArrow:
                            dir = (byte)Direction.dir_South;
                            break;
                    }
                    break;
            }

            switch (dir)
            {
                case (byte)Direction.dir_North:
                case (byte)Direction.dir_West:
                    if (pagenum > 1)
                    {
                        BackPage();
                        BackPage();
                        newpage = true;
                    }
                    TicDelay(20);
                    break;

                case (byte)Direction.dir_South:
                case (byte)Direction.dir_East:
                    if (pagenum < numpages)
                    {
                        newpage = true;
                    }
                    TicDelay(20);
                    break;
            }
        } while (LastScan != (int)ScanCodes.sc_Escape && ci.button1 == 0);

        IN_ClearKeysDown();
        fontnumber = (int)oldfontnumber;
    }

    /*
=====================
=
= BackPage
=
= Scans for a previous ^P
=
=====================
*/

    private static void BackPage()
    {
        pagenum--;
        do
        {
            textIndex--;
            if (text[textIndex] == '^' && char.ToUpper(text[textIndex+1]) == 'P')
                return;
        } while (true);
    }

    private static void CacheLayout()
    {
        int textstart = 0;
        char ch;
        int bombpoint = textIndex+30000;

        textstart = textIndex;
        numpages = pagenum = 0;

        do
        {
            if (text[textIndex] == '^')
            {
                ch = Char.ToUpper(text[++textIndex]);
                if (ch == 'P')          // start of a page
                    numpages++;
                if (ch == 'E')          // end of file, so return
                {
                    textIndex =  textstart;
                    return;
                }

                if (ch == 'G')          // draw graphic command
                    ParsePicCommand();

                if (ch == 'T')          // timed draw graphic command
                    ParseTimedCommand();
            }
            else
                textIndex++;

        } while (textIndex < bombpoint);

        Quit("CacheLayout: No ^E to terminate file!");
    }

    private static void ParsePicCommand()
    {
        picy = ParseNumber();
        picx = ParseNumber();
        picnum = ParseNumber();
        RipToEOL();
    }


    private static void ParseTimedCommand()
    {
        picy = ParseNumber();
        picx = ParseNumber();
        picnum = ParseNumber();
        picdelay = ParseNumber();
        RipToEOL();
    }
/*
=====================
=
= TimedPicCommand
=
= Call with text pointing just after a ^P
= Upon exit text points to the start of next line
=
=====================
*/

    private static void TimedPicCommand()
    {
        ParseTimedCommand();

        //
        // update the screen, and wait for time delay
        //
        VW_UpdateScreen();

        //
        // wait for time
        //
        Delay(picdelay);

        //
        // draw pic
        //
        VWB_DrawPic(picx & ~7, picy, picnum);
    }

    /*
=====================
=
= RipToEOL
=
=====================
*/

    private static void RipToEOL()
    {
        while (text[textIndex++] != '\n')         // scan to end of line
            ;
    }

    /*
    =====================
    =
    = ParseNumber
    =
    =====================
    */

    private static int ParseNumber()
    {
        char ch;
        char[] num = new char[80];
        int numptr;

        //
        // scan until a number is found
        //
        ch = text[textIndex];
        while (ch < '0' || ch > '9')
            ch = text[++textIndex];

        //
        // copy the number out
        //
        numptr = 0;
        do
        {
            num[numptr++] = ch;
            ch = text[++textIndex];
        } while (ch >= '0' && ch <= '9');
        //num[numptr] = 0;

        return Convert.ToInt32(new string(num));
    }
    /*
=====================
=
= PageLayout
=
= Clears the screen, draws the pics on the page, and word wraps the text.
= Returns a pointer to the terminating command
=
=====================
*/

    private static void PageLayout(bool shownumber)
    {
        int i, oldfontcolor;
        char ch;

        oldfontcolor = fontcolor;

        fontcolor = 0;

        //
        // clear the screen
        //
        VWB_Bar(0, 0, 320, 200, BACKCOLOR);
        VWB_DrawPic(0, 0, (int)graphicnums.H_TOPWINDOWPIC);
        VWB_DrawPic(0, 8, (int)graphicnums.H_LEFTWINDOWPIC);
        VWB_DrawPic(312, 8, (int)graphicnums.H_RIGHTWINDOWPIC);
        VWB_DrawPic(8, 176, (int)graphicnums.H_BOTTOMINFOPIC);


        for (i = 0; i < TEXTROWS; i++)
        {
            leftmargin[i] = LEFTMARGIN;
            rightmargin[i] = SCREENPIXWIDTH - RIGHTMARGIN;
        }

        px = LEFTMARGIN;
        py = TOPMARGIN;
        rowon = 0;
        layoutdone = false;

        //
        // make sure we are starting layout text (^P first command)
        //
        while (text[textIndex] <= 32)
            textIndex++;

        if (text[textIndex] != '^' || Char.ToUpper(text[++textIndex]) != 'P')
            Quit("PageLayout: Text not headed with ^P");

        while (text[textIndex++] != '\n')
            ;


        //
        // process text stream
        //
        do
        {
            ch = text[textIndex];

            if (ch == '^')
                HandleCommand();
            else
                if (ch == 9)
                {
                    px = (px + 8) & 0xf8;
                    textIndex++;
                }
                else if (ch <= 32)
                    HandleCtrls();
                else
                    HandleWord();

        } while (!layoutdone);

        pagenum++;

        if (shownumber)
        {
            var str = $"pg {pagenum} of {numpages}";
            px = 213;
            py = 183;
            fontcolor = 0x4f;                          //12^BACKCOLOR;

            VWB_DrawPropString(str);
        }

        fontcolor = (byte)oldfontcolor;
    }


    /*
    =====================
    =
    = HandleCommand
    =
    =====================
    */

    private static void HandleCommand()
    {
        int i, margin, top, bottom;
        int picwidth, picheight, picmid;

        switch (Char.ToUpper(text[++textIndex]))
        {
            case 'B':
                picy = ParseNumber();
                picx = ParseNumber();
                picwidth = ParseNumber();
                picheight = ParseNumber();
                VWB_Bar(picx, picy, picwidth, picheight, BACKCOLOR);
                RipToEOL();
                break;
            case ';':               // comment
                RipToEOL();
                break;
            case 'P':               // ^P is start of next page, ^E is end of file
            case 'E':
                layoutdone = true;
                textIndex--;             // back up to the '^'
                break;

            case 'C':               // ^c<hex digit> changes text color
                i = Char.ToUpper(text[++textIndex]);
                if (i >= '0' && i <= '9')
                    fontcolor = (byte)(i - '0');
                else if (i >= 'A' && i <= 'F')
                    fontcolor = (byte)(i - 'A' + 10);

                fontcolor *= 16;
                i = Char.ToUpper(text[++textIndex]);
                if (i >= '0' && i <= '9')
                    fontcolor += (byte)(i - '0');
                else if (i >= 'A' && i <= 'F')
                    fontcolor += (byte)(i - 'A' + 10);
                textIndex++;
                break;

            case '>':
                px = 160;
                textIndex++;
                break;

            case 'L':
                py = ParseNumber();
                rowon = (uint)((py - TOPMARGIN) / FONTHEIGHT);
                py = (int)(TOPMARGIN + rowon * FONTHEIGHT);
                px = ParseNumber();
                while (text[textIndex++] != '\n')         // scan to end of line
                    ;
                break;

            case 'T':               // ^Tyyy,xxx,ppp,ttt waits ttt tics, then draws pic
                TimedPicCommand();
                break;

            case 'G':               // ^Gyyy,xxx,ppp draws graphic
                ParsePicCommand();
                VWB_DrawPic(picx & ~7, picy, picnum);
                picwidth = pictable[picnum - STARTPICS].width;
                picheight = pictable[picnum - STARTPICS].height;
                //
                // adjust margins
                //
                picmid = picx + picwidth / 2;
                if (picmid > SCREENMID)
                    margin = picx - PICMARGIN;                        // new right margin
                else
                    margin = picx + picwidth + PICMARGIN;       // new left margin

                top = (picy - TOPMARGIN) / FONTHEIGHT;
                if (top < 0)
                    top = 0;
                bottom = (picy + picheight - TOPMARGIN) / FONTHEIGHT;
                if (bottom >= TEXTROWS)
                    bottom = TEXTROWS - 1;

                for (i = top; i <= bottom; i++)
                    if (picmid > SCREENMID)
                        rightmargin[i] = (uint)margin;
                    else
                        leftmargin[i] = (uint)margin;

                //
                // adjust this line if needed
                //
                if (px < (int)leftmargin[rowon])
                    px = (int)leftmargin[rowon];
                break;
        }
    }
    /*
    =====================
    =
    = HandleCtrls
    =
    =====================
    */

    private static void HandleCtrls()
    {
        char ch;

        ch = text[textIndex++];                   // get the character and advance

        if (ch == '\n')
        {
            NewLine();
            return;
        }
    }


    /*
    =====================
    =
    = HandleWord
    =
    =====================
    */

    private static void HandleWord()
    {
        char[] wword = new char[WORDLIMIT];
        int wordindex;
        ushort wwidth, wheight, newpos;


        //
        // copy the next word into [word]
        //
        wword[0] = text[textIndex++];
        wordindex = 1;
        while (text[textIndex] > 32)
        {
            wword[wordindex] = text[textIndex++];
            if (++wordindex == WORDLIMIT)
                Quit("PageLayout: Word limit exceeded");
        }
        wword[wordindex] = (char)0;            // stick a null at end for C

        //
        // see if it fits on this line
        //
        VW_MeasurePropString(new string(wword), out wwidth, out wheight);

        while (px + wwidth > (int)rightmargin[rowon])
        {
            NewLine();
            if (layoutdone)
                return;         // overflowed page
        }

        //
        // print it
        //
        newpos = (ushort)(px + wwidth);
        VWB_DrawPropString(new string(wword));
        px = newpos;

        //
        // suck up any extra spaces
        //
        while (text[textIndex] == ' ')
        {
            px += SPACEWIDTH;
            textIndex++;
        }
    }

    /*
    =====================
    =
    = NewLine
    =
    =====================
    */

    private static void NewLine()
    {
        char ch;

        if (++rowon == TEXTROWS)
        {
            //
            // overflowed the page, so skip until next page break
            //
            layoutdone = true;
            do
            {
                if (text[textIndex] == '^')
                {
                    ch = char.ToUpper(text[textIndex + 1]);
                    if (ch == 'E' || ch == 'P')
                    {
                        layoutdone = true;
                        return;
                    }
                }
                textIndex++;
            } while (true);
        }
        px = (int)leftmargin[rowon];
        py += FONTHEIGHT;
    }
}
