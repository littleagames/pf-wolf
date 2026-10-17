namespace PFWolf.Tests;

public class PlayAreaTests
{
    [Test]
    public void With_No_Status_Bars_The_Play_Area_Is_The_Whole_Screen()
    {
        // Arrange: no assets, so no status bar either way
        var video = new VideoManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")))
        {
            ScreenWidth = 320, ScreenHeight = 200,
        };
        var bar = new StatusBarDrawing(null!, null, null!, null!, null!, null!, null!, null!, new GameSession(),
            new Configuration.GameSettings(), new StatusBarHooks(map => map, (text, _, _) => [text]));

        // Act
        var area = new PlayArea(video, bar);

        // Assert
        Assert.That((area.StatusLines, area.TopLines), Is.EqualTo((0, 0)));
        Assert.That((area.PlayAreaTop, area.PlayAreaHeight), Is.EqualTo((0, 200)));
    }
}