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
internal sealed record LevelLoopHooks(
    Func<bool> DemoTesting,
    Action<uint> TickPlayerBodies,
    Action UpdateSoundListener,
    Action UpdateAutomap,
    Func<bool> AutosavePending,
    Action AutoSaveGame,
    Action? TickAmbience = null);

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
    internal int funnyticount;

    internal void PlayLoop()
    {
        session.playstate = PlayState.StillPlaying;
        frame.lasttimecount = (int)GameEngineManager.GetTimeCount();
        frame.frameon = 0;
        funnyticount = 0;
        input.InitButtonState();
        if (!netPlay.netJoinResume)             // (just joined mid-level: kept as the host had them)
        {
            foreach (var p in session.players)  // every player's, so every machine starts the level alike
            {
                p.AngleFrac = 0;
                p.FaceCount = 0;
                p.Input.Reset();
            }
        }
        video.ClearPaletteShifts();
        if (session.netgame)
            netPlay.NetLevelStart();            // counting frames from 0 (Networking/NetPlay.cs)

        input.CenterMouse();

        hudMessages.Clear();     // nothing left over from the last level, life or saved game

        if (session.demoplayback)
            input.StartAck();

        hooks.UpdateSoundListener();

        do
        {
            polling.PollControls();

            // With con_pause on, an open console freezes the world but keeps drawing it.
            // CalcTics still advances lasttimecount each frame, so no time builds up to be
            // spent all at once when the console closes.
            // Not with others, though: the game goes on for them
            bool worldPaused = console.IsPausingGame && !session.netgame;

            if (!worldPaused)
            {
                // this frame's controls, for each player to read as they think: with others,
                // everyone's as the host sent them (Networking/NetPlay.cs); else this machine's for
                // the local player, and whatever came for everyone else
                if (session.netgame)
                {
                    if (!netPlay.NetFrame())
                    {
                        session.playstate = PlayState.Abort;    // left the game, or the host is gone
                        continue;
                    }
                }
                else
                {
                    foreach (var p in session.players)
                        p.Input.Begin(ReferenceEquals(p, session.localplayer) ? polling.localcmd : p.PendingCmd);
                }

                //
                // actor thinking
                //
                session.madenoise = false;
                doors.MoveDoors();
                pushWalls.MovePWalls();
                timedSwitches.Tick(session.tics);       // switches turning back by themselves (Gameplay/TimedSwitches.cs)

                // Every actor lives in map._actors. The player is at its head, so it still
                // thinks before every enemy, projectile and the BJ-victory actor.
                map.DoActors(session.tics);
                floorDamage.Tick();                     // floors that hurt (Gameplay/FloorDamage.cs)
                foreach (var p in session.players)    // pickups each player walked onto (Rendering/PlayerView.cs)
                {
                    if (p.Pawn is { } pawn && session.IsTargetable(pawn))
                        using (session.ActAs(p))
                            playerView.TouchItems();
                }
                hooks.TickPlayerBodies(session.tics);     // what others see of the players, and
                session.camera.Tick(session.tics);          // a watched actor's view turning (Gameplay/Camera.cs)
                levelAI.Tick();     // wall outlets and warp sites (Managers.LevelAI)
                lighting.TickZoneLights(session.tics);       // light zones' fades and effects (Rendering/Lighting.cs)
                hooks.TickAmbience?.Invoke();               // the player's zone's ambient sound (Gameplay/ZoneAmbience.cs)
                scripts.RunTickHooks();             // the scripts' game.ontick hooks (Scripting/GameScripts.cs)
                if (session.gamemode != GameMode.Single)
                    playerView.WakeSeenEnemies();      // what the renderer did alone (Rendering/PlayerView.cs)
                if (session.gamemode is not (GameMode.Single or GameMode.Coop))
                    deathmatch.TickDeathmatch();       // items back, the objective and the time limit (Gameplay/Deathmatch.cs)
                if (session.netgame)
                    netPlay.NetFrameDone();
                console.TickWaiting((int)session.tics);      // commands put off by `wait`

                video.UpdatePaletteShifts(session.tics);
                hudMessages.Tick((int)session.tics);
            }

            hooks.UpdateAutomap();

            frame.ThreeDRefresh();

            if (hooks.AutosavePending())
                hooks.AutoSaveGame();         // now there's a picture of the level for it

            if (!worldPaused)
            {
                session.gamestate.TimeCount += (int)session.tics;
                session.gamestate.PlayTime += (int)session.tics;
            }

            hooks.UpdateSoundListener();      // JAB
            if (video.screenfaded)
                video.FadeIn();

            keys.CheckKeys();

            console.RunDeferred();      // console commands that need to run between frames

            //
            // debug aids
            //
            if (session.singlestep != 0)
            {
                GameEngineManager.WaitVBL(session.singlestep);
                frame.lasttimecount = (int)GameEngineManager.GetTimeCount();
            }
            if (session.extravbls != 0)
                GameEngineManager.WaitVBL((uint)session.extravbls);

            if (session.demoplayback && !hooks.DemoTesting())       // demotest plays each to its end
            {
                if (input.CheckAck())
                {
                    input.ClearKeysDown();
                    session.playstate = PlayState.Abort;
                }
            }
        }
        while (session.playstate == 0 && !session.startgame);

        // Intermission, death and menu screens don't draw the console, so don't leave it
        // capturing keys behind them (e.g. after a "map" command ends the level).
        console.Close();
        automap.Close();

        if (session.playstate != PlayState.Died)
            video.FinishPaletteShifts();
    }

    internal void InitActorList()
    {
        //
        // the players are created first, so they sit at the head of _actors and think first
        //
        foreach (var p in session.players.Where(p => !p.Gone))     // not anyone who has left the game
            map.CreatePlayer(p);
    }
}
