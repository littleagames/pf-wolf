using PFWolf.Networking;
using PFWolf.Scripting;

namespace PFWolf.Gameplay;

/// <summary>What playing a level needs from the game</summary>
/// <param name="DemoTesting">demotest: each demo plays to its end, keys or not</param>
/// <param name="TickPlayerBodies">What others see of the players (Gameplay/PlayerBodies.cs)</param>
/// <param name="UpdateSoundListener">Where the sounds are heard from</param>
/// <param name="UpdateAutomap">The automap's movement and zoom (Ui/AutomapInput.cs)</param>
/// <param name="AutosavePending">Whether the level asked for an autosave</param>
/// <param name="AutoSaveGame">Makes it, now there's a picture of the level for it</param>
/// <param name="TickAmbience">The player's zone's ambient sound (Gameplay/ZoneAmbience.cs)</param>
/// <param name="AfterThinking">demotrace: writes down the frame just played (Gameplay/DemoTracer.cs)</param>
/// <param name="TickLevelTimer">A level against the clock (Gameplay/LevelTimer.cs)</param>
internal sealed record LevelLoopHooks(
    Func<bool> DemoTesting,
    Action<uint> TickPlayerBodies,
    Action UpdateSoundListener,
    Action UpdateAutomap,
    Func<bool> AutosavePending,
    Action AutoSaveGame,
    Action? TickAmbience = null,
    Action? AfterThinking = null,
    Action? TickLevelTimer = null);

/// <summary>
/// Playing a level, a frame at a time until it ends (PlayLoop): the controls, everyone's
/// thinking, the view drawn, then the keys. And the actor list it starts from (InitActorList).
/// </summary>
internal sealed class LevelLoop(
    InputManager input,
    VideoManager video,
    ConsoleManager console,
    AutomapManager automap,
    HudMessageManager hudMessages,
    MapManager map,
    FrameRefresh frame,
    InputPolling polling,
    PlayKeys keys,
    Doors doors,
    PushWalls pushWalls,
    TimedSwitches timedSwitches,
    FloorDamage floorDamage,
    PlayerView playerView,
    Lighting lighting,
    GameScripts scripts,
    Deathmatch deathmatch,
    NetPlay netPlay,
    LevelAI levelAI,
    GameSession session,
    LevelLoopHooks hooks)
{
    internal int FunnyTicCount;

    internal void PlayLoop()
    {
        session.PlayState = PlayState.StillPlaying;
        frame.LastTimeCount = (int)GameEngineManager.GetTimeCount();
        frame.FrameOn = 0;
        FunnyTicCount = 0;
        input.InitButtonState();
        if (!netPlay.NetJoinResume)             // (just joined mid-level: kept as the host had them)
        {
            foreach (var p in session.Players)  // every player's, so every machine starts the level alike
            {
                p.AngleFrac = 0;
                p.FaceCount = 0;
                p.Input.Reset();
            }
        }
        video.ClearPaletteShifts();
        if (session.NetGame)
            netPlay.NetLevelStart();            // counting frames from 0 (Networking/NetPlay.cs)

        input.CenterMouse();

        hudMessages.Clear();     // nothing left over from the last level, life or saved game

        if (session.DemoPlayback)
            input.StartAck();

        hooks.UpdateSoundListener();
        frame.Interpolation?.Forget();     // nothing slides in from the last level, life or saved game

        do
        {
            FrameProfiler.BeginFrame();

            // Uncapped, a frame with no tic due yet draws the view again between tics
            bool uncapped = UncappedNow();
            if (uncapped && DrawBetweenTics())
                continue;

            polling.PollControls();
            FrameProfiler.Tics = (int)session.Tics;
            FrameProfiler.Mark(FrameProfiler.Section.Input);
            if (uncapped)
            {
                frame.Interpolation?.BeginTics();   // where everything is before these tics, to draw it between
                batchTics = (int)session.Tics;
            }

            // With con_pause on, an open console freezes the world but keeps drawing it.
            // CalcTics still advances LastTimeCount each frame, so no time builds up to be
            // spent all at once when the console closes.
            // Not with others, though: the game goes on for them
            bool worldPaused = console.IsPausingGame && !session.NetGame;

            if (!worldPaused)
            {
                // this frame's controls, for each player to read as they think: with others,
                // everyone's as the host sent them (Networking/NetPlay.cs); else this machine's for
                // the local player, and whatever came for everyone else
                if (session.NetGame)
                {
                    if (!netPlay.NetFrame())
                    {
                        session.PlayState = PlayState.Abort;    // left the game, or the host is gone
                        continue;
                    }
                }
                else
                {
                    foreach (var p in session.Players)
                        p.Input.Begin(ReferenceEquals(p, session.LocalPlayer) ? polling.LocalCmd : p.PendingCmd);
                }

                //
                // actor thinking
                //
                session.MadeNoise = false;
                doors.MoveDoors();
                pushWalls.MovePWalls();
                timedSwitches.Tick(session.Tics);       // switches turning back by themselves (Gameplay/TimedSwitches.cs)

                // Every actor lives in map._actors. The player is at its head, so it still
                // thinks before every enemy, projectile and the BJ-victory actor.
                FrameProfiler.Mark(FrameProfiler.Section.World);       // doors, pushwalls and switches
                map.DoActors(session.Tics);
                FrameProfiler.Mark(FrameProfiler.Section.Actors);
                floorDamage.Tick();                     // floors that hurt (Gameplay/FloorDamage.cs)
                hooks.TickLevelTimer?.Invoke();         // a level against the clock (Gameplay/LevelTimer.cs)
                foreach (var p in session.Players)    // pickups each player walked onto (Rendering/PlayerView.cs)
                {
                    if (p.Pawn is { } pawn && session.IsTargetable(pawn))
                        using (session.ActAs(p))
                            playerView.TouchItems();
                }
                hooks.TickPlayerBodies(session.Tics);     // what others see of the players, and
                session.Camera.Tick(session.Tics);          // a watched actor's view turning (Gameplay/Camera.cs)
                levelAI.Tick();     // wall outlets and warp sites (Managers.LevelAI)
                lighting.TickZoneLights(session.Tics);       // light zones' fades and effects (Rendering/Lighting.cs)
                map.Scrollers.Tick(session.Tics);            // scrolling walls and flats (Rendering/Scrollers.cs)
                hooks.TickAmbience?.Invoke();               // the player's zone's ambient sound (Gameplay/ZoneAmbience.cs)
                scripts.RunTickHooks();             // the scripts' game.ontick hooks (Scripting/GameScripts.cs)
                if (session.GameMode != GameMode.Single)
                    playerView.WakeSeenEnemies();      // what the renderer did alone (Rendering/PlayerView.cs)
                if (session.GameMode is not (GameMode.Single or GameMode.Coop))
                    deathmatch.TickDeathmatch();       // items back, the objective and the time limit (Gameplay/Deathmatch.cs)
                if (session.NetGame)
                    netPlay.NetFrameDone();
                console.TickWaiting((int)session.Tics);      // commands put off by `wait`

                video.UpdatePaletteShifts(session.Tics);
                hudMessages.Tick((int)session.Tics);
                hooks.AfterThinking?.Invoke();
            }

            hooks.UpdateAutomap();
            FrameProfiler.Mark(FrameProfiler.Section.World);

            DrawFrame(uncapped);

            if (hooks.AutosavePending())
                hooks.AutoSaveGame();         // now there's a picture of the level for it

            if (!worldPaused)
            {
                session.GameState.TimeCount += (int)session.Tics;
                session.GameState.PlayTime += (int)session.Tics;
            }

            hooks.UpdateSoundListener();      // JAB
            if (video.ScreenFaded)
                video.FadeIn();

            keys.CheckKeys();

            console.RunDeferred();      // console commands that need to run between frames

            //
            // debug aids
            //
            if (session.SingleStep != 0)
            {
                GameEngineManager.WaitVBL(session.SingleStep);
                frame.LastTimeCount = (int)GameEngineManager.GetTimeCount();
            }
            if (session.ExtraVbls != 0)
                GameEngineManager.WaitVBL((uint)session.ExtraVbls);

            if (session.DemoPlayback && !hooks.DemoTesting())       // demotest plays each to its end
            {
                if (input.CheckAck())
                {
                    input.ClearKeysDown();
                    session.PlayState = PlayState.Abort;
                }
            }
        }
        while (session.PlayState == 0 && !session.GameChosen);

        // Intermission, death and menu screens don't draw the console, so don't leave it
        // capturing keys behind them (e.g. after a "map" command ends the level).
        console.Close();
        automap.Close();

        if (session.PlayState != PlayState.Died)
            video.FinishPaletteShifts();
    }

    /*
    ====================
    =
    = Uncapped frame rate (docs/uncapped-framerate-plan.md)
    =
    = The world still thinks in 70 Hz tics, but frames are drawn as often as the display shows
    = them, everything placed between its last two tics (Rendering/FrameInterpolation.cs). Demos,
    = their recording and testing, and games with others keep a frame a tic: what the view sees
    = wakes enemies and marks the automap, and those have to happen as the original's do.
    =
    ====================
    */

    int batchTics = 1;          // the tics the last batch ran
    double lastFrameMs;         // when the last uncapped frame started, for the frame cap

    bool UncappedNow() =>
        video.Settings.Uncapped && frame.Interpolation != null
        && !session.DemoPlayback && !session.DemoRecord && !session.NetGame && !hooks.DemoTesting()
        && session.SingleStep == 0 && session.ExtraVbls == 0;

    // With no tic due yet, draws the view again, between tics; false once one's due (the loop runs it)
    bool DrawBetweenTics()
    {
        WaitForFrameSlot();
        if (frame.TicsDue() > 0)
            return false;

        input.ProcessEvents();          // the window stays live; the controls wait for the tic
        FrameProfiler.Tics = 0;
        FrameProfiler.Mark(FrameProfiler.Section.Input);

        // Nothing that moves on as it's drawn (a quake, the scope, the status bar's ECG) moves:
        // no time has gone by for the game
        uint tics = session.Tics;
        session.Tics = 0;
        DrawFrame(uncapped: true);
        session.Tics = tics;
        return true;
    }

    // With vsync, presenting waits for the display; without, frames are capped at vid_maxfps (the
    // display's rate)
    void WaitForFrameSlot()
    {
        if (!video.PresentWaits)
        {
            // Not woken early for a tic: it runs on the next frame, and the frames between are
            // placed by time anyway, so drawing the moment it's due gains nothing
            // The slots are on a timeline of their own, so a wait that oversleeps doesn't put
            // every frame after it later; sleeping stops short of the slot, as it can oversleep
            // by a millisecond, and the rest is yielded away
            int fps = video.Settings.MaxFps > 0 ? video.Settings.MaxFps : video.RefreshRate();
            double interval = 1000.0 / fps;
            double now = frame.NowInTics() * 100 / 7;
            double until = lastFrameMs + interval;
            if (until < now - interval)
                until = now;            // well behind (a slow frame, a pause): start the timeline again
            if (until - now > 1)
                GameEngineManager.DelayMs((uint)(until - now - 1));
            while (frame.NowInTics() * 100 / 7 < until)
                Thread.Yield();
            lastFrameMs = until;
            return;
        }
        lastFrameMs = frame.NowInTics() * 100 / 7;
    }

    // The view, drawn between tics when uncapped: as far through the last batch as real time
    // has got through the tic after it (a tic behind, so there's always somewhere to be between)
    void DrawFrame(bool uncapped)
    {
        var interpolation = frame.Interpolation;
        if (uncapped && interpolation != null)
        {
            double fraction = (frame.NowInTics() - frame.LastTimeCount + batchTics - 1) / batchTics;
            interpolation.Fraction = Math.Clamp(fraction, 0, 1);
            interpolation.Active = true;
        }
        frame.ThreeDRefresh();
        if (interpolation != null)
            interpolation.Active = false;
    }

    internal void InitActorList()
    {
        //
        // the players are created first, so they sit at the head of _actors and think first
        //
        foreach (var p in session.Players.Where(p => !p.Gone))     // not anyone who has left the game
            map.CreatePlayer(p);
    }
}
