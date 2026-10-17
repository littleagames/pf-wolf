namespace PFWolf.Tests;

public class HubsTests
{
    // The state and its saving touch none of the managers
    private static Hubs Fresh(GameSession? session = null) => new(null!, null!, session ?? new GameSession());

    private static Hubs Played()
    {
        var hubs = Fresh();
        hubs.HubLevels["MAP02"] = [1, 2, 3];
        hubs.VisitedMaps.UnionWith(["MAP01", "MAP02"]);
        hubs.UnlockedMaps.Add("MAP05");
        hubs.FloorOverheads["MAP01"] = [9, 9];
        hubs.FloorScores["MAP01"] = 250;
        hubs.LevelMap = "MAP02";
        hubs.LevelStart = [7, 7, 7];
        hubs.InformantTotal = 4;
        return hubs;
    }

    private static HubState RoundTrip(Hubs hubs)
    {
        using var stream = new MemoryStream();
        hubs.WriteHubState(new BinaryWriter(stream));
        stream.Position = 0;
        return Hubs.ReadHubState(new BinaryReader(stream));
    }

    [Test]
    public void A_Save_Reads_Back_The_Hubs_As_They_Were()
    {
        // Act
        var state = RoundTrip(Played());

        // Assert
        Assert.That(state.Levels["map02"], Is.EqualTo(new byte[] { 1, 2, 3 }));     // map names whatever their case
        Assert.That(state.Visited, Is.EquivalentTo(new[] { "MAP01", "MAP02" }));
        Assert.That(state.Unlocked, Is.EquivalentTo(new[] { "MAP05" }));
        Assert.That(state.Overheads["MAP01"], Is.EqualTo(new byte[] { 9, 9 }));
        Assert.That(state.FloorScores["MAP01"], Is.EqualTo(250));
        Assert.That((state.LevelMap, state.LevelStart, state.InformantTotal), Is.EqualTo(("MAP02", (byte[]?)new byte[] { 7, 7, 7 }, 4)));
    }

    [Test]
    public void A_New_Game_Saves_No_Level_Map_Or_Start()
    {
        // Act
        var state = RoundTrip(Fresh());

        // Assert
        Assert.That(state.LevelMap, Is.Null);
        Assert.That(state.LevelStart, Is.Null);
        Assert.That(state.Levels, Is.Empty);
    }

    [Test]
    public void Restoring_Puts_It_All_Back()
    {
        // Arrange
        var state = RoundTrip(Played());
        var hubs = Fresh();

        // Act
        hubs.RestoreHubState(state);

        // Assert
        Assert.That(hubs.HubLevels.Keys, Is.EquivalentTo(new[] { "MAP02" }));
        Assert.That(hubs.VisitedMaps, Is.EquivalentTo(new[] { "MAP01", "MAP02" }));
        Assert.That((hubs.LevelMap, hubs.InformantTotal), Is.EqualTo(("MAP02", 4)));
    }

    [Test]
    public void A_Save_Without_A_Level_Map_Takes_The_Map_Being_Played()
    {
        // Arrange: an older save, before the level map was kept
        var session = new GameSession();
        session.GameState.MapOn = "MAP07";
        var hubs = Fresh(session);

        // Act
        hubs.RestoreHubState(RoundTrip(Fresh()));

        // Assert
        Assert.That(hubs.LevelMap, Is.EqualTo("MAP07"));
    }

    [Test]
    public void A_New_Game_Forgets_Everything()
    {
        // Arrange
        var hubs = Played();

        // Act
        hubs.ResetHubs();

        // Assert
        Assert.That(hubs.HubLevels, Is.Empty);
        Assert.That(hubs.VisitedMaps, Is.Empty);
        Assert.That(hubs.UnlockedMaps, Is.Empty);
        Assert.That(hubs.FloorOverheads, Is.Empty);
        Assert.That(hubs.FloorScores, Is.Empty);
        Assert.That(hubs.LevelMap, Is.Null);
        Assert.That(hubs.LevelStart, Is.Null);
    }

    [TestCase("MAP01", true)]       // been on
    [TestCase("MAP05", true)]       // unlocked (Planet Strike's security cube)
    [TestCase("MAP03", false)]
    [TestCase("map01", true)]       // whatever the case
    public void A_Floor_Is_Open_Once_Been_On_Or_Unlocked(string map, bool open)
    {
        Assert.That(Played().FloorOpen(map), Is.EqualTo(open));
    }

    [TestCase(3, 4, 75)]
    [TestCase(0, 4, 0)]
    [TestCase(0, 0, 100)]       // nothing to get counts as all of it
    public void A_Ratio_Is_A_Percentage(int got, int total, int expected)
    {
        Assert.That(Hubs.Ratio(got, total), Is.EqualTo(expected));
    }
}
