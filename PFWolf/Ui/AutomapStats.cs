using PFWolf.Assets;
using PFWolf.Gameplay;

namespace PFWolf.Ui;

/// <summary>
/// The automap's stats lines as they read now (game-info automap-stats-lines): each line's text
/// filled from its stats, those that don't show left out, and in place of an objectives line,
/// each objective's own. A line's color is null for the theme's AutomapStats.
/// </summary>
internal static class AutomapStats
{
    /// <param name="lines">Game-info automap-stats-lines</param>
    /// <param name="stats">The stats they name</param>
    /// <param name="localize">A text or $language key as the game shows it</param>
    /// <param name="inEpisode">Whether the level is one of an episode's (by its game-info id)</param>
    /// <param name="objectiveLines">The level objectives' own stats lines (Gameplay/Objectives.cs)</param>
    internal static List<(string Text, string? Color)> Lines(IEnumerable<AutomapStatsLine> lines, StatValues stats,
        Func<string, string> localize, Func<string, bool> inEpisode, Func<IEnumerable<string>> objectiveLines)
    {
        var shown = new List<(string, string?)>();
        foreach (var line in lines)
        {
            if (line.Episodes.Count > 0 && !line.Episodes.Any(inEpisode))
                continue;
            if (line.Objectives)
            {
                shown.AddRange(objectiveLines().Select(text => (text, line.Color)));
                continue;
            }
            if ((line.OnlyWith ?? (line.MinTotal > 0 ? line.Values.FirstOrDefault() : null)) is { } with
                && (stats.Get(with)?.Total ?? 0) < Math.Max(line.MinTotal, 1))
                continue;
            shown.Add((stats.Format(localize(line.Text), line.Values), line.Color));
        }
        return shown;
    }
}
