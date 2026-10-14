using PFWolf.Assets;
using PFWolf.Configuration;

namespace PFWolf.Tests;

public class WarpRequestTests
{
    private static GameInfoAsset GameInfo(int skills = 4) => new()
    {
        Skills = Enumerable.Range(1, skills).ToDictionary(i => $"skill{i}", i => new SkillInfo { Name = $"Skill {i}" }),
        Episodes = new()
        {
            ["e1"] = new EpisodeInfo { Name = "Episode 1", StartMap = "MAP01" },
            ["e2"] = new EpisodeInfo { Name = "Episode 2", StartMap = "MAP11" },
        },
        Maps = new()
        {
            ["MAP01"] = new MapInfo(),
            ["MAP02"] = new MapInfo(),
            ["MAP11"] = new MapInfo(),
        },
    };

    private static WarpRequest Request(string warp, int? skill = null, string start = "")
    {
        var request = new WarpRequest();
        request.SetParams(new GameParams { Warp = warp, Skill = skill, Start = start });
        return request;
    }

    [Test]
    public void No_Warp_Starts_Nothing()
    {
        // Arrange
        var request = Request("");

        // Act / Assert
        Assert.That(request.TakeWarp(GameInfo(), _ => true), Is.Null);
        Assert.That(request.TakeStart(), Is.Null);
    }

    [Test]
    public void The_Level_Is_Matched_Whatever_Its_Case()
    {
        // Act
        var warp = Request(" map02 ").TakeWarp(GameInfo(), _ => true);

        // Assert
        Assert.That(warp?.Map, Is.EqualTo("MAP02"));
    }

    [Test]
    public void It_Is_Taken_Once()
    {
        // Arrange
        var request = Request("MAP01");
        request.TakeWarp(GameInfo(), _ => true);

        // Act / Assert
        Assert.That(request.TakeWarp(GameInfo(), _ => true), Is.Null);
    }

    [Test]
    public void A_Level_Game_Info_Does_Not_List_Starts_Nothing()
    {
        Assert.That(Request("NOPE").TakeWarp(GameInfo(), _ => true), Is.Null);
    }

    [Test]
    public void A_Level_Nothing_Supplies_Starts_Nothing_And_Drops_The_Start()
    {
        // Arrange
        var request = Request("MAP01", start: "3,4");

        // Act
        var warp = request.TakeWarp(GameInfo(), _ => false);

        // Assert
        Assert.That(warp, Is.Null);
        Assert.That(request.TakeStart(), Is.Null);
    }

    [TestCase(null, 4, 2)]      // left out: the third, as Wolf3D's Bring 'em on
    [TestCase(null, 2, 1)]      // or the hardest there is
    [TestCase(1, 4, 0)]
    [TestCase(4, 4, 3)]
    [TestCase(9, 4, 3)]         // past the hardest: the hardest
    [TestCase(0, 4, 0)]         // below the easiest: the easiest
    public void The_Skill_Is_One_Based_And_Clamped(int? skill, int skills, int expected)
    {
        // Act
        var warp = Request("MAP01", skill).TakeWarp(GameInfo(skills), _ => true);

        // Assert
        Assert.That(warp?.Difficulty, Is.EqualTo((short)expected));
    }

    [TestCase("MAP11", "Episode 2")]    // the episode the level starts
    [TestCase("MAP02", "Episode 1")]    // else the first
    public void The_Episode_Is_The_One_The_Level_Starts(string map, string expected)
    {
        Assert.That(Request(map).TakeWarp(GameInfo(), _ => true)?.Episode.Name, Is.EqualTo(expected));
    }

    [Test]
    public void No_Episodes_Makes_One_Starting_On_The_Level()
    {
        // Arrange
        var gameInfo = GameInfo() with { Episodes = [] };

        // Act
        var warp = Request("MAP02").TakeWarp(gameInfo, _ => true);

        // Assert
        Assert.That(warp?.Episode.StartMap, Is.EqualTo("MAP02"));
    }

    [TestCase("30,57", 30, 57, null)]
    [TestCase(" 30 , 57 , 90 ", 30, 57, 90)]
    [TestCase("30,57,east", 30, 57, null)]      // an angle that isn't a number is left out
    public void Start_Reads_The_Tile_And_Angle(string start, int x, int y, int? angle)
    {
        // Act
        var taken = Request("MAP01", start: start).TakeStart();

        // Assert
        Assert.That(taken, Is.EqualTo(((int, int, int?)?)(x, y, angle)));
    }

    [TestCase("30")]
    [TestCase("a,b")]
    [TestCase("1,2,3,4")]
    public void A_Start_That_Is_Not_A_Tile_Is_Ignored(string start)
    {
        Assert.That(Request("MAP01", start: start).TakeStart(), Is.Null);
    }

    [Test]
    public void Start_Without_Warp_Is_Ignored()
    {
        Assert.That(Request("", start: "3,4").TakeStart(), Is.Null);
    }

    [Test]
    public void Start_Is_Taken_Once()
    {
        // Arrange
        var request = Request("MAP01", start: "3,4");
        request.TakeStart();

        // Act / Assert
        Assert.That(request.TakeStart(), Is.Null);
    }
}
