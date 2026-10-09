using System.Text;
using PFWolf.Constants;
using PFWolf.Editor.Editing;
using PFWolf.Loaders;

namespace PFWolf.Tests;

public class MapImportTests
{
    // A WDC .map file: "WDC3.1", map count, plane count, name length, then each map's name,
    // width, height and planes; tile i of plane p of map m is m * 10000 + p * 1000 + i % 97
    private static byte[] WdcFile(int maps, int planes, int size = MapConstants.MAPSIZE, string name = "Imported")
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(Encoding.ASCII.GetBytes("WDC3.1"));
        bw.Write(maps);
        bw.Write((ushort)planes);
        bw.Write((ushort)16);
        for (int m = 0; m < maps; m++)
        {
            var nameBytes = new byte[16];
            Encoding.ASCII.GetBytes($"{name}{m + 1}", nameBytes);
            bw.Write(nameBytes);
            bw.Write((ushort)size);
            bw.Write((ushort)size);
            for (int p = 0; p < planes; p++)
                for (int i = 0; i < size * size; i++)
                    bw.Write((ushort)(m * 10000 + p * 1000 + i % 97));
        }
        bw.Flush();
        return ms.ToArray();
    }

    [Test]
    public void Read_Takes_Every_Map_And_Plane_Of_A_Wdc_File()
    {
        // Act
        var maps = WdcMapFile.ReadFile(WdcFile(maps: 2, planes: 3));

        // Assert
        Assert.That(maps, Has.Count.EqualTo(2));
        Assert.That(maps.Select(map => map.Name), Is.EqualTo(new[] { "Imported1", "Imported2" }));
        Assert.That(maps[1].Planes, Has.Length.EqualTo(3));
        Assert.That(maps[1].Planes[2][5], Is.EqualTo(12005));
    }

    [Test]
    public void ReadFile_Reads_An_EcWolf_Wad_Too()
    {
        // Arrange
        var wad = EcWolfMapLoader.Save(EditorMaps.Blank("Wad level"), "MAP01");

        // Act
        var maps = WdcMapFile.ReadFile(wad);

        // Assert
        Assert.That(maps.Single().Name, Is.EqualTo("Wad level"));
        Assert.That(maps.Single().Planes[0][0], Is.EqualTo(100));
    }

    [Test]
    public void Read_Refuses_A_File_That_Isnt_One()
    {
        Assert.Throws<InvalidDataException>(() => WdcMapFile.ReadFile(Encoding.ASCII.GetBytes("not a map at all")));
        Assert.Throws<InvalidDataException>(() => WdcMapFile.ReadFile(WdcFile(1, 3)[..^10]));
    }

    [Test]
    public void Check_Refuses_Another_Size()
    {
        var map = WdcMapFile.Read(WdcFile(1, 2, size: 32))[0];

        Assert.That(MapImport.Check(map), Does.Contain("32x32"));
        Assert.That(MapImport.Check(WdcMapFile.Read(WdcFile(1, 2))[0]), Is.Null);
    }

    [Test]
    public void Apply_Replaces_Only_The_Planes_Picked_As_One_Undo_Step()
    {
        // Arrange
        var document = EditorMaps.Document();
        var source = WdcMapFile.Read(WdcFile(1, 3))[0];

        // Act: just the things
        MapImport.Apply(document, source, [new PlaneImport(1, 1)], "Import");

        // Assert
        Assert.That(document.Map.MapData[1], Is.EqualTo(source.Planes[1]));
        Assert.That(document.Map.MapData[0], Has.All.EqualTo(100));
        Assert.That(document.Map.MapData[2], Has.All.EqualTo(0));
        document.Undo();
        Assert.That(document.Map.MapData[1], Has.All.EqualTo(0));
        Assert.That(document.CanUndo, Is.False);
    }

    [Test]
    public void Apply_Puts_A_Plane_Onto_Another()
    {
        // Arrange: an old level with its wall heights on plane 2
        var document = EditorMaps.Document();
        var source = WdcMapFile.Read(WdcFile(1, 3))[0];

        // Act
        MapImport.Apply(document, source, [new PlaneImport(2, MapConstants.HEIGHTPLANE)], "Import");

        // Assert
        Assert.That(document.Map.MapData[MapConstants.HEIGHTPLANE], Is.EqualTo(source.Planes[2]));
        Assert.That(document.Map.MapData[2], Has.All.EqualTo(0));
    }

    [Test]
    public void NewLevel_Fills_The_Planes_Not_Imported_As_A_New_Level()
    {
        // Arrange
        var source = WdcMapFile.Read(WdcFile(1, 3))[0];

        // Act: things only, so the walls are a new level's walled square
        var map = MapImport.NewLevel("Imported1", source, [new PlaneImport(1, 1)], wall: 1, floor: 107);

        // Assert
        Assert.That(map.Name, Is.EqualTo("Imported1"));
        Assert.That(map.MapData, Has.Length.EqualTo(MapConstants.LEVELPLANES));
        Assert.That(map.MapData[1], Is.EqualTo(source.Planes[1]));
        Assert.That(map.MapData[0][0], Is.EqualTo(1));
        Assert.That(map.MapData[0][MapConstants.MAPSIZE + 1], Is.EqualTo(107));
        Assert.That(map.MapData[2], Has.All.EqualTo(0));
    }

    [Test]
    public void ImportablePlanes_Stops_At_The_Planes_A_Level_Has()
    {
        var source = WdcMapFile.Read(WdcFile(1, 8))[0];

        Assert.That(MapImport.ImportablePlanes(source), Is.EqualTo(Enumerable.Range(0, MapConstants.LEVELPLANES)));
    }
}
