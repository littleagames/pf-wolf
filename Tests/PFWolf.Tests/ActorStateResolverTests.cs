using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class ActorStateResolverTests
{
    private static Dictionary<string, ActorStateFrame> Resolve(string statesYaml)
    {
        var actors = ActorDefs.Parse("Thing:\n  states:\n" + statesYaml);
        return ActorStateResolver.Resolve(actors["Thing"].States);
    }

    // The frames from a group's first, following Next until it leaves the group or repeats
    private static List<ActorStateFrame> Walk(ActorStateFrame first)
    {
        var frames = new List<ActorStateFrame>();
        for (var frame = first; frame != null && !frames.Contains(frame); frame = frame.Next!)
        {
            frames.Add(frame);
            if (frame.Next?.StateName != first.StateName)
                break;
        }
        return frames;
    }

    [Test]
    public void Each_Frame_Letter_Becomes_A_Linked_Frame()
    {
        // Act
        var states = Resolve("""
                Spawn:
                  - sprite: GARD
                    frames: [A, B, C]
                    tics-per-frame: 10
                    think: T_Stand
                    action: A_Look
                    modifiers: [bright]
            """);

        // Assert
        var frames = Walk(states["Spawn"]);
        Assert.That(frames.Select(f => f.FrameLetter), Is.EqualTo(new[] { "A", "B", "C" }));
        Assert.That(frames, Has.All.Matches<ActorStateFrame>(f =>
            f.Sprite == "GARD" && f.TicTime == 10 && f.Think == "T_Stand" && f.Action == "A_Look" && f.Bright));
    }

    [Test]
    public void A_Group_With_No_Ending_Loops_To_Its_First_Frame()
    {
        // Act
        var states = Resolve("""
                Spawn:
                  - sprite: LAMP
                    frames: [A, B]
                    tics-per-frame: 5
            """);

        // Assert
        var first = states["Spawn"];
        Assert.That(first.Next!.Next, Is.SameAs(first));
    }

    [Test]
    public void Next_State_Goes_To_Another_Groups_First_Frame()
    {
        // Act: Pain is declared before the group it goes to
        var states = Resolve("""
                Pain:
                  - sprite: GARD
                    frames: [H]
                    tics-per-frame: 10
                    next-state: Chase
                Chase:
                  - sprite: GARD
                    frames: [B, C]
                    tics-per-frame: 8
            """);

        // Assert
        Assert.That(states["Pain"].Next, Is.SameAs(states["Chase"]));
    }

    [Test]
    public void Next_State_Of_Its_Own_Group_Loops()
    {
        // Act
        var states = Resolve("""
                Chase:
                  - sprite: GARD
                    frames: [B, C]
                    tics-per-frame: 8
                    next-state: Chase
            """);

        // Assert
        Assert.That(states["Chase"].Next!.Next, Is.SameAs(states["Chase"]));
    }

    [Test]
    public void Entries_In_A_Group_Run_On_Into_Each_Other()
    {
        // Act: a next-state on the first entry is overridden by the entry after it
        var states = Resolve("""
                Death:
                  - sprite: GARD
                    frames: [I]
                    tics-per-frame: 15
                    action: A_Scream
                  - sprite: GARD
                    frames: [J, K]
                    tics-per-frame: 15
                  - sprite: GARD
                    frames: [L]
                    tics-per-frame: -1
            """);

        // Assert
        var frames = Walk(states["Death"]);
        Assert.That(frames.Select(f => f.FrameLetter), Is.EqualTo(new[] { "I", "J", "K", "L" }));
        Assert.That(frames[^1].HoldsForever, Is.True);
    }

    [Test]
    public void Stop_Freezes_On_The_Last_Frame()
    {
        // Act
        var states = Resolve("""
                Die:
                  - sprite: BOOM
                    frames: [A, B]
                    tics-per-frame: 4
                  - stop
            """);

        // Assert
        var last = states["Die"].Next!;
        Assert.That(last.Next, Is.SameAs(last));
    }

    [Test]
    public void Goto_Marker_Goes_To_Another_Group()
    {
        // Act
        var states = Resolve("""
                Fire:
                  - sprite: PISG
                    frames: [B]
                    tics-per-frame: 6
                  - goto: Ready
                Ready:
                  - sprite: PISG
                    frames: [A]
                    tics-per-frame: 1
            """);

        // Assert
        Assert.That(states["Fire"].Next, Is.SameAs(states["Ready"]));
    }

    [Test]
    public void Next_State_To_An_Unknown_Group_Stays_Put()
    {
        // Act
        var states = Resolve("""
                Spawn:
                  - sprite: GARD
                    frames: [A]
                    tics-per-frame: 10
                    next-state: Nowhere
            """);

        // Assert
        Assert.That(states["Spawn"].Next, Is.SameAs(states["Spawn"]));
    }

    [TestCase("-1", -1)]
    [TestCase("0", 0)]
    [TestCase("7.5", 8)]
    [TestCase("6.5", 6)]
    public void Tics_Are_Rounded_And_Negative_Means_Forever(string tics, int expected)
    {
        // Act
        var states = Resolve($"""
                Spawn:
                  - sprite: GARD
                    frames: [A]
                    tics-per-frame: {tics}
            """);

        // Assert
        Assert.That(states["Spawn"].TicTime, Is.EqualTo(expected));
    }

    [Test]
    public void Shape_Names_Add_The_Direction()
    {
        // Act
        var frame = Resolve("""
                Spawn:
                  - sprite: GARD
                    frames: [A]
                    tics-per-frame: 1
            """)["Spawn"];

        // Assert
        Assert.That(frame.GetShapeName(objdirtypes.north), Is.EqualTo("GARDA2"));
        Assert.That(frame.GetShapeName(objdirtypes.nodir), Is.EqualTo("GARDA0"));
    }

    [Test]
    public void State_Light_Is_Read_From_Yaml()
    {
        // Act
        var frame = Resolve("""
                Spawn:
                  - sprite: LAMP
                    frames: [A]
                    tics-per-frame: -1
                    light: { intensity: 200, radius: 2 }
            """)["Spawn"];

        // Assert
        Assert.That(frame.Light, Is.EqualTo(new StateLight(false, 200, 2)));
    }
}
