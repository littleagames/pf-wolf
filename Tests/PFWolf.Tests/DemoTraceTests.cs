using PFWolf.Assets.Demos;

namespace PFWolf.Tests;

public class DemoTraceTests
{
    // Three frames: a guard shot dead, a clip picked up, a door opening, the player hurt
    private const string Sample = """
        pfwolf-demotrace 1
        demo 0 map MAP01 frames 3
        f 0 4
        p 131072 196608 90 100 0 Pistol
        a 0 Guard 327680 196608 180 25 Stand GARDA
        a 1 Clip 393216 393216 0 0 Spawn CLIPA
        f 1 4
        p 163840 196608 90 92 0 Pistol
        a 0 Guard 327680 196608 180 0 Die GARDK
        d 4 3 1 1024
        some-later-line 1 2 3
        f 2 4
        p 163840 229376 90 92 100 Pistol
        r 1
        d 4 3 1 4096
        t 7 7 floor 0
        w 7 7 1 12
        end out-of-data demotest 0: data 13/13
        """;

    private static DemoTrace Read(string text) => DemoTrace.Read(new StringReader(text));

    [Test]
    public void The_Player_Is_In_Tiles_Every_Frame()
    {
        // Act
        var trace = Read(Sample);

        // Assert
        Assert.That((trace.Label, trace.MapName, trace.FrameCount), Is.EqualTo(("0", "MAP01", 3)));
        Assert.That(trace.Player[0], Is.EqualTo(new TracePlayer(2, 3, 90, 100, 0, "Pistol")));
        Assert.That((trace.Player[2].X, trace.Player[2].Y, trace.Player[2].Score), Is.EqualTo((2.5, 3.5, 100)));
        Assert.That(trace.EndedHow, Is.EqualTo("out-of-data"));
        Assert.That(trace.Report, Is.EqualTo("demotest 0: data 13/13"));
    }

    [Test]
    public void An_Actor_Stands_As_It_Last_Changed_Until_It_Leaves()
    {
        // Act
        var trace = Read(Sample);

        // Assert: the clip's gone on frame 2; the guard keeps frame 1's state
        Assert.That(trace.ActorsAt(1).Select(a => a.Class), Is.EquivalentTo(new[] { "Guard", "Clip" }));
        var atTwo = trace.ActorsAt(2).ToList();
        Assert.That(atTwo.Select(a => a.Class), Is.EqualTo(new[] { "Guard" }));
        Assert.That(atTwo[0].State, Is.EqualTo(new TraceActorState(5, 3, 180, 0, "Die", "GARDK")));
        Assert.That(DemoTrace.At(trace.Doors[new TraceDoor(4, 3, true)], 0), Is.Null);
        Assert.That(DemoTrace.At(trace.Doors[new TraceDoor(4, 3, true)], 5), Is.EqualTo(4096));
        Assert.That(trace.Tiles[(7, 7)][0], Is.EqualTo(new TraceChange<TraceTile>(2, new TraceTile(TraceTileKind.Floor, 0))));
        Assert.That(trace.PushWalls[2], Is.EqualTo(new[] { new TracePushWall(7, 7, 1, 12) }));
    }

    [Test]
    public void Kills_Pickups_Hurts_And_Doors_Are_Events()
    {
        // Act
        var trace = Read(Sample);

        // Assert
        Assert.That(trace.Events.Select(e => (e.Frame, e.Kind)), Is.EquivalentTo(new[]
        {
            (1, TraceEventKind.Kill), (1, TraceEventKind.Hurt), (1, TraceEventKind.Door),
            (2, TraceEventKind.Pickup), (2, TraceEventKind.PushWall), (2, TraceEventKind.End),
        }));
        Assert.That(trace.Events.Single(e => e.Kind == TraceEventKind.Kill).Text, Is.EqualTo("Killed Guard"));
        Assert.That(trace.Events.Select(e => e.Frame), Is.Ordered);
    }

    [Test]
    public void Something_Else_Isnt_A_Trace()
    {
        // Act / Assert
        Assert.Throws<InvalidDataException>(() => Read("hello\n"));
    }
}
