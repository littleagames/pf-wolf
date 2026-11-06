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
    [TestCase("blake", "")]
    [TestCase("blake-shareware", "")]
    [TestCase("planetstrike", "")]
    [TestCase("wolf3d", "examples/mods/switch-demo")]
    [TestCase("wolf3d", "examples/mods/fireball-demo")]
    [TestCase("wolf3d", "examples/mods/destructible-demo")]
    [TestCase("wolf3d", "examples/mods/puzzles-demo")]
    public void The_Games_Own_Levels_Have_No_Errors(string game, string mod)
    {
        // Arrange
        var content = Load(game, mod);

        // Act
        var errors = new List<string>();
        int warnings = 0;
        foreach (var entry in content.Maps.Where(map => string.IsNullOrEmpty(mod) || map.Name.Equals("MAP01", StringComparison.OrdinalIgnoreCase)))
        {
            var map = content.FindMap(entry.Name)!;
            var info = content.MapInfoOf(entry.Name);
            foreach (var problem in LevelChecks.Run(new MapTiles(content, map, () => MapProperties.From(info)), info != null, entry.Name))
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

    [Test]
    public void The_Puzzles_Demos_Switches_Each_Open_A_Puzzle_Its_Scripts_Make()
    {
        // Arrange
        var content = Load("wolf3d", "examples/mods/puzzles-demo");
        var map = content.FindMap("MAP01")!;
        var tiles = new MapTiles(content, map);

        // Act
        var puzzles = new List<string>();
        for (int y = 0; y < tiles.Height; y++)
            for (int x = 0; x < tiles.Width; x++)
                if (tiles.PuzzleAt(x, y) is { } puzzle)
                    puzzles.Add(puzzle.Name);
        var warnings = LevelChecks.Run(tiles, inGameInfo: true, "MAP01").Where(problem => problem.Message.Contains("puzzle")).ToList();

        // Assert: all eleven, and every one a script makes
        Assert.That(puzzles, Is.EquivalentTo(new[] { "needle", "keypad", "pipes", "safedial", "lockpick", "wires", "sequence", "lightsout", "sliding", "frequency", "power" }));
        Assert.That(content.PuzzleNames, Is.SupersetOf(puzzles));
        Assert.That(warnings, Is.Empty);
    }

    // The game's content as the editor loads it, with a mod (a path from the repository's root) or none
    private static GameContent Load(string game, string mod)
    {
        var folder = GameFolder();
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");
        var mods = string.IsNullOrEmpty(mod) ? [] : new[] { Path.Combine(TestPaths.RepoRoot(), mod) };

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            return GameContent.Load(folder!, game, mods);
        }
        catch (Exception e) when (e is PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"{game}'s data files aren't in {folder}");
            throw;
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
        }
    }
}
