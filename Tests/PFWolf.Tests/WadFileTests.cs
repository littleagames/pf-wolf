using System.Text;
using PFWolf.Loaders;

namespace PFWolf.Tests;

public class WadFileTests
{
    [Test]
    public void Write_Then_Read_Round_Trips()
    {
        // Arrange
        List<WadFile.Lump> lumps =
        [
            new("MAP01", []),
            new("PLANES", [1, 2, 3, 4, 5]),
            new("EXTRA", [9]),
        ];

        // Act
        var read = WadFile.Read(WadFile.Write(lumps));

        // Assert
        Assert.That(read.Select(l => l.Name), Is.EqualTo(new[] { "MAP01", "PLANES", "EXTRA" }));
        for (int i = 0; i < lumps.Count; i++)
            Assert.That(read[i].Data, Is.EqualTo(lumps[i].Data), lumps[i].Name);
    }

    [Test]
    public void Write_Upper_Cases_And_Cuts_Names_To_Eight()
    {
        // Act
        var read = WadFile.Read(WadFile.Write([new("verylongname", [])]));

        // Assert
        Assert.That(read[0].Name, Is.EqualTo("VERYLONG"));
    }

    [Test]
    public void Write_Starts_With_PWAD()
    {
        // Act
        var wad = WadFile.Write([]);

        // Assert
        Assert.That(Encoding.ASCII.GetString(wad, 0, 4), Is.EqualTo("PWAD"));
        Assert.That(WadFile.Read(wad), Is.Empty);
    }

    [Test]
    public void Read_Rejects_A_Short_File()
    {
        Assert.Throws<InvalidDataException>(() => WadFile.Read([0x50, 0x57]));
    }

    [Test]
    public void Read_Rejects_Another_Signature()
    {
        // Arrange
        var wad = WadFile.Write([new("A", [1])]);
        wad[0] = (byte)'X';

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => WadFile.Read(wad));
    }

    [Test]
    public void Read_Rejects_A_Directory_Past_The_End()
    {
        // Arrange: claim more lumps than the directory holds
        var wad = WadFile.Write([new("A", [1])]);
        BitConverter.GetBytes(50).CopyTo(wad, 4);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => WadFile.Read(wad));
    }

    [Test]
    public void Read_Rejects_A_Lump_Past_The_End()
    {
        // Arrange: the lump's size is the first directory entry's second int
        var wad = WadFile.Write([new("A", [1, 2, 3])]);
        var directory = BitConverter.ToInt32(wad, 8);
        BitConverter.GetBytes(1000).CopyTo(wad, directory + 4);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => WadFile.Read(wad));
    }
}
