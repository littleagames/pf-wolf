using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Extensions;

namespace PFWolf.Ui;

/// <summary>What the control panel needs from the game</summary>
/// <param name="SaveGame">The Save Game screen (RunSaveGame), 0 for the screen</param>
/// <param name="LoadGame">The Load Game screen (RunLoadGame), 0 for the screen</param>
/// <param name="EndGame">Ends the game being played: no lives left, died, nobody to turn to</param>
/// <param name="Random">A random number, 0 to 255, for picking a quit message</param>
/// <param name="QuitScreens">game-info's title-sequence quit screens, once quitting is confirmed</param>
/// <param name="EasterEggScreens">A menu easter egg's screens, with whether its keys are still held</param>
internal sealed record ControlPanelHooks(Func<int, int> SaveGame, Func<int, int> LoadGame,
    Action EndGame, Func<int> Random, Action QuitScreens, Action<List<TitleScreenInfo>, Func<bool>> EasterEggScreens);

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
    internal string MenuSong => engine.GetGameInfo().MenuMusic ?? assets.GetMenu("main-menu")?.Music ?? "WONDERIN";
    internal string IntroSong => engine.GetGameInfo().IntroMusic ?? "";
    internal string HighScoresSong => engine.GetGameInfo().HighScoresMusic ?? "";

    internal int StartGame;

    /// <summary>The game was ended with End Game in the menu, so the game over screen is skipped</summary>
    internal bool EndedFromMenu;
    private void EnableEndGameMenuItem()
    {
        var language = assets.GetText("en-us");
        var item = MenuDefs.FindMenuItem(MainMenu, "viewscores");
        if (item == null) return;
        item.Routine = null;
        item.Text = "$MENU_ENDGAME".ToLanguageText(language);
    }

    internal void EnableViewScoresMenuItem()
    {
        var language = assets.GetText("en-us");
        var item = MenuDefs.FindMenuItem(MainMenu, "viewscores");
        if (item == null) return;
        item.Routine = _ => ViewScores();
        item.Text = "$MENU_VIEWSCORES".ToLanguageText(language);
    }

    internal void SetupControlPanel()
    {
        //
        // CACHE SOUNDS
        //
        if (video.HasMargins)
            video.ClearScreen(0);

        if (!session.InGame)
        { }// CA_LoadAllSounds();
        else
            MenuDefs.FindMenuItem(MainMenu, "savegame")?.Active = 1;

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
            ShowHeldEasterEgg();
            switch (which < 0 ? "quit" : MenuDefs.SelectedId(MainMenu, which))
            {
                case "viewscores":
                    if (MainMenu[which].Routine == null)
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
                    if (!session.InGame)
                        audio.PlayMusic(IntroSong);
                    video.FadeOut(settings().MenuFadeStyle, new Color { Alpha = 255 }, 10, settings().MenuFadeTics);
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
        if (session.GameChosen || session.LoadedGame)
            EnableEndGameMenuItem();
    }

    /// <summary>menu-easter-eggs keys already warned about, so each is told once</summary>
    readonly HashSet<string> warnedEggKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// "EASTER EGG FOR SPEAR OF DESTINY!" (game-info menu-easter-eggs): with an egg's keys held as
    /// HandleMenu comes back, its screens, then the main menu again to its own music, before the
    /// choice is carried out. HandleMenu clears the keys before a picked item's screen runs, as
    /// id's does, so in practice they're the keys that closed that screen (View Scores, left with
    /// I and D pressed together).
    /// </summary>
    void ShowHeldEasterEgg()
    {
        // DOS's Keyboard[] was kept by the keyboard interrupt; here it's as current as the last
        // events taken, so a D pressed just after the I that ended the screen still counts
        input.ProcessEvents();
        var held = HeldEasterEgg(engine.GetGameInfo().MenuEasterEggs, input.IsInputDown, key =>
        {
            if (warnedEggKeys.Add(key))
                Console.WriteLine($"game-info menu-easter-eggs: unknown key '{key}'; that egg can't be shown");
        });
        if (held is not { } found)
            return;
        var (egg, keys) = found;

        draw.MenuFadeOut();
        hooks.EasterEggScreens(egg.Screens, () => keys.Any(input.IsInputDown));
        input.ClearKeysDown();

        DrawMainMenu();
        audio.PlayMusic(MenuSong);
        draw.MenuFadeIn();
    }

    /// <summary>
    /// The first egg whose keys are all down, with those keys. One with no keys or no screens never
    /// is, nor is one with a key that can't be read (each told to <paramref name="unknownKey"/>).
    /// </summary>
    internal static (MenuEasterEggInfo Egg, List<InputCode> Keys)? HeldEasterEgg(IEnumerable<MenuEasterEggInfo> eggs,
        Func<InputCode, bool> isDown, Action<string>? unknownKey = null)
    {
        foreach (var egg in eggs)
        {
            if (egg.Keys.Count == 0 || egg.Screens.Count == 0)
                continue;

            var keys = new List<InputCode>();
            foreach (var name in egg.Keys)
            {
                if (InputCode.TryParse(name, out var code))
                    keys.Add(code);
                else
                    unknownKey?.Invoke(name);
            }
            if (keys.Count == egg.Keys.Count && keys.All(isDown))
                return (egg, keys);
        }
        return null;
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
            if (session.InGame)
            {
                backToDemo.Text = "$MENU_BACKTOGAME".ToLanguageText(language);
                backToDemo.Active = 2;
            }
            else
            {
                backToDemo.Text = "$MENU_BACKTODEMO".ToLanguageText(language);
                backToDemo.Active = 1;
            }
        }

        draw.DrawMenu(MainItems, MainMenu);
        video.Update();
    }

    internal int ReadThis()
    {
        audio.PlayMusic("CORNER");
        articles.HelpScreens();
        audio.PlayMusic(MenuSong);
        return 1;
    }

    internal int ViewScores()
    {
        audio.PlayMusic(HighScoresSong);

        highScores.DrawHighScores();
        video.Update();
        draw.MenuFadeIn();

        input.Ack();

        audio.PlayMusic(MenuSong);
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
        EndedFromMenu = true;       // no game over screen (bstone's InstantQuit)

        MenuDefs.FindMenuItem(MainMenu, "savegame")?.Active = 0;
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
            hooks.QuitScreens();
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
