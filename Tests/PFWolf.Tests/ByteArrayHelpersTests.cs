namespace PFWolf.Tests;

public class ByteArrayHelpersTests
{
    [Test]
    public void Flatten_Then_ToFixedArray_Round_Trips()
    {
        // Arrange
        var grid = new byte[,]
        {
            { 1, 2, 3 },
            { 4, 5, 6 },
        };

        // Act
        var flat = grid.Flatten();
        var restored = flat.ToFixedArray(2, 3);

        // Assert
        Assert.That(flat, Is.EqualTo(new byte[] { 1, 2, 3, 4, 5, 6 }));
        Assert.That(restored, Is.EqualTo(grid));
    }

    [Test]
    public void Write_Matches_Flatten()
    {
        // Arrange
        var grid = new byte[,]
        {
            { 9, 8 },
            { 7, 6 },
        };
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        // Act
        writer.Write(grid);
        writer.Flush();

        // Assert
        Assert.That(stream.ToArray(), Is.EqualTo(grid.Flatten()));
    }

    [Test]
    public void ReadCount_Returns_Count_That_Fits()
    {
        // Arrange: a count of 3 followed by 3 bytes of payload
        using var reader = CreateReader(3, payloadLength: 3);

        // Act
        var count = reader.ReadCount();

        // Assert
        Assert.That(count, Is.EqualTo(3));
    }

    [TestCase(-1)]
    [TestCase(4)]
    [TestCase(int.MaxValue)]
    public void ReadCount_Rejects_Count_That_Cannot_Fit(int badCount)
    {
        // Arrange
        using var reader = CreateReader(badCount, payloadLength: 3);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => reader.ReadCount());
    }

    private static BinaryReader CreateReader(int count, int payloadLength)
    {
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(count);
            writer.Write(new byte[payloadLength]);
        }
        stream.Position = 0;
        return new BinaryReader(stream);
    }
}
