using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Rendering;

namespace PFWolf.Tests;

/// <summary>
/// The actor window's "seen from the player" preview against the game itself: the same
/// constants, the same height numerator, and the same fade step the game's Lighting gives a
/// sprite at each distance and lighten (docs/dev/lighting-in-common-plan.md phase 1).
/// </summary>
public class ShadePreviewTests
{
    private const long Tile = MapConstants.TileGlobal;

    // Blake Stone's default map: an inverse fade from 4.52 tiles, 80% at most
    private static readonly ShadingSettings Blake = new("#000000", 4.52, 16, 80, 255, InverseFade: true);
    private static readonly ShadingSettings Fog = new("#808080", 1, 12, 90, 140);

    private static PaletteColor[] GreyRamp() => [.. Enumerable.Range(0, 256).Select(i => new PaletteColor((byte)i, (byte)i, (byte)i))];

    [Test]
    public void Its_Constants_Are_The_Games()
    {
        Assert.That(SpriteShading.ActorSize, Is.EqualTo(GameConstants.ActorSize));
        Assert.That(SpriteShading.MinDist, Is.EqualTo(GameConstants.MinDist));
        Assert.That(SpriteShading.FocalLength, Is.EqualTo(GameConstants.FocalLength));
        Assert.That(SpriteShading.ViewGlobal, Is.EqualTo(GameConstants.ViewGlobal));
        Assert.That((ShadeMath.LightLevels, ShadeMath.FadeSteps), Is.EqualTo((Lighting.LightLevels, Lighting.FadeSteps)));
    }

    [Test]
    public void The_Height_Numerator_Is_The_Classic_Windows()
    {
        // Arrange: a 320x200 screen at the default view size, as the game sets it up
        var video = new VideoManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")))
        {
            ScreenWidth = 320, ScreenHeight = 200,
        };
        var render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.NoShade) { PixelAngle = new short[320] };
        var settings = new Configuration.GameSettings { ViewSize = 19 };
        var bar = new StatusBarDrawing(null!, null, null!, null!, null!, null!, null!, null!, new GameSession(),
            new Configuration.GameSettings(), new StatusBarHooks(map => map, (text, _, _) => [text]));
        var setup = new ViewSetup(video, null!, render, new PlayArea(video, bar), settings);

        // Act
        setup.NewViewSize(19);

        // Assert
        Assert.That(render.ProjectionWidth, Is.EqualTo(SpriteShading.ClassicProjectionWidth));
        Assert.That(ShadePreview.HeightNumerator, Is.EqualTo(render.HeightNumerator));
    }

    [TestCase(0)]
    [TestCase(6)]
    [TestCase(-2)]
    [TestCase(26)]
    public void Each_Distance_Gets_The_Step_The_Games_Lighting_Gives_It(int lighten)
    {
        foreach (var settings in new[] { Blake, Fog })
        {
            // Arrange: the game's Lighting for the classic window, shading this way
            var lighting = new Lighting(null!, LightingTests.GreyVideo(), new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets")), null!, new GameSession(), null),
                new LightingHooks(() => ShadePreview.HeightNumerator, () => { }, _ => { }));
            lighting.SetShading(settings);
            var preview = new ShadePreview();

            for (double tiles = 1; tiles <= 32; tiles += 0.125)
            {
                // Act
                var shade = preview.Shade(GreyRamp(), settings, tiles, inHand: false, bright: false, lighten);

                // Assert
                int height = SpriteShading.ViewHeight((long)Math.Round(tiles * Tile), ShadePreview.HeightNumerator);
                Assert.That(shade.Offset, Is.EqualTo(lighting.LightenedShadeOffset(height, lighten)), $"{settings} at {tiles} tiles");
                Assert.That(shade.Row, Is.EqualTo(lighting.LightRow), "lit by the level's light");
            }
        }
    }

    [Test]
    public void A_Sprite_Is_Measured_To_Its_Near_Side_And_Not_Drawn_Too_Close()
    {
        var preview = new ShadePreview();
        Assert.That(preview.Shade(GreyRamp(), Blake, 0.5, false, false, 0).Drawn, Is.False);
        // measured a quarter tile nearer than its middle: at 4.7 tiles it's inside fade-start (4.52), so unfaded
        Assert.That(preview.Shade(GreyRamp(), Blake, 4.7, false, false, 0).Step, Is.Zero);
        Assert.That(preview.Shade(GreyRamp(), Blake, 6, false, false, 0).Step, Is.GreaterThan(0));
    }

    [Test]
    public void Bright_Frames_Are_Unshaded_And_Weapons_In_Hand_Lit_But_Not_Faded()
    {
        var preview = new ShadePreview();

        var bright = preview.Shade(GreyRamp(), Fog, 20, inHand: false, bright: true, lighten: 0);
        var inHand = preview.Shade(GreyRamp(), Fog, 20, inHand: true, bright: false, lighten: 0);

        Assert.That((bright.Row, bright.Offset), Is.EqualTo(((byte[]?)null, 0)));
        Assert.That(inHand.Row, Is.Not.Null);
        Assert.That(inHand.Offset, Is.Zero);
        Assert.That(inHand.Row![200], Is.LessThan(200), "dimmed to light 140");
    }

    [Test]
    public void Without_Shading_The_Picture_Keeps_Its_Colors()
    {
        var preview = new ShadePreview();
        Assert.That(preview.Shade(GreyRamp(), null, 20, false, false, 6).Row, Is.Null);
        Assert.That(preview.Shade(GreyRamp(), new ShadingSettings("#000000", 0, 16, 0, 255), 20, false, false, 6).Row, Is.Null);
    }

    [Test]
    public void Its_Rows_Are_The_Games()
    {
        // Arrange
        var preview = new ShadePreview();
        var video = LightingTests.GreyVideo();

        // Act
        var shade = preview.Shade(GreyRamp(), Fog, 10, false, false, 0);

        // Assert: the game palette here is the same grey ramp
        Assert.That(shade.Row, Is.EqualTo(video.GetLightRow(ShadeMath.LightLevelOf(140), 32, 32, 0x80, 0x80, 0x80)));
    }

    [Test]
    public void Remapping_Leaves_See_Through_Pixels_See_Through()
    {
        var picture = new IndexedPicture(2, 1, [10, 20], [true, false]);
        var row = Enumerable.Range(0, 512).Select(i => (byte)(i / 2)).ToArray();

        var shaded = ShadePreview.Remap(picture, row, 256);

        Assert.That(shaded.Indices, Is.EqualTo(new byte[] { 133, 138 }));
        Assert.That(shaded.Opaque, Is.EqualTo(new[] { true, false }));
    }
}
