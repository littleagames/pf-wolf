using PFWolf.Extensions;

namespace PFWolf.Ui;

/// <summary>What the control panel needs from the game</summary>
/// <param name="SaveGame">The Save Game screen (RunSaveGame), 0 for the screen</param>
/// <param name="LoadGame">The Load Game screen (RunLoadGame), 0 for the screen</param>
/// <param name="EndGame">Ends the game being played: no lives left, died, nobody to turn to</param>
/// <param name="Random">A random number, 0 to 255, for picking a quit message</param>
internal sealed record ControlPanelHooks(Func<int, int> SaveGame, Func<int, int> LoadGame,
    Action EndGame, Func<int> Random);

/// <summary>
/// The control panel (US_ControlPanel): the main menu and what it leads to, the F-keys that
/// go straight to a screen, and Read This!, View Scores, End Game and Quit
/// </summary>
internal sealed class ControlPanel(GameEngineManager engine, AssetManager assets, InputManager input, AudioManager audio, VideoManager video,
    MenuDrawing draw, MenuLoop loop, MenuDefs defs, ArticleReader articles, HighScoresScreen highScores, SoundMenu soundMenu,
    ControlMenus controlMenus, ChangeViewScreen changeView, Func<GameInfoSettings> settings, GameSession session, ControlPanelHooks hooks)
{
    // Loaded from menudefs/ in CheckForEpisodes
    internal ControlPanelItem[] MainMenu = [];
    internal ControlPanelMenuInfo MainItems = null!;
    // Control panel song: the main menu's music in menudefs/main-menu
    internal string MENUSONG => engine.GetGameInfo().MenuMusic ?? assets.GetMenu("main-menu")?.Music ?? "WONDERIN";
    internal string INTROSONG => engine.GetGameInfo().IntroMusic ?? "";
    internal string HIGHSCORESSONG => engine.GetGameInfo().HighScoresMusic ?? "";

    internal int StartGame;

    /// <summary>The game was ended with End Game in the menu, so the game over screen is skipped</summary>
    internal bool endedFromMenu;
    private void EnableEndGameMenuItem()
    {
        var language = assets.GetText("en-us");
        var item = MenuDefs.FindMenuItem(MainMenu, "viewscores");
        if (item == null) return;
        item.routine = null;
        item.text = "$MENU_ENDGAME".ToLanguageText(language);
    }

    internal void EnableViewScoresMenuItem()
    {
        var language = assets.GetText("en-us");
        var item = MenuDefs.FindMenuItem(MainMenu, "viewscores");
        if (item == null) return;
        item.routine = _ => ViewScores();
        item.text = "$MENU_VIEWSCORES".ToLanguageText(language);
    }

    internal void SetupControlPanel()
    {
        //
        // CACHE SOUNDS
        //
        if (video.HasMargins)
            video.ClearScreen(0);

        if (!session.ingame)
        { }// CA_LoadAllSounds();
        else
            MenuDefs.FindMenuItem(MainMenu, "savegame")?.active = 1;

        input.CenterMouse();
    }

    /// <summary>
    /// The control panel, from the title or over the game being played: the screen an F-key
    /// names (F1 help, F2 save, F3 load, F4 sound, F5 view size, F6 controls), else the main
    /// menu until it starts a game or goes back. The in-play F7 to F10 and the menu music are
    /// the caller's (GameHost.OpenControlPanel).
    /// </summary>
    internal void Run(ScanCodes scancode)
    {
        int which;

        SetupControlPanel();

        //
        // F-KEYS FROM WITHIN GAME
        //
        switch (scancode)
        {
            case ScanCodes.F1:
                // Only where the main menu has "Read This!": its help text's pictures are the
                // Apogee releases' (id's GT v1.4 and Spear made F1 a boss key instead)
                if (MenuDefs.FindMenuItem(MainMenu, "readthis") != null)
                    articles.HelpScreens();
                goto finishup;
            case ScanCodes.F2:
                hooks.SaveGame(0);
                goto finishup;

            case ScanCodes.F3:
                hooks.LoadGame(0);
                goto finishup;

            case ScanCodes.F4:
                soundMenu.Run();
                goto finishup;

            case ScanCodes.F5:
                changeView.Run();
                goto finishup;

            case ScanCodes.F6:
                controlMenus.RunControl();
                goto finishup;

            finishup:
                CleanupControlPanel();
                return;
        }

        DrawMainMenu();
        draw.MenuFadeIn();
        StartGame = 0;

        //
        // MAIN MENU LOOP
        //
        do
        {
            which = loop.HandleMenu(MainItems, MainMenu, null);
            switch (which < 0 ? "quit" : MenuDefs.SelectedId(MainMenu, which))
            {
                case "viewscores":
                    if (MainMenu[which].routine == null)
                    {
                        if (EndGame() != 0)
                            StartGame = 1;
                    }
                    else
                    {
                        DrawMainMenu();
                        draw.MenuFadeIn();
                    }
                    break;

                case "backtodemo":
                    StartGame = 1;
                    if (!session.ingame)
                        audio.PlayMusic(INTROSONG);
                    video.FadeOut(settings().menuFadeStyle, new Color { Alpha = 255 }, 10, settings().menuFadeTics);
                    break;

                case "quit":
                    Quit();
                    break;

                default:
                    if (StartGame == 0)
                    {
                        DrawMainMenu();
                        draw.MenuFadeIn();
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
        if (session.startgame || session.loadedgame)
            EnableEndGameMenuItem();
    }

    internal void DrawMainMenu()
    {
        var language = assets.GetText("en-us");

        defs.DrawMenuComponents("main-menu");

        //
        // CHANGE "GAME" AND "DEMO"
        //
        var backToDemo = MenuDefs.FindMenuItem(MainMenu, "backtodemo");
        if (backToDemo != null)
        {
            if (session.ingame)
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

        draw.DrawMenu(MainItems, MainMenu);
        video.Update();
    }

    internal int ReadThis()
    {
        audio.PlayMusic("CORNER");
        articles.HelpScreens();
        audio.PlayMusic(MENUSONG);
        return 1;
    }

    internal int ViewScores()
    {
        audio.PlayMusic(HIGHSCORESSONG);

        highScores.DrawHighScores();
        video.Update();
        draw.MenuFadeIn();

        input.Ack();

        audio.PlayMusic(MENUSONG);
        draw.MenuFadeOut();

        return 0;
    }

    internal int EndGame()
    {
        var language = assets.GetText("en-us");
        int res = loop.Confirm("$ENDGAMESTR".ToLanguageText(language));

        DrawMainMenu();
        if (res == 0) return 0;

        hooks.EndGame();
        endedFromMenu = true;       // no game over screen (bstone's InstantQuit)

        MenuDefs.FindMenuItem(MainMenu, "savegame")?.active = 0;
        EnableViewScoresMenuItem();
        return 1;
    }

    /// <summary>
    /// The quit question, picked as Wolf3D did ((random &amp; (count - 2)) + (random &amp; 1), made for
    /// its nine), which stays in range for two or more; one is always that one, none is none.
    /// </summary>
    internal static string PickEndString(IReadOnlyList<string> endStrings, Func<int> random)
    {
        if (endStrings.Count <= 1)
            return endStrings.Count == 1 ? endStrings[0] : "";
        int first = random() & (endStrings.Count - 2);
        return endStrings[first + (random() & 1)];
    }

    internal int Quit()
    {
        var gameInfo = engine.GetGameInfo();
        string endStr = PickEndString(gameInfo.EndStrings, hooks.Random);
        if (loop.Confirm(endStr) != 0)
        {
            video.Update();
            audio.SetPaused(true);
            audio.StopAll();
            draw.MenuFadeOut();
            engine.Quit("");
            return 0;
        }

        DrawMainMenu();
        return 0;
    }

    internal void CleanupControlPanel()
    {
        // Keep whatever was changed in the menus (view size, controls, sensitivity) even if the
        // game doesn't get to exit cleanly.
        engine.WriteConfig();
    }
}
