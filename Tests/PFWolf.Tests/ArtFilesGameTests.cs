using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;

namespace PFWolf.Tests;

/// <summary>
/// The art browser changing a mod's pictures, checked by loading Wolf3D again with the mod. Needs a
/// built game folder (pfwolf.pk3 and the data files).
/// </summary>
[Category("GameData")]
public class ArtFilesGameTests
{
    private string _folder = "";

    [SetUp]
    public void MakeFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), "pfwolf-artfiles-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void RemoveFolder()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private static string? GameFolder()
    {
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        return new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
    }

    private static GameContent Load(params string[] mods)
    {
        var folder = GameFolder();
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            return GameContent.Load(folder!, "wolf3d", mods);
        }
        catch (PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"Wolf3D's data files aren't in {folder}");
            throw;
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
        }
    }

    private string MakeMod()
    {
        var pk3 = Path.Combine(_folder, "art.pk3");
        ModFiles.CreateArchive(pk3, [("modinfo.yaml", "name: Art test"u8.ToArray())]);
        return pk3;
    }

    [Test]
    public void Every_Wall_Texture_Saved_Into_A_Mod_Loads_Back_The_Same()
    {
        // Arrange: each of the game's wall textures written as the pixel editor would
        var content = Load();
        var mod = MakeMod();
        var names = ArtCatalog.Build(content).Where(entry => entry.Kind == ArtKind.Texture).Select(entry => entry.Name).ToList();
        var before = names.ToDictionary(name => name, name => ArtCache.Indexed(content, ArtKind.Texture, name)!);

        // Act
        var writes = names.SelectMany(name => ArtFiles.Save(content, ArtKind.Texture, name, before[name], null, mod).Writes).ToList();
        ModFiles.Write(mod, [.. writes]);
        var after = Load(mod);

        // Assert: every pixel the same color (an index can change to the first of the palette's
        // equal colors: the game matches a file's colors to that one)
        foreach (var name in names)
        {
            Assert.That(ArtFiles.InMod(after, ArtKind.Texture, name), Is.EqualTo(new ArtFile(mod, $"textures/{name}.png")), name);
            Assert.That(Colors(after, ArtCache.Indexed(after, ArtKind.Texture, name)!), Is.EqualTo(Colors(content, before[name])), name);
        }
    }

    private static PaletteColor[] Colors(GameContent content, IndexedPicture picture)
        => picture.Indices.Select(index => content.LoadedPalette[index]).ToArray();

    // Two of the game's wall textures
    private static (string First, string Second) TwoTextures(GameContent content)
    {
        var names = ArtCatalog.Build(content).Where(entry => entry.Kind == ArtKind.Texture).Select(entry => entry.Name).Take(2).ToList();
        return (names[0], names[1]);
    }

    [Test]
    public void A_Sprite_Keeps_Its_See_Through_Pixels_And_Takes_An_Offset()
    {
        // Arrange: a guard's front, from the data files
        var content = Load();
        var mod = MakeMod();
        var before = ArtCache.Indexed(content, ArtKind.Sprite, "GARDA1")!;

        // Act
        ArtFiles.SetOffset(content, "GARDA1", 3, -7, mod).Apply();
        var after = Load(mod);

        // Assert
        var sprite = after.Find<SpriteAsset>("GARDA1")!;
        Assert.That((sprite.Offset.x, sprite.Offset.y), Is.EqualTo((3, -7)));
        var picture = ArtCache.Indexed(after, ArtKind.Sprite, "GARDA1")!;
        Assert.That(picture.Opaque, Is.EqualTo(before.Opaque));
        for (int i = 0; i < picture.Indices.Length; i++)
        {
            if (before.Opaque == null || before.Opaque[i])
                Assert.That(after.LoadedPalette[picture.Indices[i]], Is.EqualTo(content.LoadedPalette[before.Indices[i]]), $"pixel {i}");
        }
    }

    [Test]
    public void A_Mods_Picture_Is_Renamed_Then_Deleted()
    {
        // Arrange: a new wall texture in the mod
        var content = Load();
        var mod = MakeMod();
        var png = PngFile.Write(ArtCache.Indexed(content, ArtKind.Texture, TwoTextures(content).First)!, content.LoadedPalette);
        ArtFiles.Add(ArtKind.Texture, [("MYWALL", png)], mod).Apply();
        var added = Load(mod);
        var file = ArtFiles.InMod(added, ArtKind.Texture, "MYWALL");

        // Act
        ArtFiles.Rename(file!, ArtKind.Texture, "NEWWALL").Apply();
        var renamed = Load(mod);
        ArtFiles.Delete(ArtFiles.InMod(renamed, ArtKind.Texture, "NEWWALL")!, ArtKind.Texture, "NEWWALL").Apply();
        var deleted = Load(mod);

        // Assert
        Assert.That(file, Is.EqualTo(new ArtFile(mod, "textures/MYWALL.png")));
        Assert.That(renamed.Assets.Exists<TextureAsset>("MYWALL"), Is.False);
        Assert.That(ArtFiles.InMod(renamed, ArtKind.Texture, "NEWWALL"), Is.EqualTo(new ArtFile(mod, "textures/NEWWALL.png")));
        Assert.That(deleted.Assets.Exists<TextureAsset>("NEWWALL"), Is.False);
    }

    [Test]
    public void Replacing_The_Games_Own_Picture_Puts_One_Of_That_Name_In_The_Mod()
    {
        // Arrange
        var content = Load();
        var mod = MakeMod();
        var (first, second) = TwoTextures(content);
        var png = PngFile.Write(ArtCache.Indexed(content, ArtKind.Texture, second)!, content.LoadedPalette);

        // Act: the first drawn as the second
        var change = ArtFiles.Replace(content, ArtKind.Texture, first, png, mod);
        change.Apply();
        var after = Load(mod);

        // Assert
        Assert.That(change.Writes.Select(write => write.EntryPath), Is.EqualTo(new[] { $"textures/{first}.png" }));
        Assert.That(Colors(after, ArtCache.Indexed(after, ArtKind.Texture, first)!), Is.EqualTo(Colors(content, ArtCache.Indexed(content, ArtKind.Texture, second)!)));
    }
}
