using PFWolf.Gameplay;

namespace PFWolf.Tests;

/// <summary>Sounds quieter through shut doors (Gameplay/SoundMuffling.cs): counting the doors between areas, and what's left</summary>
public class SoundMufflingTests
{
    // Areas 0-1-2-3 in a row, a door between each two; area 4 behind a wall (no door)
    private static (byte[,] Connect, byte[,] Doors) Level()
    {
        var connect = new byte[5, 5];
        var doors = new byte[5, 5];
        foreach (var (a, b) in new[] { (0, 1), (1, 2), (2, 3) })
            doors[a, b] = doors[b, a] = 1;
        return (connect, doors);
    }

    private static int[] Between(int from, byte[,] connect, byte[,] doors)
    {
        var result = new int[5];
        AreaConnections.DoorsBetween(from, connect, doors, result);
        return result;
    }

    [Test]
    public void Each_Shut_Door_On_The_Way_Counts()
    {
        var (connect, doors) = Level();
        Assert.That(Between(0, connect, doors), Is.EqualTo(new[] { 0, 1, 2, 3, AreaConnections.Unreached }));
    }

    [Test]
    public void An_Open_Door_Counts_For_Nothing()
    {
        // Arrange: the door between 1 and 2 open (AreaConnect counts it)
        var (connect, doors) = Level();
        connect[1, 2] = connect[2, 1] = 1;

        // Assert
        Assert.That(Between(0, connect, doors), Is.EqualTo(new[] { 0, 1, 1, 2, AreaConnections.Unreached }));
    }

    [Test]
    public void The_Way_With_Fewest_Shut_Doors_Wins()
    {
        // Arrange: a second way from 0 to 3 with one door, and a blown-through wall (AreaConnect, no door) from 3 to 4
        var (connect, doors) = Level();
        doors[0, 3] = doors[3, 0] = 1;
        connect[3, 4] = connect[4, 3] = 1;

        // Assert
        Assert.That(Between(0, connect, doors), Is.EqualTo(new[] { 0, 1, 2, 1, 1 }));
    }

    [Test]
    public void From_No_Area_Nothing_Is_Reached()
    {
        var (connect, doors) = Level();
        Assert.That(Between(-1, connect, doors), Is.All.EqualTo(AreaConnections.Unreached));
    }

    [TestCase(0, 1f)]
    [TestCase(1, 0.35f)]
    [TestCase(2, 0.1225f)]
    [TestCase(3, 0.042875f)]
    [TestCase(7, 0.042875f)]                                // at most three count
    [TestCase(AreaConnections.Unreached, 0.042875f)]        // no door path: as three
    public void Each_Shut_Door_Leaves_The_Strength_Of_It(int doors, float left) =>
        Assert.That(SoundMuffling.Factor(doors, 0.35f), Is.EqualTo(left).Within(0.0001));

    [Test]
    public void A_Strength_Of_One_Muffles_Nothing() =>
        Assert.That(SoundMuffling.Factor(3, 1), Is.EqualTo(1));

    [Test]
    public void Doors_Join_Their_Areas_Once_Each_Way()
    {
        var areas = new AreaConnections();
        areas.Reset(3);
        areas.JoinByDoor(0, 2);
        areas.JoinByDoor(1, 1);         // the same area both sides: nothing
        areas.JoinByDoor(0, 9);         // no such area: nothing
        Assert.That(areas.DoorJoin[0, 2], Is.EqualTo(1));
        Assert.That(areas.DoorJoin[2, 0], Is.EqualTo(1));
        Assert.That(areas.DoorJoin[1, 1], Is.Zero);
    }
}
