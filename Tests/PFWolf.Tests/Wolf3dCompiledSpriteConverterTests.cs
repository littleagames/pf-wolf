using PFWolf.Loaders;

namespace PFWolf.Tests;

public class Wolf3dCompiledSpriteConverterTests
{
    // Sprite 8 of Trench Warfare's FOXTROT8.WL6: columns 26-41, so a 16-entry
    // offset table, one opaque pixel (0xDD) at row 63 of the first column only
    private static readonly byte[] NarrowSprite =
    [
        0x1A, 0x00, 0x29, 0x00,
        0x25, 0x00, 0x25, 0x00, 0x25, 0x00, 0x2A, 0x00, 0x2A, 0x00, 0x2A, 0x00, 0x2A, 0x00, 0x2A, 0x00,
        0x2A, 0x00, 0x2A, 0x00, 0x2A, 0x00, 0x2A, 0x00, 0x25, 0x00, 0x2A, 0x00, 0x2A, 0x00, 0x25, 0x00,
        0xDD, 0x80, 0x00, 0xE5, 0xFF, 0x7E, 0x00, 0x00, 0x00,
    ];

    [Test]
    public void Convert_Reads_A_Sprite_Shorter_Than_A_Full_Offset_Table()
    {
        // Arrange
        Assert.That(NarrowSprite, Has.Length.EqualTo(45));

        // Act
        var sprite = Wolf3dCompiledSpriteConverter.Convert(NarrowSprite);

        // Assert: columns pointing at 0x25 draw row 63; those at 0x2A draw nothing
        int[] drawnColumns = [26, 27, 28, 38, 41];
        for (int column = 0; column < 64; column++)
        {
            int index = 63 * 64 + column;
            bool drawn = drawnColumns.Contains(column);
            Assert.That(sprite.OpacityMask[index], Is.EqualTo(drawn ? 1 : 0), $"column {column}");
            if (drawn)
                Assert.That(sprite.RawData[index], Is.EqualTo(0xDD), $"column {column}");
        }
        Assert.That(sprite.OpacityMask.Count(m => m != 0), Is.EqualTo(drawnColumns.Length));
    }
}
