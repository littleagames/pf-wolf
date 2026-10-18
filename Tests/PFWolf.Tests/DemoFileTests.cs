using PFWolf.Assets.Demos;

namespace PFWolf.Tests;

public class DemoFileTests
{
    [Test]
    public void A_Demo_Has_Its_Map_And_A_Frame_Every_Three_Bytes()
    {
        // Arrange: MAP03, two frames
        var data = HeadlessGame.ScriptedDemo(2,
            new HeadlessGame.DemoStep(HeadlessGame.Attack, -5, -100, 1),
            new HeadlessGame.DemoStep(HeadlessGame.Use | 4, 7, 50, 1));

        // Act
        var demo = DemoFile.Parse(data);

        // Assert
        Assert.That(demo.MapName, Is.EqualTo("MAP03"));
        Assert.That(demo.DeclaredLength, Is.EqualTo(10));
        Assert.That(demo.Frames, Is.EqualTo(new[] { new DemoFrame(1, -5, -100), new DemoFrame(12, 7, 50) }));
        Assert.That(demo.Problems, Is.Empty);
        Assert.That(demo.IsPlayable, Is.True);
    }

    [Test]
    public void A_Frame_Says_Which_Buttons_Are_Held()
    {
        // Arrange: fire, run and weapon slot 2
        var frame = new DemoFrame(0b0010_0101, 0, 0);

        // Assert
        Assert.That((frame.Attack, frame.Strafe, frame.Run, frame.Use), Is.EqualTo((true, false, true, false)));
        Assert.That(Enumerable.Range(1, 4).Select(frame.Slot), Is.EqualTo(new[] { false, true, false, false }));
    }

    [Test]
    public void A_Demo_Lasts_Four_Tics_A_Frame()
    {
        // Arrange: 35 frames is 140 tics, two seconds
        var demo = DemoFile.Parse(HeadlessGame.ScriptedDemo(0, new HeadlessGame.DemoStep(0, 0, 0, 35)));

        // Assert
        Assert.That(demo.Tics, Is.EqualTo(140));
        Assert.That(demo.Duration, Is.EqualTo(TimeSpan.FromSeconds(2)));
        Assert.That(DemoFile.TimeOf(35), Is.EqualTo(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void A_Length_Past_The_Data_Stops_At_Its_End()
    {
        // Arrange: says 100 bytes, has a header and a frame and a bit
        byte[] data = [0, 100, 0, 0, 1, 2, 3, 4];

        // Act
        var demo = DemoFile.Parse(data);

        // Assert: as the game plays it, the whole frame only
        Assert.That(demo.Frames, Has.Count.EqualTo(1));
        Assert.That(demo.Problems, Has.Some.Contains("only 8"));
        Assert.That(demo.Problems, Has.Some.Contains("left over"));
    }

    [Test]
    public void Too_Short_To_Be_A_Demo_Isnt_Playable()
    {
        // Act
        var demo = DemoFile.Parse([1, 2]);

        // Assert
        Assert.That(demo.IsPlayable, Is.False);
        Assert.That(demo.Frames, Is.Empty);
        Assert.That(demo.Problems, Has.Some.Contains("Too short"));
    }
}
