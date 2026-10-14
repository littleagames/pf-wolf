using PFWolf.Configuration;

namespace PFWolf.Tests;

public class VideoMenuTests
{
    private static readonly (int, int) FullHd = (1920, 1080);

    [TestCase(false, 200)]
    [TestCase(true, 240)]
    public void A_Window_Is_320_Across_For_200_Or_240_Down(bool aspectCorrect, int height)
    {
        Assert.That(VideoMenu.WindowBaseHeight(aspectCorrect), Is.EqualTo(height));
    }

    [Test]
    public void Window_Sizes_Are_The_Multiples_That_Fit_The_Desktop()
    {
        // Act
        var sizes = VideoMenu.WindowSizes(new VideoSettings(), FullHd);

        // Assert
        Assert.That(sizes, Is.EqualTo(new[] { (320, 200), (640, 400), (960, 600), (1280, 800), (1600, 1000) }));
    }

    [Test]
    public void A_Window_Size_Set_Elsewhere_Is_Kept_In_Its_Place()
    {
        // Act
        var sizes = VideoMenu.WindowSizes(new VideoSettings { WindowWidth = 1000, WindowHeight = 700 }, FullHd);

        // Assert
        Assert.That(sizes, Does.Contain((1000, 700)));
        Assert.That(sizes, Is.Ordered.By("Item1"));
    }

    [Test]
    public void An_Auto_Resolution_Offers_Common_Monitor_Sizes_That_Fit()
    {
        // Act
        var sizes = VideoMenu.WindowSizes(new VideoSettings { MatchWindow = true }, FullHd);

        // Assert
        Assert.That(sizes, Does.Contain((1920, 1080)).And.Contain((1366, 768)));
        Assert.That(sizes, Does.Not.Contain((2560, 1440)));
    }

    [Test]
    public void Render_Scales_Go_Up_To_What_The_Display_Shows()
    {
        // 1920 / 320 = 6 across, 1080 / 200 = 5 down
        Assert.That(VideoMenu.RenderScales(new VideoSettings(), FullHd), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
    }

    [Test]
    public void A_Render_Scale_Past_The_Display_Is_Still_Offered()
    {
        Assert.That(VideoMenu.RenderScales(new VideoSettings { RenderScale = 7 }, FullHd), Has.Count.EqualTo(7));
    }

    [Test]
    public void Ui_Scales_Are_Auto_Then_Quarters_Then_The_Most_That_Fits()
    {
        // Arrange: 1280x720 fits 3.6 times down
        var settings = new VideoSettings { RenderSize = (1280, 720) };

        // Act
        var scales = VideoMenu.UiScales(settings);

        // Assert
        Assert.That(scales.Take(3), Is.EqualTo(new[] { 0, 1, 1.25 }));
        Assert.That(scales.TakeLast(2), Is.EqualTo(new[] { 3.5, 3.6 }));
    }

    [Test]
    public void A_Ui_Scale_Set_In_The_Console_Is_Kept()
    {
        Assert.That(VideoMenu.UiScales(new VideoSettings { RenderSize = (1280, 720), UiScale = 2.1 }), Does.Contain(2.1));
    }

    [Test]
    public void Aspect_Reshapes_A_Standard_Window_To_Fill_It()
    {
        // Act
        var next = VideoMenu.WithAspect(new VideoSettings(), aspectCorrect: true, FullHd);

        // Assert: 640x400 becomes 640x480
        Assert.That((next.AspectCorrect, next.WindowWidth, next.WindowHeight), Is.EqualTo((true, 640, 480)));
    }

    [Test]
    public void Aspect_Shrinks_A_Window_That_Wouldnt_Fit()
    {
        // Act: 1280x800 would be 1280x960, taller than the desktop
        var next = VideoMenu.WithAspect(new VideoSettings { WindowWidth = 1280, WindowHeight = 800 }, aspectCorrect: true, (1366, 768));

        // Assert
        Assert.That((next.WindowWidth, next.WindowHeight), Is.EqualTo((960, 720)));
    }

    [Test]
    public void Aspect_Leaves_Other_Windows_And_Auto_Their_Size()
    {
        // Arrange
        var odd = new VideoSettings { WindowWidth = 1000, WindowHeight = 700 };
        var auto = new VideoSettings { MatchWindow = true };

        // Act / Assert
        Assert.That(VideoMenu.WithAspect(odd, true, FullHd) with { AspectCorrect = false }, Is.EqualTo(odd));
        Assert.That(VideoMenu.WithAspect(auto, true, FullHd) with { AspectCorrect = false }, Is.EqualTo(auto));
    }

    [Test]
    public void Changes_That_Could_Leave_The_Screen_Unreadable_Ask_To_Be_Kept()
    {
        // Arrange
        var current = new VideoSettings();

        // Act / Assert
        Assert.That(VideoMenu.NeedsVideoConfirm(current, current with { Fullscreen = true }), Is.True);
        Assert.That(VideoMenu.NeedsVideoConfirm(current, current with { RenderScale = 3 }), Is.True);
        Assert.That(VideoMenu.NeedsVideoConfirm(current, current with { VSync = false }), Is.True);
        Assert.That(VideoMenu.NeedsVideoConfirm(current, current with { WindowWidth = 960, WindowHeight = 600 }), Is.True);
        Assert.That(VideoMenu.NeedsVideoConfirm(current, current with { Filter = ScaleFilter.Linear }), Is.False);
        Assert.That(VideoMenu.NeedsVideoConfirm(current, current with { UiScale = 2 }), Is.False);
    }

    [Test]
    public void A_Fullscreen_Windows_Size_Doesnt_Matter()
    {
        // Arrange
        var fullscreen = new VideoSettings { Fullscreen = true };

        // Act / Assert
        Assert.That(VideoMenu.NeedsVideoConfirm(fullscreen, fullscreen with { WindowWidth = 960, WindowHeight = 600 }), Is.False);
    }

    [TestCase(ScaleFilter.Nearest, "$STR_NEAREST")]
    [TestCase(ScaleFilter.Linear, "$STR_LINEAR")]
    public void Filters_Are_Named_By_Language_Key(object filter, string key)
    {
        Assert.That(VideoMenu.FilterName((ScaleFilter)filter), Is.EqualTo(key));
    }
}
