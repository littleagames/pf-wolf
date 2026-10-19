using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Editing;
using PFWolf.Loaders;
using PFWolf.Loaders.Uwmf;

namespace PFWolf.Tests;

/// <summary>The editor saving a level as UWMF or binary planes (MapFiles.Save, MapDocument.SetFormat)</summary>
public class MapFormatSaveTests
{
    private string _folder = "";

    [SetUp]
    public void MakeFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), "pfwolf-format-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void RemoveFolder()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private static MapAsset ReadBack(byte[] file, MapObjectTranslationAsset defs)
    {
        var read = EcWolfMapLoader.Load(file);
        return read.NeedsTranslation ? UwmfTranslator.ToMapAsset(read.Uwmf!, defs) : read;
    }

    [Test]
    public void A_Level_Switched_To_Uwmf_Saves_As_A_Textmap_That_Reads_Back_The_Same()
    {
        // Arrange: a level read as binary planes, switched to UWMF
        var defs = UwmfTranslatorTests.Defs();
        var document = EditorMaps.Document("map07");
        EditorMaps.Set(document, 0, 3, 4, 1);
        EditorMaps.Set(document, MapConstants.TagPlane, 5, 6, 9);
        document.MarkSaved();
        document.SetFormat(MapFormat.Uwmf);
        Assert.That(document.IsDirty, Is.True, "a new format is a change to save");

        // Act
        var path = MapFiles.Save(document, _folder, defs);

        // Assert
        var file = File.ReadAllBytes(path);
        Assert.That(System.Text.Encoding.ASCII.GetString(file), Does.Contain("TEXTMAP"));
        var back = ReadBack(file, defs);
        for (int p = 0; p < MapConstants.LevelPlanes; p++)
            Assert.That(back.MapData[p], Is.EqualTo(document.Map.MapData[p]), $"plane {p}");
        Assert.That(document.IsDirty, Is.False);
    }

    [Test]
    public void A_Mod_Keeping_The_Level_As_Uwmf_Text_Gets_It_There_And_Loses_A_Wad_Of_It()
    {
        // Arrange: maps/MAP07.uwmf and a stale maps/MAP07.wad
        Directory.CreateDirectory(Path.Combine(_folder, "maps"));
        File.WriteAllText(Path.Combine(_folder, "maps", "MAP07.uwmf"), "old");
        File.WriteAllBytes(Path.Combine(_folder, "maps", "MAP07.wad"), [1, 2, 3]);
        var document = EditorMaps.Document("map07");
        document.SetFormat(MapFormat.Uwmf);

        // Act
        var path = MapFiles.Save(document, _folder, UwmfTranslatorTests.Defs());

        // Assert
        Assert.That(path, Is.EqualTo(Path.Combine(_folder, "maps", "MAP07.uwmf")));
        Assert.That(File.ReadAllText(path), Does.StartWith("namespace = "));
        Assert.That(File.Exists(Path.Combine(_folder, "maps", "MAP07.wad")), Is.False);
    }

    [Test]
    public void A_Level_Saved_As_Planes_Loses_A_Uwmf_Text_Of_It()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "maps"));
        File.WriteAllText(Path.Combine(_folder, "maps", "MAP07.uwmf"), "old");
        var document = EditorMaps.Document("map07");

        var path = MapFiles.Save(document, _folder);

        Assert.That(path, Is.EqualTo(Path.Combine(_folder, "maps", "MAP07.wad")));
        Assert.That(EcWolfMapLoader.Load(File.ReadAllBytes(path)).Format, Is.EqualTo(MapFormat.Planes));
        Assert.That(File.Exists(Path.Combine(_folder, "maps", "MAP07.uwmf")), Is.False);
    }

    [Test]
    public void A_Level_With_Its_Own_Definitions_Is_Always_Saved_As_Uwmf()
    {
        // Arrange: a binary-format level holding an extra thing
        var document = EditorMaps.Document("map07");
        document.Map.ExtraThings = [new MapThing("Barrel", 3.25, 3.5, 0)];

        // Act
        var path = MapFiles.Save(document, _folder, UwmfTranslatorTests.Defs());

        // Assert
        var back = ReadBack(File.ReadAllBytes(path), UwmfTranslatorTests.Defs());
        Assert.That(back.ExtraThings, Is.EqualTo(document.Map.ExtraThings));
    }

    [Test]
    public void A_New_Level_Is_Uwmf()
    {
        Assert.That(MapFiles.NewMap("New", 1, 107).Format, Is.EqualTo(MapFormat.Uwmf));
    }
}
