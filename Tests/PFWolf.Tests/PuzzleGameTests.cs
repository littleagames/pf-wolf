using PFWolf.Constants;
using PFWolf.Editor.Editing;
using PFWolf.Loaders;
using static PFWolf.Tests.HeadlessGame;

namespace PFWolf.Tests;

/// <summary>
/// A puzzle switch played from a demo (Wolf3D): the player uses a switch with a `puzzle:`, works
/// it with the controls a demo keeps, wins it (the switch throws), then fails the next one (its
/// fail actions hurt them) and finds it locked out. Needs Wolf3D's data files, as
/// DemoRegressionTests does.
/// </summary>
[Category("GameData")]
public class PuzzleGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);
    private const int PlayerFacingEast = 20, SwitchWall = 70;

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-puzzles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workFolder);
        HeadlessGame.PrepareFolder(_workFolder, "wolf3d");
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(_workFolder, recursive: true);
        }
        catch (IOException)
        {
            // The game may still be letting go of a file; it's only a temp folder
        }
    }

    // Use wins it, fire fails it; it tells each step, and the player's health each half second
    private const string Script = """
        game.puzzle("press", {
          start = function(pz, args) print("PUZZLE open " .. pz.x .. " " .. pz.y .. " " .. (args.need or "-")) end,
          tick = function(pz, input)
            if input.pressed.right then print("PUZZLE right") end
            if input.pressed.use then return "success" end
            if input.pressed.fire then return "fail" end
          end,
        })
        game.onpuzzle(function(player, name, result) print("PUZZLE " .. result) end)
        local due = 0
        game.ontick(function()
          if game.time() < due then return end
          due = game.time() + 35
          print("HEALTH " .. game.players()[1].health)
        end)
        """;

    // 70 is won by the puzzle and turns into 71, whose puzzle failed hurts and locks it out
    private const string Walls = """
        walls:
          '70':
            North: ELEV1_2
            South: ELEV1_2
            East: ELEV1_2
            West: ELEV1_2
            switch:
              to: 71
              puzzle: press
              puzzle-args: { need: 1 }
              actions:
                - A_Message("THROWN")
          '71':
            North: ELEV2_2
            South: ELEV2_2
            East: ELEV2_2
            West: ELEV2_2
            switch:
              to: 70
              puzzle: press
              retry: 5000
              retry-message: LOCKEDOUT
              fail:
                - A_Message("FAILED")
                - A_HurtPlayer(10)
        """;

    [Test]
    public void A_Puzzle_Switch_Is_Thrown_By_A_Win_And_Fails_Into_Its_Fail_Actions_And_Lockout()
    {
        // Arrange: the player facing the switch, a tile east of them
        var mod = WriteMod();
        var steps = new[]
        {
            new DemoStep(0, 0, 0, 10),
            new DemoStep(Use, 0, 0, 4),         // opens 70's puzzle
            new DemoStep(0, 0, 0, 4),
            new DemoStep(0, 40, 0, 4),          // turning: right, to the puzzle
            new DemoStep(0, 0, 0, 4),
            new DemoStep(Use, 0, 0, 4),         // won: 70 is thrown into 71
            new DemoStep(0, 0, 0, 10),
            new DemoStep(Use, 0, 0, 4),         // opens 71's puzzle
            new DemoStep(0, 0, 0, 4),
            new DemoStep(Attack, 0, 0, 4),      // failed: hurt, locked out
            new DemoStep(0, 0, 0, 10),
            new DemoStep(Use, 0, 0, 4),         // still locked out
            new DemoStep(0, 0, 0, 80),
        };

        // Act
        var lines = Play(mod, steps);

        // Assert: in the order they happened, each switch's actions before the scripts hear the result
        Assert.That(lines, Is.SupersetOf(new[]
        {
            "PUZZLE open 12 32 1", "PUZZLE right", "THROWN", "PUZZLE success",
            "PUZZLE open 12 32 -", "FAILED", "PUZZLE fail", "LOCKEDOUT",
        }));
        Assert.That(Order(lines, "PUZZLE open 12 32 1", "PUZZLE right", "THROWN", "PUZZLE success", "PUZZLE open 12 32 -", "FAILED", "PUZZLE fail", "LOCKEDOUT"),
            Is.Ordered);
        Assert.That(lines.Count(line => line.StartsWith("PUZZLE open")), Is.EqualTo(2), "the lockout opened it again");
        Assert.That(lines.Last(line => line.StartsWith("HEALTH ")), Is.EqualTo("HEALTH 90"));
    }

    private static List<int> Order(List<string> lines, params string[] wanted) => [.. wanted.Select(w => lines.IndexOf(w))];

    // A guard down the room with its back to the player (out of sight), tagged 5: a won puzzle
    // puts it on the player's side, a failed one sounds the alarm
    private const string GuardWalls = """
        walls:
          '70':
            North: ELEV1_2
            South: ELEV1_2
            East: ELEV1_2
            West: ELEV1_2
            switch:
              to: 71
              puzzle: press
              actions:
                - A_SetFaction("allies")
              fail:
                - A_Alarm
          '71':
            North: ELEV2_2
            South: ELEV2_2
            East: ELEV2_2
            West: ELEV2_2
        """;

    private const string GuardScript = Script + """

        game.onsight(function(monster, target) print("PUZZLE sight " .. monster.name) end)
        game.onfactionchange(function(actor, from, to) print("PUZZLE faction " .. actor.name .. " " .. tostring(to)) end)
        """;

    private const int GuardFacingEast = 108, GuardX = 20;
    private const ushort GuardTag = 5;

    [TestCase(Use, "PUZZLE faction Guard allies", "PUZZLE sight Guard")]
    [TestCase(Attack, "PUZZLE sight Guard", "PUZZLE faction Guard allies")]
    public void A_Won_Switch_Puts_The_Tagged_Guard_On_Your_Side_And_A_Failed_One_Sounds_The_Alarm(byte answer, string happens, string doesnt)
    {
        // Arrange
        var mod = WriteMod(GuardWalls, GuardScript, guard: true);
        var steps = new[]
        {
            new DemoStep(0, 0, 0, 10),
            new DemoStep(Use, 0, 0, 4),         // opens the puzzle
            new DemoStep(0, 0, 0, 4),
            new DemoStep(answer, 0, 0, 4),      // use wins it, fire fails it
            new DemoStep(0, 0, 0, 140),
        };

        // Act
        var lines = Play(mod, steps);

        // Assert
        Assert.That(lines, Has.Some.EqualTo(happens));
        Assert.That(lines, Has.None.EqualTo(doesnt));
    }

    private string WriteMod() => WriteMod(Walls, Script, guard: false);

    private string WriteMod(string walls, string script, bool guard)
    {
        var mod = Path.Combine(_workFolder, "puzzles");
        foreach (var folder in new[] { "maps", "mapdefs", "scripts" })
            Directory.CreateDirectory(Path.Combine(mod, folder));

        // One room (x 10-22, y 30-34), the player at 11,32 facing east at the switch at 12,32
        var map = MapFiles.NewMap("Puzzles", wall: 1, floor: 1);
        for (int y = 30; y <= 34; y++)
            for (int x = 10; x <= 22; x++)
                map.MapData[0][y * 64 + x] = 108;
        map.MapData[0][32 * 64 + 12] = SwitchWall;
        map.MapData[1][32 * 64 + 11] = PlayerFacingEast;
        if (guard)
        {
            map.MapData[1][32 * 64 + GuardX] = GuardFacingEast;
            map.MapData[MapConstants.TagPlane][32 * 64 + GuardX] = GuardTag;
            map.MapData[MapConstants.TagPlane][32 * 64 + 12] = GuardTag;     // the switch's actions act on its own tile's tag
        }
        File.WriteAllBytes(Path.Combine(mod, "maps", "MAP01.wad"), EcWolfMapLoader.Save(map, "MAP01"));

        File.WriteAllText(Path.Combine(mod, "modinfo.yaml"), "name: Puzzles\nversion: \"1\"\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(mod, "mapdefs", "walls.yaml"), walls);
        File.WriteAllText(Path.Combine(mod, "scripts", "puzzles.lua"), script);
        return mod;
    }

    // Plays the steps on MAP01 with the mod and messages on: the PUZZLE, HEALTH and message lines
    private List<string> Play(string mod, params DemoStep[] steps)
    {
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO1.dmo"), ScriptedDemo(0, steps));

        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", "msg_enabled 1; demotest 1; quit", Timeout, mod);
        var log = string.Join(Environment.NewLine, output.TakeLast(40));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(output, Has.None.Contains("Exception").And.None.StartsWith("Script "), log);

        string[] messages = ["THROWN", "FAILED", "LOCKEDOUT"];
        var lines = output.Where(line => line.StartsWith("PUZZLE ") || line.StartsWith("HEALTH ") || messages.Contains(line)).ToList();
        foreach (var line in lines)
            TestContext.Out.WriteLine(line);
        Assert.That(lines, Is.Not.Empty, log);
        return lines;
    }
}
