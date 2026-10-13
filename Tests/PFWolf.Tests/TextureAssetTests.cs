using PFWolf.Assets;

namespace PFWolf.Tests;

public class TextureAssetTests
{
    [Test]
    public void A_Picture_With_Clear_Pixels_Makes_A_Masked_Texture()
    {
        // Arrange: a 2x2 picture, its top right pixel clear
        var picture = new GraphicAsset([1, 2, 3, 4], 2, 2) { OpacityMask = [1, 0, 1, 1] };

        // Act
        var texture = TextureAsset.FromGraphic(picture);

        // Assert: column by column, the clear pixel is 0xFF
        Assert.That(texture.Masked, Is.True);
        Assert.That(texture.RawData, Is.EqualTo(new byte[] { 1, 3, 0xff, 4 }));
    }

    [Test]
    public void A_Picture_With_No_Clear_Pixels_Makes_An_Opaque_Texture()
    {
        Assert.That(TextureAsset.FromGraphic(new GraphicAsset([1, 2, 3, 4], 2, 2)).Masked, Is.False);
        Assert.That(TextureAsset.FromGraphic(new GraphicAsset([1, 2, 3, 4], 2, 2) { OpacityMask = [1, 1, 1, 1] }).Masked, Is.False);
    }
}
