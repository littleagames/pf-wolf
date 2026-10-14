using PFWolf.Assets;
using PFWolf.Fonts;

namespace PFWolf.Ui;

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

/// <summary>Wolf3D's articles (WL_TEXT): the help screens and a won episode's end text, page by page</summary>
internal sealed class ArticleReader(GameEngineManager engine, AssetManager assets, InputManager input, AudioManager audio, VideoManager video,
    GraphicManager graphics, MenuInput menuInput, GameSession session, TextPresenter presenter)
{
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

    int pagenum;
    int numpages;

    uint[] leftmargin = new uint[TEXTROWS];
    uint[] rightmargin = new uint[TEXTROWS];
    string text = "";
    int textIndex = 0;
    uint rowon;

    int picx;
    int picy;
    string? picName;
    int picdelay;
    bool layoutdone;

    internal void HelpScreens()
    {
        // A pack with presenter help (Blake Stone's) shows it in the briefing window
        if (engine.GetGameInfo().HelpText is { Length: > 0 } helpText)
        {
            presenter.ShowTextPages(helpText, fromMenu: true);
            return;
        }

        var textAsset = assets.Find<TextAsset>("HELPART");
        if (textAsset == null)
            return;
        var text = textAsset.ToText();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        ShowArticle(text);
        video.FadeOut();
    }

    internal void EndText()
    {
        audio.StopAll();        // ClearMemory

        var gameInfo = engine.GetGameInfo();
        if (!gameInfo.Clusters.TryGetValue(session.gamestate.cluster, out var clusterInfo)
            || string.IsNullOrEmpty(clusterInfo?.EndText))
        {
            return;
        }

        var textAsset = assets.Find<TextAsset>(clusterInfo.EndText);
        if (textAsset == null)
            return;
        var text = textAsset.ToText();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ShowArticle(text);

        video.FadeOut();
        input.ClearKeysDown();
        input.CenterMouse();
    }

    internal void ShowArticle(string article)
    {
        bool newpage, firstpage;
        ControlInfo ci;

        text = article;
        // From the start, not where the last article shown was left (the original pointed at the new text)
        textIndex = 0;
        // Where the layout has got to on the page, and the color ^C last set
        var page = TextWindow.FullScreen(graphics, new TextStyle(UiFonts.SMALL_FONT, "Black", "BACKCOLOR"));
        video.FillScreen("BACKCOLOR");
        CacheLayout();

        newpage = true;
        firstpage = true;
        do
        {
            if (newpage)
            {
                newpage = false;
                PageLayout(page, true);
                video.Update();
                if (firstpage)
                {
                    video.FadeIn(10);
                    firstpage = false;
                }
            }
            GameEngineManager.DelayMs(5);

            input.ClearLastKey();
            menuInput.ReadAnyControl(out ci);
            Direction dir = ci.dir;
            switch (dir)
            {
                case Direction.North:
                case Direction.South:
                    break;

                default:
                    if (ci.button0)
                        dir = Direction.South;
                    switch (input.GetLastKeyPressed())
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
                    menuInput.TicDelay(20);
                    break;

                case Direction.South:
                case Direction.East:
                    if (pagenum < numpages)
                    {
                        newpage = true;
                    }
                    menuInput.TicDelay(20);
                    break;
            }
        } while (input.GetLastKeyPressed() != ScanCodes.sc_Escape && !ci.button1);

        input.ClearKeysDown();
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

    private void BackPage()
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

    private void CacheLayout()
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

        engine.Quit("CacheLayout: No ^E to terminate file!");
    }

    private void ParsePicCommand()
    {
        picy = ParseNumber();
        picx = ParseNumber();
        var alias = assets.FindInGamePack<AliasAsset>("alias");
        alias.ArtExtern.TryGetValue(ParseNumber(), out picName);
        RipToEOL();
    }


    private void ParseTimedCommand()
    {
        picy = ParseNumber();
        picx = ParseNumber();
        var alias = assets.FindInGamePack<AliasAsset>("alias");
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

    private void TimedPicCommand()
    {
        ParseTimedCommand();

        //
        // update the screen, and wait for time delay
        //
        video.Update();

        //
        // wait for time
        //
        GameEngineManager.DelayTics(picdelay);

        //
        // draw pic
        //
        graphics.DrawPic(picName, picx & ~7, picy);
    }

    /*
=====================
=
= RipToEOL
=
=====================
*/

    private void RipToEOL()
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

    private int ParseNumber()
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

    private void PageLayout(TextWindow page, bool shownumber)
    {
        int i;
        char ch;

        page.Style = page.Style with { Color = "Black" };

        //
        // clear the screen
        //
        video.FillScreen("BACKCOLOR");
        graphics.DrawPic("h_topwindow", 0, 0);
        graphics.DrawPic("h_leftwindow", 0, 8);
        graphics.DrawPic("h_rightwindow", 312, 8);
        graphics.DrawPic("h_bottominfo", 8, 176);


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
            engine.Quit("PageLayout: Text not headed with ^P");

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
            graphics.DrawText(213, 183, str, page.Style with { Color = "Dark Yellow" });    //12^BACKCOLOR;
        }
    }


    /*
    =====================
    =
    = HandleCommand
    =
    =====================
    */

    private void HandleCommand(TextWindow page)
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
                video.Bar(picx, picy, picwidth, picheight, "BACKCOLOR");
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
                graphics.DrawPic(picName, picx & ~7, picy);
                var graphicAsset = assets.Find<GraphicAsset>(picName);
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

    private void HandleCtrls(TextWindow page)
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

    private void HandleWord(TextWindow page)
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
                engine.Quit("PageLayout: Word limit exceeded");
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

    private void NewLine(TextWindow page)
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
