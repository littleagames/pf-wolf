using PFWolf.Assets;

namespace PFWolf.Tests;

public class ViewSetupTests
{
    private RenderState _render = null!;
    private Configuration.GameSettings _settings = null!;
    private ViewSetup _setup = null!;

    [SetUp]
    public void Make()
    {
        var video = new VideoManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")))
        {
            screenWidth = 1920, screenHeight = 1080,
        };
        _render = new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.noshade)
        {
            pixelangle = new short[1920],
        };
        _settings = new Configuration.GameSettings { viewsize = 21 };
        // No status bar here (no assets): the play area is the whole screen
        var bar = new StatusBarDrawing(null!, null, null!, null!, null!, null!, null!, null!, new GameSession(),
            new StatusBarHooks(() => false, map => map, (text, _, _) => [text]));
        _setup = new ViewSetup(video, null!, _render, new PlayArea(video, bar), _settings);
    }

    [Test]
    public void A_Full_Screen_View_On_A_Classic_Screen_Is_Its_Own_Width()
    {
        // 320 wide over 200 high: the classic shape, so the width is the view's own
        Assert.That(_setup.ProjectionWidth(320, 200), Is.EqualTo(320));
    }

    [Test]
    public void A_Wider_Screen_Sees_More_To_The_Sides_Not_Taller_Walls()
    {
        // 16:9 is wider than 320x200's 16:10: the field is worked out from the height (Hor+)
        int width = _setup.ProjectionWidth(1920, 1080);
        Assert.That(width, Is.LessThan(1920));
        Assert.That(width, Is.EqualTo((int)Math.Ceiling(1081 * 1.6 - 1e-9)));
    }

    [Test]
    public void The_Whole_Screen_View_Fills_It_With_The_Horizon_In_The_Middle()
    {
        // Act
        _setup.NewViewSize(21);

        // Assert
        Assert.That((_render.viewwidth, _render.viewheight), Is.EqualTo((1920, 1080)));
        Assert.That((_render.viewscreenx, _render.viewscreeny, _render.screenofs), Is.EqualTo((0, 0, 0u)));
        Assert.That((_render.centerx, _render.centery), Is.EqualTo(((short)959, (short)540)));
        Assert.That(_render.shootdelta, Is.EqualTo(_render.projectionwidth / 10));
    }

    [Test]
    public void The_Rays_Fan_Out_Evenly_Either_Side_Of_The_Middle()
    {
        _setup.NewViewSize(21);

        // The middle two columns straddle the view angle; the edges are furthest out, the same either side
        Assert.That(_render.pixelangle[959], Is.EqualTo(-_render.pixelangle[960]));
        Assert.That(_render.pixelangle[0], Is.EqualTo(-_render.pixelangle[1919]));
        Assert.That(_render.pixelangle[0], Is.GreaterThan(_render.pixelangle[959]));
    }

    [Test]
    public void View_Widths_Are_Whole_Sixteens_And_Heights_Even()
    {
        _setup.SetViewSize(1000, 601);
        Assert.That((_render.viewwidth, _render.viewheight), Is.EqualTo((992, 600)));
    }
}