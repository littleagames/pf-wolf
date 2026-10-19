using PFWolf.Editor.Editing;
using PFWolf.Entities.Actors;
using PFWolf.Loaders;
using static PFWolf.Tests.HeadlessGame;

namespace PFWolf.Tests;

/// <summary>A reviver's count: a number, or "min-max" (Monster.ParseCountRange)</summary>
public class ReviveCountTests
{
    [TestCase("1", 1, 1)]
    [TestCase(" 3 ", 3, 3)]
    [TestCase("1-3", 1, 3)]
    [TestCase("2 - 4", 2, 4)]
    [TestCase("0", 0, 0)]
    public void A_Count_Is_A_Number_Or_A_Range(string text, int min, int max)
    {
        // Act
        var range = Monster.ParseCountRange(text);

        // Assert
        Assert.That(range, Is.EqualTo((min, max)));
    }

    [TestCase("")]
    [TestCase("two")]
    [TestCase("-1")]
    [TestCase("3-1")]
    [TestCase("1-")]
    public void Anything_Else_Isnt_One(string text)
    {
        // Act
        var range = Monster.ParseCountRange(text);

        // Assert
        Assert.That(range, Is.Null);
    }
}

/// <summary>
/// Revivers in play (Wolf3D): a mod puts a one-room arena in place of MAP01 with a medic beside
/// a guard of its side, a scripted demo has the player shoot the guard, and a script prints
/// everyone's side, health and state as it goes. Needs Wolf3D's data files, as
/// DemoRegressionTests does.
/// </summary>
[Category("GameData")]
public class ReviveGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-revive-" + Guid.NewGuid().ToString("N"));
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

    // The arena's things (mapdefs things of the mod): on a side neutral to everyone, a guard
    // with its back to the player, one that can't be revived, and a medic; on the axis, a guard
    // and a tough medic facing west; on the allies' side, a tough gunner facing east, shooting
    // from where it stands
    private const int NeutralGuard = 500, NeutralStiff = 501, NeutralMedic = 502;
    private const int AxisGuard = 503, AxisMedic = 504, AllyGunner = 505;
    private const int PlayerFacingEast = 20;

    // The player's shots: three, half a second apart, then a wait
    private static readonly DemoStep[] ThreeShotsThenWait =
        [.. Enumerable.Range(0, 3).SelectMany(_ => new[] { new DemoStep(Attack, 0, 0, 10), new DemoStep(0, 0, 0, 25) }),
         new DemoStep(0, 0, 0, 500)];

    [Test]
    public void A_Medic_Brings_Back_A_Guard_Of_Its_Side()
    {
        // Arrange: the player two tiles behind a guard; the guard's medic off to the side, out
        // of the line of fire
        var mod = WriteArena((11, 32, PlayerFacingEast), (13, 32, NeutralGuard), (14, 30, NeutralMedic));

        // Act
        var (census, result) = Play(mod, ThreeShotsThenWait);

        // Assert: it died, and is standing again at full health; off the kill count again
        Assert.That(census.Any(line => Health(line, "Guard:wanderers") <= 0), Is.True, "The guard was never killed");
        var last = census[^1];
        Assert.That(last, Does.Contain("Guard:wanderers:25:Spawn"), last);
        Assert.That(Kills(result), Is.Zero, "a revived guard isn't a kill any more");
    }

    [Test]
    public void A_Norevive_Corpse_Stays_Dead()
    {
        // Arrange: as above, with a guard that can't be revived
        var mod = WriteArena((11, 32, PlayerFacingEast), (13, 32, NeutralStiff), (14, 30, NeutralMedic));

        // Act
        var (census, result) = Play(mod, ThreeShotsThenWait);

        // Assert
        var last = census[^1];
        Assert.That(Health(last, "Stiff:wanderers"), Is.LessThanOrEqualTo(0), last);
        Assert.That(Kills(result), Is.EqualTo(1));
    }

    [Test]
    public void A_Medic_Keeps_Its_Side_In_A_Fight()
    {
        // Arrange: an allied gunner between the player and an axis guard (it never moves, so it
        // can't stand on the corpse), with the guard's medic behind it (tough enough to outlast
        // the guard); the player stands still
        var mod = WriteArena((10, 32, PlayerFacingEast), (12, 32, AllyGunner), (17, 32, AxisGuard), (19, 31, AxisMedic));

        // Act
        var (census, _) = Play(mod, new DemoStep(0, 0, 0, 2100));

        // Assert: the axis guard went down, and was up again later, back in the fight
        int died = census.FindIndex(line => Health(line, "Guard:axis") <= 0);
        Assert.That(died, Is.GreaterThanOrEqualTo(0), $"The axis guard never died: {census[^1]}");
        var revived = census.Skip(died).FirstOrDefault(line => Health(line, "Guard:axis") > 0
            && !line.Contains("Guard:axis:25:Raise") && !line.Contains("Guard:axis:25:Spawn"));
        Assert.That(revived, Is.Not.Null, $"It was never revived into the fight: {string.Join(Environment.NewLine, census)}");
    }

    [Test]
    public void The_Medic_Demo_Puts_Everyone_On_Their_Side()
    {
        // Arrange: examples/mods/medic-demo, with a census of its own alongside
        var demo = Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "medic-demo");
        var census = Path.Combine(_workFolder, "census");
        Directory.CreateDirectory(Path.Combine(census, "scripts"));
        File.WriteAllText(Path.Combine(census, "modinfo.yaml"), "name: Census\nversion: \"1\"\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(census, "scripts", "census.lua"), CensusScript);
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO1.dmo"), ScriptedDemo(0, new DemoStep(0, 0, 0, 140)));

        // Act
        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", "demotest 1; quit", Timeout, demo, census);

        // Assert
        var log = string.Join(Environment.NewLine, output.TakeLast(30));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(output, Has.None.Contains("isn't in factions.yaml").And.None.Contains("isn't in translations.yaml")
            .And.None.Contains("left out").And.None.Contains("monster.revive").And.None.Contains("Exception"), log);
        var first = output.First(line => line.StartsWith("CENSUS "));
        int Count(string who) => first.Split(' ').Count(part => part.StartsWith(who + ":"));
        Assert.That(Count("SquadGuard:allies"), Is.EqualTo(2), first);
        Assert.That(Count("SquadMedic:allies"), Is.EqualTo(1), first);
        Assert.That(Count("Medic:axis"), Is.EqualTo(1), first);
        Assert.That(Count("Surgeon:axis"), Is.EqualTo(1), first);
        Assert.That(Count("Guard:axis"), Is.EqualTo(12), first);
        var result = output.First(line => line.StartsWith("demotest 1:"));
        Assert.That(KillTotal(result), Is.EqualTo(14), "the axis, not the squad");
    }

    // Prints, each second, every actor on a side: class:faction:health:state:TileX
    private const string CensusScript = """
        local due = 0
        game.ontick(function(tics)
          if game.time() < due then return end
          due = game.time() + 70
          local parts = {}
          for _, a in ipairs(game.actors()) do
            if a.faction ~= nil then
              parts[#parts + 1] = a.name .. ":" .. a.faction .. ":" .. a.health .. ":" .. tostring(a.state) .. ":" .. a.tilex
            end
          end
          print("CENSUS t=" .. game.time() .. " " .. table.concat(parts, " "))
        end)
        """;

    /// <summary>
    /// A mod in the work folder: MAP01 as one room (x 10-22, y 30-34) with the things given at
    /// their tiles; the mod's things, factions, actors and census script
    /// </summary>
    private string WriteArena(params (int X, int Y, int Thing)[] things)
    {
        var mod = Path.Combine(_workFolder, "arena");
        Directory.CreateDirectory(Path.Combine(mod, "maps"));
        Directory.CreateDirectory(Path.Combine(mod, "mapdefs"));
        Directory.CreateDirectory(Path.Combine(mod, "actordefs"));
        Directory.CreateDirectory(Path.Combine(mod, "scripts"));

        var map = MapFiles.NewMap("Arena", wall: 1, floor: 1);
        for (int y = 30; y <= 34; y++)
            for (int x = 10; x <= 22; x++)
                map.MapData[0][y * 64 + x] = 108;       // area 1
        foreach (var (x, y, thing) in things)
            map.MapData[1][y * 64 + x] = (ushort)thing;
        File.WriteAllBytes(Path.Combine(mod, "maps", "MAP01.wad"), EcWolfMapLoader.Save(map, "MAP01"));

        File.WriteAllText(Path.Combine(mod, "modinfo.yaml"), "name: Arena\nversion: \"1\"\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(mod, "factions.yaml"), "factions:\n  wanderers:\n    default: neutral\n");
        File.WriteAllText(Path.Combine(mod, "actordefs", "medics.yaml"), """
            Medic:
              parent: Guard
              properties:
                monster.revive.radius: 2
              states:
                Spawn:
                  - sprite: GARD
                    frames: [A]
                    tics-per-frame: -1
                    think: T_ReviveStand
                Chase:
                  - sprite: GARD
                    frames: [B, C, D, E]
                    tics-per-frame: 8
                    think: T_ReviveChase
                Heal:
                  - sprite: GARD
                    frames: [A]
                    tics-per-frame: 20
                    action: A_Revive
                  - sprite: GARD
                    frames: [A]
                    tics-per-frame: 10
                    action: A_Resume
            Stiff:
              parent: Guard
              flags: [NOREVIVE]
            Gunner:
              parent: Guard
              properties:
                health: 1000
              states:
                Chase:
                  - sprite: GARD
                    frames: [A]
                    tics-per-frame: 20
                    next-state: Attack
            ToughMedic:
              parent: Medic
              properties:
                health: 1000
            """);
        File.WriteAllText(Path.Combine(mod, "mapdefs", "arena.yaml"), $$"""
            things:
              {{NeutralGuard}}: { class: Guard, angles: 0, faction: wanderers }
              {{NeutralStiff}}: { class: Stiff, angles: 0, faction: wanderers }
              {{NeutralMedic}}: { class: Medic, angles: 270, faction: wanderers }
              {{AxisGuard}}: { class: Guard, angles: 180 }
              {{AxisMedic}}: { class: ToughMedic, angles: 180 }
              {{AllyGunner}}: { class: Gunner, angles: 0, faction: allies }
            """);
        File.WriteAllText(Path.Combine(mod, "scripts", "census.lua"), CensusScript);
        return mod;
    }

    // Plays the steps on MAP01 with the mod: the census lines, and demotest's result line
    private (List<string> Census, string Result) Play(string mod, params DemoStep[] steps)
    {
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO1.dmo"), ScriptedDemo(0, steps));

        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", "demotest 1; quit", Timeout, mod);
        var log = string.Join(Environment.NewLine, output.TakeLast(30));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(output, Has.None.Contains("Exception"), log);

        var census = output.Where(line => line.StartsWith("CENSUS ")).ToList();
        var result = output.FirstOrDefault(line => line.StartsWith("demotest 1:"));
        Assert.That(census, Is.Not.Empty, log);
        Assert.That(result, Is.Not.Null, log);
        TestContext.Out.WriteLine(string.Join(Environment.NewLine, census));
        TestContext.Out.WriteLine(result);
        return (census, result!);
    }

    // An actor's health in a census line ("Guard:axis" -> its number; the first such)
    private static int Health(string census, string who) =>
        census.Split(' ').Where(part => part.StartsWith(who + ":")).Select(part => int.Parse(part.Split(':')[2])).First();

    // demotest's "kills 1/2"
    private static string[] Field(string result, string name)
    {
        var words = result.Split(' ');
        return words[Array.IndexOf(words, name) + 1].Split('/');
    }

    private static int Kills(string result) => int.Parse(Field(result, "kills")[0]);
    private static int KillTotal(string result) => int.Parse(Field(result, "kills")[1]);
}
