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

    static string lastmusicchunk = "";

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

    internal static void StartMusic()
    {
        //_audioManager.SetPaused(true);
        var gameInfo = _gameEngineManager.GetGameInfo();
        var song = gameInfo.Maps[gamestate.mapon].Music;
        lastmusicchunk = song;
        if (!string.IsNullOrEmpty(song))        // a map with no music keeps quiet
            _audioManager.PlayMusic(lastmusicchunk);
    }

    // Music offsets aren't tracked, so this resumes the level's song if it's the one paused, and
    // otherwise (after the menu or a loaded game replaced it) starts it over.
    internal static void ContinueMusic(int offs)
    {
        var gameInfo = _gameEngineManager.GetGameInfo();
        var song = gameInfo.Maps[gamestate.mapon].Music;
        lastmusicchunk = song;
        if (string.Equals(_audioManager.CurrentMusicTrack, song, StringComparison.OrdinalIgnoreCase))
            _audioManager.SetPaused(false);
        else if (!string.IsNullOrEmpty(song))
            _audioManager.PlayMusic(song);
    }

    internal static int StopMusic()
    {
        _audioManager.SetPaused(true);
        return 0;
    }

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

    internal static void CheckKeys()
    {
        var language = _assetManager.GetText("en-us");
        ScanCodes scan;

        if (_videoManager.screenfaded || demoplayback)    // don't do anything with a faded screen
        {
            while (_inputManager.TryTakePressedKey(out _)) { }     // nor fire binds for these keys later
            return;
        }

        //
        // command console: it only opens from here, so it's never up during a blocking menu or
        // screen with nothing drawing it. Once open, InputManager routes all keys to it.
        //
        if (_consoleManager.IsOpen)
            return;

        // Typing something to say to the others: the keys are the line's (Program.NetPlay.cs)
        if (netChatting)
        {
            NetChatKeys();
            return;
        }

        if (_inputManager.IsKeyDown(ScanCodes.sc_Grave) && !demorecord)
        {
            _inputManager.ClearKeysDown();
            _inputManager.ClearTextInput();
            _automapManager.Close();
            _consoleManager.Open();
            return;
        }

        //
        // automap
        //
        if (_inputManager.IsButtonPressed(buttontypes.bt_automap) && !_inputManager.IsButtonHeld(buttontypes.bt_automap))
            ToggleAutomap();

        //
        // console binds: run the command bound to each key or button pressed since the last
        // frame (never while recording, or the demo wouldn't match what was played)
        //
        var pressedInputs = new List<InputCode>();
        while (_inputManager.TryTakePressedKey(out var pressed))
            pressedInputs.Add(InputCode.FromKey(pressed));
        pressedInputs.AddRange(TakeFreshPresses());

        foreach (var input in pressedInputs)
        {
            if (HandleAutomapKey(input))        // the automap's own keys, while it's open
                continue;

            if (!demorecord && _consoleManager.Binds.TryGetValue(input, out var boundCommand))
                _consoleManager.Execute(boundCommand);
        }

        scan = _inputManager.GetLastKeyPressed();

        // With others: no cheat keys, no pausing and no menus over the game; Esc asks to leave
        if (netgame)
        {
            NetCheckKeys(scan);
            return;
        }

        //
        // SECRET CHEAT CODE: 'MLI'
        //
        if (_inputManager.IsKeyDown(ScanCodes.sc_M) && _inputManager.IsKeyDown(ScanCodes.sc_L) && _inputManager.IsKeyDown(ScanCodes.sc_I))
        {
            playerstate.health = MaxHealth;
            _inventoryManager.Give("GoldKey", 1);
            _inventoryManager.Give("SilverKey", 1);
            playerstate.score = 0;
            gamestate.TimeCount += (int)42000L;
            // the best weapon there is (the gatling gun)
            if (AllWeapons().OrderBy(WeaponSelectionOrder).FirstOrDefault() is { } bestWeapon)
                GiveWeapon(bestWeapon);
            GiveAllAmmo(99);
            DrawWeapon();
            DrawHealth();
            DrawKeys();
            DrawAmmo();
            DrawScore();

            ClearMemory();

            Message("$STR_CHEATER1".ToLanguageText(language) + "\n" +
                    "$STR_CHEATER2".ToLanguageText(language) + "\n\n" +
                    "$STR_CHEATER3".ToLanguageText(language) + "\n" +
                    "$STR_CHEATER4".ToLanguageText(language) + "\n" +
                    "$STR_CHEATER5".ToLanguageText(language), MAXY);

            _inputManager.ClearKeysDown();
            _inputManager.Ack();

            if (viewsize < 17)
                DrawPlayBorder();
        }

        //
        // OPEN UP DEBUG KEYS
        //
        if (_inputManager.IsKeyDown(ScanCodes.sc_BackSpace) && _inputManager.IsKeyDown(ScanCodes.sc_LShift) && _inputManager.IsKeyDown(ScanCodes.sc_Alt))
        {
            ClearMemory();

            Message("Cheat commands are\nnow available!\nPress ` for the console.", MAXY);
            _inputManager.ClearKeysDown();
            _inputManager.Ack();

            DrawPlayBorderSides();
            DebugOk = 1;
        }

        //
        // TRYING THE KEEN CHEAT CODE!
        //
        if (_inputManager.IsKeyDown(ScanCodes.sc_B) && _inputManager.IsKeyDown(ScanCodes.sc_A) && _inputManager.IsKeyDown(ScanCodes.sc_T))
        {
            ClearMemory();

            Message("Commander Keen is also\n" +
                        "available from Apogee, but\n" +
                        "then, you already know\n" +
                        "that - right, Cheatmeister?!", MAXY);

            _inputManager.ClearKeysDown();
            _inputManager.Ack();

            if (viewsize < 18)
                DrawPlayBorder();
        }

        //
        // pause key weirdness can't be checked as a scan code
        //
        if (_inputManager.IsButtonPressed(buttontypes.bt_pause))
            _gameEngineManager.SetPaused(true);
        if (_gameEngineManager.IsPaused())
        {
            int lastoffs = StopMusic();
            _graphicManager.DrawPic("paused", 16 * 8, 80 - 2 * 8);
            _videoManager.Update();
            _inputManager.Ack();
            _gameEngineManager.SetPaused(false);
            ContinueMusic(lastoffs);
            _inputManager.CenterMouse();
            lasttimecount = (int)GameEngineManager.GetTimeCount();
            return;
        }
        if (scan == ScanCodes.sc_F10 ||
            scan == ScanCodes.sc_F9 || scan == ScanCodes.sc_F7 || scan == ScanCodes.sc_F8)     // pop up quit dialog
        {
            _automapManager.Close();
            ClearMemory();
            US_ControlPanel(scan);

            DrawPlayBorderSides();

            _inputManager.ClearKeysDown();
            return;
        }

        if ((scan >= ScanCodes.sc_F1 && scan <= ScanCodes.sc_F9) || scan == ScanCodes.sc_Escape || _inputManager.IsButtonPressed(buttontypes.bt_esc))
        {
            int lastoffs = StopMusic();
            _automapManager.Close();
            ClearMemory();
            _videoManager.FadeOut();

            US_ControlPanel(_inputManager.IsButtonPressed(buttontypes.bt_esc) ? ScanCodes.sc_Escape : scan);

            _inputManager.ClearKeysDown();
            _videoManager.FadeOut();
            if (viewsize != 21)
                DrawPlayScreen();
            if (!startgame && !loadedgame)
                ContinueMusic(lastoffs);
            if (loadedgame)
                playstate = playstatetypes.ex_abort;
            lasttimecount = (int)GameEngineManager.GetTimeCount();
            _inputManager.CenterMouse();
            return;
        }
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
