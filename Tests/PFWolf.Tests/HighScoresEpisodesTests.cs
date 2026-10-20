using PFWolf.Assets;
using PFWolf.Gameplay;
using PFWolf.Loaders;
using PFWolf.Ui;

namespace PFWolf.Tests;

/// <summary>The high scores as each episode's best (game-info high-scores episodes, Ui/HighScoresScreen.cs)</summary>
public class HighScoresEpisodesTests
{
    private static LevelResult Record(int seconds, int kills, int killTotal, int secrets, int secretTotal, params (string Name, int Done, int Total)[] objectives) =>
        new()
        {
            Time = seconds * 70, Kills = kills, KillTotal = killTotal, Secrets = secrets, SecretTotal = secretTotal,
            Objectives = objectives.ToDictionary(o => o.Name, o => (o.Done, o.Total), StringComparer.OrdinalIgnoreCase),
        };

    [Test]
    public void An_Episode_With_No_Level_Finished_Has_No_Best()
    {
        Assert.That(HighScoresScreen.EpisodeBest([null, null, null]), Is.Null);
    }

    [Test]
    public void An_Episodes_Best_Averages_Over_All_Its_Levels()
    {
        // Arrange: one of three levels finished
        var best = HighScoresScreen.EpisodeBest([Record(180, 15, 20, 1, 2, ("intel", 3, 4)), null, null])!;

        // Act / Assert: the time as it is, the percentages a third of that level's
        Assert.That(LevelStatsPanel.Text(new MenuStatsLine { Value = "time" }, best), Is.EqualTo("0:03:00"));
        Assert.That(HighScoresScreen.LineText(new HighScoresEpisodeLine { Value = "kill" }, best), Is.EqualTo("25%"));
        Assert.That(HighScoresScreen.LineText(new HighScoresEpisodeLine { Value = "secret" }, best), Is.EqualTo("16%"));
        Assert.That(HighScoresScreen.LineText(new HighScoresEpisodeLine { Value = "objective.intel" }, best), Is.EqualTo("25%"));
        Assert.That(HighScoresScreen.LineText(new HighScoresEpisodeLine { Value = "objective.intel", Count = true }, best), Is.EqualTo("3"));
    }

    [Test]
    public void The_Layout_Reads_From_Game_Info()
    {
        // Act
        var gameInfo = YamlDataEntryLoader.Deserialize<GameInfoAsset>("""
            high-scores:
              episodes:
                y: 54
                value-color: "224"
                incomplete: "$STR_AWARDS_INCOMPLETE"
                rows:
                  - { episode: EP01, name: "$STR_EP1" }
                  - { episode: EP02 }
                lines:
                  - { label: "Kills:", value: kill, y: -12 }
                  - { label: "Intel:", value: objective.intel, y: 12 }
            """);

        // Assert
        var episodes = gameInfo.HighScores.Episodes!;
        Assert.That(episodes.Rows.Select(r => (r.Episode, r.Name)), Is.EqualTo(new[] { ("EP01", (string?)"$STR_EP1"), ("EP02", null) }));
        Assert.That(episodes.Lines.Select(l => (l.Value, l.Y)), Is.EqualTo(new[] { ("kill", -12), ("objective.intel", 12) }));
        Assert.That((episodes.Y, episodes.RowHeight, episodes.ValueColor, episodes.Incomplete), Is.EqualTo((54, 40, "224", "$STR_AWARDS_INCOMPLETE")));
    }

    [Test]
    public void Without_Episodes_It_Is_The_Score_Table()
    {
        Assert.That(new HighScoresInfo().Episodes, Is.Null);
    }
}
