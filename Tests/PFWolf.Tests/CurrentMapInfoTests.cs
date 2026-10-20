using PFWolf.Assets;

namespace PFWolf.Tests;

/// <summary>The map being played's game-info entry (Gameplay/CurrentMapInfo.cs)</summary>
public class CurrentMapInfoTests
{
    [Test]
    public void The_Map_Being_Played_Has_Its_Own_Entry_And_Others_None()
    {
        // Arrange
        var session = new GameSession();
        var info = new GameInfoAsset { Maps = { ["MAP01"] = new MapInfo { TimeLimit = 90 } } };
        var current = new CurrentMapInfo(session, () => info);

        // Act / Assert
        session.GameState.MapOn = "MAP01";
        Assert.That(current.Map?.TimeLimit, Is.EqualTo(90));
        session.GameState.MapOn = "MAP02";
        Assert.That(current.Map, Is.Null);
        Assert.That(current.Default, Is.SameAs(info.DefaultMap));
    }

    [Test]
    public void It_Asks_For_The_Game_Info_Each_Time()
    {
        // Arrange: another game's info loaded since
        var session = new GameSession();
        session.GameState.MapOn = "MAP01";
        var info = new GameInfoAsset();
        var current = new CurrentMapInfo(session, () => info);

        // Act
        info = new GameInfoAsset { Maps = { ["MAP01"] = new MapInfo { Radar = true } } };

        // Assert
        Assert.That(current.Map?.Radar, Is.True);
    }
}
