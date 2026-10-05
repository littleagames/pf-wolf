using CommandLine;
using SDL2;
using Wolf3D.Assets;
using Wolf3D.Assets.Sounds;
using Wolf3D.Configuration;
using Wolf3D.Constants;
using Wolf3D.Entities;
using Wolf3D.Extensions;
using Wolf3D.Fonts;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    internal const int SM_X = 48;
    internal const int SM_W = 250;

    internal const int SM_Y1 = 20;
    internal const int SM_H1 = 4 * 13 - 7;
    internal const int SM_Y2 = SM_Y1 + 5 * 13;
    internal const int SM_H2 = 4 * 13 - 7;
    internal const int SM_Y3 = SM_Y2 + 5 * 13;
    internal const int SM_H3 = 3 * 13 - 7;

    internal const int CTL_X = 24;
    internal const int CTL_Y = 86;
    internal const int CTL_W = 284;
    internal const int CTL_H = 60;

    // Control panel song: the main menu's music in menudefs/main-menu
    internal static string MENUSONG => _gameEngineManager.GetGameInfo().MenuMusic ?? _assetManager.GetMenu("main-menu")?.Music ?? "WONDERIN";
    internal static string INTROSONG => _gameEngineManager.GetGameInfo().IntroMusic ?? "";
    internal static string HIGHSCORESSONG => _gameEngineManager.GetGameInfo().HighScoresMusic ?? "";

    internal static int SENSITIVE = 60;

    internal static CP_itemtype[] MainMenu = [];
    internal static CP_itemtype[] OptMenu = [];
    internal static CP_itemtype[] VidMenu = [];
    internal static CP_itemtype[] SndMenu = [];
    internal static CP_itemtype[] CtlMenu = [];
    internal static CP_itemtype[] JoyMenu = [];
    internal static CP_itemtype[] NewEmenu = [];
    internal static CP_itemtype[] NewMenu = [];
    internal static CP_itemtype[] ClassMenu = [];

    internal static CP_itemtype[] MusicMenu = [];
    // The jukebox shows one page of songs at a time
    internal const int JukeboxPageSize = 6;

    // Loaded from menudefs/ in CheckForEpisodes
    internal static CP_iteminfo MainItems;
    internal static CP_iteminfo OptItems;
    internal static CP_iteminfo VidItems;
    internal static CP_iteminfo SndItems;
    internal static CP_iteminfo CtlItems;
    internal static CP_iteminfo JoyItems;
    internal static CP_iteminfo NewEitems;
    internal static CP_iteminfo NewItems;
    internal static CP_iteminfo ClassItems;
    internal static CP_iteminfo MusicItems;
    static string[] color_hlite =
    {
        "DEACTIVE",
        "HIGHLIGHT",
        "READHCOLOR",
        "Green" // 4 165 0 (used for disabled episodes)
    };

    static string[] color_norml =
    {
        "DEACTIVE",
        "TEXTCOLOR",
        "READCOLOR",
        "DarkGreen" // 4 113 0 (used for disabled episodes)
    };

    internal static int StartGame;

    /// <summary>The game was ended with End Game in the menu, so the game over screen is skipped</summary>
    internal static bool endedFromMenu;
    internal static int SoundStatus = 1;

    private static void EnableEndGameMenuItem()
    {
        var language = _assetManager.GetText("en-us");
        var item = FindMenuItem(MainMenu, "viewscores");
        if (item == null) return;
        item.routine = null;
        item.text = "$MENU_ENDGAME".ToLanguageText(language);
    }

    internal static void EnableViewScoresMenuItem()
    {
        var language = _assetManager.GetText("en-us");
        var item = FindMenuItem(MainMenu, "viewscores");
        if (item == null) return;
        item.routine = CP_ViewScores;
        item.text = "$MENU_VIEWSCORES".ToLanguageText(language);
    }

    internal static void ClearMScreen()
    {
        _graphicManager.DrawMenuBackground("BORDCOLOR");
    }

    internal static void DrawStripes(int y)
    {
        _graphicManager.DrawStripe(y);
    }

    internal static void DrawWindow(int x, int y, int w, int h, string wcolor)
    {
        _videoManager.Bar(x, y, w, h, wcolor);
        DrawOutline(x, y, w, h, "BORD2COLOR", "DEACTIVE");
    }

    internal static void DrawOutline(int x, int y, int w, int h, string color1, string color2)
    {
        _videoManager.HorizontalLine(x, x + w, y, color2);
        _videoManager.VerticalLine(y, y + h, x, color2);
        _videoManager.HorizontalLine(x, x + w, y + h, color1);
        _videoManager.VerticalLine(y, y + h, x + w, color1);
    }

    internal static void MenuFadeOut()
    {
        var fadeColor = _gameEngineManager.GetGameInfo().MenuFadeColor;
        _videoManager.FadeOut(menuFadeStyle, string.IsNullOrEmpty(fadeColor) ? new Color { Alpha = 255 } : Color.FromHexRGBA(fadeColor), 10, menuFadeTics);
    }
    internal static void MenuFadeIn() => _videoManager.FadeIn(menuFadeStyle, 10, menuFadeTics);

    internal static void DrawMenu(CP_iteminfo item_i, CP_itemtype[] items)
    {
        int which = item_i.curpos;

        for (int i = 0; i < item_i.amount; i++)
            PrintMenuItem(item_i, items, i, items[i].active > 0 ? MenuItemColor(items[i], which == i) : "DEACTIVE");
    }

    /// <summary>An item's text in its row; a newline in it goes on under the start of the text</summary>
    internal static void PrintMenuItem(CP_iteminfo item_i, CP_itemtype[] items, int which, string color)
        => TextAt(item_i.x + item_i.indent, MenuItemY(item_i, which), MenuItemStyle(item_i, color)).Print(items[which].text);

    /// <summary>The top of an item's row</summary>
    internal static int MenuItemY(CP_iteminfo item_i, int which) => item_i.y + which * item_i.rowHeight;

    /// <summary>The menu's text style for its items: its font and shadow (menudef font, item-shadow)</summary>
    internal static TextStyle MenuItemStyle(CP_iteminfo item_i, string color)
        => new(item_i.font ?? MENU_FONT, color,
            Shadow: item_i.itemShadow is { } shadow ? new FontShadow(1, 1, shadow) : null);

    /// <summary>
    /// Draws or erases a menu's highlight bar (menudef cursor) behind the item, printing the item
    /// over it highlighted, or plain when the bar is off
    /// </summary>
    internal static void DrawMenuBar(CP_iteminfo item_i, CP_itemtype[] items, int which, bool on)
    {
        var bar = item_i.cursor!;
        _videoManager.Bar(bar.X, MenuItemY(item_i, which) + bar.YOffset, bar.Width, bar.Height, on ? bar.Color : bar.EraseColor);
        PrintMenuItem(item_i, items, which, MenuItemColor(items[which], on));
        if (items[which].checkbox is bool checkedOn)
            DrawMenuCheckbox(item_i, which, checkedOn, highlighted: on);
    }

    internal static int StartCPMusic(string song)
    {
        //int lastoffs;

        //lastoffs = _audioManager.SetPaused(true);

        _audioManager.PlayMusic(song);
        //return lastoffs;
        return 0;
    }


    internal static int redrawitem = 1, lastitem = -1;

    /// <param name="adjust">
    /// Called with the highlighted item and -1 or +1 when left or right is pressed, for menus
    /// with rows that change in place (sliders, choices). Without it, left/right do nothing.
    /// </param>
    /// <param name="idle">
    /// Called again and again while the menu waits (a network game's screens take in what has
    /// arrived and redraw); returning true leaves the menu at once, with -2.
    /// </param>
    internal static int HandleMenu(CP_iteminfo item_i, CP_itemtype[] items, Action<int>? routine, Action<int, int>? adjust = null, Func<bool>? idle = null)
    {
        char key;
        int i, x, y, basey, exit, which;
        string shape;
        int lastBlinkTime, timer;
        ControlInfo ci;

        which = item_i.curpos;
        x = item_i.x & -8;
        basey = item_i.y - 2;
        y = basey + which * item_i.rowHeight;
        // A highlight bar (Blake Stone's) flashes on and off, where the gun blinks its shape
        bool bar = item_i.cursor != null, barOn = true;

        if (bar)
            DrawMenuBar(item_i, items, which, true);
        else
        {
            _graphicManager.DrawPic("c_cursor1", x, y);
            if (redrawitem != 0)
                PrintMenuItem(item_i, items, which, MenuItemColor(items[which], true));
        }
        //
        // CALL CUSTOM ROUTINE IF IT IS NEEDED
        //
        routine?.Invoke(which);

        _videoManager.Update();

        shape = "c_cursor1";
        timer = bar ? item_i.cursor!.FlashTics : 8;
        exit = 0;
        lastBlinkTime = (int)GameEngineManager.GetTimeCount();
        _inputManager.ClearKeysDown();
        _inputManager.ClearTextInput();

        do
        {
            //
            // CHANGE GUN SHAPE
            //
            if ((int)GameEngineManager.GetTimeCount() - lastBlinkTime > timer)
            {
                lastBlinkTime = (int)GameEngineManager.GetTimeCount();
                if (bar)
                {
                    barOn = !barOn;
                    DrawMenuBar(item_i, items, which, barOn);
                }
                else
                {
                    if (shape == "c_cursor1")
                    {
                        shape = "c_cursor2";
                        timer = 8;
                    }
                    else
                    {
                        shape = "c_cursor1";
                        timer = 70;
                    }

                    _graphicManager.DrawPic(shape, x, y);
                }
                routine?.Invoke(which);
                _videoManager.Update();
            }
            else
                GameEngineManager.DelayMs(5);

            CheckPause();

            if (idle?.Invoke() == true)
            {
                exit = 3;
                break;
            }

            //
            // SEE IF ANY KEYS ARE PRESSED FOR INITIAL CHAR FINDING
            //

            key = _inputManager.GetTextInput()[0];

            _inputManager.ClearTextInput();

            if (key != 0)
            {
                int ok = 0;

                if (key >= 'a')
                    key -= (char)('a' - 'A');

                for (i = which + 1; i < item_i.amount; i++)
                {
                    if (items[i].active != 0 && ItemKey(items[i]) == key)
                    {
                        EraseGun(item_i, items, x, y, which);
                        which = i;
                        DrawGun(item_i, items, x, ref y, which, basey, routine);
                        ok = 1;
                        _inputManager.ClearKeysDown();
                        break;
                    }
                }

                //
                // DIDN'T FIND A MATCH FIRST TIME THRU. CHECK AGAIN.
                //
                if (ok == 0)
                {
                    for (i = 0; i < which; i++)
                        if (items[i].active != 0 && ItemKey(items[i]) == key)
                        {
                            EraseGun(item_i, items, x, y, which);
                            which = i;
                            DrawGun(item_i, items, x, ref y, which, basey, routine);
                            _inputManager.ClearKeysDown();
                            break;
                        }
                }
            }

            //
            // GET INPUT
            //
            ReadAnyControl(out ci);
            switch (ci.dir)
            {
                ////////////////////////////////////////////////
                //
                // MOVE UP
                //
                case Direction.North:

                    EraseGun(item_i, items, x, y, which);

                    //
                    // ANIMATE HALF-STEP
                    //
                    if (!bar && which != 0 && items[which - 1].active != 0)
                    {
                        y -= 6;
                        DrawHalfStep(x, y);
                    }

                    //
                    // MOVE TO NEXT AVAILABLE SPOT
                    //
                    do
                    {
                        if (which == 0)
                            which = item_i.amount - 1;
                        else
                            which--;
                    }
                    while (items[which].active == 0);

                    DrawGun(item_i, items, x, ref y, which, basey, routine);
                    //
                    // WAIT FOR BUTTON-UP OR DELAY NEXT MOVE
                    //
                    TicDelay(20);
                    break;

                ////////////////////////////////////////////////
                //
                // MOVE DOWN
                //
                case Direction.South:

                    EraseGun(item_i, items, x, y, which);
                    //
                    // ANIMATE HALF-STEP
                    //
                    if (!bar && which != item_i.amount - 1 && items[which + 1].active != 0)
                    {
                        y += 6;
                        DrawHalfStep(x, y);
                    }

                    do
                    {
                        if (which == item_i.amount - 1)
                            which = 0;
                        else
                            which++;
                    }
                    while (items[which].active == 0);

                    DrawGun(item_i, items, x, ref y, which, basey, routine);

                    //
                    // WAIT FOR BUTTON-UP OR DELAY NEXT MOVE
                    //
                    TicDelay(20);
                    break;

                ////////////////////////////////////////////////
                //
                // ADJUST THE HIGHLIGHTED ITEM
                //
                case Direction.West when adjust != null:
                    adjust(which, -1);
                    TicDelay(20);
                    break;

                case Direction.East when adjust != null:
                    adjust(which, 1);
                    TicDelay(20);
                    break;
            }

            if (ci.button0 || _inputManager.IsKeyDown(ScanCodes.sc_Space) || _inputManager.IsKeyDown(ScanCodes.sc_Enter))
                exit = 1;

            if (ci.button1 && !_inputManager.IsKeyDown(ScanCodes.sc_Alt) || _inputManager.IsKeyDown(ScanCodes.sc_Escape))
                exit = 2;

        }
        while (exit == 0);
        _inputManager.ClearKeysDown();
        WaitKeyUp();        // or a held button picks in, or backs out of, the next menu too

        //
        // ERASE EVERYTHING
        //
        if (bar)
            DrawMenuBar(item_i, items, which, false);
        else if (lastitem != which)
        {
            _videoManager.Bar(x - 1, y, 25, 16, "BKGDCOLOR");
            PrintMenuItem(item_i, items, which, MenuItemColor(items[which], true));
            redrawitem = 1;
        }
        else
            redrawitem = 0;

        routine?.Invoke(which);
        _videoManager.Update();

        item_i.curpos = (short)which;

        lastitem = which;
        switch (exit)
        {
            case 1:
                //
                // CALL THE ROUTINE
                //
                if (items[which].routine != null)
                {
                    ShootSnd();
                    MenuFadeOut();
                    items[which].routine!.Invoke(0);
                }
                return which;

            case 2:
                _audioManager.Play("menu/escape");
                return -1;

            case 3:
                return -2;      // idle asked to leave
        }

        return 0; // JUST TO SHUT UP THE ERROR MESSAGES!
    }

    private static char ItemKey(CP_itemtype item)
    {
        if (item.shortKey != '\0')
            return char.ToUpperInvariant(item.shortKey);

        return string.IsNullOrWhiteSpace(item.text) ? '\0' : item.text[0];
    }

    internal static void EraseGun(CP_iteminfo item_i, CP_itemtype[] items, int x, int y, int which)
    {
        if (item_i.cursor != null)
            DrawMenuBar(item_i, items, which, false);
        else
        {
            _videoManager.Bar(x - 1, y, 25, 16, "BKGDCOLOR");
            PrintMenuItem(item_i, items, which, MenuItemColor(items[which], false));
        }
        _videoManager.Update();
    }

    //
    // DRAW HALF STEP OF GUN TO NEXT POSITION
    //
    internal static void DrawHalfStep(int x, int y)
    {
        _graphicManager.DrawPic("c_cursor1", x, y);
        _videoManager.Update();
        _audioManager.Play("menu/move1");
        GameEngineManager.DelayMs(8 * 100 / 7);
    }

    internal static void DrawGun(CP_iteminfo item_i, CP_itemtype[] items, int x, ref int y, int which, int basey, Action<int>? routine)
    {
        if (item_i.cursor != null)
        {
            y = basey + which * item_i.rowHeight;
            DrawMenuBar(item_i, items, which, true);
        }
        else
        {
            _videoManager.Bar(x - 1, y, 25, 16, "BKGDCOLOR");
            y = basey + which * item_i.rowHeight;
            _graphicManager.DrawPic("c_cursor1", x, y);
            PrintMenuItem(item_i, items, which, MenuItemColor(items[which], true));
        }

        //
        // CALL CUSTOM ROUTINE IF IT IS NEEDED
        //
        routine?.Invoke(which);
        _videoManager.Update();
        _audioManager.Play("menu/move2");
    }

    internal static void CheckPause()
    {
        if (_gameEngineManager.IsPaused())
        {
            switch (SoundStatus)
            {
                case 0:
                    _audioManager.SetPaused(false);
                    break;
                case 1:
                    _audioManager.SetPaused(true);
                    break;
            }

            SoundStatus ^= 1;
            GameEngineManager.WaitVBL(3);
            _inputManager.ClearKeysDown();
            _gameEngineManager.SetPaused(false);
        }
    }

    internal static string MenuItemColor(CP_itemtype item, bool hlight)
        => hlight ? color_hlite[item.active] : color_norml[item.active];

    internal static void ShootSnd()
    {
        _audioManager.Play("menu/activate"); // TODO: "shoot" is something else now
    }

    internal static void TicDelay(int count)
    {
        ControlInfo ci;

        int startTime = (int)GameEngineManager.GetTimeCount();

        do
        {
            GameEngineManager.DelayMs(5);
            ReadAnyControl(out ci);
        }
        while ((int)GameEngineManager.GetTimeCount() - startTime < count && ci.dir != Direction.None);
    }

    static int totalMousex = 0, totalMousey = 0;
    internal static void ReadAnyControl(out ControlInfo ci)
    {
        int mouseactive = 0;
        _inputManager.ReadControl(out ci);
        if (mouseenabled && _inputManager.IsMouseInputGrabbed())
        {
            int mousex, mousey, buttons;

            buttons = (int)SDL.SDL_GetRelativeMouseState(out mousex, out mousey);


            int middlePressed = (int)(buttons & SDL.SDL_BUTTON(SDL.SDL_BUTTON_MIDDLE));
            int rightPressed = (int)(buttons & SDL.SDL_BUTTON(SDL.SDL_BUTTON_RIGHT));
            buttons &= (int)~(SDL.SDL_BUTTON(SDL.SDL_BUTTON_MIDDLE) | SDL.SDL_BUTTON(SDL.SDL_BUTTON_RIGHT));
            if (middlePressed != 0) buttons |= 1 << 2;
            if (rightPressed != 0) buttons |= 1 << 1;

            totalMousex += mousex;
            totalMousey += mousey;

            if (totalMousey < -SENSITIVE)
            {
                ci.dir = Direction.North;
                mouseactive = 1;
            }
            else if (totalMousey > SENSITIVE)
            {
                ci.dir = Direction.South;
                mouseactive = 1;
            }

            if (totalMousex < -SENSITIVE)
            {
                ci.dir = Direction.West;
                mouseactive = 1;
            }
            else if (totalMousex > SENSITIVE)
            {
                ci.dir = Direction.East;
                mouseactive = 1;
            }

            if (mouseactive != 0)
            {
                totalMousex = 0;
                totalMousey = 0;
            }

            if (buttons != 0)
            {
                ci.button0 = (buttons & 1) != 0;
                ci.button1 = (buttons & 2) != 0;
                ci.button2 = (buttons & 4) != 0;
                ci.button3 = false;
                mouseactive = 1;
            }
        }

        if (joystickenabled && mouseactive == 0)
        {
            int jx, jy, jb;

            _inputManager.GetJoyDelta(out jx, out jy);
            if (jy < -SENSITIVE)
                ci.dir = Direction.North;
            else if (jy > SENSITIVE)
                ci.dir = Direction.South;

            if (jx < -SENSITIVE)
                ci.dir = Direction.West;
            else if (jx > SENSITIVE)
                ci.dir = Direction.East;

            jb = _inputManager.JoyButtons();
            if (jb != 0)
            {
                ci.button0 = (jb & 1) != 0;
                ci.button1 = (jb & 2) != 0;
                ci.button2 = (jb & 4) != 0;
                ci.button3 = (jb & 8) != 0;
            }
        }
    }

    internal static void SetupControlPanel()
    {
        //
        // CACHE SOUNDS
        //
        if (_videoManager.HasMargins)
            _videoManager.ClearScreen(0);

        if (!ingame)
        { }// CA_LoadAllSounds();
        else
            FindMenuItem(MainMenu, "savegame")?.active = 1;

        _inputManager.CenterMouse();
    }

    internal static void US_ControlPanel(ScanCodes scancode)
    {
        int which;

        if (ingame)
        {
            if (CP_CheckQuick(scancode) != 0)
                return;
            lastgamemusicoffset = StartCPMusic(MENUSONG);
        }
        else
            StartCPMusic(MENUSONG);
        SetupControlPanel();

        //
        // F-KEYS FROM WITHIN GAME
        //
        switch (scancode)
        {
            case ScanCodes.sc_F1:
                // Only where the main menu has "Read This!": its help text's pictures are the
                // Apogee releases' (id's GT v1.4 and Spear made F1 a boss key instead)
                if (FindMenuItem(MainMenu, "readthis") != null)
                    HelpScreens();
                goto finishup;
            case ScanCodes.sc_F2:
                CP_SaveGame(0);
                goto finishup;

            case ScanCodes.sc_F3:
                CP_LoadGame(0);
                goto finishup;

            case ScanCodes.sc_F4:
                CP_Sound(0);
                goto finishup;

            case ScanCodes.sc_F5:
                CP_ChangeView(0);
                goto finishup;

            case ScanCodes.sc_F6:
                CP_Control(0);
                goto finishup;

            finishup:
                CleanupControlPanel();
                return;
        }

        DrawMainMenu();
        MenuFadeIn();
        StartGame = 0;

        //
        // MAIN MENU LOOP
        //
        do
        {
            which = HandleMenu(MainItems, MainMenu, null);
            switch (which < 0 ? "quit" : SelectedId(MainMenu, which))
            {
                case "viewscores":
                    if (MainMenu[which].routine == null)
                    {
                        if (CP_EndGame(0) != 0)
                            StartGame = 1;
                    }
                    else
                    {
                        DrawMainMenu();
                        MenuFadeIn();
                    }
                    break;

                case "backtodemo":
                    StartGame = 1;
                    if (!ingame)
                        StartCPMusic(INTROSONG);
                    _videoManager.FadeOut(menuFadeStyle, new Color { Alpha = 255 }, 10, menuFadeTics);
                    break;

                case "quit":
                    CP_Quit(0);
                    break;

                default:
                    if (StartGame == 0)
                    {
                        DrawMainMenu();
                        MenuFadeIn();
                    }
                    break;
            }
            //
            // "EXIT OPTIONS" OR "NEW GAME" EXITS
            //
        }
        while (StartGame == 0);

        //
        // DEALLOCATE EVERYTHING
        //
        CleanupControlPanel();

        //
        // CHANGE MAINMENU ITEM
        //
        if (startgame || loadedgame)
            EnableEndGameMenuItem();
    }

    internal static int CP_CheckQuick(ScanCodes scancode)
    {
        var gameInfo = _gameEngineManager.GetGameInfo();
        var language = _assetManager.GetText("en-us");
        switch (scancode)
        {
            //
            // END GAME
            //
            case ScanCodes.sc_F7:
                if (Confirm("$ENDGAMESTR".ToLanguageText(language), MAXY) != 0)
                {
                    playstate = playstatetypes.ex_died;
                    LastAttacker = null;
                    playerstate.lives = 0;
                }

                FindMenuItem(MainMenu, "savegame")?.active = 0;
                return 1;
            //
            // QUICKSAVE
            //
            // Always the quicksave file, never a save picked in the menus
            case ScanCodes.sc_F8:
                Message("$STR_SAVING".ToLanguageText(language) + "...", MAXY);
                CP_SaveGame(1);
                return 1;

            //
            // QUICKLOAD
            //
            // The quicksave, or the load screen until there is one
            case ScanCodes.sc_F9:
                if (File.Exists(QuickSavePath))
                {
                    if (Confirm("$STR_LS_QUICKLOAD".ToLanguageText(language), MAXY) != 0)
                        CP_LoadGame(1);
                }
                else
                {
                    _videoManager.FadeOut();
                    if (_videoManager.HasMargins)
                        _videoManager.ClearScreen(0);

                    lastgamemusicoffset = StartCPMusic(MENUSONG);
                    CP_LoadGame(0);    // loads lastgamemusicoffs

                    _inputManager.ClearKeysDown();
                    _videoManager.FadeOut();
                    if (viewsize != 21)
                        DrawPlayScreen();

                    if (!startgame && !loadedgame)
                        ContinueMusic(lastgamemusicoffset);

                    if (loadedgame)
                        playstate = playstatetypes.ex_abort;

                    lasttimecount = (int)GameEngineManager.GetTimeCount();

                    _inputManager.CenterMouse();
                }
                return 1;

            //
            // QUIT
            //
            case ScanCodes.sc_F10:
                string endStr = gameInfo.EndStrings[(US_RndT() & (gameInfo.EndStrings.Count - 2)) + (US_RndT() & 1)];
                if (Confirm(endStr, MAXY) != 0)
                {
                    _videoManager.Update();
                    _audioManager.SetPaused(true);
                    _audioManager.StopAll();
                    MenuFadeOut();

                    _gameEngineManager.Quit("");
                }

                DrawPlayBorder();
                return 1;
        }

        return 0;
    }

    internal static void DrawMainMenu()
    {
        var language = _assetManager.GetText("en-us");

        DrawMenuComponents("main-menu");

        //
        // CHANGE "GAME" AND "DEMO"
        //
        var backToDemo = FindMenuItem(MainMenu, "backtodemo");
        if (backToDemo != null)
        {
            if (ingame)
            {
                backToDemo.text = "$MENU_BACKTOGAME".ToLanguageText(language);
                backToDemo.active = 2;
            }
            else
            {
                backToDemo.text = "$MENU_BACKTODEMO".ToLanguageText(language);
                backToDemo.active = 1;
            }
        }

        DrawMenu(MainItems, MainMenu);
        _videoManager.Update();
    }

    internal static int CP_NewGame(int _)
    {
        var language = _assetManager.GetText("en-us");
        int which;
        MapInfo? mapInfo = null;
        EpisodeInfo? episodeInfo = null;
        // Episode, then class (only when there's a choice), then skill; Esc steps back one
        bool chooseClass = ClassMenu.Length > 1;
        string? playerClass = null;

        // A game with one episode (Spear) has no episode menu: straight to the class or difficulty menu
        var episodes = _gameEngineManager.GetGameInfo().Episodes;
        bool singleEpisode = episodes.Count == 1;
        if (singleEpisode)
        {
            episodeInfo = episodes.Values.First();
            if (!_gameEngineManager.GetGameInfo().Maps.TryGetValue(episodeInfo.StartMap, out mapInfo))
            {
                _audioManager.Play("player/usefail");
                Message($"Starting Map \"{episodeInfo.StartMap}\" unavailable!");
                _inputManager.ClearKeysDown();
                _inputManager.Ack();
                MenuFadeOut();
                return 0;
            }
            goto confirm;
        }

    firstpart:
        DrawNewEpisode();
        do
        {
            which = HandleMenu(NewEitems, NewEmenu, NewEitems.selectionPic != null ? DrawNewEpisodePic : null);
            switch (which)
            {
                case -1:
                    MenuFadeOut();
                    return 0;

                default:
                    episodeInfo = (EpisodeInfo?)NewEmenu[which].data;

                    string? refusal = null;
                    if (episodeInfo == null)
                        refusal = "Episode unavailable!";
                    else if (episodeInfo.Locked)
                        refusal = episodeInfo.LockedMessage!.ToLanguageText(language);   // the shareware's "Read This!" note
                    else if (!_gameEngineManager.GetGameInfo().Maps.TryGetValue(episodeInfo.StartMap, out mapInfo))
                        refusal = $"Starting Map \"{episodeInfo.StartMap}\" unavailable!";

                    if (refusal != null)
                    {
                        _audioManager.Play("player/usefail");
                        Message(refusal);
                        _inputManager.ClearKeysDown();
                        _inputManager.Ack();
                        DrawNewEpisode();
                        which = 0;
                    }
                    else
                        which = 1;
                    break;
            }

        }
        while (which == 0);

        ShootSnd();

    confirm:
        //
        // ALREADY IN A GAME?
        //
        if (ingame)
            if (Confirm($"$CURGAME".ToLanguageText(language)) == 0)
            {
                MenuFadeOut();
                return 0;
            }

    classpart:
        if (chooseClass)
        {
            MenuFadeOut();
            DrawNewClass();
            which = HandleMenu(ClassItems, ClassMenu, DrawNewClassPic);
            if (which < 0)
            {
                MenuFadeOut();
                if (singleEpisode)
                    return 0;
                goto firstpart;
            }

            playerClass = ((PlayerClassChoice)ClassMenu[which].data!).Class;
            ShootSnd();
        }

    skillpart:
        MenuFadeOut();
        DrawNewGame();
        which = HandleMenu(NewItems, NewMenu, DrawNewGameDiff);
        if (which < 0)
        {
            if (chooseClass)
                goto classpart;
            MenuFadeOut();
            if (singleEpisode)
                return 0;
            goto firstpart;
        }

        ShootSnd();

        // The episode's briefing (Blake Stone's missions); Esc there goes back to the skills
        if (!string.IsNullOrEmpty(episodeInfo!.Briefing))
        {
            MenuFadeOut();
            if (ShowBriefing(episodeInfo.Briefing))
                goto skillpart;
        }

        NewGame((short)which, episodeInfo, mapInfo, playerClass);     // one menu item per skill, in order
        StartGame = 1;
        MenuFadeOut();

        //
        // CHANGE "READ THIS!" TO NORMAL COLOR
        //
        FindMenuItem(MainMenu, "readthis")?.active = 1;

        return 0;
    }

    internal static void DrawNewEpisode()
    {
        DrawMenuComponents("new-episode");
        DrawMenu(NewEitems, NewEmenu);

        // Each episode's picture (pic-name in game-info) sits between the cursor and the name,
        // or the highlighted one's at the menu's selection-pic
        if (NewEitems.selectionPic != null)
            DrawNewEpisodePic(NewEitems.curpos);
        else
        {
            for (int i = 0; i < NewEmenu.Length; i++)
            {
                if (NewEmenu[i].data is EpisodeInfo episode && !string.IsNullOrEmpty(episode.PicName))
                    _graphicManager.DrawPic(episode.PicName, NewEitems.x + 32, MenuItemY(NewEitems, i));
            }
        }

        _videoManager.Update();
        MenuFadeIn();
        WaitKeyUp();
    }

    /// <summary>The highlighted episode's picture at the menu's selection-pic, when it has one</summary>
    internal static void DrawNewEpisodePic(int w)
    {
        if (NewEitems.selectionPic is { } at && w >= 0 && w < NewEmenu.Length
            && NewEmenu[w].data is EpisodeInfo { PicName: { Length: > 0 } pic })
            _graphicManager.DrawPic(pic, at.X, at.Y);
    }

    internal static void DrawNewGame()
    {
        DrawMenuComponents("new-game");

        DrawMenu(NewItems, NewMenu);
        DrawNewGameDiff(NewItems.curpos);
        _videoManager.Update();
        MenuFadeIn();
        WaitKeyUp();
    }

    internal static void DrawNewGameDiff(int w)
    {
        // Face picture for the highlighted skill (pic-name in game-info), at the menu's
        // selection-pic or beside the skills
        if (w >= 0 && w < NewMenu.Length && NewMenu[w].data is SkillInfo skill)
        {
            if (NewItems.selectionPic is { } at)
                _graphicManager.DrawPic(skill.PicName, at.X, at.Y);
            else
                _graphicManager.DrawPic(skill.PicName, NewItems.x + 185, NewItems.y + 7);
        }
    }

    /// <summary>A class menu item: the actordefs class and its picture (game-info pic-name)</summary>
    internal sealed record PlayerClassChoice(string Class, string? PicName);

    internal static void DrawNewClass()
    {
        DrawMenuComponents("new-class");
        DrawMenu(ClassItems, ClassMenu);
        DrawNewClassPic(ClassItems.curpos);
        _videoManager.Update();
        MenuFadeIn();
        WaitKeyUp();
    }

    internal static void DrawNewClassPic(int w)
    {
        // The highlighted class's picture; pictures can differ in size, so the last one is cleared first
        int x = ClassItems.x + 175, y = ClassItems.y - 6;
        _videoManager.Bar(x, y, 96, 100, "BKGDCOLOR");
        if (w >= 0 && w < ClassMenu.Length && ClassMenu[w].data is PlayerClassChoice { PicName: { Length: > 0 } pic })
            _graphicManager.DrawPic(pic, x, y);
    }

    /// <summary>
    /// The Options submenu. Most items run their own screen (Sound, Control, Change View,
    /// Video), and this menu is drawn again when that screen is left; Messages and Automap Stats
    /// switch on and off in place.
    /// </summary>
    internal static int CP_Options(int _)
    {
        int which;

        DrawOptionsMenu();
        MenuFadeIn();
        WaitKeyUp();

        do
        {
            which = HandleMenu(OptItems, OptMenu, null);
            if (SelectedId(OptMenu, which) == "messages")
            {
                // The player's own choice from now on, over the game pack's default (msg_enabled)
                _hudMessageManager.EnabledSetting = !_hudMessageManager.Enabled;
                if (!_hudMessageManager.Enabled)
                    _hudMessageManager.Clear();
                DrawOptionsMenu();
                ShootSnd();
            }
            else if (SelectedId(OptMenu, which) == "automapstats")
            {
                _automapManager.ShowStats = !_automapManager.ShowStats;
                DrawOptionsMenu();
                ShootSnd();
            }
            else if (which >= 0)
            {
                DrawOptionsMenu();
                MenuFadeIn();
                WaitKeyUp();
            }
        }
        while (which >= 0);

        MenuFadeOut();

        return 0;
    }

    internal static void DrawOptionsMenu()
    {
        DrawMenuComponents("options");
        DrawMenu(OptItems, OptMenu);
        DrawMenuCheckbox(OptItems, OptMenu, "messages", _hudMessageManager.Enabled);
        DrawMenuCheckbox(OptItems, OptMenu, "automapstats", _automapManager.ShowStats);
        DrawMenuGun(OptItems);
        _videoManager.Update();
    }

    /// <summary>
    /// The Video submenu. Toggles switch with Enter; choices step with left/right or Enter.
    /// Every change takes effect at once, and ones that could leave the screen unreadable
    /// ask to be kept (see ChangeVideo).
    /// </summary>
    internal static int CP_Video(int _)
    {
        int which;

        DrawVideoMenu();
        MenuFadeIn();
        WaitKeyUp();

        do
        {
            which = HandleMenu(VidItems, VidMenu, null, (w, delta) => StepVideoChoice(w, delta, wrap: false));

            var settings = _videoManager.Settings;
            switch (SelectedId(VidMenu, which))
            {
                case "fullscreen":
                    ShootSnd();
                    ChangeVideo(settings with { Fullscreen = !settings.Fullscreen });
                    break;

                case "vsync":
                    ShootSnd();
                    ChangeVideo(settings with { VSync = !settings.VSync });
                    break;

                case "aspect":
                    ShootSnd();
                    ChangeVideo(WithAspect(settings, !settings.AspectCorrect));
                    break;

                case "window-size":
                case "render-scale":
                case "ui-scale":
                case "filter":
                    StepVideoChoice(which, 1, wrap: true);
                    break;
            }
        }
        while (which >= 0);

        MenuFadeOut();

        return 0;
    }

    internal static void DrawVideoMenu(bool update = true)
    {
        var settings = _videoManager.Settings;
        var language = _assetManager.GetText("en-us");

        DrawMenuComponents("video");

        // The window's size means nothing while fullscreen
        FindMenuItem(VidMenu, "window-size")?.active = (short)(settings.Fullscreen ? 0 : 1);
        if (VidMenu[VidItems.curpos].active == 0)
            VidItems.curpos = (short)Math.Max(0, Array.FindIndex(VidMenu, item => item.active != 0));

        DrawMenu(VidItems, VidMenu);

        DrawMenuCheckbox(VidItems, VidMenu, "fullscreen", settings.Fullscreen);
        DrawMenuCheckbox(VidItems, VidMenu, "vsync", settings.VSync);
        DrawMenuCheckbox(VidItems, VidMenu, "aspect", settings.AspectCorrect);
        DrawMenuChoice(VidItems, VidMenu, "window-size", $"{settings.WindowWidth}x{settings.WindowHeight}");
        DrawMenuChoice(VidItems, VidMenu, "render-scale", settings.MatchWindow
            ? "$STR_AUTO".ToLanguageText(language)
            : $"{settings.RenderWidth}x{settings.RenderHeight}");
        var uiScale = VideoSettings.FormatScale(settings.EffectiveUiScale);
        DrawMenuChoice(VidItems, VidMenu, "ui-scale", settings.UiScale <= 0
            ? $"{"$STR_AUTO".ToLanguageText(language)} ({uiScale}x)"
            : $"{uiScale}x");
        DrawMenuChoice(VidItems, VidMenu, "filter", FilterName(settings.Filter).ToLanguageText(language));

        DrawMenuGun(VidItems);
        if (update)
            _videoManager.Update();
    }

    private static string FilterName(ScaleFilter filter) => filter switch
    {
        ScaleFilter.Linear => "$STR_LINEAR",
        _ => "$STR_NEAREST",
    };

    // Moves a Video menu choice row to its next value, and switches to it
    private static void StepVideoChoice(int which, int delta, bool wrap)
    {
        var id = SelectedId(VidMenu, which);
        if (id == null)
            return;

        var settings = _videoManager.Settings;
        var choices = VideoChoices(id, settings);
        if (choices.Count == 0)
            return;

        int current = Math.Max(0, choices.IndexOf(settings));
        int next = StepMenuChoice(current, choices.Count, delta, wrap);
        if (next == current)
            return;

        // Left/right arrive while HandleMenu is still running, before it records where the cursor is
        VidItems.curpos = (short)which;
        _audioManager.Play("menu/move1");
        ChangeVideo(choices[next]);
    }

    /// <summary>
    /// The video modes a choice row steps through, in order, each differing from the current
    /// one only in that row's setting. The current mode is always one of them.
    /// </summary>
    private static List<VideoSettings> VideoChoices(string id, VideoSettings settings) => id switch
    {
        "window-size" => WindowSizes(settings)
            .Select(size => settings with { WindowWidth = size.Width, WindowHeight = size.Height })
            .ToList(),
        // Auto first, then the whole multiples of 320x200
        "render-scale" => RenderScales(settings)
            .Select(scale => settings with { RenderScale = scale, RenderSize = null, MatchWindow = false })
            .Prepend(settings.MatchWindow ? settings : settings with { MatchWindow = true })
            .ToList(),
        "ui-scale" => UiScales(settings)
            .Select(scale => settings with { UiScale = scale })
            .ToList(),
        "filter" => Enum.GetValues<ScaleFilter>()
            .Select(filter => settings with { Filter = filter })
            .ToList(),
        _ => [],
    };

    // The window's height for each 320 across: 200 with square pixels, 240 at 4:3
    private static int WindowBaseHeight(bool aspectCorrect) => aspectCorrect ? 240 : 200;

    // Common monitor sizes offered as window sizes, for an Auto resolution to fill: 4:3, 16:10, 16:9 and 21:9
    private static readonly (int Width, int Height)[] CommonWindowSizes =
    [
        (800, 600), (1024, 768), (1280, 960), (1600, 1200),
        (1280, 800), (1440, 900), (1680, 1050), (1920, 1200), (2560, 1600),
        (1280, 720), (1366, 768), (1600, 900), (1920, 1080), (2560, 1440), (3200, 1800), (3840, 2160),
        (2560, 1080), (3440, 1440),
    ];

    /// <summary>
    /// Whole multiples of 320x200 (320x240 at 4:3, so the picture fills the window) and, with
    /// an Auto resolution (which fills a window of any shape), the common monitor sizes, that
    /// fit on the desktop, plus the current size if it's something else, as vid_window can set.
    /// </summary>
    private static List<(int Width, int Height)> WindowSizes(VideoSettings settings)
    {
        var (maxWidth, maxHeight) = _videoManager.GetLargestWindowSize();
        int baseHeight = WindowBaseHeight(settings.AspectCorrect);

        var sizes = new List<(int Width, int Height)> { (VideoSettings.BaseWidth, baseHeight) };
        for (int k = 2; VideoSettings.BaseWidth * k <= maxWidth && baseHeight * k <= maxHeight; k++)
            sizes.Add((VideoSettings.BaseWidth * k, baseHeight * k));

        if (settings.MatchWindow)
            sizes.AddRange(CommonWindowSizes.Where(size => size.Width <= maxWidth && size.Height <= maxHeight));

        if (!sizes.Contains((settings.WindowWidth, settings.WindowHeight)))
            sizes.Add((settings.WindowWidth, settings.WindowHeight));

        return sizes.Distinct().OrderBy(size => size.Width).ThenBy(size => size.Height).ToList();
    }

    private const double UiScaleStep = 0.25;

    /// <summary>
    /// Auto (0), then quarter steps from 1 up to the most that fits, then that most itself (to
    /// the hundredth below, as config.cfg keeps it), which fills the screen's height or width.
    /// A scale set in the console that's none of these is put in its place.
    /// </summary>
    private static List<double> UiScales(VideoSettings settings)
    {
        double max = Math.Floor(settings.MaxUiScale * 100) / 100;
        var scales = new List<double> { 0 };
        for (double scale = 1; scale <= max + 1e-9; scale += UiScaleStep)
            scales.Add(scale);
        scales.Add(max);
        if (settings.UiScale > 0)
            scales.Add(settings.UiScale);

        return scales.Distinct().Order().ToList();
    }

    private const int MaxRenderScale = 8;

    // Render scales up to the display's size, since drawing more than it can show is wasted
    private static List<int> RenderScales(VideoSettings settings)
    {
        var (displayWidth, displayHeight) = _videoManager.GetDisplaySize();
        int max = Math.Min(MaxRenderScale, Math.Min(displayWidth / VideoSettings.BaseWidth, displayHeight / VideoSettings.BaseHeight));
        max = Math.Max(max, Math.Max(1, settings.RenderScale));

        return Enumerable.Range(1, max).ToList();
    }

    /// <summary>
    /// Aspect correction on or off. A window of a standard size is reshaped to match, so the
    /// picture still fills it: 640x400 becomes 640x480, made smaller if that won't fit.
    /// </summary>
    private static VideoSettings WithAspect(VideoSettings settings, bool aspectCorrect)
    {
        var result = settings with { AspectCorrect = aspectCorrect };

        // An Auto resolution reshapes itself to fill the window instead
        if (settings.MatchWindow)
            return result;

        int k = settings.WindowWidth / VideoSettings.BaseWidth;
        bool standardSize = settings.WindowWidth == VideoSettings.BaseWidth * k
            && settings.WindowHeight == WindowBaseHeight(settings.AspectCorrect) * k;
        if (!standardSize)
            return result;

        var (maxWidth, maxHeight) = _videoManager.GetLargestWindowSize();
        int baseHeight = WindowBaseHeight(aspectCorrect);
        while (k > 1 && (VideoSettings.BaseWidth * k > maxWidth || baseHeight * k > maxHeight))
            k--;

        return result with { WindowWidth = VideoSettings.BaseWidth * k, WindowHeight = baseHeight * k };
    }

    /// <summary>
    /// Switches to a video mode and redraws the menu in it. A change that could leave the
    /// screen unreadable (fullscreen, the window's size, the resolution, VSync) then asks to be
    /// kept, and goes back if it isn't.
    /// </summary>
    private static void ChangeVideo(VideoSettings next)
    {
        var language = _assetManager.GetText("en-us");
        var previous = _videoManager.Settings;

        if (!_videoManager.ApplyVideoSettings(next))
        {
            DrawVideoMenu(update: false);
            _audioManager.Play("player/usefail");
            Message("$STR_VIDEOFAILED".ToLanguageText(language));
            _inputManager.ClearKeysDown();
            _inputManager.Ack();
        }
        // Against the mode as set, with an Auto resolution's size worked out
        else if (NeedsVideoConfirm(previous, _videoManager.Settings) && !ConfirmVideoMode())
        {
            _videoManager.ApplyVideoSettings(previous);
        }

        DrawVideoMenu();
    }

    private static bool NeedsVideoConfirm(VideoSettings previous, VideoSettings next)
        => next.Fullscreen != previous.Fullscreen
           || next.RenderWidth != previous.RenderWidth || next.RenderHeight != previous.RenderHeight
           || next.VSync != previous.VSync
           || (!next.Fullscreen && (next.WindowWidth != previous.WindowWidth || next.WindowHeight != previous.WindowHeight));

    private const int VideoConfirmSeconds = 10;

    /// <summary>
    /// Asks whether to keep the video mode just switched to, counting down on screen. Only
    /// the yes key keeps it: no, escape, or letting the time run out goes back.
    /// </summary>
    private static bool ConfirmVideoMode()
    {
        var language = _assetManager.GetText("en-us");
        uint start = GameEngineManager.GetTimeCount();
        int shown = -1;
        bool keep = false;
        ControlInfo ci;

        _inputManager.ClearKeysDown();

        while (true)
        {
            int left = VideoConfirmSeconds - (int)((GameEngineManager.GetTimeCount() - start) / Timing.TickBase);
            if (left <= 0)
                break;

            // The box is redrawn over a fresh menu each second, since its width changes with the number
            if (left != shown)
            {
                shown = left;
                DrawVideoMenu(update: false);
                Message("$STR_KEEPVIDEO".ToLanguageText(language).Replace("{SECONDS}", left.ToString()));
            }

            ReadAnyControl(out ci);
            if (_inputManager.IsKeyDown(ScanCodes.sc_Y) || ci.button0)
            {
                keep = true;
                break;
            }

            if (_inputManager.IsKeyDown(ScanCodes.sc_N) || _inputManager.IsKeyDown(ScanCodes.sc_Escape) || ci.button1)
                break;

            GameEngineManager.DelayMs(5);
        }

        _inputManager.ClearKeysDown();
        _audioManager.Play(keep ? "menu/activate" : "menu/escape");
        return keep;
    }

    internal static int CP_Sound(int _)
    {
        int which;

        DrawSoundMenu();
        MenuFadeIn();
        WaitKeyUp();

        do
        {
            which = HandleMenu(SndItems, SndMenu, null, AdjustSoundVolume);
            //
            // HANDLE MENU CHOICES
            //
            // Each row switches its device; the shot sound previews what the fallback plays now
            switch (SelectedId(SndMenu, which))
            {
                case "sound-pc":
                    _audioManager.PcSoundEnabled ^= true;
                    DrawSoundMenu();
                    ShootSnd();
                    break;
                case "sound-adlib":
                    _audioManager.AdLibSoundEnabled ^= true;
                    DrawSoundMenu();
                    ShootSnd();
                    break;
                case "sound-digitized":
                    _audioManager.DigitizedSoundEnabled ^= true;
                    DrawSoundMenu();
                    ShootSnd();
                    break;
                case "music":
                    _audioManager.MusicEnabled ^= true;
                    DrawSoundMenu();
                    ShootSnd();
                    break;
            }
        }
        while (which >= 0);

        MenuFadeOut();

        return 0;
    }

    internal static void DrawSoundMenu()
    {
        DrawMenuComponents("sound");
        DrawMenu(SndItems, SndMenu);

        DrawMenuCheckbox(SndItems, SndMenu, "sound-pc", _audioManager.PcSoundEnabled);
        DrawMenuCheckbox(SndItems, SndMenu, "sound-adlib", _audioManager.AdLibSoundEnabled);
        DrawMenuCheckbox(SndItems, SndMenu, "sound-digitized", _audioManager.DigitizedSoundEnabled);
        DrawMenuCheckbox(SndItems, SndMenu, "music", _audioManager.MusicEnabled);
        DrawMenuSlider(SndItems, SndMenu, "sound-volume", _audioManager.SoundVolume, AudioManager.MaxVolume);
        DrawMenuSlider(SndItems, SndMenu, "music-volume", _audioManager.MusicVolume, AudioManager.MaxVolume);

        DrawMenuGun(SndItems);
        _videoManager.Update();
    }

    // Left/right on a volume row. The sound volume previews with a menu sound; the music
    // volume is heard straight away on the menu's track.
    private static void AdjustSoundVolume(int which, int delta)
    {
        switch (SelectedId(SndMenu, which))
        {
            case "sound-volume":
                int soundVolume = _audioManager.SoundVolume;
                _audioManager.SoundVolume += delta;
                if (_audioManager.SoundVolume == soundVolume)
                    return;
                DrawMenuSlider(SndItems, SndMenu, "sound-volume", _audioManager.SoundVolume, AudioManager.MaxVolume);
                break;

            case "music-volume":
                int musicVolume = _audioManager.MusicVolume;
                _audioManager.MusicVolume += delta;
                if (_audioManager.MusicVolume == musicVolume)
                    return;
                DrawMenuSlider(SndItems, SndMenu, "music-volume", _audioManager.MusicVolume, AudioManager.MaxVolume);
                break;

            default:
                return;
        }

        _videoManager.Update();
        _audioManager.Play("menu/move1");
    }

    private const int MenuSliderWidth = 88;
    private const int MenuSliderOffset = 120; // from the start of the row's text, clear of "Sound Volume"; choices use it too

    // A level bar to the right of a SliderMenuItem's text, drawn like the mouse sensitivity bar:
    // a track with a highlighted knob at the current value (0 to max).
    private static void DrawMenuSlider(CP_iteminfo iteminfo, CP_itemtype[] items, string id, int value, int max)
    {
        int index = Array.FindIndex(items, item => item.id == id);
        if (index < 0)
            return;

        int knobWidth = MenuSliderWidth / (max + 1);
        int x = iteminfo.x + iteminfo.indent + MenuSliderOffset;
        int y = iteminfo.y + index * iteminfo.rowHeight + 2;
        _videoManager.Bar(x, y, knobWidth * (max + 1), 9, "TEXTCOLOR");
        DrawOutline(x, y, knobWidth * (max + 1), 9, "Black", "HIGHLIGHT");
        DrawOutline(x + knobWidth * value, y, knobWidth, 9, "Black", "READCOLOR");
        _videoManager.Bar(x + knobWidth * value + 1, y + 1, knobWidth - 1, 8, "READHCOLOR");
    }

    private const int MenuChoiceWidth = 96; // room for the widest value, like "1920x1200"

    // A ChoiceMenuItem's current value, in the same column as the sliders. Greyed out with the row.
    private static void DrawMenuChoice(CP_iteminfo iteminfo, CP_itemtype[] items, string id, string value)
    {
        int index = Array.FindIndex(items, item => item.id == id);
        if (index < 0)
            return;

        int x = iteminfo.x + iteminfo.indent + MenuSliderOffset;
        int y = iteminfo.y + index * iteminfo.rowHeight;
        _videoManager.Bar(x, y, MenuChoiceWidth, iteminfo.rowHeight, "BKGDCOLOR");

        TextAt(x, y, MenuItemStyle(iteminfo, MenuItemColor(items[index], false))).Print(value);
    }

    /// <summary>
    /// The choice after stepping a ChoiceMenuItem by delta. Left/right stop at the ends of the
    /// list; Enter wraps round, so it can reach every value on its own.
    /// </summary>
    private static int StepMenuChoice(int current, int count, int delta, bool wrap)
    {
        if (count <= 0)
            return 0;

        int next = current + delta;
        return wrap ? ((next % count) + count) % count : Math.Clamp(next, 0, count - 1);
    }

    internal static int CP_Control(int _)
    {
        int which;

        DrawCtlScreen();
        MenuFadeIn();
        WaitKeyUp();

        do
        {
            which = HandleMenu(CtlItems, CtlMenu, null);
            switch (SelectedId(CtlMenu, which))
            {
                case "mouse-enabled":
                    mouseenabled ^= true;
                    _inputManager.CenterMouse();
                    DrawCtlScreen();
                    ShootSnd();
                    break;

                case "mouse-look":
                    mouselook ^= true;
                    DrawCtlScreen();
                    ShootSnd();
                    break;

                case "mouse-invert":
                    mouseinvert ^= true;
                    DrawCtlScreen();
                    ShootSnd();
                    break;

                case "joystick-enabled":
                    joystickenabled ^= true;
                    DrawCtlScreen();
                    ShootSnd();
                    break;

                case "mouse-sensitivity":
                case "controller-settings":
                case "customize":
                    DrawCtlScreen();
                    MenuFadeIn();
                    WaitKeyUp();
                    break;
            }
        }
        while (which >= 0);

        MenuFadeOut();

        return 0;
    }

    internal static void DrawCtlScreen()
    {
        int i;
        DrawMenuComponents("control");

        var mouseEnabledItem = FindMenuItem(CtlMenu, "mouse-enabled");
        var mouseSensItem = FindMenuItem(CtlMenu, "mouse-sensitivity");

        if (_inputManager.JoyPresent())
            FindMenuItem(CtlMenu, "joystick-enabled")?.active = 1;

        if (_inputManager.IsMousePresent())
        {
            mouseEnabledItem?.active = 1;
        }

        mouseSensItem?.active = (short)(mouseenabled ? 1 : 0);

        // Inverting only means something while the mouse looks
        FindMenuItem(CtlMenu, "mouse-look")?.active = (short)(mouseenabled ? 1 : 0);
        FindMenuItem(CtlMenu, "mouse-invert")?.active = (short)(mouseenabled && mouselook ? 1 : 0);

        DrawMenu(CtlItems, CtlMenu);

        DrawMenuCheckbox(CtlItems, CtlMenu, "mouse-enabled", mouseenabled);
        DrawMenuCheckbox(CtlItems, CtlMenu, "mouse-look", mouselook);
        DrawMenuCheckbox(CtlItems, CtlMenu, "mouse-invert", mouseinvert);
        DrawMenuCheckbox(CtlItems, CtlMenu, "joystick-enabled", joystickenabled);

        //
        // PICK FIRST AVAILABLE SPOT
        //
        if (CtlItems.curpos < 0 || CtlMenu[CtlItems.curpos].active == 0)
        {
            for (i = 0; i < CtlItems.amount; i++)
            {
                if (CtlMenu[i].active != 0)
                {
                    CtlItems.curpos = (short)i;
                    break;
                }
            }
        }

        DrawMenuGun(CtlItems);
        _videoManager.Update();
    }

    ////////////////////////////////////////////////////////////////////
    //
    // CONTROLLER SETTINGS
    //
    ////////////////////////////////////////////////////////////////////

    // The dead zone slider moves in steps of this many percent
    private const int JoyDeadzoneStep = 5;
    private const int JoyDeadzoneMax = 50;

    internal static int CP_ControllerSettings(int _)
    {
        int which;

        DrawControllerMenu();
        MenuFadeIn();
        WaitKeyUp();

        do
        {
            which = HandleMenu(JoyItems, JoyMenu, null, AdjustControllerSetting);

            // Enter steps the stick layout round too; the sliders only move with left/right
            if (SelectedId(JoyMenu, which) == "joy-sticks")
                AdjustControllerSetting(which, 1);
        }
        while (which >= 0);

        MenuFadeOut();

        return 0;
    }

    internal static void DrawControllerMenu()
    {
        var language = _assetManager.GetText("en-us");

        DrawMenuComponents("controller");
        DrawMenu(JoyItems, JoyMenu);
        DrawControllerValues();

        // Which controller the settings are for, under the hints
        CenteredText(0, 320, 160, new TextStyle(SMALL_FONT, "READCOLOR")).CPrint(FitText(_inputManager.ControllerName is string name
            ? $"{"$STR_CTL_PAD".ToLanguageText(language)} {name}"
            : "$STR_CTL_NOPAD".ToLanguageText(language), 300, SMALL_FONT));     // a controller's name can be long

        DrawMenuGun(JoyItems);
        _videoManager.Update();
    }

    private static void DrawControllerValues()
    {
        var language = _assetManager.GetText("en-us");

        DrawMenuSlider(JoyItems, JoyMenu, "joy-deadzone", Math.Clamp(joydeadzone, 0, JoyDeadzoneMax) / JoyDeadzoneStep, JoyDeadzoneMax / JoyDeadzoneStep);
        DrawMenuSlider(JoyItems, JoyMenu, "joy-turnspeed", Math.Clamp(joyturnspeed, 0, JOYTURNSPEEDS - 1), JOYTURNSPEEDS - 1);
        DrawMenuChoice(JoyItems, JoyMenu, "joy-sticks", (joyclassicsticks ? "$STR_JOYCLASSIC" : "$STR_JOYMODERN").ToLanguageText(language));
    }

    // Left/right on a Controller Settings row
    private static void AdjustControllerSetting(int which, int delta)
    {
        switch (SelectedId(JoyMenu, which))
        {
            case "joy-deadzone":
                int deadzone = Math.Clamp((joydeadzone / JoyDeadzoneStep + delta) * JoyDeadzoneStep, 0, JoyDeadzoneMax);
                if (deadzone == joydeadzone)
                    return;
                joydeadzone = deadzone;
                break;

            case "joy-turnspeed":
                int turnspeed = Math.Clamp(joyturnspeed + delta, 0, JOYTURNSPEEDS - 1);
                if (turnspeed == joyturnspeed)
                    return;
                joyturnspeed = turnspeed;
                break;

            case "joy-sticks":
                joyclassicsticks = !joyclassicsticks;
                break;

            default:
                return;
        }

        DrawControllerValues();
        _videoManager.Update();
        _audioManager.Play("menu/move1");
    }

    // The on/off box to the left of a ToggleMenuItem's text
    private static void DrawMenuCheckbox(CP_iteminfo iteminfo, CP_itemtype[] items, string id, bool on)
    {
        int index = Array.FindIndex(items, item => item.id == id);
        if (index >= 0)
        {
            items[index].checkbox = on;
            DrawMenuCheckbox(iteminfo, index, on);
        }
    }

    /// <param name="highlighted">Under a highlight bar: the pack's c_selected_hi / c_notselected_hi, when it has them</param>
    private static void DrawMenuCheckbox(CP_iteminfo iteminfo, int index, bool on, bool highlighted = false)
    {
        int x = iteminfo.x + iteminfo.indent + iteminfo.checkboxX;
        int y = iteminfo.y + index * iteminfo.rowHeight + iteminfo.checkboxY;
        var pic = on ? "c_selected" : "c_notselected";
        if (highlighted && _assetManager.Exists<GraphicAsset>(pic + "_hi"))
            pic += "_hi";
        _graphicManager.DrawPic(pic, x, y);
    }

    internal static int CP_ChangeView(int _)
    {
        var language = _assetManager.GetText("en-us");
        int exit = 0, oldview, newview;
        ControlInfo ci;

        newview = oldview = viewsize;
        DrawChangeView(oldview);
        MenuFadeIn();

        do
        {
            CheckPause();
            GameEngineManager.DelayMs(5);
            ReadAnyControl(out ci);
            switch (ci.dir)
            {
                case Direction.South:
                case Direction.West:
                    newview--;
                    if (newview < 4)
                        newview = 4;
                    if (newview >= 19) DrawChangeView(newview);
                    else ShowViewSize(newview);
                    _videoManager.Update();
                    _audioManager.Play("world/hitwall");
                    TicDelay(10);
                    break;

                case Direction.North:
                case Direction.East:
                    newview++;
                    if (newview >= 21)
                    {
                        newview = 21;
                        DrawChangeView(newview);
                    }
                    else ShowViewSize(newview);
                    _videoManager.Update();
                    _audioManager.Play("world/hitwall");
                    TicDelay(10);
                    break;
            }

            if (ci.button0 || _inputManager.IsKeyDown(ScanCodes.sc_Enter))
                exit = 1;
            else if (ci.button1 || _inputManager.IsKeyDown(ScanCodes.sc_Escape))
            {
                _audioManager.Play("menu/escape");
                MenuFadeOut();
                if (_videoManager.HasMargins)
                    _videoManager.ClearScreen(0);
                return 0;
            }
        }
        while (exit == 0);

        if (oldview != newview)
        {
            _audioManager.Play("menu/activate");
            Message("$STR_THINK".ToLanguageText(language) + "...");
            NewViewSize(newview);
        }

        ShootSnd();
        MenuFadeOut();
        if (_videoManager.HasMargins)
            _videoManager.ClearScreen(0);

        return 0;
    }

    internal static void DrawChangeView(int view)
    {
        var language = _assetManager.GetText("en-us");
        // The help goes in the bottom 40 lines of the screen, where the status bar is
        int top = _videoManager.ScreenYAboveBottom(40);
        if (view != 21)
            _videoManager.BarScaledCoord(0, top, _videoManager.screenWidth, _videoManager.screenHeight - top, bordercol);

        ShowViewSize(view);

        using var _ = _videoManager.UseUiOrigin(UiAnchor.Bottom);
        var help = CenteredText(0, 320, 200 - 39, MenuStyle("HIGHLIGHT"));
        help.CPrint("$STR_SIZE1".ToLanguageText(language) + "\n");
        help.CPrint("$STR_SIZE2".ToLanguageText(language) + "\n");
        help.CPrint("$STR_SIZE3".ToLanguageText(language));
        _videoManager.Update();
    }

    internal static int CP_ReadThis(int _)
    {
        StartCPMusic("CORNER");
        HelpScreens();
        StartCPMusic(MENUSONG);
        return 1;
    }

    internal static int CP_ViewScores(int _)
    {
        StartCPMusic(HIGHSCORESSONG);

        DrawHighScores();
        _videoManager.Update();
        MenuFadeIn();

        _inputManager.Ack();

        StartCPMusic(MENUSONG);
        MenuFadeOut();

        return 0;
    }

    internal static int CP_EndGame(int _)
    {
        var language = _assetManager.GetText("en-us");
        int res = Confirm("$ENDGAMESTR".ToLanguageText(language));

        DrawMainMenu();
        if (res == 0) return 0;

        playerstate.lives = 0;
        playstate = playstatetypes.ex_died;
        LastAttacker = null;
        endedFromMenu = true;       // no game over screen (bstone's InstantQuit)

        FindMenuItem(MainMenu, "savegame")?.active = 0;
        EnableViewScoresMenuItem();
        return 1;
    }

    internal static int CP_Quit(int _)
    {
        var gameInfo = _gameEngineManager.GetGameInfo();
        string endStr = gameInfo.EndStrings[(US_RndT() & (gameInfo.EndStrings.Count - 2)) + (US_RndT() & 1)];
        if (Confirm(endStr) != 0)
        {
            _videoManager.Update();
            _audioManager.SetPaused(true);
            _audioManager.StopAll();
            MenuFadeOut();
            _gameEngineManager.Quit("");
            return 0;
        }

        DrawMainMenu();
        return 0;
    }

    internal static int MouseSensitivity(int _)
    {
        ControlInfo ci;
        int exit = 0, oldMA;


        oldMA = mouseadjustment;
        DrawMouseSens();
        do
        {
            GameEngineManager.DelayMs(5);
            ReadAnyControl(out ci);
            switch (ci.dir)
            {
                case Direction.North:
                case Direction.West:
                    if (mouseadjustment != 0)
                    {
                        mouseadjustment--;
                        _videoManager.Bar(60, 97, 200, 10, "TEXTCOLOR");
                        DrawOutline(60, 97, 200, 10, "Black", "HIGHLIGHT");
                        DrawOutline(60 + 20 * mouseadjustment, 97, 20, 10, "Black", "READCOLOR");
                        _videoManager.Bar(61 + 20 * mouseadjustment, 98, 19, 9, "READHCOLOR");
                        _videoManager.Update();
                        _audioManager.Play("menu/move1");
                        TicDelay(20);
                    }
                    break;

                case Direction.South:
                case Direction.East:
                    if (mouseadjustment < 9)
                    {
                        mouseadjustment++;
                        _videoManager.Bar(60, 97, 200, 10, "TEXTCOLOR");
                        DrawOutline(60, 97, 200, 10, "Black", "HIGHLIGHT");
                        DrawOutline(60 + 20 * mouseadjustment, 97, 20, 10, "Black", "READCOLOR");
                        _videoManager.Bar(61 + 20 * mouseadjustment, 98, 19, 9, "READHCOLOR");
                        _videoManager.Update();
                        _audioManager.Play("menu/move1");
                        TicDelay(20);
                    }
                    break;
            }

            if (ci.button0 || _inputManager.IsKeyDown(ScanCodes.sc_Space) || _inputManager.IsKeyDown(ScanCodes.sc_Enter))
                exit = 1;
            else if (ci.button1 || _inputManager.IsKeyDown(ScanCodes.sc_Escape))
                exit = 2;

        }
        while (exit == 0);

        if (exit == 2)
        {
            mouseadjustment = oldMA;
            _audioManager.Play("menu/escape");
        }
        else
            _audioManager.Play("menu/activate");

        WaitKeyUp();
        MenuFadeOut();

        return 0;
    }

    internal static void DrawMouseSens()
    {
        DrawMenuComponents("mouse-sensitivity");

        _videoManager.Bar(60, 97, 200, 10, "TEXTCOLOR");
        DrawOutline(60, 97, 200, 10, "Black", "HIGHLIGHT");
        DrawOutline(60 + 20 * mouseadjustment, 97, 20, 10, "Black", "READCOLOR");
        _videoManager.Bar(61 + 20 * mouseadjustment, 98, 19, 9, "READHCOLOR");

        _videoManager.Update();
        MenuFadeIn();
    }

    internal static void CleanupControlPanel()
    {
        // Keep whatever was changed in the menus (view size, controls, sensitivity) even if the
        // game doesn't get to exit cleanly.
        _gameEngineManager.WriteConfig();
    }

    internal static void DrawMenuGun(CP_iteminfo iteminfo)
    {
        int x, y;

        // A highlight bar goes on with its item's text when HandleMenu starts
        if (iteminfo.cursor != null)
            return;

        x = iteminfo.x & -8;    // same column HandleMenu draws and erases the gun in
        y = iteminfo.y + iteminfo.curpos * iteminfo.rowHeight - 2;
        _graphicManager.DrawPic("c_cursor1", x, y);
    }

    /// <param name="areaHeight">Height of the screen area the question is centered in: the play view (MAXY) in a game</param>
    internal static int Confirm(string text, int areaHeight = 200)
    {
        var language = _assetManager.GetText("en-us");
        int xit = 0, x, y, tick = 0, lastBlinkTime;
        string[] whichsnd = ["menu/escape", "menu/activate"];
        ControlInfo ci;

        var message = Message(text.ToLanguageText(language), areaHeight);
        _inputManager.ClearKeysDown();
        WaitKeyUp();

        //
        // BLINK CURSOR
        //
        x = message.PrintX;
        y = message.PrintY;
        lastBlinkTime = (int)GameEngineManager.GetTimeCount();

        do
        {
            ReadAnyControl(out ci);

            if (GameEngineManager.GetTimeCount() - lastBlinkTime >= 10)
            {
                switch (tick)
                {
                    case 0:
                        _videoManager.Bar(x, y, 8, 13, message.Style.Background);
                        break;
                    case 1:
                        _graphicManager.DrawText(x, y, "_", message.Style);
                        break;
                }
                _videoManager.Update();
                tick ^= 1;
                lastBlinkTime = (int)GameEngineManager.GetTimeCount();
            }
            else GameEngineManager.DelayMs(5);
        }
        while (!_inputManager.IsKeyDown(ScanCodes.sc_Y) && !_inputManager.IsKeyDown(ScanCodes.sc_N) && !_inputManager.IsKeyDown(ScanCodes.sc_Escape) && !ci.button0 && !ci.button1);

        if (_inputManager.IsKeyDown(ScanCodes.sc_Y) || ci.button0)
        {
            xit = 1;
            ShootSnd();
        }
        _inputManager.ClearKeysDown();
        WaitKeyUp();

        _audioManager.Play(whichsnd[xit]);

        return xit;
    }

    /// <summary>
    /// Waits for the select and back buttons, Space, Enter and Escape to be let go, so one press
    /// isn't taken again by the next screen. Matters most for a controller, whose buttons are read
    /// as they are rather than as presses that ClearKeysDown can throw away.
    /// </summary>
    internal static void WaitKeyUp()
    {
        ControlInfo ci;
        while (true)
        {
            ReadAnyControl(out ci);
            bool keyPressed =
               ci.button0 ||
               ci.button1 ||
               ci.button2 ||
               ci.button3 ||
               _inputManager.IsKeyDown(ScanCodes.sc_Space) ||
               _inputManager.IsKeyDown(ScanCodes.sc_Enter) ||
               _inputManager.IsKeyDown(ScanCodes.sc_Escape);

            if (!keyPressed)
                break;

            GameEngineManager.DelayMs(5);
        }
    }

    /// <summary>
    /// Shows text in a box in the middle of the screen, and returns the box's text window with its
    /// print position just after the text
    /// </summary>
    /// <param name="areaHeight">Height of the screen area the box is centered in: the play view (MAXY) in a game</param>
    internal static TextWindow Message(string text, int areaHeight = 200)
    {
        int h = 0, w = 0, mw = 0, i, len = text.Length;
        var box = _gameEngineManager.GetGameInfo().MenuMessage;
        var style = box == null
            ? new TextStyle(LARGE_FONT, "Black", "TEXTCOLOR")
            : new TextStyle(box.Font, box.Color, box.Med, Shadow: new FontShadow(1, 1, box.Shadow));

        var font = _fontManager.Find(style.Font);
        if (font == null)
            return TextWindow.FullScreen(_graphicManager, style);
        h = font.LineHeight;

        for (i = 0; i < len; i++)
        {
            if (text[i] == '\n')
            {
                if (w > mw)
                    mw = w;
                w = 0;
                h += font.LineHeight;
            }
            else
                w += font.Advance(text[i]);
        }

        if (w + 10 > mw)
            mw = w + 10;

        int x = 160 - (mw / 2);
        int y = (areaHeight / 2) - (h / 2);

        if (box != null)
            BevelBox(x - 5, y - 5, mw + 10, h + 10, box.Hi, box.Med, box.Lo);
        else
        {
            DrawWindow(x - 5, y - 5, mw + 10, h + 10, "TEXTCOLOR");
            DrawOutline(x - 5, y - 5, mw + 10, h + 10, "Black", "HIGHLIGHT");
        }
        var window = new TextWindow(_graphicManager, x, y, mw, h, style);
        window.Print(text);
        _videoManager.Update();
        return window;
    }

    internal static void FreeMusic()
    {
        //UNCACHEAUDIOCHUNK(STARTMUSIC + chunk);
    }

    internal static void CheckForEpisodes()
    {
        /*if (configdir != string.Empty)
        {
            if (!Directory.Exists(configdir))
            {
                try
                {
                    DirectoryInfo di = Directory.CreateDirectory(configdir);
                }
                catch (IOException e)
                {
                    _gameEngineManager.Quit($"The configuration directory \"{configdir}\" could not be created.");
                }
            }
        }*/

        // The running release's data files were checked for when the assets were loaded
        // Build every menudef now so problems in any of them are reported at startup
        foreach (var menuName in _assetManager.GetMenuNames())
        {
            var menu = _assetManager.GetMenu(menuName);
            if (menu == null)
                continue;

            var music = _gameEngineManager.GetGameInfo().MenuMusic ?? menu.Music;
            if (!string.IsNullOrEmpty(music) && !_assetManager.Exists<Wolf3dImfAudio>(music))
                Console.WriteLine($"Menu '{menuName}': unknown music '{music}'");

            var packLists = menu.MenuItems.Select(item => item.GamePacks)
                .Concat(menu.Components.Select(component => component.GamePacks));
            foreach (var pack in packLists.Where(list => list != null).SelectMany(list => list!).Distinct())
            {
                if (!GameEngineManager.KnownGamePackIds.Contains(GamePackList.PackName(pack), StringComparer.OrdinalIgnoreCase))
                    Console.WriteLine($"Menu '{menuName}': unknown game pack '{pack}' in game-packs");
            }
        }

        (MainMenu, MainItems) = LoadMenu("main-menu");
        (OptMenu, OptItems) = LoadMenu("options");
        (VidMenu, VidItems) = LoadMenu("video");
        (SndMenu, SndItems) = LoadMenu("sound");
        (CtlMenu, CtlItems) = LoadMenu("control", curpos: -1);
        (JoyMenu, JoyItems) = LoadMenu("controller");
        customizeRows = LoadCustomizeRows();
        (NewEmenu, NewEitems) = LoadMenu("new-episode");
        (NewMenu, NewItems) = LoadMenu("new-game");
        (ClassMenu, ClassItems) = LoadMenu("new-class");

        (MusicMenu, MusicItems) = LoadMenu("jukebox");
        // Only the songs the game has (the shareware has 10 of the 27)
        MusicMenu = MusicMenu.Where(item => item.data is not string song || _assetManager.Exists<Wolf3dImfAudio>(song)).ToArray();
        MusicItems.amount = (short)Math.Min(JukeboxPageSize, MusicMenu.Length);
    }

    /// <summary>
    /// Builds the item list and layout for a menu defined in menudefs/
    /// </summary>
    private static (CP_itemtype[] items, CP_iteminfo info) LoadMenu(string name, short curpos = 0)
    {
        var menuAsset = _assetManager.GetMenu(name)
            ?? throw new InvalidOperationException($"Menu '{name}' not found in menudefs");
        var language = _assetManager.GetText("en-us");

        CP_itemtype[] items;
        if (menuAsset.ItemsSource != null)
        {
            if (menuAsset.MenuItems.Count > 0)
                Console.WriteLine($"Menu '{name}': has items-source '{menuAsset.ItemsSource}', so its menu-items are ignored");
            items = BuildSourceItems(name, menuAsset.ItemsSource);
        }
        else
        {
            items = menuAsset.MenuItems
                    .Where(mi => InCurrentGamePack(mi.GamePacks))
                    .Select(mi =>
                    new CP_itemtype(
                        (short)(mi.Enabled && mi is not BlankMenuItem ? (mi.Highlighted ? 2 : 1) : 0),
                        (mi.Text ?? "").ToLanguageText(language),
                        MapFunction(name, mi as MenuSwitcher))
                    {
                        id = mi.Id,
                        shortKey = string.IsNullOrEmpty(mi.ShortKey) ? '\0' : mi.ShortKey[0],
                        data = (mi as MusicMenuItem)?.Music
                    })
                    .ToArray();
        }

        if (menuAsset.DefaultSelection is int selection)
        {
            if (selection >= 0 && selection < items.Length)
                curpos = (short)selection;
            else
                Console.WriteLine($"Menu '{name}': default-selection {selection} is out of range (0-{items.Length - 1})");
        }

        var info = new CP_iteminfo(
            (short)menuAsset.Position.X,
            (short)menuAsset.Position.Y,
            amount: (short)items.Length,
            curpos: curpos,
            indent: (short)menuAsset.Indent)
        {
            rowHeight = menuAsset.RowHeight is > 0 and int rowHeight ? rowHeight : 13,
            font = menuAsset.Font,
            itemShadow = menuAsset.ItemShadow,
            cursor = menuAsset.Cursor,
            selectionPic = menuAsset.SelectionPic,
            checkboxX = menuAsset.CheckboxOffset?.X ?? -24,
            checkboxY = menuAsset.CheckboxOffset?.Y ?? 3,
        };

        return (items, info);
    }

    /// <summary>
    /// Builds menu items from game data for a menu's items-source
    /// </summary>
    private static CP_itemtype[] BuildSourceItems(string menuName, string source)
    {
        var gameInfo = _gameEngineManager.GetGameInfo();
        var language = _assetManager.GetText("en-us");

        switch (source.ToLowerInvariant())
        {
            case "episodes":
                // Episode names are two lines, so a blank row follows each one, unless the menu's
                // rows are tall enough for both (spacer-rows: false)
                if (_assetManager.GetMenu(menuName)?.SpacerRows == false)
                    return gameInfo.Episodes.Values
                        .Select(ep => new CP_itemtype(1, ep.Name.ToLanguageText(language), null, ep) { shortKey = ep.Key })
                        .ToArray();
                return gameInfo.Episodes.Values.SelectMany(ep =>
                    new CP_itemtype[]
                    {
                        new CP_itemtype(1, ep.Name.ToLanguageText(language), null, ep) { shortKey = ep.Key },
                        new CP_itemtype(0, "", null)
                    }).SkipLast(1)
                    .ToArray();

            case "skills":
                return gameInfo.Skills.Values
                    .Select(skill => new CP_itemtype(1, skill.Name.ToLanguageText(language), null, skill))
                    .ToArray();

            case "player-classes":
                // Named by game-info, else by the class's tag (the name obituaries use), else the class
                return PlayerClasses().Select(playerClass =>
                {
                    var info = gameInfo.PlayerClasses.FirstOrDefault(c => string.Equals(c.Key, playerClass, StringComparison.OrdinalIgnoreCase)).Value;
                    var name = info?.Name ?? _inventoryManager.GetStringProperty(playerClass, "tag") ?? playerClass;
                    return new CP_itemtype(1, name.ToLanguageText(language), null, new PlayerClassChoice(playerClass, info?.PicName));
                }).ToArray();

            case "mods":
                return BuildModsPage();

            case "lan-games":
                return BuildJoinRows();

            default:
                Console.WriteLine($"Menu '{menuName}': unknown items-source '{source}'");
                return [];
        }
    }

    /// <summary>
    /// Finds a menu item by its YAML id, or null if the menu has no such item (or isn't loaded yet)
    /// </summary>
    internal static CP_itemtype? FindMenuItem(CP_itemtype[] items, string id)
        => items.FirstOrDefault(item => item.id == id);

    /// <summary>
    /// Id of the chosen item from HandleMenu's result, or null for escape / an item without an id
    /// </summary>
    private static string? SelectedId(CP_itemtype[] items, int which)
        => which >= 0 && which < items.Length ? items[which].id : null;

    /// <summary>
    /// Draws the non-interactive parts of a menu (background, windows, graphics, labels)
    /// </summary>
    internal static void DrawMenuComponents(string name)
    {
        var menu = _assetManager.GetMenu(name);
        if (menu == null)
            return;

        StartMenuMusic(menu);

        foreach (var component in menu.Components)
        {
            if (!InCurrentGamePack(component.GamePacks))
                continue;

            if (component is Label label)
                DrawLabel(label);
            else
                _graphicManager.DrawComponent(component);
        }
    }

    /// <summary>
    /// Whether a menu item or component with this game-packs list belongs in the running game pack
    /// </summary>
    private static bool InCurrentGamePack(List<string>? gamePacks)
        => GamePackList.Includes(gamePacks, _gameEngineManager.GamePackId, _assetManager.BasePackId);

    /// <summary>
    /// Plays a menu's music. A track that is already playing carries on rather than restarting,
    /// and a menu without music leaves the current track alone.
    /// </summary>
    private static void StartMenuMusic(MenuMetadata menu)
    {
        if (string.IsNullOrEmpty(menu.Music))
            return;

        var music = _gameEngineManager.GetGameInfo().MenuMusic ?? menu.Music;
        if (string.Equals(_audioManager.CurrentMusicTrack, music, StringComparison.OrdinalIgnoreCase))
        {
            _audioManager.SetPaused(false);
            return;
        }

        StartCPMusic(music);
    }

    /// <summary>A label's shadow: its own, none, or null to keep the font's</summary>
    private static FontShadow? LabelShadow(Label label)
    {
        if (label.Shadow == false)
            return FontShadow.None;

        if (label.Shadow == true || label.ShadowX != null || label.ShadowY != null || label.ShadowColor != null)
        {
            var d = FontShadow.Default;
            return new FontShadow(label.ShadowX ?? d.X, label.ShadowY ?? d.Y, label.ShadowColor ?? d.Color);
        }

        return null;
    }

    /// <summary>A label's gradient: its own, none, or null to keep the font's</summary>
    private static FontGradient? LabelGradient(Label label)
    {
        if (label.Gradient == false)
            return FontGradient.None;

        if (label.Gradient == true || label.GradientTop != null || label.GradientBottom != null)
        {
            var d = FontGradient.Default;
            return new FontGradient(label.GradientTop ?? d.Top, label.GradientBottom ?? d.Bottom);
        }

        return null;
    }

    /// <summary>A label's outline: its own, none, or null to keep the font's</summary>
    private static FontOutline? LabelOutline(Label label)
    {
        if (label.Outline == false)
            return FontOutline.None;

        if (label.Outline == true || label.OutlineColor != null || label.OutlineThickness != null)
        {
            var d = FontOutline.Default;
            return new FontOutline(label.OutlineColor ?? d.Color, Math.Max(label.OutlineThickness ?? d.Thickness, 1));
        }

        return null;
    }

    /// <summary>A label's glow: its own, none, or null to keep the font's</summary>
    private static FontGlow? LabelGlow(Label label)
    {
        if (label.Glow == false)
            return FontGlow.None;

        if (label.Glow == true || label.GlowColor != null || label.GlowRadius != null || label.GlowStrength != null)
        {
            var d = FontGlow.Default;
            return new FontGlow(label.GlowColor ?? d.Color, Math.Max(label.GlowRadius ?? d.Radius, 1),
                Math.Clamp(label.GlowStrength ?? d.Strength, 0, 100));
        }

        return null;
    }

    private static void DrawLabel(Label label)
    {
        var language = _assetManager.GetText("en-us");
        var style = new TextStyle(label.Font, label.Color, Shadow: LabelShadow(label), Gradient: LabelGradient(label),
            Outline: LabelOutline(label), Glow: LabelGlow(label));

        var text = label.Text.ToLanguageText(language);
        if (label.HorizontalOrientation == HorizontalOrientation.Center && label.Width > 0)
            CenteredText(label.X, label.Width, label.Y, style).CPrint(text);
        else if (label.HorizontalOrientation == HorizontalOrientation.Center)
            CenteredText(0, 320, label.Y, style).CPrint(text);
        else
            TextAt(label.X, label.Y, style).Print(text);
    }

    private static Func<int, int>? MapFunction(string menuName, MenuSwitcher? mi)
    {
        // This list will be built with attributes or scripting on the pk3
        List<Func<int, int>> avaiableFunctions = 
            [
            CP_NewGame,
            CP_Multiplayer,
            CP_Options,
            CP_Video,
            CP_Mods,
            CP_Sound,
            CP_Control,
            CP_ControllerSettings,
            CP_LoadGame,
            CP_SaveGame,
            CP_ChangeView,
            CP_ReadThis,
            CP_ViewScores,
            CP_EndGame,
            CP_Quit,
            MouseSensitivity,
            CustomControls
        ];

        var funcDict = avaiableFunctions.ToDictionary(f => f.Method.Name, f => f);

        if (mi == null || string.IsNullOrEmpty(mi.Action))
            return null;

        // A page of presenter text (Blake Stone's instructions, story and ordering info)
        if (mi.Action == nameof(CP_TextScreen))
        {
            var script = mi.Script;
            if (string.IsNullOrEmpty(script))
            {
                Console.WriteLine($"Menu '{menuName}': item '{mi.Id ?? mi.Text}' is a CP_TextScreen without a script");
                return null;
            }
            return _ => CP_TextScreen(script);
        }

        if (!funcDict.TryGetValue(mi.Action, out var func))
        {
            Console.WriteLine($"Menu '{menuName}': unknown action '{mi.Action}' on item '{mi.Id ?? mi.Text}'");
            return null;
        }

        return func;
    }
}
