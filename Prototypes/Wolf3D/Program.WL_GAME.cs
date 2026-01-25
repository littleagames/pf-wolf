namespace Wolf3D;

internal partial class Program
{
    static bool ingame, fizzlein;
    static gametype gamestate;
    static byte bordercol = VIEWCOLOR; // color of the Change View/Ingame border

    internal static void GameLoop()
    {
        bool died;

        // TODO:
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

    internal static void DrawPlayScreen()
    {
        VWB_DrawPicScaledCoord((screenWidth - scaleFactor * 320) / 2, screenHeight - scaleFactor * STATUSLINES, (int)graphicnums.STATUSBARPIC);
        DrawPlayBorder();

        //DrawFace();
        //DrawHealth();
        //DrawLives();
        //DrawLevel();
        //DrawAmmo();
        //DrawKeys();
        //DrawWeapon();
        //DrawScore();
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

        // TODO: Later
        //CA_CacheMap(mapnum);

        //mapwidth = mapheaderseg[mapnum].width;
        //mapheight = mapheaderseg[mapnum].height;

        //tilemap = new tiletype[MAPSIZE];
        //actorat = new objtype[MAPSIZE];
    }
}
