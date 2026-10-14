using PFWolf.Assets;
using PFWolf.Extensions;
using PFWolf.Fonts;

namespace PFWolf.Ui;

/// <summary>The high scores: drawn (game-info high-scores lays them out), and a new one ranked and named</summary>
/// <param name="scores">The table (Program.Scores, which InitHighScores can replace)</param>
/// <param name="floorScore">The last floor's rating, 0 to 300 (Blake Stone's mission ratio)</param>
internal sealed class HighScoresScreen(GameEngineManager engine, AssetManager assets, InputManager input, AudioManager audio,
    VideoManager video, GraphicManager graphics, MenuDrawing draw, MenuDefs defs, UiText ui, LineInput lineInput,
    GameSession session, Func<HighScore[]> scores, Func<int> floorScore)
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

        var table = scores();
        for (int i = 0; i < table.Length; i++)
        {
            HighScore s = table[i];
            int y = layout.RowY + (layout.RowHeight * i);

            //
            // name
            //
            ui.TextAt(layout.NameX, y, style).Print(s.name);

            //
            // level
            //
            var buffer = (s.completed & ~HighScore.Won).ToString();
            int x = layout.LevelRight - ui.TextWidth(buffer, style.Font);
            if (!layout.ShowLevel)
            { }
            else if ((s.completed & HighScore.Won) != 0 && !string.IsNullOrEmpty(layout.WonPic))
                graphics.DrawPic(layout.WonPic, x + 8, y - 1);
            else if (layout.ShowEpisode)
                ui.TextAt(x - 6, y, style).Print($"E{s.episode + 1}/L{buffer}");
            else
                ui.TextAt(x, y, style).Print(buffer);

            //
            // score
            //
            buffer = s.score.ToString();
            ui.TextAt(layout.ScoreRight - ui.TextWidth(buffer, style.Font), y, style).Print(buffer);

            //
            // ratio
            //
            if (layout.RatioRight > 0)
            {
                buffer = s.ratio.ToString();
                ui.TextAt(layout.RatioRight - ui.TextWidth(buffer, style.Font), y, style).Print(buffer);
            }
        }

        video.Update();
    }

    /// <summary>
    /// Ranks the score among the high scores, and has a name typed for it if it made the list.
    /// The level recorded is the current map's floor number, marked as won when <paramref name="won"/>.
    /// </summary>
    internal void CheckHighScore(int score, bool won)
    {
        var gameInfo = engine.GetGameInfo();
        var gamestate = session.gamestate;
        int floor = gameInfo.Maps.TryGetValue(gamestate.mapon, out var mapInfo) ? mapInfo.FloorNumber : 1;

        var myscore = NewScore(score, won, floor, gamestate.cluster, floorScore());
        var table = scores();
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

            string str = table[n].name;
            lineInput.Run(x, y, ref str, "", true, HighScore.MaxHighName, layout.EntryWidth,
                new TextStyle(layout.Font, layout.EntryColor, layout.EntryBackground));
            table[n].name = str;
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
        name = "",
        score = score,
        episode = (ushort)Math.Max(cluster - 1, 0),
        completed = (ushort)(Math.Clamp(floor, 0, HighScore.Won - 1) | (won ? HighScore.Won : 0)),
        ratio = (ushort)Math.Clamp(floorScore * 100 / 300, 0, 100),     // the last floor's rating
    };
}
