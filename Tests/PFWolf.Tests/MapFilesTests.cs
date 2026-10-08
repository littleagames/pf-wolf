using PFWolf.Constants;
using PFWolf.Editor.Editing;
using PFWolf.Loaders;

namespace PFWolf.Tests;

public class MapFilesTests
{
    private string _folder = "";

    [SetUp]
    public void MakeFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), "pfwolf-editor-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void RemoveFolder()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Test]
    public void Save_Writes_Maps_Name_Wad_That_Loads_Back_The_Same()
    {
        // Arrange
        var document = EditorMaps.Document("map07");
        EditorMaps.Set(document, 0, 3, 4, 1);
        EditorMaps.Set(document, MapConstants.ZONEPLANE, 5, 6, 9);

        // Act
        var path = MapFiles.Save(document, _folder);
        var loaded = EcWolfMapLoader.Load(File.ReadAllBytes(path));

        // Assert
        Assert.That(path, Is.EqualTo(Path.Combine(_folder, "maps", "MAP07.wad")));
        for (int plane = 0; plane < MapConstants.LEVELPLANES; plane++)
            Assert.That(loaded.MapData[plane], Is.EqualTo(document.Map.MapData[plane]), $"plane {plane}");
        Assert.That(loaded.Name, Is.EqualTo("Test"));
    }

    [Test]
    public void Save_Marks_The_Level_Saved_In_That_Folder()
    {
        // Arrange
        var document = EditorMaps.Document();
        EditorMaps.Set(document, 0, 3, 4, 1);

        // Act
        MapFiles.Save(document, _folder);

        // Assert
        Assert.That(document.IsDirty, Is.False);
        Assert.That(document.SaveFolder, Is.EqualTo(_folder));
        Assert.That(Directory.GetFiles(Path.Combine(_folder, "maps")), Has.Length.EqualTo(1), "no temporary file is left");
    }

    [TestCase("MAP61", true)]
    [TestCase("e1m1_x", true)]
    [TestCase("TOOLONGNAME", false)]
    [TestCase("MAP 61", false)]
    [TestCase("", false)]
    public void CheckName_Takes_WAD_Lump_Names(string name, bool fine)
    {
        // Act
        var problem = MapFiles.CheckName(name);

        // Assert
        Assert.That(problem == null, Is.EqualTo(fine));
    }

    [Test]
    public void NewMap_Is_Open_Floor_Inside_A_Wall()
    {
        // Act
        var map = MapFiles.NewMap("MAP61", wall: 1, floor: 107);

        // Assert
        Assert.That(map.MapData, Has.Length.EqualTo(MapConstants.LEVELPLANES));
        Assert.That(map.MapData[0][0], Is.EqualTo(1));
        Assert.That(map.MapData[0][63 * 64 + 63], Is.EqualTo(1));
        Assert.That(map.MapData[0][1 * 64 + 1], Is.EqualTo(107));
        Assert.That(map.MapData[1].All(tile => tile == 0), Is.True);
    }
}
