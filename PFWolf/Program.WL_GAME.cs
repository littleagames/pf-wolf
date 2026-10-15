using SDL2;
using PFWolf.Assets;
using PFWolf.Extensions;
using PFWolf.Managers;

namespace PFWolf;

internal partial class Program
{
    /*
=============================================================================

                             GLOBAL VARIABLES

=============================================================================
*/
    static bool ingame, fizzlein;
    internal static gametype gamestate { get => session.gamestate; set => session.gamestate = value; }
    static string bordercol = "VIEWCOLOR"; // color of the Change View/Ingame border
    /// <summary>
    /// A map switch asked for by A_ChangeMap (Spear's pickup), an elevator or a teleporter:
    /// GameLoop waits WaitTics (Spear lets the pickup sound play out), loads the map without an
    /// intermission and, with KeepPosition, puts the player back where they stood, facing the
    /// same way. With ElevatorWall, they're then moved into the elevator by that panel wall.
    /// </summary>
    private record PendingMapChange(string Map, bool KeepPosition, int X, int Y, short Angle, int WaitTics = 150, int ElevatorWall = 0);
    private static PendingMapChange? pendingMapChange;

    //
    // ELEVATOR BACK MAPS - REMEMBER (-1)!!
    //
    private static int[] ElevatorBackTo = { 1, 1, 7, 3, 5, 3 };

    //===========================================================================


    /// <summary>
    /// After an A_ChangeMap map loads: with KeepPosition, moves the player to where they were
    /// on the old map, facing the same way.
    /// </summary>
    private static void ApplyPendingMapChange()
    {
        if (pendingMapChange is not { } change)
            return;

        pendingMapChange = null;
        if (!change.KeepPosition)
            return;

        player.SetPosition(change.X >> (int)MapConstants.TILESHIFT, change.Y >> (int)MapConstants.TILESHIFT);
        player.X = change.X;
        player.Y = change.Y;
        player.Angle = change.Angle;
        Thrust(0, 0); // settles the player's tile and area at the new position

        if (change.ElevatorWall > 0)
            AlignPlayerInElevator(player.TileX, player.TileY, change.ElevatorWall);
    }

    internal static void GameLoop()
    {
        var language = _assetManager.GetText("en-us");
        bool died;
        bool warped = false;

        ClearMemory();
        _videoManager.FadeOut();
        DrawPlayScreen();
        died = false;
        do
        {
            if (!loadedgame)
                foreach (var p in players) p.score = p.oldscore;
            if (!died || viewsize != 21) DrawScore();

            startgame = false;
            string levelStart = "loaded";
            if (!loadedgame)
            {
                bool kept = EnterLevel();   // a kept hub level as it was left, or the map afresh (Program.Hubs.cs)
                ApplyPendingMapChange();
                ApplyPendingWarpStart();    // --start's tile, the first time (Program.cs)
                if (!died)
                    MarkLevelStart();       // what dying puts the player back to, with death-restores-level-start
                levelStart = died ? "restarted" : kept ? "returned" : "new";
            }
            RunLevelHooks(levelStart);      // the scripts' game.onlevel hooks (Program.Scripting.cs)

            // A level being entered, not one loaded or restarted after dying, saves itself
            autosavePending = !loadedgame && !died && !demoplayback && !demorecord && !netgame;
            DrawLevel();

            ingame = true;
            if (loadedgame)
            {
                ContinueMusic(lastgamemusicoffset);
                loadedgame = false;
            }
            else StartMusic();

            var levelStartMessage = _gameEngineManager.GetGameInfo().LevelStartMessage;
            if (warped)
            {
                warped = false;                 // an A_ChangeMap switch loads silently, no "get psyched!"
                if (!string.IsNullOrEmpty(levelStartMessage))
                    ShowLevelStartMessage(levelStartMessage);
            }
            else if (!died)
                PreloadGraphics();             // TODO: Let this do something useful!
            else
            {
                died = false;
                fizzlein = true;
            }

            DrawLevel ();

            PlayLoop();

            // The deathmatch has been won, or its time is up: who won, then back to the menus
            if (matchover)
            {
                StopMusic();
                ingame = false;
                ShowFinalScoreboard();
                matchover = false;
                return;
            }

            // Left the game with others, or lost the host: back to the menus
            if (NetGameOver)
            {
                StopMusic();
                ingame = false;
                ClearMemory();
                _videoManager.FadeOut();
                return;
            }

            if (playstate is playstatetypes.ex_completed or playstatetypes.ex_secretlevel or playstatetypes.ex_warped)
                LeaveLevel();               // a hub level is kept as it's left

            if (playstate == playstatetypes.ex_warped && pendingMapChange != null)
            {
                // Spear waits 150 tics for its pickup sound before the new map loads; the score
                // carries over, as the level ends without an intermission to bank it
                if (pendingMapChange.WaitTics > 0)
                    GameEngineManager.WaitVBL((uint)pendingMapChange.WaitTics);
                foreach (var p in players) p.oldscore = p.score;
                warped = true;
            }
            else
                pendingMapChange = null;

            StopMusic();
            ingame = false;

            if (demorecord && playstate != playstatetypes.ex_warped)
                FinishDemoRecord();

            if (pendingDemo != null || pendingRecord != null)
            {
                // playdemo or recorddemo ended the game: back to the title loop, which starts the demo
                ClearMemory();
                _videoManager.FadeOut();
                FindMenuItem(MainMenu, "savegame")?.active = 0;
                EnableViewScoresMenuItem();
                return;
            }

            if (startgame || loadedgame)
            {
                ClearMemory();
                _videoManager.FadeOut();
                DrawPlayScreen();
                died = false;
                warped = false;
                continue;
            }

            switch (playstate)
            {
                case playstatetypes.ex_completed:
                case playstatetypes.ex_secretlevel:
                    // A level with no next level (a boss floor left by exitlevel, or by a map's own
                    // exit) ends its cluster, as the victory tile does
                    var leftInfo = _gameEngineManager.GetGameInfo().Maps[gamestate.mapon];
                    var nextMap = playstate == playstatetypes.ex_secretlevel ? leftInfo.SecretNext ?? leftInfo.Next : leftInfo.Next;
                    if (string.IsNullOrEmpty(nextMap) && string.IsNullOrEmpty(leftInfo.EndMessage))
                    {
                        playstate = playstatetypes.ex_victorious;
                        goto case playstatetypes.ex_victorious;
                    }

                    if (viewsize == 21) DrawPlayScreen();
                    foreach (var p in players)
                    {
                        using var _ = ActAs(p);
                        _inventoryManager.ResetForNextLevel();
                    }
                    DrawKeys();
                    _videoManager.FadeOut();

                    ClearMemory();

                    if (!IsHubMap(levelMap))
                        LevelCompleted();          // do the intermission; a hub's levels are just left
                    if (viewsize == 21) DrawPlayScreen();
                    foreach (var p in players) p.oldscore = p.score;

                    // The Spear demo's last floor: its message ends the game (shown at the end of
                    // the intermission), then the high scores
                    if (!string.IsNullOrEmpty(leftInfo.EndMessage))
                    {
                        died = true;                // don't "get psyched!" next time
                        _videoManager.FadeOut();
                        ClearMemory();
                        CheckHighScore(playerstate.score, won: false);
                        EnableViewScoresMenuItem();
                        return;
                    }

                    gamestate.mapon = nextMap;      // the secret exit falls back to next if there's no secret-next
                    break;

                case playstatetypes.ex_died:
                    Died();
                    died = true;                    // don't "get psyched!"

                    if (playerstate.lives > -1)
                        break;                          // more lives left

                    _videoManager.FadeOut();
                    if (_videoManager.HasMargins)
                        _videoManager.ClearScreen(0);
                    ClearMemory();

                    // The pack's game over screen, unless the game was ended from the menu
                    if (!endedFromMenu && _gameEngineManager.GetGameInfo().LoseScreen is { } loseScreen)
                        ShowLoseScreen(loseScreen);
                    endedFromMenu = false;

                    CheckHighScore(playerstate.score, won: false);
                    EnableViewScoresMenuItem();
                    return;

                case playstatetypes.ex_victorious:
                    if (CurrentEpisode()?.EndBriefing is { Length: > 0 } endBriefing)
                    {
                        // Blake Stone: the mission's won message over the status bar, then its
                        // end briefing, then the high scores
                        if (_gameEngineManager.GetGameInfo().MissionWonMessage is { Length: > 0 } wonMessage
                            && PresenterScript(wonMessage) is { } wonScript)
                        {
                            PresenterMessageBox(wonScript);
                            _videoManager.Update();
                            _audioManager.Play("bs/bonus1");
                            _inputManager.UserInput(5 * 70);
                        }
                        _videoManager.FadeOut();
                        ClearMemory();
                        if (CurrentEpisode()?.EndMovie is { Length: > 0 } endMovie)
                        {
                            PlayMovie(endMovie);
                            _videoManager.FadeOut();
                        }
                        ShowBriefing(endBriefing);

                        CheckHighScore(playerstate.score, won: true);
                        EnableViewScoresMenuItem();
                        return;
                    }

                    if (viewsize == 21) DrawPlayScreen();
                    // A cluster with a victory-fade-color fades to it slowly, as Spear does when the Angel falls
                    var wonCluster = WonCluster();
                    if (string.IsNullOrEmpty(wonCluster.VictoryFadeColor))
                        _videoManager.FadeOut();
                    else
                        _videoManager.FadeOut(VictoryFadeColor(wonCluster), 300);
                    ClearMemory();

                    Victory();

                    ClearMemory();

                    CheckHighScore(playerstate.score, won: true);
                    EnableViewScoresMenuItem();
                    return;

                default:
                    if (viewsize == 21) DrawPlayScreen();
                    ClearMemory();
                    break;
            }
        } while (true);
    }

    // Demos: played, recorded and asked for from the console (Gameplay/Demos.cs)
    static Demos demos = null!;     // made in InitServices

    static Demos MakeDemos() => new(_assetManager, _gameEngineManager, _videoManager, _audioManager, lineInput, demoBuffer, levelMusic, levelLoop,
        new DemosHooks((skill, episode, map, playerClass) => NewGame(skill, episode, map, playerClass), () => DemoSkill, DrawPlayScreen,
            SetupGameLevel, RunLevelHooks, () => startgame = false, on => demoplayback = on, on => demorecord = on, () => demoTesting,
            DemoTestReport, () => fizzlein = true, () => INTROSONG));

    internal static int? pendingDemo { get => demos.pendingDemo; set => demos.pendingDemo = value; }
    internal static DemoRecordRequest? pendingRecord { get => demos.pendingRecord; set => demos.pendingRecord = value; }

    internal static void PlayDemo(int demonumber) => demos.PlayDemo(demonumber);
    internal static bool DemoExists(int demonumber) => demos.DemoExists(demonumber);
    internal static void PlayPendingDemo() => demos.PlayPendingDemo();
    internal static void FinishDemoRecord(int? demonumber = null) => demos.FinishDemoRecord(demonumber);
    internal static void RecordDemo() => demos.RecordDemo();
    internal static int DemoMapCount() => demos.DemoMapCount();
    internal static bool RecordPendingDemo() => demos.RecordPendingDemo();
    internal static void RecordDemo(int level, int? demonumber = null) => demos.RecordDemo(level, demonumber);

    // The screen around the view: status bar, border and "get psyched" (Ui/PlayScreen.cs)
    static PlayScreen playScreen = null!;       // made in InitServices

    static PlayScreen MakePlayScreen() => new(_assetManager, _gameEngineManager, _videoManager, _graphicManager, _inputManager,
        statusBarDrawing, playerWeapons, radar, render,
        new PlayScreenHooks(() => viewsize, () => ingame, () => bordercol, ShowLevelStartMessage));

    internal static void DrawPlayScreen() => playScreen.DrawPlayScreen();
    internal static void DrawPlayBorder() => playScreen.DrawPlayBorder();
    internal static void DrawPlayBorderSides() => playScreen.DrawPlayBorderSides();
    internal static void DrawStatusBorder(string color) => playScreen.DrawStatusBorder(color);
    internal static string GetMapDisplayName(string mapon) => playScreen.GetMapDisplayName(mapon);
    internal static void PreloadGraphics() => playScreen.PreloadGraphics();

    internal static void SetupGameLevel()
    {
        foreach (var p in players)
        {
            p.Pitch = 0;                    // each level, and each loaded game, starts looking straight ahead
            p.EyeZ = StandingEyeZ(p);       // from standing height
        }
        camera.FollowPlayer();              // through the player's eyes
        weaponBob.Init();

        if (!loadedgame)
        {
            gamestate.TimeCount =
            gamestate.secrettotal =
            gamestate.killtotal =
            gamestate.treasuretotal =
            gamestate.secretcount =
            gamestate.killcount =
            gamestate.treasurecount = 0;
            ClearPushWalls();
            foreach (var p in players)
            {
                p.WeaponSprite = null;      // the weapon in hand starts on its Ready state
                p.FaceTimes = 0;
                p.LastAttacker = null;
            }
        }

        if (demoplayback || demorecord)
            US_InitRndT(false);
        else if (netgame)
            rndindex = netseed;         // where the host said, the same on every machine
        else
            US_InitRndT(true);

        //
        // load the level
        //
        DeathmatchLevelSetup();         // with or without its enemies (Program.Deathmatch.cs)
        //int mapnum = gamestate.mapon + 10 * gamestate.cluster;
        _mapManager.LoadMap(gamestate.mapon, gamestate.difficulty, CurrentSkill.EnemyHealth,
            _gameEngineManager.GetGameInfo().Maps.TryGetValue(gamestate.mapon, out var loadingMap) ? loadingMap.FloorNumber : -1);

        // The cluster follows the map, however it was reached: a new game, the next/secret
        // level, a map-change trigger or the console's "map" warp
        if (_gameEngineManager.GetGameInfo().Maps.TryGetValue(gamestate.mapon, out var mapInfo))
            gamestate.cluster = mapInfo.Cluster;

        wallstories = Math.Clamp(mapInfo?.WallHeight ?? _gameEngineManager.GetGameInfo().DefaultMap.WallHeight,
            1, MAXWALLSTORIES);
        levelsky = mapInfo?.Sky ?? _gameEngineManager.GetGameInfo().DefaultMap.Sky;
        SetWeather(mapInfo?.Weather ?? _gameEngineManager.GetGameInfo().DefaultMap.Weather);
        _mapManager.BuildFlats(mapInfo?.DefaultFloor ?? _gameEngineManager.GetGameInfo().DefaultMap.DefaultFloor,
            mapInfo?.DefaultCeiling ?? _gameEngineManager.GetGameInfo().DefaultMap.DefaultCeiling);

        //
        // spawn doors
        //
        InitActorList();                       // start spawning things with a clean slate
        InitDoorList();
        //InitStaticList();


        int x, y;
        var doors = _mapManager.GetMapData().Doors;
        for (y = 0; y < _mapManager.mapheight; y++)
        {
            for (x = 0; x < _mapManager.mapwidth; x++)
            {
                // door (a plane 0 tile listed in the mapdefs doors)
                if (doors.TryGetValue(_mapManager.MAPSPOT(x, y, 0), out var doorXlat))
                    SpawnDoor(x, y, doorXlat.Vertical, doorXlat);
            }
        }

        //
        // spawn the player (MapManager.LoadMap already spawned every other thing and counted
        // the secret pushwalls, from the mapdefs)
        //
        if (_mapManager.PlayerStart is { } start)
            SpawnPlayers(start.TileX, start.TileY, start.Angle);
        else
        {
            var (standx, standy) = StandInStart();
            Console.WriteLine($"{gamestate.mapon} has no mapdefs player start; starting on {standx},{standy}");
            SpawnPlayers(standx, standy, 90);
        }

        //
        // take out the ambush markers
        //
        for (y = 0; y < _mapManager.mapheight; y++)
        {
            for (x = 0; x < _mapManager.mapwidth; x++)
            {
                var tile = _mapManager.MAPSPOT(x, y, 0);

                if (tile == _mapManager.Floors.AmbushTile)
                {
                    if (_mapManager.VALIDAREA(_mapManager.MAPSPOT(x + 1, y, 0)))
                        tile = (ushort)_mapManager.MAPSPOT(x + 1, y, 0);
                    if (_mapManager.VALIDAREA(_mapManager.MAPSPOT(x, y - 1, 0)))
                        tile = (ushort)_mapManager.MAPSPOT(x, y - 1, 0);
                    if (_mapManager.VALIDAREA(_mapManager.MAPSPOT(x, y + 1, 0)))
                        tile = (ushort)_mapManager.MAPSPOT(x, y + 1, 0);
                    if (_mapManager.VALIDAREA(_mapManager.MAPSPOT(x - 1, y, 0)))
                        tile = (ushort)_mapManager.MAPSPOT(x - 1, y, 0);

                    _mapManager.SetMapSpot(x, y, 1, 0);
                }
            }
        }

        InitLevelShading(mapInfo);
        _mapManager.AI.OnLevelStart();      // wall outlets, Goldfire's spawn sites, once-a-level drops (Managers.LevelAI)

        //
        // load floor/ceiling textures
        //
#if USE_FLOORCEILINGTEXT && !USE_MULTIFLATS
        GetFlatTextures();
#endif

#if USE_PARALLAX
    SetParallaxStartTexture();
#endif
        //
        // have the caching manager load and purge stuff to make sure all marks
        // are in memory
        //
        //CA_LoadAllSounds();
    }

    internal static void Died()
    {
        float fangle;
        int dx, dy;
        int iangle, curangle, clockwise, counter, change;

        if (_videoManager.screenfaded)
        {
            ThreeDRefresh();
            _videoManager.FadeIn();
        }

        playerstate.weapon = null;                     // take away weapon
        playerpitch = 0;                             // and face the attacker straight on
        camera.FollowPlayer();                       // seen through their own eyes
        if (_inventoryManager.GetStringProperty(PlayerClass, "deathsound") is { Length: > 0 } deathSound)
            _audioManager.Play(deathSound);     // the Player class's deathsound
        ShowObituary();

        //
        // swing around to face attacker
        //
        if (LastAttacker != null)
        {
            dx = LastAttacker.X - player.X;
            dy = player.Y - LastAttacker.Y;

            fangle = (float)Math.Atan2((float)dy, (float)dx);     // returns -pi to pi
            if (fangle < 0)
                fangle = (float)(M_PI * 2 + fangle);

            iangle = (int)(fangle / (M_PI * 2) * ANGLES);
        }
        else
        {
            iangle = player.Angle + ANGLES / 2;
            if (iangle >= ANGLES) iangle -= ANGLES;
        }

        if (player.Angle > iangle)
        {
            counter = player.Angle - iangle;
            clockwise = ANGLES - player.Angle + iangle;
        }
        else
        {
            clockwise = iangle - player.Angle;
            counter = player.Angle + ANGLES - iangle;
        }

        curangle = player.Angle;

        //
        // rotate the shorter way round, while the view drops (game-info death-drop-height)
        //
        bool rotateclockwise = clockwise < counter;
        if (rotateclockwise && curangle > iangle)
            curangle -= ANGLES;
        else if (!rotateclockwise && curangle < iangle)
            curangle += ANGLES;

        int dropto = deathDropHeight ?? playereyez;
        do
        {
            if (curangle != iangle)
            {
                change = rotateclockwise ? (int)(tics * deathTurnSpeed) : -(int)tics * deathTurnSpeed;
                if (rotateclockwise ? curangle + change > iangle : curangle + change < iangle)
                    change = iangle - curangle;

                curangle += change;
                player.Angle += (short)change;
                if (player.Angle >= ANGLES)
                    player.Angle -= ANGLES;
                else if (player.Angle < 0)
                    player.Angle += ANGLES;
            }

            if (playereyez > dropto)
                playereyez = Math.Max(playereyez - (int)tics * deathDropSpeed, dropto);

            ThreeDRefresh();
            CalcTics();
            _hudMessageManager.Tick((int)tics);
        } while (curangle != iangle || playereyez > dropto);

        //
        // fade to red
        //
        _videoManager.FinishPaletteShifts();

        _videoManager.BarScaledCoord(viewscreenx, viewscreeny, viewwidth, viewheight, deathFadeColor);
        DrawHudMessages();      // the obituary stays up on the red, through the fade and the wait after it

        _inputManager.ClearKeysDown();

        _videoManager.Transition(deathFadeStyle, viewscreenx, viewscreeny, viewwidth, viewheight, (uint)deathFadeTics);

        _inputManager.UserInput((uint)deathHoldTics);
        _audioManager.WaitSoundDone();
        ClearMemory();
        playereyez = StandingEyeZ(playerstate);      // back on their feet for the restart

        playerstate.lives--;

        if (playerstate.lives > -1)
        {
            // Back as they came into the level (Blake Stone), or fresh with the starting items
            if (!_gameEngineManager.GetGameInfo().DeathRestoresLevelStart || !RestoreLevelStart())
            {
                playerstate.health = StartingHealth;
                GiveStartingInventory();
            }
            ClearPushWalls();
            weaponSprite = null;            // the weapon in hand starts on its Ready state

            if (viewsize != 21)
            {
                DrawKeys();
                DrawWeapon();
                DrawAmmo();
                DrawHealth();
                DrawArmor();
                DrawFace();
                DrawLives();
            }
        }
    }
}
