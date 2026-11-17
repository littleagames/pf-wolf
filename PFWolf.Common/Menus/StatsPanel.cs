using PFWolf.Assets;
using PFWolf.Fonts;

namespace PFWolf.Menus;

/// <summary>What a stats panel shows: seconds, kill and secret percentages, and each objective's percentage and counts</summary>
public sealed record LevelStats(int Seconds, int Kill, int Secret, Dictionary<string, (int Percent, int Done, int Total)> Objectives);

/// <summary>
/// The new-episode and new-level menus' stats-panel (menudefs): its window, title and lines.
/// What goes in it (records.cfg bests) is the game's (Ui/LevelStatsPanel.cs).
/// </summary>
public static class StatsPanel
{
    /// <summary>A line's value as shown, or null if it hides (hide-empty with none of its objective)</summary>
    public static string? Text(MenuStatsLine line, LevelStats stats)
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
    /// <param name="text">A title's or label's text as shown: a $language key's string</param>
    public static void Draw(MenuStatsPanel panel, LevelStats stats, MenuPainter painter, Func<string, string> text)
    {
        var canvas = painter.Canvas;
        var shown = panel.Lines.Select(line => (line, value: Text(line, stats))).Where(l => l.value != null).ToList();
        int hidden = panel.Lines.Count - shown.Count;
        painter.DrawWindow(panel.X, panel.Y, panel.Width, panel.Height - hidden * panel.LineHeight, panel.WindowColor);

        int x = panel.X + panel.Margin, y = panel.Y + panel.Top;
        var style = new TextStyle(panel.Font, panel.Color);
        if (panel.Title.Length > 0)
            TextWindow.At(canvas, x + panel.TitleX, y, new TextStyle(panel.Font, panel.TitleColor)).Print(text(panel.Title));
        for (int i = 0; i < shown.Count; i++)
        {
            int lineY = y + panel.LineHeight * (i + 1);
            TextWindow.At(canvas, x, lineY, style).Print(text(shown[i].line.Label));
            TextWindow.At(canvas, x + (shown[i].line.ValueX ?? panel.ValueX), lineY, style).Print(shown[i].value!);
        }
    }
}
