using PFWolf.Editor.Data;
using PFWolf.Loaders.Uwmf;

namespace PFWolf.Tests;

/// <summary>
/// UWMF over the real games: every level written as a TEXTMAP and read back gives the same planes,
/// and the games' demos play out the same on UWMF copies of their levels. Needs a built game
/// folder with the data files, as DemoRegressionTests does.
/// </summary>
[Category("GameData")]
public class UwmfGameTests
{
    private static string? GameFolder()
    {
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        return new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
    }

    private static GameContent Load(string game)
    {
        var folder = GameFolder();
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");
        try
        {
            return GameContent.Load(folder!, game, []);
        }
        catch (PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"{game}'s data files aren't in {folder}");
            throw;
        }
    }

    [TestCase("wolf3d")]
    [TestCase("spear")]
    [TestCase("blake")]
    [TestCase("planetstrike")]
    public void Every_Level_Reads_Back_From_Uwmf_To_The_Same_Planes(string game)
    {
        // Arrange
        var content = Load(game);
        var problems = new List<string>();

        foreach (var entry in content.Maps)
        {
            var map = content.FindMap(entry.Name)!;

            // Act
            var writeWarnings = new List<string>();
            var text = UwmfWriter.Write(map, content.MapDefs, writeWarnings.Add);
            var readWarnings = new List<string>();
            var back = UwmfTranslator.ToMapAsset(UwmfReader.Read(text), content.MapDefs, readWarnings.Add);

            // Assert (collected, so one run shows every level that differs)
            for (int p = 0; p < map.MapData.Length; p++)
            {
                var differing = Enumerable.Range(0, map.MapData[p].Length).Where(i => map.MapData[p][i] != back.MapData[p][i]).ToList();
                if (differing.Count > 0)
                {
                    int i = differing[0];
                    problems.Add($"{entry.Name} plane {p}: {differing.Count} tiles differ, first at {i % map.Width},{i / map.Width}: "
                                 + $"{map.MapData[p][i]} became {back.MapData[p][i]}");
                }
            }
            if (back.LocalDefs != null)
                problems.Add($"{entry.Name}: read back with its own definitions");
            if (back.ExtraThings.Count > 0)
                problems.Add($"{entry.Name}: read back with {back.ExtraThings.Count} extra things");
            problems.AddRange(readWarnings.Concat(writeWarnings).Select(w => $"{entry.Name}: {w}"));
        }

        Assert.That(problems, Is.Empty, string.Join(Environment.NewLine, problems.Take(40)));
    }

    [TestCase("wolf3d")]
    [TestCase("spear")]
    public void Without_Ids_A_Level_Reads_Back_By_Its_Textures_And_Classes_To_The_Same_Planes(string game)
    {
        // Arrange: as an ECWolf-made level would be, with nothing naming PFWolf's codes
        var content = Load(game);
        var problems = new List<string>();

        foreach (var entry in content.Maps)
        {
            var map = content.FindMap(entry.Name)!;
            var text = string.Join("\n", UwmfWriter.Write(map, content.MapDefs).Split('\n')
                .Where(line => !line.TrimStart().StartsWith("id = ")));

            // Act
            var warnings = new List<string>();
            var back = UwmfTranslator.ToMapAsset(UwmfReader.Read(text), content.MapDefs, warnings.Add);

            // Assert
            for (int p = 0; p < map.MapData.Length; p++)
            {
                var differing = Enumerable.Range(0, map.MapData[p].Length).Where(i => map.MapData[p][i] != back.MapData[p][i]).ToList();
                if (differing.Count > 0)
                {
                    int i = differing[0];
                    problems.Add($"{entry.Name} plane {p}: {differing.Count} tiles differ, first at {i % map.Width},{i / map.Width}: "
                                 + $"{map.MapData[p][i]} became {back.MapData[p][i]}");
                }
            }
            if (back.LocalDefs != null || back.ExtraThings.Count > 0)
                problems.Add($"{entry.Name}: {back.LocalDefs?.Walls.Count ?? 0} own walls, {back.LocalDefs?.Doors.Count ?? 0} own doors, "
                             + $"{back.LocalDefs?.Triggers.Count ?? 0} own triggers ({string.Join(", ", back.LocalDefs?.Triggers.Values.Select(t => t.Action) ?? [])}), "
                             + $"area count {back.LocalDefs?.Floors.AreaCount}, {back.ExtraThings.Count} extra things");
            problems.AddRange(warnings.Select(w => $"{entry.Name}: {w}"));
        }

        Assert.That(problems, Is.Empty, string.Join(Environment.NewLine, problems.Take(40)));
    }

    [Test]
    public void A_Stock_Level_Is_Written_In_Ecwolfs_Namespace_With_Its_Names()
    {
        // Arrange
        var content = Load("wolf3d");
        var map = content.FindMap(content.Maps[0].Name)!;

        // Act
        var uwmf = UwmfReader.Read(UwmfWriter.Write(map, content.MapDefs));

        // Assert
        Assert.That(uwmf.Namespace, Is.EqualTo("Wolf3D"));
        Assert.That((uwmf.Width, uwmf.Height), Is.EqualTo((64, 64)));
        Assert.That(uwmf.Things.Select(t => t.String("type")), Has.Some.EqualTo("$Player1Start"));
        Assert.That(uwmf.Triggers.Select(t => t.String("action")), Has.Some.EqualTo("Door_Open").And.Some.EqualTo("Exit_Normal"));
    }

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    [TestCase("wolf3d")]
    [TestCase("spear")]
    public void The_Demos_Play_Out_The_Same_On_Uwmf_Copies_Of_The_Levels(string release)
    {
        // Arrange: every level exported as UWMF into a mod's maps/
        var work = Path.Combine(Path.GetTempPath(), "pfwolf-uwmfdemo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            HeadlessGame.PrepareFolder(work, release);
            var mod = Path.Combine(work, "uwmf");
            var maps = Path.Combine(mod, "maps");
            Directory.CreateDirectory(maps);
            File.WriteAllText(Path.Combine(mod, "modinfo.yaml"), $"name: Uwmf\nversion: \"1\"\ngame-packs: [{release}]\n");
            var (exportCode, exportOutput) = HeadlessGame.Run(work, release, $"exportmap all \"{maps}\" uwmf; quit", Timeout);
            Assert.That(exportCode, Is.Zero, string.Join(Environment.NewLine, exportOutput.TakeLast(30)));
            Assert.That(Directory.GetFiles(maps, "*.wad"), Is.Not.Empty, string.Join(Environment.NewLine, exportOutput.TakeLast(30)));
            Assert.That(File.ReadAllText(Directory.GetFiles(maps, "*.wad")[0]), Does.Contain("TEXTMAP"));

            // Act
            var (exitCode, output) = HeadlessGame.Run(work, release, "demotest; quit", Timeout, mod);

            // Assert
            var log = string.Join(Environment.NewLine, output.TakeLast(30));
            Assert.That(exitCode, Is.Zero, log);
            Assert.That(output, Has.None.Contains("UWMF").And.None.Contains("left out"), log);
            var results = output.Where(line => line.StartsWith("demotest ")).ToList();
            var baseline = Path.Combine(TestPaths.RepoRoot(), "Tests", "PFWolf.Tests", "Baselines", $"demotest-{release}.txt");
            Assert.That(results, Is.EqualTo(File.ReadAllLines(baseline).Where(line => line.Length > 0)), log);
        }
        finally
        {
            try
            {
                Directory.Delete(work, recursive: true);
            }
            catch (IOException)
            {
                // The game may still be letting go of a file; it's only a temp folder
            }
        }
    }
}
