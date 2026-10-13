using PFWolf.Editor.Editing;
using PFWolf.Loaders;
using static PFWolf.Tests.HeadlessGame;

namespace PFWolf.Tests;

/// <summary>
/// Factions in play (Wolf3D): a mod puts a one-room arena in place of MAP01 with guards on
/// different sides, a scripted demo plays it, and a script prints everyone's side and health
/// as it goes. Needs Wolf3D's data files, as DemoRegressionTests does.
/// </summary>
[Category("GameData")]
public class FactionGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-factions-" + Guid.NewGuid().ToString("N"));
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

    // The arena's things (mapdefs things of the mod): guards on the players' side facing either
    // way, and a tough guard of a side that's neutral to everyone
    private const int AllyFacingEast = 500, AllyFacingWest = 501, NeutralFacingWest = 502;
    private const int AxisGuardEast = 108, AxisGuardWest = 110, PlayerFacingEast = 20;

    [Test]
    public void Guards_On_Opposite_Sides_Fight_When_They_See_Each_Other()
    {
        // Arrange: the player at the west end behind an allied guard facing east at an axis
        // guard facing west, and the player standing still
        var mod = WriteArena((11, PlayerFacingEast), (14, AllyFacingEast), (19, AxisGuardWest));

        // Act
        var (census, result) = Play(mod, new DemoStep(0, 0, 0, 1400));

        // Assert: one of them is dead, and an axis guard killed by the ally scores nothing
        var last = census[^1];
        bool allyDead = Health(last, "Guard:allies") <= 0, axisDead = Health(last, "Guard:axis") <= 0;
        Assert.That(allyDead || axisDead, Is.True, $"Nobody died: {last}");
        Assert.That(KillTotal(result), Is.EqualTo(1), "the allied guard isn't in the kill total");
        if (axisDead)
        {
            Assert.That(Kills(result), Is.EqualTo(1), "the axis guard counts as a kill whoever killed it");
            Assert.That(Score(result), Is.Zero, "no points for an enemy one of the player's allies killed");
        }
    }

    [Test]
    public void The_Players_Shots_Pass_Their_Allies_By()
    {
        // Arrange: an allied guard two tiles in front of the player, facing them, and no enemies
        var mod = WriteArena((11, PlayerFacingEast), (13, AllyFacingWest));
        var steps = Enumerable.Range(0, 8).SelectMany(_ => new[] { new DemoStep(Attack, 0, 0, 10), new DemoStep(0, 0, 0, 20) })
            .Append(new DemoStep(0, 0, 0, 140)).ToArray();

        // Act
        var (census, _) = Play(mod, steps);

        // Assert: unhurt, and, with no one to go after, not roused by the noise
        var last = census[^1];
        Assert.That(Health(last, "Guard:allies"), Is.EqualTo(25), last);
        Assert.That(last, Does.Contain("Guard:allies:25:Spawn"), last);
    }

    [Test]
    public void A_Neutral_Side_Leaves_The_Player_Be_Until_Hurt()
    {
        // Arrange: a tough neutral guard facing the player three tiles away; the player waits,
        // then shoots it
        var mod = WriteArena((11, PlayerFacingEast), (14, NeutralFacingWest));
        var steps = new[] { new DemoStep(0, 0, 0, 420) }
            .Concat(Enumerable.Range(0, 8).SelectMany(_ => new[] { new DemoStep(Attack, 0, 0, 10), new DemoStep(0, 0, 0, 20) }))
            .Append(new DemoStep(0, 0, 0, 700)).ToArray();

        // Act
        var (census, _) = Play(mod, steps);

        // Assert: left alone while waiting; shot, it turns on the player
        var waited = census.Last(line => Time(line) <= 400);
        Assert.That(Health(waited, "Player:allies"), Is.EqualTo(100), waited);
        Assert.That(waited, Does.Contain("Brute:wanderers:400:Spawn"), waited);

        var last = census[^1];
        Assert.That(Health(last, "Brute:wanderers"), Is.LessThan(400), last);
        Assert.That(Health(last, "Player:allies"), Is.LessThan(100), $"It never fought back: {last}");
    }

    /// <summary>
    /// A mod in the work folder: MAP01 as one room (x 10-22, y 30-34) with the things given along
    /// its middle row (y 32), at their x; the mod's things, factions and census script
    /// </summary>
    private string WriteArena(params (int X, int Thing)[] things)
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
        foreach (var (x, thing) in things)
            map.MapData[1][32 * 64 + x] = (ushort)thing;
        File.WriteAllBytes(Path.Combine(mod, "maps", "MAP01.wad"), EcWolfMapLoader.Save(map, "MAP01"));

        File.WriteAllText(Path.Combine(mod, "modinfo.yaml"), "name: Arena\nversion: \"1\"\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(mod, "factions.yaml"), "factions:\n  wanderers:\n    default: neutral\n");
        File.WriteAllText(Path.Combine(mod, "actordefs", "brute.yaml"),
            "Brute:\n  parent: Guard\n  properties:\n    health: 400\n    faction: wanderers\n");
        File.WriteAllText(Path.Combine(mod, "mapdefs", "arena.yaml"), $$"""
            things:
              {{AllyFacingEast}}: { class: Guard, angles: 0, faction: allies }
              {{AllyFacingWest}}: { class: Guard, angles: 180, faction: allies }
              {{NeutralFacingWest}}: { class: Brute, angles: 180 }
            """);
        File.WriteAllText(Path.Combine(mod, "scripts", "census.lua"), """
            local due = 0
            game.ontick(function(tics)
              if game.time() < due then return end
              due = game.time() + 70
              local parts = {}
              for _, a in ipairs(game.actors()) do
                if a.faction ~= nil then
                  parts[#parts + 1] = a.name .. ":" .. a.faction .. ":" .. a.health .. ":" .. tostring(a.state)
                end
              end
              print("CENSUS t=" .. game.time() .. " " .. table.concat(parts, " "))
            end)
            """);
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
        TestContext.Out.WriteLine(census[^1]);
        TestContext.Out.WriteLine(result);
        return (census, result!);
    }

    // A census line's time, and an actor's health in it ("Guard:allies" -> its number; the first such)
    private static int Time(string census) => int.Parse(census.Split(' ')[1]["t=".Length..]);

    private static int Health(string census, string who) =>
        census.Split(' ').Where(part => part.StartsWith(who + ":")).Select(part => int.Parse(part.Split(':')[2])).First();

    // demotest's "kills 1/2" and "score 100"
    private static string[] Field(string result, string name)
    {
        var words = result.Split(' ');
        return words[Array.IndexOf(words, name) + 1].Split('/');
    }

    private static int Kills(string result) => int.Parse(Field(result, "kills")[0]);
    private static int KillTotal(string result) => int.Parse(Field(result, "kills")[1]);
    private static int Score(string result) => int.Parse(Field(result, "score")[0]);
}
