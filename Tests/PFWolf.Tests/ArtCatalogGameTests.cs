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

    [Test]
    public void The_Switch_Demos_Animations_Are_Listed_With_Their_Frames()
    {
        // Arrange
        var content = Load("wolf3d", Path.Combine("examples", "mods", "switch-demo"));

        // Act
        var entries = ArtCatalog.Build(content);

        // Assert
        var alarm = entries.Single(entry => entry.Name == "ALARMLEVER");
        Assert.That(alarm.Kind, Is.EqualTo(ArtKind.Texture));
        Assert.That(alarm.Animation!.Frames.Select(frame => frame.Name), Is.EqualTo(new[] { "ELEV2_2", "ELEV1_2" }));
        Assert.That(alarm.Animation.Loops, Is.True);
        Assert.That(alarm.Origins.Select(origin => origin.Action), Does.Contain("animated"));
        var sticky = entries.Single(entry => entry.Name == "STICKYLEVER");
        Assert.That(sticky.Animation!.Loops, Is.False);
        Assert.That(sticky.Uses.Select(use => use.Label), Has.Some.StartsWith("Wall 55"));
        // A frame stays a plain texture, used by the animations it's in
        var frame = entries.Single(entry => entry.Name == "ELEV1_2" && entry.Kind == ArtKind.Texture);
        Assert.That(frame.Animation, Is.Null);
        Assert.That(frame.Uses.Select(use => use.Label), Does.Contain("Animation STICKYLEVER, frame 1").And.Contain("Animation ALARMLEVER, frame 2"));
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
        var sprites = entries.Where(entry => entry.Kind == ArtKind.Sprite).ToList();
        foreach (var thingClass in content.MapDefs.Things.Values.Select(thing => thing.Class).Distinct())
        {
            if (content.ThingSpriteName(thingClass) is not { } name)
                continue;
            var entry = sprites.SingleOrDefault(sprite => sprite.Holds(name));
            Assert.That(entry, Is.Not.Null, thingClass);
            // (by the class, or the parent whose Spawn state it has)
            Assert.That(entry!.Uses, Is.Not.Empty, thingClass);
        }

        // ... with a guard's walking frame as one entry of 8 sides
        var walking = sprites.Single(sprite => sprite.Name == "GARDB");
        Assert.That(walking.Rotations, Is.EqualTo(Enumerable.Range(1, 8).Select(side => $"GARDB{side}")));
        Assert.That(sprites.Any(sprite => sprite.Name == "GARDB1"), Is.False);

        Assert.That(entries.Count(entry => entry.Kind == ArtKind.Picture), Is.GreaterThan(50), "the VGA pictures");
        TestContext.Out.WriteLine(string.Join(", ", entries.GroupBy(entry => entry.Kind).Select(kind => $"{kind.Key}: {kind.Count()}")));
    }

    [TestCase("wolf3d")]
    [TestCase("blake-aog")]
    public void Stock_Flats_Have_No_See_Through_Pixels(string game)
    {
        // Arrange
        var content = Load(game);
        var flats = content.MapDefs.Flats.Floor.Values.Concat(content.MapDefs.Flats.Ceiling.Values).Distinct(StringComparer.OrdinalIgnoreCase);

        // Act
        var holed = flats.Where(name => content.Find<PFWolf.Assets.TextureAsset>(name) is { } texture
            && texture.RawData.Contains(PFWolf.Editor.Rendering.ArtCache.FlatMask)).ToList();

        // Assert: 0xFF in a flat is see-through, so the games' own flats mustn't use it
        Assert.That(holed, Is.Empty);
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
