using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;

namespace PFWolf.Tests;

/// <summary>
/// The level checks over the games' own levels, which play, so the checks mustn't call
/// anything in them an error. Needs a built game folder (pfwolf.pk3 and the data files).
/// </summary>
[Category("GameData")]
public class LevelChecksGameTests
{
    private static string? GameFolder()
    {
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        return new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
    }

    [TestCase("wolf3d", "")]
    [TestCase("spear", "")]
    [TestCase("wolf3d", "examples/mods/switch-demo")]
    public void The_Games_Own_Levels_Have_No_Errors(string game, string mod)
    {
        // Arrange
        var folder = GameFolder();
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");
        var mods = string.IsNullOrEmpty(mod) ? [] : new[] { Path.Combine(TestPaths.RepoRoot(), mod) };

        var workingFolder = Directory.GetCurrentDirectory();
        GameContent content;
        try
        {
            content = GameContent.Load(folder!, game, mods);
        }
        catch (Exception e) when (e is PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"{game}'s data files aren't in {folder}");
            return;
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
        }

        // Act
        var errors = new List<string>();
        int warnings = 0;
        foreach (var entry in content.Maps.Where(map => string.IsNullOrEmpty(mod) || map.Name.Equals("MAP01", StringComparison.OrdinalIgnoreCase)))
        {
            var map = content.FindMap(entry.Name)!;
            var info = content.MapInfoOf(entry.Name);
            foreach (var problem in LevelChecks.Run(new MapTiles(content, map, () => MapProperties.From(info)), info != null))
            {
                TestContext.Out.WriteLine($"{entry.Name} {problem}");
                if (problem.Severity == CheckSeverity.Error)
                    errors.Add($"{entry.Name} {problem}");
                else
                    warnings++;
            }
        }

        // Assert
        TestContext.Out.WriteLine($"{warnings} warnings");
        Assert.That(errors, Is.Empty);
    }
}
