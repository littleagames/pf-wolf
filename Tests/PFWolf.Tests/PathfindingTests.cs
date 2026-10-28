namespace PFWolf.Tests;

/// <summary>A* over a level's tiles (Gameplay/Pathfinding.cs), on maps drawn as text: # a wall, D a door (open straight on, not diagonally)</summary>
public class PathfindingTests
{
    private readonly Pathfinder _pathfinder = new();

    // The way from S toward G on the map, with diagonal steps or without
    private ObjDir? Step(string[] rows, bool diagonals = true)
    {
        int width = rows[0].Length, height = rows.Length;
        (int X, int Y) Find(char c) => Enumerable.Range(0, height).SelectMany(y => Enumerable.Range(0, width).Select(x => (x, y)))
            .First(t => rows[t.y][t.x] == c);
        var (sx, sy) = Find('S');
        var (gx, gy) = Find('G');
        bool Open(int x, int y) => rows[y][x] != '#';
        bool OpenDiagonally(int x, int y) => rows[y][x] is not ('#' or 'D');
        return _pathfinder.FirstStep(width, height, sx, sy, gx, gy, Open, diagonals ? OpenDiagonally : null);
    }

    [Test]
    public void Across_Open_Floor_It_Heads_Straight_For_The_Goal()
    {
        Assert.That(Step(["S...G"]), Is.EqualTo(ObjDir.East));
        Assert.That(Step(["S....", ".....", "....G"]), Is.EqualTo(ObjDir.SouthEast));
    }

    [Test]
    public void It_Goes_Round_A_Wall_The_Shorter_Way()
    {
        // Up against the wall, with gaps north and south: the nearer one
        Assert.That(Step(
        [
            ".....",
            ".S#.G",
            "..#..",
            "..#..",
            ".....",
        ], diagonals: false), Is.EqualTo(ObjDir.North));
        // and with the only gap to the south, that way, though the goal's straight ahead
        Assert.That(Step(
        [
            "..#..",
            ".S#.G",
            "..#..",
            ".....",
        ], diagonals: false), Is.EqualTo(ObjDir.South));
    }

    [Test]
    public void A_Diagonal_Step_Needs_Both_Tiles_Beside_It_Open()
    {
        // The corner's wall keeps it from cutting across: straight on first
        Assert.That(Step(
        [
            "S#",
            ".G",
        ]), Is.EqualTo(ObjDir.South));
    }

    [Test]
    public void Through_A_Door_Only_Straight_On()
    {
        Assert.That(Step(["###", "SDG", "###"]), Is.EqualTo(ObjDir.East));
        // Diagonally past a door's no good: round by the open side
        Assert.That(Step(
        [
            "S..",
            "D.G",
        ]), Is.EqualTo(ObjDir.East));
    }

    [Test]
    public void With_No_Way_There_It_Heads_For_The_Nearest_It_Can_Reach()
    {
        Assert.That(Step(["S..#G"], diagonals: false), Is.EqualTo(ObjDir.East));
        Assert.That(Step(["S#G"]), Is.Null);
    }

    [Test]
    public void At_The_Goal_There_Is_No_Way_To_Go()
    {
        Assert.That(_pathfinder.FirstStep(4, 4, 1, 1, 1, 1, (_, _) => true), Is.Null);
    }

    [Test]
    public void The_Same_Search_Always_Comes_Out_The_Same()
    {
        // Two ways round the pillar as short: the same one every time, and again after other searches
        string[] map = ["S.....", "..##..", "..##..", ".....G"];
        var first = Step(map, diagonals: false);
        Step(["S..#G"]);
        Assert.That(Enumerable.Range(0, 5).Select(_ => Step(map, diagonals: false)), Has.All.EqualTo(first));
    }

    [Test]
    public void A_Big_Open_Level_Is_Searched_Within_Its_Limit()
    {
        // 256 x 256 with a wall across all but its far end: still a step, toward somewhere nearer
        bool Open(int x, int y) => !(x == 128 && y < 255);
        var way = _pathfinder.FirstStep(256, 256, 0, 0, 255, 0, Open, Open);
        Assert.That(way, Is.Not.Null);
    }

    // (ObjDir is internal, so the cases give its name)
    [TestCase("north", "North")]
    [TestCase("SouthWest", "SouthWest")]
    [TestCase("none", "NoDir")]
    [TestCase("up", null)]
    [TestCase("3", null)]
    public void A_Ways_Name_Reads_As_The_Way(string name, string? way)
    {
        var parsed = Pathfinder.ParseDir(name);
        Assert.That(parsed?.ToString(), Is.EqualTo(way));
        if (parsed is { } dir)
            Assert.That(Pathfinder.ParseDir(Pathfinder.DirName(dir)), Is.EqualTo(dir));
    }
}
