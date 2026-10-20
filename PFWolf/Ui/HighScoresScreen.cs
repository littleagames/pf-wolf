using PFWolf.Assets;
using PFWolf.Extensions;
using PFWolf.Fonts;

namespace PFWolf.Ui;

/// <summary>
/// The high scores: drawn (game-info high-scores lays them out), and a new one ranked and named;
/// or each episode's best in their place (high-scores episodes)
/// </summary>
/// <param name="settings">Its table (Scores, which GameHost.InitHighScores can replace)</param>
/// <param name="floorScore">The last floor's rating, 0 to 300 (Blake Stone's mission ratio)</param>
/// <param name="levelRecord">A map's best (records.cfg), for the episodes' rows</param>
internal sealed class HighScoresScreen(GameEngineManager engine, AssetManager assets, InputManager input, AudioManager audio,
    VideoManager video, GraphicManager graphics, MenuDrawing draw, MenuDefs defs, UiText ui, LineInput lineInput,
    GameSession session, Configuration.GameSettings settings, Func<int> floorScore, Func<string, Gameplay.LevelResult?>? levelRecord = null)
{
    private HighScoresInfo HighScoresLayout => engine.GetGameInfo().HighScores;

    internal void DrawHighScores()
    {
        var layout = HighScoresLayout;
        var shadow = layout.Shadow is { } shadowColor ? new FontShadow(1, 1, shadowColor) : null;
        var style = new TextStyle(layout.Font, layout.Color, "BORDCOLOR", Shadow: shadow);

        // A pack's frame (a menudef's components), else Wolf3D's stripes
        if (!string.IsNullOrEmpty(layout.Frame))
            defs.DrawMenuComponents(layout.Frame);
        else
        {
            draw.ClearMScreen();
            draw.DrawStripes(10);
        }

        if (!string.IsNullOrEmpty(layout.Pic))      // pic: "" for none (labels can head the screen)
            graphics.DrawPic(layout.Pic, layout.PicX, layout.PicY);
        foreach (var header in layout.Headers)
            graphics.DrawPic(header.Pic, header.X, header.Y);
        var language = assets.GetText("en-us");
        foreach (var label in layout.Labels)
            ui.TextAt(label.X, label.Y, style with { Font = label.Font ?? style.Font, Color = label.Color ?? style.Color })
                .Print(label.Text.ToLanguageText(language));

        if (layout.Episodes is { } episodes)
        {
            DrawEpisodes(episodes, style, text => text.ToLanguageText(language));
            video.Update();
            return;
        }

        var table = settings.Scores;
        for (int i = 0; i < table.Length; i++)
        {
            HighScore s = table[i];
            int y = layout.RowY + (layout.RowHeight * i);

            //
            // name
            //
            ui.TextAt(layout.NameX, y, style).Print(s.Name);

            //
            // level
            //
            var buffer = (s.Completed & ~HighScore.Won).ToString();
            int x = layout.LevelRight - ui.TextWidth(buffer, style.Font);
            if (!layout.ShowLevel)
            { }
            else if ((s.Completed & HighScore.Won) != 0 && !string.IsNullOrEmpty(layout.WonPic))
                graphics.DrawPic(layout.WonPic, x + 8, y - 1);
            else if (layout.ShowEpisode)
                ui.TextAt(x - 6, y, style).Print($"E{s.Episode + 1}/L{buffer}");
            else
                ui.TextAt(x, y, style).Print(buffer);

            //
            // score
            //
            buffer = s.Score.ToString();
            ui.TextAt(layout.ScoreRight - ui.TextWidth(buffer, style.Font), y, style).Print(buffer);

            //
            // ratio
            //
            if (layout.RatioRight > 0)
            {
                buffer = s.Ratio.ToString();
                ui.TextAt(layout.RatioRight - ui.TextWidth(buffer, style.Font), y, style).Print(buffer);
            }
        }

        video.Update();
    }

    // Each episode's best in place of the table (see HighScoresEpisodes)
    private void DrawEpisodes(HighScoresEpisodes layout, TextStyle style, Func<string, string> text)
    {
        var gameInfo = engine.GetGameInfo();
        var valueStyle = style with { Color = layout.ValueColor ?? style.Color };
        for (int i = 0; i < layout.Rows.Count; i++)
        {
            var row = layout.Rows[i];
            int y = layout.Y + layout.RowHeight * i;
            gameInfo.Episodes.TryGetValue(row.Episode, out var episode);
            ui.TextAt(layout.NameX, y, style).Print(text(row.Name ?? episode?.Name ?? row.Episode));

            var best = episode != null && levelRecord != null
                ? EpisodeBest([.. LevelStatsPanel.EpisodeLevels(episode, gameInfo.Maps).Select(levelRecord)])
                : null;
            if (best == null)
            {
                ui.TextAt(layout.LabelX, y, style).Print(text(layout.Incomplete));
                continue;
            }

            if (layout.TimeX >= 0)
                ui.TextAt(layout.TimeX, y, valueStyle).Print(LevelStatsPanel.Text(new MenuStatsLine { Value = "time" }, best)!);
            foreach (var line in layout.Lines)
            {
                ui.TextAt(layout.LabelX, y + line.Y, style).Print(text(line.Label));
                ui.TextAt(layout.ValueX, y + line.Y, valueStyle).Print(LineText(line, best));
            }
        }
    }

    /// <summary>An episode's levels' bests together; null when none of them has been finished</summary>
    internal static LevelStats? EpisodeBest(IReadOnlyList<Gameplay.LevelResult?> records) =>
        records.Any(r => r != null) ? LevelStatsPanel.Episode(records) : null;

    /// <summary>A line's value as the episodes' rows show it</summary>
    internal static string LineText(HighScoresEpisodeLine line, LevelStats stats) =>
        LevelStatsPanel.Text(new MenuStatsLine { Value = line.Value, Count = line.Count }, stats) ?? "";

    /// <summary>
    /// Ranks the score among the high scores, and has a name typed for it if it made the list.
    /// The level recorded is the current map's floor number, marked as won when <paramref name="won"/>.
    /// With the episodes' bests in place of the table (high-scores episodes), they're just shown.
    /// </summary>
    internal void CheckHighScore(int score, bool won)
    {
        var gameInfo = engine.GetGameInfo();
        if (gameInfo.HighScores.Episodes != null)
        {
            audio.PlayMusic(gameInfo.HighScoresMusic ?? "");
            DrawHighScores();
            video.FadeIn();
            input.ClearKeysDown();
            input.UserInput(500);
            return;
        }

        var gamestate = session.GameState;
        int floor = gameInfo.Maps.TryGetValue(gamestate.MapOn, out var mapInfo) ? mapInfo.FloorNumber : 1;

        var myscore = NewScore(score, won, floor, gamestate.Cluster, floorScore());
        var table = settings.Scores;
        int n = HighScore.Insert(table, myscore);

        audio.PlayMusic(gameInfo.HighScoresMusic ?? "");     // StartCPMusic(HIGHSCORESSONG)
        DrawHighScores();

        video.FadeIn();

        if (n != -1)
        {
            //
            // got a high score
            //
            var layout = HighScoresLayout;
            int x = layout.NameX, y = layout.RowY + (layout.RowHeight * n);
            if (layout.EntryBarWidth > 0)
            {
                video.Bar(x - 2, y - 2, layout.EntryBarWidth, Math.Min(15, layout.RowHeight), layout.EntryBackground);
                video.Update();
            }
            else
            {
                // The name typed over a blank row
                video.Bar(x, y, Math.Max(layout.EntryWidth, 1), layout.RowHeight, layout.EntryBackground);
                video.Update();
            }

            string str = table[n].Name;
            lineInput.Run(x, y, ref str, "", true, HighScore.MaxHighName, layout.EntryWidth,
                new TextStyle(layout.Font, layout.EntryColor, layout.EntryBackground));
            table[n].Name = str;
            engine.WriteConfig();
        }
        else
        {
            input.ClearKeysDown();
            input.UserInput(500);
        }
    }

    /// <summary>
    /// A finished game's score as the table keeps it, not yet named: the floor it ended on (won
    /// or not), the episode (the cluster, shown from 1 as id's were) and the last floor's rating
    /// </summary>
    internal static HighScore NewScore(int score, bool won, int floor, int cluster, int floorScore) => new()
    {
        Name = "",
        Score = score,
        Episode = (ushort)Math.Max(cluster - 1, 0),
        Completed = (ushort)(Math.Clamp(floor, 0, HighScore.Won - 1) | (won ? HighScore.Won : 0)),
        Ratio = (ushort)Math.Clamp(floorScore * 100 / 300, 0, 100),     // the last floor's rating
    };
}
