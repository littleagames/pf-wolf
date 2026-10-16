using PFWolf.Assets;

namespace PFWolf.Tests;

public class StatusBarOverlayTests
{
    private sealed class Rig
    {
        public readonly GameSession Session = new();
        public readonly StatusBarDrawing Bar;
        public readonly PlayArea Area;
        public readonly ViewSetup View;
        public bool FullScreen;

        public Rig(bool overlay)
        {
            Bar = new StatusBarDrawing(null!, null, null!, null!, null!, null!, null!, null!, Session,
                new StatusBarHooks(() => FullScreen, map => map, (text, _, _) => [text]));
            Bar.StatusBar = new StatusBarAsset(new Dictionary<string, StatusBarElement>
            {
                ["background"] = new() { Height = 40, Overlay = overlay },
            });
            Area = new PlayArea(null!, Bar);
            View = new ViewSetup(null!, null!, null!, Area, new ViewSetupHooks(() => 19, _ => { }));
        }
    }

    [Test]
    public void A_Bar_Of_Its_Own_Takes_Its_Lines_From_The_Play_Area()
    {
        Assert.That(new Rig(overlay: false).Area.StatusLines, Is.EqualTo(40));
    }

    [Test]
    public void A_Bar_Over_The_View_Takes_None()
    {
        var rig = new Rig(overlay: true);
        Assert.That(rig.Area.StatusLines, Is.Zero);
        Assert.That(rig.Bar.StatusBar.Get("background")!.Height, Is.EqualTo(40));      // still where its parts are measured from
    }

    [Test]
    public void A_Bar_Over_The_View_Is_Drawn_Only_With_The_Frame()
    {
        // Arrange
        var rig = new Rig(overlay: true);

        // Act / Assert: not when something changes, only in the frame's pass
        Assert.That(rig.Bar.StatusBarHidden, Is.True);
        rig.Bar.DrawingOverlay = true;
        Assert.That(rig.Bar.StatusBarHidden, Is.False);
        rig.FullScreen = true;
        Assert.That(rig.Bar.StatusBarHidden, Is.True);          // the whole screen hides it
    }

    [Test]
    public void A_Bar_Of_Its_Own_Is_Drawn_As_Things_Change()
    {
        Assert.That(new Rig(overlay: false).Bar.StatusBarHidden, Is.False);
    }

    [TestCase(false, 15, 15)]
    [TestCase(true, 15, 20)]     // no bordered views under a bar over the view
    [TestCase(true, 21, 21)]
    public void View_Sizes_Under_A_Bar_Over_The_View_Fill_The_Play_Area(bool overlay, int asked, int size)
    {
        Assert.That(new Rig(overlay).View.AllowedViewSize(asked), Is.EqualTo(size));
    }
}
