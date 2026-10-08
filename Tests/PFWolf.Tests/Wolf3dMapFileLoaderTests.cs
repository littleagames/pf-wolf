using System.Text;
using PFWolf.Exceptions;
using PFWolf.Loaders;
using PFWolf.Managers;

namespace PFWolf.Tests;

public class Wolf3dMapFileLoaderTests
{
    private const ushort RlewTag = 0xABCD;
    private const int PlaneBytes = MapManager.MAPAREA * sizeof(ushort);

    // A loader with no levels, for its decompression routines
    private static Wolf3dMapFileLoader EmptyLoader() => new(BitConverter.GetBytes(RlewTag), []);

    private static byte[] Words(params ushort[] words) => words.SelectMany(BitConverter.GetBytes).ToArray();

    [Test]
    public void CarmackExpand_Copies_Plain_Words()
    {
        // Arrange
        var dest = new ushort[3];

        // Act
        EmptyLoader().CAL_CarmackExpand(Words(1, 2, 3), dest, 6);

        // Assert
        Assert.That(dest, Is.EqualTo(new ushort[] { 1, 2, 3 }));
    }

    [Test]
    public void CarmackExpand_Near_Copy_Repeats_Recent_Words()
    {
        // Arrange: two words, then "copy 2 words from 2 back"
        byte[] source = [.. Words(0x1234, 0x5678), 0x02, (byte)Wolf3dMapFileLoader.NEARTAG, 0x02];
        var dest = new ushort[4];

        // Act
        EmptyLoader().CAL_CarmackExpand(source, dest, 8);

        // Assert
        Assert.That(dest, Is.EqualTo(new ushort[] { 0x1234, 0x5678, 0x1234, 0x5678 }));
    }

    [Test]
    public void CarmackExpand_Far_Copy_Repeats_From_An_Absolute_Place()
    {
        // Arrange: three words, then "copy 2 words from word 1"
        byte[] source = [.. Words(10, 20, 30), 0x02, (byte)Wolf3dMapFileLoader.FARTAG, .. Words(1)];
        var dest = new ushort[5];

        // Act
        EmptyLoader().CAL_CarmackExpand(source, dest, 10);

        // Assert
        Assert.That(dest, Is.EqualTo(new ushort[] { 10, 20, 30, 20, 30 }));
    }

    [TestCase(Wolf3dMapFileLoader.NEARTAG)]
    [TestCase(Wolf3dMapFileLoader.FARTAG)]
    public void CarmackExpand_Zero_Count_Escapes_A_Tag_Word(ushort tag)
    {
        // Arrange: a literal word whose high byte is the tag
        byte[] source = [0x00, (byte)tag, 0x42];
        var dest = new ushort[1];

        // Act
        EmptyLoader().CAL_CarmackExpand(source, dest, 2);

        // Assert
        Assert.That(dest[0], Is.EqualTo((ushort)((tag << 8) | 0x42)));
    }

    [Test]
    public void RlewExpand_Repeats_Tagged_Runs()
    {
        // Act
        EmptyLoader().CA_RLEWexpand([5, RlewTag, 3, 9, 7], out var dest, 10, RlewTag);

        // Assert
        Assert.That(dest, Is.EqualTo(new ushort[] { 5, 9, 9, 9, 7 }));
    }

    // A one-level GAMEMAPS/MAPHEAD pair: plane 0 Carmack- then RLEW-compressed (as Wolf3D's are),
    // plane 1 only RLEW-compressed (as Blake Stone's MAPTEMP), plane 2 empty
    private static (byte[] MapHead, byte[] GameMaps) BuildMapPair(ushort plane0Fill, ushort plane1Fill, string name)
    {
        var rlew0 = Words(RlewTag, MapManager.MAPAREA, plane0Fill);
        // The Carmack-expanded data starts with the RLEW data's expanded length
        var carmack = Words((ushort)PlaneBytes).Concat(rlew0).ToArray();
        byte[] chunk0 = [.. Words((ushort)carmack.Length), .. carmack];

        byte[] chunk1 = [.. Words((ushort)PlaneBytes), .. Words(RlewTag, MapManager.MAPAREA, plane1Fill)];

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(Encoding.ASCII.GetBytes("TED5v1.0"));
        int start0 = (int)ms.Position;
        bw.Write(chunk0);
        int start1 = (int)ms.Position;
        bw.Write(chunk1);

        int header = (int)ms.Position;
        bw.Write(start0);
        bw.Write(start1);
        bw.Write(0);
        bw.Write((ushort)chunk0.Length);
        bw.Write((ushort)chunk1.Length);
        bw.Write((ushort)0);
        bw.Write((ushort)MapManager.MAPSIZE);
        bw.Write((ushort)MapManager.MAPSIZE);
        var nameBytes = new byte[16];
        Encoding.ASCII.GetBytes(name).CopyTo(nameBytes, 0);
        bw.Write(nameBytes);
        bw.Flush();

        byte[] mapHead = [.. BitConverter.GetBytes(RlewTag), .. BitConverter.GetBytes(header), .. BitConverter.GetBytes(0)];
        return (mapHead, ms.ToArray());
    }

    [Test]
    public void CacheMap_Expands_Both_Compressions_And_Leaves_Missing_Planes_Empty()
    {
        // Arrange
        var (mapHead, gameMaps) = BuildMapPair(1, 2, "Test Map");
        var loader = new Wolf3dMapFileLoader(mapHead, gameMaps);

        // Act
        var map = loader.CacheMap(0);

        // Assert
        Assert.That(map.Name, Is.EqualTo("Test Map"));
        Assert.That(map.MapData, Has.Length.EqualTo(MapManager.LEVELPLANES));
        Assert.That(map.MapData[0], Has.Length.EqualTo(MapManager.MAPAREA).And.All.EqualTo(1));
        Assert.That(map.MapData[1], Has.All.EqualTo(2));
        for (int plane = 2; plane < MapManager.LEVELPLANES; plane++)
            Assert.That(map.MapData[plane], Has.All.EqualTo(0), $"plane {plane}");
    }

    [Test]
    public void CacheMap_Name_Ends_At_The_First_NUL()
    {
        // Arrange: the 16-character name buffer has leftovers after its NUL, as id's levels do
        var (mapHead, gameMaps) = BuildMapPair(1, 2, "Wolf3 Map3\0t");
        var loader = new Wolf3dMapFileLoader(mapHead, gameMaps);

        // Act
        var map = loader.CacheMap(0);

        // Assert
        Assert.That(map.Name, Is.EqualTo("Wolf3 Map3"));
    }

    [Test]
    public void GetAssets_Names_Levels_And_Skips_Missing_Ones()
    {
        // Arrange: MAPHEAD's second level offset is 0 (none)
        var (mapHead, gameMaps) = BuildMapPair(1, 2, "Test Map");
        var loader = new Wolf3dMapFileLoader(mapHead, gameMaps);

        // Act
        var assets = loader.GetAssets(["E1M1", "E1M2", "E1M3"]);

        // Assert
        Assert.That(assets.Keys, Is.EqualTo(new[] { "e1m1" }));
    }

    [Test]
    public void GetAssets_Warns_About_And_Skips_A_Damaged_Level()
    {
        // Arrange: plane 0's length runs past the end of GAMEMAPS
        var (mapHead, gameMaps) = BuildMapPair(1, 2, "Broken");
        var header = BitConverter.ToInt32(mapHead, 2);
        BitConverter.GetBytes((ushort)0xFFFF).CopyTo(gameMaps, header + 12);
        var warnings = new List<string>();

        // Act
        var assets = new Wolf3dMapFileLoader(mapHead, gameMaps).GetAssets(["E1M1"], warnings.Add);

        // Assert
        Assert.That(assets, Is.Empty);
        Assert.That(warnings, Has.Count.EqualTo(1));
    }

    [Test]
    public void Constructor_Refuses_A_Level_Past_The_End()
    {
        // Arrange
        byte[] mapHead = [.. BitConverter.GetBytes(RlewTag), .. BitConverter.GetBytes(1000)];

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => new Wolf3dMapFileLoader(mapHead, new byte[100]));
    }

    [Test]
    public void Constructor_Refuses_A_Short_MapHead()
    {
        Assert.Throws<InvalidDataException>(() => new Wolf3dMapFileLoader([0], []));
    }

    [Test]
    public void CacheMap_Of_A_Missing_Level_Throws()
    {
        // Arrange
        var (mapHead, gameMaps) = BuildMapPair(1, 2, "Only");
        var loader = new Wolf3dMapFileLoader(mapHead, gameMaps);

        // Act / Assert
        Assert.Throws<PfWolfMapException>(() => loader.CacheMap(1));
    }
}
