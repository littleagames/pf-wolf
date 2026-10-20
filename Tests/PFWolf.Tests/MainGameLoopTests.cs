namespace PFWolf.Tests;

public class MainGameLoopTests
{
    private static MainGameLoop Make() =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            null!, null!, null!, null!, null!, null!, new GameSession(), new Configuration.GameSettings(), null!);

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
        loop.PendingMapChange = new PendingMapChange("MAP02", KeepPosition: false, 0, 0, 0);

        // Act
        loop.ApplyPendingMapChange();

        // Assert
        Assert.That(loop.PendingMapChange, Is.Null);
    }

    [Test]
    public void A_Map_That_Ends_The_Game_Is_Read_From_Game_Info()
    {
        // Act
        var gameInfo = Loaders.YamlDataEntryLoader.Deserialize<Assets.GameInfoAsset>("""
            maps:
              DT01: { ends-game: true }
              DT02: {}
            """);

        // Assert
        Assert.That(MainGameLoop.LeavingEndsGame(gameInfo.Maps["DT01"]), Is.True);
        Assert.That(MainGameLoop.LeavingEndsGame(gameInfo.Maps["DT02"]), Is.False);
    }

    [Test]
    public void An_End_Message_Ends_The_Game_Too()
    {
        Assert.That(MainGameLoop.LeavingEndsGame(new Assets.MapInfo { EndMessage = "The demo's over" }), Is.True);
    }
}
