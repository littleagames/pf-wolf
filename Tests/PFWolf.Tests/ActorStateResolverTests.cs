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
    public void A_Next_State_Before_The_Last_Entry_Is_Passed_Over_And_Told()
    {
        // Arrange: Fire's first entry names Ready, but the second entry's frames follow it
        var actors = ActorDefs.Parse("""
            Thing:
              states:
                Ready:
                  - { sprite: GUNG, frames: [A], tics-per-frame: 1 }
                Fire:
                  - { sprite: GUNG, frames: [B], tics-per-frame: 6, next-state: Ready }
                  - { sprite: GUNG, frames: [C], tics-per-frame: 6, next-state: Ready }
            """);
        var ignored = new List<string>();

        // Act
        var states = ActorStateResolver.Resolve(actors["Thing"].States,
            (state, _, marker) => ignored.Add(ActorStateResolver.IgnoredMessage("Thing", state, marker)));

        // Assert: B goes on to C, and only the first entry's next-state is told of
        Assert.That(states["Fire"].Next!.FrameLetter, Is.EqualTo("C"));
        Assert.That(states["Fire"].Next!.Next, Is.SameAs(states["Ready"]));
        Assert.That(ignored, Is.EqualTo(new[]
        {
            "Actor Thing: in its Fire state, next-state (or goto) Ready isn't on the state's last entry, so it's never reached: the frames after it play instead",
        }));
    }

    [Test]
    public void Endings_Where_They_Belong_Are_Not_Told()
    {
        // Arrange: a state that loops by itself, one with a goto, one with a stop
        var actors = ActorDefs.Parse("""
            Thing:
              states:
                Spawn:
                  - { sprite: LAMP, frames: [A, B], tics-per-frame: 5 }
                See:
                  - { sprite: GARD, frames: [A], tics-per-frame: 5 }
                  - goto: Spawn
                Death:
                  - { sprite: GARD, frames: [K], tics-per-frame: 5 }
                  - stop
            """);
        var ignored = new List<string>();

        // Act
        ActorStateResolver.Resolve(actors["Thing"].States, (state, _, _) => ignored.Add(state));

        // Assert
        Assert.That(ignored, Is.Empty);
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
    public void A_Class_That_Dies_Gets_Up_By_Its_Death_Frames_Backwards()
    {
        // Act
        var states = Resolve("""
                Spawn:
                  - sprite: GARD
                    frames: [A]
                    tics-per-frame: -1
                Death:
                  - sprite: GARD
                    frames: [K]
                    tics-per-frame: 15
                    action: A_DeathScream
                  - sprite: GARD
                    frames: [L, M]
                    tics-per-frame: 12
                  - sprite: GARD
                    frames: [N]
                    tics-per-frame: -1
            """);

        // Assert: the corpse's own frame left out, no death scream, and A_Resume at the end,
        // with Spawn after it
        var frames = Walk(states[ActorStateResolver.RaiseState]);
        Assert.That(frames.Select(f => f.FrameLetter), Is.EqualTo(new[] { "M", "L", "K" }));
        Assert.That(frames.Select(f => (int)f.TicTime), Is.EqualTo(new[] { 12, 12, 15 }));
        Assert.That(frames.Select(f => f.Action), Is.EqualTo(new[] { null, null, "A_Resume" }));
        Assert.That(frames[^1].Next, Is.SameAs(states["Spawn"]));
    }

    [Test]
    public void A_Death_Of_One_Held_Frame_Gets_Up_On_It()
    {
        // Act
        var states = Resolve("""
                Death:
                  - sprite: BARL
                    frames: [B]
                    tics-per-frame: -1
            """);

        // Assert
        var raise = states[ActorStateResolver.RaiseState];
        Assert.That(raise.FrameLetter, Is.EqualTo("B"));
        Assert.That(raise.TicTime, Is.GreaterThan(0));
        Assert.That(raise.Action, Is.EqualTo("A_Resume"));
    }

    [Test]
    public void A_Raise_State_Of_Its_Own_Is_Kept()
    {
        // Act
        var states = Resolve("""
                Death:
                  - sprite: GARD
                    frames: [K, N]
                    tics-per-frame: 10
                Raise:
                  - sprite: GARD
                    frames: [Z]
                    tics-per-frame: 5
            """);

        // Assert
        Assert.That(Walk(states[ActorStateResolver.RaiseState]).Select(f => f.FrameLetter), Is.EqualTo(new[] { "Z" }));
    }

    [Test]
    public void A_Class_That_Never_Dies_Gets_No_Raise()
    {
        // Act
        var states = Resolve("""
                Spawn:
                  - sprite: LAMP
                    frames: [A]
                    tics-per-frame: -1
            """);

        // Assert
        Assert.That(states.Keys, Is.EquivalentTo(new[] { "Spawn" }));
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
        Assert.That(frame.GetShapeName(ObjDir.North), Is.EqualTo("GARDA2"));
        Assert.That(frame.GetShapeName(ObjDir.NoDir), Is.EqualTo("GARDA0"));
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
