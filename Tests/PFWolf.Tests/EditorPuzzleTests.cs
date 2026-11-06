using PFWolf.Assets;
using PFWolf.Definitions;
using PFWolf.Editor.Data;
using PFWolf.Editor.Editing;
using PFWolf.Scripting;

namespace PFWolf.Tests;

/// <summary>
/// The editor and puzzle switches: finding a switch's puzzle on a tile (Try this puzzle), where
/// to stand to use it, the game's command line for it, the scripts' puzzle names, the level
/// checks and the switch fields' suggestions
/// </summary>
public class EditorPuzzleTests
{
    private const ushort Plain = 1, Floor = 107, Hacked = 70, Powered = 71, Done = 72, Lever = 73, Panel = 2;

    private static MapTiles Tiles(Action<MapAsset> arrange, Dictionary<string, ActorData>? actors = null)
    {
        var content = GameContent.FromDefinitions(new MapObjectTranslationAsset
        {
            Walls = new()
            {
                [Plain] = new() { North = "GSTONEA1" },
                [Hacked] = new()
                {
                    North = "TERM",
                    Switch = new MapSwitchTranslation
                    {
                        To = Done, Puzzle = "needle", PuzzleArgs = new() { ["hits"] = "4" }, SkipItem = "NoSuchTool", Broken = 99,
                    },
                },
                [Powered] = new() { North = "TERM", Switch = new MapSwitchTranslation { To = Done, Actions = ["A_Puzzle(\"power\", \"vault, east\", armory)"] } },
                [Done] = new() { North = "TERMOK" },
                [Lever] = new() { North = "LEVER", Switch = new MapSwitchTranslation { To = Done, Actions = ["A_OpenDoor(\"hold\")"] } },
            },
            Patches = new() { [Panel] = new MapPatchTranslation { Texture = "SIGN", Switch = new MapSwitchTranslation { Puzzle = "keypad" } } },
            Floors = new MapFloorsTranslation { AreaStart = Floor, AreaCount = 37, Ambush = 106, SecretExit = -1 },
        }, actors: actors);
        var map = MapFiles.NewMap("Test", Plain, Floor);
        arrange(map);
        return new MapTiles(content, map);
    }

    private static int At(int x, int y) => y * 64 + x;

    // A room (x 10-20, y 10-20) with the given walls set in it
    private static MapTiles Room(params (int X, int Y, ushort Value)[] tiles) => Tiles(map =>
    {
        for (int y = 10; y <= 20; y++)
            for (int x = 10; x <= 20; x++)
                map.MapData[0][At(x, y)] = Floor;
        foreach (var (x, y, value) in tiles)
            map.MapData[0][At(x, y)] = value;
    });

    [Test]
    public void A_Switch_With_A_Puzzle_Has_Its_Name_And_Words()
    {
        // Arrange
        var tiles = Room((12, 12, Hacked));

        // Act
        var puzzle = tiles.PuzzleAt(12, 12);

        // Assert
        Assert.That(puzzle, Is.Not.Null);
        Assert.That((puzzle!.Name, puzzle.Source), Is.EqualTo(("needle", "wall 70")));
        Assert.That(puzzle.Args, Is.EqualTo(new[] { "hits=4" }));
    }

    [Test]
    public void A_Switch_Opening_One_With_A_Puzzle_Has_Its_Words_Commas_And_All()
    {
        // Act
        var puzzle = Room((12, 12, Powered)).PuzzleAt(12, 12);

        // Assert: a quoted word keeps its comma
        Assert.That(puzzle!.Name, Is.EqualTo("power"));
        Assert.That(puzzle.Args, Is.EqualTo(new[] { "vault, east", "armory" }));
    }

    [Test]
    public void A_Patchs_Switch_Has_Its_Puzzle_And_A_Plain_Switch_Or_Floor_None()
    {
        // Arrange: a patch on a plain wall
        var tiles = Tiles(map =>
        {
            map.MapData[0][At(12, 12)] = (ushort)(Plain | Panel << Constants.MapConstants.PatchShift);
            map.MapData[0][At(14, 12)] = Lever;
        });

        // Act / Assert
        Assert.That(tiles.PuzzleAt(12, 12)?.Source, Is.EqualTo("patch 2"));
        Assert.That(tiles.PuzzleAt(14, 12), Is.Null);
        Assert.That(Room().PuzzleAt(15, 15), Is.Null);
    }

    [Test]
    public void Its_Player_Stands_On_The_Floor_Beside_It_Facing_It()
    {
        // Arrange: a switch on the room's north wall (floor south of it), and one walled in
        var tiles = Room((15, 9, Hacked), (12, 12, Hacked), (12, 11, Plain), (12, 13, Plain), (11, 12, Plain), (13, 12, Plain));

        // Act / Assert
        Assert.That(tiles.StandBeside(15, 9), Is.EqualTo((15, 10, 90)));
        Assert.That(tiles.StandBeside(12, 12), Is.Null);
    }

    [Test]
    public void Trying_A_Puzzle_Starts_The_Game_There_With_It_Open()
    {
        // Act
        var arguments = GameLaunch.PuzzleArguments("wolf3d", "map01", 3, (15, 10, 90), "needle", ["hits=4", "name=two words"], ["mods/puzzles"]);

        // Assert
        Assert.That(arguments, Is.EqualTo(new[]
        {
            "--game", "wolf3d", "--nowait", "--warp", "MAP01", "--skill", "3", "--file", "mods/puzzles",
            "--cheats", "--exec", "wait 10; tp 15 10 90; puzzle needle hits=4 name=two_words",
        }));
    }

    [Test]
    public void The_Scripts_Puzzle_Names_Are_The_Ones_Written_Out()
    {
        // Act
        var names = ScriptApi.PuzzleNamesIn("""
            game.puzzle("needle", { tick = function() end })
            game.puzzle( 'keypad' , {})
            local name = "made" game.puzzle(name, {})
            -- game.puzzle"not a call"
            """).ToList();

        // Assert
        Assert.That(names, Is.EqualTo(new[] { "needle", "keypad" }));
    }

    [Test]
    public void The_Checks_Find_A_Puzzle_No_Script_Makes_And_A_Switchs_Bad_Fields()
    {
        // Arrange: no scripts at all, so needle isn't made
        var tiles = Room((12, 12, Hacked));

        // Act
        var messages = LevelChecks.Run(tiles, inGameInfo: true).Select(problem => problem.Message).ToList();

        // Assert
        Assert.That(messages, Has.Some.Contains("wall 70's puzzle \"needle\" isn't made by any script"));
        Assert.That(messages, Has.Some.Contains("wall 70's skip-item \"NoSuchTool\" isn't an actordefs class"));
        Assert.That(messages, Has.Some.Contains("wall 70's broken wall 99 isn't in the mapdefs"));
    }

    [Test]
    public void A_Switchs_Puzzle_Fields_Suggest_Puzzles_Classes_And_Actions_Each_Action_A_Row()
    {
        // Act
        var schema = FieldSchema.For(typeof(MapSwitchTranslation));

        // Assert
        Assert.That(schema.Field("puzzle")!.Hint, Is.EqualTo(FieldHint.Puzzle));
        Assert.That(schema.Field("skip-item")!.Hint, Is.EqualTo(FieldHint.Class));
        foreach (var list in new[] { "actions", "fail", "cancel", "reset-actions" })
        {
            Assert.That(schema.Field(list)!.Item!.Hint, Is.EqualTo(FieldHint.SwitchAction), list);
            Assert.That(schema.Field(list)!.IsInlineList, Is.False, $"{list}: an action's own commas would split it on one line");
        }
        var suggestions = new Editor.Definitions.DefinitionSuggestions(GameContent.FromDefinitions(new MapObjectTranslationAsset()));
        Assert.That(suggestions.For(FieldHint.SwitchAction), Does.Contain("A_Puzzle").And.Contain("A_Alarm").And.Contain("A_OpenDoor"));
    }
}
