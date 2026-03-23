using SDL2;
using Wolf3D.Managers;
using Wolf3D.Mappers;
using Wolf3D.Entities;

namespace Wolf3D;

internal struct CustomCtrls
{
    public short[] allowed;

    public CustomCtrls(short n0, short n1, short n2, short n3)
    {
        allowed = [n0, n1, n2, n3];
    }
};

internal enum menuitems
{
    newgame,
    soundmenu,
    control,
    loadgame,
    savegame,
    changeview,
#if !GOODTIMES
#if !SPEAR
    readthis,
#endif
#endif
    viewscores,
    backtodemo,
    quit
}

internal partial class Program
{
    internal const menuitems STARTITEM = menuitems.readthis;

    // ENDSTRx constants are defined in foreign.h
    static string[] endStrings = [
        ENDSTR1,
        ENDSTR2,
        ENDSTR3,
        ENDSTR4,
        ENDSTR5,
        ENDSTR6,
        ENDSTR7,
        ENDSTR8,
        ENDSTR9
    ];

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

    internal const int LSM_X = 85;
    internal const int LSM_Y = 55;
    internal const int LSM_W = 175;
    internal const int LSM_H = 10 * 13 + 10;

    internal const int NM_X = 50;
    internal const int NM_Y = 100;
    internal const int NM_W = 225;
    internal const int NM_H = 13 * 4 + 15;

    internal const int NE_X = 10;
    internal const int NE_Y = 23;
    internal const int NE_W = 320 - NE_X * 2;
    internal const int NE_H = 200 - NE_Y * 2;

    internal const int CST_X = 20;
    internal const int CST_Y = 48;
    internal const int CST_START = 60;
    internal const int CST_SPC = 60;

#if SPEAR
    internal const byte BORDCOLOR = 0x29;
    internal const byte BORD2COLOR = 0x23;
    internal const byte DEACTIVE = 0x2b;
    internal const byte BKGDCOLOR = 0x2d;
#else

    internal const byte BORDCOLOR = 0x29;
    internal const byte BORD2COLOR = 0x23;
    internal const byte DEACTIVE = 0x2b;
    internal const byte BKGDCOLOR = 0x2d;
    internal const byte STRIPE = 0x2c;
#endif

    internal const byte READCOLOR = 0x4a;
    internal const byte READHCOLOR = 0x47;
    internal const byte VIEWCOLOR = 0x7f;
    internal const byte TEXTCOLOR = 0x17;
    internal const byte HIGHLIGHT = 0x13;

    internal static musicnames MENUSONG => musicnames.WONDERIN_MUS;
#if SPEAR
    internal static musicnames INTROSONG => musicnames.XTOWER2_MUS;
#else
    internal static musicnames INTROSONG => musicnames.NAZI_NOR_MUS;
#endif

    internal static int SENSITIVE = 60;

    internal static CP_itemtype[] MainMenu = [];
    internal static CP_itemtype[] SndMenu = [];
    internal enum CtlOptions { CTL_MOUSEENABLE, CTL_MOUSESENS, CTL_JOYENABLE, CTL_CUSTOMIZE };

    internal static CP_itemtype[] CtlMenu = [];
    internal static CP_itemtype[] NewEmenu = [];
    internal static CP_itemtype[] NewMenu = [];

    internal static CP_itemtype[] LSMenu =
    {
        new(1, "", null),
        new(1, "", null),
        new(1, "", null),
        new(1, "", null),
        new(1, "", null),
        new(1, "", null),
        new(1, "", null),
        new(1, "", null),
        new(1, "", null),
        new(1, "", null),
    };

    internal static CP_itemtype[] CusMenu =
    {
        new(1, "", null),
        new(0, "", null),
        new(0, "", null),
        new(1, "", null),
        new(0, "", null),
        new(0, "", null),
        new(1, "", null),
        new(0, "", null),
        new(1, "", null),
    };

    internal static CP_itemtype[] MusicMenu =
    {
        new (1,"Get Them!", null),
        new (1,"Searching", null),
        new (1,"P.O.W.", null),
        new (1,"Suspense", null),
        new (1,"War March", null),
        new (1,"Around The Corner!", null),

        new (1,"Nazi Anthem", null),
        new (1,"Lurking...", null),
        new (1,"Going After Hitler", null),
        new (1,"Pounding Headache", null),
        new (1,"Into the Dungeons", null),
        new (1,"Ultimate Conquest", null),

        new (1,"Kill the S.O.B.", null),
        new (1,"The Nazi Rap", null),
        new (1,"Twelfth Hour", null),
        new (1,"Zero Hour", null),
        new (1,"Ultimate Conquest", null),
        new (1,"Wolfpack", null)
    };

    internal static CP_iteminfo MainItems;
    internal static CP_iteminfo SndItems;
    internal static CP_iteminfo LSItems = new(LSM_X, LSM_Y, (short)LSMenu.Length, 0, 24);
    internal static CP_iteminfo CtlItems;
    internal static CP_iteminfo CusItems = new(8, CST_Y + 13 * 2, (short)CusMenu.Length, -1, 0);
    internal static CP_iteminfo NewEitems = new(NE_X, NE_Y, (short)NewEmenu.Length, 0, 88);
    internal static CP_iteminfo NewItems = new(NM_X, NM_Y, (short)NewMenu.Length, 2, 24);
    internal static CP_iteminfo MusicItems = new(CTL_X, CTL_Y, 6, 0, 32);
    static int[] color_hlite =
    {
        DEACTIVE,
        HIGHLIGHT,
        READHCOLOR,
        0x67
    };

    static int[] color_norml =
    {
        DEACTIVE,
        TEXTCOLOR,
        READCOLOR,
        0x6b
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

    internal static string SaveName = "savegam?.";

    internal static void SetupSaveGames()
    {
        string savepath = string.Empty;
        for (int i = 0; i < 10; i++)
        {
            var name = SaveName.Replace('?', (char)('0' + i));
            // TODO: Set up "Saves" folder
           // if (!string.IsNullOrEmpty(configdir))
           //     savepath = $"{configdir}/{name}";
           // else
                savepath = $"{name}";

            if (File.Exists(savepath))
            {
                SaveGamesAvail[i] = 1;

                using (FileStream fs = File.OpenRead(savepath))
                    using (BinaryReader br = new(fs))
                    {
                        SaveGameNames[i] = new string(br.ReadChars(MaxGameName));
                    }
            }
        }
    }

    private static void EnableEndGameMenuItem()
    {
        MainMenu[(int)menuitems.viewscores].routine = null;
        MainMenu[(int)menuitems.viewscores].text = STR_EG;
    }

    internal static void ClearMScreen()
    {
        _videoManager.Bar(0, 0, 320, 200, BORDCOLOR);
    }

    internal static void DrawStripes(int y)
    {
        _videoManager.Bar(0, y, 320, 24, 0);
        _videoManager.HorizontalLine(0, 319, y + 22, STRIPE);
    }

    internal static void DrawWindow(int x, int y, int w, int h, int wcolor)
    {
        _videoManager.Bar(x, y, w, h, wcolor);
        DrawOutline(x, y, w, h, BORD2COLOR, DEACTIVE);
    }

    internal static void DrawOutline(int x, int y, int w, int h, int color1, int color2)
    {
        _videoManager.HorizontalLine(x, x + w, y, color2);
        _videoManager.VerticalLine(y, y + h, x, color2);
        _videoManager.HorizontalLine(x, x + w, y + h, color1);
        _videoManager.VerticalLine(y, y + h, x + w, color1);
    }

#if SPEAR
    internal static void MenuFadeOut() => VL_FadeOut(0, 255, 0, 0, 51, 10);
#else
    internal static void MenuFadeOut() => _videoManager.FadeOut(0, 255, 43, 0, 0, 10);
#endif
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
                SETFONTCOLOR(DEACTIVE, BKGDCOLOR);
                US_Print((items[i]).text);
                SETFONTCOLOR(TEXTCOLOR, BKGDCOLOR);
            }

            US_Print("\n");
        }
    }

    static int lastmusic;
    internal static int StartCPMusic(musicnames song)
    {
        int lastoffs;

        lastmusic = (int)song;
        lastoffs = SD_MusicOff();
        UNCACHEAUDIOCHUNK(STARTMUSIC + lastmusic);

        SD_StartMusic(STARTMUSIC + song);
        return lastoffs;
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
                    if (items[i].active != 0 && items[i].text[0] == key)
                    {
                        EraseGun(item_i, items, x, y, which);
                        which = i;
                        DrawGun(item_i, items, x, ref y, which, basey, routine);
                        ok = 1;
                        _inputManager.ClearKeysDown();
                        break;
                    }

                //
                // DIDN'T FIND A MATCH FIRST TIME THRU. CHECK AGAIN.
                //
                if (ok != 0)
                {
                    for (i = 0; i < which; i++)
                        if (items[i].active != 0 && (items[i]).text[0] == key)
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
                case Direction.dir_North:

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
                case Direction.dir_South:

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
            _videoManager.Bar(x - 1, y, 25, 16, BKGDCOLOR);
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
                SD_PlaySound((int)soundnames.ESCPRESSEDSND);
                return -1;
        }

        return 0; // JUST TO SHUT UP THE ERROR MESSAGES!
    }

    internal static void EraseGun(CP_iteminfo item_i, CP_itemtype[] items, int x, int y, int which)
    {
        _videoManager.Bar(x - 1, y, 25, 16, BKGDCOLOR);
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
        SD_PlaySound((int)soundnames.MOVEGUN1SND);
        GameEngineManager.DelayMs(8 * 100 / 7);
    }

    internal static void DrawGun(CP_iteminfo item_i, CP_itemtype[] items, int x, ref int y, int which, int basey, Action<int>? routine)
    {
        _videoManager.Bar(x - 1, y, 25, 16, BKGDCOLOR);
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
        SD_PlaySound((int)soundnames.MOVEGUN2SND);
    }

    internal static void CheckPause()
    {
        if (_gameEngineManager.IsPaused())
        {
            switch (SoundStatus)
            {
                case 0:
                    SD_MusicOn();
                    break;
                case 1:
                    SD_MusicOff();
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
            SETFONTCOLOR((byte)color_hlite[items.active], BKGDCOLOR);
        }
        else
        {
            SETFONTCOLOR((byte)color_norml[items.active], BKGDCOLOR);
        }
    }

    internal static void ShootSnd()
    {
        SD_PlaySound((int)soundnames.SHOOTSND);
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
        while ((int)GameEngineManager.GetTimeCount() - startTime < count && ci.dir != Direction.dir_None);
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
                ci.dir = Direction.dir_North;
                mouseactive = 1;
            }
            else if (totalMousey > SENSITIVE)
            {
                ci.dir = Direction.dir_South;
                mouseactive = 1;
            }

            if (totalMousex < -SENSITIVE)
            {
                ci.dir = Direction.dir_West;
                mouseactive = 1;
            }
            else if (totalMousex > SENSITIVE)
            {
                ci.dir = Direction.dir_East;
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
                ci.dir = Direction.dir_North;
            else if (jy > SENSITIVE)
                ci.dir = Direction.dir_South;

            if (jx < -SENSITIVE)
                ci.dir = Direction.dir_West;
            else if (jx > SENSITIVE)
                ci.dir = Direction.dir_East;

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
        SETFONTCOLOR(TEXTCOLOR, BKGDCOLOR);
        fontnumber = 1;
        WindowH = 200;
        if (_videoManager.screenHeight % 200 != 0)
            _videoManager.ClearScreen(0);

        if (!ingame)
            CA_LoadAllSounds();
        else
            MainMenu[(int)menuitems.savegame].active = 1;

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
            switch (which)
            {
                case (int)menuitems.viewscores:
                    if (MainMenu[(int)menuitems.viewscores].routine == null)
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

                case (int)menuitems.backtodemo:
                    StartGame = 1;
                    if (!ingame)
                        StartCPMusic(INTROSONG);
                    _videoManager.FadeOut(0, 255, 0, 0, 0, 10);
                    break;

                case -1:
                case (int)menuitems.quit:
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
        switch (scancode)
        {
            //
            // END GAME
            //
            case ScanCodes.sc_F7:
                WindowH = 160;
                if (Confirm(ENDGAMESTR) != 0)
                {
                    playstate = playstatetypes.ex_died;
                    LastAttacker = null;
                    pickquick = gamestate.lives = 0;
                }

                WindowH = 200;
                fontnumber = 0;
                MainMenu[(int)menuitems.savegame].active = 0;
                return 1;
            //
            // QUICKSAVE
            //
            case ScanCodes.sc_F8:
                if (SaveGamesAvail[LSItems.curpos] != 0 && pickquick != 0)
                {
                    fontnumber = 1;
                    Message(STR_SAVING + "...");
                    CP_SaveGame(1);
                    fontnumber = 0;
                }
                else
                {
                    _videoManager.FadeOut();
                    if (_videoManager.screenHeight % 200 != 0)
                        _videoManager.ClearScreen(0);

                    lastgamemusicoffset = StartCPMusic(MENUSONG);
                    pickquick = CP_SaveGame(0);

                    SETFONTCOLOR(0, 15);
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
                    fontnumber = 1;

                    var str = $"{STR_LGC} {SaveGameNames[LSItems.curpos]}\"?";

                    if (Confirm(str) != 0)
                        CP_LoadGame(1);

                    fontnumber = 0;
                }
                else
                {
                    _videoManager.FadeOut();
                    if (_videoManager.screenHeight % 200 != 0)
                        _videoManager.ClearScreen(0);

                    lastgamemusicoffset = StartCPMusic(MENUSONG);
                    pickquick = CP_LoadGame(0);    // loads lastgamemusicoffs

                    SETFONTCOLOR(0, 15);
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
                if (Confirm(endStrings[(US_RndT() & 0x7) + (US_RndT() & 1)]) != 0)
                {
                    _videoManager.Update();
                    SD_MusicOff();
                    SD_StopSound();
                    MenuFadeOut();

                    _gameEngineManager.Quit("");
                }

                DrawPlayBorder();
                WindowH = 200;
                fontnumber = 0;
                return 1;
        }

        return 0;
    }

    internal static void DrawMainMenu()
    {
        var mainMenu = _assetManager.GetMenu("main-menu");
        
        foreach (var menuComponent in mainMenu.Components)
        {
            _graphicManager.DrawComponent(menuComponent);
        }

        //
        // CHANGE "GAME" AND "DEMO"
        //
        if (ingame)
        {
            MainMenu[(int)menuitems.backtodemo].text = STR_BG;
            MainMenu[(int)menuitems.backtodemo].active = 2;
        }
        else
        {
            MainMenu[(int)menuitems.backtodemo].text = STR_BD;
            MainMenu[(int)menuitems.backtodemo].active = 1;
        }

        DrawMenu(MainItems, MainMenu);
        _videoManager.Update();
    }

    internal static int CP_NewGame(int _)
    {
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
                        SD_PlaySound((int)soundnames.NOWAYSND);
                        Message("Episode unavailable!");
                        _inputManager.ClearKeysDown();
                        _inputManager.Ack();
                        DrawNewEpisode();
                        which = 0;
                    }
                    else
                    {
                        if (!_assetManager.GetGameInfo().Maps.TryGetValue(episodeInfo.StartMap, out mapInfo))
                        {
                            SD_PlaySound((int)soundnames.NOWAYSND);
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
            if (Confirm(CURGAME) == 0)
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
        MainMenu[(int)menuitems.readthis].active = 1;
        pickquick = 0;

        return 0;
    }

    internal static void DrawNewEpisode()
    {
        int i;
        ClearMScreen();
        _graphicManager.DrawPic("c_mouselback", 112, 184);

        DrawWindow(NE_X - 4, NE_Y - 4, NE_W + 8, NE_H + 8, BKGDCOLOR);
        SETFONTCOLOR(READHCOLOR, BKGDCOLOR);
        PrintY = 2;
        WindowX = 0;
        US_CPrint("Which episode to play?");
        SETFONTCOLOR(TEXTCOLOR, BKGDCOLOR);
        DrawMenu(NewEitems, NewEmenu);

        string[] episodePics = new[] { "c_episode1", "c_episode2", "c_episode3", "c_episode4", "c_episode5", "c_episode6" };
        for (i = 0; i < episodePics.Length; i++)
            _graphicManager.DrawPic(episodePics[i], NE_X + 32, NE_Y + i * 26);

        _videoManager.Update();
        MenuFadeIn();
        WaitKeyUp();
    }

    internal static void DrawNewGame()
    {
        ClearMScreen();
        _graphicManager.DrawPic("c_mouselback", 112, 184);

        SETFONTCOLOR(READHCOLOR, BKGDCOLOR);
        PrintX = NM_X + 20;
        PrintY = NM_Y - 32;
        US_Print("How tough are you?");
        DrawWindow(NM_X - 5, NM_Y - 10, NM_W, NM_H, BKGDCOLOR);

        DrawMenu(NewItems, NewMenu);
        DrawNewGameDiff(NewItems.curpos);
        _videoManager.Update();
        MenuFadeIn();
        WaitKeyUp();
    }

    internal static void DrawNewGameDiff(int w)
    {
        SkillInfo[] skills = _assetManager.GetGameInfo().Skills.Values.ToArray();
        _graphicManager.DrawPic(skills[w].PicName, NM_X + 185, NM_Y + 7);
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
            switch (which)
            {
                //
                // SOUND EFFECTS
                //
                case 0:
                    if (SoundMode != SDMode.Off)
                    {
                        SD_WaitSoundDone();
                        SD_SetSoundMode(SDMode.Off);
                        DrawSoundMenu();
                    }
                    break;
                case 1:
                    if (SoundMode != SDMode.PC)
                    {
                        SD_WaitSoundDone();
                        SD_SetSoundMode(SDMode.PC);
                        CA_LoadAllSounds();
                        DrawSoundMenu();
                        ShootSnd();
                    }
                    break;
                case 2:
                    if (SoundMode != SDMode.AdLib)
                    {
                        SD_WaitSoundDone();
                        SD_SetSoundMode(SDMode.AdLib);
                        CA_LoadAllSounds();
                        DrawSoundMenu();
                        ShootSnd();
                    }
                    break;

                //
                // DIGITIZED SOUND
                //
                case 5:
                    if (DigiMode != (byte)SDSMode.Off)
                    {
                        SD_SetDigiDevice((byte)SDSMode.Off);
                        DrawSoundMenu();
                    }
                    break;
                case 6:
                    /*                if (DigiMode != sds_SoundSource)
                                    {
                                        SD_SetDigiDevice (sds_SoundSource);
                                        DrawSoundMenu ();
                                        ShootSnd ();
                                    }*/
                    break;
                case 7:
                    if (DigiMode != SDSMode.SoundBlaster)
                    {
                        SD_SetDigiDevice(SDSMode.SoundBlaster);
                        DrawSoundMenu();
                        ShootSnd();
                    }
                    break;

                //
                // MUSIC
                //
                case 10:
                    if (MusicMode != SMMode.Off)
                    {
                        SD_SetMusicMode(SMMode.Off);
                        DrawSoundMenu();
                        ShootSnd();
                    }
                    break;
                case 11:
                    if (MusicMode != SMMode.AdLib)
                    {
                        SD_SetMusicMode(SMMode.AdLib);
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
        var soundMenu = _assetManager.GetMenu("sound");

        foreach (var menuComponent in soundMenu.Components)
        {
            _graphicManager.DrawComponent(menuComponent);
        }

        //
        // IF NO ADLIB, NON-CHOOSENESS!
        //
        if (!AdLibPresent && !SoundBlasterPresent)
        {
            SndMenu[2].active = SndMenu[10].active = SndMenu[11].active = 0;
        }

        if (!SoundBlasterPresent)
            SndMenu[7].active = 0;

        if (!SoundBlasterPresent)
            SndMenu[5].active = 0;

        DrawMenu(SndItems, SndMenu);
        for (i = 0; i < SndItems.amount; i++)
            if (SndMenu[i].text != string.Empty)
            {
                //
                // DRAW SELECTED/NOT SELECTED GRAPHIC BUTTONS
                //
                on = 0;
                switch (i)
                {
                    //
                    // SOUND EFFECTS
                    //
                    case 0:
                        if (SoundMode == SDMode.Off)
                            on = 1;
                        break;
                    case 1:
                        if (SoundMode == SDMode.PC)
                            on = 1;
                        break;
                    case 2:
                        if (SoundMode == SDMode.AdLib)
                            on = 1;
                        break;

                    //
                    // DIGITIZED SOUND
                    //
                    case 5:
                        if (DigiMode == SDSMode.Off)
                            on = 1;
                        break;
                    case 6:
                        //                    if (DigiMode == sds_SoundSource)
                        //                        on = 1;
                        break;
                    case 7:
                        if (DigiMode == SDSMode.SoundBlaster)
                            on = 1;
                        break;

                    //
                    // MUSIC
                    //
                    case 10:
                        if (MusicMode == SMMode.Off)
                            on = 1;
                        break;
                    case 11:
                        if (MusicMode == SMMode.AdLib)
                            on = 1;
                        break;
                }

                if (on != 0)
                    _graphicManager.DrawPic("c_selected", SM_X + 24, SM_Y1 + i * 13 + 2);
                else
                    _graphicManager.DrawPic("c_notselected", SM_X + 24, SM_Y1 + i * 13 + 2);
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
            switch ((CtlOptions)which)
            {
                case CtlOptions.CTL_MOUSEENABLE:
                    mouseenabled ^= true;
                    _inputManager.CenterMouse();
                    DrawCtlScreen();
                    CusItems.curpos = -1;
                    ShootSnd();
                    break;

                case CtlOptions.CTL_JOYENABLE:
                    joystickenabled ^= true;
                    DrawCtlScreen();
                    CusItems.curpos = -1;
                    ShootSnd();
                    break;

                case CtlOptions.CTL_MOUSESENS:
                case CtlOptions.CTL_CUSTOMIZE:
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
        int i, x, y;
        var assetMenu = _assetManager.GetMenu("control");

        foreach (var menuComponent in assetMenu.Components)
        {
            _graphicManager.DrawComponent(menuComponent);
        }

        WindowX = 0;
        WindowW = 320;
        SETFONTCOLOR(TEXTCOLOR, BKGDCOLOR);

        if (_inputManager.JoyPresent())
            CtlMenu[(int)CtlOptions.CTL_JOYENABLE].active = 1;

        if (_inputManager.IsMousePresent())
        {
            CtlMenu[(int)CtlOptions.CTL_MOUSESENS].active = CtlMenu[(int)CtlOptions.CTL_MOUSEENABLE].active = 1;
        }

        CtlMenu[(int)CtlOptions.CTL_MOUSESENS].active = (short)(mouseenabled ? 1 : 0);


        DrawMenu(CtlItems, CtlMenu);


        x = CTL_X + CtlItems.indent - 24;
        y = CTL_Y + 3;
        if (mouseenabled)
            _graphicManager.DrawPic("c_selected", x, y);
        else
            _graphicManager.DrawPic("c_notselected", x, y);

        y = CTL_Y + 29;
        if (joystickenabled)
            _graphicManager.DrawPic("c_selected", x, y);
        else
            _graphicManager.DrawPic("c_notselected", x, y);

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

    internal static int CP_LoadGame(int quick)
    {
        int which, exit = 0;
        string name;
        string loadpath;

        name = SaveName;
        //
        // QUICKLOAD?
        //
        if (quick != 0)
        {
            which = LSItems.curpos;

            if (SaveGamesAvail[which] != 0)
            {
                name.Replace('?', (char)(which + '0'));

                //if (string.IsNullOrEmpty(configdir))
                //    loadpath = $"{configdir}/{name}";
                //else
                    loadpath = $"{name}";

                using (FileStream fs = File.OpenRead(loadpath))
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        fs.Seek(32, SeekOrigin.Begin);
                        loadedgame = true;
                        LoadTheGame(br, 0, 0);
                        loadedgame = false;
                    }

                DrawFace();
                DrawHealth();
                DrawLives();
                DrawLevel();
                DrawAmmo();
                DrawKeys();
                DrawWeapon();
                DrawScore();
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
                name = name.Replace('?', (char)(which + '0'));

                //if (!string.IsNullOrEmpty(configdir))
                //    loadpath = $"{configdir}/{name}";
                //else
                    loadpath = $"{name}";

                using (FileStream fs = File.OpenRead(loadpath))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    fs.Seek(32, SeekOrigin.Begin);

                    DrawLSAction(0);
                    loadedgame = true;

                    LoadTheGame(br, LSA_X + 8, LSA_Y + 5);
                }

                StartGame = 1;
                ShootSnd();
                //
                // CHANGE "READ THIS!" TO NORMAL COLOR
                //
                MainMenu[(int)menuitems.readthis].active = 1;
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
        const int DISKX = 100;
        const int DISKY = 0;

        int i;


        ClearMScreen();
        fontnumber = 1;
        _graphicManager.DrawPic("c_mouselback",112, 184);
        DrawWindow(LSM_X - 10, LSM_Y - 5, LSM_W, LSM_H, BKGDCOLOR);
        DrawStripes(10);

        if (loadsave == 0)
            _graphicManager.DrawPic("c_loadgame", 60, 0);
        else
            _graphicManager.DrawPic("c_savegame", 60, 0);

        for (i = 0; i < 10; i++)
            PrintLSEntry(i, TEXTCOLOR);

        DrawMenu(LSItems, LSMenu);
        _videoManager.Update();
        MenuFadeIn();
        WaitKeyUp();
    }

    internal static void TrackWhichGame (int w)
    {
        int lastgameon = 0;

        PrintLSEntry(lastgameon, TEXTCOLOR);
        PrintLSEntry(w, HIGHLIGHT);

        lastgameon = w;
    }

    internal static void PrintLSEntry(int w, int color)
    {
        SETFONTCOLOR((byte)color, BKGDCOLOR);
        DrawOutline(LSM_X + LSItems.indent, LSM_Y + w * 13, LSM_W - LSItems.indent - 15, 11, color,
                     color);
        PrintX = (ushort)(LSM_X + LSItems.indent + 2);
        PrintY = (ushort)(LSM_Y + w * 13 + 1);
        fontnumber = 0;

        if (SaveGamesAvail[w] != 0)
            US_Print(new string(SaveGameNames[w]));
        else
            US_Print($"      - {STR_EMPTY} -");

        fontnumber = 1;
    }

    internal const int LSA_X = 96;
    internal const int LSA_Y = 80;
    internal const int LSA_W = 130;
    internal const int LSA_H = 42;

    internal static void DrawLSAction(int which)
    {
        DrawWindow(LSA_X, LSA_Y, LSA_W, LSA_H, TEXTCOLOR);
        DrawOutline(LSA_X, LSA_Y, LSA_W, LSA_H, 0, HIGHLIGHT);
        _graphicManager.DrawPic("c_diskloading1", LSA_X + 8, LSA_Y + 5);

        fontnumber = 1;
        SETFONTCOLOR(0, TEXTCOLOR);
        PrintX = LSA_X + 46;
        PrintY = LSA_Y + 13;

        if (which == 0)
            US_Print(STR_LOADING + "...");
        else
            US_Print(STR_SAVING + "...");

        _videoManager.Update();
    }

    internal static int CP_SaveGame(int quick)
    {
        int which, exit = 0;
        string name;
        string savepath;
        string input = "";

        name = SaveName;
        //
        // QUICKSAVE?
        //
        if (quick != 0)
        {
            which = LSItems.curpos;

            if (SaveGamesAvail[which] != 0)
            {
                name.Replace('?', (char)(which + '0'));

                //if (!string.IsNullOrEmpty(configdir))
                //    savepath = $"{configdir}/{name}";
                //else
                    savepath = $"{name}";

                File.Delete(savepath);
                using (FileStream fs = File.OpenRead(savepath))
                    using (BinaryWriter bw = new BinaryWriter(fs))
                    {
                        input = SaveGameNames[which];
                        bw.Write(input.ToFixedArray(32), 0, 32);
                        bw.Seek(32, SeekOrigin.Begin);
                        SaveTheGame(bw, 0, 0);
                    }

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
                    if (Confirm(GAMESVD) == 0)
                    {
                        DrawLoadSaveScreen(1);
                        continue;
                    }
                    else
                    {
                        DrawLoadSaveScreen(1);
                        PrintLSEntry(which, HIGHLIGHT);
                        _videoManager.Update();
                    }
                }

                ShootSnd();

                SaveGameNames[which] = input;
                name = name.Replace('?', (char)(which + '0'));

                fontnumber = 0;
                if (SaveGamesAvail[which] == 0)
                    _videoManager.Bar(LSM_X + LSItems.indent + 1, LSM_Y + which * 13 + 1,
                             LSM_W - LSItems.indent - 16, 10, BKGDCOLOR);
                _videoManager.Update();

                if (US_LineInput
                    (LSM_X + LSItems.indent + 2, LSM_Y + which * 13 + 1, ref input, input, true, 31,
                     LSM_W - LSItems.indent - 30))
                {
                    SaveGamesAvail[which] = 1;
                    SaveGameNames[which] = input;

                    //if (!string.IsNullOrEmpty(configdir))
                    //    savepath = $"{configdir}/{name}";
                    //else
                        savepath = $"{name}";

                    if (File.Exists(savepath))
                        File.Delete(savepath);

                    using (FileStream fs = File.OpenWrite(savepath))
                    using (BinaryWriter bw = new BinaryWriter(fs))
                    {
                        bw.Write(input.ToFixedArray(32), 0, 32);
                        bw.Seek(32, SeekOrigin.Begin);

                        DrawLSAction(1);
                        SaveTheGame(bw, LSA_X + 8, LSA_Y + 5);
                    }
                    ShootSnd();
                    exit = 1;
                }
                else
                {
                    _videoManager.Bar(LSM_X + LSItems.indent + 1, LSM_Y + which * 13 + 1,
                             LSM_W - LSItems.indent - 16, 10, BKGDCOLOR);
                    PrintLSEntry(which, HIGHLIGHT);
                    _videoManager.Update();
                    SD_PlaySound((int)soundnames.ESCPRESSEDSND);
                    continue;
                }

                fontnumber = 1;
                break;
            }

        }
        while (which >= 0);

        MenuFadeOut();

        return exit;
    }

    internal static int CP_ChangeView(int _)
    {
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
                case Direction.dir_South:
                case Direction.dir_West:
                    newview--;
                    if (newview < 4)
                        newview = 4;
                    if (newview >= 19) DrawChangeView(newview);
                    else ShowViewSize(newview);
                    _videoManager.Update();
                    SD_PlaySound((int)soundnames.HITWALLSND);
                    TicDelay(10);
                    break;

                case Direction.dir_North:
                case Direction.dir_East:
                    newview++;
                    if (newview >= 21)
                    {
                        newview = 21;
                        DrawChangeView(newview);
                    }
                    else ShowViewSize(newview);
                    _videoManager.Update();
                    SD_PlaySound((int)soundnames.HITWALLSND);
                    TicDelay(10);
                    break;
            }

            if (ci.button0 || _inputManager.IsKeyDown(ScanCodes.sc_Enter))
                exit = 1;
            else if (ci.button1 || _inputManager.IsKeyDown(ScanCodes.sc_Escape))
            {
                SD_PlaySound((int)soundnames.ESCPRESSEDSND);
                MenuFadeOut();
                if (_videoManager.screenHeight % 200 != 0)
                    _videoManager.ClearScreen(0);
                return 0;
            }
        }
        while (exit == 0);

        if (oldview != newview)
        {
            SD_PlaySound((int)soundnames.SHOOTSND);
            Message(STR_THINK + "...");
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
        int rescaledHeight = _videoManager.screenHeight / _videoManager.scaleFactor;
        if (view != 21) _videoManager.Bar(0, rescaledHeight - 40, 320, 40, bordercol);

        ShowViewSize(view);

        PrintY = (ushort)((_videoManager.screenHeight / _videoManager.scaleFactor) - 39);
        WindowX = 0;
        WindowY = 320;                                  // TODO: Check this!
        SETFONTCOLOR(HIGHLIGHT, BKGDCOLOR);

        US_CPrint(STR_SIZE1 +"\n");
        US_CPrint(STR_SIZE2 +"\n");
        US_CPrint(STR_SIZE3);
        _videoManager.Update();
    }

    internal static int CP_ReadThis(int _)
    {
        StartCPMusic((int)musicnames.CORNER_MUS);
        HelpScreens();
        StartCPMusic(MENUSONG);
        return 1;
    }

    internal static int CP_ViewScores(int _)
    {
        fontnumber = 0;

#if SPEAR
        StartCPMusic(musicnames.XAWARD_MUS);
#else
        StartCPMusic(musicnames.ROSTER_MUS);
#endif

        DrawHighScores();
        _videoManager.Update();
        MenuFadeIn();
        fontnumber = 1;

        _inputManager.Ack();

        StartCPMusic(MENUSONG);
        MenuFadeOut();

        return 0;
    }

    internal static int CP_EndGame(int _)
    {
        int res = Confirm(ENDGAMESTR);

        DrawMainMenu();
        if (res == 0) return 0;

        pickquick = gamestate.lives = 0;
        playstate = playstatetypes.ex_died;
        LastAttacker = null;

        MainMenu[(int)menuitems.savegame].active = 0;
        MainMenu[(int)menuitems.viewscores].routine = CP_ViewScores;
        MainMenu[(int)menuitems.viewscores].text = STR_VS;
        return 1;
    }

    internal static int CP_Quit(int _)
    {
        if (Confirm(endStrings[US_RndT() & 0x7 + (US_RndT() & 1)]) != 0)
        {
            _videoManager.Update();
            SD_MusicOff();
            SD_StopSound();
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
                case Direction.dir_North:
                case Direction.dir_West:
                    if (mouseadjustment != 0)
                    {
                        mouseadjustment--;
                        _videoManager.Bar(60, 97, 200, 10, TEXTCOLOR);
                        DrawOutline(60, 97, 200, 10, 0, HIGHLIGHT);
                        DrawOutline(60 + 20 * mouseadjustment, 97, 20, 10, 0, READCOLOR);
                        _videoManager.Bar(61 + 20 * mouseadjustment, 98, 19, 9, READHCOLOR);
                        _videoManager.Update();
                        SD_PlaySound((int)soundnames.MOVEGUN1SND);
                        TicDelay(20);
                    }
                    break;

                case Direction.dir_South:
                case Direction.dir_East:
                    if (mouseadjustment < 9)
                    {
                        mouseadjustment++;
                        _videoManager.Bar(60, 97, 200, 10, TEXTCOLOR);
                        DrawOutline(60, 97, 200, 10, 0, HIGHLIGHT);
                        DrawOutline(60 + 20 * mouseadjustment, 97, 20, 10, 0, READCOLOR);
                        _videoManager.Bar(61 + 20 * mouseadjustment, 98, 19, 9, READHCOLOR);
                        _videoManager.Update();
                        SD_PlaySound((int)soundnames.MOVEGUN1SND);
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
            SD_PlaySound((int)soundnames.ESCPRESSEDSND);
        }
        else
            SD_PlaySound((int)soundnames.SHOOTSND);

        WaitKeyUp();
        MenuFadeOut();

        return 0;
    }

    internal static void DrawMouseSens()
    {
        ClearMScreen();
        _graphicManager.DrawPic("c_mouselback", 112, 184);
        DrawWindow(10, 80, 300, 30, BKGDCOLOR);
        WindowX = 0;
        WindowW = 320;
        PrintY = 82;
        SETFONTCOLOR(READCOLOR, BKGDCOLOR);
        US_CPrint(STR_MOUSEADJ);

        SETFONTCOLOR(TEXTCOLOR, BKGDCOLOR);
        PrintX = 14;
        PrintY = 95;
        US_Print(STR_SLOW);
        PrintX = 269;
        US_Print(STR_FAST);

        _videoManager.Bar(60, 97, 200, 10, TEXTCOLOR);
        DrawOutline(60, 97, 200, 10, 0, HIGHLIGHT);
        DrawOutline(60 + 20 * mouseadjustment, 97, 20, 10, 0, READCOLOR);
        _videoManager.Bar(61 + 20 * mouseadjustment, 98, 19, 9, READHCOLOR);

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
            switch (which)
            {
                case 0:
                    DefineMouseBtns();
                    DrawCustMouse(1);
                    break;
                case 3:
                    DefineJoyBtns();
                    DrawCustJoy(0);
                    break;
                case 6:
                    DefineKeyBtns();
                    DrawCustKeybd(0);
                    break;
                case 8:
                    DefineKeyMove();
                    DrawCustKeys(0);
                    break;
            }
        }
        while (which >= 0);

        MenuFadeOut();

        return 0;
    }

    ////////////////////////
    //
    // DEFINE THE MOUSE BUTTONS
    //
    internal static void
    DefineMouseBtns()
    {
        CustomCtrls mouseallowed = new( 0, 1, 1, 1 );
        EnterCtrlData(2, ref mouseallowed, DrawCustMouse, PrintCustMouse, CustomCtlOptions.MOUSE);
    }


    ////////////////////////
    //
    // DEFINE THE JOYSTICK BUTTONS
    //
    internal static void
    DefineJoyBtns()
    {
        CustomCtrls joyallowed = new(1, 1, 1, 1);
        EnterCtrlData(5, ref joyallowed, DrawCustJoy, PrintCustJoy, CustomCtlOptions.JOYSTICK);
    }


    ////////////////////////
    //
    // DEFINE THE KEYBOARD BUTTONS
    //
    internal static void
    DefineKeyBtns()
    {
        CustomCtrls keyallowed = new(1, 1, 1, 1);
        EnterCtrlData(8, ref keyallowed, DrawCustKeybd, PrintCustKeybd, CustomCtlOptions.KEYBOARDBTNS);
    }


    ////////////////////////
    //
    // DEFINE THE KEYBOARD BUTTONS
    //
    internal static void
    DefineKeyMove()
    {
        CustomCtrls keyallowed = new( 1, 1, 1, 1 );
        EnterCtrlData(10, ref keyallowed, DrawCustKeys, PrintCustKeys, CustomCtlOptions.KEYBOARDMOVE);
    }

    internal static void EnterCtrlData(int index, ref CustomCtrls cust, Action<int> DrawRtn, Action<int> PrintRtn,
                   CustomCtlOptions type)
    {
        int j, z, exit, tick, redraw, which = 0, x = 0, picked, lastFlashTime;
        ControlInfo ci;


        ShootSnd();
        PrintY = (ushort)(CST_Y + 13 * index);
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
                DrawWindow(5, PrintY - 1, 310, 13, BKGDCOLOR);

                DrawRtn(1);
                DrawWindow(x - 2, PrintY, CST_SPC, 11, TEXTCOLOR);
                DrawOutline(x - 2, PrintY, CST_SPC, 11, 0, HIGHLIGHT);
                SETFONTCOLOR(0, TEXTCOLOR);
                PrintRtn(which);
                PrintX = (ushort)x;
                SETFONTCOLOR(TEXTCOLOR, BKGDCOLOR);
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
                SETFONTCOLOR(0, TEXTCOLOR);

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
                                _videoManager.Bar(x, PrintY + 1, CST_SPC - 2, 10, TEXTCOLOR);
                                break;
                            case 1:
                                PrintX = (ushort)x;
                                US_Print("?");
                                SD_PlaySound((int)soundnames.HITWALLSND);
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
                                SD_PlaySound((int)soundnames.SHOOTDOORSND);
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
                                SD_PlaySound((int)soundnames.SHOOTDOORSND);
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
                        SD_PlaySound((int)soundnames.ESCPRESSEDSND);
                    }

                    if (picked != 0) break;

                    ReadAnyControl(out ci);
                }

                SETFONTCOLOR(TEXTCOLOR, BKGDCOLOR);
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
                case Direction.dir_West:
                    do
                    {
                        which--;
                        if (which < 0)
                            which = 3;
                    }
                    while (cust.allowed[which] == 0);
                    redraw = 1;
                    SD_PlaySound((int)soundnames.MOVEGUN1SND);
                    do
                    {
                        ReadAnyControl(out ci);
                        GameEngineManager.DelayMs(5);
                    }
                    while (ci.dir != Direction.dir_None);
                    _inputManager.ClearKeysDown();
                    break;

                case Direction.dir_East:
                    do
                    {
                        which++;
                        if (which > 3)
                            which = 0;
                    }
                    while (cust.allowed[which] == 0);
                    redraw = 1;
                    SD_PlaySound((int)soundnames.MOVEGUN1SND);
                    do
                    {
                        ReadAnyControl(out ci);
                        GameEngineManager.DelayMs(5);
                    }
                    while (ci.dir != Direction.dir_None);
                    _inputManager.ClearKeysDown();
                    break;
                case Direction.dir_North:
                case Direction.dir_South:
                    exit = 1;
                    break;
            }
        }
        while (exit == 0);

        SD_PlaySound((int)soundnames.ESCPRESSEDSND);
        WaitKeyUp();
        DrawWindow(5, PrintY - 1, 310, 13, BKGDCOLOR);
    }


    ////////////////////////
    //
    // FIXUP GUN CURSOR OVERDRAW SHIT
    //
    internal static int fixup_lastwhich = -1;
    internal static void FixupCustom(int w)
    {
        int y = CST_Y + 26 + w * 13;


        _videoManager.HorizontalLine(7, 32, y - 1, DEACTIVE);
        _videoManager.HorizontalLine(7, 32, y + 12, BORD2COLOR);
        _videoManager.HorizontalLine(7, 32, y - 2, BORDCOLOR);
        _videoManager.HorizontalLine(7, 32, y + 13, BORDCOLOR);
        switch (w)
        {
            case 0:
                DrawCustMouse(1);
                break;
            case 3:
                DrawCustJoy(1);
                break;
            case 6:
                DrawCustKeybd(1);
                break;
            case 8:
                DrawCustKeys(1);
                break;
        }


        if (fixup_lastwhich >= 0)
        {
            y = CST_Y + 26 + fixup_lastwhich * 13;
            _videoManager.HorizontalLine(7, 32, y - 1, DEACTIVE);
            _videoManager.HorizontalLine(7, 32, y + 12, BORD2COLOR);
            _videoManager.HorizontalLine(7, 32, y - 2, BORDCOLOR);
            _videoManager.HorizontalLine(7, 32, y + 13, BORDCOLOR);
            if (fixup_lastwhich != w)
                switch (fixup_lastwhich)
                {
                    case 0:
                        DrawCustMouse(0);
                        break;
                    case 3:
                        DrawCustJoy(0);
                        break;
                    case 6:
                        DrawCustKeybd(0);
                        break;
                    case 8:
                        DrawCustKeys(0);
                        break;
                }
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
        ClearMScreen();
        WindowX = 0;
        WindowW = 320;
        _graphicManager.DrawPic("c_mouselback", 112, 184);
        DrawStripes(10);
        _graphicManager.DrawPic("c_customize", 80, 0);

        //
        // MOUSE
        //
        SETFONTCOLOR(READCOLOR, BKGDCOLOR);
        WindowX = 0;
        WindowW = 320;

        PrintY = CST_Y;
        US_CPrint("Mouse\n");

        SETFONTCOLOR(TEXTCOLOR, BKGDCOLOR);
        PrintX = CST_START;
        US_Print(STR_CRUN);
        PrintX = CST_START + CST_SPC * 1;
        US_Print(STR_COPEN);
        PrintX = CST_START + CST_SPC * 2;
        US_Print(STR_CFIRE);
        PrintX = CST_START + CST_SPC * 3;
        US_Print(STR_CSTRAFE + "\n");

        DrawWindow(5, PrintY - 1, 310, 13, BKGDCOLOR);
        DrawCustMouse(0);
        US_Print("\n");


        //
        // JOYSTICK/PAD
        //
        SETFONTCOLOR(READCOLOR, BKGDCOLOR);
        US_CPrint("Joystick/Gravis GamePad\n");
        SETFONTCOLOR(TEXTCOLOR, BKGDCOLOR);
        PrintX = CST_START;
        US_Print(STR_CRUN);
        PrintX = CST_START + CST_SPC * 1;
        US_Print(STR_COPEN);
        PrintX = CST_START + CST_SPC * 2;
        US_Print(STR_CFIRE);
        PrintX = CST_START + CST_SPC * 3;
        US_Print(STR_CSTRAFE + "\n");
        DrawWindow(5, PrintY - 1, 310, 13, BKGDCOLOR);
        DrawCustJoy(0);
        US_Print("\n");


        //
        // KEYBOARD
        //
        SETFONTCOLOR(READCOLOR, BKGDCOLOR);
        US_CPrint("Keyboard\n");
        SETFONTCOLOR(TEXTCOLOR, BKGDCOLOR);

        PrintX = CST_START;
        US_Print(STR_CRUN);
        PrintX = CST_START + CST_SPC * 1;
        US_Print(STR_COPEN);
        PrintX = CST_START + CST_SPC * 2;
        US_Print(STR_CFIRE);
        PrintX = CST_START + CST_SPC * 3;
        US_Print(STR_CSTRAFE + "\n");
        DrawWindow(5, PrintY - 1, 310, 13, BKGDCOLOR);
        DrawCustKeybd(0);
        US_Print("\n");


        //
        // KEYBOARD MOVE KEYS
        //
        SETFONTCOLOR(TEXTCOLOR, BKGDCOLOR);
        PrintX = CST_START;
        US_Print(STR_LEFT);
        PrintX = CST_START + CST_SPC * 1;
        US_Print(STR_RIGHT);
        PrintX = CST_START + CST_SPC * 2;
        US_Print(STR_FRWD);
        PrintX = CST_START + CST_SPC * 3;
        US_Print(STR_BKWD +"\n");
        DrawWindow(5, PrintY - 1, 310, 13, BKGDCOLOR);
        DrawCustKeys(0);
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
        int i, color;


        color = TEXTCOLOR;
        if (hilight != 0)
            color = HIGHLIGHT;
        SETFONTCOLOR((byte)color, BKGDCOLOR);

        if (!mouseenabled)
        {
            SETFONTCOLOR(DEACTIVE, BKGDCOLOR);
            CusMenu[0].active = 0;
        }
        else
            CusMenu[0].active = 1;

        PrintY = CST_Y + 13 * 2;
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
        int i, color;

        color = TEXTCOLOR;
        if (hilight != 0)
            color = HIGHLIGHT;
        SETFONTCOLOR((byte)color, BKGDCOLOR);

        if (!joystickenabled)
        {
            SETFONTCOLOR(DEACTIVE, BKGDCOLOR);
            CusMenu[3].active = 0;
        }
        else
            CusMenu[3].active = 1;

        PrintY = CST_Y + 13 * 5;
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
        int i, color;


        color = TEXTCOLOR;
        if (hilight != 0)
            color = HIGHLIGHT;
        SETFONTCOLOR((byte)color, BKGDCOLOR);

        PrintY = CST_Y + 13 * 8;
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
        int i, color;


        color = TEXTCOLOR;
        if (hilight != 0)
            color = HIGHLIGHT;
        SETFONTCOLOR((byte)color, BKGDCOLOR);

        PrintY = CST_Y + 13 * 10;
        for (i = 0; i < 4; i++)
            PrintCustKeys(i);
    }
    internal static void CleanupControlPanel()
    {
        fontnumber = 0;
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

        int xit = 0, x, y, tick = 0, lastBlinkTime;
        soundnames[] whichsnd = [soundnames.ESCPRESSEDSND, soundnames.SHOOTSND];
        ControlInfo ci;

        Message(text);
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
                        _videoManager.Bar(x, y, 8, 13, TEXTCOLOR);
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

        SD_PlaySound((int)whichsnd[xit]);

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

        fontnumber = 1;
        fontstruct font = _graphicManager.GetFont(fontnumber);
        h = font.height;

        for (i = 0; i < len; i++)
        {
            if (text[i] == '\n')
            {
                if (w > mw)
                    mw = w;
                w = 0;
                h += font.height;
            }
            else
                w += font.width[(byte)text[i]];
        }

        if (w + 10 > mw)
            mw = w + 10;

        PrintY = (ushort)((WindowH / 2) - (h / 2));
        PrintX = WindowX = (ushort)(160 - (mw / 2));

        DrawWindow(WindowX - 5, PrintY - 5, mw + 10, h + 10, TEXTCOLOR);
        DrawOutline(WindowX - 5, PrintY - 5, mw + 10, h + 10, 0, HIGHLIGHT);
        SETFONTCOLOR(0, TEXTCOLOR);
        US_Print(text);
        _videoManager.Update();
    }

    internal static void FreeMusic()
    {
        UNCACHEAUDIOCHUNK(STARTMUSIC + lastmusic);
    }

    internal static void IntroScreen()
    {
        const byte MAINCOLOR = 0x6c;
        const byte EMSCOLOR = 0x6c; // 0x4f
        const byte XMSCOLOR = 0x6c; // 0x7f

        const byte FILLCOLOR = 14;

        int i;
        for (i = 0; i < 10; i++)
            _videoManager.Bar(49, 163 - 8 * i, 6, 5, MAINCOLOR - i);
        for (i = 0; i < 10; i++)
            _videoManager.Bar(89, 163 - 8 * i, 6, 5, EMSCOLOR - i);
        for (i = 0; i < 10; i++)
            _videoManager.Bar(129, 163 - 8 * i, 6, 5, XMSCOLOR - i);

        //
        // FILL BOXES
        //
        if (_inputManager.IsMousePresent())
            _videoManager.Bar(164, 82, 12, 2, FILLCOLOR);

        if (_inputManager.JoyPresent())
            _videoManager.Bar(164, 105, 12, 2, FILLCOLOR);

        if (AdLibPresent && !SoundBlasterPresent)
            _videoManager.Bar(164, 128, 12, 2, FILLCOLOR);

        if (SoundBlasterPresent)
            _videoManager.Bar(164, 151, 12, 2, FILLCOLOR);

        //    if (SoundSourcePresent)
        //        _videoManager.Bar (164, 174, 12, 2, FILLCOLOR);
    }

    internal static void CheckForEpisodes()
    {
        NewMenu = _assetManager.GetGameInfo().Skills.Values.Select(s => new CP_itemtype(1, s.Name, null)).ToArray();
        NewEitems.amount = (short) NewMenu.Length;
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

        if (File.Exists("vswap.wl6"))
        {
            extension = "wl6";
            NewEmenu = _assetManager.GetGameInfo().Episodes.Values.SelectMany(ep =>
                new CP_itemtype[]
                {
                    new CP_itemtype(1, ep.Name, null, ep),
                    new CP_itemtype(0, "", null)
                }).SkipLast(1)
                .ToArray();
            NewEitems.amount = (short)NewEmenu.Length;
        }
        else
        {

            if (File.Exists("vswap.wl3"))
            {
                extension = "wl3";
                NewEmenu[2].active =
                NewEmenu[4].active = 1;
            }
            else
            {

                if (File.Exists("vswap.wl1"))
                {
                    extension = "wl1";
                }
                else
                {
                    _gameEngineManager.Quit("NO WOLFENSTEIN 3-D DATA FILES to be found!");
                }
            }
        }

        SaveName += extension;
        demoname += extension;

        var menuAsset = _assetManager.GetMenu("main-menu");
        MainMenu = menuAsset.MenuItems.Select(mi =>
                new CP_itemtype((short)(mi.IsEnabled ? 1 : 0), mi.Text, MapFunction(mi as MenuSwitcher)))
                .ToArray();
        MainItems = new CP_iteminfo(
            (short)menuAsset.Position.X, 
            (short)menuAsset.Position.Y, 
            amount: (short)MainMenu.Length, 
            curpos: 0, 
            indent: 24);

        menuAsset = _assetManager.GetMenu("sound");
        SndMenu = menuAsset.MenuItems.Select(mi =>
                new CP_itemtype((short)(mi.IsEnabled ? 1 : 0), mi.Text, MapFunction(mi as MenuSwitcher)))
                .ToArray();
        SndItems = new CP_iteminfo(
            (short)menuAsset.Position.X,
            (short)menuAsset.Position.Y,
            amount: (short)SndMenu.Length,
            curpos: 0,
            indent: (short)menuAsset.Indent);

        menuAsset = _assetManager.GetMenu("control");
        CtlMenu = menuAsset.MenuItems.Select(mi =>
                new CP_itemtype((short)(mi.IsEnabled ? 1 : 0), mi.Text, MapFunction(mi as MenuSwitcher)))
                .ToArray();
        CtlItems = new CP_iteminfo(
            (short)menuAsset.Position.X,
            (short)menuAsset.Position.Y,
            amount: (short)CtlMenu.Length,
            curpos: -1,
            indent: (short)menuAsset.Indent);
    }

    private static Func<int, int>? MapFunction(MenuSwitcher? mi)
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

        if (mi == null 
            || string.IsNullOrEmpty(mi.Action)
            || !funcDict.TryGetValue(mi.Action, out var func))
        {
            return null;
        }

        return func;
    }
}