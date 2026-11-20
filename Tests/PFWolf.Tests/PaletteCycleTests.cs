using PFWolf.Assets;

namespace PFWolf.Tests;

/// <summary>game-info palette-cycles: Blake Stone's CycleColors (Managers/VideoManager.cs CyclePalette)</summary>
public class PaletteCycleTests
{
    private static readonly List<PaletteCycleInfo> Cycles =
    [
        new() { First = 1, Last = 2, Tics = 7 },
        new() { First = 3, Last = 5, Tics = 10 },
    ];

    [Test]
    public void The_First_Call_Moves_Every_Run_Its_Last_Color_Coming_Round_To_Its_First()
    {
        // Arrange
        int[] palette = [0, 1, 2, 3, 4, 5, 6];
        var countdowns = new int[Cycles.Count];

        // Act
        bool changed = VideoManager.CyclePalette(palette, Cycles, countdowns, tics: 1);

        // Assert
        Assert.That(changed, Is.True);
        Assert.That(palette, Is.EqualTo(new[] { 0, 2, 1, 5, 3, 4, 6 }));
        Assert.That(countdowns, Is.EqualTo(new[] { 7, 10 }));
    }

    [Test]
    public void A_Run_Then_Waits_Its_Tics_Before_Moving_Again()
    {
        // Arrange: both runs just moved
        int[] palette = [0, 1, 2, 3, 4, 5, 6];
        var countdowns = new int[Cycles.Count];
        VideoManager.CyclePalette(palette, Cycles, countdowns, tics: 1);

        // Act: a tic at a time, until the first run's 7 have gone by
        var moves = Enumerable.Range(0, 7).Select(_ => VideoManager.CyclePalette(palette, Cycles, countdowns, tics: 1)).ToList();

        // Assert: only the 7th moves it (back as it was), and the slower run hasn't moved
        Assert.That(moves, Is.EqualTo(new[] { false, false, false, false, false, false, true }));
        Assert.That(palette, Is.EqualTo(new[] { 0, 1, 2, 5, 3, 4, 6 }));
    }

    [Test]
    public void No_Tics_Moves_Nothing_Once_Counting()
    {
        // Arrange
        int[] palette = [0, 1, 2, 3, 4, 5, 6];
        int[] countdowns = [3, 3];

        // Act
        bool changed = VideoManager.CyclePalette(palette, Cycles, countdowns, tics: 0);

        // Assert
        Assert.That(changed, Is.False);
        Assert.That(palette, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6 }));
    }

    [Test]
    public void Light_Rows_Never_Match_A_Shaded_Color_To_A_Cycling_Index()
    {
        // Arrange: Blake Stone's 244 (dark green, cycles with bright green 245) is the nearest
        // match for a faded bright green; 100 is a slightly worse dark green that stays put
        var palette = Enumerable.Range(0, 256).Select(_ => new PaletteColor(255, 0, 255)).ToArray();
        palette[0] = new PaletteColor(0, 0, 0);
        palette[100] = new PaletteColor(0, 40, 0);
        palette[200] = new PaletteColor(0, 250, 0);
        palette[244] = new PaletteColor(0, 48, 0);
        palette[245] = new PaletteColor(0, 250, 0);
        List<PaletteCycleInfo> cycles = [new() { First = 244, Last = 245, Tics = 30 }];
        const int Steps = 32, FadedStep = 25;

        // Act
        var plain = new Shading.LightRowBuilder(palette).Row(31, 32, Steps, 0, 0, 0);
        var skipping = new Shading.LightRowBuilder(palette, PaletteCycleInfo.IndicesOf(cycles)).Row(31, 32, Steps, 0, 0, 0);

        // Assert: without the skip the far green lands on 244; with it, on 100
        Assert.That(plain[FadedStep * 256 + 200], Is.EqualTo(244));
        Assert.That(skipping[FadedStep * 256 + 200], Is.EqualTo(100));
        Assert.That(skipping[245], Is.EqualTo(245), "step 0 at full light keeps every index");
    }
}
