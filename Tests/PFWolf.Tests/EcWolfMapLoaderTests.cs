using System.Text;
using PFWolf.Assets;
using PFWolf.Loaders;
using PFWolf.Managers;

namespace PFWolf.Tests;

public class EcWolfMapLoaderTests
{
    private static MapAsset MakeMap(string name, int planesInUse)
    {
        var data = new ushort[MapManager.LEVELPLANES][];
        for (int plane = 0; plane < data.Length; plane++)
        {
            data[plane] = new ushort[MapManager.MAPAREA];
            if (plane < planesInUse)
            {
                for (int i = 0; i < data[plane].Length; i++)
                    data[plane][i] = (ushort)(plane * 1000 + i % 97);
            }
        }

        return new MapAsset
        {
            Width = MapManager.MAPSIZE,
            Height = MapManager.MAPSIZE,
            Name = name,
            MapData = data,
        };
    }

    // The PLANES lump's plane count: after "WDC3.1" and the int32 map count
    private static int SavedPlaneCount(byte[] wad) => BitConverter.ToUInt16(WadFile.Read(wad)[1].Data, 10);

    [Test]
    public void Save_Then_Load_Round_Trips_Every_Plane()
    {
        // Arrange
        var map = MakeMap("Wolf1 Map1", MapManager.LEVELPLANES);

        // Act
        var loaded = EcWolfMapLoader.Load(EcWolfMapLoader.Save(map, "MAP01"));

        // Assert
        Assert.That(loaded.Name, Is.EqualTo("Wolf1 Map1"));
        Assert.That((loaded.Width, loaded.Height), Is.EqualTo((map.Width, map.Height)));
        for (int plane = 0; plane < MapManager.LEVELPLANES; plane++)
            Assert.That(loaded.MapData[plane], Is.EqualTo(map.MapData[plane]), $"plane {plane}");
    }

    [Test]
    public void Save_Leaves_Off_Empty_Trailing_Planes()
    {
        // Arrange: only GAMEMAPS' three planes are used
        var map = MakeMap("E1M1", MapManager.MAPPLANES);

        // Act
        var wad = EcWolfMapLoader.Save(map, "MAP01");
        var loaded = EcWolfMapLoader.Load(wad);

        // Assert
        Assert.That(SavedPlaneCount(wad), Is.EqualTo(MapManager.MAPPLANES));
        Assert.That(loaded.MapData, Has.Length.EqualTo(MapManager.LEVELPLANES));
        Assert.That(loaded.MapData[MapManager.LEVELPLANES - 1], Has.All.EqualTo(0));
    }

    [Test]
    public void Save_Keeps_Empty_Planes_Before_One_In_Use()
    {
        // Arrange: heights and tags empty, but light zones (the last) used
        var map = MakeMap("Zones", MapManager.MAPPLANES);
        map.MapData[MapManager.LEVELPLANES - 1][0] = 7;

        // Act
        var wad = EcWolfMapLoader.Save(map, "MAP01");

        // Assert
        Assert.That(SavedPlaneCount(wad), Is.EqualTo(MapManager.LEVELPLANES));
        Assert.That(EcWolfMapLoader.Load(wad).MapData[MapManager.LEVELPLANES - 1][0], Is.EqualTo(7));
    }

    [Test]
    public void Save_Cuts_The_Name_To_Sixteen()
    {
        // Act
        var loaded = EcWolfMapLoader.Load(EcWolfMapLoader.Save(MakeMap("A Name Far Too Long For It", 1), "MAP01"));

        // Assert
        Assert.That(loaded.Name, Is.EqualTo("A Name Far Too L"));
    }

    [Test]
    public void Load_Refuses_A_Textmap()
    {
        // Arrange
        var wad = WadFile.Write([new("MAP01", []), new("TEXTMAP", Encoding.ASCII.GetBytes("namespace = \"Wolf3D\";"))]);

        // Act / Assert
        Assert.Throws<NotSupportedException>(() => EcWolfMapLoader.Load(wad));
    }

    [Test]
    public void Load_Refuses_A_Wad_Without_Planes()
    {
        Assert.Throws<InvalidDataException>(() => EcWolfMapLoader.Load(WadFile.Write([new("MAP01", [])])));
        Assert.Throws<InvalidDataException>(() => EcWolfMapLoader.Load(WadFile.Write([new("MAP01", []), new("OTHER", [])])));
    }

    [Test]
    public void Load_Refuses_Another_Map_Size()
    {
        // Arrange
        var map = MakeMap("Small", 1);
        map.Width = 32;
        map.Height = 32;

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => EcWolfMapLoader.Load(EcWolfMapLoader.Save(map, "MAP01")));
    }

    [Test]
    public void Load_Refuses_A_Truncated_Planes_Lump()
    {
        // Arrange
        var lumps = WadFile.Read(EcWolfMapLoader.Save(MakeMap("Cut", 3), "MAP01"));
        var planes = lumps[1].Data[..^100];
        var wad = WadFile.Write([lumps[0], new("PLANES", planes)]);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => EcWolfMapLoader.Load(wad));
    }
}
