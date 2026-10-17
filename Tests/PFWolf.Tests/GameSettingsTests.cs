using PFWolf.Configuration;

namespace PFWolf.Tests;

public class GameSettingsTests
{
    [Test]
    public void A_New_Game_Autosaves_With_Wolf3Ds_High_Scores()
    {
        // Act
        var settings = new GameSettings();

        // Assert
        Assert.That(settings.autosaveEnabled, Is.True);
        Assert.That(settings.Scores, Is.EqualTo(HighScore.WolfDefaults()).Using<HighScore>((a, b) => a.Name == b.Name && a.Score == b.Score));
    }

    [Test]
    public void Saving_Keeps_The_Autosave_Setting_In_The_Settings()
    {
        // Arrange
        var settings = new GameSettings();
        var saves = new GameSaves(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            null!, null!, settings, new GameSession(), null!);

        // Act
        saves.autosaveEnabled = false;

        // Assert
        Assert.That(settings.autosaveEnabled, Is.False);
    }
}
