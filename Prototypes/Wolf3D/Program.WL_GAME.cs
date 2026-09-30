using SDL2;
using Wolf3D.Assets;
using Wolf3D.Extensions;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    /*
=============================================================================

                             GLOBAL VARIABLES

=============================================================================
*/
    static bool ingame, fizzlein;
    internal static gametype gamestate = new gametype();
    static string bordercol = "VIEWCOLOR"; // color of the Change View/Ingame border
    /// <summary>
    /// A map switch asked for by A_ChangeMap (Spear's pickup): GameLoop lets the pickup sound play
    /// out, loads the map without an intermission and, with KeepPosition, puts the player back
    /// where they stood, facing the same way.
    /// </summary>
    private record PendingMapChange(string Map, bool KeepPosition, int X, int Y, short Angle);
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
                gamestate.score = gamestate.oldscore;
            if (!died || viewsize != 21) DrawScore();

            startgame = false;
            if (!loadedgame)
            {
                SetupGameLevel();
                ApplyPendingMapChange();
            }

            // A level being entered, not one loaded or restarted after dying, saves itself
            autosavePending = !loadedgame && !died && !demoplayback && !demorecord;
            DrawLevel();

            ingame = true;
            if (loadedgame)
            {
                ContinueMusic(lastgamemusicoffset);
                loadedgame = false;
            }
            else StartMusic();

            if (warped)
                warped = false;                 // an A_ChangeMap switch loads silently, no "get psyched!"
            else if (!died)
                PreloadGraphics();             // TODO: Let this do something useful!
            else
            {
                died = false;
                fizzlein = true;
            }

            DrawLevel ();

            PlayLoop();

            if (playstate == playstatetypes.ex_warped && pendingMapChange != null)
            {
                // Spear waits 150 tics for its pickup sound before the new map loads; the score
                // carries over, as the level ends without an intermission to bank it
                GameEngineManager.WaitVBL(150);
                gamestate.oldscore = gamestate.score;
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
                    if (viewsize == 21) DrawPlayScreen();
                    _inventoryManager.ResetForNextLevel();
                    DrawKeys();
                    _videoManager.FadeOut();

                    ClearMemory();

                    LevelCompleted();              // do the intermission
                    if (viewsize == 21) DrawPlayScreen();
                    gamestate.oldscore = gamestate.score;

                    var gameInfo = _gameEngineManager.GetGameInfo();
                    var mapInfo = gameInfo.Maps[gamestate.mapon];
                    if (playstate == playstatetypes.ex_secretlevel)
                    {
                        gamestate.mapon = mapInfo.SecretNext ?? mapInfo.Next; // Falls back if no secretnext defined
                    }
                    else
                    {
                        gamestate.mapon = mapInfo.Next;
                    }
                    break;

                case playstatetypes.ex_died:
                    Died();
                    died = true;                    // don't "get psyched!"

                    if (gamestate.lives > -1)
                        break;                          // more lives left

                    _videoManager.FadeOut();
                    if (_videoManager.screenHeight % 200 != 0)
                        _videoManager.ClearScreen(0);
                    ClearMemory();

                    CheckHighScore(gamestate.score, won: false);
                    EnableViewScoresMenuItem();
                    return;

                case playstatetypes.ex_victorious:
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

                    CheckHighScore(gamestate.score, won: true);
                    EnableViewScoresMenuItem();
                    return;

                default:
                    if (viewsize == 21) DrawPlayScreen();
                    ClearMemory();
                    break;
            }
        } while (true);
    }

    internal static void PlayDemo(int demonumber)
    {
        short length;

        // A demo recorded with that number plays in place of the game's own
        var recorded = ReadRecordedDemo(demonumber);
        if (recorded != null)
            demoData = recorded;
        else
        {
            var demoAsset = _assetManager.Find<DemoAsset>($"demo{demonumber}");
            if (demoAsset == null)
                return;

            demoData = demoAsset.RawData;
        }
        demoptr = 0;

        // id's header: the floor in the first episode (0 = MAP01), a 16-bit length counting the
        // header, and a pad byte. Every demo plays on game-info's demo-skill (the hardest, as id's did).
        if (demoData.Length < 4)
            return;

        var mapName = $"MAP{demoData[0] + 1:D2}";
        if (!_gameEngineManager.GetGameInfo().Maps.TryGetValue(mapName, out var mapInfo))
            return;

        length = BitConverter.ToInt16(demoData, 1);
        demoptr = 4;
        lastdemoptr = Math.Min((int)length, demoData.Length);   // stop at the data's end if the length overshoots
        if (lastdemoptr - demoptr < 3)
            return;

        NewGame(DemoSkill, new EpisodeInfo { StartMap = mapName }, mapInfo, BasePlayerClass);   // recorded as Player: another class's stats would change how it plays

        _videoManager.FadeOut();

        DrawPlayScreen();

        startgame = false;
        demoplayback = true;

        SetupGameLevel();
        StartMusic();

        PlayLoop();

        demoplayback = false;

        StopMusic();
        ClearMemory();
    }

    internal const int MAXDEMOSIZE = 8192;

    /// <summary>
    /// A demo asked for by playdemo; the title loop plays it next (a game in progress ends first)
    /// </summary>
    internal static int? pendingDemo;

    /// <summary>Whether demo number <paramref name="demonumber"/> has been recorded or comes with the game</summary>
    internal static bool DemoExists(int demonumber)
        => File.Exists(DemoFilePath(demonumber)) || _assetManager.Exists<DemoAsset>($"demo{demonumber}");

    /// <summary>
    /// Plays the demo playdemo asked for, if any, leaving the screen faded and the title music on
    /// </summary>
    internal static void PlayPendingDemo()
    {
        if (pendingDemo is not int demonumber)
            return;

        pendingDemo = null;
        PlayDemo(demonumber);
        _videoManager.FadeOut();
        if (_videoManager.screenHeight % 200 != 0)
            _videoManager.ClearScreen(0x00);
        StartCPMusic(INTROSONG);
    }

    /// <summary>A recorded demo's file: DEMO0.dmo to DEMO9.dmo in the demos folder</summary>
    private static string DemoFilePath(int demonumber)
        => Path.Combine(_gameEngineManager.ConfigDirectories.DemosDirectory ?? "", $"DEMO{demonumber}.dmo");

    private static byte[]? ReadRecordedDemo(int demonumber)
    {
        var path = DemoFilePath(demonumber);
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't read the demo {path}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Starts recording in id's format: the map's number less one (0 = MAP01) in the first byte,
    /// then room for the length, then 3 bytes a frame from PollControls
    /// </summary>
    internal static void StartDemoRecord(int mapIndex)
    {
        demoData = new byte[MAXDEMOSIZE];
        demoData[0] = (byte)mapIndex;
        demoptr = 4;                            // leave space for length
        lastdemoptr = MAXDEMOSIZE;
        demorecord = true;
    }

    /// <summary>
    /// Ends the recording and saves it as demo <paramref name="demonumber"/>, or asks which
    /// number to save it as when none was given (Esc throws it away)
    /// </summary>
    internal static void FinishDemoRecord(int? demonumber = null)
    {
        int length;

        demorecord = false;

        // The length counts the 4 header bytes; the byte after it is padding
        length = demoptr;
        demoData[1] = (byte)length;
        demoData[2] = (byte)(length >> 8);
        demoData[3] = 0;

        if (demonumber == null)
        {
            _videoManager.FadeIn();
            var window = CenterWindow(24, 3, PromptStyle);
            window.PrintY += 6;
            window.Print(" Demo number (0-9): ");
            _videoManager.Update();

            string str = "";
            if (US_LineInput(window.PrintX, window.PrintY, ref str, "", true, 1, 0, window.Style)
                && int.TryParse(str, out int typed) && typed >= 0 && typed <= 9)
                demonumber = typed;
        }

        if (demonumber is int number)
            SaveDemo(number, demoData[..length]);

        demoData = [];
    }

    private static void SaveDemo(int demonumber, byte[] data)
    {
        var path = DemoFilePath(demonumber);
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, data);
            Console.WriteLine($"Recorded demo saved to {path}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Couldn't save the demo to {path}: {e.Message}");
        }
    }

    //==========================================================================

    /*
    ==================
    =
    = RecordDemo
    =
    = Fades the screen out, then starts a demo.  Exits with the screen faded
    =
    ==================
    */
    internal static void RecordDemo()
    {
        int maps = DemoMapCount();
        if (maps == 0)
            return;

        // Clear what's behind: a title drawn in its own palette (Spear's) is noise in the game's
        _videoManager.ClearScreen(0);
        var window = CenterWindow(26, 3, PromptStyle);
        window.PrintY += 6;
        window.Print($"  Demo which level(1-{maps}): ");
        _videoManager.Update();
        _videoManager.FadeIn();
        string str = "";
        var esc = !US_LineInput(window.PrintX, window.PrintY, ref str, "", true, 2, 0, window.Style);
        if (esc || !int.TryParse(str, out int level) || level < 1 || level > maps)
            return;

        RecordDemo(level);
    }

    /// <summary>
    /// How many maps a demo can be recorded on: a demo names its map by one byte, so it can be
    /// any of MAP01 to MAP99 the game has, counting up from MAP01
    /// </summary>
    internal static int DemoMapCount()
    {
        var gameInfo = _gameEngineManager.GetGameInfo();
        int maps = 0;
        while (maps < 99 && gameInfo.Maps.ContainsKey($"MAP{maps + 1:D2}"))
            maps++;
        return maps;
    }

    /// <summary>
    /// A recording asked for by recorddemo; the title loop starts it next (a game in progress ends first)
    /// </summary>
    internal record DemoRecordRequest(int Level, int? DemoNumber);
    internal static DemoRecordRequest? pendingRecord;

    /// <summary>
    /// Starts the recording recorddemo asked for, if any. Returns whether one ran.
    /// </summary>
    internal static bool RecordPendingDemo()
    {
        if (pendingRecord is not { } request)
            return false;

        pendingRecord = null;
        RecordDemo(request.Level, request.DemoNumber);
        return true;
    }

    /// <summary>
    /// Records a demo of MAP<paramref name="level"/> on game-info's demo-skill until the level ends, then
    /// saves it as <paramref name="demonumber"/>, or asks for a number when none is given
    /// </summary>
    internal static void RecordDemo(int level, int? demonumber = null)
    {
        var gameInfo = _gameEngineManager.GetGameInfo();
        var mapName = $"MAP{level:D2}";
        _videoManager.FadeOut();
        NewGame(DemoSkill, new EpisodeInfo { StartMap = mapName }, gameInfo.Maps[mapName], BasePlayerClass);
        StartDemoRecord(level - 1);

        DrawPlayScreen();
        _videoManager.FadeIn();

        startgame = false;
        demorecord = true;

        SetupGameLevel();
        StartMusic();

        fizzlein = true;

        PlayLoop();

        demoplayback = false;

        StopMusic();
        _videoManager.FadeOut();
        ClearMemory();

        FinishDemoRecord(demonumber);
    }

    internal static void DrawPlayScreen()
    {
        if (StatusBar.Get("background")?.Pic is { Length: > 0 } statusbarpic)
            _graphicManager.DrawPic(statusbarpic, 0, 200 - STATUSLINES); // TODO: Orientation: Bottom/Centered
        DrawPlayBorder();

        DrawFace();
        DrawHealth();
        DrawArmor();
        DrawLives();
        DrawLevel();
        DrawAmmo();
        DrawKeys();
        DrawWeapon();
        DrawScore();
    }

    internal static void DrawPlayBorder()
    {
        int px = _videoManager.scaleFactor;

        if (bordercol != "VIEWCOLOR")
            DrawStatusBorder(bordercol);
        else
        {
            // Beside the status bar: from each screen edge to the border's sides into the picture
            int statusborderw = (_videoManager.screenWidth - px * 320) / 2;
            int sides = statusborderw + px * (StatusBar.Get("border")?.Sides ?? 0);
            if (sides > 0 && STATUSLINES > 0)
            {
                _videoManager.BarScaledCoord(0, _videoManager.screenHeight - px * STATUSLINES,
                    sides, px * STATUSLINES, bordercol);
                _videoManager.BarScaledCoord(_videoManager.screenWidth - sides, _videoManager.screenHeight - px * STATUSLINES,
                    sides, px * STATUSLINES, bordercol);
            }
        }

        if (viewheight == _videoManager.screenHeight) return;

        _videoManager.BarScaledCoord(0, 0, _videoManager.screenWidth, _videoManager.screenHeight - px * STATUSLINES, bordercol);

        int xl = _videoManager.screenWidth / 2 - viewwidth / 2;
        int yl = (_videoManager.screenHeight - px * STATUSLINES - viewheight) / 2;
        _videoManager.BarScaledCoord(xl, yl, viewwidth, viewheight, "Black");

        if (xl != 0)
        {
            // Paint game view border lines
            _videoManager.BarScaledCoord(xl - px, yl - px, viewwidth + px, px, "Black");                      // upper border
            _videoManager.BarScaledCoord(xl, yl + viewheight, viewwidth + px, px, bordercol);// - 2);       // lower border
            _videoManager.BarScaledCoord(xl - px, yl - px, px, viewheight + px, "Black");                     // left border
            _videoManager.BarScaledCoord(xl + viewwidth, yl - px, px, viewheight + 2 * px, bordercol);// - 2);  // right border
            _videoManager.BarScaledCoord(xl - px, yl + viewheight, px, px, bordercol);// - 3);              // lower left highlight
        }
        else
        {
            // Just paint a lower border line
            _videoManager.BarScaledCoord(0, yl + viewheight, viewwidth, px, bordercol);// - 2);       // lower border
        }
    }

    internal static void DrawPlayBorderSides()
    {
        if (viewsize == 21) return;

        int sw = _videoManager.screenWidth;
        int sh = _videoManager.screenHeight;
        int vw = viewwidth;
        int vh = viewheight;
        int px = _videoManager.scaleFactor; // size of one "pixel"

        int h = sh - px * STATUSLINES;
        int xl = sw / 2 - vw / 2;
        int yl = (h - vh) / 2;

        if (xl != 0)
        {
            _videoManager.BarScaledCoord(0, 0, xl - px, h, bordercol);                 // left side
            _videoManager.BarScaledCoord(xl + vw + px, 0, sw - (xl + vw + px), h, bordercol);          // right side, out to the screen edge
        }

        if (yl != 0)
        {
            _videoManager.BarScaledCoord(0, 0, sw, yl - px, bordercol);                    // upper side
            _videoManager.BarScaledCoord(0, yl + vh + px, sw, h - (yl + vh + px), bordercol);         // lower side, down to the status bar
        }

        if (xl != 0)
        {
            // Paint game view border lines
            _videoManager.BarScaledCoord(xl - px, yl - px, vw + px, px, "Black");                      // upper border
            _videoManager.BarScaledCoord(xl, yl + vh, vw + px, px, bordercol);// - 2);          // lower border
            _videoManager.BarScaledCoord(xl - px, yl - px, px, vh + px, "Black");                      // left border
            _videoManager.BarScaledCoord(xl + vw, yl - px, px, vh + px * 2, bordercol);// - 2);          // right border
            _videoManager.BarScaledCoord(xl - px, yl + vh, px, px, bordercol);// - 3);          // lower left highlight
        }
        else
        {
            // Just paint a lower border line
            _videoManager.BarScaledCoord(0, yl + vh, vw, px, bordercol);// - 2);       // lower border
        }
    }

    /// <summary>
    /// Paints the view border and the status bar's own border-colored areas (statusbar.yaml
    /// border rects) in <paramref name="color"/>.
    /// </summary>
    internal static void DrawStatusBorder(string color)
    {
        int px = _videoManager.scaleFactor;
        int statusborderw = (_videoManager.screenWidth - px * 320) / 2;
        int top = _videoManager.screenHeight - px * STATUSLINES;

        _videoManager.BarScaledCoord(0, 0, _videoManager.screenWidth, top, color);

        foreach (var rect in StatusBar.Get("border")?.Rects ?? [])
        {
            if (rect.Count != 4)
                continue;
            int x = statusborderw + px * rect[0], width = px * rect[2];
            if (rect[0] <= 0)
            {
                width += x;             // out to the left edge
                x = 0;
            }
            if (rect[0] + rect[2] >= 320)
                width = _videoManager.screenWidth - x;  // out to the right edge
            _videoManager.BarScaledCoord(x, top + px * rect[1], width, px * rect[3], color);
        }
    }

    /// <summary>
    /// The level's name for showing to the player: game-info's name for it, else
    /// "Episode X, Floor Y" ("Floor Y" when there's one episode), else the map's lump name.
    /// </summary>
    internal static string GetMapDisplayName(string mapon)
    {
        var gameInfo = _gameEngineManager.GetGameInfo();
        if (!gameInfo.Maps.TryGetValue(mapon, out var mapInfo))
            return mapon;

        var language = _assetManager.GetText("en-us");
        if (!string.IsNullOrEmpty(mapInfo.Name))
            return mapInfo.Name.ToLanguageText(language);
        if (mapInfo.FloorNumber <= 0)
            return mapon;

        return gameInfo.Episodes.Count > 1
            ? string.Format("$STR_MAPEPISODEFLOOR".ToLanguageText(language), mapInfo.Cluster, mapInfo.FloorNumber)
            : string.Format("$STR_MAPFLOOR".ToLanguageText(language), mapInfo.FloorNumber);
    }

    internal static void SetupGameLevel()
    {
        viewpitch = 0;                      // each level, and each loaded game, starts looking straight ahead

        if (!loadedgame)
        {
            gamestate.TimeCount =
            gamestate.secrettotal =
            gamestate.killtotal =
            gamestate.treasuretotal =
            gamestate.secretcount =
            gamestate.killcount =
            gamestate.treasurecount = 0;
            weaponSprite = null;            // the weapon in hand starts on its Ready state
            pwallstate =
            pwallpos = 0;
            facetimes = 0;
            LastAttacker = null;
        }

        if (demoplayback || demorecord)
            US_InitRndT(false);
        else
            US_InitRndT(true);

        //
        // load the level
        //
        //int mapnum = gamestate.mapon + 10 * gamestate.cluster;
        _mapManager.LoadMap(gamestate.mapon, gamestate.difficulty, CurrentSkill.EnemyHealth);

        // The cluster follows the map, however it was reached: a new game, the next/secret
        // level, a map-change trigger or the console's "map" warp
        if (_gameEngineManager.GetGameInfo().Maps.TryGetValue(gamestate.mapon, out var mapInfo))
            gamestate.cluster = mapInfo.Cluster;

        wallstories = Math.Clamp(mapInfo?.WallHeight ?? _gameEngineManager.GetGameInfo().DefaultMap.WallHeight,
            1, MAXWALLSTORIES);
        levelsky = mapInfo?.Sky ?? _gameEngineManager.GetGameInfo().DefaultMap.Sky;
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
            SpawnPlayer(start.TileX, start.TileY, start.Angle);

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

        InitLevelShadeTable();

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

        gamestate.weapon = null;                     // take away weapon
        viewpitch = 0;                               // and face the attacker straight on
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

        if (clockwise < counter)
        {
            //
            // rotate clockwise
            //
            if (curangle > iangle)
                curangle -= ANGLES;
            do
            {
                change = (int)(tics * deathTurnSpeed);
                if (curangle + change > iangle)
                    change = iangle - curangle;

                curangle += change;
                player.Angle += (short)change;
                if (player.Angle >= ANGLES)
                    player.Angle -= ANGLES;

                ThreeDRefresh();
                CalcTics();
                _hudMessageManager.Tick((int)tics);
            } while (curangle != iangle);
        }
        else
        {
            //
            // rotate counterclockwise
            //
            if (curangle < iangle)
                curangle += ANGLES;
            do
            {
                change = -(int)tics * deathTurnSpeed;
                if (curangle + change < iangle)
                    change = iangle - curangle;

                curangle += change;
                player.Angle += (short)change;
                if (player.Angle < 0)
                    player.Angle += ANGLES;

                ThreeDRefresh();
                CalcTics();
                _hudMessageManager.Tick((int)tics);
            } while (curangle != iangle);
        }

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

        gamestate.lives--;

        if (gamestate.lives > -1)
        {
            gamestate.health = StartingHealth;
            GiveStartingInventory();
            pwallstate = pwallpos = 0;
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
