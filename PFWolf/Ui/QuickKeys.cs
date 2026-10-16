using PFWolf.Extensions;
using PFWolf.SaveGames;

namespace PFWolf.Ui;

/// <summary>What the menu keys during play need from the game</summary>
/// <param name="InGame">Whether a level is being played (the quick keys work then)</param>
/// <param name="SetLastMusicOffset">Where the level's song was left, to pick it up after</param>
/// <param name="LastMusicOffset">Where it was left</param>
/// <param name="SetPlayState">Ends the level: the game ended, or a game loaded</param>
/// <param name="ViewSize">The view's size setting (21: the full screen, nothing to redraw around it)</param>
/// <param name="StartGame">Whether a new game was started</param>
/// <param name="LoadedGame">Whether a game was loaded</param>
/// <param name="UiRandom">A random number for show (GameHost.NetPlay.cs)</param>
internal sealed record QuickKeysHooks(
    Func<bool> InGame,
    Action<int> SetLastMusicOffset,
    Func<int> LastMusicOffset,
    Action<playstatetypes> SetPlayState,
    Func<int> ViewSize,
    Func<bool> StartGame,
    Func<bool> LoadedGame,
    Func<int> UiRandom);

/// <summary>
/// The menu from play (US_ControlPanel): F7 to F10 do their thing without the menu while a
/// level is being played (CP_CheckQuick: end the game, quicksave, quickload, quit); otherwise
/// the menu's music comes on and the control panel opens (Ui/ControlPanel.cs).
/// </summary>
internal sealed class QuickKeys(
    GameEngineManager engine,
    AssetManager assets,
    VideoManager video,
    AudioManager audio,
    InputManager input,
    MenuDrawing draw,
    MenuLoop loop,
    ControlPanel controlPanel,
    GameSaves gameSaves,
    LevelMusic music,
    FrameRefresh frame,
    PlayScreen playScreen,
    GameSession session,
    QuickKeysHooks hooks)
{
    internal void US_ControlPanel(ScanCodes scancode)
    {
        if (hooks.InGame())
        {
            if (CP_CheckQuick(scancode) != 0)
                return;
            hooks.SetLastMusicOffset(StartCPMusic(controlPanel.MENUSONG));
        }
        else
            StartCPMusic(controlPanel.MENUSONG);

        controlPanel.Run(scancode);
    }

    internal int StartCPMusic(string song)
    {
        //int lastoffs;

        //lastoffs = audio.SetPaused(true);

        audio.PlayMusic(song);
        //return lastoffs;
        return 0;
    }

    internal int CP_CheckQuick(ScanCodes scancode)
    {
        var gameInfo = engine.GetGameInfo();
        var language = assets.GetText("en-us");
        switch (scancode)
        {
            //
            // END GAME
            //
            case ScanCodes.sc_F7:
                if (loop.Confirm("$ENDGAMESTR".ToLanguageText(language), GameConstants.MAXY) != 0)
                {
                    hooks.SetPlayState(playstatetypes.ex_died);
                    session.playerstate.LastAttacker = null;
                    session.playerstate.lives = 0;
                }

                MenuDefs.FindMenuItem(controlPanel.MainMenu, "savegame")?.active = 0;
                return 1;
            //
            // QUICKSAVE
            //
            // Always the quicksave file, never a save picked in the menus
            case ScanCodes.sc_F8:
                draw.Message("$STR_SAVING".ToLanguageText(language) + "...", GameConstants.MAXY);
                gameSaves.CP_SaveGame(1);
                return 1;

            //
            // QUICKLOAD
            //
            // The quicksave, or the load screen until there is one
            case ScanCodes.sc_F9:
                if (File.Exists(gameSaves.QuickSavePath))
                {
                    if (loop.Confirm("$STR_LS_QUICKLOAD".ToLanguageText(language), GameConstants.MAXY) != 0)
                        gameSaves.CP_LoadGame(1);
                }
                else
                {
                    video.FadeOut();
                    if (video.HasMargins)
                        video.ClearScreen(0);

                    hooks.SetLastMusicOffset(StartCPMusic(controlPanel.MENUSONG));
                    gameSaves.CP_LoadGame(0);    // loads lastgamemusicoffs

                    input.ClearKeysDown();
                    video.FadeOut();
                    if (hooks.ViewSize() != 21)
                        playScreen.DrawPlayScreen();

                    if (!hooks.StartGame() && !hooks.LoadedGame())
                        music.ContinueMusic(hooks.LastMusicOffset());

                    if (hooks.LoadedGame())
                        hooks.SetPlayState(playstatetypes.ex_abort);

                    frame.lasttimecount = (int)GameEngineManager.GetTimeCount();

                    input.CenterMouse();
                }
                return 1;

            //
            // QUIT
            //
            case ScanCodes.sc_F10:
                string endStr = ControlPanel.PickEndString(gameInfo.EndStrings, hooks.UiRandom);
                if (loop.Confirm(endStr, GameConstants.MAXY) != 0)
                {
                    video.Update();
                    audio.SetPaused(true);
                    audio.StopAll();
                    draw.MenuFadeOut();

                    engine.Quit("");
                }

                playScreen.DrawPlayBorder();
                return 1;
        }

        return 0;
    }
}
