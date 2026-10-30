using PFWolf.Configuration;
using PFWolf.Gameplay;
using PFWolf.Loaders;
using PFWolf.Managers;

namespace PFWolf.Tests;

/// <summary>
/// The high scores with a mod loaded (Wolf3D): the mods' own table in modscores.dat is read and
/// written back, and config.cfg's, the game's own, is left as it was. Needs Wolf3D's data files,
/// as DemoRegressionTests does.
/// </summary>
[Category("GameData")]
public class ModScoresGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-modscores-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workFolder);
        HeadlessGame.PrepareFolder(_workFolder, "wolf3d");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_workFolder, recursive: true);
        }
        catch (IOException)
        {
            // The game may still be letting go of a file; it's only a temp folder
        }
    }

    [Test]
    public void A_Mods_Scores_Stay_In_Its_Own_Table()
    {
        // Arrange: config.cfg as a run without mods leaves it, and a mod with a table of its own
        // beside another mod set's
        var configFolder = Path.Combine(_workFolder, "config");
        var configPath = Path.Combine(configFolder, GameEngineManager.ConfigFileName);
        var modScoresPath = Path.Combine(configFolder, ModScores.FileName);
        var (exitCode, _) = HeadlessGame.Run(_workFolder, "wolf3d", "quit", Timeout);
        Assert.That(exitCode, Is.Zero);
        var configBefore = File.ReadAllBytes(configPath);
        Assert.That(File.Exists(modScoresPath), Is.False, "no mods, no mod scores");

        var modFolder = Path.Combine(_workFolder, "score-test");
        Directory.CreateDirectory(modFolder);
        File.WriteAllText(Path.Combine(modFolder, "modinfo.yaml"), "name: Score Test\n");
        var key = ModScores.KeyFor([ModSource.TryOpen(modFolder, [])!], null);
        using (var stream = File.Create(modScoresPath))
        {
            ModScores.Write(stream, new Dictionary<string, HighScore[]>
            {
                [key] = [new HighScore { Name = "Modded Player", Score = 99999, Completed = 2, Ratio = 50 }],
                ["other-mod"] = [new HighScore { Name = "Someone Else", Score = 1 }],
            });
        }

        // Act
        (exitCode, _) = HeadlessGame.Run(_workFolder, "wolf3d", "quit", Timeout, modFolder);

        // Assert
        Assert.That(exitCode, Is.Zero);
        Assert.That(File.ReadAllBytes(configPath), Is.EqualTo(configBefore), "config.cfg's own scores are untouched");
        var tables = ModScores.Read(modScoresPath);
        Assert.That(tables.Keys, Is.EquivalentTo(new[] { key, "other-mod" }));
        var top = tables[key][0];
        Assert.That((top.Name, top.Score, top.Ratio), Is.EqualTo(("Modded Player", 99999, (ushort)50)), "the mod's table was read and written back");
        Assert.That(tables[key], Has.Length.EqualTo(HighScore.WolfDefaults().Length), "the game's table size");
    }
}
