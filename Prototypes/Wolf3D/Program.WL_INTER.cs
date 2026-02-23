using SDL2;

namespace Wolf3D;

internal partial class Program
{
    internal static LRstruct[] LevelRatios = Enumerable.Range(0, LRpack).Select(x => new LRstruct()).ToArray();// new LRstruct[LRpack];
    internal static int lastBreathTime = 0;

    internal static void NonShareware()
    {
        VW_FadeOut();

        ClearMScreen();
        DrawStripes(10);

        fontnumber = 1;

        SETFONTCOLOR(READHCOLOR, BKGDCOLOR);
        PrintX = 110;
        PrintY = 15;

        US_Print("Attention");

        SETFONTCOLOR(HIGHLIGHT, BKGDCOLOR);
        WindowX = PrintX = 40;
        PrintY = 60;

        US_Print("This game is NOT shareware.\n");
        US_Print("Please do not distribute it.\n");
        US_Print("Thanks.\n\n");

        US_Print("        Id Software\n");

        VW_UpdateScreen();
        VW_FadeIn();
        IN_Ack();
    }

    internal static void PG13()
    {
        VW_FadeOut();
        VWB_Bar(0, 0, 320, 200, 0x82);     // background

        VWB_DrawPic(216, 110, (int)graphicnums.PG13PIC);
        VW_UpdateScreen();

        VW_FadeIn();
        IN_UserInput(TickBase * 7);

        VW_FadeOut();
    }

    internal static void DrawHighScores()
    {
        ushort i, w, h;

        ClearMScreen();
        DrawStripes(10);

        VWB_DrawPic(48, 0, (int)graphicnums.HIGHSCORESPIC);

        VWB_DrawPic(4 * 8, 68, (int)graphicnums.C_NAMEPIC);
        VWB_DrawPic(20 * 8, 68, (int)graphicnums.C_LEVELPIC);
        VWB_DrawPic(28 * 8, 68, (int)graphicnums.C_SCOREPIC);

        fontnumber = 0;
        SETFONTCOLOR(15, 0x29);


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

        VW_UpdateScreen();
    }

    internal static void CheckHighScore(int score, ushort other)
    {
        ushort i, j;
        int n;
        HighScore myscore = new HighScore();

        myscore.name = "";
        myscore.score = score;
        myscore.episode = (ushort)gamestate.episode;
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
        StartCPMusic(musicnames.ROSTER_MUS);
        DrawHighScores();

        VW_FadeIn();

        if (n != -1)
        {
            //
            // got a high score
            //
            PrintY = (ushort)(76 + (16 * n));
            PrintX = 4 * 8;
            backcolor = BORDCOLOR;
            fontcolor = 15;
            string str = new string(Scores[n].name);
            US_LineInput(PrintX, PrintY, ref str, "", true, MaxHighName, 100);
            Scores[n].name = str;
        }
        else
        {
            IN_ClearKeysDown();
            IN_UserInput(500);
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
        uint w = (uint)(WindowW - scaleFactor * 10);

        VWB_BarScaledCoord(WindowX + scaleFactor * 5, WindowY + WindowH - scaleFactor * 3,
            (int)w, scaleFactor * 2, BLACK);
        w = (uint)((int)w * current / total);
        if (w != 0)
        {
            VWB_BarScaledCoord(WindowX + scaleFactor * 5, WindowY + WindowH - scaleFactor * 3,
                (int)w, scaleFactor * 2, 0x37);       //SECONDCOLOR);
            VWB_BarScaledCoord(WindowX + scaleFactor * 5, WindowY + WindowH - scaleFactor * 3,
                (int)(w - scaleFactor * 1), scaleFactor * 1, 0x32);

        }
        VW_UpdateScreen();
        //      if (LastScan == sc_Escape)
        //      {
        //              IN_ClearKeysDown();
        //              return(true);
        //      }
        //      else
        return (false);
    }

    internal static void PreloadGraphics()
    {
        DrawLevel();
        ClearSplitVWB();           // set up for double buffering in split screen

        VWB_BarScaledCoord(0, 0, screenWidth, screenHeight - scaleFactor * (STATUSLINES - 1), bordercol);
        VWB_DrawPicScaledCoord(((screenWidth - scaleFactor * 224) / 16) * 8,
            (screenHeight - scaleFactor * (STATUSLINES + 48)) / 2, (int)graphicnums.GETPSYCHEDPIC);

        WindowX = (ushort)((screenWidth - scaleFactor * 224) / 2);
        WindowY = (ushort)((screenHeight - scaleFactor * (STATUSLINES + 48)) / 2);
        WindowW = (ushort)(scaleFactor * 28 * 8);
        WindowH = (ushort)(scaleFactor * 48);

        VW_UpdateScreen();
        VW_FadeIn();

        //      PM_Preload (PreloadUpdate);
        PreloadUpdate(10, 10);
        IN_UserInput(70);
        VW_FadeOut();

        DrawPlayBorder();
        VW_UpdateScreen();
    }

    internal struct times
    {
        public float time;
        public char[] timestr;

        public times()
        {
            timestr = new char[6];
        }

        public times(float time, string timestr)
        {
            this.time = time;
            this.timestr = timestr.ToCharArray();
        }
    }

    internal static void LevelCompleted()
    {
        const int VBLWAIT = 30;
        const int PAR_AMOUNT = 500;
        const int PERCENT100AMT = 10000;

        int x, i, min, sec, ratio, kr, sr, tr;
        string tempstr = "";
        int bonus, timeleft = 0;
        times[] parTimes = {
        //
        // Episode One Par Times
        //
        new (1.5f, "01:30"),
        new (2, "02:00"),
        new (2, "02:00"),
        new (3.5f, "03:30"),
        new (3, "03:00"),
        new (3, "03:00"),
        new (2.5f, "02:30"),
        new (2.5f, "02:30"),
        new (0, "??:??"),           // Boss level
        new (0, "??:??"),           // Secret level

        //
        // Episode Two Par Times
        //
        new (1.5f, "01:30"),
        new (3.5f, "03:30"),
        new (3, "03:00"),
        new (2, "02:00"),
        new (4, "04:00"),
        new (6, "06:00"),
        new (1, "01:00"),
        new (3, "03:00"),
        new (0, "??:??"),
        new(0, "??:??"),

        //
        // Episode Three Par Times
        //
        new (1.5f, "01:30"),
        new (1.5f, "01:30"),
        new (2.5f, "02:30"),
        new (2.5f, "02:30"),
        new (3.5f, "03:30"),
        new (2.5f, "02:30"),
        new (2, "02:00"),
        new (6, "06:00"),
        new (0, "??:??"),
        new (0, "??:??"),

        //
        // Episode Four Par Times
        //
        new (2, "02:00"),
        new (2, "02:00"),
        new (1.5f, "01:30"),
        new (1, "01:00"),
        new (4.5f, "04:30"),
        new (3.5f, "03:30"),
        new (2, "02:00"),
        new (4.5f, "04:30"),
        new (0, "??:??"),
        new(0, "??:??"),

        //
        // Episode Five Par Times
        //
        new (2.5f, "02:30"),
        new (1.5f, "01:30"),
        new (2.5f, "02:30"),
        new (2.5f, "02:30"),
        new (4, "04:00"),
        new (3, "03:00"),
        new (4.5f, "04:30"),
        new (3.5f, "03:30"),
        new (0, "??:??"),
        new (0, "??:??"),

        //
        // Episode Six Par Times
        //
        new (6.5f, "06:30"),
        new (4, "04:00"),
        new (4.5f, "04:30"),
        new (6, "06:00"),
        new (5, "05:00"),
        new (5.5f, "05:30"),
        new (5.5f, "05:30"),
        new (8.5f, "08:30"),
        new (0, "??:??"),
        new(0, "??:??")
    };

        ClearSplitVWB();           // set up for double buffering in split screen
        VWB_Bar(0, 0, 320, screenHeight / scaleFactor - STATUSLINES + 1, VIEWCOLOR);

        if (bordercol != VIEWCOLOR)
            DrawStatusBorder(VIEWCOLOR);

        StartCPMusic(musicnames.ENDLEVEL_MUS);

        //
        // do the intermission
        //
        IN_ClearKeysDown();
        IN_StartAck();
        VWB_DrawPic(0, 16, (int)graphicnums.L_GUYPIC);
        if (gamestate.mapon < LRpack)
        {
            Write(14, 2, "floor\ncompleted");
            Write(14, 7, STR_BONUS + "     0");
            Write(16, 10, STR_TIME);
            Write(16, 12, STR_PAR);
            Write(9, 14, STR_RAT2KILL);
            Write(5, 16, STR_RAT2SECRET);
            Write(1, 18, STR_RAT2TREASURE);
            Write(26, 2, (gamestate.mapon + 1).ToString());
            Write(26, 12, new string(parTimes[gamestate.episode * 10 + gamestate.mapon].timestr));
            //
            // PRINT TIME
            //
            sec = gamestate.TimeCount / 70;

            if (sec > 99 * 60)      // 99 minutes max
                sec = 99 * 60;

            if (gamestate.TimeCount < parTimes[gamestate.episode * 10 + gamestate.mapon].time * 4200)
                timeleft = (int)((parTimes[gamestate.episode * 10 + gamestate.mapon].time * 4200) / 70 - sec);

            min = sec / 60;
            sec %= 60;
            i = 26 * 8;
            VWB_DrawPic(i, 10 * 8, (int)graphicnums.L_NUM0PIC + (min / 10));
            i += 2 * 8;
            VWB_DrawPic(i, 10 * 8, (int)graphicnums.L_NUM0PIC + (min % 10));
            i += 2 * 8;
            Write(i / 8, 10, ":");
            i += 1 * 8;
            VWB_DrawPic(i, 10 * 8, (int)graphicnums.L_NUM0PIC + (sec / 10));
            i += 2 * 8;
            VWB_DrawPic(i, 10 * 8, (int)graphicnums.L_NUM0PIC + (sec % 10));

            VW_UpdateScreen();
            VW_FadeIn();


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
                        SD_PlaySound((int)soundnames.ENDBONUS1SND);
                    VW_UpdateScreen();
                    while (SD_SoundPlaying() != 0)
                        BJ_Breathe();
                    if (IN_CheckAck())
                        goto done;
                }

                VW_UpdateScreen();

                SD_PlaySound((int)soundnames.ENDBONUS2SND);
                while (SD_SoundPlaying() != 0)
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
                    SD_PlaySound((int)soundnames.ENDBONUS1SND);
                VW_UpdateScreen();
                while (SD_SoundPlaying() != 0)
                    BJ_Breathe();

                if (IN_CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                VW_WaitVBL(VBLWAIT);
                SD_StopSound();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                x = (RATIOXX - 1) - tempstr.Length * 2;
                Write(x, 7, tempstr);
                VW_UpdateScreen();
                SD_PlaySound((int)soundnames.PERCENT100SND);
            }
            else if (ratio == 0)
            {
                VW_WaitVBL(VBLWAIT);
                SD_StopSound();
                SD_PlaySound((int)soundnames.NOBONUSSND);
            }
            else
                SD_PlaySound((int)soundnames.ENDBONUS2SND);

            VW_UpdateScreen();
            while (SD_SoundPlaying() != 0)
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
                    SD_PlaySound((int)soundnames.ENDBONUS1SND);
                VW_UpdateScreen();
                while (SD_SoundPlaying() != 0)
                    BJ_Breathe();

                if (IN_CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                VW_WaitVBL(VBLWAIT);
                SD_StopSound();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                x = (RATIOXX - 1) - tempstr.Length * 2;
                Write(x, 7, tempstr);
                VW_UpdateScreen();
                SD_PlaySound((int)soundnames.PERCENT100SND);
            }
            else if (ratio == 0)
            {
                VW_WaitVBL(VBLWAIT);
                SD_StopSound();
                SD_PlaySound((int)soundnames.NOBONUSSND);
            }
            else
                SD_PlaySound((int)soundnames.ENDBONUS2SND);
            VW_UpdateScreen();
            while (SD_SoundPlaying() != 0)
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
                    SD_PlaySound((int)soundnames.ENDBONUS1SND);
                VW_UpdateScreen();
                while (SD_SoundPlaying() != 0)
                    BJ_Breathe();
                if (IN_CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                VW_WaitVBL(VBLWAIT);
                SD_StopSound();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                x = (RATIOXX - 1) - tempstr.Length * 2;
                Write(x, 7, tempstr);
                VW_UpdateScreen();
                SD_PlaySound((int)soundnames.PERCENT100SND);
            }
            else if (ratio == 0)
            {
                VW_WaitVBL(VBLWAIT);
                SD_StopSound();
                SD_PlaySound((int)soundnames.NOBONUSSND);
            }
            else
                SD_PlaySound((int)soundnames.ENDBONUS2SND);
            VW_UpdateScreen();
            while (SD_SoundPlaying() != 0)
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
            LevelRatios[gamestate.mapon].kill = (short)kr;
            LevelRatios[gamestate.mapon].secret = (short)sr;
            LevelRatios[gamestate.mapon].treasure = (short)tr;
            LevelRatios[gamestate.mapon].time = min * 60 + sec;
        }
        else
        {
            Write(14, 4, "secret floor\n completed!");
            Write(10, 16, "15000 bonus!");

            VW_UpdateScreen();
            VW_FadeIn();

            GivePoints(15000);
        }


        DrawScore();
        VW_UpdateScreen();

        lastBreathTime = (int)GetTimeCount();
        IN_StartAck();
        while (!IN_CheckAck())
            BJ_Breathe();

        //
        // done
        //

        VW_FadeOut();
        DrawPlayBorder();
    }

    internal static void Victory()
    {
        int sec;
        int i, min, kr, sr, tr, x;
        string tempstr;
        const int RATIOX = 6;
        const int RATIOY = 14;
        const int TIMEX = 14;
        const int TIMEY = 8;

        StartCPMusic(musicnames.URAHERO_MUS);
        ClearSplitVWB();

        VWB_Bar(0, 0, 320, screenHeight / scaleFactor - STATUSLINES + 1, VIEWCOLOR);
        if (bordercol != VIEWCOLOR)
            DrawStatusBorder(VIEWCOLOR);
        Write(18, 2, STR_YOUWIN);

        Write(TIMEX, TIMEY - 2, STR_TOTALTIME);

        Write(12, RATIOY - 2, "averages");

        Write(RATIOX + 8, RATIOY, STR_RATKILL);
        Write(RATIOX + 4, RATIOY + 2, STR_RATSECRET);
        Write(RATIOX, RATIOY + 4, STR_RATTREASURE);

        VWB_DrawPic(8, 4, (int)graphicnums.L_BJWINSPIC);

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
        VWB_DrawPic(i, TIMEY * 8, (int)graphicnums.L_NUM0PIC + (min / 10));
        i += 2 * 8;
        VWB_DrawPic(i, TIMEY * 8, (int)graphicnums.L_NUM0PIC + (min % 10));
        i += 2 * 8;
        Write(i / 8, TIMEY, ":");
        i += 1 * 8;
        VWB_DrawPic(i, TIMEY * 8, (int)graphicnums.L_NUM0PIC + (sec / 10));
        i += 2 * 8;
        VWB_DrawPic(i, TIMEY * 8, (int)graphicnums.L_NUM0PIC + (sec % 10));
        VW_UpdateScreen();

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
        if (gamestate.difficulty >= (short)difficultytypes.gd_medium)
        {
            VWB_DrawPic(30 * 8, TIMEY * 8, (int)graphicnums.C_TIMECODEPIC);
            fontnumber = 0;
            fontcolor = READHCOLOR;
            PrintX = 30 * 8 - 3;
            PrintY = TIMEY * 8 + 8;
            PrintX += 4;
            var a = (((min / 10) ^ (min % 10)) ^ 0xa) + 'A';
            var b = (int)((((sec / 10) ^ (sec % 10)) ^ 0xa) + 'A');
            var c = (tempstr[0] ^ tempstr[1]) + 'A';
            tempstr = $"{a}{b}{c}";
            US_Print(tempstr);
        }

        fontnumber = 1;

        VW_UpdateScreen();
        VW_FadeIn();

        IN_Ack();

        VW_FadeOut();
        if (screenHeight % 200 != 0)
            VL_ClearScreen(0);

        MainMenu[(int)menuitems.savegame].active = 0;  // ADDEDFIX 3 - Tricob

        EndText();
    }

    //
    // Breathe Mr. BJ!!!
    //
    internal static void BJ_Breathe()
    {
        int which = 0, max = 10; // static!!!
        int[] pics = { (int)graphicnums.L_GUYPIC, (int)graphicnums.L_GUY2PIC };

        SDL.SDL_Delay(5);

        if ((int)GetTimeCount() - lastBreathTime > max)
        {
            which ^= 1;
            VWB_DrawPic(0, 16, pics[which]);
            VW_UpdateScreen();
            lastBreathTime = (int)GetTimeCount();
            max = 35;
        }
    }

    internal static void Write(int x, int y, string text)
    {
        int[] alpha = { (int)graphicnums.L_NUM0PIC, (int)graphicnums.L_NUM1PIC, (int)graphicnums.L_NUM2PIC, (int)graphicnums.L_NUM3PIC, (int)graphicnums.L_NUM4PIC, (int)graphicnums.L_NUM5PIC,
            (int)graphicnums.L_NUM6PIC, (int)graphicnums.L_NUM7PIC, (int)graphicnums.L_NUM8PIC, (int)graphicnums.L_NUM9PIC,(int)graphicnums. L_COLONPIC, 0, 0, 0, 0, 0, 0, (int)graphicnums.L_APIC, (int)graphicnums.L_BPIC,
            (int)graphicnums.L_CPIC, (int)graphicnums.L_DPIC, (int)graphicnums.L_EPIC, (int)graphicnums.L_FPIC, (int)graphicnums.L_GPIC, (int)graphicnums.L_HPIC, (int)graphicnums.L_IPIC, (int)graphicnums.L_JPIC, (int)graphicnums.L_KPIC,
            (int)graphicnums.L_LPIC, (int)graphicnums.L_MPIC, (int)graphicnums.L_NPIC, (int)graphicnums.L_OPIC, (int)graphicnums.L_PPIC, (int)graphicnums.L_QPIC, (int)graphicnums.L_RPIC, (int)graphicnums.L_SPIC, (int)graphicnums.L_TPIC,
            (int)graphicnums.L_UPIC, (int)graphicnums.L_VPIC, (int)graphicnums.L_WPIC, (int)graphicnums.L_XPIC, (int)graphicnums.L_YPIC, (int)graphicnums.L_ZPIC
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
                        VWB_DrawPic(nx, ny, (int)graphicnums.L_EXPOINTPIC);
                        nx += 8;
                        continue;
                    case '\'':
                        VWB_DrawPic(nx, ny, (int)graphicnums.L_APOSTROPHEPIC);
                        nx += 8;
                        continue;
                case ' ':
                        break;

                    case ':':
                        VWB_DrawPic(nx, ny, (int)graphicnums.L_COLONPIC);
                        nx += 8;
                        continue;

                    case '%':
                        VWB_DrawPic(nx, ny, (int)graphicnums.L_PERCENTPIC);
                        break;

                    default:
                        VWB_DrawPic(nx, ny, alpha[ch]);
                        break;
                }
                nx += 16;
            }
        }
    }
}
