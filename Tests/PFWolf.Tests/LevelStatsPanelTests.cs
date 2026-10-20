using PFWolf.Assets;
using PFWolf.Gameplay;
using PFWolf.Loaders;
using PFWolf.Ui;

namespace PFWolf.Tests;

/// <summary>The new-episode and new-level menus' stats panel (Ui/LevelStatsPanel.cs)</summary>
public class LevelStatsPanelTests
{
    private static LevelResult Record(int seconds, int kills, int killTotal, int secrets, int secretTotal, params (string Name, int Done, int Total)[] objectives) =>
        new()
        {
            Time = seconds * 70, Kills = kills, KillTotal = killTotal, Secrets = secrets, SecretTotal = secretTotal,
            Objectives = objectives.ToDictionary(o => o.Name, o => (o.Done, o.Total), StringComparer.OrdinalIgnoreCase),
        };

    private static readonly MenuStatsLine Time = new() { Value = "time" }, Kill = new() { Value = "kill" }, Secret = new() { Value = "secret" },
        Intel = new() { Value = "objective.intel" }, Kilroys = new() { Value = "objective.kilroys", Count = true, HideEmpty = true };

    [Test]
    public void A_Levels_Stats_Are_Its_Best()
    {
        var stats = LevelStatsPanel.Of(Record(3725, 9, 12, 1, 4, ("intel", 1, 2), ("kilroys", 1, 2)));

        Assert.That(LevelStatsPanel.Text(Time, stats), Is.EqualTo("1:02:05"));
        Assert.That(LevelStatsPanel.Text(Kill, stats), Is.EqualTo("75%"));
        Assert.That(LevelStatsPanel.Text(Secret, stats), Is.EqualTo("25%"));
        Assert.That(LevelStatsPanel.Text(Intel, stats), Is.EqualTo("50%"));
        Assert.That(LevelStatsPanel.Text(Kilroys, stats), Is.EqualTo("1"));
    }

    [Test]
    public void A_Level_Never_Finished_Is_Nothing_And_Hides_Its_Empty_Lines()
    {
        var stats = LevelStatsPanel.Of(null);

        Assert.That(LevelStatsPanel.Text(Time, stats), Is.EqualTo("0:00:00"));
        Assert.That(LevelStatsPanel.Text(Kill, stats), Is.EqualTo("0%"));
        Assert.That(LevelStatsPanel.Text(Intel, stats), Is.EqualTo("0%"));
        Assert.That(LevelStatsPanel.Text(Kilroys, stats), Is.Null);
    }

    [Test]
    public void An_Episodes_Stats_Add_Up_Times_And_Counts_And_Average_The_Rest()
    {
        // Arrange: one level with half its intel, one finished with none to find, one never finished
        var stats = LevelStatsPanel.Episode([
            Record(60, 10, 10, 1, 1, ("intel", 1, 2), ("kilroys", 2, 2)),
            Record(30, 5, 10, 0, 0),
            null]);

        // Assert: kills (100 + 50 + 0) / 3, secrets (100 + 0 + 0) / 3, intel (50 + 100 + 0) / 3
        Assert.That(LevelStatsPanel.Text(Time, stats), Is.EqualTo("0:01:30"));
        Assert.That(LevelStatsPanel.Text(Kill, stats), Is.EqualTo("50%"));
        Assert.That(LevelStatsPanel.Text(Secret, stats), Is.EqualTo("33%"));
        Assert.That(LevelStatsPanel.Text(Intel, stats), Is.EqualTo("50%"));
        Assert.That(LevelStatsPanel.Text(Kilroys, stats), Is.EqualTo("2"));
    }

    [Test]
    public void A_Menudefs_Stats_Panel_Is_Read_And_Reaches_The_Menu()
    {
        // Act
        var asset = YamlDataEntryLoader.Deserialize<MenuAsset>("""
            position: { x: 16, y: 30 }
            stats-panel:
              x: 200
              y: 24
              top: 6
              font: SmallFont
              title: "$STR_EPISODE_STATS_STATS"
              lines:
                - { label: "Time:", value: time, value-x: 48 }
                - { label: "Kilroys:", value: objective.kilroys, count: true, hide-empty: true }
            """);
        var menu = Entities.MenuMetadata.BuildFromAsset(asset)!;

        // Assert
        var panel = menu.StatsPanel!;
        Assert.That((panel.X, panel.Y, panel.Top, panel.Width, panel.LineHeight, panel.Title), Is.EqualTo((200, 24, 6, 104, 12, "$STR_EPISODE_STATS_STATS")));
        Assert.That(panel.Lines.Select(l => (l.Value, l.Count, l.HideEmpty, l.ValueX)), Is.EqualTo(new[]
        {
            ("time", false, false, (int?)48),
            ("objective.kilroys", true, true, (int?)null),
        }));
    }

    [Test]
    public void An_Episodes_Levels_Are_Its_Level_Select_Else_Its_Cluster()
    {
        var info = YamlDataEntryLoader.Deserialize<GameInfoAsset>("""
            episodes:
              EP01: { name: "Campaign", start-map: MAP01, level-select: [MAP02, MAP01] }
              EP02: { name: "Two", start-map: MAP03 }
              EP03: { name: "Lone", start-map: MAP05 }
            maps:
              MAP01: { cluster: 1 }
              MAP02: { cluster: 1 }
              MAP03: { cluster: 2 }
              MAP04: { cluster: 2 }
              MAP05: {}
            """);

        Assert.That(LevelStatsPanel.EpisodeLevels(info.Episodes["EP01"], info.Maps), Is.EqualTo(new[] { "MAP02", "MAP01" }));
        Assert.That(LevelStatsPanel.EpisodeLevels(info.Episodes["EP02"], info.Maps), Is.EqualTo(new[] { "MAP03", "MAP04" }));
        Assert.That(LevelStatsPanel.EpisodeLevels(info.Episodes["EP03"], info.Maps), Is.EqualTo(new[] { "MAP05" }));
    }
}
