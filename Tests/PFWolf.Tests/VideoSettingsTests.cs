using PFWolf.Configuration;

namespace PFWolf.Tests;

public class VideoSettingsTests
{
    [TestCase(2.0, "2")]
    [TestCase(2.5, "2.5")]
    [TestCase(3.375, "3.38")]
    [TestCase(1.10, "1.1")]
    public void FormatScale_Shows_Up_To_Two_Decimals(double scale, string expected)
    {
        Assert.That(VideoSettings.FormatScale(scale), Is.EqualTo(expected));
    }

    [Test]
    public void Render_Size_Defaults_To_Base_Times_Scale()
    {
        // Act
        var settings = new VideoSettings { RenderScale = 3 };

        // Assert
        Assert.That((settings.RenderWidth, settings.RenderHeight), Is.EqualTo((960, 600)));
    }

    [Test]
    public void Render_Size_Overrides_The_Scale()
    {
        // Act
        var settings = new VideoSettings { RenderScale = 3, RenderSize = (1280, 720) };

        // Assert
        Assert.That((settings.RenderWidth, settings.RenderHeight), Is.EqualTo((1280, 720)));
    }

    [Test]
    public void Auto_Ui_Scale_Is_The_Largest_Whole_Number_That_Fits()
    {
        // Arrange: 1280x720 fits 4x across but only 3.6x down
        var settings = new VideoSettings { RenderSize = (1280, 720), UiScale = 0 };

        // Act / Assert
        Assert.That(settings.MaxUiScale, Is.EqualTo(3.6).Within(1e-9));
        Assert.That(settings.EffectiveUiScale, Is.EqualTo(3));
    }

    [TestCase(2.5, 2.5)]
    [TestCase(10, 3.6)]
    [TestCase(0.5, 1)]
    public void Set_Ui_Scale_Is_Clamped_To_What_Fits(double uiScale, double expected)
    {
        // Arrange
        var settings = new VideoSettings { RenderSize = (1280, 720), UiScale = uiScale };

        // Act / Assert
        Assert.That(settings.EffectiveUiScale, Is.EqualTo(expected).Within(1e-9));
    }

    [Test]
    public void Aspect_Correction_Stretches_The_Height_Six_Fifths()
    {
        // Act
        var settings = new VideoSettings { RenderScale = 1, AspectCorrect = true };

        // Assert
        Assert.That((settings.DisplayWidth, settings.DisplayHeight), Is.EqualTo((320, 240)));
    }
}
