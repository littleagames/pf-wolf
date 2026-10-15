namespace PFWolf.Tests;

public class MainGameLoopTests
{
    private static MainGameLoop Make() =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            null!, null!, null!, null!, null!, null!, new GameSession(), null!);

    [Test]
    public void With_No_Map_Change_Pending_The_Player_Stays_Put()
    {
        // No player, no movement: touching either would throw
        Assert.DoesNotThrow(() => Make().ApplyPendingMapChange());
    }

    [Test]
    public void A_Map_Change_Is_Used_Once_And_Without_KeepPosition_Moves_No_One()
    {
        // Arrange
        var loop = Make();
        loop.pendingMapChange = new PendingMapChange("MAP02", KeepPosition: false, 0, 0, 0);

        // Act
        loop.ApplyPendingMapChange();

        // Assert
        Assert.That(loop.pendingMapChange, Is.Null);
    }
}
