using SDL2;
using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Extensions;
using PFWolf.Fonts;
using PFWolf.Managers;

namespace PFWolf;

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
        _videoManager.FillScreen("Light Blue");     // background

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
        var shadow = layout.Shadow is { } shadowColor ? new FontShadow(1, 1, shadowColor) : null;
        var style = new TextStyle(layout.Font, layout.Color, "BORDCOLOR", Shadow: shadow);

        // A pack's frame (a menudef's components), else Wolf3D's stripes
        if (!string.IsNullOrEmpty(layout.Frame))
            DrawMenuComponents(layout.Frame);
        else
        {
            ClearMScreen();
            DrawStripes(10);
        }

        if (!string.IsNullOrEmpty(layout.Pic))      // pic: "" for none (labels can head the screen)
            _graphicManager.DrawPic(layout.Pic, layout.PicX, layout.PicY);
        foreach (var header in layout.Headers)
            _graphicManager.DrawPic(header.Pic, header.X, header.Y);
        var language = _assetManager.GetText("en-us");
        foreach (var label in layout.Labels)
            TextAt(label.X, label.Y, style with { Font = label.Font ?? style.Font, Color = label.Color ?? style.Color })
                .Print(label.Text.ToLanguageText(language));

        for (int i = 0; i < MaxScores; i++)
        {
            HighScore s = Scores[i];
            int y = layout.RowY + (layout.RowHeight * i);

            //
            // name
            //
            TextAt(layout.NameX, y, style).Print(s.name);

            //
            // level
            //
            var buffer = (s.completed & ~HighScoreWon).ToString();
            int x = layout.LevelRight - TextWidth(buffer, style.Font);
            if (!layout.ShowLevel)
            { }
            else if ((s.completed & HighScoreWon) != 0 && !string.IsNullOrEmpty(layout.WonPic))
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

            //
            // ratio
            //
            if (layout.RatioRight > 0)
            {
                buffer = s.ratio.ToString();
                TextAt(layout.RatioRight - TextWidth(buffer, style.Font), y, style).Print(buffer);
            }
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
        myscore.ratio = (ushort)Math.Clamp(FloorScore() * 100 / 300, 0, 100);    // the last floor's rating

        for (i = 0, n = -1; i < MaxScores; i++)
        {
            if ((myscore.score > Scores[i].score)
                || ((myscore.score == Scores[i].score) && (myscore.completed > Scores[i].completed)))
            {
                for (j = (ushort)MaxScores; --j > i;)
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
            int x = layout.NameX, y = layout.RowY + (layout.RowHeight * n);
            if (layout.EntryBarWidth > 0)
            {
                _videoManager.Bar(x - 2, y - 2, layout.EntryBarWidth, Math.Min(15, layout.RowHeight), layout.EntryBackground);
                _videoManager.Update();
            }
            else
            {
                // The name typed over a blank row
                _videoManager.Bar(x, y, Math.Max(layout.EntryWidth, 1), layout.RowHeight, layout.EntryBackground);
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

    /// <summary>
    /// The "get psyched" progress bar, along the bottom of a box drawn at the UI scale from a
    /// screen position; its width and height are in layout pixels
    /// </summary>
    internal static bool PreloadUpdate(uint current, uint total, int boxX, int boxY, int boxW, int boxH)
    {
        // Edges at the UI scale from the box's corner, as the picture's are
        int L(int n) => _videoManager.ToScreenLength(n);
        int x = boxX + L(5);
        int y = boxY + L(boxH - 3);
        int h = L(boxH - 1) - L(boxH - 3), line = L(boxH - 2) - L(boxH - 3);
        uint w = (uint)(L(boxW - 5) - L(5));

        _videoManager.BarScaledCoord(x, y, (int)w, h, "Black");
        w = (uint)((int)w * current / total);
        if (w != 0)
        {
            _videoManager.BarScaledCoord(x, y, (int)w, h, "SECONDCOLOR");       //SECONDCOLOR 0x37);
            _videoManager.BarScaledCoord(x, y, (int)w - L(1), line, "FIRSTCOLOR"); // 0x32

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

    /// <summary>
    /// Fills the screen in the view color from the top down to the bottom status bar's first
    /// line, for the level-end screens drawn over the play area
    /// </summary>
    static void ClearAboveStatusBar() =>
        _videoManager.BarScaledCoord(0, 0, _videoManager.screenWidth,
            _videoManager.ScreenYAboveBottom(STATUSLINES - 1), "VIEWCOLOR");

    internal static void PreloadGraphics()
    {
        // A game with a level start message (Blake Stone) shows it instead of "get psyched"
        if (_gameEngineManager.GetGameInfo().LevelStartMessage is { Length: > 0 } message)
        {
            ShowLevelStartMessage(message);
            return;
        }

        DrawLevel();

        // the play area, below any top status bar, down to the bottom one's first line
        _videoManager.BarScaledCoord(0, PlayAreaTop, _videoManager.screenWidth, PlayAreaAndStatusLine, bordercol);

        // Centered over the screen above the status bar
        int boxX = (_videoManager.screenWidth - _videoManager.ToScreenLength(224)) / 2;
        int boxY = (_videoManager.ScreenYAboveBottom(STATUSLINES) - _videoManager.ToScreenLength(48)) / 2;
        if (_assetManager.Exists<GraphicAsset>("getpsyched"))     // a standalone game may have none
            _graphicManager.DrawPicScaledCoord("getpsyched", boxX, boxY);

        _videoManager.Update();
        _videoManager.FadeIn();

        //      PM_Preload (PreloadUpdate);
        PreloadUpdate(10, 10, boxX, boxY, 28 * 8, 48);
        _inputManager.UserInput(70);
        _videoManager.FadeOut();

        DrawPlayBorder();
        _videoManager.Update();
    }

    private static IntermissionAsset? intermission;
    private static readonly IntermissionAsset NoIntermission = new();

    /// <summary>The game pack's level-end screen settings (intermission.yaml); empty if it has none.</summary>
    static IntermissionAsset Intermission =>
        intermission ?? (intermission = _assetManager.FindInGamePackIfAny<IntermissionAsset>("intermission")) ?? NoIntermission;

    /// <summary>Plays one of the intermission's sounds; one it leaves out is silent.</summary>
    static void PlayIntermissionSound(string? name)
    {
        if (!string.IsNullOrEmpty(name))
            _audioManager.Play(name);
    }

    internal static void LevelCompleted()
    {
        var language = _assetManager.GetText("en-us");
        // intermission.yaml sounds pause-ms
        uint PAUSE_MS = (uint)Math.Max(Intermission.Sounds.PauseMs ?? 0, 0);
        // intermission.yaml scoring
        int PAR_AMOUNT = Math.Max(Intermission.Scoring.TimeBonus ?? 0, 0);
        int PERCENT100AMT = Math.Max(Intermission.Scoring.PerfectBonus ?? 0, 0);

        int i, min, sec, ratio, kr, sr, tr;
        string tempstr = "";
        int bonus, timeleft = 0;

        ClearAboveStatusBar();

        if (bordercol != "VIEWCOLOR")
            DrawStatusBorder("VIEWCOLOR");

        // Laid out in the screen above the status bar, however tall
        using var origin = _videoManager.UseUiOriginAboveBottom(STATUSLINES);

        if (!string.IsNullOrEmpty(Intermission.Music))
            StartCPMusic(Intermission.Music);

        //
        // do the intermission
        //
        _inputManager.ClearKeysDown();
        _inputManager.StartAck();
        bj_which = 0;
        DrawBJ();

        var gameInfo = _gameEngineManager.GetGameInfo();
        var mapInfo = gameInfo.Maps[gamestate.mapon];
        //if (gamestate.mapon < LRpack)
        {
            foreach (var label in Intermission.Labels ?? [])
                TextAt(label.X, label.Y, IntermissionTextStyle).Print(label.Text.ToLanguageText(language));
            WriteValue("bonus", "0");
            WriteValue("floor", mapInfo.FloorNumber.ToString());
            WriteValue("par", int.SecondsAsTime(mapInfo.ParTime));
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
            WriteValue("time", $"{min:00}:{sec:00}");

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
                    WriteValue("bonus", tempstr);
                    if ((i % Math.Max(PAR_AMOUNT / 10, 1)) == 0)
                        PlayIntermissionSound(Intermission.Sounds.Tally);
                    _videoManager.Update();
                    while (_audioManager.IsAnySoundPlaying())
                        BJ_Breathe();
                    if (_inputManager.CheckAck())
                        goto done;
                }

                _videoManager.Update();

                PlayIntermissionSound(Intermission.Sounds.TallyDone);
                while (_audioManager.IsAnySoundPlaying())
                    BJ_Breathe();
            }

            //
            // KILL RATIO
            //
            ratio = kr;
            for (i = 0; i <= ratio; i++)
            {
                tempstr = i.ToString();
                WriteValue("kill", tempstr);
                if ((i % 10) == 0)
                    PlayIntermissionSound(Intermission.Sounds.Tally);
                _videoManager.Update();
                while (_audioManager.IsAnySoundPlaying())
                    BJ_Breathe();

                if (_inputManager.CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                GameEngineManager.DelayMs(PAUSE_MS);
                _audioManager.StopAll();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                WriteValue("bonus", tempstr);
                _videoManager.Update();
                PlayIntermissionSound(Intermission.Sounds.Perfect);
            }
            else if (ratio == 0)
            {
                GameEngineManager.DelayMs(PAUSE_MS);
                _audioManager.StopAll();
                PlayIntermissionSound(Intermission.Sounds.None);
            }
            else
                PlayIntermissionSound(Intermission.Sounds.TallyDone);

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
                WriteValue("secret", tempstr);
                if ((i % 10) == 0)
                    PlayIntermissionSound(Intermission.Sounds.Tally);
                _videoManager.Update();
                while (_audioManager.IsAnySoundPlaying())
                    BJ_Breathe();

                if (_inputManager.CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                GameEngineManager.DelayMs(PAUSE_MS);
                _audioManager.StopAll();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                WriteValue("bonus", tempstr);
                _videoManager.Update();
                PlayIntermissionSound(Intermission.Sounds.Perfect);
            }
            else if (ratio == 0)
            {
                GameEngineManager.DelayMs(PAUSE_MS);
                _audioManager.StopAll();
                PlayIntermissionSound(Intermission.Sounds.None);
            }
            else
                PlayIntermissionSound(Intermission.Sounds.TallyDone);
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
                WriteValue("treasure", tempstr);
                if ((i % 10) == 0)
                    PlayIntermissionSound(Intermission.Sounds.Tally);
                _videoManager.Update();
                while (_audioManager.IsAnySoundPlaying())
                    BJ_Breathe();
                if (_inputManager.CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                GameEngineManager.DelayMs(PAUSE_MS);
                _audioManager.StopAll();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                WriteValue("bonus", tempstr);
                _videoManager.Update();
                PlayIntermissionSound(Intermission.Sounds.Perfect);
            }
            else if (ratio == 0)
            {
                GameEngineManager.DelayMs(PAUSE_MS);
                _audioManager.StopAll();
                PlayIntermissionSound(Intermission.Sounds.None);
            }
            else
                PlayIntermissionSound(Intermission.Sounds.TallyDone);
            _videoManager.Update();
            while (_audioManager.IsAnySoundPlaying())
                BJ_Breathe();


            //
            // JUMP STRAIGHT HERE IF KEY PRESSED
            //
        done:
            tempstr = kr.ToString();
            WriteValue("kill", tempstr);

            tempstr = sr.ToString();
            WriteValue("secret", tempstr);

            tempstr = tr.ToString();
            WriteValue("treasure", tempstr);

            bonus = (int)timeleft * PAR_AMOUNT +
                (PERCENT100AMT * ((kr >= 100) ? 1 : 0)) +
                (PERCENT100AMT * ((sr >= 100) ? 1 : 0)) +
                (PERCENT100AMT * ((tr >= 100) ? 1 : 0));

            // Everyone's bonus, with others: each machine gives each player theirs alike
            foreach (var p in players)
            {
                using var _ = ActAs(p);
                GivePoints(bonus);
            }
            tempstr = bonus.ToString();
            WriteValue("bonus", tempstr);

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

        // A level whose end-message ends the game (the Spear demo's last floor) shows it here
        if (!string.IsNullOrEmpty(mapInfo.EndMessage))
        {
            _audioManager.Play("misc/1up");
            Message(mapInfo.EndMessage.ToLanguageText(language));
            _inputManager.ClearKeysDown();
            _inputManager.Ack();
        }

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
        int min, kr, sr, tr;
        var screen = Intermission.Victory ?? new VictoryScreen();

        var cluster = WonCluster();
        VictoryFrames(cluster);

        if (!string.IsNullOrEmpty(screen.Music))
            StartCPMusic(screen.Music);

        ClearAboveStatusBar();
        if (bordercol != "VIEWCOLOR")
            DrawStatusBorder("VIEWCOLOR");

        // Laid out in the screen above the status bar, until the end text and screens take over
        var origin = _videoManager.UseUiOriginAboveBottom(STATUSLINES);

        // intermission.yaml victory
        foreach (var label in screen.Labels)
            TextAt(label.X, label.Y, IntermissionTextStyle).Print(label.Text.ToLanguageText(language));
        foreach (var pic in screen.Pics)
            _graphicManager.DrawPic(pic.Pic, pic.X, pic.Y);
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

        WriteValue(screen.Values, "time", $"{min:00}:{sec:00}");
        _videoManager.Update();

        WriteValue(screen.Values, "kill", kr.ToString());
        WriteValue(screen.Values, "secret", sr.ToString());
        WriteValue(screen.Values, "treasure", tr.ToString());

        //
        // TOTAL TIME VERIFICATION CODE
        //
        //if (gamestate.difficulty >= 2)   // medium or harder
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
        origin.Dispose();

        _videoManager.FadeOut();
        if (_videoManager.HasMargins)
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
            _videoManager.FillScreen("VIEWCOLOR");
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
    // The first breath comes sooner, once per run, as in the original
    private static int bj_which = 0, bj_max = 10;
    internal static void BJ_Breathe()
    {
        GameEngineManager.DelayMs(5);

        if (Intermission.Bj is not { Pics.Count: > 1 } bj)
            return;

        if ((int)GameEngineManager.GetTimeCount() - lastBreathTime > bj_max)
        {
            bj_which = (bj_which + 1) % bj.Pics.Count;
            DrawBJ();
            _videoManager.Update();
            lastBreathTime = (int)GameEngineManager.GetTimeCount();
            bj_max = bj.BreathTics;
        }
    }

    /// <summary>BJ on the level-end screen (intermission.yaml bj), in his current breath</summary>
    static void DrawBJ()
    {
        if (Intermission.Bj is { Pics.Count: > 0 } bj)
            _graphicManager.DrawPic(bj.Pics[bj_which % bj.Pics.Count], bj.X, bj.Y);
    }

    /// <summary>The level-end screen's text style: intermission.yaml's font and color, else the intermission font's</summary>
    static TextStyle IntermissionTextStyle =>
        new(Intermission.Font ?? IntermissionStyle.Font, Intermission.Color ?? IntermissionStyle.Color);

    /// <summary>
    /// A number (or time) on the level-end screen, where intermission.yaml's values put it: from
    /// its x, or with its right edge at its right. Left out of the layout, it isn't shown.
    /// </summary>
    static void WriteValue(string name, string text) => WriteValue(Intermission.Values, name, text);

    static void WriteValue(Dictionary<string, IntermissionValue> values, string name, string text)
    {
        if (!values.TryGetValue(name, out var value))
            return;
        var style = IntermissionTextStyle;
        int x = value.Right is { } right ? right - TextWidth(text, style.Font) : value.X;
        TextAt(x, value.Y, style).Print(text);
    }

    /// <summary>The intermission, victory and death cam text when intermission.yaml doesn't set a font or color</summary>
    internal static readonly TextStyle IntermissionStyle = new("IntermissionFont", "White");
}
