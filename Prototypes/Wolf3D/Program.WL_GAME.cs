using System;
using System.Data;

namespace Wolf3D;

internal partial class Program
{
    static bool ingame, fizzlein;
    static gametype gamestate;
    static byte bordercol = VIEWCOLOR; // color of the Change View/Ingame border

    //
    // ELEVATOR BACK MAPS - REMEMBER (-1)!!
    //
    private static int[] ElevatorBackTo = { 1, 1, 7, 3, 5, 3 };

    internal static void ScanInfoPlane()
    {
        int x, y;
        int tile;
        for (y = 0; y < mapheight; y++)
        {
            for (x = 0; x < mapwidth; x++)
            {
                tile = MAPSPOT(x, y, 1);
                switch (tile)
                {
                    case 19:
                    case 20:
                    case 21:
                    case 22:
                        SpawnPlayer(x, y, NORTH + tile - 19);
                        break;
                }
            }
        }
    }


    internal static void GameLoop()
    {
        bool died;

        ClearMemory();
        SETFONTCOLOR(0, 15);
        VW_FadeOut();
        DrawPlayScreen();
        died = false;
        do
        {
            if (!loadedgame)
                gamestate.score = gamestate.oldscore;
            if (!died || viewsize != 21) DrawScore();

            startgame = false;
            if (!loadedgame)
                SetupGameLevel();
            DrawLevel();                        // ADDEDFIX 5 -  Chris Chokan

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

            //        DrawLevel ();                     // ADDEDFIX 5 - moved up  Chris Chokan

            PlayLoop();
            StopMusic();
            ingame = false;

            if (demorecord && playstate != (byte)playstatetypes.ex_warped)
                FinishDemoRecord();

            if (startgame || loadedgame)
            {
                ClearMemory();
                SETFONTCOLOR(0, 15);
                VW_FadeOut();
                DrawPlayScreen();
                died = false;
                continue;
            }

            switch (playstate)
            {
                case (byte)playstatetypes.ex_completed:
                case (byte)playstatetypes.ex_secretlevel:
                    if (viewsize == 21) DrawPlayScreen();
                    gamestate.keys = 0;
                    DrawKeys();
                    VW_FadeOut();

                    ClearMemory();

                    LevelCompleted();              // do the intermission
                    if (viewsize == 21) DrawPlayScreen();
                    gamestate.oldscore = gamestate.score;
                    //
                    // COMING BACK FROM SECRET LEVEL
                    //
                    if (gamestate.mapon == 9)
                        gamestate.mapon = (short)ElevatorBackTo[gamestate.episode];    // back from secret
                    else
                        //
                        // GOING TO SECRET LEVEL
                        //
                        if (playstate == (byte)playstatetypes.ex_secretlevel)
                            gamestate.mapon = 9;
                        else
                            //
                            // GOING TO NEXT LEVEL
                            //
                            gamestate.mapon++;
                    break;

                case (byte)playstatetypes.ex_died:
                    Died();
                    died = true;                    // don't "get psyched!"

                    if (gamestate.lives > -1)
                        break;                          // more lives left

                    VW_FadeOut();
                    if (screenHeight % 200 != 0)
                        VL_ClearScreen(0);
                    ClearMemory();

                    CheckHighScore(gamestate.score, (ushort)(gamestate.mapon + 1));
                    MainMenu[(int)menuitems.viewscores].text = STR_VS;
                    MainMenu[(int)menuitems.viewscores].routine = CP_ViewScores;
                    return;

                case (byte)playstatetypes.ex_victorious:
                    if (viewsize == 21) DrawPlayScreen();
                    VW_FadeOut();
                    ClearMemory();

                    Victory();

                    ClearMemory();

                    CheckHighScore(gamestate.score, (ushort)(gamestate.mapon + 1));
                    MainMenu[(int)menuitems.viewscores].text = STR_VS;
                    MainMenu[(int)menuitems.viewscores].routine = CP_ViewScores;
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
        int[] dems = { (int)graphicnums.T_DEMO0, (int)graphicnums.T_DEMO1, (int)graphicnums.T_DEMO2, (int)graphicnums.T_DEMO3 };

        demoptr = 0;
        var demoData = grsegs[dems[demonumber]];

        NewGame(1, 0);
        gamestate.mapon = demoData[demoptr++];
        gamestate.difficulty = (short)difficultytypes.gd_hard;
        length = BitConverter.ToInt16(demoData, demoptr);
        demoptr += sizeof(short);

        demoptr += 3;
        lastdemoptr = demoptr - 4 + length;

        VW_FadeOut();

        SETFONTCOLOR(0, 15);
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

    internal static void RecordDemo()
    {
        // TODO:
    }

    internal static void FinishDemoRecord()
    {
        int length, level;

        demorecord = false;
        // TODO:
    }
    
    internal static void DrawPlayScreen()
    {
        VWB_DrawPicScaledCoord((screenWidth - scaleFactor * 320) / 2, screenHeight - scaleFactor * STATUSLINES, (int)graphicnums.STATUSBARPIC);
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
        int px = scaleFactor;

        if (bordercol != VIEWCOLOR)
            DrawStatusBorder(bordercol);
        else
        {
            int statusborderw = (screenWidth - px * 320) / 2;
            VWB_BarScaledCoord(0, screenHeight - px * STATUSLINES,
                statusborderw + px * 8, px * STATUSLINES, bordercol);
            VWB_BarScaledCoord(screenWidth - statusborderw - px * 8, screenHeight - px * STATUSLINES,
                statusborderw + px * 8, px * STATUSLINES, bordercol);
        }

        if (viewheight == screenHeight) return;

        VWB_BarScaledCoord(0, 0, screenWidth, screenHeight - px * STATUSLINES, bordercol);

        int xl = screenWidth / 2 - viewwidth / 2;
        int yl = (screenHeight - px * STATUSLINES - viewheight) / 2;
        VWB_BarScaledCoord(xl, yl, viewwidth, viewheight, 0);

        if (xl != 0)
        {
            // Paint game view border lines
            VWB_BarScaledCoord(xl - px, yl - px, viewwidth + px, px, 0);                      // upper border
            VWB_BarScaledCoord(xl, yl + viewheight, viewwidth + px, px, bordercol - 2);       // lower border
            VWB_BarScaledCoord(xl - px, yl - px, px, viewheight + px, 0);                     // left border
            VWB_BarScaledCoord(xl + viewwidth, yl - px, px, viewheight + 2 * px, bordercol - 2);  // right border
            VWB_BarScaledCoord(xl - px, yl + viewheight, px, px, bordercol - 3);              // lower left highlight
        }
        else
        {
            // Just paint a lower border line
            VWB_BarScaledCoord(0, yl + viewheight, viewwidth, px, bordercol - 2);       // lower border
        }
    }

    internal static void DrawPlayBorderSides()
    {
        if (viewsize == 21) return;

        int sw = screenWidth;
        int sh = screenHeight;
        int vw = viewwidth;
        int vh = viewheight;
        int px = scaleFactor; // size of one "pixel"

        int h = sh - px * STATUSLINES;
        int xl = sw / 2 - vw / 2;
        int yl = (h - vh) / 2;

        if (xl != 0)
        {
            VWB_BarScaledCoord(0, 0, xl - px, h, bordercol);                 // left side
            VWB_BarScaledCoord(xl + vw + px, 0, xl - px * 2, h, bordercol);                 // right side
        }

        if (yl != 0)
        {
            VWB_BarScaledCoord(0, 0, sw, yl - px, bordercol);                    // upper side
            VWB_BarScaledCoord(0, yl + vh + px, sw, yl - px, bordercol);                    // lower side
        }

        if (xl != 0)
        {
            // Paint game view border lines
            VWB_BarScaledCoord(xl - px, yl - px, vw + px, px, 0);                      // upper border
            VWB_BarScaledCoord(xl, yl + vh, vw + px, px, bordercol - 2);          // lower border
            VWB_BarScaledCoord(xl - px, yl - px, px, vh + px, 0);                      // left border
            VWB_BarScaledCoord(xl + vw, yl - px, px, vh + px * 2, bordercol - 2);          // right border
            VWB_BarScaledCoord(xl - px, yl + vh, px, px, bordercol - 3);          // lower left highlight
        }
        else
        {
            // Just paint a lower border line
            VWB_BarScaledCoord(0, yl + vh, vw, px, bordercol - 2);       // lower border
        }
    }

    internal static void DrawStatusBorder(byte color)
    {
        int statusborderw = (screenWidth - scaleFactor * 320) / 2;

        VWB_BarScaledCoord(0, 0, screenWidth, screenHeight - scaleFactor * (STATUSLINES - 3), color);
        VWB_BarScaledCoord(0, screenHeight - scaleFactor * (STATUSLINES - 3),
            statusborderw + scaleFactor * 8, scaleFactor * (STATUSLINES - 4), color);
        VWB_BarScaledCoord(0, screenHeight - scaleFactor * 2, screenWidth, scaleFactor * 2, color);
        VWB_BarScaledCoord(screenWidth - statusborderw - scaleFactor * 8, screenHeight - scaleFactor * (STATUSLINES - 3),
            statusborderw + scaleFactor * 8, scaleFactor * (STATUSLINES - 4), color);

        VWB_BarScaledCoord(statusborderw + scaleFactor * 9, screenHeight - scaleFactor * 3,
            scaleFactor * 97, scaleFactor * 1, color - 1);
        VWB_BarScaledCoord(statusborderw + scaleFactor * 106, screenHeight - scaleFactor * 3,
            scaleFactor * 161, scaleFactor * 1, color - 2);
        VWB_BarScaledCoord(statusborderw + scaleFactor * 267, screenHeight - scaleFactor * 3,
            scaleFactor * 44, scaleFactor * 1, color - 3);
        VWB_BarScaledCoord(screenWidth - statusborderw - scaleFactor * 9, screenHeight - scaleFactor * (STATUSLINES - 4),
            scaleFactor * 1, scaleFactor * 20, color - 2);
        VWB_BarScaledCoord(screenWidth - statusborderw - scaleFactor * 9, screenHeight - scaleFactor * (STATUSLINES / 2 - 4),
            scaleFactor * 1, scaleFactor * 14, color - 3);
    }

    internal static void SetupGameLevel()
    {
        int x, y;
        int mapnum;

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
        mapnum = gamestate.mapon + 10 * gamestate.episode;

        CA_CacheMap(mapnum);

        mapwidth = mapheaderseg[mapnum].width;
        mapheight = mapheaderseg[mapnum].height;

        tilemap = new byte[MAPSIZE, MAPSIZE];
        actorat = new objstruct[MAPSIZE, MAPSIZE];

        for (y = 0; y < mapheight; y++)
        {
            for (x = 0; x < mapwidth; x++)
            {
                int tile = MAPSPOT(x, y, 0);
                if (tile < AMBUSHTILE)
                {
                    // solid wall
                    tilemap[x,y] = (byte)tile;
                    //actorat[x,y] = (objtype*)(uintptr_t)tile; // TODO: Later
                }
                else
                {
                    // area floor
                    tilemap[x,y] = 0;
                    //actorat[x,y] = 0; // TODO: Later
                }
            }
        }

        //
        // spawn doors
        //
        InitActorList();                       // start spawning things with a clean slate
        InitDoorList();
        //InitStaticList();


        for (y = 0; y < mapheight; y++)
        {
            for (x = 0; x < mapwidth; x++)
            {
                int tile = MAPSPOT(x, y, 0);
                if (tile >= 90 && tile <= 101)
                {
                    // door
                    switch (tile)
                    {
                        case 90:
                        case 92:
                        case 94:
                        case 96:
                        case 98:
                        case 100:
                            SpawnDoor(x, y, true, (tile - 90) / 2);
                            break;
                        case 91:
                        case 93:
                        case 95:
                        case 97:
                        case 99:
                        case 101:
                            SpawnDoor(x, y, false, (tile - 91) / 2);
                            break;
                    }
                }
            }
        }

        //
        // spawn actors
        //
        ScanInfoPlane();

        //
        // take out the ambush markers
        //
        //for (y = 0; y < mapheight; y++)
        //{
        //    for (x = 0; x < mapwidth; x++)
        //    {
        //        var tile = MAPSPOT(x, y, 0);

        //        if (tile == AMBUSHTILE)
        //        {
        //            if (VALIDAREA(*(map + 1)))
        //                tile = *map;
        //            if (VALIDAREA(*(map - mapwidth)))
        //                tile = *(map - mapwidth);
        //            if (VALIDAREA(*(map + mapwidth)))
        //                tile = *(map + mapwidth);
        //            if (VALIDAREA(*(map - 1)))
        //                tile = *(map - 1);

        //            *map = tile;
        //        }

        //        map++;
        //    }
        //}

        //
        // have the caching manager load and purge stuff to make sure all marks
        // are in memory
        //
        CA_LoadAllSounds();
    }

    internal const int DEATHROTATE = 2;
    internal static void Died()
    {
        float fangle;
        int dx, dy;
        int iangle, curangle, clockwise, counter, change;

        if (screenfaded)
        {
            ThreeDRefresh();
            VW_FadeIn();
        }

        gamestate.weapon = -1;                     // take away weapon
        SD_PlaySound((int)soundnames.PLAYERDEATHSND);

        //
        // swing around to face attacker
        //
        if (LastAttacker != null)
        {
            dx = LastAttacker.x - player.x;
            dy = player.y - LastAttacker.y;

            fangle = (float)Math.Atan2((float)dy, (float)dx);     // returns -pi to pi
            if (fangle < 0)
                fangle = (float)(M_PI * 2 + fangle);

            iangle = (int)(fangle / (M_PI * 2) * ANGLES);
        }
        else
        {
            iangle = player.angle + ANGLES / 2;
            if (iangle >= ANGLES) iangle -= ANGLES;
        }

        if (player.angle > iangle)
        {
            counter = player.angle - iangle;
            clockwise = ANGLES - player.angle + iangle;
        }
        else
        {
            clockwise = iangle - player.angle;
            counter = player.angle + ANGLES - iangle;
        }

        curangle = player.angle;

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
                player.angle += (short)change;
                if (player.angle >= ANGLES)
                    player.angle -= ANGLES;

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
                player.angle += (short)change;
                if (player.angle < 0)
                    player.angle += ANGLES;

                ThreeDRefresh();
                CalcTics();
            } while (curangle != iangle);
        }

        //
        // fade to red
        //
        FinishPaletteShifts();

        VL_BarScaledCoord(viewscreenx, viewscreeny, viewwidth, viewheight, 4);

        IN_ClearKeysDown();

        FizzleFade(screenBuffer, viewscreenx, viewscreeny, (uint)viewwidth, (uint)viewheight, 70, false);

        IN_UserInput(100);
        SD_WaitSoundDone();
        ClearMemory();

        gamestate.lives--;

        if (gamestate.lives > -1)
        {
            gamestate.health = 100;
            gamestate.weapon = gamestate.bestweapon
                = gamestate.chosenweapon = (int)weapontypes.wp_pistol;
            gamestate.ammo = STARTAMMO;
            gamestate.keys = 0;
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

    internal static void UpdateSoundLoc()
    {
        int i;

        // TODO:
        //for (i = 0; i < MIX_CHANNELS; i++)
        //{
        //    if (channelSoundPos[i].valid)
        //    {
        //        SetSoundLoc(channelSoundPos[i].globalsoundx,
        //            channelSoundPos[i].globalsoundy);
        //        SD_SetPosition(i, leftchannel, rightchannel);
        //    }
        //}
    }
}
