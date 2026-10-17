using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class ActorWorldTests
{
    private GameSession _session = null!;
    private ActorWorld _world = null!;

    [SetUp]
    public void Make()
    {
        _session = new GameSession();
        _world = new ActorWorld(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            null!, null!, null!, null!, null!, _session);
    }

    private static PlayerPawn Pawn(short health) => new() { State = new Entities.PlayerState { Health = health } };

    // GameMode by number: Single, Coop, Deathmatch
    [TestCase(0, 0, true)]          // alone, dying ends the level: still gone after that frame
    [TestCase(1, 0, false)]
    [TestCase(1, 50, true)]
    [TestCase(2, 0, false)]
    public void A_Player_Can_Be_Gone_After_While_Alive_Or_Alone(int mode, short health, bool targetable)
    {
        // Arrange
        _session.SetGameMode((GameMode)mode);

        // Act / Assert
        Assert.That(_session.IsTargetable(Pawn(health)), Is.EqualTo(targetable));
    }

    [Test]
    public void A_Monster_Asks_Its_Worlds_Session()
    {
        // Arrange: a monster in the world, with a dead player in a co-op game
        var guard = new Monster { Name = "Guard", World = _world };
        _session.SetGameMode(GameMode.Coop);

        // Act / Assert
        Assert.That(guard.IsTargetable(Pawn(0)), Is.False);
        Assert.That(guard.IsTargetable((Entities.Actors.Actor)Pawn(10)), Is.True);
    }
}
