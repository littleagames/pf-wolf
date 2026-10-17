using PFWolf.Extensions;
using PFWolf.Assets;
using PFWolf.Fonts;
using PFWolf.Networking;
using PFWolf.Startup;

namespace PFWolf.Ui;

/// <summary>What the title loop needs from the game</summary>
/// <param name="NoWait">--nowait: no signon key, intro screens or title loop</param>
/// <param name="IntroSong">The title's music</param>
/// <param name="NewGame">Starts a game on a skill, from a map (GameHost.NewGame)</param>
internal sealed record TitleLoopHooks(
    Func<bool> NoWait,
    Func<string> IntroSong,
    Action<short, EpisodeInfo, MapInfo> NewGame);

/// <summary>
/// From the signon to the end: the signon screen (SignonScreen, FinishSignon), then the title
/// loop (DemoLoop): the intro or title pictures, credits, high scores and demos round and round
/// until a key brings up the menu, and the game the menu (or --warp, --host, --join, a demo
/// request) starts (RunStartedGame).
/// </summary>
internal sealed class TitleLoop(
    GameEngineManager engine,
    AssetManager assets,
    VideoManager video,
    GraphicManager graphics,
    InputManager input,
    AudioManager audio,
    UiText ui,
    SignonPanel signonPanel,
    TitleScreens titleScreens,
    HighScoresScreen highScores,
    Demos demos,
    NetGames netGames,
    QuickKeys quickKeys,
    MainGameLoop mainGameLoop,
    CheatCommands cheats,
    WarpRequest warpRequest,
    GameSession session,
    TitleLoopHooks hooks)
{
    internal void SignonScreen()
    {
        graphics.DrawPic(engine.GetGameInfo().Signon.Pic ?? "", 0, 0);
        signonPanel.PrintSignonInfo();
    }

    /// <summary>
    /// Blanks the screen to black when it's bigger than the 320x200 layout, so a full-screen
    /// picture about to be drawn in the middle doesn't keep the last screen round its edges
    /// </summary>
    internal void ClearMargins()
    {
        if (video.HasMargins)
            video.ClearScreen(0);
    }

    /// <summary>
    /// The game-info title-pics, each drawn below the one before
    /// </summary>
    internal void DrawTitle()
    {
        ClearMargins();
        int y = 0;
        foreach (var pic in engine.GetGameInfo().TitlePics)
        {
            var graphic = assets.Find<GraphicAsset>(pic);
            if (graphic == null)
                continue;

            graphics.DrawPic(pic, 0, y);
            y += graphic.Height;
        }
    }

    /// <summary>
    /// Fades the title in with its own palette (game-info title-palette), or the game palette.
    /// The next screen's fade-in puts the game palette back.
    /// </summary>
    internal void FadeInTitle()
    {
        var paletteName = engine.GetGameInfo().TitlePalette;
        var palette = string.IsNullOrEmpty(paletteName) ? null : assets.Find<Palette>(paletteName);
        if (palette == null)
            video.FadeIn();
        else
            video.FadeIn(new GamePalette { Colors = palette.ToSDLColors() }, 30);
    }

    /// <param name="startupLines">What startup printed, shown as warnings in the signon's text area</param>
    internal void FinishSignon(List<string> startupLines)
    {
        signonPanel.FinishSignonInfo(startupLines);

        if (!engine.GetGameInfo().Signon.PressAKey)
        {
            video.Update();

            if (!hooks.NoWait())
                GameEngineManager.WaitVBL(3 * 70);
        }
        else
        {
            video.Bar(0, 189, 300, 11, "Maroon");
            ui.CenteredText(0, 320, 190, new TextStyle(UiFonts.SmallFont, "Bright Yellow", "Maroon")).CPrint("Press a key"); // "Oprima una tecla"

            video.Update();

            if (!hooks.NoWait())
                input.Ack();

            video.Bar(0, 189, 300, 11, "Maroon");
            ui.CenteredText(0, 320, 190, new TextStyle(UiFonts.SmallFont, "Lime", "Maroon")).CPrint("Working..."); // "pensando..."

            video.Update();
        }
    }

    internal void DemoLoop()
    {
        var gameInfo = engine.GetGameInfo();

        //
        // main game cycle
        //
        if (netGames.HasPendingNetStart)
        {
            // --host or --join: straight to the lobby, with the menu's music
        }
        else if (gameInfo.Intro.Count > 0)
        {
            // The game pack's own intro (Blake Stone's), which starts its own music
            if (hooks.NoWait())
                audio.PlayMusic(hooks.IntroSong());
            else
                titleScreens.RunTitleScreens(gameInfo.Intro, titleLoop: false);
        }
        else
        {
            if (!hooks.NoWait() && gameInfo.NonSharewareNotice)
                titleScreens.NonShareware();

            audio.PlayMusic(hooks.IntroSong());

            if (!hooks.NoWait() && assets.Exists<GraphicAsset>("pg13"))    // a standalone game may have none
                titleScreens.Pg13();
        }

        while (true)
        {
            if (netGames.RunPendingNetStart() || RunPendingWarp())
            {
                RunStartedGame();
                continue;
            }

            // recorddemo and playdemo, from the command line's --exec or a game they ended
            if (demos.RecordPendingDemo())
            {
                RunStartedGame();       // New Game from the menu ends a recording and starts one
                continue;
            }
            demos.PlayPendingDemo();

            while (!hooks.NoWait() && gameInfo.TitleLoop.Count > 0)
            {
                if (string.IsNullOrEmpty(audio.CurrentMusicTrack))
                    audio.PlayMusic(hooks.IntroSong());
                if (titleScreens.RunTitleScreens(gameInfo.TitleLoop, titleLoop: true))
                    break;
            }

            while (!hooks.NoWait() && gameInfo.TitleLoop.Count == 0)
            {
                //
                // title page
                //
                DrawTitle();
                video.Update();
                FadeInTitle();

                if (input.UserInput(Timing.TickBase * 15))
                    break;
                video.FadeOut();

                //
                // credits page
                //
                ClearMargins();
                graphics.DrawPic("credits", 0, 0);
                video.Update();
                video.FadeIn();
                if (input.UserInput(Timing.TickBase * 10))
                    break;
                video.FadeOut();

                //
                // high scores
                //
                highScores.DrawHighScores();
                video.Update();
                video.FadeIn();

                if (input.UserInput(Timing.TickBase * 10))
                    break;

                //
                // demo
                //
                demos.PlayDemo(titleScreens.TakeNextTitleDemo());
                if (session.PlayState == PlayState.Abort)
                    break;
                video.FadeOut();
                if (video.HasMargins)
                    video.ClearScreen(0x00); // 0x00 = Black
                audio.PlayMusic(hooks.IntroSong());
            }

            video.FadeOut();

            if (input.IsKeyDown(ScanCodes.Tab))
                demos.RecordDemo();
            else
                quickKeys.OpenControlPanel(0);

            RunStartedGame();
        }
    }

    /// <summary>Plays the game the menu started or loaded, if it did, then puts the title music back</summary>
    internal void RunStartedGame()
    {
        if (!session.GameChosen && !session.LoadedGame)
            return;

        mainGameLoop.GameLoop();
        if (session.NetGame)
            netGames.EndNetGame();       // a game with others is over: leave it (Networking/NetGames.cs)
        if (!hooks.NoWait())
        {
            video.FadeOut();
            audio.PlayMusic(hooks.IntroSong());
        }
    }

    /// <summary>Starts the new game --warp asked for (once): true if it did, for the demo loop to play</summary>
    internal bool RunPendingWarp()
    {
        var gameInfo = engine.GetGameInfo();
        if (warpRequest.TakeWarp(gameInfo, map => assets.Exists<MapAsset>(map)) is not { } warp)
            return false;

        hooks.NewGame(warp.Difficulty, warp.Episode, gameInfo.Maps[warp.Map]);
        session.GameState.MapOn = warp.Map;
        Console.WriteLine($"Warping to {warp.Map}, skill {warp.Difficulty + 1}");
        return true;
    }

    /// <summary>Puts the player where --start said, once, after the warped-to level has loaded</summary>
    internal void ApplyPendingWarpStart()
    {
        if (warpRequest.TakeStart() is not { } start)
            return;

        if (cheats.TryTeleportPlayer(start.X, start.Y, start.Angle, out var problem))
            Console.WriteLine($"Starting on {start.X},{start.Y}");
        else
            Console.WriteLine($"--start {start.X},{start.Y}: {problem}; starting on the level's player start");
    }
}
