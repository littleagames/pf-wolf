using CommandLine;
using SDL2;
using Wolf3D.Assets;
using Wolf3D.Entities;
using Wolf3D.Extensions;
using Wolf3D.Managers;

namespace Wolf3D;

internal struct CustomCtrls
{
    public short[] allowed;

    public CustomCtrls(short n0, short n1, short n2, short n3)
    {
        allowed = [n0, n1, n2, n3];
    }
};

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

    // Width of the slot window in menudefs/load-game and save-game; slot outlines are sized from it
    internal const int LSM_W = 175;

    // Binding columns on the customize screen; the column labels in menudefs/customize line up with these
    internal const int CST_START = 60;
    internal const int CST_SPC = 60;

    internal static string MENUSONG => "WONDERIN";
//#if SPEAR
//    internal static string INTROSONG => musicnames.XTOWER2_MUS;
//#else
    internal static string INTROSONG => "NAZI_NOR";
//#endif

    internal static int SENSITIVE = 60;

    internal static CP_itemtype[] MainMenu = [];
    internal static CP_itemtype[] SndMenu = [];
    internal static CP_itemtype[] CtlMenu = [];
    internal static CP_itemtype[] NewEmenu = [];
    internal static CP_itemtype[] NewMenu = [];

    internal static CP_itemtype[] LSMenu = [];

    internal static CP_itemtype[] CusMenu = [];

    internal static CP_itemtype[] MusicMenu = [];
    // The jukebox shows one page of songs at a time
    internal const int JukeboxPageSize = 6;

    // Loaded from menudefs/ in CheckForEpisodes
    internal static CP_iteminfo MainItems;
    internal static CP_iteminfo SndItems;
    internal static CP_iteminfo LSItems;
    internal static CP_iteminfo CtlItems;
    internal static CP_iteminfo CusItems;
    internal static CP_iteminfo NewEitems;
    internal static CP_iteminfo NewItems;
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

    internal static int[] SaveGamesAvail = new int[10] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    internal static int StartGame;
    internal static int SoundStatus = 1;
    internal static int pickquick;
    internal static string[] SaveGameNames = new string[10] {
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
        "",
    };

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
        _videoManager.Bar(0, 0, 320, 200, "BORDCOLOR");
    }

    internal static void DrawStripes(int y)
    {
        _videoManager.Bar(0, y, 320, 24, "Black");
        _videoManager.HorizontalLine(0, 319, y + 22, "STRIPE");
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

//#if SPEAR
 //   internal static void MenuFadeOut() => VL_FadeOut(0, 255, 0, 0, 51, 10);
//#else
    internal static void MenuFadeOut() => _videoManager.FadeOut(0, 255, 43, 0, 0, 10);
//#endif
    internal static void MenuFadeIn() => _videoManager.FadeIn(10);

    internal static void DrawMenu(CP_iteminfo item_i, CP_itemtype[] items)
    {
        int i, which = item_i.curpos;

        WindowX = PrintX = (ushort)(item_i.x + item_i.indent);
        WindowY = PrintY = (ushort)item_i.y;
        WindowW = 320;
        WindowH = 200;

        for (i = 0; i < item_i.amount; i++)
        {
            SetTextColor(items[i], which == i);

            PrintY = (ushort)(item_i.y + i * 13);
            if (items[i].active > 0)
                US_Print((items[i]).text);
            else
            {
                SETFONTCOLOR("DEACTIVE", "BKGDCOLOR");
                US_Print((items[i]).text);
                SETFONTCOLOR("TEXTCOLOR", "BKGDCOLOR");
            }

            US_Print("\n");
        }
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

    internal static int HandleMenu(CP_iteminfo item_i, CP_itemtype[] items, Action<int>? routine)
    {
        char key;
        int i, x, y, basey, exit, which;
        string shape;
        int lastBlinkTime, timer;
        ControlInfo ci;

        which = item_i.curpos;
        x = item_i.x & -8;
        basey = item_i.y - 2;
        y = basey + which * 13;

        _graphicManager.DrawPic("c_cursor1", x, y);
        SetTextColor(items[which], true);
        if (redrawitem != 0)
        {
            PrintX = (ushort)(item_i.x + item_i.indent);
            PrintY = (ushort)(item_i.y + which * 13);
            US_Print((items[which].text));
        }
        //
        // CALL CUSTOM ROUTINE IF IT IS NEEDED
        //
        routine?.Invoke(which);

        _videoManager.Update();

        shape = "c_cursor1";
        timer = 8;
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
                routine?.Invoke(which);
                _videoManager.Update();
            }
            else
                GameEngineManager.DelayMs(5);

            CheckPause();

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
                    if (which != 0 && items[which - 1].active != 0)
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
                    if (which != item_i.amount - 1 && items[which + 1].active != 0)
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
            }

            if (ci.button0 || _inputManager.IsKeyDown(ScanCodes.sc_Space) || _inputManager.IsKeyDown(ScanCodes.sc_Enter))
                exit = 1;

            if (ci.button1 && !_inputManager.IsKeyDown(ScanCodes.sc_Alt) || _inputManager.IsKeyDown(ScanCodes.sc_Escape))
                exit = 2;

        }
        while (exit == 0);
        _inputManager.ClearKeysDown();

        //
        // ERASE EVERYTHING
        //
        if (lastitem != which)
        {
            _videoManager.Bar(x - 1, y, 25, 16, "BKGDCOLOR");
            PrintX = (ushort)(item_i.x + item_i.indent);
            PrintY = (ushort)(item_i.y + which * 13);
            US_Print(items[which].text);
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
        _videoManager.Bar(x - 1, y, 25, 16, "BKGDCOLOR");
        SetTextColor(items[which], false);

        PrintX = (ushort)(item_i.x + item_i.indent);
        PrintY = (ushort)(item_i.y + which * 13);
        US_Print(items[which].text);
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
        _videoManager.Bar(x - 1, y, 25, 16, "BKGDCOLOR");
        y = basey + which * 13;
        _graphicManager.DrawPic("c_cursor1", x, y);
        SetTextColor(items[which], true);

        PrintX = (ushort)(item_i.x + item_i.indent);
        PrintY = (ushort)(item_i.y + which * 13);
        US_Print(items[which].text);

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

    internal static void SetTextColor(CP_itemtype items, bool hlight)
    {
        if (hlight)
        {
            SETFONTCOLOR(color_hlite[items.active], "BKGDCOLOR");
        }
        else
        {
            SETFONTCOLOR(color_norml[items.active], "BKGDCOLOR");
        }
    }

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
        SETFONTCOLOR("TEXTCOLOR", "BKGDCOLOR");
        fontnumber = "LargeFont";
        WindowH = 200;
        if (_videoManager.screenHeight % 200 != 0)
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
                    _videoManager.FadeOut(0, 255, 0, 0, 0, 10);
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
                WindowH = 160;
                if (Confirm("$ENDGAMESTR".ToLanguageText(language)) != 0)
                {
                    playstate = playstatetypes.ex_died;
                    LastAttacker = null;
                    pickquick = gamestate.lives = 0;
                }

                WindowH = 200;
                fontnumber = "SmallFont";
                FindMenuItem(MainMenu, "savegame")?.active = 0;
                return 1;
            //
            // QUICKSAVE
            //
            case ScanCodes.sc_F8:
                if (SaveGamesAvail[LSItems.curpos] != 0 && pickquick != 0)
                {
                    fontnumber = "LargeFont";
                    Message("$STR_SAVING".ToLanguageText(language) + "...");
                    CP_SaveGame(1);
                    fontnumber = "SmallFont";
                }
                else
                {
                    _videoManager.FadeOut();
                    if (_videoManager.screenHeight % 200 != 0)
                        _videoManager.ClearScreen(0);

                    lastgamemusicoffset = StartCPMusic(MENUSONG);
                    pickquick = CP_SaveGame(0);

                    SETFONTCOLOR("Black", "White");
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
            // QUICKLOAD
            //
            case ScanCodes.sc_F9:
                if (SaveGamesAvail[LSItems.curpos] != 0 && pickquick != 0)
                {
                    fontnumber = "LargeFont";

                    var str = $"{"$STR_LGC".ToLanguageText(language)} {SaveGameNames[LSItems.curpos]}\"?";

                    if (Confirm(str) != 0)
                        CP_LoadGame(1);

                    fontnumber = "SmallFont";
                }
                else
                {
                    _videoManager.FadeOut();
                    if (_videoManager.screenHeight % 200 != 0)
                        _videoManager.ClearScreen(0);

                    lastgamemusicoffset = StartCPMusic(MENUSONG);
                    pickquick = CP_LoadGame(0);    // loads lastgamemusicoffs

                    SETFONTCOLOR("Black", "White");
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
                WindowX = WindowY = 0;
                WindowW = 320;
                WindowH = 160;
                string endStr = gameInfo.EndStrings[(US_RndT() & (gameInfo.EndStrings.Count - 2)) + (US_RndT() & 1)];
                if (Confirm(endStr) != 0)
                {
                    _videoManager.Update();
                    _audioManager.SetPaused(true);
                    _audioManager.StopAll();
                    MenuFadeOut();

                    _gameEngineManager.Quit("");
                }

                DrawPlayBorder();
                WindowH = 200;
                fontnumber = "SmallFont";
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
    firstpart:
        DrawNewEpisode();
        do
        {
            which = HandleMenu(NewEitems, NewEmenu, null);
            switch (which)
            {
                case -1:
                    MenuFadeOut();
                    return 0;

                default:
                    episodeInfo = (EpisodeInfo?)NewEmenu[which].data;

                    if (episodeInfo == null)
                    {
                        _audioManager.Play("player/usefail");
                        Message("Episode unavailable!");
                        _inputManager.ClearKeysDown();
                        _inputManager.Ack();
                        DrawNewEpisode();
                        which = 0;
                    }
                    else
                    {
                        var gameInfo = _gameEngineManager.GetGameInfo();
                        if (!gameInfo.Maps.TryGetValue(episodeInfo.StartMap, out mapInfo))
                        {
                            _audioManager.Play("player/usefail");
                            Message($"Starting Map \"{episodeInfo.StartMap}\" unavailable!");
                            _inputManager.ClearKeysDown();
                            _inputManager.Ack();
                            DrawNewEpisode();
                            which = 0;
                        }
                        which = 1;
                    }
                    break;
            }

        }
        while (which == 0);

        ShootSnd();

        //
        // ALREADY IN A GAME?
        //
        if (ingame)
            if (Confirm($"$CURGAME".ToLanguageText(language)) == 0)
            {
                MenuFadeOut();
                return 0;
            }

        MenuFadeOut();
        DrawNewGame();
        which = HandleMenu(NewItems, NewMenu, DrawNewGameDiff);
        if (which < 0)
        {
            MenuFadeOut();
            goto firstpart;
        }

        ShootSnd();

        NewGame((difficultytypes)which, episodeInfo, mapInfo);
        StartGame = 1;
        MenuFadeOut();

        //
        // CHANGE "READ THIS!" TO NORMAL COLOR
        //
        FindMenuItem(MainMenu, "readthis")?.active = 1;
        pickquick = 0;

        return 0;
    }

    internal static void DrawNewEpisode()
    {
        DrawMenuComponents("new-episode");
        SETFONTCOLOR("TEXTCOLOR", "BKGDCOLOR");
        DrawMenu(NewEitems, NewEmenu);

        // Each episode's picture (pic-name in game-info) sits between the cursor and the name
        for (int i = 0; i < NewEmenu.Length; i++)
        {
            if (NewEmenu[i].data is EpisodeInfo episode && !string.IsNullOrEmpty(episode.PicName))
                _graphicManager.DrawPic(episode.PicName, NewEitems.x + 32, NewEitems.y + i * 13);
        }

        _videoManager.Update();
        MenuFadeIn();
        WaitKeyUp();
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
        // Face picture for the highlighted skill (pic-name in game-info)
        if (w >= 0 && w < NewMenu.Length && NewMenu[w].data is SkillInfo skill)
            _graphicManager.DrawPic(skill.PicName, NewItems.x + 185, NewItems.y + 7);
    }

    internal static int CP_Sound(int _)
    {
        int which;

        DrawSoundMenu();
        MenuFadeIn();
        WaitKeyUp();

        do
        {
            which = HandleMenu(SndItems, SndMenu, null);
            //
            // HANDLE MENU CHOICES
            //
            switch (SelectedId(SndMenu, which))
            {
                //
                // SOUND EFFECTS
                //
                case "sfx-none":
                   // if (_audioManager.SoundMode != SDMode.Off)
                    {
                    //    _audioManager.SD_WaitSoundDone();
                    //    _audioManager.SetSoundMode(SDMode.Off);
                        DrawSoundMenu();
                    }
                    break;
                case "sfx-pc":
                   // if (_audioManager.SoundMode != SDMode.PC)
                    {
                    //    _audioManager.SD_WaitSoundDone();
                    //    _audioManager.SetSoundMode(SDMode.PC);
                        //CA_LoadAllSounds();
                        DrawSoundMenu();
                        ShootSnd();
                    }
                    break;
                case "sfx-adlib":
                   // if (_audioManager.SoundMode != SDMode.AdLib)
                    {
                    //    _audioManager.SD_WaitSoundDone();
                    //    _audioManager.SetSoundMode(SDMode.AdLib);
                        //CA_LoadAllSounds();
                        DrawSoundMenu();
                        ShootSnd();
                    }
                    break;

                //
                // DIGITIZED SOUND
                //
                case "digi-none":
                  //  if (_audioManager.DigiMode != (byte)SDSMode.Off)
                    {
                    //    _audioManager.SetDigiDevice((byte)SDSMode.Off);
                        DrawSoundMenu();
                    }
                    break;
                case "digi-soundsource":
                    /*                if (DigiMode != sds_SoundSource)
                                    {
                                        SD_SetDigiDevice (sds_SoundSource);
                                        DrawSoundMenu ();
                                        ShootSnd ();
                                    }*/
                    break;
                case "digi-soundblaster":
                   // if (_audioManager.DigiMode != SDSMode.SoundBlaster)
                    {
                   //     _audioManager.SetDigiDevice(SDSMode.SoundBlaster);
                        DrawSoundMenu();
                        ShootSnd();
                    }
                    break;

                //
                // MUSIC
                //
                case "music-none":
                    //if (_audioManager.MusicMode != SMMode.Off)
                    {
                   //     _audioManager.SetMusicMode(SMMode.Off);
                        DrawSoundMenu();
                        ShootSnd();
                    }
                    break;
                case "music-adlib":
                    //if (_audioManager.MusicMode != SMMode.AdLib)
                    {
                    //    _audioManager.SetMusicMode(SMMode.AdLib);
                        DrawSoundMenu();
                        ShootSnd();
                        StartCPMusic(MENUSONG);
                    }
                    break;
            }
        }
        while (which >= 0);

        MenuFadeOut();

        return 0;
    }

    internal static void DrawSoundMenu()
    {
        int i, on;

        //
        // DRAW SOUND MENU
        //
        DrawMenuComponents("sound");

        //
        // IF NO ADLIB, NON-CHOOSENESS!
        //
        //if (!_audioManager.AdLibPresent && !_audioManager.SoundBlasterPresent)
       // {
       //     SndMenu[2].active = SndMenu[10].active = SndMenu[11].active = 0;
        //}

       // if (!_audioManager.SoundBlasterPresent)
       //     SndMenu[7].active = 0;

        //if (!_audioManager.SoundBlasterPresent)
        //    SndMenu[5].active = 0;

        DrawMenu(SndItems, SndMenu);
        for (i = 0; i < SndItems.amount; i++)
            if (SndMenu[i].text != string.Empty)
            {
                //
                // DRAW SELECTED/NOT SELECTED GRAPHIC BUTTONS
                //
                on = 0;
                switch (SndMenu[i].id)
                {
                    //
                    // SOUND EFFECTS
                    //
                    case "sfx-none":
                        //if (_audioManager.SoundMode == SDMode.Off)
                            on = 1;
                        break;
                    case "sfx-pc":
                        //if (_audioManager.SoundMode == SDMode.PC)
                            on = 1;
                        break;
                    case "sfx-adlib":
                        //if (_audioManager.SoundMode == SDMode.AdLib)
                            on = 1;
                        break;

                    //
                    // DIGITIZED SOUND
                    //
                    case "digi-none":
                        //if (_audioManager.DigiMode == SDSMode.Off)
                            on = 1;
                        break;
                    case "digi-soundsource":
                        //                    if (DigiMode == sds_SoundSource)
                        //                        on = 1;
                        break;
                    case "digi-soundblaster":
                       // if (_audioManager.DigiMode == SDSMode.SoundBlaster)
                            on = 1;
                        break;

                    //
                    // MUSIC
                    //
                    case "music-none":
                        //if (_audioManager.MusicMode == SMMode.Off)
                            on = 1;
                        break;
                    case "music-adlib":
                       // if (_audioManager.MusicMode == SMMode.AdLib)
                            on = 1;
                        break;
                }

                int x = SndItems.x + 24;
                int y = SndItems.y + i * 13 + 2;
                if (on != 0)
                    _graphicManager.DrawPic("c_selected", x, y);
                else
                    _graphicManager.DrawPic("c_notselected", x, y);
            }

        DrawMenuGun(SndItems);
        _videoManager.Update();
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
                    CusItems.curpos = -1;
                    ShootSnd();
                    break;

                case "joystick-enabled":
                    joystickenabled ^= true;
                    DrawCtlScreen();
                    CusItems.curpos = -1;
                    ShootSnd();
                    break;

                case "mouse-sensitivity":
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

        WindowX = 0;
        WindowW = 320;
        SETFONTCOLOR("TEXTCOLOR", "BKGDCOLOR");

        var mouseEnabledItem = FindMenuItem(CtlMenu, "mouse-enabled");
        var mouseSensItem = FindMenuItem(CtlMenu, "mouse-sensitivity");

        if (_inputManager.JoyPresent())
            FindMenuItem(CtlMenu, "joystick-enabled")?.active = 1;

        if (_inputManager.IsMousePresent())
        {
            mouseEnabledItem?.active = 1;
        }

        mouseSensItem?.active = (short)(mouseenabled ? 1 : 0);


        DrawMenu(CtlItems, CtlMenu);

        DrawCtlCheckbox("mouse-enabled", mouseenabled);
        DrawCtlCheckbox("joystick-enabled", joystickenabled);

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

    private static void DrawCtlCheckbox(string id, bool on)
    {
        int index = Array.FindIndex(CtlMenu, item => item.id == id);
        if (index < 0)
            return;

        int x = CtlItems.x + CtlItems.indent - 24;
        int y = CtlItems.y + index * 13 + 3;
        _graphicManager.DrawPic(on ? "c_selected" : "c_notselected", x, y);
    }

    internal static int CP_LoadGame(int quick)
    {
        int which, exit = 0;

        //
        // QUICKLOAD?
        //
        if (quick != 0)
        {
            which = LSItems.curpos;

            if (SaveGamesAvail[which] != 0)
            {
                // Loaded in place: play carries straight on in the restored level.
                loadedgame = true;
                var loaded = LoadTheGame(GetSaveGamePath(which), 0, 0);
                loadedgame = false;
                if (!loaded)
                    return 0;

                if (viewsize != 21)
                    DrawPlayScreen();
                ContinueMusic(lastgamemusicoffset);
                return 1;
            }
        }

        DrawLoadSaveScreen(0);

        do
        {
            which = HandleMenu(LSItems, LSMenu, TrackWhichGame);
            if (which >= 0 && SaveGamesAvail[which] != 0)
            {
                ShootSnd();

                DrawLSAction(0);
                loadedgame = true;

                if (!LoadTheGame(GetSaveGamePath(which), LSA_X + 8, LSA_Y + 5))
                {
                    loadedgame = false;
                    DrawLoadSaveScreen(0);
                    continue;
                }

                StartGame = 1;
                ShootSnd();
                //
                // CHANGE "READ THIS!" TO NORMAL COLOR
                //
                FindMenuItem(MainMenu, "readthis")?.active = 1;
                exit = 1;
                break;
            }

        }
        while (which >= 0);

        MenuFadeOut();

        return exit;
    }

    internal static void DrawLoadSaveScreen(int loadsave)
    {
        int i;

        fontnumber = "LargeFont";
        DrawMenuComponents(loadsave == 0 ? "load-game" : "save-game");

        for (i = 0; i < LSMenu.Length; i++)
            PrintLSEntry(i, "TEXTCOLOR");

        DrawMenu(LSItems, LSMenu);
        _videoManager.Update();
        MenuFadeIn();
        WaitKeyUp();
    }

    // Slot highlighted last, so it can be un-highlighted when the cursor moves
    private static int lastgameon = 0;

    internal static void TrackWhichGame (int w)
    {
        PrintLSEntry(lastgameon, "TEXTCOLOR");
        PrintLSEntry(w, "HIGHLIGHT");

        lastgameon = w;
    }

    internal static void PrintLSEntry(int w, string color)
    {
        var language = _assetManager.GetText("en-us");
        SETFONTCOLOR(color, "BKGDCOLOR");
        DrawOutline(LSItems.x + LSItems.indent, LSItems.y + w * 13, LSM_W - LSItems.indent - 15, 11, color,
                     color);
        PrintX = (ushort)(LSItems.x + LSItems.indent + 2);
        PrintY = (ushort)(LSItems.y + w * 13 + 1);
        fontnumber = "SmallFont";

        if (SaveGamesAvail[w] != 0)
            US_Print(new string(SaveGameNames[w]));
        else
            US_Print($"      - {"$STR_EMPTY".ToLanguageText(language)} -");

        fontnumber = "LargeFont";
    }

    internal const int LSA_X = 96;
    internal const int LSA_Y = 80;
    internal const int LSA_W = 130;
    internal const int LSA_H = 42;

    internal static void DrawLSAction(int which)
    {
        var language = _assetManager.GetText("en-us");
        DrawWindow(LSA_X, LSA_Y, LSA_W, LSA_H, "TEXTCOLOR");
        DrawOutline(LSA_X, LSA_Y, LSA_W, LSA_H, "Black", "HIGHLIGHT");
        _graphicManager.DrawPic("c_diskloading1", LSA_X + 8, LSA_Y + 5);

        fontnumber = "LargeFont";
        SETFONTCOLOR("Black", "TEXTCOLOR");
        PrintX = LSA_X + 46;
        PrintY = LSA_Y + 13;

        if (which == 0)
            US_Print("$STR_LOADING".ToLanguageText(language) + "...");
        else
            US_Print("$STR_SAVING".ToLanguageText(language) + "...");

        _videoManager.Update();
    }

    internal static int CP_SaveGame(int quick)
    {
        var language = _assetManager.GetText("en-us");
        int which, exit = 0;
        string input = "";

        //
        // QUICKSAVE?
        //
        if (quick != 0)
        {
            which = LSItems.curpos;

            if (SaveGamesAvail[which] != 0)
            {
                if (!SaveTheGame(GetSaveGamePath(which), SaveGameNames[which], 0, 0))
                    ShowSaveFailed();
                return 1;
            }
        }

        DrawLoadSaveScreen(1);
        do
        {
            which = HandleMenu(LSItems, LSMenu, TrackWhichGame);
            if (which >= 0)
            {
                //
                // OVERWRITE EXISTING SAVEGAME?
                //
                if (SaveGamesAvail[which] != 0)
                {
                    if (Confirm("$GAMESVD".ToLanguageText(language)) == 0)
                    {
                        DrawLoadSaveScreen(1);
                        continue;
                    }
                    else
                    {
                        DrawLoadSaveScreen(1);
                        PrintLSEntry(which, "HIGHLIGHT");
                        _videoManager.Update();
                    }
                }

                ShootSnd();

                input = SaveGameNames[which];

                fontnumber = "SmallFont";
                if (SaveGamesAvail[which] == 0)
                    _videoManager.Bar(LSItems.x + LSItems.indent + 1, LSItems.y + which * 13 + 1,
                             LSM_W - LSItems.indent - 16, 10, "BKGDCOLOR");
                _videoManager.Update();

                if (US_LineInput
                    (LSItems.x + LSItems.indent + 2, LSItems.y + which * 13 + 1, ref input, input, true, 31,
                     LSM_W - LSItems.indent - 30))
                {
                    DrawLSAction(1);
                    if (!SaveTheGame(GetSaveGamePath(which), input, LSA_X + 8, LSA_Y + 5))
                    {
                        ShowSaveFailed();
                        DrawLoadSaveScreen(1);
                        continue;
                    }

                    SaveGamesAvail[which] = 1;
                    SaveGameNames[which] = input;
                    ShootSnd();
                    exit = 1;
                }
                else
                {
                    _videoManager.Bar(LSItems.x + LSItems.indent + 1, LSItems.y + which * 13 + 1,
                             LSM_W - LSItems.indent - 16, 10, "BKGDCOLOR");
                    PrintLSEntry(which, "HIGHLIGHT");
                    _videoManager.Update();
                    _audioManager.Play("menu/escape");
                    continue;
                }

                fontnumber = "LargeFont";
                break;
            }

        }
        while (which >= 0);

        MenuFadeOut();

        return exit;
    }

    internal static int CP_ChangeView(int _)
    {
        var language = _assetManager.GetText("en-us");
        int exit = 0, oldview, newview;
        ControlInfo ci;

        WindowX = WindowY = 0;
        WindowW = 320;
        WindowH = 200;
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
                if (_videoManager.screenHeight % 200 != 0)
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
        if (_videoManager.screenHeight % 200 != 0)
            _videoManager.ClearScreen(0);

        return 0;
    }

    internal static void DrawChangeView(int view)
    {
        var language = _assetManager.GetText("en-us");
        int rescaledHeight = _videoManager.screenHeight / _videoManager.scaleFactor;
        if (view != 21) _videoManager.Bar(0, rescaledHeight - 40, 320, 40, bordercol);

        ShowViewSize(view);

        PrintY = (ushort)((_videoManager.screenHeight / _videoManager.scaleFactor) - 39);
        WindowX = 0;
        WindowY = 320;                                  // TODO: Check this!
        SETFONTCOLOR("HIGHLIGHT", "BKGDCOLOR");

        US_CPrint("$STR_SIZE1".ToLanguageText(language) +"\n");
        US_CPrint("$STR_SIZE2".ToLanguageText(language) +"\n");
        US_CPrint("$STR_SIZE3".ToLanguageText(language));
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
        fontnumber = "SmallFont";

//#if SPEAR
//        StartCPMusic(musicnames.XAWARD_MUS);
//#else
        StartCPMusic("ROSTER");
//#endif

        DrawHighScores();
        _videoManager.Update();
        MenuFadeIn();
        fontnumber = "LargeFont";

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

        pickquick = gamestate.lives = 0;
        playstate = playstatetypes.ex_died;
        LastAttacker = null;

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

    ////////////////////////////////////////////////////////////////////
    //
    // CUSTOMIZE CONTROLS
    //
    ////////////////////////////////////////////////////////////////////

    internal enum CustomCtlOptions { MOUSE, JOYSTICK, KEYBOARDBTNS, KEYBOARDMOVE };        // FOR INPUT TYPES
    internal enum CustomCtlActions : byte { FIRE, STRAFE, RUN, OPEN };
    enum CustomCtlMove : byte { FWRD, RIGHT, BKWD, LEFT };
    static int[] moveorder = { (byte)CustomCtlMove.LEFT, (byte)CustomCtlMove.RIGHT, (byte)CustomCtlMove.FWRD, (byte)CustomCtlMove.BKWD };
    static string[] mbarray = { "b0", "b1", "b2", "b3" };
    static byte[] order = { (byte)CustomCtlActions.RUN, (byte)CustomCtlActions.OPEN, (byte)CustomCtlActions.FIRE, (byte)CustomCtlActions.STRAFE };
    internal static int CustomControls(int _)
    {
        int which;

        DrawCustomScreen();
        do
        {
            which = HandleMenu(CusItems, CusMenu, FixupCustom);
            switch (SelectedId(CusMenu, which))
            {
                case "mouse":
                    DefineMouseBtns();
                    DrawCustMouse(1);
                    break;
                case "joystick":
                    DefineJoyBtns();
                    DrawCustJoy(0);
                    break;
                case "keyboard":
                    DefineKeyBtns();
                    DrawCustKeybd(0);
                    break;
                case "keyboard-move":
                    DefineKeyMove();
                    DrawCustKeys(0);
                    break;
            }
        }
        while (which >= 0);

        MenuFadeOut();

        return 0;
    }

    /// <summary>
    /// Screen Y of a binding row on the customize screen; each row sits on its menu item
    /// </summary>
    private static int CustomRowY(string id)
    {
        int index = Array.FindIndex(CusMenu, item => item.id == id);
        return CusItems.y + Math.Max(index, 0) * 13;
    }

    /// <summary>
    /// Redraws the binding row for a customize menu item
    /// </summary>
    private static void DrawCustomRow(string? id, int hilight)
    {
        switch (id)
        {
            case "mouse":
                DrawCustMouse(hilight);
                break;
            case "joystick":
                DrawCustJoy(hilight);
                break;
            case "keyboard":
                DrawCustKeybd(hilight);
                break;
            case "keyboard-move":
                DrawCustKeys(hilight);
                break;
        }
    }

    ////////////////////////
    //
    // DEFINE THE MOUSE BUTTONS
    //
    internal static void
    DefineMouseBtns()
    {
        CustomCtrls mouseallowed = new( 0, 1, 1, 1 );
        EnterCtrlData(CustomRowY("mouse"), ref mouseallowed, DrawCustMouse, PrintCustMouse, CustomCtlOptions.MOUSE);
    }


    ////////////////////////
    //
    // DEFINE THE JOYSTICK BUTTONS
    //
    internal static void
    DefineJoyBtns()
    {
        CustomCtrls joyallowed = new(1, 1, 1, 1);
        EnterCtrlData(CustomRowY("joystick"), ref joyallowed, DrawCustJoy, PrintCustJoy, CustomCtlOptions.JOYSTICK);
    }


    ////////////////////////
    //
    // DEFINE THE KEYBOARD BUTTONS
    //
    internal static void
    DefineKeyBtns()
    {
        CustomCtrls keyallowed = new(1, 1, 1, 1);
        EnterCtrlData(CustomRowY("keyboard"), ref keyallowed, DrawCustKeybd, PrintCustKeybd, CustomCtlOptions.KEYBOARDBTNS);
    }


    ////////////////////////
    //
    // DEFINE THE KEYBOARD BUTTONS
    //
    internal static void
    DefineKeyMove()
    {
        CustomCtrls keyallowed = new( 1, 1, 1, 1 );
        EnterCtrlData(CustomRowY("keyboard-move"), ref keyallowed, DrawCustKeys, PrintCustKeys, CustomCtlOptions.KEYBOARDMOVE);
    }

    internal static void EnterCtrlData(int rowY, ref CustomCtrls cust, Action<int> DrawRtn, Action<int> PrintRtn,
                   CustomCtlOptions type)
    {
        int j, z, exit, tick, redraw, which = 0, x = 0, picked, lastFlashTime;
        ControlInfo ci;


        ShootSnd();
        PrintY = (ushort)rowY;
        _inputManager.ClearKeysDown();
        exit = 0;
        redraw = 1;
        //
        // FIND FIRST SPOT IN ALLOWED ARRAY
        //
        for (j = 0; j < 4; j++)
            if (cust.allowed[j] != 0)
            {
                which = j;
                break;
            }

        do
        {
            if (redraw != 0)
            {
                x = CST_START + CST_SPC * which;
                DrawWindow(5, PrintY - 1, 310, 13, "BKGDCOLOR");

                DrawRtn(1);
                DrawWindow(x - 2, PrintY, CST_SPC, 11, "TEXTCOLOR");
                DrawOutline(x - 2, PrintY, CST_SPC, 11, "Black", "HIGHLIGHT");
                SETFONTCOLOR("Black", "TEXTCOLOR");
                PrintRtn(which);
                PrintX = (ushort)x;
                SETFONTCOLOR("TEXTCOLOR", "BKGDCOLOR");
                _videoManager.Update();
                WaitKeyUp();
                redraw = 0;
            }

            GameEngineManager.DelayMs(5);
            ReadAnyControl(out ci);

            if (type == CustomCtlOptions.MOUSE || type == CustomCtlOptions.JOYSTICK)
                if (_inputManager.IsKeyDown(ScanCodes.sc_Enter) || _inputManager.IsKeyDown(ScanCodes.sc_Control) || _inputManager.IsKeyDown(ScanCodes.sc_Alt))
                {
                    _inputManager.ClearKeysDown();
                    ci.button0 = ci.button1 = false;
                }

            //
            // CHANGE BUTTON VALUE?
            //
            if ((type != CustomCtlOptions.KEYBOARDBTNS && type != CustomCtlOptions.KEYBOARDMOVE) && (ci.button0 || ci.button1 || ci.button2 || ci.button3) ||
                ((type == CustomCtlOptions.KEYBOARDBTNS || type == CustomCtlOptions.KEYBOARDMOVE) && _inputManager.GetLastKeyPressed() == ScanCodes.sc_Enter))
            {
                lastFlashTime = (int)GameEngineManager.GetTimeCount();
                tick = picked = 0;
                SETFONTCOLOR("Black", "TEXTCOLOR");

                if (type == CustomCtlOptions.KEYBOARDBTNS || type == CustomCtlOptions.KEYBOARDMOVE)
                    _inputManager.ClearKeysDown();

                while (true)
                {
                    int button, result = 0;

                    //
                    // FLASH CURSOR
                    //
                    if (GameEngineManager.GetTimeCount() - lastFlashTime > 10)
                    {
                        switch (tick)
                        {
                            case 0:
                                _videoManager.Bar(x, PrintY + 1, CST_SPC - 2, 10, "TEXTCOLOR");
                                break;
                            case 1:
                                PrintX = (ushort)x;
                                US_Print("?");
                                _audioManager.Play("world/hitwall");
                                break;
                        }
                        tick ^= 1;
                        lastFlashTime = (int)GameEngineManager.GetTimeCount();
                        _videoManager.Update();
                    }
                    else GameEngineManager.DelayMs(5);

                    //
                    // WHICH TYPE OF INPUT DO WE PROCESS?
                    //
                    switch (type)
                    {
                        case CustomCtlOptions.MOUSE:
                            button = _inputManager.MouseButtons();
                            switch (button)
                            {
                                case 1:
                                    result = 1;
                                    break;
                                case 2:
                                    result = 2;
                                    break;
                                case 4:
                                    result = 3;
                                    break;
                            }

                            if (result != 0)
                            {
                                for (z = 0; z < 4; z++)
                                    if (order[which] == (byte)buttonmouse[z])
                                    {
                                        buttonmouse[z] = buttontypes.bt_nobutton;
                                        break;
                                    }

                                buttonmouse[result - 1] = (buttontypes)order[which];
                                picked = 1;
                                ShootSnd();
                            }
                            break;

                        case CustomCtlOptions.JOYSTICK:
                            if (ci.button0)
                                result = 1;
                            else if (ci.button1)
                                result = 2;
                            else if (ci.button2)
                                result = 3;
                            else if (ci.button3)
                                result = 4;

                            if (result != 0)
                            {
                                for (z = 0; z < 4; z++)
                                {
                                    if (order[which] == (byte)buttonjoy[z])
                                    {
                                        buttonjoy[z] = buttontypes.bt_nobutton;
                                        break;
                                    }
                                }

                                buttonjoy[result - 1] = (buttontypes)order[which];
                                picked = 1;
                                ShootSnd();
                            }
                            break;

                        case CustomCtlOptions.KEYBOARDBTNS:
                            if (_inputManager.GetLastKeyPressed() != 0 && _inputManager.GetLastKeyPressed() != ScanCodes.sc_Escape)
                            {
                                buttonscan[order[which]] = _inputManager.GetLastKeyPressed();
                                picked = 1;
                                ShootSnd();
                                _inputManager.ClearKeysDown();
                            }
                            break;

                        case CustomCtlOptions.KEYBOARDMOVE:
                            if (_inputManager.GetLastKeyPressed() != 0 && _inputManager.GetLastKeyPressed() != ScanCodes.sc_Escape)
                            {
                                dirscan[moveorder[which]] = _inputManager.GetLastKeyPressed();
                                picked = 1;
                                ShootSnd();
                                _inputManager.ClearKeysDown();
                            }
                            break;
                    }

                    //
                    // EXIT INPUT?
                    //
                    if (_inputManager.IsKeyDown(ScanCodes.sc_Escape) || type != CustomCtlOptions.JOYSTICK && ci.button1)
                    {
                        picked = 1;
                        _audioManager.Play("menu/escape");
                    }

                    if (picked != 0) break;

                    ReadAnyControl(out ci);
                }

                SETFONTCOLOR("TEXTCOLOR", "BKGDCOLOR");
                redraw = 1;
                WaitKeyUp();
                continue;
            }

            if (ci.button1 || _inputManager.IsKeyDown(ScanCodes.sc_Escape))
                exit = 1;

            //
            // MOVE TO ANOTHER SPOT?
            //
            switch (ci.dir)
            {
                case Direction.West:
                    do
                    {
                        which--;
                        if (which < 0)
                            which = 3;
                    }
                    while (cust.allowed[which] == 0);
                    redraw = 1;
                    _audioManager.Play("menu/move1");
                    do
                    {
                        ReadAnyControl(out ci);
                        GameEngineManager.DelayMs(5);
                    }
                    while (ci.dir != Direction.None);
                    _inputManager.ClearKeysDown();
                    break;

                case Direction.East:
                    do
                    {
                        which++;
                        if (which > 3)
                            which = 0;
                    }
                    while (cust.allowed[which] == 0);
                    redraw = 1;
                    _audioManager.Play("menu/move1");
                    do
                    {
                        ReadAnyControl(out ci);
                        GameEngineManager.DelayMs(5);
                    }
                    while (ci.dir != Direction.None);
                    _inputManager.ClearKeysDown();
                    break;
                case Direction.North:
                case Direction.South:
                    exit = 1;
                    break;
            }
        }
        while (exit == 0);

        _audioManager.Play("menu/escape");
        WaitKeyUp();
        DrawWindow(5, PrintY - 1, 310, 13, "BKGDCOLOR");
    }


    ////////////////////////
    //
    // FIXUP GUN CURSOR OVERDRAW SHIT
    //
    internal static int fixup_lastwhich = -1;
    internal static void FixupCustom(int w)
    {
        int y = CusItems.y + w * 13;


        _videoManager.HorizontalLine(7, 32, y - 1, "DEACTIVE");
        _videoManager.HorizontalLine(7, 32, y + 12, "BORD2COLOR");
        _videoManager.HorizontalLine(7, 32, y - 2, "BORDCOLOR");
        _videoManager.HorizontalLine(7, 32, y + 13, "BORDCOLOR");
        DrawCustomRow(SelectedId(CusMenu, w), 1);


        if (fixup_lastwhich >= 0)
        {
            y = CusItems.y + fixup_lastwhich * 13;
            _videoManager.HorizontalLine(7, 32, y - 1, "DEACTIVE");
            _videoManager.HorizontalLine(7, 32, y + 12, "BORD2COLOR");
            _videoManager.HorizontalLine(7, 32, y - 2, "BORDCOLOR");
            _videoManager.HorizontalLine(7, 32, y + 13, "BORDCOLOR");
            if (fixup_lastwhich != w)
                DrawCustomRow(SelectedId(CusMenu, fixup_lastwhich), 0);
        }

        fixup_lastwhich = w;
    }


    ////////////////////////
    //
    // DRAW CUSTOMIZE SCREEN
    //

    internal static void DrawCustomScreen()
    {
        int i;

        // Title, section headers, column labels and row windows
        DrawMenuComponents("customize");
        WindowX = 0;
        WindowW = 320;

        // The current bindings, one row per menu item
        foreach (var item in CusMenu)
            DrawCustomRow(item.id, 0);
        //
        // PICK STARTING POINT IN MENU
        //
        if (CusItems.curpos < 0)
            for (i = 0; i < CusItems.amount; i++)
                if (CusMenu[i].active != 0)
                {
                    CusItems.curpos = (short)i;
                    break;
                }


        _videoManager.Update();
        MenuFadeIn();
    }

    internal static void
    PrintCustMouse(int i)
    {
        int j;

        for (j = 0; j < 4; j++)
            if (order[i] == (byte)buttonmouse[j])
            {
                PrintX = (ushort)(CST_START + CST_SPC * i);
                US_Print(mbarray[j]);
                break;
            }
    }

    internal static void DrawCustMouse(int hilight)
    {
        int i;
        string color;


        color = "TEXTCOLOR";
        if (hilight != 0)
            color = "HIGHLIGHT";
        SETFONTCOLOR(color, "BKGDCOLOR");

        if (!mouseenabled)
        {
            SETFONTCOLOR("DEACTIVE", "BKGDCOLOR");
            FindMenuItem(CusMenu, "mouse")?.active = 0;
        }
        else
            FindMenuItem(CusMenu, "mouse")?.active = 1;

        PrintY = (ushort)CustomRowY("mouse");
        for (i = 0; i < 4; i++)
            PrintCustMouse(i);
    }

    internal static void PrintCustJoy(int i)
    {
        int j;

        for (j = 0; j < 4; j++)
        {
            if (order[i] == (byte)buttonjoy[j])
            {
                PrintX = (ushort)(CST_START + CST_SPC * i);
                US_Print(mbarray[j]);
                break;
            }
        }
    }

    internal static void DrawCustJoy(int hilight)
    {
        int i;
        string color;

        color = "TEXTCOLOR";
        if (hilight != 0)
            color = "HIGHLIGHT";
        SETFONTCOLOR(color, "BKGDCOLOR");

        if (!joystickenabled)
        {
            SETFONTCOLOR("DEACTIVE", "BKGDCOLOR");
            FindMenuItem(CusMenu, "joystick")?.active = 0;
        }
        else
            FindMenuItem(CusMenu, "joystick")?.active = 1;

        PrintY = (ushort)CustomRowY("joystick");
        for (i = 0; i < 4; i++)
            PrintCustJoy(i);
    }


    internal static void
    PrintCustKeybd(int i)
    {
        PrintX = (ushort)(CST_START + CST_SPC * i);
        US_Print(_inputManager.GetScanName(buttonscan[order[i]]));
    }

    internal static void
    DrawCustKeybd(int hilight)
    {
        int i;
        string color;


        color = "TEXTCOLOR";
        if (hilight != 0)
            color = "HIGHLIGHT";
        SETFONTCOLOR(color, "BKGDCOLOR");

        PrintY = (ushort)CustomRowY("keyboard");
        for (i = 0; i < 4; i++)
            PrintCustKeybd(i);
    }

    internal static void
    PrintCustKeys(int i)
    {
        PrintX = (ushort)(CST_START + CST_SPC * i);
        US_Print(_inputManager.GetScanName(dirscan[moveorder[i]]));
    }

    internal static void DrawCustKeys(int hilight)
    {
        int i;
        string color;


        color = "TEXTCOLOR";
        if (hilight != 0)
            color = "HIGHLIGHT";
        SETFONTCOLOR(color, "BKGDCOLOR");

        PrintY = (ushort)CustomRowY("keyboard-move");
        for (i = 0; i < 4; i++)
            PrintCustKeys(i);
    }
    internal static void CleanupControlPanel()
    {
        fontnumber = "SmallFont";

        // Keep whatever was changed in the menus (view size, controls, sensitivity) even if the
        // game doesn't get to exit cleanly.
        _gameEngineManager.WriteConfig();
    }

    internal static void DrawMenuGun(CP_iteminfo iteminfo)
    {
        int x, y;

        x = iteminfo.x;
        y = iteminfo.y + iteminfo.curpos * 13 - 2;
        _graphicManager.DrawPic("c_cursor1", x, y);
    }

    internal static int Confirm(string text)
    {
        var language = _assetManager.GetText("en-us");
        int xit = 0, x, y, tick = 0, lastBlinkTime;
        string[] whichsnd = ["menu/escape", "menu/activate"];
        ControlInfo ci;

        Message(text.ToLanguageText(language));
        _inputManager.ClearKeysDown();
        WaitKeyUp();

        //
        // BLINK CURSOR
        //
        x = PrintX;
        y = PrintY;
        lastBlinkTime = (int)GameEngineManager.GetTimeCount();

        do
        {
            ReadAnyControl(out ci);

            if (GameEngineManager.GetTimeCount() - lastBlinkTime >= 10)
            {
                switch (tick)
                {
                    case 0:
                        _videoManager.Bar(x, y, 8, 13, "TEXTCOLOR");
                        break;
                    case 1:
                        PrintX = (ushort)x;
                        PrintY = (ushort)y;
                        US_Print("_");
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

    internal static void WaitKeyUp()
    {
        ControlInfo ci;
        bool keyPressed = false;
        while (keyPressed)
        {
            ReadAnyControl(out ci);
            keyPressed =
               ci.button0 ||
               ci.button1 ||
               ci.button2 ||
               ci.button3 ||
               _inputManager.IsKeyDown(ScanCodes.sc_Space) ||
               _inputManager.IsKeyDown(ScanCodes.sc_Enter) ||
               _inputManager.IsKeyDown(ScanCodes.sc_Escape);

            _inputManager.WaitAndProcessEvents();
        }
    }

    internal static void Message(string text)
    {
        int h = 0, w = 0, mw = 0, i, len = text.Length;

        fontnumber = "LargeFont";
        FontAsset font = _assetManager.Find<FontAsset>(fontnumber);
        if (font == null) return;
        h = font.Height;

        for (i = 0; i < len; i++)
        {
            if (text[i] == '\n')
            {
                if (w > mw)
                    mw = w;
                w = 0;
                h += font.Height;
            }
            else
                w += font.Width[(byte)text[i]];
        }

        if (w + 10 > mw)
            mw = w + 10;

        PrintY = (ushort)((WindowH / 2) - (h / 2));
        PrintX = WindowX = (ushort)(160 - (mw / 2));

        DrawWindow(WindowX - 5, PrintY - 5, mw + 10, h + 10, "TEXTCOLOR");
        DrawOutline(WindowX - 5, PrintY - 5, mw + 10, h + 10, "Black", "HIGHLIGHT");
        SETFONTCOLOR("Black", "TEXTCOLOR");
        US_Print(text);
        _videoManager.Update();
    }

    internal static void FreeMusic()
    {
        //UNCACHEAUDIOCHUNK(STARTMUSIC + chunk);
    }

    internal static void IntroScreen()
    {
        //const byte MAINCOLOR = 0x6c;
        //const byte EMSCOLOR = 0x6c; // 0x4f
        //const byte XMSCOLOR = 0x6c; // 0x7f

        const byte FILLCOLOR = 14;

        int i;
        //for (i = 0; i < 10; i++)
        //    _videoManager.Bar(49, 163 - 8 * i, 6, 5, MAINCOLOR - i);
        //for (i = 0; i < 10; i++)
        //    _videoManager.Bar(89, 163 - 8 * i, 6, 5, EMSCOLOR - i);
        //for (i = 0; i < 10; i++)
        //    _videoManager.Bar(129, 163 - 8 * i, 6, 5, XMSCOLOR - i);

        //
        // FILL BOXES
        //
        if (_inputManager.IsMousePresent())
            _videoManager.Bar(164, 82, 12, 2, "FILLCOLOR");

        if (_inputManager.JoyPresent())
            _videoManager.Bar(164, 105, 12, 2, "FILLCOLOR");

        //if (_audioManager.AdLibPresent && !_audioManager.SoundBlasterPresent)
            _videoManager.Bar(164, 128, 12, 2, "FILLCOLOR");

       // if (_audioManager.SoundBlasterPresent)
            _videoManager.Bar(164, 151, 12, 2, "FILLCOLOR");

        //    if (SoundSourcePresent)
        //        _videoManager.Bar (164, 174, 12, 2, FILLCOLOR);
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

        // TODO: Create all directories? Or do it when the need arises?
        // TODO: Shareware/3-episode data (wl1/wl3) should disable the missing episodes
        if (!File.Exists("vswap.wl6") && !File.Exists("vswap.wl3") && !File.Exists("vswap.wl1"))
            _gameEngineManager.Quit("NO WOLFENSTEIN 3-D DATA FILES to be found!");

        // Build every menudef now so problems in any of them are reported at startup
        foreach (var menuName in _assetManager.GetMenuNames())
            _assetManager.GetMenu(menuName);

        (MainMenu, MainItems) = LoadMenu("main-menu");
        (SndMenu, SndItems) = LoadMenu("sound");
        (CtlMenu, CtlItems) = LoadMenu("control", curpos: -1);
        (CusMenu, CusItems) = LoadMenu("customize", curpos: -1);
        (NewEmenu, NewEitems) = LoadMenu("new-episode");
        (NewMenu, NewItems) = LoadMenu("new-game");
        (LSMenu, LSItems) = LoadMenu("load-game");

        (MusicMenu, MusicItems) = LoadMenu("jukebox");
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
            items = menuAsset.MenuItems.Select(mi =>
                    new CP_itemtype(
                        (short)(mi.Enabled && mi is not BlankMenuItem ? 1 : 0),
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
            indent: (short)menuAsset.Indent);

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
                // Episode names are two lines, so a blank row follows each one
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

            case "save-slots":
                // Text is drawn by PrintLSEntry from the save files, not by the menu
                return SaveGamesAvail
                    .Select(_ => new CP_itemtype(1, "", null))
                    .ToArray();

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

        foreach (var component in menu.Components)
        {
            if (component is Label label)
                DrawLabel(label);
            else
                _graphicManager.DrawComponent(component);
        }
    }

    private static void DrawLabel(Label label)
    {
        var language = _assetManager.GetText("en-us");
        var oldFont = fontnumber;

        fontnumber = label.Font;
        SETFONTCOLOR(label.Color, "BKGDCOLOR");
        PrintY = (ushort)label.Y;

        var text = label.Text.ToLanguageText(language);
        if (label.HorizontalOrientation == HorizontalOrientation.Center)
        {
            WindowX = 0;
            WindowW = 320;
            US_CPrint(text);
        }
        else
        {
            PrintX = (ushort)label.X;
            US_Print(text);
        }

        fontnumber = oldFont;
        SETFONTCOLOR("TEXTCOLOR", "BKGDCOLOR");
    }

    private static Func<int, int>? MapFunction(string menuName, MenuSwitcher? mi)
    {
        // This list will be built with attributes or scripting on the pk3
        List<Func<int, int>> avaiableFunctions = 
            [
            CP_NewGame,
            CP_Sound,
            CP_Control,
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

        if (!funcDict.TryGetValue(mi.Action, out var func))
        {
            Console.WriteLine($"Menu '{menuName}': unknown action '{mi.Action}' on item '{mi.Id ?? mi.Text}'");
            return null;
        }

        return func;
    }
}