using SDL2;
using Wolf3D.Assets;
using Wolf3D.Constants;
using Wolf3D.Extensions;
using Wolf3D.Fonts;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    internal static Dictionary<string, LRstruct> LevelRatios = new Dictionary<string, LRstruct>();
    internal static int lastBreathTime = 0;

    internal static void NonShareware()
    {
        _videoManager.FadeOut();

        ClearMScreen();
        DrawStripes(10);

        TextAt(110, 15, MenuStyle("READHCOLOR")).Print("Attention");

        var notice = TextAt(40, 60, MenuStyle("HIGHLIGHT"));
        notice.Print("This game is NOT shareware.\n");
        notice.Print("Please do not distribute it.\n");
        notice.Print("Thanks.\n\n");

        notice.Print("        Id Software\n");

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
        _inputManager.UserInput(Timing.TickBase * 7);

        _videoManager.FadeOut();
    }

    /// <summary>
    /// Set in a high score's completed level when the game was won, so it ranks above a score
    /// that ended part way and can show the game-info high-scores won-pic
    /// </summary>
    internal const ushort HighScoreWon = 0x8000;

    private static HighScoresInfo HighScoresLayout => _gameEngineManager.GetGameInfo().HighScores;

    internal static void DrawHighScores()
    {
        var layout = HighScoresLayout;
        var style = new TextStyle(layout.Font, layout.Color, "BORDCOLOR");

        ClearMScreen();
        DrawStripes(10);

        _graphicManager.DrawPic(layout.Pic, layout.PicX, layout.PicY);
        foreach (var header in layout.Headers)
            _graphicManager.DrawPic(header.Pic, header.X, header.Y);

        for (int i = 0; i < MaxScores; i++)
        {
            HighScore s = Scores[i];
            int y = layout.RowY + (16 * i);

            //
            // name
            //
            TextAt(layout.NameX, y, style).Print(s.name);

            //
            // level
            //
            var buffer = (s.completed & ~HighScoreWon).ToString();
            int x = layout.LevelRight - TextWidth(buffer, style.Font);
            if ((s.completed & HighScoreWon) != 0 && !string.IsNullOrEmpty(layout.WonPic))
                _graphicManager.DrawPic(layout.WonPic, x + 8, y - 1);
            else if (layout.ShowEpisode)
                TextAt(x - 6, y, style).Print($"E{s.episode + 1}/L{buffer}");
            else
                TextAt(x, y, style).Print(buffer);

            //
            // score
            //
            buffer = s.score.ToString();
            TextAt(layout.ScoreRight - TextWidth(buffer, style.Font), y, style).Print(buffer);
        }

        _videoManager.Update();
    }

    /// <summary>
    /// Ranks the score among the high scores, and has a name typed for it if it made the list.
    /// The level recorded is the current map's floor number, marked as won when <paramref name="won"/>.
    /// </summary>
    internal static void CheckHighScore(int score, bool won)
    {
        ushort i, j;
        int n;
        HighScore myscore = new HighScore();

        var gameInfo = _gameEngineManager.GetGameInfo();
        int floor = gameInfo.Maps.TryGetValue(gamestate.mapon, out var mapInfo) ? mapInfo.FloorNumber : 1;

        myscore.name = "";
        myscore.score = score;
        myscore.episode = (ushort)Math.Max(gamestate.cluster - 1, 0);   // shown from 1, as id's were
        myscore.completed = (ushort)(Math.Clamp(floor, 0, HighScoreWon - 1) | (won ? HighScoreWon : 0));

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
        StartCPMusic(HIGHSCORESSONG);
        DrawHighScores();

        _videoManager.FadeIn();

        if (n != -1)
        {
            //
            // got a high score
            //
            var layout = HighScoresLayout;
            int x = layout.NameX, y = layout.RowY + (16 * n);
            if (layout.EntryBarWidth > 0)
            {
                _videoManager.Bar(x - 2, y - 2, layout.EntryBarWidth, 15, layout.EntryBackground);
                _videoManager.Update();
            }

            string str = Scores[n].name;
            US_LineInput(x, y, ref str, "", true, MaxHighName, layout.EntryWidth,
                new TextStyle(layout.Font, layout.EntryColor, layout.EntryBackground));
            Scores[n].name = str;
            _gameEngineManager.WriteConfig();
        }
        else
        {
            _inputManager.ClearKeysDown();
            _inputManager.UserInput(500);
        }
    }

    /// <summary>The "get psyched" progress bar, along the bottom of a box in screen (scaled) pixels</summary>
    internal static bool PreloadUpdate(uint current, uint total, int boxX, int boxY, int boxW, int boxH)
    {
        int scale = _videoManager.scaleFactor;
        int x = boxX + scale * 5;
        int y = boxY + boxH - scale * 3;
        uint w = (uint)(boxW - scale * 10);

        _videoManager.BarScaledCoord(x, y, (int)w, scale * 2, "Black");
        w = (uint)((int)w * current / total);
        if (w != 0)
        {
            _videoManager.BarScaledCoord(x, y, (int)w, scale * 2, "SECONDCOLOR");       //SECONDCOLOR 0x37);
            _videoManager.BarScaledCoord(x, y, (int)(w - scale * 1), scale * 1, "FIRSTCOLOR"); // 0x32

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

        _videoManager.BarScaledCoord(0, 0, _videoManager.screenWidth, _videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES - 1), bordercol);

        // TODO: This may have just been centered in the viewport area
        //    ((_videoManager.screenWidth - _videoManager.scaleFactor * 224) / 16) * 8,
        //    (_videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES + 48)) / 2,
        _graphicManager.DrawPic("getpsyched", (320 - 224) / 2, (200 - STATUSLINES - 48) / 2);

        int boxX = (_videoManager.screenWidth - _videoManager.scaleFactor * 224) / 2;
        int boxY = (_videoManager.screenHeight - _videoManager.scaleFactor * (STATUSLINES + 48)) / 2;

        _videoManager.Update();
        _videoManager.FadeIn();

        //      PM_Preload (PreloadUpdate);
        PreloadUpdate(10, 10, boxX, boxY, _videoManager.scaleFactor * 28 * 8, _videoManager.scaleFactor * 48);
        _inputManager.UserInput(70);
        _videoManager.FadeOut();

        DrawPlayBorder();
        _videoManager.Update();
    }

    internal static void LevelCompleted()
    {
        var language = _assetManager.GetText("en-us");
        const int VBLWAIT = 30;
        const int PAR_AMOUNT = 500;
        const int PERCENT100AMT = 10000;

        int x, i, min, sec, ratio, kr, sr, tr;
        string tempstr = "";
        int bonus, timeleft = 0;

        _videoManager.Bar(0, 0, 320, _videoManager.screenHeight / _videoManager.scaleFactor - STATUSLINES + 1, "VIEWCOLOR");

        if (bordercol != "VIEWCOLOR")
            DrawStatusBorder("VIEWCOLOR");

        StartCPMusic("ENDLEVEL");

        //
        // do the intermission
        //
        _inputManager.ClearKeysDown();
        _inputManager.StartAck();
        _graphicManager.DrawPic("l_guy", 0, 16);

        var gameInfo = _gameEngineManager.GetGameInfo();
        var mapInfo = gameInfo.Maps[gamestate.mapon];
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
            WriteTime(26 * 8, 10 * 8, min, sec);

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
                        _audioManager.Play("misc/end_bonus1");
                    _videoManager.Update();
                    while (_audioManager.IsAnySoundPlaying())
                        BJ_Breathe();
                    if (_inputManager.CheckAck())
                        goto done;
                }

                _videoManager.Update();

                _audioManager.Play("misc/end_bonus2");
                while (_audioManager.IsAnySoundPlaying())
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
                    _audioManager.Play("misc/end_bonus1");
                _videoManager.Update();
                while (_audioManager.IsAnySoundPlaying())
                    BJ_Breathe();

                if (_inputManager.CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                GameEngineManager.WaitVBL(VBLWAIT);
                _audioManager.StopAll();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                x = (RATIOXX - 1) - tempstr.Length * 2;
                Write(x, 7, tempstr);
                _videoManager.Update();
                _audioManager.Play("misc/100percent");
            }
            else if (ratio == 0)
            {
                GameEngineManager.WaitVBL(VBLWAIT);
                _audioManager.StopAll();
                _audioManager.Play("misc/no_bonus");
            }
            else
                _audioManager.Play("misc/end_bonus2");

            _videoManager.Update();
            while (_audioManager.IsAnySoundPlaying())
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
                    _audioManager.Play("misc/end_bonus1");
                _videoManager.Update();
                while (_audioManager.IsAnySoundPlaying())
                    BJ_Breathe();

                if (_inputManager.CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                GameEngineManager.WaitVBL(VBLWAIT);
                _audioManager.StopAll();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                x = (RATIOXX - 1) - tempstr.Length * 2;
                Write(x, 7, tempstr);
                _videoManager.Update();
                _audioManager.Play("misc/100percent");
            }
            else if (ratio == 0)
            {
                GameEngineManager.WaitVBL(VBLWAIT);
                _audioManager.StopAll();
                _audioManager.Play("misc/no_bonus");
            }
            else
                _audioManager.Play("misc/end_bonus2");
            _videoManager.Update();
            while (_audioManager.IsAnySoundPlaying())
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
                    _audioManager.Play("misc/end_bonus1");
                _videoManager.Update();
                while (_audioManager.IsAnySoundPlaying())
                    BJ_Breathe();
                if (_inputManager.CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                GameEngineManager.WaitVBL(VBLWAIT);
                _audioManager.StopAll();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                x = (RATIOXX - 1) - tempstr.Length * 2;
                Write(x, 7, tempstr);
                _videoManager.Update();
                _audioManager.Play("misc/100percent");
            }
            else if (ratio == 0)
            {
                GameEngineManager.WaitVBL(VBLWAIT);
                _audioManager.StopAll();
                _audioManager.Play("misc/no_bonus");
            }
            else
                _audioManager.Play("misc/end_bonus2");
            _videoManager.Update();
            while (_audioManager.IsAnySoundPlaying())
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
            // The latest run through a floor counts; the win tally averages the won cluster's floors
            LevelRatios[gamestate.mapon] =
                new LRstruct
            {
                kill = (short)kr,
                secret = (short)sr,
                treasure = (short)tr,
                time = min * 60 + sec
            };

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

    /// <summary>
    /// What the game-info shows when the current cluster is won; an empty one when it has no entry
    /// </summary>
    internal static ClusterInfo WonCluster()
        => _gameEngineManager.GetGameInfo().Clusters.GetValueOrDefault(gamestate.cluster) ?? new();

    /// <summary>
    /// The color a won cluster fades to (its victory-fade-color), or black
    /// </summary>
    internal static Entities.Color VictoryFadeColor(ClusterInfo cluster)
        => string.IsNullOrEmpty(cluster.VictoryFadeColor)
            ? new Entities.Color { Alpha = 255 }
            : Entities.Color.FromHexRGBA(cluster.VictoryFadeColor);

    internal static void Victory()
    {
        var language = _assetManager.GetText("en-us");
        int sec;
        int min, kr, sr, tr, x;
        string tempstr;
        const int RATIOX = 6;
        const int RATIOY = 14;
        const int TIMEX = 14;
        const int TIMEY = 8;

        var cluster = WonCluster();
        VictoryFrames(cluster);

        StartCPMusic("URAHERO");

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
        // Total time and average ratios over the floors of the won cluster that were completed.
        // id divided by a fixed floor count (8, or 20 in Spear), counting floors never played
        // (Spear's boss floors, a skipped secret floor) as 0%; this averages the ones played.
        var gameInfo = _gameEngineManager.GetGameInfo();
        var floors = LevelRatios
            .Where(lr => gameInfo.Maps.TryGetValue(lr.Key, out var map) && map.Cluster == gamestate.cluster)
            .Select(lr => lr.Value)
            .ToList();

        sec = floors.Sum(f => f.time);
        kr = sr = tr = 0;
        if (floors.Count > 0)
        {
            kr = floors.Sum(f => f.kill) / floors.Count;
            sr = floors.Sum(f => f.secret) / floors.Count;
            tr = floors.Sum(f => f.treasure) / floors.Count;
        }

        min = sec / 60;
        sec %= 60;

        if (min > 99)
            min = sec = 99;

        WriteTime(TIMEX * 8 + 1, TIMEY * 8, min, sec);
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
        //    fontnumber = "SmallFont";
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


        _videoManager.Update();
        _videoManager.FadeIn();

        _inputManager.Ack();

        _videoManager.FadeOut();
        if (_videoManager.screenHeight % 200 != 0)
            _videoManager.ClearScreen(0);

        FindMenuItem(MainMenu, "savegame")?.active = 0;

        EndText();
        EndScreens(cluster);
    }

    /// <summary>
    /// The cluster's victory-frames, each on the view color for its tics (Spear's BJ collapsing),
    /// ending in a quick fade to the victory-fade-color
    /// </summary>
    private static void VictoryFrames(ClusterInfo cluster)
    {
        if (cluster.VictoryFrames.Count == 0)
            return;

        if (!string.IsNullOrEmpty(cluster.VictoryMusic))
            StartCPMusic(cluster.VictoryMusic);

        for (int i = 0; i < cluster.VictoryFrames.Count; i++)
        {
            var frame = cluster.VictoryFrames[i];
            _videoManager.Bar(0, 0, 320, 200, "VIEWCOLOR");
            _graphicManager.DrawPic(frame.Pic, frame.X, frame.Y);
            _videoManager.Update();
            if (i == 0)
                _videoManager.FadeIn();
            GameEngineManager.WaitVBL((uint)frame.Tics);
        }

        _videoManager.FadeOut(VictoryFadeColor(cluster), 5);
    }

    /// <summary>
    /// The cluster's end-screens (Spear's EndSpear): each picture fades in with its palette,
    /// shows its captions in turn along the bottom (or waits for a key), then fades out
    /// </summary>
    private static void EndScreens(ClusterInfo cluster)
    {
        var language = _assetManager.GetText("en-us");

        foreach (var screen in cluster.EndScreens)
        {
            _graphicManager.DrawPic(screen.Pic, 0, 0);
            _videoManager.Update();

            var palette = string.IsNullOrEmpty(screen.Palette) ? null : _assetManager.Find<Palette>(screen.Palette);
            if (palette == null)
                _videoManager.FadeIn();
            else
                _videoManager.FadeIn(new GamePalette { Colors = palette.ToSDLColors() }, 30);

            if (screen.Captions.Count == 0)
            {
                _inputManager.ClearKeysDown();
                _inputManager.Ack();
            }

            var style = new TextStyle(SMALL_FONT, screen.CaptionColor, screen.CaptionBackground);
            foreach (var caption in screen.Captions)
            {
                _videoManager.Bar(0, screen.CaptionY, 320, 200 - screen.CaptionY, screen.CaptionBackground);
                CenteredText(0, 320, screen.CaptionY, style).CPrint(caption.ToLanguageText(language));
                _videoManager.Update();
                _inputManager.UserInput((uint)screen.CaptionTics);
            }

            _videoManager.FadeOut();
        }
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

    /// <summary>The intermission and victory screens' big letters (fonts.yaml)</summary>
    internal static readonly TextStyle IntermissionStyle = new("IntermissionFont", "White");

    /// <summary>Intermission text at (x, y) in 8 pixel tiles</summary>
    internal static void Write(int x, int y, string text) => WriteAt(x * 8, y * 8, text);

    /// <summary>Intermission text at (x, y) in pixels</summary>
    internal static void WriteAt(int x, int y, string text) => TextAt(x, y, IntermissionStyle).Print(text);

    /// <summary>Minutes and seconds as mm:ss, in the intermission font at (x, y) in pixels</summary>
    private static void WriteTime(int x, int y, int min, int sec) => WriteAt(x, y, $"{min:00}:{sec:00}");
}
