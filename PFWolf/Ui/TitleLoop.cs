using PFWolf.Extensions;
using PFWolf.Assets;
using PFWolf.Fonts;
using PFWolf.Networking;
using PFWolf.Startup;

namespace PFWolf.Ui;

/// <summary>What the title loop needs from the game</summary>
/// <param name="IntroSong">The title's music</param>
/// <param name="NewGame">Starts a game on a skill, from a map (GameHost.NewGame)</param>
internal sealed record TitleLoopHooks(
    Func<string> IntroSong,
    Action<short, EpisodeInfo, MapInfo> NewGame);

/// <summary>
/// From the signon to the end: the signon screen (SignonScreen, FinishSignon), then the title
/// loop (DemoLoop): game-info's title-sequence, its intro once and then its loop (title,
/// credits, high scores and demos) round and round until a key brings up the menu
/// (Ui/TitleScreens.cs), and the game the menu (or --warp, --host, --join, a demo request)
/// starts (RunStartedGame).
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

    /// <param name="startupLines">What startup printed, shown as warnings in the signon's text area</param>
    internal void FinishSignon(List<string> startupLines)
    {
        signonPanel.FinishSignonInfo(startupLines);

        if (!engine.GetGameInfo().Signon.PressAKey)
        {
            video.Update();

            if (!engine.GameParams.NoWait)
                GameEngineManager.WaitVBL(3 * 70);
        }
        else
        {
            var background = PromptBackground(0, 189, 300, 11);
            video.Bar(0, 189, 300, 11, background);
            ui.CenteredText(0, 320, 190, new TextStyle(UiFonts.SmallFont, "Bright Yellow", background)).CPrint("Press a key"); // "Oprima una tecla"

            video.Update();

            if (!engine.GameParams.NoWait)
                input.Ack();

            video.Bar(0, 189, 300, 11, background);
            ui.CenteredText(0, 320, 190, new TextStyle(UiFonts.SmallFont, "Lime", background)).CPrint("Working..."); // "pensando..."

            video.Update();
        }
    }

    /// <summary>
    /// The color the signon pic has most of under the prompt's bar (Wolf3D's is its maroon), so the
    /// bar blends into whatever pic a game or mod uses
    /// </summary>
    private string PromptBackground(int x, int y, int width, int height)
    {
        var counts = new int[256];
        for (int ly = y; ly < y + height; ly++)
            for (int lx = x; lx < x + width; lx++)
                counts[video.GetPixel(video.ScreenX(lx), video.ScreenY(ly))]++;

        return Array.IndexOf(counts, counts.Max()).ToString();
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
        else if (!engine.GameParams.NoWait && gameInfo.TitleSequence.Intro.Count > 0)
        {
            // game-info's intro (Wolf3D's notice and PG-13, Blake Stone's logos), which starts its own music
            titleScreens.RunTitleScreens(gameInfo.TitleSequence.Intro, TitleKeyAction.Next, out _);
        }
        else
            audio.PlayMusic(hooks.IntroSong());

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

            // game-info's title loop (title, credits, high scores, demos) round and round until a
            // key goes to the menu. One with nothing the game can show goes straight there.
            var loop = gameInfo.TitleSequence.Loop;
            while (!engine.GameParams.NoWait && loop.Count > 0)
            {
                if (string.IsNullOrEmpty(audio.CurrentMusicTrack))
                    audio.PlayMusic(hooks.IntroSong());
                if (titleScreens.RunTitleScreens(loop, TitleKeyAction.Menu, out bool showedAny) == TitleKeyAction.Menu || !showedAny)
                    break;
            }

            video.FadeOut();

            // Tab held as the loop is left records a demo (Wolf3D's)
            if (titleScreens.LeftHoldingTab || input.IsKeyDown(ScanCodes.Tab))
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
        if (!engine.GameParams.NoWait)
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
        session.GameState.Entrance = "";       // by no entrance in particular: the level's own start
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
