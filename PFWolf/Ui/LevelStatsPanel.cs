using PFWolf.Assets;
using PFWolf.Extensions;
using PFWolf.Fonts;
using PFWolf.Gameplay;
using PFWolf.Menus;

namespace PFWolf.Ui;

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
}
