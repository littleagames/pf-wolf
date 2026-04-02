using SDL2;
using Wolf3D.Extensions;
using Wolf3D.Managers;
using Wolf3D.Mappers;

namespace Wolf3D;

internal partial class Program
{
    internal static LRstruct[] LevelRatios = Enumerable.Range(0, MapManager.NUMMAPS).Select(x => new LRstruct()).ToArray();// new LRstruct[LRpack];
    internal static int lastBreathTime = 0;

    internal static void NonShareware()
    {
        _videoManager.FadeOut();

        ClearMScreen();
        DrawStripes(10);

        fontnumber = 1;

        SETFONTCOLOR("READHCOLOR", "BKGDCOLOR");
        PrintX = 110;
        PrintY = 15;

        US_Print("Attention");

        SETFONTCOLOR("HIGHLIGHT", "BKGDCOLOR");
        WindowX = PrintX = 40;
        PrintY = 60;

        US_Print("This game is NOT shareware.\n");
        US_Print("Please do not distribute it.\n");
        US_Print("Thanks.\n\n");

        US_Print("        Id Software\n");

        _videoManager.Update();
        _videoManager.FadeIn();
        _inputManager.Ack();
    }

    internal static void PG13()
    {
        _videoManager.FadeOut();
        _videoManager.Bar(0, 0, 320, 200, "Light Blue");     // background

        _graphicManager.DrawPic("pg13", 216, 110);
        _videoManager.Update();

        _videoManager.FadeIn();
        _inputManager.UserInput(TickBase * 7);

        _videoManager.FadeOut();
    }

    internal static void DrawHighScores()
    {
        ushort i, w, h;

        ClearMScreen();
        DrawStripes(10);

        _graphicManager.DrawPic("highscores", 48, 0);

        _graphicManager.DrawPic("c_name", 4 * 8, 68);
        _graphicManager.DrawPic("c_level", 20 * 8, 68);
        _graphicManager.DrawPic("c_score", 28 * 8, 68);
        //_graphicManager.DrawPic(35 * 8, 68, graphicnums.C_CODEPIC);

        fontnumber = 0;
        SETFONTCOLOR("White", "BORDCOLOR");


        for (i = 0; i < MaxScores; i++)
        {
            HighScore s = Scores[i];
            PrintY = (ushort)(76 + (16 * i));

            //
            // name
            //
            PrintX = 4 * 8;
            US_Print(new string(s.name));

            //
            // level
            //
            var buffer = s.completed.ToString();
            USL_MeasureString(buffer, out w, out h);
            PrintX = (ushort)((22 * 8) - w);

            PrintX -= 6;
            var buffer1 = (s.episode + 1).ToString();
            US_Print("E");
            US_Print(buffer1);
            US_Print("/L");

            US_Print(buffer);

            //
            // score
            //

            buffer = s.score.ToString();
            USL_MeasureString(buffer, out w, out h);
            PrintX = (ushort)((34 * 8) - 8 - w);
            US_Print(buffer);
        }

        _videoManager.Update();
    }

    internal static void CheckHighScore(int score, ushort other)
    {
        ushort i, j;
        int n;
        HighScore myscore = new HighScore();

        myscore.name = "";
        myscore.score = score;
        myscore.episode = (ushort)gamestate.cluster;
        myscore.completed = other;

        for (i = 0, n = -1; i < MaxScores; i++)
        {
            if ((myscore.score > Scores[i].score)
                || ((myscore.score == Scores[i].score) && (myscore.completed > Scores[i].completed)))
            {
                for (j = MaxScores; --j > i;)
                    Scores[j] = Scores[j - 1];
                Scores[i] = myscore;
                n = i;
                break;
            }
        }
        StartCPMusic("Roster");
        DrawHighScores();

        _videoManager.FadeIn();

        if (n != -1)
        {
            //
            // got a high score
            //
            PrintY = (ushort)(76 + (16 * n));
            PrintX = 4 * 8;
            backcolor = "BORDCOLOR";
            fontcolor = "White";
            string str = new string(Scores[n].name);
            US_LineInput(PrintX, PrintY, ref str, "", true, MaxHighName, 100);
            Scores[n].name = str;
        }
        else
        {
            _inputManager.ClearKeysDown();
            _inputManager.UserInput(500);
        }
    }

    internal static void ClearSplitVWB()
    {
        WindowX = 0;
        WindowY = 0;
        WindowW = 320;
        WindowH = 160;
    }

    internal static bool PreloadUpdate(uint current, uint total)
    {
        uint w = (uint)(WindowW - _videoManager.scaleFactor * 10);

        _videoManager.BarScaledCoord(WindowX + _videoManager.scaleFactor * 5, WindowY + WindowH - _videoManager.scaleFactor * 3,
            (int)w, _videoManager.scaleFactor * 2, "Black");
        w = (uint)((int)w * current / total);
        if (w != 0)
        {
            _videoManager.BarScaledCoord(WindowX + _videoManager.scaleFactor * 5, WindowY + WindowH - _videoManager.scaleFactor * 3,
                (int)w, _videoManager.scaleFactor * 2, "SECONDCOLOR");       //SECONDCOLOR 0x37);
            _videoManager.BarScaledCoord(WindowX + _videoManager.scaleFactor * 5, WindowY + WindowH - _videoManager.scaleFactor * 3,
                (int)(w - _videoManager.scaleFactor * 1), _videoManager.scaleFactor * 1, "FIRSTCOLOR"); // 0x32

        }
        _videoManager.Update();
        //      if (LastScan == sc_Escape)
        //      {
        //              _inputManager.ClearKeysDown();
        //              return(true);
        //      }
        //      else
        return (false);
    }

    internal static void PreloadGraphics()
    {
        DrawLevel();
        ClearSplitVWB();           // set up for double buffering in split screen

        _videoManager.BarScaledCoord(0, 0, _videoManager.screenWidth, _videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES - 1), bordercol);

        // TODO: This may have just been centered in the viewport area
        //    ((_videoManager.screenWidth - _videoManager.scaleFactor * 224) / 16) * 8,
        //    (_videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES + 48)) / 2,
        _graphicManager.DrawPic("getpsyched", (320 - 224) / 2, (200 - STATUSLINES - 48) / 2);

        WindowX = (ushort)((_videoManager.screenWidth - _videoManager.scaleFactor * 224) / 2);
        WindowY = (ushort)((_videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES + 48)) / 2);
        WindowW = (ushort)(_videoManager.scaleFactor * 28 * 8);
        WindowH = (ushort)(_videoManager.scaleFactor * 48);

        _videoManager.Update();
        _videoManager.FadeIn();

        //      PM_Preload (PreloadUpdate);
        PreloadUpdate(10, 10);
        _inputManager.UserInput(70);
        _videoManager.FadeOut();

        DrawPlayBorder();
        _videoManager.Update();
    }

    private static string[] numpics = new[] { "FONTL048", "FONTL049", "FONTL050", "FONTL051", "FONTL052", "FONTL053", "FONTL054", "FONTL055", "FONTL056", "FONTL057" };
    internal static void LevelCompleted()
    {
        var language = _assetManager.GetText("en-us");
        const int VBLWAIT = 30;
        const int PAR_AMOUNT = 500;
        const int PERCENT100AMT = 10000;

        int x, i, min, sec, ratio, kr, sr, tr;
        string tempstr = "";
        int bonus, timeleft = 0;

        ClearSplitVWB();           // set up for double buffering in split screen
        _videoManager.Bar(0, 0, 320, _videoManager.screenHeight / _videoManager.scaleFactor - STATUSLINES + 1, "VIEWCOLOR");

        if (bordercol != "VIEWCOLOR")
            DrawStatusBorder("VIEWCOLOR");

        StartCPMusic("EndLevel");

        //
        // do the intermission
        //
        _inputManager.ClearKeysDown();
        _inputManager.StartAck();
        _graphicManager.DrawPic("l_guy", 0, 16);

        var mapInfo = _assetManager.GetGameInfo().Maps[gamestate.mapon];
        //if (gamestate.mapon < LRpack)
        {
            Write(14, 2, "floor\ncompleted");
            Write(14, 7, "$STR_BONUS".ToLanguageText(language) + "     0");
            Write(16, 10, "$STR_TIME".ToLanguageText(language));
            Write(16, 12, "$STR_PAR".ToLanguageText(language));
            Write(9, 14, "$STR_RAT2KILL".ToLanguageText(language));
            Write(5, 16, "$STR_RAT2SECRET".ToLanguageText(language));
            Write(1, 18, "$STR_RAT2TREASURE".ToLanguageText(language));
            Write(26, 2, (mapInfo.FloorNumber).ToString());
            Write(26, 12, int.SecondsAsTime(mapInfo.ParTime));
            //
            // PRINT TIME
            //
            sec = gamestate.TimeCount / 70;

            if (sec > 99 * 60)      // 99 minutes max
                sec = 99 * 60;
            if (gamestate.TimeCount < mapInfo.ParTime * 70)
                timeleft = mapInfo.ParTime - sec;

            min = sec / 60;
            sec %= 60;
            i = 26 * 8;
            _graphicManager.DrawPic(numpics[(min / 10)], i, 10 * 8);
            i += 2 * 8;
            _graphicManager.DrawPic(numpics[(min % 10)], i, 10 * 8);
            i += 2 * 8;
            Write(i / 8, 10, ":");
            i += 1 * 8;
            _graphicManager.DrawPic(numpics[(sec / 10)], i, 10 * 8);
            i += 2 * 8;
            _graphicManager.DrawPic(numpics[(sec % 10)], i, 10 * 8);

            _videoManager.Update();
            _videoManager.FadeIn();


            //
            // FIGURE RATIOS OUT BEFOREHAND
            //
            kr = sr = tr = 0;
            if (gamestate.killtotal != 0)
                kr = (gamestate.killcount * 100) / gamestate.killtotal;
            if (gamestate.secrettotal != 0)
                sr = (gamestate.secretcount * 100) / gamestate.secrettotal;
            if (gamestate.treasuretotal != 0)
                tr = (gamestate.treasurecount * 100) / gamestate.treasuretotal;


            //
            // PRINT TIME BONUS
            //
            bonus = timeleft * PAR_AMOUNT;
            if (bonus != 0)
            {
                for (i = 0; i <= timeleft; i++)
                {
                    tempstr = (i * PAR_AMOUNT).ToString();
                    x = 36 - tempstr.Length * 2;
                    Write(x, 7, tempstr);
                    if ((i % (PAR_AMOUNT / 10)) == 0)
                        SD_PlaySound("ENDBONUS1SND");
                    _videoManager.Update();
                    while (SD_SoundPlaying() != "")
                        BJ_Breathe();
                    if (_inputManager.CheckAck())
                        goto done;
                }

                _videoManager.Update();

                SD_PlaySound("ENDBONUS2SND");
                while (SD_SoundPlaying() != "")
                    BJ_Breathe();
            }

            const int RATIOXX = 37;
            //
            // KILL RATIO
            //
            ratio = kr;
            for (i = 0; i <= ratio; i++)
            {
                tempstr = i.ToString();
                x = RATIOXX - tempstr.Length * 2;
                Write(x, 14, tempstr);
                if ((i % 10) == 0)
                    SD_PlaySound("ENDBONUS1SND");
                _videoManager.Update();
                while (SD_SoundPlaying() != "")
                    BJ_Breathe();

                if (_inputManager.CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                GameEngineManager.WaitVBL(VBLWAIT);
                SD_StopSound();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                x = (RATIOXX - 1) - tempstr.Length * 2;
                Write(x, 7, tempstr);
                _videoManager.Update();
                SD_PlaySound("PERCENT100SND");
            }
            else if (ratio == 0)
            {
                GameEngineManager.WaitVBL(VBLWAIT);
                SD_StopSound();
                SD_PlaySound("NOBONUSSND");
            }
            else
                SD_PlaySound("ENDBONUS2SND");

            _videoManager.Update();
            while (SD_SoundPlaying() != "")
                BJ_Breathe();

            //
            // SECRET RATIO
            //
            ratio = sr;
            for (i = 0; i <= ratio; i++)
            {
                tempstr = i.ToString();
                x = RATIOXX - tempstr.Length * 2;
                Write(x, 16, tempstr);
                if ((i % 10) == 0)
                    SD_PlaySound("ENDBONUS1SND");
                _videoManager.Update();
                while (SD_SoundPlaying() != "")
                    BJ_Breathe();

                if (_inputManager.CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                GameEngineManager.WaitVBL(VBLWAIT);
                SD_StopSound();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                x = (RATIOXX - 1) - tempstr.Length * 2;
                Write(x, 7, tempstr);
                _videoManager.Update();
                SD_PlaySound("PERCENT100SND");
            }
            else if (ratio == 0)
            {
                GameEngineManager.WaitVBL(VBLWAIT);
                SD_StopSound();
                SD_PlaySound("NOBONUSSND");
            }
            else
                SD_PlaySound("ENDBONUS2SND");
            _videoManager.Update();
            while (SD_SoundPlaying() != "")
                BJ_Breathe();

            //
            // TREASURE RATIO
            //
            ratio = tr;
            for (i = 0; i <= ratio; i++)
            {
                tempstr = i.ToString();
                x = RATIOXX - tempstr.Length * 2;
                Write(x, 18, tempstr);
                if ((i % 10) == 0)
                    SD_PlaySound("ENDBONUS1SND");
                _videoManager.Update();
                while (SD_SoundPlaying() != "")
                    BJ_Breathe();
                if (_inputManager.CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                GameEngineManager.WaitVBL(VBLWAIT);
                SD_StopSound();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                x = (RATIOXX - 1) - tempstr.Length * 2;
                Write(x, 7, tempstr);
                _videoManager.Update();
                SD_PlaySound("PERCENT100SND");
            }
            else if (ratio == 0)
            {
                GameEngineManager.WaitVBL(VBLWAIT);
                SD_StopSound();
                SD_PlaySound("NOBONUSSND");
            }
            else
                SD_PlaySound("ENDBONUS2SND");
            _videoManager.Update();
            while (SD_SoundPlaying() != "")
                BJ_Breathe();


            //
            // JUMP STRAIGHT HERE IF KEY PRESSED
            //
        done:
            tempstr = kr.ToString();
            x = RATIOXX - tempstr.Length * 2;
            Write(x, 14, tempstr);

            tempstr = sr.ToString();
            x = RATIOXX - tempstr.Length * 2;
            Write(x, 16, tempstr);

            tempstr = tr.ToString();
            x = RATIOXX - tempstr.Length * 2;
            Write(x, 18, tempstr);

            bonus = (int)timeleft * PAR_AMOUNT +
                (PERCENT100AMT * ((kr >= 100) ? 1 : 0)) +
                (PERCENT100AMT * ((sr >= 100) ? 1 : 0)) +
                (PERCENT100AMT * ((tr >= 100) ? 1 : 0));

            GivePoints(bonus);
            tempstr = bonus.ToString();
            x = 36 - tempstr.Length * 2;
            Write(x, 7, tempstr);

            //
            // SAVE RATIO INFORMATION FOR ENDGAME
            //
            // TODO: Store this via "cluster"
            var mapon = MapInfoMappings.MapAssetToIndex[gamestate.mapon];
            LevelRatios[mapon].kill = (short)kr;
            LevelRatios[mapon].secret = (short)sr;
            LevelRatios[mapon].treasure = (short)tr;
            LevelRatios[mapon].time = min * 60 + sec;

            // TODO This should be set up as different LevelCompleted "screens"???
        }
        //else
        //{
        //    Write(14, 4, "secret floor\n completed!");
        //    Write(10, 16, "15000 bonus!");

        //    _videoManager.Update();
        //    _videoManager.FadeIn();

        //    GivePoints(15000);
        //}


        DrawScore();
        _videoManager.Update();

        lastBreathTime = (int)GameEngineManager.GetTimeCount();
        _inputManager.StartAck();
        while (!_inputManager.CheckAck())
            BJ_Breathe();

        //
        // done
        //

        _videoManager.FadeOut();
        DrawPlayBorder();
    }

    internal static void Victory()
    {
        var language = _assetManager.GetText("en-us");
        int sec;
        int i, min, kr, sr, tr, x;
        string tempstr;
        const int RATIOX = 6;
        const int RATIOY = 14;
        const int TIMEX = 14;
        const int TIMEY = 8;

        StartCPMusic("YoureAHero");
        ClearSplitVWB();

        _videoManager.Bar(0, 0, 320, _videoManager.screenHeight / _videoManager.scaleFactor - STATUSLINES + 1, "VIEWCOLOR");
        if (bordercol != "VIEWCOLOR")
            DrawStatusBorder("VIEWCOLOR");
        Write(18, 2, "$STR_YOUWIN".ToLanguageText(language));

        Write(TIMEX, TIMEY - 2, "$STR_TOTALTIME".ToLanguageText(language));

        Write(12, RATIOY - 2, "averages");

        Write(RATIOX + 8, RATIOY, "$STR_RATKILL".ToLanguageText(language));
        Write(RATIOX + 4, RATIOY + 2, "$STR_RATSECRET".ToLanguageText(language));
        Write(RATIOX, RATIOY + 4, "$STR_RATTREASURE".ToLanguageText(language));

        _graphicManager.DrawPic("L_BJWINS", 8, 4);
        const int LRpack = 8;
        for (kr = sr = tr = sec = i = 0; i < LRpack; i++)
        {
            sec += LevelRatios[i].time;
            kr += LevelRatios[i].kill;
            sr += LevelRatios[i].secret;
            tr += LevelRatios[i].treasure;
        }

        kr /= LRpack;
        sr /= LRpack;
        tr /= LRpack;

        min = sec / 60;
        sec %= 60;

        if (min > 99)
            min = sec = 99;

        i = TIMEX * 8 + 1;
        _graphicManager.DrawPic(numpics[(min / 10)], i, TIMEY * 8);
        i += 2 * 8;
        _graphicManager.DrawPic(numpics[(min % 10)], i, TIMEY * 8);
        i += 2 * 8;
        Write(i / 8, TIMEY, ":");
        i += 1 * 8;
        _graphicManager.DrawPic(numpics[(sec / 10)], i, TIMEY * 8);
        i += 2 * 8;
        _graphicManager.DrawPic(numpics[(sec % 10)], i, TIMEY * 8);
        _videoManager.Update();

        tempstr = kr.ToString();
        x = RATIOX + 24 - tempstr.Length * 2;
        Write(x, RATIOY, tempstr);

        tempstr = sr.ToString();
        x = RATIOX + 24 - tempstr.Length * 2;
        Write(x, RATIOY + 2, tempstr);

        tempstr = tr.ToString();
        x = RATIOX + 24 - tempstr.Length * 2;
        Write(x, RATIOY + 4, tempstr);

        //
        // TOTAL TIME VERIFICATION CODE
        //
        //if (gamestate.difficulty >= difficultytypes.gd_medium)
        //{
        //    _graphicManager.DrawPic(30 * 8, TIMEY * 8, graphicnums.C_TIMECODEPIC);
        //    fontnumber = 0;
        //    fontcolor = READHCOLOR;
        //    PrintX = 30 * 8 - 3;
        //    PrintY = TIMEY * 8 + 8;
        //    PrintX += 4;
        //    char a = (char)((((min / 10) ^ (min % 10)) ^ 0xa) + 'A');
        //    char b = (char)((((sec / 10) ^ (sec % 10)) ^ 0xa) + 'A');
        //    char c = (char)((tempstr[0] ^ tempstr[1]) + 'A');
        //    tempstr = $"{a}{b}{c}";
        //    US_Print(tempstr);
        //}

        fontnumber = 1;

        _videoManager.Update();
        _videoManager.FadeIn();

        _inputManager.Ack();

        _videoManager.FadeOut();
        if (_videoManager.screenHeight % 200 != 0)
            _videoManager.ClearScreen(0);

        MainMenu[(int)menuitems.savegame].active = 0;  // ADDEDFIX 3 - Tricob

        EndText();
    }

    //
    // Breathe Mr. BJ!!!
    //
    private static int bj_which = 0, bj_max = 10;
    internal static void BJ_Breathe()
    {
        string[] pics = { "L_Guy", "L_GUY2" };

        GameEngineManager.DelayMs(5);

        if ((int)GameEngineManager.GetTimeCount() - lastBreathTime > bj_max)
        {
            bj_which ^= 1;
            _graphicManager.DrawPic(pics[bj_which], 0, 16);
            _videoManager.Update();
            lastBreathTime = (int)GameEngineManager.GetTimeCount();
            bj_max = 35;
        }
    }

    internal static void Write(int x, int y, string text)
    {
        // TODO: This would be a good ASCII text map in the pk3, which allows for easy graphic pickings
        string[] alpha = { "FONTL048", "FONTL049", "FONTL050", "FONTL051", "FONTL052", "FONTL053",
            "FONTL054", "FONTL055", "FONTL056", "FONTL057", "FONTL058", "", "", "", "", "", "", "FONTL065", "FONTL066",
            "FONTL067", "FONTL068", "FONTL069", "FONTL070", "FONTL071", "FONTL072", "FONTL073", "FONTL074", "FONTL075",
            "FONTL076", "FONTL077", "FONTL078", "FONTL079", "FONTL080", "FONTL081", "FONTL082", "FONTL083", "FONTL084",
            "FONTL085", "FONTL086", "FONTL087", "FONTL088", "FONTL089", "FONTL090"
        };

        int i, ox, nx, ny, len = text.Length;
        char ch;

        ox = nx = x * 8;
        ny = y * 8;
        for (i = 0; i < len; i++)
        {
            if (text[i] == '\n')
            {
                nx = ox;
                ny += 16;
            }
            else
            {
                ch = text[i];

                if (char.ToUpper(ch) != 0)
                    ch = char.ToUpper(ch);

                ch -= '0';

                switch (text[i])
                {
                    case '!':
                        _graphicManager.DrawPic("FONTL033", nx, ny);
                        nx += 8;
                        continue;
                    case '\'':
                        _graphicManager.DrawPic("FONTL039", nx, ny);
                        nx += 8;
                        continue;
                case ' ':
                        break;

                    case ':':
                        _graphicManager.DrawPic("FONTL058", nx, ny);
                        nx += 8;
                        continue;

                    case '%':
                        _graphicManager.DrawPic("FONTL037", nx, ny);
                        break;

                    default:
                        _graphicManager.DrawPic(alpha[ch], nx, ny);
                        break;
                }
                nx += 16;
            }
        }
    }
}
