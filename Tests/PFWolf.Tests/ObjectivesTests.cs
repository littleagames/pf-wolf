using PFWolf.Constants;
using PFWolf.Assets;
using PFWolf.Entities.Actors;

namespace PFWolf.Tests;

public class ObjectivesTests
{
    private MapManager _map = null!;
    private Objectives _objectives = null!;
    private List<string> _messages = null!;
    private List<MapSwitchTranslation> _switches = null!;
    private Dictionary<string, ObjectiveInfo> _settings = null!;

    [SetUp]
    public void Make()
    {
        var session = new GameSession();
        _map = new MapManager(new Lazy<AssetManager>(() => throw new InvalidOperationException("no assets in this test")), null!, session, null)
        {
            TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
            ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize],
        };
        _messages = [];
        _switches = [];
        _settings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["sabotage"] = new ObjectiveInfo
            {
                BlocksExit = true,
                Start = "There are %d to sabotage.",
                Progress = "Sabotaged: %d of %d",
                Done = "All sabotaged.",
                Refused = "%d left to sabotage.",
                Stats = "Sabotages: %d of %d",
            },
            ["targets"] = new ObjectiveInfo { Start = "%d targets.", StartFrom = 2, Progress = "Defeated: %d of %d" },
        };
        _objectives = new Objectives(_map, new ObjectiveHooks(() => _settings, text => text, _messages.Add, () => _switches));
    }

    private static MapSwitchTranslation Panel() => new() { To = 27, Objective = "sabotage" };

    private Monster Target(short health)
    {
        var tank = new Monster { Name = "GoalTank", Hitpoints = health };
        tank.Properties["objective"] = "targets";
        _map.GetActors().AddLast(tank);
        return tank;
    }

    // ==== Markers =================================================================================

    // Walls: 5 a sabotage panel (thrown, it's 6), 7 a destructible wall toward "radios"
    private void MarkerLevel()
    {
        var data = new MapObjectTranslationAsset
        {
            Walls =
            {
                [5] = new MapTextureTranslation { North = "PANEL", Switch = new MapSwitchTranslation { To = 6, Objective = "sabotage" } },
                [6] = new MapTextureTranslation { North = "PANELX" },
                [7] = new MapTextureTranslation { North = "RADIO", Destructible = new MapDestructibleTranslation { Objective = "radios" } },
            },
        };
        typeof(MapManager).GetField("_levelMapData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(_map, data);
        _map.TileMap[3, 4] = 5;
        _map.TileMap[8, 4] = 6;       // one thrown already
        _map.TileMap[10, 12] = 7;
        _settings["sabotage"] = _settings["sabotage"] with { Marker = true, MarkerLabel = "Panel", MarkerColor = "Yellow" };
        _settings["targets"] = _settings["targets"] with { Marker = true };
        _settings["radios"] = new ObjectiveInfo { Marker = true, MarkerLabel = "Radio" };
        _objectives = new Objectives(_map, new ObjectiveHooks(() => _settings, text => text, _messages.Add, () => _switches,
            WallObjectiveAt: (x, y) => _map.TileMap[x, y] == 7 ? "radios" : null));
        _map.ObjectiveCounts["sabotage"] = new ObjectiveCount { Done = 1, Total = 2 };
        _map.ObjectiveCounts["radios"] = new ObjectiveCount { Total = 1 };
    }

    [Test]
    public void Marked_Objectives_Mark_Their_Switches_Not_Yet_Thrown_Their_Walls_And_Their_Living_Actors()
    {
        // Arrange: a living target and a dead one
        MarkerLevel();
        var tank = Target(100);
        (tank.X, tank.Y) = (0x50000, 0x60000);
        Target(0);
        _map.ObjectiveCounts["targets"] = new ObjectiveCount { Done = 1, Total = 2 };

        // Act
        var markers = _objectives.Markers().ToList();

        // Assert
        Assert.That(markers, Is.EqualTo(new[]
        {
            ObjectiveMarker.OnTile((3, 4), "Panel", -1, color: "Yellow"),
            ObjectiveMarker.OnTile((10, 12), "Radio", -1),
            new ObjectiveMarker(0x50000, 0x60000, "", -1),
        }));
    }

    [Test]
    public void An_Objective_All_Done_Or_Not_Marked_Has_No_Markers()
    {
        MarkerLevel();
        _map.ObjectiveCounts["sabotage"].Done = 2;
        _settings["radios"] = _settings["radios"] with { Marker = false };

        Assert.That(_objectives.Markers(), Is.Empty);
    }

    [Test]
    public void A_Switch_Thrown_Loses_Its_Marker()
    {
        // Arrange
        MarkerLevel();
        Assert.That(_objectives.Markers().Count(m => m.Label == "Panel"), Is.EqualTo(1));

        // Act: thrown, it's the other wall, and one more's done (with one left to do elsewhere)
        _map.TileMap[3, 4] = 6;
        _map.ObjectiveCounts["sabotage"] = new ObjectiveCount { Done = 2, Total = 3 };

        // Assert
        Assert.That(_objectives.Markers().Count(m => m.Label == "Panel"), Is.Zero);
    }

    [Test]
    public void A_Level_Counts_Its_Objective_Switches_And_Actors_And_Says_So()
    {
        // Arrange: three panels, a plain switch, two living targets and a dead one
        _switches.AddRange([Panel(), Panel(), new MapSwitchTranslation { To = 20 }, Panel()]);
        Target(100);
        Target(100);
        Target(0);

        // Act
        _objectives.CountLevel(announce: true);

        // Assert
        Assert.That((_objectives.Count("sabotage").Total, _objectives.Count("targets").Total), Is.EqualTo((3, 2)));
        Assert.That(_messages, Is.EqualTo(new[] { "There are 3 to sabotage.", "2 targets." }));
    }

    [Test]
    public void Each_One_Done_Is_Told_To_The_Scripts()
    {
        // Arrange
        var told = new List<(string, int, int)>();
        _objectives = new Objectives(_map, new ObjectiveHooks(() => _settings, text => text, _messages.Add, () => _switches,
            (name, done, total) => told.Add((name, done, total))));
        _switches.AddRange([Panel(), Panel()]);
        _objectives.CountLevel(announce: false);

        // Act
        _objectives.Complete("sabotage");
        _objectives.Complete("sabotage");
        _objectives.Complete("sabotage");     // all done already: nothing

        // Assert
        Assert.That(told, Is.EqualTo(new[] { ("sabotage", 1, 2), ("sabotage", 2, 2) }));
    }

    [Test]
    public void How_Its_Going_Is_Told_To_Everyone_But_A_Refused_Exit_Only_To_Whoever_Tried()
    {
        // Arrange: messages to everyone kept apart from the acting player's
        var everyone = new List<string>();
        _objectives = new Objectives(_map, new ObjectiveHooks(() => _settings, text => text, _messages.Add, () => _switches,
            Announce: everyone.Add));
        _switches.AddRange([Panel(), Panel()]);
        _objectives.CountLevel(announce: true);

        // Act
        _objectives.Complete("sabotage");
        _objectives.ExitAllowed();

        // Assert
        Assert.That(everyone, Is.EqualTo(new[] { "There are 2 to sabotage.", "Sabotaged: 1 of 2" }));
        Assert.That(_messages, Is.EqualTo(new[] { "1 left to sabotage." }));
    }

    [Test]
    public void One_Target_Isnt_Announced_Below_Its_Start_From()
    {
        Target(100);
        _objectives.CountLevel(announce: true);
        Assert.That(_messages, Is.Empty);
    }

    [Test]
    public void Each_One_Done_Says_How_Many_And_The_Last_Says_All_Done()
    {
        // Arrange
        _switches.AddRange([Panel(), Panel()]);
        _objectives.CountLevel(announce: false);

        // Act
        _objectives.Complete("sabotage");
        _objectives.Complete("sabotage");
        _objectives.Complete("sabotage");      // nothing left: ignored

        // Assert
        Assert.That(_messages, Is.EqualTo(new[] { "Sabotaged: 1 of 2", "Sabotaged: 2 of 2", "All sabotaged." }));
        Assert.That(_objectives.Count("sabotage").Done, Is.EqualTo(2));
    }

    [Test]
    public void The_Exits_Wait_For_A_Blocks_Exit_Objective()
    {
        // Arrange
        _switches.AddRange([Panel(), Panel(), Panel()]);
        Target(100);
        _objectives.CountLevel(announce: false);
        _objectives.Complete("sabotage");

        // Act / Assert: two left, said so; the targets don't block
        Assert.That(_objectives.ExitAllowed(), Is.False);
        Assert.That(_messages.Last(), Is.EqualTo("2 left to sabotage."));
        _objectives.Complete("sabotage", 2);
        Assert.That(_objectives.ExitAllowed(), Is.True);
    }

    [Test]
    public void Switches_Done_Before_The_Level_Count_As_Done()
    {
        // Arrange: two Kilroys still to draw, and one drawn in an earlier game (shown drawn already)
        _switches.AddRange([new MapSwitchTranslation { To = 2, Objective = "kilroys" }, new MapSwitchTranslation { To = 2, Objective = "kilroys" }]);

        // Act
        _objectives.CountLevel(announce: false, doneBefore: ["kilroys", " "]);

        // Assert
        Assert.That((_objectives.Count("kilroys").Done, _objectives.Count("kilroys").Total), Is.EqualTo((1, 3)));
    }

    [TestCase("{map}-kilroy-{tag}", "CAMP01", 3, "CAMP01-kilroy-3")]
    [TestCase("{MAP}:{Tag}", "DT02", 12, "DT02:12")]
    [TestCase("plain", "CAMP01", 3, "plain")]
    public void A_Switches_Unlock_Name_Fills_In_The_Map_And_Tag(string pattern, string map, int tag, string expected)
    {
        Assert.That(PlayerUse.SwitchUnlockName(pattern, map, (ushort)tag), Is.EqualTo(expected));
    }

    [Test]
    public void Exit_Block_Says_Why_Without_Showing_It()
    {
        // Arrange
        _switches.AddRange([Panel(), Panel()]);
        _objectives.CountLevel(announce: false);
        int shown = _messages.Count;

        // Act / Assert
        Assert.That(_objectives.ExitBlock(), Is.EqualTo((true, (string?)"2 left to sabotage.")));
        Assert.That(_messages, Has.Count.EqualTo(shown));
        _objectives.Complete("sabotage", 2);
        Assert.That(_objectives.ExitBlock(), Is.EqualTo((false, (string?)null)));
    }

    [TestCase("A_Exit", true, "2 left", "2 left")]                 // a shut exit says why
    [TestCase("A_SecretExit", true, "2 left", "2 left")]
    [TestCase("A_Exit", false, null, "Press Use to Exit Level")]   // an open one, its own
    [TestCase("A_PushWall", true, "2 left", "Press Use to Exit Level")]   // not an exit
    public void A_Use_Triggers_Facing_Message(string action, bool blocked, string? refused, string expected)
    {
        var trigger = new MapTriggerTranslation { Action = action, FacingMessage = "Press Use to Exit Level" };
        Assert.That(PlayerUse.FacingText(trigger, (blocked, refused)), Is.EqualTo(expected));
    }

    [Test]
    public void A_Targets_Death_Completes_One_And_The_Last_Can_Jump()
    {
        // Arrange: two targets, one with a Goal state to go to once all are down
        var first = Target(100);
        var last = Target(100);
        last.ResolvedStates["Goal"] = new ActorStateFrame { StateName = "Goal", Sprite = "TANK", FrameLetter = "N", TicTime = -1 };
        _objectives.CountLevel(announce: false);

        // Act / Assert
        _objectives.ActorDied(first);
        _objectives.JumpIfObjectiveDone(last, ["targets", "Goal"]);
        Assert.That(last.CurrentState?.StateName, Is.Not.EqualTo("Goal"));
        _objectives.ActorDied(last);
        _objectives.JumpIfObjectiveDone(last, ["targets", "Goal"]);
        Assert.That(last.CurrentState?.StateName, Is.EqualTo("Goal"));
        Assert.That(_messages, Is.EqualTo(new[] { "Defeated: 1 of 2", "Defeated: 2 of 2" }));
    }

    [Test]
    public void A_Scripts_Own_Objective_Can_Be_Added_To()
    {
        _objectives.Add("intel", 3);
        _objectives.Complete("intel");
        Assert.That((_objectives.Count("intel").Done, _objectives.Count("intel").Total), Is.EqualTo((1, 3)));
        Assert.That(_objectives.IsDone("intel"), Is.False);
        Assert.That(_objectives.IsDone("nothing"), Is.True);
    }

    [Test]
    public void The_Automap_Shows_Each_Objective_With_A_Stats_Line()
    {
        _switches.AddRange([Panel(), Panel()]);
        Target(100);
        _objectives.CountLevel(announce: false);
        _objectives.Complete("sabotage");
        Assert.That(_objectives.StatsLines(), Is.EqualTo(new[] { "Sabotages: 1 of 2" }));
    }

    [Test]
    public void Game_Info_Reads_Objectives()
    {
        var info = PFWolf.Loaders.YamlDataEntryLoader.Deserialize<GameInfoAsset>(
            "objectives:\n  sabotage: { blocks-exit: true, refused: \"$STR_SABOTAGE_ITEMSLEFT\" }\n  targets: { start-from: 2 }\n");
        Assert.That(info.Objectives["sabotage"].BlocksExit, Is.True);
        Assert.That(info.Objectives["sabotage"].Refused, Is.EqualTo("$STR_SABOTAGE_ITEMSLEFT"));
        Assert.That(info.Objectives["targets"].StartFrom, Is.EqualTo(2));
    }
}
