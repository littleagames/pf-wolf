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

        ClearMemory();
        SETFONTCOLOR("Black", "White");
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
            DrawLevel();

            ingame = true;
            if (loadedgame)
            {
                ContinueMusic(lastgamemusicoffset);
                loadedgame = false;
            }
            else StartMusic();

            if (!died)
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
            }
            else
                pendingMapChange = null;

            StopMusic();
            ingame = false;

            if (demorecord && playstate != playstatetypes.ex_warped)
                FinishDemoRecord();

            if (startgame || loadedgame)
            {
                ClearMemory();
                SETFONTCOLOR("Black", "White");
                _videoManager.FadeOut();
                DrawPlayScreen();
                died = false;
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

                    CheckHighScore(gamestate.score, (ushort)/*(MapInfoMappings.MapAssetToIndex[gamestate.mapon] + 1)*/1); // TODO: Redo this to support map names
                    EnableViewScoresMenuItem();
                    return;

                case playstatetypes.ex_victorious:
                    if (viewsize == 21) DrawPlayScreen();
                    _videoManager.FadeOut();
                    ClearMemory();

                    Victory();

                    ClearMemory();

                    CheckHighScore(gamestate.score, (ushort)/*(MapInfoMappings.MapAssetToIndex[gamestate.mapon] + 1)*/1); // TODO: Redo this to support map names
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
        if (true)
        {
            var demoAsset = _assetManager.Find<DemoAsset>($"demo{demonumber}");
            if (demoAsset == null)
                return;

            demoData = demoAsset.RawData;// _graphicManager.GetDemo(demonumber);
            demoptr = 0;
        }
        else
        {

            var demoFileName = demoname.Replace('?', (char)('0' + demonumber));
            if (!File.Exists(demoFileName))
                return;

            demoData = File.ReadAllBytes(demoFileName);
            demoptr = 0;
        }

        throw new NotImplementedException("Need to rewrite demo storage to save mapon as string data");
        //NewGame(difficultytypes.gd_hard, cluster: 0, mapon: demoData[demoptr++]); // TODO: Allow demo to set difficulty too
        length = BitConverter.ToInt16(demoData, demoptr);

        demoptr += 3;
        lastdemoptr = demoptr - 4 + length;

        _videoManager.FadeOut();

        SETFONTCOLOR("Black", "White");
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

    internal static string demoname = "DEMO?.dmo";
    internal const int MAXDEMOSIZE = 8192;
    internal static void StartDemoRecord(int levelnumber)
    {
        demoData = new byte[MAXDEMOSIZE];
        demoptr = 0;
        lastdemoptr = MAXDEMOSIZE;

        Buffer.BlockCopy(BitConverter.GetBytes(levelnumber), 0, demoData, demoptr, sizeof(int));
        demoptr += sizeof(int); // += 4, leave space for length
        demorecord = true;

    }

    internal static void FinishDemoRecord()
    {
        int length, level;

        demorecord = false;

        length = demoptr;

        demoptr++;
        demoData[demoptr] = (byte)length;
        demoData[demoptr + 1] = (byte)(length >> 8);
        demoData[demoptr + 2] = 0;

        _videoManager.FadeIn();
        CenterWindow(24, 3);
        PrintY += 6;
        fontnumber = "SmallFont";
        SETFONTCOLOR("Black", "White");
        US_Print(" Demo number (0-9): ");
        _videoManager.Update();

        string str = "";
        if (US_LineInput(px, py, ref str, "", true, 1, 0))
        {
            if (string.IsNullOrEmpty(str))
                return;

            level = Convert.ToInt32(str);
            if (level >= 0 && level <= 9)
            {
                var demoFileName = demoname.Replace('?', (char)('0' + level));
                throw new NotImplementedException("Need to rewrite demo storage to save mapon as string data");
                //CA_WriteFile(demoFileName, demoData, length);
            }
        }

        demoData = [];
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
        int level, maps;
        CenterWindow(26, 3);
        PrintY += 6;
        fontnumber = "SmallFont";
        SETFONTCOLOR("Black", "White");
        US_Print("  Demo which level(1-60): "); maps = 60;
        _videoManager.Update();
        _videoManager.FadeIn();
        string str = "";
        var esc = !US_LineInput(px, py, ref str, "", true, 2, 0);
        if (esc || string.IsNullOrEmpty(str))
            return;

        level = Convert.ToInt32(str);
        level--;

        if (level >= maps || level < 0)
            return;

        _videoManager.FadeOut();
        //NewGame(difficultytypes.gd_hard, level / 10);
        //gamestate.mapon = (short)(level % 10);
        throw new NotImplementedException("Need to rewrite demo storage to save mapon as string data");
        StartDemoRecord(level);

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

        FinishDemoRecord();
    }

    internal static void DrawPlayScreen()
    {
        _graphicManager.DrawPic("statusbar", 0, 200 - STATUSLINES); // TODO: Orientation: Bottom/Centered
        DrawPlayBorder();

        DrawFace();
        DrawHealth();
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
            int statusborderw = (_videoManager.screenWidth - px * 320) / 2;
            _videoManager.BarScaledCoord(0, _videoManager.screenHeight - px * STATUSLINES,
                statusborderw + px * 8, px * STATUSLINES, bordercol);
            _videoManager.BarScaledCoord(_videoManager.screenWidth - statusborderw - px * 8, _videoManager.screenHeight - px * STATUSLINES,
                statusborderw + px * 8, px * STATUSLINES, bordercol);
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

    internal static void DrawStatusBorder(string color)
    {
        int statusborderw = (_videoManager.screenWidth - _videoManager.scaleFactor * 320) / 2;

        _videoManager.BarScaledCoord(0, 0, _videoManager.screenWidth, _videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES - 3), color);
        _videoManager.BarScaledCoord(0, _videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES - 3),
            statusborderw + _videoManager.scaleFactor * 8, _videoManager.scaleFactor * (STATUSLINES - 4), color);
        _videoManager.BarScaledCoord(0, _videoManager.screenHeight - _videoManager.scaleFactor * 2, _videoManager.screenWidth, _videoManager.scaleFactor * 2, color);
        _videoManager.BarScaledCoord(_videoManager.screenWidth - statusborderw - _videoManager.scaleFactor * 8, _videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES - 3),
            statusborderw + _videoManager.scaleFactor * 8, _videoManager.scaleFactor * (STATUSLINES - 4), color);

        _videoManager.BarScaledCoord(statusborderw + _videoManager.scaleFactor * 9, _videoManager.screenHeight - _videoManager.scaleFactor * 3,
            _videoManager.scaleFactor * 97, _videoManager.scaleFactor * 1, color);// - 1);
        _videoManager.BarScaledCoord(statusborderw + _videoManager.scaleFactor * 106, _videoManager.screenHeight - _videoManager.scaleFactor * 3,
            _videoManager.scaleFactor * 161, _videoManager.scaleFactor * 1, color);// - 2);
        _videoManager.BarScaledCoord(statusborderw + _videoManager.scaleFactor * 267, _videoManager.screenHeight - _videoManager.scaleFactor * 3,
            _videoManager.scaleFactor * 44, _videoManager.scaleFactor * 1, color);// - 3);
        _videoManager.BarScaledCoord(_videoManager.screenWidth - statusborderw - _videoManager.scaleFactor * 9, _videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES - 4),
           _videoManager.scaleFactor * 1, _videoManager.scaleFactor * 20, color);// - 2);
        _videoManager.BarScaledCoord(_videoManager.screenWidth - statusborderw - _videoManager.scaleFactor * 9, _videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES / 2 - 4),
            _videoManager.scaleFactor * 1, _videoManager.scaleFactor * 14, color);// - 3);
    }

    internal static void SetupGameLevel()
    {
        if (!loadedgame)
        {
            gamestate.TimeCount =
            gamestate.secrettotal =
            gamestate.killtotal =
            gamestate.treasuretotal =
            gamestate.secretcount =
            gamestate.killcount =
            gamestate.treasurecount =
            gamestate.attackframe =
            gamestate.attackcount =
            gamestate.weaponframe = 0;
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
        _mapManager.LoadMap(gamestate.mapon, (int)gamestate.difficulty);

        // The cluster follows the map, however it was reached: a new game, the next/secret
        // level, a map-change trigger or the console's "map" warp
        if (_gameEngineManager.GetGameInfo().Maps.TryGetValue(gamestate.mapon, out var mapInfo))
            gamestate.cluster = mapInfo.Cluster;

        //
        // spawn doors
        //
        InitActorList();                       // start spawning things with a clean slate
        InitDoorList();
        //InitStaticList();


        int x, y;
        for (y = 0; y < _mapManager.mapheight; y++)
        {
            for (x = 0; x < _mapManager.mapwidth; x++)
            {
                int tile = _mapManager.MAPSPOT(x, y, 0);
                if (tile >= 90 && tile <= 101)
                {
                    var mapDef = _mapManager.GetMapData();
                    mapDef.Doors.TryGetValue(tile, out var doorXlat);
                    // door
                    switch (tile)
                    {
                        case 90:
                        case 92:
                        case 94:
                        case 96:
                        case 98:
                        case 100:
                            SpawnDoor(x, y, true, doorXlat ?? MapTextureTranslation.None);
                            break;
                        case 91:
                        case 93:
                        case 95:
                        case 97:
                        case 99:
                        case 101:
                            SpawnDoor(x, y, false, doorXlat ?? MapTextureTranslation.None);
                            break;
                    }
                }
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

                if (tile == MapDataConstants.AMBUSHTILE)
                {
                    if (MapManager.VALIDAREA(_mapManager.MAPSPOT(x + 1, y, 0)))
                        tile = (ushort)_mapManager.MAPSPOT(x + 1, y, 0);
                    if (MapManager.VALIDAREA(_mapManager.MAPSPOT(x, y - 1, 0)))
                        tile = (ushort)_mapManager.MAPSPOT(x, y - 1, 0);
                    if (MapManager.VALIDAREA(_mapManager.MAPSPOT(x, y + 1, 0)))
                        tile = (ushort)_mapManager.MAPSPOT(x, y + 1, 0);
                    if (MapManager.VALIDAREA(_mapManager.MAPSPOT(x - 1, y, 0)))
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

    internal const int DEATHROTATE = 2;
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

        gamestate.weapon = weapontypes.wp_none;                     // take away weapon
        _audioManager.Play("player/death");

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
                change = (int)(tics * DEATHROTATE);
                if (curangle + change > iangle)
                    change = iangle - curangle;

                curangle += change;
                player.Angle += (short)change;
                if (player.Angle >= ANGLES)
                    player.Angle -= ANGLES;

                ThreeDRefresh();
                CalcTics();
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
                change = -(int)tics * DEATHROTATE;
                if (curangle + change < iangle)
                    change = iangle - curangle;

                curangle += change;
                player.Angle += (short)change;
                if (player.Angle < 0)
                    player.Angle += ANGLES;

                ThreeDRefresh();
                CalcTics();
            } while (curangle != iangle);
        }

        //
        // fade to red
        //
        _videoManager.FinishPaletteShifts();

        _videoManager.BarScaledCoord(viewscreenx, viewscreeny, viewwidth, viewheight, "Maroon");

        _inputManager.ClearKeysDown();

        _videoManager.FizzleFade(viewscreenx, viewscreeny, (uint)viewwidth, (uint)viewheight, 70, false);

        _inputManager.UserInput(100);
        _audioManager.WaitSoundDone();
        ClearMemory();

        gamestate.lives--;

        if (gamestate.lives > -1)
        {
            gamestate.health = 100;
            GiveStartingInventory();
            pwallstate = pwallpos = 0;
            gamestate.attackframe = gamestate.attackcount =
                gamestate.weaponframe = 0;

            if (viewsize != 21)
            {
                DrawKeys();
                DrawWeapon();
                DrawAmmo();
                DrawHealth();
                DrawFace();
                DrawLives();
            }
        }
    }
}
