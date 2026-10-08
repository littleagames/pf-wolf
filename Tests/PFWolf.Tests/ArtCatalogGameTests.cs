using PFWolf.Editor.Data;

namespace PFWolf.Tests;

/// <summary>The art browser's catalog over the real games. Needs a built game folder (pfwolf.pk3 and the data files).</summary>
[Category("GameData")]
public class ArtCatalogGameTests
{
    private static string? GameFolder()
    {
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        return new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
    }

    private static GameContent Load(string game, params string[] mods)
    {
        var folder = GameFolder();
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            return GameContent.Load(folder!, game, mods.Select(mod => Path.Combine(TestPaths.RepoRoot(), mod)));
        }
        catch (PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"{game}'s data files aren't in {folder}");
            throw;
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
        }
    }

    [TestCase("wolf3d")]
    [TestCase("spear")]
    public void Every_Wall_And_Thing_Picture_Is_Listed_And_Used(string game)
    {
        // Arrange
        var content = Load(game);

        // Act
        var entries = ArtCatalog.Build(content);

        // Assert: every texture mapdefs walls name is there, and says so
        var textures = entries.Where(entry => entry.Kind == ArtKind.Texture).ToDictionary(entry => entry.Name);
        foreach (var wall in content.MapDefs.Walls.Values.Where(wall => !string.IsNullOrEmpty(wall.North)))
        {
            Assert.That(textures, Does.ContainKey(wall.North.ToUpperInvariant()), wall.North);
            Assert.That(textures[wall.North.ToUpperInvariant()].Uses, Is.Not.Empty, wall.North);
        }

        // ... and every thing the palette shows has its sprite listed, used by its class
        var sprites = entries.Where(entry => entry.Kind == ArtKind.Sprite).ToDictionary(entry => entry.Name);
        foreach (var thingClass in content.MapDefs.Things.Values.Select(thing => thing.Class).Distinct())
        {
            if (content.ThingSpriteName(thingClass) is not { } name)
                continue;
            Assert.That(sprites, Does.ContainKey(name.ToUpperInvariant()), thingClass);
            // (by the class, or the parent whose Spawn state it has)
            Assert.That(sprites[name.ToUpperInvariant()].Uses, Is.Not.Empty, thingClass);
        }

        Assert.That(entries.Count(entry => entry.Kind == ArtKind.Picture), Is.GreaterThan(50), "the VGA pictures");
        TestContext.Out.WriteLine(string.Join(", ", entries.GroupBy(entry => entry.Kind).Select(kind => $"{kind.Key}: {kind.Count()}")));
    }

    [Test]
    public void A_Mods_Tall_Textures_Have_Their_Size_And_Its_Sky_Is_Used()
    {
        // Arrange
        var content = Load("wolf3d", "examples/mods/tall-walls-demo");

        // Act
        var entries = ArtCatalog.Build(content);

        // Assert: the mod's own textures come from it, at their own size
        var fromMod = entries.Where(entry => entry.Name.StartsWith("TW")).ToList();
        TestContext.Out.WriteLine(string.Join(", ", fromMod.Select(entry => $"{entry.Kind} {entry.Name} {entry.Width}x{entry.Height} from {entry.Source}")));
        Assert.That(fromMod.Where(entry => entry.Kind == ArtKind.Texture), Has.Some.Matches<ArtEntry>(entry => entry.Height > entry.Width));
        Assert.That(fromMod.Single(entry => entry.Name == "TWSKY").Uses.Select(use => use.Label), Has.Some.EndsWith("'s sky"));
    }
}
