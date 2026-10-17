using PFWolf.Assets;

namespace PFWolf.Tests;

public class PlayScreenTests
{
    private static PlayScreen Make(VideoManager? video, int viewsize) =>
        new(null!, null!, video!, null!, null!, null!, null!, null!,
            new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.noshade), null!,
            new Configuration.GameSettings { viewsize = viewsize }, new GameSession { ingame = true }, new PlayScreenHooks(_ => { }));

    private static VideoManager Video(double uiScale) =>
        new(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test"))) { uiScale = uiScale };

    [TestCase(1.0, 1)]
    [TestCase(3.0, 3)]
    [TestCase(2.5, 2)]
    [TestCase(0.5, 1)]      // never thinner than a pixel
    public void The_Border_Lines_Are_A_Layout_Pixel_Thick(double uiScale, int pixels)
    {
        Assert.That(Make(Video(uiScale), 15).BorderLinePixels, Is.EqualTo(pixels));
    }

    [Test]
    public void A_Full_Screen_View_Has_No_Border_Sides_To_Draw()
    {
        // No video at all: drawing anything would throw
        Assert.DoesNotThrow(() => Make(null, 21).DrawPlayBorderSides());
    }
}
