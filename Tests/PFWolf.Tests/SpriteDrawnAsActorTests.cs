using PFWolf.Entities.Actors;
using PFWolf.Rendering;

namespace PFWolf.Tests;

/// <summary>
/// Which actors DrawScaleds draws where they really are (TransformActor) rather than in the
/// middle of their tile, as a static
/// </summary>
public class SpriteDrawnAsActorTests
{
    [Test]
    public void AWolfMonsterWithNoChaseIsDrawnWhereItIs()
    {
        // The Pac-Man ghosts walk in their Spawn state (T_Ghosts) and are never woken: drawn as
        // statics they jumped a tile ahead of where they were, and vanished on reaching the player
        var ghost = new Monster { Name = "Blinky" };

        Assert.That(SpriteDrawing.DrawnAsActor(ghost), Is.True);
    }

    [Test]
    public void ABlakeMachineWithNoChaseStaysAStatic()
    {
        var vent = new BlakeMonster { Name = "SteamGrate" };

        Assert.That(SpriteDrawing.DrawnAsActor(vent), Is.False);
    }

    [Test]
    public void AnAwakeBlakeMachineIsDrawnWhereItIs()
    {
        var vent = new BlakeMonster { Name = "SteamGrate", Active = ActiveType.Yes };

        Assert.That(SpriteDrawing.DrawnAsActor(vent), Is.True);
    }

    [Test]
    public void AnItemStaysAStatic()
    {
        var crown = new ScoreItem { Name = "Crown" };

        Assert.That(SpriteDrawing.DrawnAsActor(crown), Is.False);
    }
}
