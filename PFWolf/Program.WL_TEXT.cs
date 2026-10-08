using PFWolf.Assets;
using PFWolf.Fonts;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    /*
    =============================================================================

    TEXT FORMATTING COMMANDS
    ------------------------
    ^C<hex digit>           Change text color
    ^E[enter]               End of layout (all pages)
    ^G<y>,<x>,<pic>[enter]  Draw a graphic and push margins
    ^P[enter]               start new page, must be the first chars in a layout
    ^L<x>,<y>[ENTER]        Locate to a specific spot, x in pixels, y in lines

    =============================================================================
    */

    /*
    =============================================================================

                                                     LOCAL CONSTANTS

    =============================================================================
    */

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
    static string? picName;
    static int picdelay;
    static bool layoutdone;

    internal static void HelpScreens()
    {
        // A pack with presenter help (Blake Stone's) shows it in the briefing window
        if (_gameEngineManager.GetGameInfo().HelpText is { Length: > 0 } helpText)
        {
            ShowTextPages(helpText, fromMenu: true);
            return;
        }

        var textAsset = _assetManager.Find<TextAsset>("HELPART");
        if (textAsset == null)
            return;
        var text = textAsset.ToText();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        ShowArticle(text);
        _videoManager.FadeOut();

        FreeMusic();
    }

    internal static void EndText()
    {
        ClearMemory();

        var gameInfo = _gameEngineManager.GetGameInfo();
        if (!gameInfo.Clusters.TryGetValue(gamestate.cluster, out var clusterInfo)
            || string.IsNullOrEmpty(clusterInfo?.EndText))
        {
            return;
        }

        var textAsset = _assetManager.Find<TextAsset>(clusterInfo.EndText);
        if (textAsset == null)
            return;
        var text = textAsset.ToText();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ShowArticle(text);

        _videoManager.FadeOut();
        _inputManager.ClearKeysDown();
        _inputManager.CenterMouse();

        FreeMusic();
    }

    internal static void ShowArticle(string article)
    {
        bool newpage, firstpage;
        ControlInfo ci;

        text = article;
        // From the start, not where the last article shown was left (the original pointed at the new text)
        textIndex = 0;
        // Where the layout has got to on the page, and the color ^C last set
        var page = TextWindow.FullScreen(_graphicManager, new TextStyle(SMALL_FONT, "Black", "BACKCOLOR"));
        _videoManager.FillScreen("BACKCOLOR");
        CacheLayout();

        newpage = true;
        firstpage = true;
        do
        {
            if (newpage)
            {
                newpage = false;
                PageLayout(page, true);
                _videoManager.Update();
                if (firstpage)
                {
                    _videoManager.FadeIn(10);
                    firstpage = false;
                }
            }
            GameEngineManager.DelayMs(5);

            _inputManager.ClearLastKey();
            ReadAnyControl(out ci);
            Direction dir = ci.dir;
            switch (dir)
            {
                case Direction.North:
                case Direction.South:
                    break;

                default:
                    if (ci.button0)
                        dir = Direction.South;
                    switch (_inputManager.GetLastKeyPressed())
                    {
                        case ScanCodes.sc_UpArrow:
                        case ScanCodes.sc_PgUp:
                        case ScanCodes.sc_LeftArrow:
                            dir = Direction.North;
                            break;

                        case ScanCodes.sc_Enter:
                        case ScanCodes.sc_DownArrow:
                        case ScanCodes.sc_PgDn:
                        case ScanCodes.sc_RightArrow:
                            dir = Direction.South;
                            break;
                    }
                    break;
            }

            switch (dir)
            {
                case Direction.North:
                case Direction.West:
                    if (pagenum > 1)
                    {
                        BackPage();
                        BackPage();
                        newpage = true;
                    }
                    TicDelay(20);
                    break;

                case Direction.South:
                case Direction.East:
                    if (pagenum < numpages)
                    {
                        newpage = true;
                    }
                    TicDelay(20);
                    break;
            }
        } while (_inputManager.GetLastKeyPressed() != ScanCodes.sc_Escape && !ci.button1);

        _inputManager.ClearKeysDown();
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

    //===========================================================================


    /*
    =====================
    =
    = CacheLayout
    =
    = Scans an entire layout file (until a ^E), counting pages
    =
    =====================
    */

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

        } while (textIndex < bombpoint && textIndex < text.Length - 1);

        _gameEngineManager.Quit("CacheLayout: No ^E to terminate file!");
    }

    private static void ParsePicCommand()
    {
        picy = ParseNumber();
        picx = ParseNumber();
        var alias = _assetManager.FindInGamePack<AliasAsset>("alias");
        alias.ArtExtern.TryGetValue(ParseNumber(), out picName);
        RipToEOL();
    }


    private static void ParseTimedCommand()
    {
        picy = ParseNumber();
        picx = ParseNumber();
        var alias = _assetManager.FindInGamePack<AliasAsset>("alias");
        alias.ArtExtern.TryGetValue(ParseNumber(), out picName);
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
        _videoManager.Update();

        //
        // wait for time
        //
        GameEngineManager.DelayTics(picdelay);

        //
        // draw pic
        //
        _graphicManager.DrawPic(picName, picx & ~7, picy);
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

    private static void PageLayout(TextWindow page, bool shownumber)
    {
        int i;
        char ch;

        page.Style = page.Style with { Color = "Black" };

        //
        // clear the screen
        //
        _videoManager.FillScreen("BACKCOLOR");
        _graphicManager.DrawPic("h_topwindow", 0, 0);
        _graphicManager.DrawPic("h_leftwindow", 0, 8);
        _graphicManager.DrawPic("h_rightwindow", 312, 8);
        _graphicManager.DrawPic("h_bottominfo", 8, 176);


        for (i = 0; i < TEXTROWS; i++)
        {
            leftmargin[i] = LEFTMARGIN;
            rightmargin[i] = SCREENPIXWIDTH - RIGHTMARGIN;
        }

        page.PrintX = LEFTMARGIN;
        page.PrintY = TOPMARGIN;
        rowon = 0;
        layoutdone = false;

        //
        // make sure we are starting layout text (^P first command)
        //
        while (text[textIndex] <= 32)
            textIndex++;

        if (text[textIndex] != '^' || Char.ToUpper(text[++textIndex]) != 'P')
            _gameEngineManager.Quit("PageLayout: Text not headed with ^P");

        while (text[textIndex++] != '\n')
            ;


        //
        // process text stream
        //
        do
        {
            ch = text[textIndex];

            if (ch == '^')
                HandleCommand(page);
            else
                if (ch == 9)
                {
                    page.PrintX = (page.PrintX + 8) & 0xf8;
                    textIndex++;
                }
                else if (ch <= 32)
                    HandleCtrls(page);
                else
                    HandleWord(page);

        } while (!layoutdone);

        pagenum++;

        if (shownumber)
        {
            var str = $"pg {pagenum} of {numpages}";
            _graphicManager.DrawText(213, 183, str, page.Style with { Color = "Dark Yellow" });    //12^BACKCOLOR;
        }
    }


    /*
    =====================
    =
    = HandleCommand
    =
    =====================
    */

    private static void HandleCommand(TextWindow page)
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
                _videoManager.Bar(picx, picy, picwidth, picheight, "BACKCOLOR");
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

            case 'C':               // ^c<hex digit><hex digit> changes text color
                int colorValue = 0;

                i = Char.ToUpper(text[++textIndex]);
                if (i >= '0' && i <= '9')
                    colorValue = i - '0';
                else if (i >= 'A' && i <= 'F')
                    colorValue = i - 'A' + 10;

                colorValue *= 16;
                i = Char.ToUpper(text[++textIndex]);
                if (i >= '0' && i <= '9')
                    colorValue += i - '0';
                else if (i >= 'A' && i <= 'F')
                    colorValue += i - 'A' + 10;

                page.Style = page.Style with { Color = colorValue.ToString() };
                textIndex++;
                break;

            case '>':
                page.PrintX = 160;
                textIndex++;
                break;

            case 'L':
                page.PrintY = ParseNumber();
                rowon = (uint)((page.PrintY - TOPMARGIN) / FONTHEIGHT);
                page.PrintY = (int)(TOPMARGIN + rowon * FONTHEIGHT);
                page.PrintX = ParseNumber();
                while (text[textIndex++] != '\n')         // scan to end of line
                    ;
                break;

            case 'T':               // ^Tyyy,xxx,ppp,ttt waits ttt tics, then draws pic
                TimedPicCommand();
                break;

            case 'G':               // ^Gyyy,xxx,ppp draws graphic
                ParsePicCommand();
                _graphicManager.DrawPic(picName, picx & ~7, picy);
                var graphicAsset = _assetManager.Find<GraphicAsset>(picName);
                if (graphicAsset == null)
                    return;

                picwidth = graphicAsset.Width;
                picheight = graphicAsset.Height;
                if (picwidth == 0 || picheight == 0) return;

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
                if (page.PrintX < (int)leftmargin[rowon])
                    page.PrintX = (int)leftmargin[rowon];
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

    private static void HandleCtrls(TextWindow page)
    {
        char ch;

        ch = text[textIndex++];                   // get the character and advance

        if (ch == '\n')
        {
            NewLine(page);
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

    private static void HandleWord(TextWindow page)
    {
        char[] wword = new char[WORDLIMIT];
        int wordindex;


        //
        // copy the next word into [word]
        //
        wword[0] = text[textIndex++];
        wordindex = 1;
        while (text[textIndex] > 32)
        {
            wword[wordindex] = text[textIndex++];
            if (++wordindex == WORDLIMIT)
                _gameEngineManager.Quit("PageLayout: Word limit exceeded");
        }
        string word = new string(wword, 0, wordindex);

        //
        // see if it fits on this line
        //
        page.Measure(word, out int wwidth, out _);

        while (page.PrintX + wwidth > (int)rightmargin[rowon])
        {
            NewLine(page);
            if (layoutdone)
                return;         // overflowed page
        }

        //
        // print it
        //
        page.Print(word);

        //
        // suck up any extra spaces
        //
        while (text[textIndex] == ' ')
        {
            page.PrintX += SPACEWIDTH;
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

    private static void NewLine(TextWindow page)
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
        page.PrintX = (int)leftmargin[rowon];
        page.PrintY += FONTHEIGHT;
    }
}
