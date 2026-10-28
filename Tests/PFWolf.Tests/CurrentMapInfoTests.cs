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

    private static readonly Dictionary<string, string> Language = new()
    {
        ["$ONFLOOR"] = "ON FLOOR {floor}",
        ["$SECRET1"] = "ON SECRET FLOOR 1",
    };

    private static string Localize(string text) => Language.GetValueOrDefault(text, text);

    [TestCase(3, null, "BARRIER\nON FLOOR 3")]
    [TestCase(0, "$SECRET1", "BARRIER\nON SECRET FLOOR 1")]
    public void A_Message_Gets_The_Maps_Floor_Text_Else_The_Default_With_Its_Floor_Number(int floor, string? own, string expected)
    {
        // Arrange
        var map = new MapInfo { FloorNumber = floor, FloorText = own };
        var defaults = new DefaultMapInfo { FloorText = "$ONFLOOR" };

        // Act
        var text = CurrentMapInfo.FillFloor("BARRIER\n{floor-text}", map, defaults, Localize);

        // Assert
        Assert.That(text, Is.EqualTo(expected));
    }

    [Test]
    public void Without_Any_Floor_Text_It_Says_Nothing_And_Other_Braces_Are_Left()
    {
        // Act
        var text = CurrentMapInfo.FillFloor("A{floor-text}B {floor} {tag}", new MapInfo { FloorNumber = 7 }, new DefaultMapInfo(), Localize);

        // Assert
        Assert.That(text, Is.EqualTo("AB 7 {tag}"));
    }
}
