using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Enums;

namespace PFWolf.Tests;

public class LevelChecksTests
{
    private const ushort Wall = 1, Floor = 107, Ambush = 106, VerticalDoor = 90, HorizontalDoor = 91;
    private const ushort Start = 19, Puddle = 23, PushWall = 98, Diagonal = 400, DoorSwitch = 50, WallSwitch = 51;

    private static GameContent Content() => GameContent.FromDefinitions(new MapObjectTranslationAsset
    {
        Walls = new()
        {
            [Wall] = new() { North = "GSTONEA1", East = "GSTONEA2", South = "GSTONEA1", West = "GSTONEA2" },
            [DoorSwitch] = new() { North = "SW", Switch = new MapSwitchTranslation { Actions = ["A_OpenDoor"] } },
            [WallSwitch] = new() { North = "SW", Switch = new MapSwitchTranslation { Actions = ["A_MoveWall(\"east\")"] } },
        },
        Doors = new()
        {
            [VerticalDoor] = new() { East = "DOOR", Vertical = true },
            [HorizontalDoor] = new() { North = "DOOR" },
        },
        Floors = new MapFloorsTranslation { AreaStart = Floor, AreaCount = 37, Ambush = Ambush, SecretExit = -1 },
        PlayerStarts = new() { [Start] = new MapPlayerStartTranslation { Angles = 90 } },
        Things = new() { [Puddle] = new MapActorTranslation { Class = "Puddle" } },
        Triggers = new() { [PushWall] = new MapTriggerTranslation { Action = "A_PushWall", Secret = true } },
        Diagonals = new() { [Diagonal] = new MapDiagonalTranslation { Shape = WallShape.SolidNW } },
    });

    /// <summary>A level of open floor walled in all round, with the player starting at (5, 5)</summary>
    private static MapDocument Room()
    {
        var map = MapFiles.NewMap("Test", Wall, Floor);
        map.MapData[1][5 * 64 + 5] = Start;
        return new MapDocument("MAP01", map, properties: new MapProperties());
    }

    private static List<LevelProblem> Check(MapDocument document, bool inGameInfo = true)
        => LevelChecks.Run(new MapTiles(Content(), document.Map, () => document.Properties), inGameInfo);

    /// <summary>Walls a rectangle off: a room of floor inside it that nothing opens into</summary>
    private static void WallOff(MapDocument document, int left, int top, int right, int bottom)
    {
        using var edit = document.BeginEdit("Walls");
        foreach (var (x, y) in TileShapes.Rectangle(new TileRect(left, top, right, bottom), outline: true))
            edit.Set(0, x, y, Wall);
    }

    [Test]
    public void A_Walled_Level_With_A_Start_Is_Fine()
    {
        // Act
        var problems = Check(Room());

        // Assert
        Assert.That(problems, Is.Empty);
    }

    [Test]
    public void No_Player_Start_Is_An_Error()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 1, 5, 5, 0);

        // Act
        var problems = Check(document);

        // Assert
        Assert.That(problems.Single().Severity, Is.EqualTo(CheckSeverity.Error));
        Assert.That(problems.Single().Message, Does.Contain("no player start"));
    }

    [Test]
    public void Extra_Player_Starts_Are_Pointed_Out_At_The_Ones_The_Game_Ignores()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 1, 9, 9, Start);

        // Act
        var problem = Check(document).Single();

        // Assert: the game uses the last one it comes to, (9, 9)
        Assert.That((problem.X, problem.Y), Is.EqualTo((5, 5)));
        Assert.That(problem.Message, Does.Contain("(9, 9)"));
    }

    [Test]
    public void A_Level_Game_Info_Lacks_Is_Warned_About()
    {
        // Act
        var problems = Check(Room(), inGameInfo: false);

        // Assert
        Assert.That(problems.Single().Severity, Is.EqualTo(CheckSeverity.Warning));
        Assert.That(problems.Single().Message, Does.Contain("game-info doesn't list"));
    }

    [Test]
    public void Unknown_Plane_0_Values_Are_Counted_Once()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 0, 10, 10, 999);
        EditorMaps.Set(document, 0, 11, 10, 999);

        // Act
        var problem = Check(document).Single();

        // Assert
        Assert.That(problem.Severity, Is.EqualTo(CheckSeverity.Error));
        Assert.That(problem.Message, Does.Contain("999").And.Contain("2 tiles"));
        Assert.That((problem.X, problem.Y), Is.EqualTo((10, 10)));
    }

    [Test]
    public void Floor_On_The_Edge_The_Player_Can_Reach_Is_Warned_About()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 0, 0, 20, Floor);

        // Act
        var problem = Check(document).Single();

        // Assert
        Assert.That(problem.Severity, Is.EqualTo(CheckSeverity.Warning));
        Assert.That(problem.Message, Does.Contain("edge"));
        Assert.That((problem.X, problem.Y), Is.EqualTo((0, 20)));
    }

    [Test]
    public void Floor_On_The_Edge_Out_Of_Reach_Is_Only_An_Unreachable_Room()
    {
        // Arrange: a corner of floor walled off from the level, as id's levels have
        var document = Room();
        using (var edit = document.BeginEdit("Corner"))
        {
            edit.Set(0, 0, 63, Floor);
            edit.Set(0, 1, 62, Wall);
        }

        // Act
        var problem = Check(document).Single();

        // Assert
        Assert.That(problem.Severity, Is.EqualTo(CheckSeverity.Warning));
        Assert.That(problem.Message, Does.Contain("can't reach"));
    }

    [Test]
    public void A_Door_Needs_Walls_On_The_Sides_It_Slides_Into()
    {
        // Arrange: a vertical door in a north-south wall line is fine; one in open floor isn't
        var document = Room();
        using (var edit = document.BeginEdit("Doors"))
        {
            for (int y = 1; y < 63; y++)
                edit.Set(0, 20, y, Wall);
            edit.Set(0, 20, 10, VerticalDoor);
            edit.Set(0, 30, 30, HorizontalDoor);
        }

        // Act
        var problems = Check(document);

        // Assert
        Assert.That(problems.Single().Message, Does.Contain("west and east"));
        Assert.That((problems.Single().X, problems.Single().Y), Is.EqualTo((30, 30)));
    }

    [Test]
    public void A_Thing_On_A_Wall_Is_Not_Placed()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, 1, 0, 10, Puddle);

        // Act
        var problem = Check(document).Single();

        // Assert
        Assert.That(problem.Message, Does.Contain("Puddle is on a wall"));
    }

    [Test]
    public void Switches_And_Tags_That_Link_To_Nothing_Are_Warned_About()
    {
        // Arrange: an untagged switch, a switch whose tag nothing else has, a tag no switch has
        var document = Room();
        EditorMaps.Set(document, 0, 0, 10, DoorSwitch);
        EditorMaps.Set(document, 0, 0, 20, DoorSwitch);
        EditorMaps.Set(document, MapConstants.TAGPLANE, 0, 20, 4);
        EditorMaps.Set(document, MapConstants.TAGPLANE, 30, 30, 9);

        // Act
        var messages = Check(document).Select(problem => problem.Message).ToList();

        // Assert
        Assert.That(messages, Has.Some.Contains("switch without a tag"));
        Assert.That(messages, Has.Some.Contains("switch with tag 4, which nothing else has"));
        Assert.That(messages, Has.Some.Contains("tag 9 is on 1 tile, but no switch has it"));
        Assert.That(messages, Has.Count.EqualTo(3));
    }

    [Test]
    public void A_Zone_With_No_Definition_Is_Warned_About_Until_The_Properties_Define_It()
    {
        // Arrange
        var document = Room();
        EditorMaps.Set(document, MapConstants.ZONEPLANE, 10, 10, 3);

        // Act
        var before = Check(document);
        document.SetProperties(document.Properties with { Zones = [new ZoneProperties(3, Light: 40)] });
        var after = Check(document);

        // Assert
        Assert.That(before.Single().Message, Does.Contain("light zone 3"));
        Assert.That(after, Is.Empty);
    }

    [Test]
    public void A_Room_Nothing_Opens_Into_Is_Unreachable()
    {
        // Arrange: a 3x3 room with a puddle in it
        var document = Room();
        WallOff(document, 30, 30, 34, 34);
        EditorMaps.Set(document, 1, 32, 32, Puddle);

        // Act
        var problem = Check(document).Single();

        // Assert
        Assert.That(problem.Message, Does.Contain("9 tiles of floor the player can't reach").And.Contain("1 thing"));
        Assert.That((problem.X, problem.Y), Is.EqualTo((31, 31)));
    }

    [TestCase(PushWall, TestName = "A_Room_Behind_A_Pushwall_Is_Reachable")]
    [TestCase(Diagonal, TestName = "A_Room_Behind_A_Diagonal_Is_Reachable")]
    public void A_Room_Behind_A_Wall_That_Opens_Is_Reachable(int marker)
    {
        // Arrange
        var document = Room();
        WallOff(document, 30, 30, 34, 34);
        EditorMaps.Set(document, 1, 30, 32, (ushort)marker);

        // Act
        var problems = Check(document);

        // Assert
        Assert.That(problems, Is.Empty);
    }

    [Test]
    public void A_Room_Behind_A_Wall_A_Switch_Moves_Is_Reachable()
    {
        // Arrange: the switch and the wall it moves share tag 5
        var document = Room();
        WallOff(document, 30, 30, 34, 34);
        EditorMaps.Set(document, 0, 0, 10, WallSwitch);
        EditorMaps.Set(document, MapConstants.TAGPLANE, 0, 10, 5);
        EditorMaps.Set(document, MapConstants.TAGPLANE, 30, 32, 5);

        // Act
        var problems = Check(document);

        // Assert
        Assert.That(problems, Is.Empty);
    }
}
