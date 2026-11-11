using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

/// <summary>The editor's frame player against the game's own stepping (DoActor, Actor.AdvanceFrames)</summary>
public class StatePlayerTests
{
    private const string Yaml = """
        Thing:
          states:
            Spawn:
              - { sprite: THNG, frames: [A], tics-per-frame: 3 }
              - { sprite: THNG, frames: [B], tics-per-frame: 0 }
              - { sprite: THNG, frames: [C, D], tics-per-frame: 2, next-state: Walk }
            Walk:
              - { sprite: THNG, frames: [E], tics-per-frame: 0 }
              - { sprite: THNG, frames: [F, G], tics-per-frame: 4 }
            Death:
              - { sprite: THNG, frames: [H], tics-per-frame: 5 }
              - { sprite: THNG, frames: [I], tics-per-frame: -1 }
        """;

    // The game's actor stepping one tic at a time, as DoActor does for an actor with no actions
    private static List<string> GameFrames(string state, int tics)
    {
        var metadata = ActorDefs.Metadata(Yaml);
        var actor = metadata.CreateActor("Thing", metadata.Actors["Thing"]);
        actor.SetState(state);
        var seen = new List<string> { actor.CurrentState!.FrameLetter };
        for (int i = 0; i < tics; i++)
        {
            var frame = actor.CurrentState!;
            if (!frame.HoldsForever && !(actor.TicCount == 0 && frame.TicTime > 0))
            {
                actor.TicCount -= 1;
                actor.AdvanceFrames();
            }
            seen.Add(actor.CurrentState!.FrameLetter);
        }
        return seen;
    }

    private static List<string> PlayerFrames(string state, int tics)
    {
        var states = ActorStateResolver.Resolve(ActorDefs.Parse(Yaml)["Thing"].States);
        var player = new StatePlayer();
        player.Start(states[state]);
        var seen = new List<string> { player.Frame!.FrameLetter };
        for (int i = 0; i < tics; i++)
        {
            player.Step();
            seen.Add(player.Frame!.FrameLetter);
        }
        return seen;
    }

    [TestCase("Spawn")]
    [TestCase("Walk")]
    [TestCase("Death")]
    public void It_Shows_The_Frames_The_Game_Shows_Tic_By_Tic(string state)
    {
        Assert.That(PlayerFrames(state, 40), Is.EqualTo(GameFrames(state, 40)));
    }

    [Test]
    public void It_Tells_Each_Action_As_Its_Frame_Ends_And_Stops_At_A_Jump_When_Asked()
    {
        // Arrange
        var states = ActorStateResolver.Resolve(ActorDefs.Parse("""
            Thing:
              states:
                Attack:
                  - { sprite: THNG, frames: [A], tics-per-frame: 2, action: A_Shoot }
                  - { sprite: THNG, frames: [B], tics-per-frame: 2, action: A_JumpIfInSight("Attack") }
                  - { sprite: THNG, frames: [C], tics-per-frame: 2, next-state: Attack }
            """)["Thing"].States);
        var player = new StatePlayer { StopAtJumps = true };
        player.Start(states["Attack"]);

        // Act
        var first = player.Step(2);
        var second = player.Step(2);

        // Assert
        Assert.That(first.Select(e => e.Action), Is.EqualTo(new[] { "A_Shoot" }));
        Assert.That(first[0].MayJump, Is.False);
        Assert.That(second.Select(e => e.Action), Is.EqualTo(new[] { "A_JumpIfInSight(\"Attack\")" }));
        Assert.That(second[0].MayJump, Is.True);
        Assert.That(player.Stuck, Is.True);
        Assert.That(player.Frame!.FrameLetter, Is.EqualTo("B"), "it waits on the frame whose action may jump");

        // Going on to the frame's next, as if it didn't jump
        player.Resume(player.Frame.Next!);
        Assert.That(player.Stuck, Is.False);
        Assert.That(player.Frame!.FrameLetter, Is.EqualTo("C"));
    }

    [Test]
    public void A_Loop_Of_Zero_Tic_Frames_Is_Stopped_On()
    {
        var states = ActorStateResolver.Resolve(ActorDefs.Parse("""
            Thing:
              states:
                Spin:
                  - { sprite: THNG, frames: [A, B], tics-per-frame: 0 }
            """)["Thing"].States);
        var player = new StatePlayer();

        player.Start(states["Spin"]);

        Assert.That(player.Stuck, Is.True);
    }
}
