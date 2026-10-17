using PFWolf.Extensions;
using PFWolf.SaveGames;

namespace PFWolf.Ui;

/// <summary>What the menu keys during play need from the game</summary>
/// <param name="UiRandom">A random number for show (GameHost.Network.cs)</param>
internal sealed record QuickKeysHooks(
    Func<int> UiRandom);

/// <summary>
/// The menu from play (OpenControlPanel): F7 to F10 do their thing without the menu while a
/// level is being played (CheckQuickKeys: end the game, quicksave, quickload, quit); otherwise
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
    Configuration.GameSettings settings,
    QuickKeysHooks hooks)
{
    internal void OpenControlPanel(ScanCodes scancode)
    {
        if (session.InGame)
        {
            if (CheckQuickKeys(scancode) != 0)
                return;
            session.LastGameMusicOffset = StartCPMusic(controlPanel.MenuSong);
        }
        else
            StartCPMusic(controlPanel.MenuSong);

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

    internal int CheckQuickKeys(ScanCodes scancode)
    {
        var gameInfo = engine.GetGameInfo();
        var language = assets.GetText("en-us");
        switch (scancode)
        {
            //
            // END GAME
            //
            case ScanCodes.F7:
                if (loop.Confirm("$ENDGAMESTR".ToLanguageText(language), GameConstants.MaxY) != 0)
                {
                    session.PlayState = PlayState.Died;
                    session.PlayerState.LastAttacker = null;
                    session.PlayerState.Lives = 0;
                }

                MenuDefs.FindMenuItem(controlPanel.MainMenu, "savegame")?.Active = 0;
                return 1;
            //
            // QUICKSAVE
            //
            // Always the quicksave file, never a save picked in the menus
            case ScanCodes.F8:
                draw.Message("$STR_SAVING".ToLanguageText(language) + "...", GameConstants.MaxY);
                gameSaves.RunSaveGame(1);
                return 1;

            //
            // QUICKLOAD
            //
            // The quicksave, or the load screen until there is one
            case ScanCodes.F9:
                if (File.Exists(gameSaves.QuickSavePath))
                {
                    if (loop.Confirm("$STR_LS_QUICKLOAD".ToLanguageText(language), GameConstants.MaxY) != 0)
                        gameSaves.RunLoadGame(1);
                }
                else
                {
                    video.FadeOut();
                    if (video.HasMargins)
                        video.ClearScreen(0);

                    session.LastGameMusicOffset = StartCPMusic(controlPanel.MenuSong);
                    gameSaves.RunLoadGame(0);    // loads lastgamemusicoffs

                    input.ClearKeysDown();
                    video.FadeOut();
                    if (settings.ViewSize != 21)
                        playScreen.DrawPlayScreen();

                    if (!session.GameChosen && !session.LoadedGame)
                        music.ContinueMusic(session.LastGameMusicOffset);

                    if (session.LoadedGame)
                        session.PlayState = PlayState.Abort;

                    frame.LastTimeCount = (int)GameEngineManager.GetTimeCount();

                    input.CenterMouse();
                }
                return 1;

            //
            // QUIT
            //
            case ScanCodes.F10:
                string endStr = ControlPanel.PickEndString(gameInfo.EndStrings, hooks.UiRandom);
                if (loop.Confirm(endStr, GameConstants.MaxY) != 0)
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
