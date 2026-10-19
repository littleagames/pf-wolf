using PFWolf.Assets;
using PFWolf.Extensions;
using PFWolf.Networking;
using PFWolf.Scripting;

namespace PFWolf.Gameplay;

/// <summary>
/// A map switch asked for by A_ChangeMap (Spear's pickup), an elevator or a teleporter:
/// GameLoop waits WaitTics (Spear lets the pickup sound play out), loads the map without an
/// intermission and, with KeepPosition, puts the player back where they stood, facing the
/// same way. With ElevatorWall, they're then moved into the elevator by that panel wall.
/// </summary>
internal record PendingMapChange(string Map, bool KeepPosition, int X, int Y, short Angle, int WaitTics = 150, int ElevatorWall = 0);

/// <summary>What the game's main loop needs from the game</summary>
/// <param name="SetAutosavePending">The level saves itself once it's drawn</param>
/// <param name="EnterLevel">A kept hub level as it was left, or the map afresh: true if kept (Gameplay/LevelEntry.cs)</param>
/// <param name="LeaveLevel">Keeps a hub level as it's left (Gameplay/LevelEntry.cs)</param>
/// <param name="MarkLevelStart">What dying puts the player back to (Gameplay/LevelEntry.cs)</param>
/// <param name="ApplyPendingWarpStart">--start's tile, the first time (TitleLoop.ApplyPendingWarpStart)</param>
/// <param name="AlignPlayerInElevator">Moves the player into the elevator by a panel wall (Gameplay/LevelEntry.cs)</param>
/// <param name="ShowLevelStartMessage">A game's level start message (Blake Stone)</param>
/// <param name="ShowText">Shows a text as an article (a map's briefing or debriefing: Ui/ArticleReader.cs)</param>
/// <param name="LevelWon">A level's finished (its exit, a secret exit or the game won), before the tally: its record and the scripts' game.onlevelend hooks</param>
internal sealed record MainGameLoopHooks(
    Action<bool> SetAutosavePending,
    Func<bool> EnterLevel,
    Action LeaveLevel,
    Action MarkLevelStart,
    Action ApplyPendingWarpStart,
    Action<int, int, int> AlignPlayerInElevator,
    Action<string> ShowLevelStartMessage,
    Action<string> ShowText,
    Action<PlayState>? LevelWon = null);

/// <summary>
/// The game from its first level to its end (GameLoop): each level entered, played and left,
/// then on to the next with the intermission, back after dying, or the game's end: game over,
/// victory, a deathmatch's final scores or leaving a game with others.
/// </summary>
internal sealed class MainGameLoop(
    GameEngineManager engine,
    AssetManager assets,
    VideoManager video,
    AudioManager audio,
    InputManager input,
    InventoryManager inventory,
    StatusBarDrawing statusBar,
    PlayScreen playScreen,
    LevelMusic music,
    LevelLoop loop,
    PlayerMovement movement,
    PlayerDeath death,
    Demos demos,
    Deathmatch deathmatch,
    Scoreboard scoreboard,
    NetPlay netPlay,
    GameScripts scripts,
    Hubs hubs,
    ControlPanel controlPanel,
    IntermissionScreen intermission,
    HighScoresScreen highScores,
    TextPresenter presenter,
    MoviePlayer movies,
    GameSession session,
    Configuration.GameSettings settings,
    MainGameLoopHooks hooks)
{
    internal PendingMapChange? PendingMapChange;

    //
    // ELEVATOR BACK MAPS - REMEMBER (-1)!!
    //
    private int[] ElevatorBackTo = { 1, 1, 7, 3, 5, 3 };

    //===========================================================================


    /// <summary>
    /// After an A_ChangeMap map loads: with KeepPosition, moves the player to where they were
    /// on the old map, facing the same way.
    /// </summary>
    internal void ApplyPendingMapChange()
    {
        if (PendingMapChange is not { } change)
            return;

        PendingMapChange = null;
        if (!change.KeepPosition)
            return;

        session.Player.SetPosition(change.X >> (int)MapConstants.TileShift, change.Y >> (int)MapConstants.TileShift);
        session.Player.X = change.X;
        session.Player.Y = change.Y;
        session.Player.Angle = change.Angle;
        movement.Thrust(0, 0); // settles the player's tile and area at the new position

        if (change.ElevatorWall > 0)
            hooks.AlignPlayerInElevator(session.Player.TileX, session.Player.TileY, change.ElevatorWall);
    }

    /// <summary>
    /// Whether a level coming up shows its briefing: one entered afresh (not loaded, returned to
    /// or restarted after dying, nor switched to by A_ChangeMap), outside a demo or a game with others
    /// </summary>
    internal static bool ShowsBriefing(string levelStart, bool warped, bool demo, bool netGame) =>
        levelStart == "new" && !warped && !demo && !netGame;

    /// <summary>A level's briefing: its entrance-briefings' for the entrance it's entered by, else its own</summary>
    internal static string? BriefingFor(MapInfo map, string? entrance) =>
        !string.IsNullOrEmpty(entrance)
        && map.EntranceBriefings.FirstOrDefault(e => e.Key.Equals(entrance, StringComparison.OrdinalIgnoreCase)).Value is { Length: > 0 } briefing
            ? briefing : map.Briefing;

    // A won level's debriefing (game-info debriefing), outside a demo or a game with others
    void ShowDebriefing(MapInfo map)
    {
        if (map.Debriefing is not { Length: > 0 } debriefing || (session.DemoPlayback || session.DemoRecord) || session.NetGame)
            return;
        video.FadeOut();
        hooks.ShowText(debriefing);
    }

    internal void GameLoop()
    {
        var language = assets.GetText("en-us");
        bool died;
        bool warped = false;

        audio.StopAll();
        video.FadeOut();
        playScreen.DrawPlayScreen();
        died = false;
        do
        {
            if (!session.LoadedGame)
                foreach (var p in session.Players) p.Score = p.OldScore;
            if (!died || settings.ViewSize != 21) statusBar.DrawScore();

            session.GameChosen = false;
            string levelStart = "loaded";
            if (!session.LoadedGame)
            {
                bool kept = hooks.EnterLevel();   // a kept hub level as it was left, or the map afresh (Gameplay/LevelEntry.cs)
                ApplyPendingMapChange();
                hooks.ApplyPendingWarpStart();    // --start's tile, the first time (TitleLoop.ApplyPendingWarpStart)
                if (!died)
                    hooks.MarkLevelStart();       // what dying puts the player back to, with death-restores-level-start
                levelStart = died ? "restarted" : kept ? "returned" : "new";
            }
            scripts.RunLevelHooks(levelStart);      // the scripts' game.onlevel hooks (Scripting/GameScripts.cs)

            // A level being entered, not one loaded or restarted after dying, saves itself
            hooks.SetAutosavePending(!session.LoadedGame && !died && !(session.DemoPlayback || session.DemoRecord) && !session.NetGame);

            // Its briefing first (game-info briefing; Midnight Assault's missions)
            if (ShowsBriefing(levelStart, warped, (session.DemoPlayback || session.DemoRecord), session.NetGame)
                && engine.GetGameInfo().Maps.TryGetValue(session.GameState.MapOn, out var enteredMap)
                && BriefingFor(enteredMap, session.GameState.Entrance) is { Length: > 0 } briefing)
            {
                hooks.ShowText(briefing);
                playScreen.DrawPlayScreen();        // the article covered the screen
            }
            statusBar.DrawLevel();

            session.InGame = true;
            if (session.LoadedGame)
            {
                music.ContinueMusic(session.LastGameMusicOffset);
                session.LoadedGame = false;
            }
            else music.StartMusic();

            var levelStartMessage = engine.GetGameInfo().LevelStartMessage;
            if (warped)
            {
                warped = false;                 // an A_ChangeMap switch loads silently, no "get psyched!"
                if (!string.IsNullOrEmpty(levelStartMessage))
                    hooks.ShowLevelStartMessage(levelStartMessage);
            }
            else if (!died)
                playScreen.PreloadGraphics();             // TODO: Let this do something useful!
            else
            {
                died = false;
                session.FizzleIn = true;
            }

            statusBar.DrawLevel ();

            loop.PlayLoop();

            // The deathmatch has been won, or its time is up: who won, then back to the menus
            if (deathmatch.MatchOver)
            {
                music.StopMusic();
                session.InGame = false;
                scoreboard.ShowFinalScoreboard();
                deathmatch.MatchOver = false;
                return;
            }

            // Left the game with others, or lost the host: back to the menus
            if (netPlay.NetGameOver)
            {
                music.StopMusic();
                session.InGame = false;
                audio.StopAll();
                video.FadeOut();
                return;
            }

            if (session.PlayState is PlayState.Completed or PlayState.SecretLevel or PlayState.Warped)
                hooks.LeaveLevel();               // a hub level is kept as it's left

            if (session.PlayState == PlayState.Warped && PendingMapChange != null)
            {
                // Spear waits 150 tics for its pickup sound before the new map loads; the score
                // carries over, as the level ends without an intermission to bank it
                if (PendingMapChange.WaitTics > 0)
                    GameEngineManager.WaitVBL((uint)PendingMapChange.WaitTics);
                foreach (var p in session.Players) p.OldScore = p.Score;
                warped = true;
            }
            else
                PendingMapChange = null;

            music.StopMusic();
            session.InGame = false;

            if (session.DemoRecord && session.PlayState != PlayState.Warped)
                demos.FinishDemoRecord();

            if (demos.PendingDemo != null || demos.PendingRecord != null)
            {
                // playdemo or recorddemo ended the game: back to the title loop, which starts the demo
                audio.StopAll();
                video.FadeOut();
                MenuDefs.FindMenuItem(controlPanel.MainMenu, "savegame")?.Active = 0;
                controlPanel.EnableViewScoresMenuItem();
                return;
            }

            if (session.GameChosen || session.LoadedGame)
            {
                audio.StopAll();
                video.FadeOut();
                playScreen.DrawPlayScreen();
                died = false;
                warped = false;
                continue;
            }

            // Its record and the scripts' level-end hooks, before the tally tells what they unlocked
            if (session.PlayState is PlayState.Completed or PlayState.SecretLevel or PlayState.Victorious)
                hooks.LevelWon?.Invoke(session.PlayState);

            switch (session.PlayState)
            {
                case PlayState.Completed:
                case PlayState.SecretLevel:
                    // A level with no next level (a boss floor left by exitlevel, or by a map's own
                    // exit) ends its cluster, as the victory tile does
                    var leftInfo = engine.GetGameInfo().Maps[session.GameState.MapOn];
                    var nextMap = session.PlayState == PlayState.SecretLevel ? leftInfo.SecretNext ?? leftInfo.Next : leftInfo.Next;
                    if (string.IsNullOrEmpty(nextMap) && string.IsNullOrEmpty(leftInfo.EndMessage))
                    {
                        session.PlayState = PlayState.Victorious;
                        goto case PlayState.Victorious;
                    }

                    if (settings.ViewSize == 21) playScreen.DrawPlayScreen();
                    foreach (var p in session.Players)
                    {
                        using var _ = session.ActAs(p);
                        inventory.ResetForNextLevel();
                    }
                    statusBar.DrawKeys();
                    video.FadeOut();

                    audio.StopAll();

                    if (!hubs.IsHubMap(hubs.LevelMap))
                        intermission.LevelCompleted();          // do the intermission; a hub's levels are just left
                    ShowDebriefing(leftInfo);
                    if (settings.ViewSize == 21) playScreen.DrawPlayScreen();
                    foreach (var p in session.Players) p.OldScore = p.Score;

                    // The Spear demo's last floor: its message ends the game (shown at the end of
                    // the intermission), then the high scores
                    if (!string.IsNullOrEmpty(leftInfo.EndMessage))
                    {
                        died = true;                // don't "get psyched!" next time
                        video.FadeOut();
                        audio.StopAll();
                        highScores.CheckHighScore(session.PlayerState.Score, won: false);
                        controlPanel.EnableViewScoresMenuItem();
                        return;
                    }

                    session.GameState.MapOn = nextMap;      // the secret exit falls back to next if there's no secret-next
                    // and is entered by the way game-info says (next-entrance: a start of its own)
                    session.GameState.Entrance = (session.PlayState == PlayState.SecretLevel && leftInfo.SecretNext != null
                        ? leftInfo.SecretNextEntrance : leftInfo.NextEntrance) ?? "";
                    break;

                case PlayState.Died:
                    death.Died();
                    died = true;                    // don't "get psyched!"

                    if (session.PlayerState.Lives > -1)
                        break;                          // more lives left

                    video.FadeOut();
                    if (video.HasMargins)
                        video.ClearScreen(0);
                    audio.StopAll();

                    // The pack's game over screen, unless the game was ended from the menu
                    if (!controlPanel.EndedFromMenu && engine.GetGameInfo().LoseScreen is { } loseScreen)
                        presenter.ShowLoseScreen(loseScreen);
                    controlPanel.EndedFromMenu = false;

                    highScores.CheckHighScore(session.PlayerState.Score, won: false);
                    controlPanel.EnableViewScoresMenuItem();
                    return;

                case PlayState.Victorious:
                    if (hubs.CurrentEpisode()?.EndBriefing is { Length: > 0 } endBriefing)
                    {
                        // Blake Stone: the mission's won message over the status bar, then its
                        // end briefing, then the high scores
                        if (engine.GetGameInfo().MissionWonMessage is { Length: > 0 } wonMessage
                            && presenter.PresenterScript(wonMessage) is { } wonScript)
                        {
                            presenter.PresenterMessageBox(wonScript);
                            video.Update();
                            audio.Play("bs/bonus1");
                            input.UserInput(5 * 70);
                        }
                        video.FadeOut();
                        audio.StopAll();
                        if (hubs.CurrentEpisode()?.EndMovie is { Length: > 0 } endMovie)
                        {
                            movies.PlayMovie(endMovie);
                            video.FadeOut();
                        }
                        presenter.ShowBriefing(endBriefing);

                        highScores.CheckHighScore(session.PlayerState.Score, won: true);
                        controlPanel.EnableViewScoresMenuItem();
                        return;
                    }

                    if (settings.ViewSize == 21) playScreen.DrawPlayScreen();
                    // The won level's debriefing first (a Midnight Assault mission's), then the tally
                    if (engine.GetGameInfo().Maps.TryGetValue(session.GameState.MapOn, out var wonMap))
                        ShowDebriefing(wonMap);
                    // A cluster with a victory-fade-color fades to it slowly, as Spear does when the Angel falls
                    var wonCluster = intermission.WonCluster();
                    if (string.IsNullOrEmpty(wonCluster.VictoryFadeColor))
                        video.FadeOut();
                    else
                        video.FadeOut(IntermissionScreen.VictoryFadeColor(wonCluster), 300);
                    audio.StopAll();

                    intermission.Victory();

                    audio.StopAll();

                    highScores.CheckHighScore(session.PlayerState.Score, won: true);
                    controlPanel.EnableViewScoresMenuItem();
                    return;

                default:
                    if (settings.ViewSize == 21) playScreen.DrawPlayScreen();
                    audio.StopAll();
                    break;
            }
        } while (true);
    }
}
