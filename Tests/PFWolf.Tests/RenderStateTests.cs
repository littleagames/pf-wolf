using PFWolf.Assets;

namespace PFWolf.Tests;

public class RenderStateTests
{
    [Test]
    public void A_Frame_Starts_One_Story_Tall_At_Eye_Height_With_Nothing_Hit()
    {
        // Arrange
        var notexture = new TextureAsset { RawData = new byte[64 * 64] };
        var noshade = new byte[256];

        // Act
        var state = new RenderState(notexture, noshade);

        // Assert
        Assert.That(state.PostSource, Is.SameAs(notexture));
        Assert.That(state.ArchLight, Is.SameAs(noshade));
        Assert.That((state.ViewEyeZ, state.HitTile), Is.EqualTo((GameConstants.EyeDefault, -1)));
        Assert.That(state.PostHeights, Has.Length.EqualTo(RenderState.MaxPosts));
        Assert.That(state.PostLight, Has.Length.EqualTo(RenderState.MaxPosts));
    }

    [Test]
    public void Program_Reads_And_Writes_The_Same_State()
    {
        // The renderer's old names are GameHost's forwards onto its one RenderState
        int before = GameHost.Render.ViewX;
        try
        {
            GameHost.Render.ViewX = 123;
            Assert.That(GameHost.ViewX, Is.EqualTo(123));
        }
        finally
        {
            GameHost.Render.ViewX = before;
        }
    }
}
