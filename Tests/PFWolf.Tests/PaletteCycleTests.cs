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
}
