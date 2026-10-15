using PFWolf.Configuration;
using PFWolf.Entities.Actors;
using PFWolf.Extensions;
using PFWolf.Managers;
using static SDL2.SDL;

namespace PFWolf;

internal partial class Program
{
    internal static bool madenoise; // true when shooting or screaming

    internal static playstatetypes playstate;

    internal static int DebugOk;

    // The acting player's pawn and controls, and this frame's tics (Gameplay/GameSession.cs)
    internal static Entities.Actors.PlayerPawn player => session.player;
    internal static PlayerInput playerinput => session.playerinput;
    internal static uint tics { get => session.tics; set => session.tics = value; }

    internal static byte singlestep, godmode, noclip, ammocheat, mapreveal;
    internal static int extravbls;

    //
    // control info
    //
    internal static bool mouseenabled { get => controlSettings.mouseenabled; set => controlSettings.mouseenabled = value; }
    internal static bool joystickenabled { get => controlSettings.joystickenabled; set => controlSettings.joystickenabled = value; }

    /// <summary>The keys, mouse buttons and joystick buttons bound to each button and automap key (saved to controls.cfg).</summary>
    internal static readonly ControlBindings controls = new();

    internal static int viewsize;

    internal static bool demorecord, demoplayback;
    // A demo's controls (Gameplay/DemoBuffer.cs)
    internal static readonly DemoBuffer demoBuffer = new();
    static byte[] demoData { get => demoBuffer.demoData; set => demoBuffer.demoData = value; }
    static int demoptr { get => demoBuffer.demoptr; set => demoBuffer.demoptr = value; }
    static int lastdemoptr { get => demoBuffer.lastdemoptr; set => demoBuffer.lastdemoptr = value; }


    //
    // current user input: gathered each frame by PollControls into localcmd, which is all the
    // game itself reads (through the player's PlayerInput) (Gameplay/InputPolling.cs)
    //
    static InputPolling inputPolling = null!;       // made in InitServices

    static InputPolling MakeInputPolling() => new(_inputManager, _consoleManager, _automapManager, controls, controlSettings,
        frameRefresh, demoBuffer, session,
        new InputPollingHooks(s => playstate = s, () => netgame, () => netChatting, () => netLeavePrompt,
            () => demoplayback, () => demorecord, () => demoTesting, RouteMovementToAutomap));

    static TicCmd localcmd => inputPolling.localcmd;
    static int controlx { get => inputPolling.controlx; set => inputPolling.controlx = value; }
    static int controly { get => inputPolling.controly; set => inputPolling.controly = value; }
    static int controlstrafe { get => inputPolling.controlstrafe; set => inputPolling.controlstrafe = value; }
    static int wheelnotches => inputPolling.wheelnotches;

    internal static void PollControls() => inputPolling.PollControls();
    static bool IsAutomapInput(InputCode code) => inputPolling.IsAutomapInput(code);
    internal static bool IsInputActive(InputCode code) => inputPolling.IsInputActive(code);
    static List<InputCode> TakeFreshPresses() => inputPolling.TakeFreshPresses();
    static int WheelNotches(InputCode code) => inputPolling.WheelNotches(code);

    // The mouse look and controller settings (Configuration/ControlSettings.cs)
    internal static readonly ControlSettings controlSettings = new();
    internal static bool mouselook { get => controlSettings.mouselook; set => controlSettings.mouselook = value; }
    internal static bool mouseinvert { get => controlSettings.mouseinvert; set => controlSettings.mouseinvert = value; }

    internal static int joydeadzone { get => controlSettings.joydeadzone; set => controlSettings.joydeadzone = value; }
    internal static int joyturnspeed { get => controlSettings.joyturnspeed; set => controlSettings.joyturnspeed = value; }
    internal static bool joyclassicsticks { get => controlSettings.joyclassicsticks; set => controlSettings.joyclassicsticks = value; }

    internal const int JOYTURNSPEEDS = ControlSettings.JOYTURNSPEEDS;

    static int lastgamemusicoffset = 0;

    /*
    =============================================================================

                                                     LOCAL VARIABLES

    =============================================================================
    */

    // The level's song (Gameplay/LevelMusic.cs)
    static LevelMusic levelMusic = null!;       // made in InitServices

    internal static void StartMusic() => levelMusic.StartMusic();
    internal static void ContinueMusic(int offs) => levelMusic.ContinueMusic(offs);
    internal static int StopMusic() => levelMusic.StopMusic();

    // The keys during play (Ui/PlayKeys.cs)
    static PlayKeys playKeys = null!;       // made in InitServices

    static PlayKeys MakePlayKeys() => new(_assetManager, _videoManager, _inputManager, _audioManager, _graphicManager, _gameEngineManager,
        _consoleManager, _automapManager, _inventoryManager, menuDrawing, inputPolling, statusBarDrawing, playerWeapons, playerClassRules,
        levelMusic, frameRefresh, session,
        new PlayKeysHooks(() => demoplayback, () => demorecord, () => netgame, () => netChatting, NetChatKeys, NetCheckKeys,
            ToggleAutomap, HandleAutomapKey, () => viewsize, DrawPlayBorder, DrawPlayBorderSides, () => DebugOk = 1,
            US_ControlPanel, DrawPlayScreen, () => startgame, () => loadedgame, s => playstate = s));

    internal static void CheckKeys() => playKeys.CheckKeys();

    static int funnyticount;

    internal static void PlayLoop()
    {
        playstate = playstatetypes.ex_stillplaying;
        lasttimecount = (int)GameEngineManager.GetTimeCount();
        frameon = 0;
        funnyticount = 0;
        _inputManager.InitButtonState();
        if (!netJoinResume)             // (just joined mid-level: kept as the host had them)
        {
            foreach (var p in players)  // every player's, so every machine starts the level alike
            {
                p.AngleFrac = 0;
                p.FaceCount = 0;
                p.Input.Reset();
            }
        }
        _videoManager.ClearPaletteShifts();
        if (netgame)
            NetLevelStart();            // counting frames from 0 (Program.NetPlay.cs)

        _inputManager.CenterMouse();

        _hudMessageManager.Clear();     // nothing left over from the last level, life or saved game

        if (demoplayback)
            _inputManager.StartAck();

        UpdateSoundListener();

        do
        {
            PollControls();

            // With con_pause on, an open console freezes the world but keeps drawing it.
            // CalcTics still advances lasttimecount each frame, so no time builds up to be
            // spent all at once when the console closes.
            // Not with others, though: the game goes on for them
            bool worldPaused = _consoleManager.IsPausingGame && !netgame;

            if (!worldPaused)
            {
                // this frame's controls, for each player to read as they think: with others,
                // everyone's as the host sent them (Program.NetPlay.cs); else this machine's for
                // the local player, and whatever came for everyone else
                if (netgame)
                {
                    if (!NetFrame())
                    {
                        playstate = playstatetypes.ex_abort;    // left the game, or the host is gone
                        continue;
                    }
                }
                else
                {
                    foreach (var p in players)
                        p.Input.Begin(ReferenceEquals(p, localplayer) ? localcmd : p.PendingCmd);
                }

                //
                // actor thinking
                //
                madenoise = false;
                MoveDoors();
                MovePWalls();

                // Every actor lives in _mapManager._actors. The player is at its head, so it still
                // thinks before every enemy, projectile and the BJ-victory actor.
                _mapManager.DoActors(tics);
                foreach (var p in players)    // pickups each player walked onto (Program.PlayerSight.cs)
                {
                    if (p.Pawn is { } pawn && Entities.Actors.Monster.IsTargetable(pawn))
                        using (ActAs(p))
                            TouchItems();
                }
                TickPlayerBodies(tics);     // what others see of the players, and
                camera.Tick(tics);          // a watched actor's view turning (Program.Camera.cs)
                _mapManager.AI.Tick();     // wall outlets and warp sites (Managers.LevelAI)
                TickZoneLights(tics);       // light zones' fades and effects (Program.ZoneLights.cs)
                RunTickHooks();             // the scripts' game.ontick hooks (Program.Scripting.cs)
                if (gamemode != GameMode.Single)
                    WakeSeenEnemies();      // what the renderer did alone (Program.Players.cs)
                if (gamemode == GameMode.Deathmatch)
                    TickDeathmatch();       // items back, and the time limit (Program.Deathmatch.cs)
                if (netgame)
                    NetFrameDone();

                _videoManager.UpdatePaletteShifts(tics);
                _hudMessageManager.Tick((int)tics);
            }

            UpdateAutomap();

            ThreeDRefresh();

            if (autosavePending)
                AutoSaveGame();         // now there's a picture of the level for it

            if (!worldPaused)
            {
                gamestate.TimeCount += (int)tics;
                gamestate.PlayTime += (int)tics;
            }

            UpdateSoundListener();      // JAB
            if (_videoManager.screenfaded)
                _videoManager.FadeIn();

            CheckKeys();

            _consoleManager.RunDeferred();      // console commands that need to run between frames

            //
            // debug aids
            //
            if (singlestep != 0)
            {
                GameEngineManager.WaitVBL(singlestep);
                lasttimecount = (int)GameEngineManager.GetTimeCount();
            }
            if (extravbls != 0)
                GameEngineManager.WaitVBL((uint)extravbls);

            if (demoplayback && !demoTesting)       // demotest plays each to its end
            {
                if (_inputManager.CheckAck())
                {
                    _inputManager.ClearKeysDown();
                    playstate = playstatetypes.ex_abort;
                }
            }
        }
        while (playstate == 0 && !startgame);

        // Intermission, death and menu screens don't draw the console, so don't leave it
        // capturing keys behind them (e.g. after a "map" command ends the level).
        _consoleManager.Close();
        _automapManager.Close();

        if (playstate != playstatetypes.ex_died)
            _videoManager.FinishPaletteShifts();
    }

    internal static void InitActorList()
    {
        //
        // the players are created first, so they sit at the head of _actors and think first
        //
        foreach (var p in players.Where(p => !p.Gone))     // not anyone who has left the game
            _mapManager.CreatePlayer(p);
    }


    internal const int MAXX = 320;
    internal const int MAXY = 160;

    /// <summary>Black text on the white of a <see cref="CenterWindow"/></summary>
    internal static readonly Fonts.TextStyle PromptStyle = new(SMALL_FONT, "Black", "White");

    /// <summary>A framed window of w by h tiles, centered in the play view</summary>
    internal static Fonts.TextWindow CenterWindow(int w, int h, Fonts.TextStyle style)
        => US_DrawWindow(((MAXX / 8) - w) / 2, ((MAXY / 8) - h) / 2, w, h, style);

/*
=============================================================================

                               USER CONTROL

=============================================================================
*/

}
