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
        Assert.That(state.postsource, Is.SameAs(notexture));
        Assert.That(state.archlight, Is.SameAs(noshade));
        Assert.That((state.wallstories, state.vieweyez, state.hittile), Is.EqualTo((1, Program.EYEDEFAULT, -1)));
        Assert.That(state.postheights, Has.Length.EqualTo(RenderState.MAXPOSTS));
        Assert.That(state.postlight, Has.Length.EqualTo(RenderState.MAXPOSTS));
    }

    [Test]
    public void Program_Reads_And_Writes_The_Same_State()
    {
        // The renderer's old names are Program's forwards onto its one RenderState
        int before = Program.render.viewx;
        try
        {
            Program.render.viewx = 123;
            Assert.That(Program.viewx, Is.EqualTo(123));
        }
        finally
        {
            Program.render.viewx = before;
        }
    }
}
