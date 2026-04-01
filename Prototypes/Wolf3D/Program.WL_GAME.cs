using SDL2;
using Wolf3D.Extensions;
using Wolf3D.Managers;
using Wolf3D.Mappers;

namespace Wolf3D;

internal partial class Program
{
    /*
=============================================================================

                             GLOBAL VARIABLES

=============================================================================
*/
    static bool ingame, fizzlein;
    static gametype gamestate = new gametype();
    static byte bordercol = VIEWCOLOR; // color of the Change View/Ingame border
# if SPEAR
    internal static int spearx, speary;
    internal static uint spearangle;
    internal static bool spearflag;
#endif

#if USE_FEATUREFLAGS
    internal static int ffDataTopLeft, ffDataTopRight, ffDataBottomLeft, ffDataBottomRight;
#endif

    //
    // ELEVATOR BACK MAPS - REMEMBER (-1)!!
    //
    private static int[] ElevatorBackTo = { 1, 1, 7, 3, 5, 3 };

    //===========================================================================


    /*
    ==========================
    =
    = SetSoundLoc - Given the location of an object (in terms of global
    =       coordinates, held in globalsoundx and globalsoundy), munges the values
    =       for an approximate distance from the left and right ear, and puts
    =       those values into leftchannel and rightchannel.
    =
    = JAB
    =
    ==========================
    */

    internal static int leftchannel, rightchannel;
    internal const int ATABLEMAX = 15;
    internal static byte[,] righttable = new byte[ATABLEMAX, ATABLEMAX * 2] {
        { 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 7, 7, 7, 6, 0, 0, 0, 0, 0, 1, 3, 5, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 7, 7, 6, 4, 0, 0, 0, 0, 0, 2, 4, 6, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 7, 6, 6, 4, 1, 0, 0, 0, 1, 2, 4, 6, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 7, 6, 5, 4, 2, 1, 0, 1, 2, 3, 5, 7, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 6, 5, 4, 3, 2, 2, 3, 3, 5, 6, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 6, 6, 5, 4, 4, 4, 4, 5, 6, 7, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 6, 6, 5, 5, 5, 6, 6, 7, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 7, 7, 7, 6, 6, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8}
    };
    internal static byte[,] lefttable = new byte[ATABLEMAX, ATABLEMAX * 2] {
        { 8, 8, 8, 8, 8, 8, 8, 8, 5, 3, 1, 0, 0, 0, 0, 0, 6, 7, 7, 7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 6, 4, 2, 0, 0, 0, 0, 0, 4, 6, 7, 7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 6, 4, 2, 1, 0, 0, 0, 1, 4, 6, 6, 7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 7, 5, 3, 2, 1, 0, 1, 2, 4, 5, 6, 7, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 6, 5, 3, 3, 2, 2, 3, 4, 5, 6, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 7, 6, 5, 4, 4, 4, 4, 5, 6, 6, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 7, 6, 6, 5, 5, 5, 6, 6, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 7, 7, 6, 6, 7, 7, 7, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8},
        { 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8}
    };

    internal static void SetSoundLoc(int gx,int gy)
    {
        int xt, yt;
        int x, y;

        //
        // translate point to view centered coordinates
        //
        gx -= viewx;
        gy -= viewy;

    //
    // calculate newx
    //
        xt = FixedMul(gx, viewcos);
        yt = FixedMul(gy, viewsin);
        x = (xt - yt) >> TILESHIFT;

    //
    // calculate newy
    //
        xt = FixedMul(gx, viewsin);
        yt = FixedMul(gy, viewcos);
        y = (yt + xt) >> TILESHIFT;

        if (y >= ATABLEMAX)
            y = ATABLEMAX - 1;
        else if (y <= -ATABLEMAX)
            y = -ATABLEMAX;
        if (x< 0)
            x = -x;
        if (x >= ATABLEMAX)
            x = ATABLEMAX - 1;
        leftchannel  =  lefttable[x, y + ATABLEMAX];
        rightchannel = righttable[x, y + ATABLEMAX];

    //#if 0
    //    CenterWindow(8,1);
    //    US_PrintSigned(leftchannel);
    //    US_Print(",");
    //    US_PrintSigned(rightchannel);
    //    _videoManager.Update();
    //#endif
    }
/*
==========================
=
= SetSoundLocGlobal - Sets up globalsoundx & globalsoundy and then calls
=       UpdateSoundLoc() to transform that into relative channel volumes. Those
=       values are then passed to the Sound Manager so that they'll be used for
=       the next sound played (if possible).
=
= JAB
=
==========================
*/
internal static void PlaySoundLocGlobal(string s, int gx, int gy)
    {
        SetSoundLoc(gx, gy);
        SD_PositionSound(leftchannel, rightchannel);

        int channel = SD_PlaySound(s);
        if (channel != 0)
        {
            channelSoundPos[channel - 1].globalsoundx = gx;
            channelSoundPos[channel - 1].globalsoundy = gy;
            channelSoundPos[channel - 1].valid = 1;
        }
    }

    internal static void ScanInfoPlane()
    {
        int x, y;
        int tile;
        for (y = 0; y < _mapManager.mapheight; y++)
        {
            for (x = 0; x < _mapManager.mapwidth; x++)
            {
                tile = _mapManager.MAPSPOT(x, y, 1);
                switch (tile)
                {
                    case 19:
                    case 20:
                    case 21:
                    case 22:
                        SpawnPlayer(x, y, NORTH + tile - 19);
                        break;


                    case 23:
                    case 24:
                    case 25:
                    case 26:
                    case 27:
                    case 28:
                    case 29:
                    case 30:

                    case 31:
                    case 32:
                    case 33:
                    case 34:
                    case 35:
                    case 36:
                    case 37:
                    case 38:

                    case 39:
                    case 40:
                    case 41:
                    case 42:
                    case 43:
                    case 44:
                    case 45:
                    case 46:

                    case 47:
                    case 48:
                    case 49:
                    case 50:
                    case 51:
                    case 52:
                    case 53:
                    case 54:

                    case 55:
                    case 56:
                    case 57:
                    case 58:
                    case 59:
                    case 60:
                    case 61:
                    case 62:

                    case 63:
                    case 64:
                    case 65:
                    case 66:
                    case 67:
                    case 68:
                    case 69:
                    case 70:
                    case 71:
                    case 72:
                        SpawnStatic(x, y, tile - 23);
                        break;

                    //
                    // P wall
                    //
                    case 98:
                        if (!loadedgame)
                            gamestate.secrettotal++;
                        break;
                    //
                    // guard
                    //
                    case 180:
                    case 181:
                    case 182:
                    case 183:
                        if (gamestate.difficulty < difficultytypes.gd_hard)
                            break;
                        tile -= 36;
                        goto case 144;
                    case 144:
                    case 145:
                    case 146:
                    case 147:
                        if (gamestate.difficulty < difficultytypes.gd_medium)
                            break;
                        tile -= 36;
                        goto case 108;
                    case 108:
                    case 109:
                    case 110:
                    case 111:
                        SpawnStand((int)enemytypes.en_guard, x, y, tile - 108);
                        break;


                    case 184:
                    case 185:
                    case 186:
                    case 187:
                        if (gamestate.difficulty < difficultytypes.gd_hard)
                            break;
                        tile -= 36;
                        goto case 148;
                    case 148:
                    case 149:
                    case 150:
                    case 151:
                        if (gamestate.difficulty < difficultytypes.gd_medium)
                            break;
                        tile -= 36;
                        goto case 112;
                    case 112:
                    case 113:
                    case 114:
                    case 115:
                        SpawnPatrol((int)enemytypes.en_guard, x, y, tile - 112);
                        break;

                    case 124:
                        SpawnDeadGuard(x, y);
                        break;
                    //
                    // officer
                    //
                    case 188:
                    case 189:
                    case 190:
                    case 191:
                        if (gamestate.difficulty < difficultytypes.gd_hard)
                            break;
                        tile -= 36;
                        goto case 152;
                    case 152:
                    case 153:
                    case 154:
                    case 155:
                        if (gamestate.difficulty < difficultytypes.gd_medium)
                            break;
                        tile -= 36;
                        goto case 116;
                    case 116:
                    case 117:
                    case 118:
                    case 119:
                        SpawnStand((int)enemytypes.en_officer, x, y, tile - 116);
                        break;


                    case 192:
                    case 193:
                    case 194:
                    case 195:
                        if (gamestate.difficulty < difficultytypes.gd_hard)
                            break;
                        tile -= 36;
                        goto case 156;
                    case 156:
                    case 157:
                    case 158:
                    case 159:
                        if (gamestate.difficulty < difficultytypes.gd_medium)
                            break;
                        tile -= 36;
                        goto case 120;
                    case 120:
                    case 121:
                    case 122:
                    case 123:
                        SpawnPatrol((int)enemytypes.en_officer, x, y, tile - 120);
                        break;


                    //
                    // ss
                    //
                    case 198:
                    case 199:
                    case 200:
                    case 201:
                        if (gamestate.difficulty < difficultytypes.gd_hard)
                            break;
                        tile -= 36;
                        goto case 162;
                    case 162:
                    case 163:
                    case 164:
                    case 165:
                        if (gamestate.difficulty < difficultytypes.gd_medium)
                            break;
                        tile -= 36;
                        goto case 126;
                    case 126:
                    case 127:
                    case 128:
                    case 129:
                        SpawnStand((int)enemytypes.en_ss, x, y, tile - 126);
                        break;


                    case 202:
                    case 203:
                    case 204:
                    case 205:
                        if (gamestate.difficulty < difficultytypes.gd_hard)
                            break;
                        tile -= 36;
                        goto case 166;
                    case 166:
                    case 167:
                    case 168:
                    case 169:
                        if (gamestate.difficulty < difficultytypes.gd_medium)
                            break;
                        tile -= 36;
                        goto case 130;
                    case 130:
                    case 131:
                    case 132:
                    case 133:
                        SpawnPatrol((int)enemytypes.en_ss, x, y, tile - 130);
                        break;

                    //
                    // dogs
                    //
                    case 206:
                    case 207:
                    case 208:
                    case 209:
                        if (gamestate.difficulty < difficultytypes.gd_hard)
                            break;
                        tile -= 36;
                        goto case 170;
                    case 170:
                    case 171:
                    case 172:
                    case 173:
                        if (gamestate.difficulty < difficultytypes.gd_medium)
                            break;
                        tile -= 36;
                        goto case 134;
                    case 134:
                    case 135:
                    case 136:
                    case 137:
                        SpawnStand((int)enemytypes.en_dog, x, y, tile - 134);
                        break;


                    case 210:
                    case 211:
                    case 212:
                    case 213:
                        if (gamestate.difficulty < difficultytypes.gd_hard)
                            break;
                        tile -= 36;
                        goto case 174;
                    case 174:
                    case 175:
                    case 176:
                    case 177:
                        if (gamestate.difficulty < difficultytypes.gd_medium)
                            break;
                        tile -= 36;
                        goto case 138;
                    case 138:
                    case 139:
                    case 140:
                    case 141:
                        SpawnPatrol((int)enemytypes.en_dog, x, y, tile - 138);
                        break;

                    //
                    // boss
                    //
                    case 214:
                        SpawnBoss(x, y);
                        break;
                    case 197:
                        SpawnGretel(x, y);
                        break;
                    case 215:
                        SpawnGift(x, y);
                        break;
                    case 179:
                        SpawnFat(x, y);
                        break;
                    case 196:
                        SpawnSchabbs(x, y);
                        break;
                    case 160:
                        SpawnFakeHitler(x, y);
                        break;
                    case 178:
                        SpawnHitler(x, y);
                        break;
                    //
                    // mutants
                    //
                    case 252:
                    case 253:
                    case 254:
                    case 255:
                        if (gamestate.difficulty < difficultytypes.gd_hard)
                            break;
                        tile -= 18;
                        goto case 234;
                    case 234:
                    case 235:
                    case 236:
                    case 237:
                        if (gamestate.difficulty < difficultytypes.gd_medium)
                            break;
                        tile -= 18;
                        goto case 216;
                    case 216:
                    case 217:
                    case 218:
                    case 219:
                        SpawnStand((int)enemytypes.en_mutant, x, y, tile - 216);
                        break;

                    case 256:
                    case 257:
                    case 258:
                    case 259:
                        if (gamestate.difficulty < difficultytypes.gd_hard)
                            break;
                        tile -= 18;
                        goto case 238;
                    case 238:
                    case 239:
                    case 240:
                    case 241:
                        if (gamestate.difficulty < difficultytypes.gd_medium)
                            break;
                        tile -= 18;
                        goto case 220;
                    case 220:
                    case 221:
                    case 222:
                    case 223:
                        SpawnPatrol((int)enemytypes.en_mutant, x, y, tile - 220);
                        break;

                    //
                    // ghosts
                    //
                    case 224:
                        SpawnGhosts((int)enemytypes.en_blinky, x, y);
                        break;
                    case 225:
                        SpawnGhosts((int)enemytypes.en_clyde, x, y);
                        break;
                    case 226:
                        SpawnGhosts((int)enemytypes.en_pinky, x, y);
                        break;
                    case 227:
                        SpawnGhosts((int)enemytypes.en_inky, x, y);
                        break;
                }
            }
        }
    }


    internal static void GameLoop()
    {
        var language = _assetManager.GetText("en-us");
        bool died;

        ClearMemory();
        SETFONTCOLOR(0, 15);
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

            DrawLevel ();                     // ADDEDFIX 5 - moved up  Chris Chokan

            PlayLoop();
            StopMusic();
            ingame = false;

            if (demorecord && playstate != playstatetypes.ex_warped)
                FinishDemoRecord();

            if (startgame || loadedgame)
            {
                ClearMemory();
                SETFONTCOLOR(0, 15);
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
                    gamestate.keys = 0;
                    DrawKeys();
                    _videoManager.FadeOut();

                    ClearMemory();

                    LevelCompleted();              // do the intermission
                    if (viewsize == 21) DrawPlayScreen();
                    gamestate.oldscore = gamestate.score;

                    var mapInfo = _assetManager.GetGameInfo().Maps[gamestate.mapon];
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

                    CheckHighScore(gamestate.score, (ushort)(MapInfoMappings.MapAssetToIndex[gamestate.mapon] + 1));
                    MainMenu[(int)menuitems.viewscores].text = "$MENU_VIEWSCORES".ToLanguageText(language);
                    MainMenu[(int)menuitems.viewscores].routine = CP_ViewScores;
                    return;

                case playstatetypes.ex_victorious:
                    if (viewsize == 21) DrawPlayScreen();
                    _videoManager.FadeOut();
                    ClearMemory();

                    Victory();

                    ClearMemory();

                    CheckHighScore(gamestate.score, (ushort)(MapInfoMappings.MapAssetToIndex[gamestate.mapon] + 1));
                    MainMenu[(int)menuitems.viewscores].text = "$MENU_VIEWSCORES".ToLanguageText(language);
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
        if (true)
        {
            demoData = _graphicManager.GetDemo(demonumber);
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

    internal static string demoname = "DEMO?.";
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
        fontnumber = 0;
        SETFONTCOLOR(0, 15);
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
                CA_WriteFile(demoFileName, demoData, length);
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
        fontnumber = 0;
        SETFONTCOLOR(0, 15);
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

    internal static void ShowActStatus()
    {
        // Draw status bar without borders
        //byte[] source = grsegs[(int)graphicnums.STATUSBARPIC];
        //graphicnums picnum = graphicnums.STATUSBARPIC - STARTPICS;
        //int width = pictable[(int)picnum].width;
        //int height = pictable[(int)picnum].height;
        //int destx = (_videoManager.screenWidth - _videoManager.scaleFactor * 320) / 2 + 9 * _videoManager.scaleFactor;
        //int desty = _videoManager.screenHeight - (height - 4) * _videoManager.scaleFactor;
        //_videoManager.MemToScreenScaledCoord2(source, width, 9, 4, destx, desty, width - 18, height - 7);

        ingame = false;
        DrawFace();
        DrawHealth();
        DrawLives();
        DrawLevel();
        DrawAmmo();
        DrawKeys();
        DrawWeapon();
        DrawScore();
        ingame = true;
    }

    internal static void DrawPlayBorder()
    {
        int px = _videoManager.scaleFactor;

        if (bordercol != VIEWCOLOR)
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
        _videoManager.BarScaledCoord(xl, yl, viewwidth, viewheight, 0);

        if (xl != 0)
        {
            // Paint game view border lines
            _videoManager.BarScaledCoord(xl - px, yl - px, viewwidth + px, px, 0);                      // upper border
            _videoManager.BarScaledCoord(xl, yl + viewheight, viewwidth + px, px, bordercol - 2);       // lower border
            _videoManager.BarScaledCoord(xl - px, yl - px, px, viewheight + px, 0);                     // left border
            _videoManager.BarScaledCoord(xl + viewwidth, yl - px, px, viewheight + 2 * px, bordercol - 2);  // right border
            _videoManager.BarScaledCoord(xl - px, yl + viewheight, px, px, bordercol - 3);              // lower left highlight
        }
        else
        {
            // Just paint a lower border line
            _videoManager.BarScaledCoord(0, yl + viewheight, viewwidth, px, bordercol - 2);       // lower border
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
            _videoManager.BarScaledCoord(xl + vw + px, 0, xl - px * 2, h, bordercol);                 // right side
        }

        if (yl != 0)
        {
            _videoManager.BarScaledCoord(0, 0, sw, yl - px, bordercol);                    // upper side
            _videoManager.BarScaledCoord(0, yl + vh + px, sw, yl - px, bordercol);                    // lower side
        }

        if (xl != 0)
        {
            // Paint game view border lines
            _videoManager.BarScaledCoord(xl - px, yl - px, vw + px, px, 0);                      // upper border
            _videoManager.BarScaledCoord(xl, yl + vh, vw + px, px, bordercol - 2);          // lower border
            _videoManager.BarScaledCoord(xl - px, yl - px, px, vh + px, 0);                      // left border
            _videoManager.BarScaledCoord(xl + vw, yl - px, px, vh + px * 2, bordercol - 2);          // right border
            _videoManager.BarScaledCoord(xl - px, yl + vh, px, px, bordercol - 3);          // lower left highlight
        }
        else
        {
            // Just paint a lower border line
            _videoManager.BarScaledCoord(0, yl + vh, vw, px, bordercol - 2);       // lower border
        }
    }

    internal static void DrawStatusBorder(byte color)
    {
        int statusborderw = (_videoManager.screenWidth - _videoManager.scaleFactor * 320) / 2;

        _videoManager.BarScaledCoord(0, 0, _videoManager.screenWidth, _videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES - 3), color);
        _videoManager.BarScaledCoord(0, _videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES - 3),
            statusborderw + _videoManager.scaleFactor * 8, _videoManager.scaleFactor * (STATUSLINES - 4), color);
        _videoManager.BarScaledCoord(0, _videoManager.screenHeight - _videoManager.scaleFactor * 2, _videoManager.screenWidth, _videoManager.scaleFactor * 2, color);
        _videoManager.BarScaledCoord(_videoManager.screenWidth - statusborderw - _videoManager.scaleFactor * 8, _videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES - 3),
            statusborderw + _videoManager.scaleFactor * 8, _videoManager.scaleFactor * (STATUSLINES - 4), color);

        _videoManager.BarScaledCoord(statusborderw + _videoManager.scaleFactor * 9, _videoManager.screenHeight - _videoManager.scaleFactor * 3,
            _videoManager.scaleFactor * 97, _videoManager.scaleFactor * 1, color - 1);
        _videoManager.BarScaledCoord(statusborderw + _videoManager.scaleFactor * 106, _videoManager.screenHeight - _videoManager.scaleFactor * 3,
            _videoManager.scaleFactor * 161, _videoManager.scaleFactor * 1, color - 2);
        _videoManager.BarScaledCoord(statusborderw + _videoManager.scaleFactor * 267, _videoManager.screenHeight - _videoManager.scaleFactor * 3,
            _videoManager.scaleFactor * 44, _videoManager.scaleFactor * 1, color - 3);
        _videoManager.BarScaledCoord(_videoManager.screenWidth - statusborderw - _videoManager.scaleFactor * 9, _videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES - 4),
           _videoManager.scaleFactor * 1, _videoManager.scaleFactor * 20, color - 2);
        _videoManager.BarScaledCoord(_videoManager.screenWidth - statusborderw - _videoManager.scaleFactor * 9, _videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES / 2 - 4),
            _videoManager.scaleFactor * 1, _videoManager.scaleFactor * 14, color - 3);
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
        _mapManager.LoadMap(gamestate.mapon);

        //
        // spawn doors
        //
        InitActorList();                       // start spawning things with a clean slate
        InitDoorList();
        InitStaticList();


        int x, y;
        for (y = 0; y < _mapManager.mapheight; y++)
        {
            for (x = 0; x < _mapManager.mapwidth; x++)
            {
                int tile = _mapManager.MAPSPOT(x, y, 0);
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
        for (y = 0; y < _mapManager.mapheight; y++)
        {
            for (x = 0; x < _mapManager.mapwidth; x++)
            {
                var tile = _mapManager.MAPSPOT(x, y, 0);

                if (tile == MapConstants.AMBUSHTILE)
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
        CA_LoadAllSounds();
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
        SD_PlaySound("PLAYERDEATHSND");

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
        _videoManager.FinishPaletteShifts();

        _videoManager.BarScaledCoord(viewscreenx, viewscreeny, viewwidth, viewheight, 4);

        _inputManager.ClearKeysDown();

        _videoManager.FizzleFade(viewscreenx, viewscreeny, (uint)viewwidth, (uint)viewheight, 70, false);

        _inputManager.UserInput(100);
        SD_WaitSoundDone();
        ClearMemory();

        gamestate.lives--;

        if (gamestate.lives > -1)
        {
            gamestate.health = 100;
            gamestate.weapon = gamestate.bestweapon
                = gamestate.chosenweapon = weapontypes.wp_pistol;
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

        /*    if (SoundPositioned)
            {
                SetSoundLoc(globalsoundx,globalsoundy);
                SD_SetPosition(leftchannel,rightchannel);
            }*/

        for (i = 0; i < SDL_mixer.MIX_CHANNELS; i++)
        {
            if (channelSoundPos[i].valid != 0)
            {
                SetSoundLoc(channelSoundPos[i].globalsoundx,
                    channelSoundPos[i].globalsoundy);
                SD_SetPosition(i, leftchannel, rightchannel);
            }
        }
    }
}
