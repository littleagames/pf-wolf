using PFWolf.Assets;

namespace PFWolf.Tests;

public class PlayScreenTests
{
    private static PlayScreen Make(VideoManager? video, int viewsize) =>
        new(null!, null!, video!, null!, null!, null!, null!, null!,
            new RenderState(new TextureAsset { RawData = new byte[64 * 64] }, Lighting.NoShade), null!,
            new Configuration.GameSettings { ViewSize = viewsize }, new GameSession { InGame = true }, new PlayScreenHooks(_ => { }));

    private static VideoManager Video(double uiScale) =>
        new(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test"))) { UiScale = uiScale };

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

    [TestCase(null, 70)]        // Wolf3D's second
    [TestCase(210, 210)]        // Midnight Assault's three
    [TestCase(0, 70)]
    public void The_Get_Psyched_Screen_Holds_For_Game_Infos_Tics(int? tics, int held)
    {
        Assert.That(PlayScreen.LevelStartTics(new GameInfoAsset { LevelStartTics = tics }), Is.EqualTo(held));
    }

    [Test]
    public void The_Level_Start_Settings_Read_From_Game_Info()
    {
        // Act
        var gameInfo = Loaders.YamlDataEntryLoader.Deserialize<GameInfoAsset>("""
            level-start-text:
              - { text: "$STR_LEVEL_PRELOAD_ENTERING", font: SmallFont, y: 86 }
              - { text: "%m", font: LargeFont, y: 96 }
            level-start-tics: 210
            """);

        // Assert
        Assert.That(gameInfo.LevelStartTics, Is.EqualTo(210));
        Assert.That(gameInfo.LevelStartText.Select(l => (l.Text, l.Y)), Is.EqualTo(new[] { ("$STR_LEVEL_PRELOAD_ENTERING", (int?)86), ("%m", 96) }));
    }
}
