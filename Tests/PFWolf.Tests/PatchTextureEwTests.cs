using PFWolf.Assets;

namespace PFWolf.Tests;

public class PatchTextureEwTests
{
    [TestCase("north", "HOOD")]
    [TestCase("south", "HOOD")]
    [TestCase("east", "HOODEW")]
    [TestCase("West", "HOODEW")]
    public void A_Patch_With_An_East_West_Picture_Shows_It_On_Those_Faces(string face, string texture)
    {
        var patch = new MapPatchTranslation { Texture = "HOOD", TextureEw = "HOODEW" };
        Assert.That(patch.TextureFor(face), Is.EqualTo(texture));
    }

    [TestCase("north")]
    [TestCase("east")]
    public void Without_One_Every_Face_Shows_Its_Picture(string face)
    {
        Assert.That(new MapPatchTranslation { Texture = "SIGN" }.TextureFor(face), Is.EqualTo("SIGN"));
    }

    [Test]
    public void Mapdefs_Give_It_As_Texture_Ew()
    {
        // Act: as mapdefs are read
        var patch = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<MapPatchTranslation>("texture: Wall-184\ntexture-ew: Wall-185\n");

        // Assert
        Assert.That((patch.Texture, patch.TextureEw), Is.EqualTo(("Wall-184", "Wall-185")));
    }
}
