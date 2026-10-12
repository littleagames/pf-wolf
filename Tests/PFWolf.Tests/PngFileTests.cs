using PFWolf.Assets;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>The editor's PNG writer: palette pictures with see-through pixels and a grAb offset, as the game reads them</summary>
public class PngFileTests
{
    // A palette of 256 different greys and reds
    private static PaletteColor[] Palette()
        => Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)(255 - i), (byte)(i / 2))).ToArray();

    [Test]
    public void A_Picture_Reads_Back_In_Its_Colors_With_Its_See_Through_Pixels_Clear()
    {
        // Arrange: 3x2, the middle of the top row see-through, one pixel of index 255
        var palette = Palette();
        var picture = new IndexedPicture(3, 2, [1, 2, 3, 4, 5, 255], [true, false, true, true, true, true]);

        // Act
        var png = PngFile.Write(picture, palette);
        var image = ImageDecoder.Decode(png);

        // Assert
        Assert.That((image.Width, image.Height), Is.EqualTo((3, 2)));
        for (int i = 0; i < 6; i++)
        {
            var (r, g, b, a) = image[i % 3, i / 3];
            if (i == 1)
            {
                Assert.That(a, Is.EqualTo(0), "see-through");
                continue;
            }
            var color = palette[picture.Indices[i]];
            Assert.That((r, g, b, a), Is.EqualTo((color.Red, color.Green, color.Blue, (byte)255)), $"pixel {i}");
        }
    }

    [Test]
    public void A_Picture_Using_Every_Color_With_See_Through_Pixels_Is_Written_As_Rgba()
    {
        // Arrange: 257 pixels, every index once and one see-through
        var palette = Palette();
        var indices = Enumerable.Range(0, 257).Select(i => (byte)(i % 256)).ToArray();
        var opaque = Enumerable.Range(0, 257).Select(i => i < 256).ToArray();
        var picture = new IndexedPicture(257, 1, indices, opaque);

        // Act
        var image = ImageDecoder.Decode(PngFile.Write(picture, palette));

        // Assert
        Assert.That(image[200, 0], Is.EqualTo((palette[200].Red, palette[200].Green, palette[200].Blue, (byte)255)));
        Assert.That(image[256, 0].A, Is.EqualTo(0));
    }

    [Test]
    public void The_Offset_Is_Written_Read_And_Replaced()
    {
        // Arrange
        var png = PngFile.Write(new IndexedPicture(1, 1, [7], null), Palette(), (12, -34));

        // Act
        var moved = PngFile.WithOffset(png, -5, 60);

        // Assert
        Assert.That(PngFile.ReadOffset(png), Is.EqualTo((12, -34)));
        Assert.That(PngFile.ReadOffset(moved), Is.EqualTo((-5, 60)));
        Assert.That(PngFile.ReadOffset(PngFile.Write(new IndexedPicture(1, 1, [7], null), Palette())), Is.Null);
        Assert.That(ImageDecoder.Decode(moved)[0, 0], Is.EqualTo(ImageDecoder.Decode(png)[0, 0]), "the picture's the same");
    }
}
