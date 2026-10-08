using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Editing;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// Every level the repository ships, and the games' own levels when their data files are about,
/// saved the way the editor saves them and read back unchanged
/// </summary>
public class MapRoundTripTests
{
    /// <summary>The maps/NAME.wad files in the repository (mods, examples, the arenas)</summary>
    public static IEnumerable<TestCaseData> RepositoryWads()
    {
        var root = RepoRootFrom(AppContext.BaseDirectory);
        if (root == null)
            yield break;

        foreach (var wad in Directory.EnumerateFiles(root, "*.wad", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                    && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                    && !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
                     .Order())
            yield return new TestCaseData(wad).SetName($"RoundTrip_{Path.GetRelativePath(root, wad).Replace('\\', '/')}");
    }

    [TestCaseSource(nameof(RepositoryWads))]
    public void A_Repository_Level_Saves_And_Loads_Back_Unchanged(string wad)
    {
        // Arrange
        var original = EcWolfMapLoader.Load(File.ReadAllBytes(wad));
        var name = Path.GetFileNameWithoutExtension(wad);

        // Act
        var saved = SaveThroughEditor(name, original);
        var reloaded = EcWolfMapLoader.Load(saved);

        // Assert
        AssertSameLevel(reloaded, original);
        Assert.That(SaveThroughEditor(name, reloaded), Is.EqualTo(saved), "saving it again writes the same bytes");
    }

    [TestCase("GAMEMAPS.WL6", "MAPHEAD.WL6")]
    [TestCase("GAMEMAPS.SOD", "MAPHEAD.SOD")]
    [Category("GameData")]
    public void Every_Game_Level_Saves_And_Loads_Back_Unchanged(string gameMaps, string mapHead)
    {
        // Arrange
        var folder = DataFolderCandidates().FirstOrDefault(folder => File.Exists(Path.Combine(folder, gameMaps)) && File.Exists(Path.Combine(folder, mapHead)));
        if (folder == null)
            Assert.Ignore($"{gameMaps} isn't in {string.Join(" or ", DataFolderCandidates())}; set PFWOLF_GAMEDATA to a folder that has it");
        var loader = Wolf3dMapFileLoader.FromFiles(Path.Combine(folder!, mapHead), Path.Combine(folder!, gameMaps));
        var levels = loader.GetAssets(Enumerable.Range(1, 100).Select(n => $"MAP{n:00}").ToList());

        // Act / Assert
        Assert.That(levels, Is.Not.Empty);
        foreach (var (name, level) in levels)
        {
            var original = (MapAsset)level;
            var reloaded = EcWolfMapLoader.Load(SaveThroughEditor(name, original));
            AssertSameLevel(reloaded, original, name);
        }
    }

    /// <summary>The bytes MapFiles.Save writes for the level</summary>
    private static byte[] SaveThroughEditor(string name, MapAsset map)
    {
        var folder = Path.Combine(Path.GetTempPath(), "pfwolf-roundtrip-" + Guid.NewGuid().ToString("N"));
        try
        {
            return File.ReadAllBytes(MapFiles.Save(new MapDocument(name, map), folder));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private static void AssertSameLevel(MapAsset actual, MapAsset expected, string what = "")
    {
        Assert.That(actual.Name, Is.EqualTo(expected.Name), $"{what} name");
        Assert.That(actual.Width, Is.EqualTo(expected.Width), $"{what} width");
        Assert.That(actual.Height, Is.EqualTo(expected.Height), $"{what} height");
        for (int plane = 0; plane < MapConstants.LEVELPLANES; plane++)
            Assert.That(actual.MapData[plane], Is.EqualTo(expected.MapData[plane]), $"{what} plane {plane}");
    }

    private static string? RepoRootFrom(string start)
    {
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "pfwolf-pk3")))
                return dir.FullName;
        }
        return null;
    }

    private static IEnumerable<string> DataFolderCandidates()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            yield return fromEnvironment;

        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        foreach (var configuration in new[] { "Debug", "Release" })
            yield return Path.Combine(gameBin, configuration, "net10.0");
    }
}
