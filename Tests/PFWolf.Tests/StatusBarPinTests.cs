using PFWolf.Assets;

namespace PFWolf.Tests;

/// <summary>Status bar parts pinned to a wide screen's edges (statusbar.yaml pin, background pin-edges)</summary>
public class StatusBarPinTests
{
    private static VideoManager WideScreen() =>
        new(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")))
        {
            ScreenWidth = 428, ScreenHeight = 200,      // 54 pixels each side of the 320 layout
        };

    private static StatusBarDrawing Bar(bool pinEdges) =>
        new(null!, null, null!, null!, null!, null!, null!, null!, new GameSession(),
            new Configuration.GameSettings(), new StatusBarHooks(map => map, (text, _, _) => [text]))
        {
            StatusBar = new StatusBarAsset(new Dictionary<string, StatusBarElement>
            {
                ["background"] = new() { Height = 40, Overlay = true, PinEdges = pinEdges },
            }),
        };

    [TestCase("Center", 54)]
    [TestCase("Left", 0)]
    [TestCase("Right", 108)]
    public void A_Side_Puts_The_Layout_Flush_With_That_Edge(string side, int left)
    {
        // Arrange
        var video = WideScreen();
        int before = video.ScreenX(0);

        // Act / Assert: only until the scope ends
        using (video.UseUiOrigin(UiAnchor.Bottom, Enum.Parse<UiSide>(side)))
            Assert.That(video.ScreenX(0), Is.EqualTo(left));
        Assert.That(video.ScreenX(0), Is.EqualTo(before));
    }

    [TestCase(false, null, 10, "Center")]
    [TestCase(false, "right", 10, "Right")]
    [TestCase(true, null, 159, "Left")]
    [TestCase(true, null, 160, "Right")]
    [TestCase(true, "center", 10, "Center")]
    [TestCase(true, "Left", 300, "Left")]
    public void A_Part_Takes_Its_Own_Pin_Else_The_Nearer_Edge_With_Pin_Edges(bool pinEdges, string? pin, int x, string side)
    {
        Assert.That(Bar(pinEdges).PinSide(new StatusBarElement { X = x, Pin = pin }), Is.EqualTo(Enum.Parse<UiSide>(side)));
    }
}
