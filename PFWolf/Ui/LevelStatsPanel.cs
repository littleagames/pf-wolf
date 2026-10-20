using PFWolf.Assets;
using PFWolf.Extensions;
using PFWolf.Fonts;
using PFWolf.Gameplay;

namespace PFWolf.Ui;

/// <summary>What a stats panel shows: seconds, kill and secret percentages, and each objective's percentage and counts</summary>
internal sealed record LevelStats(int Seconds, int Kill, int Secret, Dictionary<string, (int Percent, int Done, int Total)> Objectives);

/*
=============================================================================

                              STATS PANEL

The new-episode and new-level menus' stats-panel (menudefs): the highlighted level's best from
records.cfg (Gameplay/LevelRecords.cs), or the episode's levels' (its level-select, else the maps
in its start map's cluster): their times added up, their percentages averaged with a level never
finished counting as nothing, their counts added up. Midnight Assault's DrawLevelStats and
DrawEpisodeStats. Kills and secrets with none on the level are 0%; an objective with none is 100%,
as the level-end screen has them.

=============================================================================
*/

internal static class LevelStatsPanel
{
    /// <summary>A level's best; nothing (never finished) is all zeros</summary>
    internal static LevelStats Of(LevelResult? record)
    {
        if (record == null)
            return new LevelStats(0, 0, 0, new(StringComparer.OrdinalIgnoreCase));
        return new LevelStats(record.Time / 70,
            record.KillTotal > 0 ? record.Kills * 100 / record.KillTotal : 0,
            record.SecretTotal > 0 ? record.Secrets * 100 / record.SecretTotal : 0,
            record.Objectives.ToDictionary(o => o.Key,
                o => (o.Value.Total > 0 ? o.Value.Done * 100 / o.Value.Total : 100, o.Value.Done, o.Value.Total),
                StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>An episode's levels' bests together (see STATS PANEL)</summary>
    internal static LevelStats Episode(IReadOnlyList<LevelResult?> records)
    {
        var levels = records.Select(Of).ToList();
        int n = Math.Max(levels.Count, 1);
        var names = levels.SelectMany(l => l.Objectives.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
        var objectives = names.ToDictionary(name => name, name =>
        {
            int percent = 0, done = 0, total = 0;
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].Objectives.TryGetValue(name, out var o))
                    (percent, done, total) = (percent + o.Percent, done + o.Done, total + o.Total);
                else if (records[i] != null)
                    percent += 100;             // finished, with none of it to do
            }
            return (percent / n, done, total);
        }, StringComparer.OrdinalIgnoreCase);
        return new LevelStats(levels.Sum(l => l.Seconds), levels.Sum(l => l.Kill) / n, levels.Sum(l => l.Secret) / n, objectives);
    }

    /// <summary>The episode's levels: its level-select, else the maps in its start map's cluster (game-info order), else its start map</summary>
    internal static List<string> EpisodeLevels(EpisodeInfo episode, IReadOnlyDictionary<string, MapInfo> maps)
    {
        if (episode.LevelSelect.Count > 0)
            return [.. episode.LevelSelect];
        if (maps.TryGetValue(episode.StartMap, out var start) && start.Cluster != 0)
            return [.. maps.Where(m => m.Value.Cluster == start.Cluster).Select(m => m.Key)];
        return [episode.StartMap];
    }

    /// <summary>A line's value as shown, or null if it hides (hide-empty with none of its objective)</summary>
    internal static string? Text(MenuStatsLine line, LevelStats stats)
    {
        var value = line.Value.Trim();
        if (value.Equals("time", StringComparison.OrdinalIgnoreCase))
            return $"{stats.Seconds / 3600}:{stats.Seconds / 60 % 60:00}:{stats.Seconds % 60:00}";
        if (value.Equals("kill", StringComparison.OrdinalIgnoreCase))
            return $"{stats.Kill}%";
        if (value.Equals("secret", StringComparison.OrdinalIgnoreCase))
            return $"{stats.Secret}%";
        if (value.StartsWith("objective.", StringComparison.OrdinalIgnoreCase))
        {
            bool any = stats.Objectives.TryGetValue(value["objective.".Length..], out var o);
            if (line.HideEmpty && (!any || o.Total == 0))
                return null;
            return line.Count ? $"{(any ? o.Done : 0)}" : $"{(any ? o.Percent : 0)}%";
        }
        return "";
    }

    /// <summary>The panel: its window, title and lines (those that hide take their room with them)</summary>
    internal static void Draw(MenuStatsPanel panel, LevelStats stats, GraphicManager graphics, MenuDrawing draw, Func<string, string> text)
    {
        var shown = panel.Lines.Select(line => (line, value: Text(line, stats))).Where(l => l.value != null).ToList();
        int hidden = panel.Lines.Count - shown.Count;
        draw.DrawWindow(panel.X, panel.Y, panel.Width, panel.Height - hidden * panel.LineHeight, panel.WindowColor);

        int x = panel.X + panel.Margin, y = panel.Y + panel.Top;
        var style = new TextStyle(panel.Font, panel.Color);
        if (panel.Title.Length > 0)
            TextWindow.At(graphics, x + panel.TitleX, y, new TextStyle(panel.Font, panel.TitleColor)).Print(text(panel.Title));
        for (int i = 0; i < shown.Count; i++)
        {
            int lineY = y + panel.LineHeight * (i + 1);
            TextWindow.At(graphics, x, lineY, style).Print(text(shown[i].line.Label));
            TextWindow.At(graphics, x + (shown[i].line.ValueX ?? panel.ValueX), lineY, style).Print(shown[i].value!);
        }
    }
}
