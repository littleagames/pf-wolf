namespace PFWolf.Tests;

public class AreaConnectionsTests
{
    private const int Areas = 5;

    private static AreaConnections WithDoorsOpen(params (int A, int B)[] doors)
    {
        var areas = new AreaConnections();
        areas.Reset(Areas);
        foreach (var (a, b) in doors)
        {
            areas.AreaConnect[a, b]++;
            areas.AreaConnect[b, a]++;
        }
        return areas;
    }

    private static int[] ByPlayer(AreaConnections areas) =>
        Enumerable.Range(0, Areas).Where(i => areas.AreaByPlayer[i] != 0).ToArray();

    [Test]
    public void A_New_Level_Has_Nothing_Connected()
    {
        // Act
        var areas = WithDoorsOpen();

        // Assert
        Assert.That(areas.AreaByPlayer, Has.Length.EqualTo(Areas));
        Assert.That(ByPlayer(areas), Is.Empty);
    }

    [Test]
    public void Open_Doors_Join_Areas_In_A_Chain()
    {
        // Arrange: 0-1 and 1-2 open, 3 shut off
        var areas = WithDoorsOpen((0, 1), (1, 2));

        // Act
        areas.ConnectAreas([0], Areas);

        // Assert
        Assert.That(ByPlayer(areas), Is.EqualTo(new[] { 0, 1, 2 }));
    }

    [Test]
    public void A_Closed_Door_Cuts_The_Chain()
    {
        // Arrange: the 1-2 door has closed again
        var areas = WithDoorsOpen((0, 1), (1, 2));
        areas.AreaConnect[1, 2]--;
        areas.AreaConnect[2, 1]--;

        // Act
        areas.ConnectAreas([0], Areas);

        // Assert
        Assert.That(ByPlayer(areas), Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void Two_Doors_Between_The_Same_Areas_Both_Have_To_Close()
    {
        // Arrange: two doors open between 0 and 1, then one closes
        var areas = WithDoorsOpen((0, 1), (0, 1));
        areas.AreaConnect[0, 1]--;
        areas.AreaConnect[1, 0]--;

        // Act
        areas.ConnectAreas([0], Areas);

        // Assert
        Assert.That(ByPlayer(areas), Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void With_Several_Players_Any_Of_Theirs_Counts()
    {
        // Arrange
        var areas = WithDoorsOpen((0, 1), (3, 4));

        // Act
        areas.ConnectAreas([0, 4], Areas);

        // Assert
        Assert.That(ByPlayer(areas), Is.EqualTo(new[] { 0, 1, 3, 4 }));
    }

    [Test]
    public void A_Player_Off_The_Areas_Connects_Nothing()
    {
        // Arrange: standing on a tile that isn't an area (a door, say)
        var areas = WithDoorsOpen((0, 1));

        // Act
        areas.ConnectAreas([Areas + 2], Areas);

        // Assert
        Assert.That(ByPlayer(areas), Is.Empty);
    }

    [Test]
    public void A_Level_Starts_With_Only_The_Players_Own_Areas()
    {
        // Arrange: even with doors open, InitAreas marks only where the players are
        var areas = WithDoorsOpen((0, 1));

        // Act
        areas.InitAreas([2], Areas);

        // Assert
        Assert.That(ByPlayer(areas), Is.EqualTo(new[] { 2 }));
    }
}
