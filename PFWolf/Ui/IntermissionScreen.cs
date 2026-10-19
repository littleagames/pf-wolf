using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Extensions;
using PFWolf.Fonts;

namespace PFWolf.Ui;

/// <summary>What the level-end and victory screens need from the game</summary>
/// <param name="ViewColorBorder">Paints the status bar's border in the view color, if it isn't already</param>
/// <param name="GiveEveryone">Gives every player the level's bonus points (each machine gives each player theirs alike)</param>
/// <param name="DrawScore">Draws the score on the status bar</param>
/// <param name="DrawPlayBorder">Draws the border round the view again</param>
/// <param name="GameWon">After the victory tally: the menu's Save Game goes grey</param>
/// <param name="EndText">The won game's end text</param>
/// <param name="MapDisplayName">A map's name as the game shows it (PlayScreen.GetMapDisplayName)</param>
/// <param name="NewUnlocks">Whether anything's been unlocked since the last tally (Gameplay/Unlocks.cs TakeNews)</param>
/// <param name="Objective">A level objective's done and total (Gameplay/Objectives.cs), null for one the level hasn't</param>
internal sealed record IntermissionHooks(Action ViewColorBorder, Action<int> GiveEveryone, Action DrawScore, Action DrawPlayBorder,
    Action GameWon, Action EndText, Func<string, string> MapDisplayName, Func<bool>? NewUnlocks = null,
    Func<string, (int Done, int Total)?>? Objective = null);

/// <summary>
/// One of the level-end screen's tallies after the treasure ratio (intermission.yaml
/// objective.NAME, stealth): done of total, shown as a percentage or as done/total
/// </summary>
internal readonly record struct ExtraTally(string Name, int Done, int Total, bool AsCount)
{
    /// <summary>Where it counts up to: done, or its percentage (100 with nothing to do)</summary>
    internal int Target => AsCount ? Done : Total > 0 ? Done * 100 / Total : 100;

    internal string Text(int value) => AsCount ? $"{value}/{Total}" : value.ToString();

    internal bool Perfect => AsCount ? Done >= Total : Target >= 100;
}

/// <summary>
/// The level-end screen (intermission.yaml): the floor, time and par, the time bonus and the
/// kill, secret and treasure ratios counted up, with BJ breathing; and the won game's victory
/// tally, victory frames and end screens
/// </summary>
internal sealed class IntermissionScreen(GameEngineManager engine, AssetManager assets, InputManager input, AudioManager audio,
    VideoManager video, GraphicManager graphics, MenuDrawing draw, UiText ui, GameSession session, PlayArea playArea,
    IntermissionHooks hooks)
{
    int lastBreathTime = 0;

    /// <summary>
    /// Fills the screen in the view color from the top down to the bottom status bar's first
    /// line, for the level-end screens drawn over the play area
    /// </summary>
    private void ClearAboveStatusBar() =>
        video.BarScaledCoord(0, 0, video.ScreenWidth,
            video.ScreenYAboveBottom(playArea.StatusLines - 1), "VIEWCOLOR");

    private IntermissionAsset? intermission;
    private static readonly IntermissionAsset NoIntermission = new();

    /// <summary>The game pack's level-end screen settings (intermission.yaml); empty if it has none.</summary>
    internal IntermissionAsset Intermission =>
        intermission ?? (intermission = assets.FindInGamePackIfAny<IntermissionAsset>("intermission")) ?? NoIntermission;

    /// <summary>Plays one of the intermission's sounds; one it leaves out is silent.</summary>
    private void PlayIntermissionSound(string? name)
    {
        if (!string.IsNullOrEmpty(name))
            audio.Play(name);
    }

    internal void LevelCompleted()
    {
        var language = assets.GetText("en-us");
        // intermission.yaml sounds pause-ms
        uint PAUSE_MS = (uint)Math.Max(Intermission.Sounds.PauseMs ?? 0, 0);
        // intermission.yaml scoring
        int PAR_AMOUNT = Math.Max(Intermission.Scoring.TimeBonus ?? 0, 0);
        int PERCENT100AMT = Math.Max(Intermission.Scoring.PerfectBonus ?? 0, 0);

        int i, min, sec, ratio, kr, sr, tr;
        string tempstr = "";
        int bonus, timeleft = 0;

        ClearAboveStatusBar();
        valueAreas.Clear();

        hooks.ViewColorBorder();

        // Laid out in the screen above the status bar, however tall
        using var origin = video.UseUiOriginAboveBottom(playArea.StatusLines);

        if (!string.IsNullOrEmpty(Intermission.Music))
            audio.PlayMusic(Intermission.Music);

        //
        // do the intermission
        //
        input.ClearKeysDown();
        input.StartAck();
        bjWhich = 0;
        DrawBJ();

        var gameInfo = engine.GetGameInfo();
        var mapInfo = gameInfo.Maps[session.GameState.MapOn];
        var extras = ExtraTallies();
        //if (session.GameState.MapOn < LRpack)
        {
            foreach (var label in Intermission.Labels ?? [])
                if (label.OnlyWith is not { Length: > 0 } with || extras.Any(e => e.Name.Equals(with, StringComparison.OrdinalIgnoreCase)))
                    ui.TextAt(label.X, label.Y, IntermissionTextStyle).Print(label.Text.ToLanguageText(language));
            WriteValue("bonus", "0");
            WriteValue("floor", mapInfo.FloorNumber.ToString());
            WriteValue("name", hooks.MapDisplayName(session.GameState.MapOn));
            if (!string.IsNullOrWhiteSpace(mapInfo.Author))     // a map pack's credit; none, nothing drawn
                WriteValue("author", mapInfo.Author.ToLanguageText(language));
            WriteValue("par", int.SecondsAsTime(mapInfo.ParTime));
            //
            // PRINT TIME
            //
            sec = session.GameState.TimeCount / 70;

            if (sec > 99 * 60)      // 99 minutes max
                sec = 99 * 60;
            if (session.GameState.TimeCount < mapInfo.ParTime * 70)
                timeleft = mapInfo.ParTime - sec;

            min = sec / 60;
            sec %= 60;
            WriteValue("time", $"{min:00}:{sec:00}");

            video.Update();
            video.FadeIn();


            //
            // FIGURE RATIOS OUT BEFOREHAND
            //
            (kr, sr, tr) = Ratios(session.GameState);


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
                    video.Update();
                    while (audio.IsAnySoundPlaying())
                        BjBreathe();
                    if (input.CheckAck())
                        goto done;
                }

                video.Update();

                PlayIntermissionSound(Intermission.Sounds.TallyDone);
                while (audio.IsAnySoundPlaying())
                    BjBreathe();
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
                video.Update();
                while (audio.IsAnySoundPlaying())
                    BjBreathe();

                if (input.CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                GameEngineManager.DelayMs(PAUSE_MS);
                audio.StopAll();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                WriteValue("bonus", tempstr);
                video.Update();
                PlayIntermissionSound(Intermission.Sounds.Perfect);
            }
            else if (ratio == 0)
            {
                GameEngineManager.DelayMs(PAUSE_MS);
                audio.StopAll();
                PlayIntermissionSound(Intermission.Sounds.None);
            }
            else
                PlayIntermissionSound(Intermission.Sounds.TallyDone);

            video.Update();
            while (audio.IsAnySoundPlaying())
                BjBreathe();

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
                video.Update();
                while (audio.IsAnySoundPlaying())
                    BjBreathe();

                if (input.CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                GameEngineManager.DelayMs(PAUSE_MS);
                audio.StopAll();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                WriteValue("bonus", tempstr);
                video.Update();
                PlayIntermissionSound(Intermission.Sounds.Perfect);
            }
            else if (ratio == 0)
            {
                GameEngineManager.DelayMs(PAUSE_MS);
                audio.StopAll();
                PlayIntermissionSound(Intermission.Sounds.None);
            }
            else
                PlayIntermissionSound(Intermission.Sounds.TallyDone);
            video.Update();
            while (audio.IsAnySoundPlaying())
                BjBreathe();

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
                video.Update();
                while (audio.IsAnySoundPlaying())
                    BjBreathe();
                if (input.CheckAck())
                    goto done;
            }
            if (ratio >= 100)
            {
                GameEngineManager.DelayMs(PAUSE_MS);
                audio.StopAll();
                bonus += PERCENT100AMT;
                tempstr = bonus.ToString();
                WriteValue("bonus", tempstr);
                video.Update();
                PlayIntermissionSound(Intermission.Sounds.Perfect);
            }
            else if (ratio == 0)
            {
                GameEngineManager.DelayMs(PAUSE_MS);
                audio.StopAll();
                PlayIntermissionSound(Intermission.Sounds.None);
            }
            else
                PlayIntermissionSound(Intermission.Sounds.TallyDone);
            video.Update();
            while (audio.IsAnySoundPlaying())
                BjBreathe();

            //
            // OBJECTIVES AND STEALTH (Midnight Assault's intel, stealth kills and Kilroys)
            //
            foreach (var extra in extras)
                if (!CountUp(extra, PAUSE_MS))
                    goto done;


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

            foreach (var extra in extras)
                WriteValue(extra.Name, extra.Text(extra.Target));

            bonus = LevelBonus(timeleft, PAR_AMOUNT, PERCENT100AMT, kr, sr, tr);

            // Everyone's bonus, with others: each machine gives each player theirs alike
            hooks.GiveEveryone(bonus);
            tempstr = bonus.ToString();
            WriteValue("bonus", tempstr);

            // Anything unlocked this level (intermission.yaml `unlocked`, its text the format:
            // Midnight Assault's "New Levels Unlocked!")
            if (hooks.NewUnlocks?.Invoke() == true)
                WriteValue("unlocked", "");

            //
            // SAVE RATIO INFORMATION FOR ENDGAME
            //
            // The latest run through a floor counts; the win tally averages the won cluster's floors
            session.LevelRatios[session.GameState.MapOn] =
                new LevelRatio
            {
                Kill = (short)kr,
                Secret = (short)sr,
                Treasure = (short)tr,
                Time = min * 60 + sec
            };

            // TODO This should be set up as different LevelCompleted "screens"???
        }
        //else
        //{
        //    Write(14, 4, "secret floor\n completed!");
        //    Write(10, 16, "15000 bonus!");

        //    video.Update();
        //    video.FadeIn();

        //    GivePoints(15000);
        //}


        hooks.DrawScore();
        video.Update();

        lastBreathTime = (int)GameEngineManager.GetTimeCount();
        input.StartAck();
        while (!input.CheckAck())
            BjBreathe();

        //
        // done
        //

        // A level whose end-message ends the game (the Spear demo's last floor) shows it here
        if (!string.IsNullOrEmpty(mapInfo.EndMessage))
        {
            audio.Play("misc/1up");
            draw.Message(mapInfo.EndMessage.ToLanguageText(language));
            input.ClearKeysDown();
            input.Ack();
        }

        video.FadeOut();
        hooks.DrawPlayBorder();
    }

    /// <summary>
    /// What the game-info shows when the current cluster is won; an empty one when it has no entry
    /// </summary>
    internal ClusterInfo WonCluster()
        => engine.GetGameInfo().Clusters.GetValueOrDefault(session.GameState.Cluster) ?? new();

    /// <summary>
    /// The color a won cluster fades to (its victory-fade-color), or black
    /// </summary>
    internal static Entities.Color VictoryFadeColor(ClusterInfo cluster)
        => string.IsNullOrEmpty(cluster.VictoryFadeColor)
            ? new Entities.Color { Alpha = 255 }
            : Entities.Color.FromHexRGBA(cluster.VictoryFadeColor);

    /// <summary>
    /// The tallies after the treasure ratio that show for this level, in the order intermission.yaml
    /// lists them: each objective.NAME (a count of nothing isn't shown), and stealth when the level
    /// was finished unseen with stealth kills, or with none killed of some there (Midnight
    /// Assault's showStealthStats)
    /// </summary>
    internal List<ExtraTally> ExtraTallies() =>
        ExtraTallies(Intermission.Values, hooks.Objective, session.PlayerState, session.GameState);

    internal static List<ExtraTally> ExtraTallies(Dictionary<string, IntermissionValue> values,
        Func<string, (int Done, int Total)?>? objective, Entities.PlayerState player, GameState game)
    {
        var tallies = new List<ExtraTally>();
        foreach (var (name, value) in values)
        {
            if (name.StartsWith("objective.", StringComparison.OrdinalIgnoreCase))
            {
                var counts = objective?.Invoke(name["objective.".Length..]);
                if (value.Count && counts is not { Total: > 0 })
                    continue;
                tallies.Add(new ExtraTally(name, counts?.Done ?? 0, counts?.Total ?? 0, value.Count));
            }
            else if (name.Equals("stealth", StringComparison.OrdinalIgnoreCase)
                && player.Undetected && (player.StealthKills > 0 || (game.KillCount == 0 && game.KillTotal > 0)))
                tallies.Add(new ExtraTally(name, player.StealthKills, game.KillTotal, value.Count));
        }
        return tallies;
    }

    // Counts a tally up in place with the ratios' sounds, as the kill ratio does; false if a key cut it short
    bool CountUp(ExtraTally tally, uint pauseMs)
    {
        for (int i = 0; i <= tally.Target; i++)
        {
            WriteValue(tally.Name, tally.Text(i));
            if ((i % 10) == 0)
                PlayIntermissionSound(Intermission.Sounds.Tally);
            video.Update();
            while (audio.IsAnySoundPlaying())
                BjBreathe();
            if (input.CheckAck())
                return false;
        }
        if (tally.Perfect || tally.Target == 0)
        {
            GameEngineManager.DelayMs(pauseMs);
            audio.StopAll();
            PlayIntermissionSound(tally.Perfect ? Intermission.Sounds.Perfect : Intermission.Sounds.None);
        }
        else
            PlayIntermissionSound(Intermission.Sounds.TallyDone);
        video.Update();
        while (audio.IsAnySoundPlaying())
            BjBreathe();
        return true;
    }

    /// <summary>A level's kill, secret and treasure ratios in percent; one with none of something is 0</summary>
    internal static (int Kill, int Secret, int Treasure) Ratios(GameState gamestate)
    {
        int kr = 0, sr = 0, tr = 0;
        if (gamestate.KillTotal != 0)
            kr = (gamestate.KillCount * 100) / gamestate.KillTotal;
        if (gamestate.SecretTotal != 0)
            sr = (gamestate.SecretCount * 100) / gamestate.SecretTotal;
        if (gamestate.TreasureTotal != 0)
            tr = (gamestate.TreasureCount * 100) / gamestate.TreasureTotal;
        return (kr, sr, tr);
    }

    /// <summary>The level's bonus: the time left under par at so much a second, and so much for each ratio at 100%</summary>
    internal static int LevelBonus(int timeleft, int parAmount, int perfectAmount, int kr, int sr, int tr)
        => timeleft * parAmount +
            (perfectAmount * ((kr >= 100) ? 1 : 0)) +
            (perfectAmount * ((sr >= 100) ? 1 : 0)) +
            (perfectAmount * ((tr >= 100) ? 1 : 0));

    /// <summary>
    /// Total time (minutes and seconds, 99:99 at most) and average ratios over the floors of the
    /// won cluster that were completed. id divided by a fixed floor count (8, or 20 in Spear),
    /// counting floors never played (Spear's boss floors, a skipped secret floor) as 0%; this
    /// averages the ones played.
    /// </summary>
    internal static (int Min, int Sec, int Kill, int Secret, int Treasure) ClusterTotals(
        Dictionary<string, LevelRatio> levelRatios, Dictionary<string, MapInfo> maps, int cluster)
    {
        var floors = levelRatios
            .Where(lr => maps.TryGetValue(lr.Key, out var map) && map.Cluster == cluster)
            .Select(lr => lr.Value)
            .ToList();

        int sec = floors.Sum(f => f.Time);
        int kr = 0, sr = 0, tr = 0;
        if (floors.Count > 0)
        {
            kr = floors.Sum(f => f.Kill) / floors.Count;
            sr = floors.Sum(f => f.Secret) / floors.Count;
            tr = floors.Sum(f => f.Treasure) / floors.Count;
        }

        int min = sec / 60;
        sec %= 60;

        if (min > 99)
            min = sec = 99;
        return (min, sec, kr, sr, tr);
    }

    internal void Victory()
    {
        var language = assets.GetText("en-us");
        int sec;
        int min, kr, sr, tr;
        var screen = Intermission.Victory ?? new VictoryScreen();

        var cluster = WonCluster();
        VictoryFrames(cluster);

        if (!string.IsNullOrEmpty(screen.Music))
            audio.PlayMusic(screen.Music);

        ClearAboveStatusBar();
        valueAreas.Clear();
        hooks.ViewColorBorder();

        // Laid out in the screen above the status bar, until the end text and screens take over
        var origin = video.UseUiOriginAboveBottom(playArea.StatusLines);

        // intermission.yaml victory
        foreach (var label in screen.Labels.Where(label => string.IsNullOrEmpty(label.OnlyWith)))     // no level tallies here
            ui.TextAt(label.X, label.Y, IntermissionTextStyle).Print(label.Text.ToLanguageText(language));
        foreach (var pic in screen.Pics)
            graphics.DrawPic(pic.Pic, pic.X, pic.Y);
        (min, sec, kr, sr, tr) = ClusterTotals(session.LevelRatios, engine.GetGameInfo().Maps, session.GameState.Cluster);

        WriteValue(screen.Values, "time", $"{min:00}:{sec:00}");
        video.Update();

        WriteValue(screen.Values, "kill", kr.ToString());
        WriteValue(screen.Values, "secret", sr.ToString());
        WriteValue(screen.Values, "treasure", tr.ToString());
        if (hooks.NewUnlocks?.Invoke() == true)
            WriteValue(screen.Values, "unlocked", "");

        //
        // TOTAL TIME VERIFICATION CODE
        //
        //if (session.GameState.difficulty >= 2)   // medium or harder
        //{
        //    graphics.DrawPic(30 * 8, TIMEY * 8, graphicnums.C_TIMECODEPIC);
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


        video.Update();
        video.FadeIn();

        input.Ack();
        origin.Dispose();

        video.FadeOut();
        if (video.HasMargins)
            video.ClearScreen(0);

        hooks.GameWon();

        hooks.EndText();
        EndScreens(cluster);
    }

    /// <summary>
    /// The cluster's victory-frames, each on the view color for its tics (Spear's BJ collapsing),
    /// ending in a quick fade to the victory-fade-color
    /// </summary>
    private void VictoryFrames(ClusterInfo cluster)
    {
        if (cluster.VictoryFrames.Count == 0)
            return;

        if (!string.IsNullOrEmpty(cluster.VictoryMusic))
            audio.PlayMusic(cluster.VictoryMusic);

        for (int i = 0; i < cluster.VictoryFrames.Count; i++)
        {
            var frame = cluster.VictoryFrames[i];
            video.FillScreen("VIEWCOLOR");
            graphics.DrawPic(frame.Pic, frame.X, frame.Y);
            video.Update();
            if (i == 0)
                video.FadeIn();
            GameEngineManager.WaitVBL((uint)frame.Tics);
        }

        video.FadeOut(VictoryFadeColor(cluster), 5);
    }

    /// <summary>
    /// The cluster's end-screens (Spear's EndSpear): each picture fades in with its palette,
    /// shows its captions in turn along the bottom (or waits for a key), then fades out
    /// </summary>
    private void EndScreens(ClusterInfo cluster)
    {
        var language = assets.GetText("en-us");

        foreach (var screen in cluster.EndScreens)
        {
            graphics.DrawPic(screen.Pic, 0, 0);
            video.Update();

            var palette = string.IsNullOrEmpty(screen.Palette) ? null : assets.Find<Palette>(screen.Palette);
            if (palette == null)
                video.FadeIn();
            else
                video.FadeIn(new GamePalette { Colors = palette.ToSDLColors() }, 30);

            if (screen.Captions.Count == 0)
            {
                input.ClearKeysDown();
                input.Ack();
            }

            var style = new TextStyle(UiFonts.SmallFont, screen.CaptionColor, screen.CaptionBackground);
            foreach (var caption in screen.Captions)
            {
                video.Bar(0, screen.CaptionY, 320, 200 - screen.CaptionY, screen.CaptionBackground);
                ui.CenteredText(0, 320, screen.CaptionY, style).CPrint(caption.ToLanguageText(language));
                video.Update();
                input.UserInput((uint)screen.CaptionTics);
            }

            video.FadeOut();
        }
    }

    //
    // Breathe Mr. BJ!!!
    //
    // The first breath comes sooner, once per run, as in the original
    int bjWhich = 0, bjMax = 10;
    internal void BjBreathe()
    {
        GameEngineManager.DelayMs(5);

        if (Intermission.Bj is not { Pics.Count: > 1 } bj)
            return;

        if ((int)GameEngineManager.GetTimeCount() - lastBreathTime > bjMax)
        {
            bjWhich = (bjWhich + 1) % bj.Pics.Count;
            DrawBJ();
            video.Update();
            lastBreathTime = (int)GameEngineManager.GetTimeCount();
            bjMax = bj.BreathTics;
        }
    }

    /// <summary>BJ on the level-end screen (intermission.yaml bj), in his current breath</summary>
    private void DrawBJ()
    {
        if (Intermission.Bj is { Pics.Count: > 0 } bj)
            graphics.DrawPic(bj.Pics[bjWhich % bj.Pics.Count], bj.X, bj.Y);
    }

    /// <summary>The level-end screen's text style: intermission.yaml's font and color, else the intermission font's</summary>
    internal TextStyle IntermissionTextStyle =>
        new(Intermission.Font ?? IntermissionStyle.Font, Intermission.Color ?? IntermissionStyle.Color);

    /// <summary>
    /// A number (or time) on the level-end screen, where intermission.yaml's values put it: from
    /// its x, or with its right edge at its right. Left out of the layout, it isn't shown.
    /// </summary>
    private void WriteValue(string name, string text) => WriteValue(Intermission.Values, name, text);

    private void WriteValue(Dictionary<string, IntermissionValue> values, string name, string text)
    {
        if (!values.TryGetValue(name, out var value))
            return;
        var style = IntermissionTextStyle;
        text = FormatValue(value, text, format => format.ToLanguageText(assets.GetText("en-us")));
        ui.MeasureText(text, style.Font, out int width, out int height);
        int x = ValueX(value, width);

        // intermission.yaml value-background: the number drawn before goes first
        if (Intermission.ValueBackground is { Length: > 0 } background)
        {
            if (valueAreas.TryGetValue((values, name), out var last))
                video.Bar(last.X, last.Y, last.Width, last.Height, background);
            video.Bar(x, value.Y, width, height, background);
            valueAreas[(values, name)] = (x, value.Y, width, height);
        }
        ui.TextAt(x, value.Y, style).Print(text);
    }

    /// <summary>A value put in its format (a language key or text, {0} for the value); as it is with none</summary>
    internal static string FormatValue(IntermissionValue value, string text, Func<string, string> localize)
    {
        if (string.IsNullOrEmpty(value.Format))
            return text;
        var format = localize(value.Format);
        return format.Contains("{0}") ? format.Replace("{0}", text) : format + text;
    }

    /// <summary>Where a value that wide starts across: from x, back from right, or either side of center</summary>
    internal static int ValueX(IntermissionValue value, int width) =>
        value.Right is { } right ? right - width
        : value.Center is { } center ? center - width / 2
        : value.X;

    // Where each number was last drawn, for value-background to clear (by layout and name)
    private readonly Dictionary<(Dictionary<string, IntermissionValue>, string), (int X, int Y, int Width, int Height)> valueAreas = [];

    /// <summary>The intermission, victory and death cam text when intermission.yaml doesn't set a font or color</summary>
    internal static readonly TextStyle IntermissionStyle = new("IntermissionFont", "White");
}