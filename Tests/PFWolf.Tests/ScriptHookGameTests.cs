using PFWolf.Editor.Editing;
using PFWolf.Loaders;
using static PFWolf.Tests.HeadlessGame;

namespace PFWolf.Tests;

/// <summary>
/// The scripts' event hooks in play (Wolf3D): a mod puts a one-room arena in place of MAP01, a
/// scripted demo plays it, and a script prints each hook as the game calls it. Needs Wolf3D's
/// data files, as DemoRegressionTests does.
/// </summary>
[Category("GameData")]
public class ScriptHookGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-hooks-" + Guid.NewGuid().ToString("N"));
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

    private const int PlayerFacingEast = 20, GuardFacingWest = 110, ToughGuardFacingWest = 500;

    // Prints each hook as it's called, and the player's health each second
    private const string HookScript = """
        local function who(a) return a and a.name or "nil" end
        game.onspawn(function(player, reason) print("HOOK spawn " .. who(player) .. " " .. reason) end)
        game.onsight(function(monster, target) print("HOOK sight " .. who(monster) .. " " .. who(target)) end)
        game.ondamage(function(victim, amount, attacker, type)
          print("HOOK damage " .. who(victim) .. " " .. amount .. " " .. who(attacker))
          if victim.isplayer then return 0 end
        end)
        game.onkill(function(victim, killer, type) print("HOOK kill " .. who(victim) .. " " .. who(killer)) end)
        game.onplayerdeath(function(player, killer) print("HOOK playerdeath " .. who(killer)) end)
        local due = 0
        game.ontick(function()
          if game.time() < due then return end
          due = game.time() + 70
          print("HEALTH " .. game.players()[1].health)
        end)
        """;

    [Test]
    public void The_Hooks_Follow_A_Fight_With_A_Guard()
    {
        // Arrange: the player at the west end facing a guard five tiles east; they shoot it
        var mod = WriteArena((11, PlayerFacingEast), (16, GuardFacingWest));
        var steps = new[] { new DemoStep(0, 0, 0, 20) }
            .Concat(Enumerable.Range(0, 8).SelectMany(_ => new[] { new DemoStep(Attack, 0, 0, 10), new DemoStep(0, 0, 0, 20) }))
            .Append(new DemoStep(0, 0, 0, 140)).ToArray();

        // Act
        var hooks = Play(mod, steps);

        // Assert: in the order they happened
        Assert.That(hooks[0], Is.EqualTo("HOOK spawn Player new"));
        Assert.That(hooks, Has.Some.EqualTo("HOOK sight Guard Player"));
        Assert.That(hooks, Has.Some.Matches<string>(line => line.StartsWith("HOOK damage Guard ") && line.EndsWith(" Player")));
        Assert.That(hooks, Has.Some.EqualTo("HOOK kill Guard Player"));
        Assert.That(hooks.IndexOf("HOOK kill Guard Player"), Is.GreaterThan(hooks.FindIndex(line => line.StartsWith("HOOK damage Guard"))));
        Assert.That(hooks, Has.None.StartsWith("HOOK playerdeath"));
    }

    [Test]
    public void A_Damage_Hook_Of_Nothing_Keeps_The_Player_Unhurt()
    {
        // Arrange: a guard too tough to die facing the player, who stands there for ten seconds
        var mod = WriteArena((11, PlayerFacingEast), (15, ToughGuardFacingWest));

        // Act
        var lines = Play(mod, new DemoStep(0, 0, 0, 700));

        // Assert: shot at, and never hurt
        Assert.That(lines, Has.Some.Matches<string>(line => line.StartsWith("HOOK damage Player ") && line.EndsWith(" ToughGuard")));
        Assert.That(lines.Where(line => line.StartsWith("HEALTH ")), Has.All.EqualTo("HEALTH 100"));
    }

    // Refuses every shot, and prints the guard told as the level starts, steps, hits and the HUD drawn
    private const string RefusingScript = """
        local function who(a) return a and a.name or "nil" end
        game.onactorspawn(function(actor) if actor.name == "Guard" then print("HOOK actorspawn Guard") end end)
        game.onfire(function(player, weapon) print("HOOK fire " .. who(player) .. " " .. weapon) return false end)
        game.ondamage(function(victim, amount, attacker)
          print("HOOK damage " .. who(victim))
          if victim.isplayer then return 0 end
        end)
        game.onenter(function(actor, x, y) if actor.isplayer then print("HOOK enter " .. x .. " " .. y) end end)
        local shown = false
        game.onhud(function()
          local w, h = game.viewsize()
          game.drawtext("hud", 2, 2)
          if not shown then print("HOOK hud " .. w .. "x" .. h) shown = true end
        end)
        """;

    [Test]
    public void Shots_A_Script_Refuses_Never_Hit_And_The_Level_Is_Told_What_Moves()
    {
        // Arrange: the player facing a guard five tiles east; they walk up to it, then try to shoot it
        var mod = WriteArena(RefusingScript, (11, PlayerFacingEast), (16, GuardFacingWest));
        var steps = new[] { new DemoStep(0, 0, -35, 40), new DemoStep(0, 0, 0, 20) }
            .Concat(Enumerable.Range(0, 6).SelectMany(_ => new[] { new DemoStep(Attack, 0, 0, 10), new DemoStep(0, 0, 0, 20) }))
            .Append(new DemoStep(0, 0, 0, 70)).ToArray();

        // Act
        var lines = Play(mod, steps);

        // Assert
        Assert.That(lines, Has.Some.EqualTo("HOOK actorspawn Guard"));
        Assert.That(lines, Has.Some.StartsWith("HOOK fire Player "));
        Assert.That(lines, Has.None.EqualTo("HOOK damage Guard"));
        Assert.That(lines, Has.Some.StartsWith("HOOK enter 12 32"));
        Assert.That(lines, Has.Some.StartsWith("HOOK hud "));
    }

    /// <summary>
    /// A mod in the work folder: MAP01 as one room (x 10-22, y 30-34) with the things given along
    /// its middle row (y 32), at their x, and the hook script
    /// </summary>
    private string WriteArena(params (int X, int Thing)[] things) => WriteArena(HookScript, things);

    private string WriteArena(string script, params (int X, int Thing)[] things)
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
        File.WriteAllText(Path.Combine(mod, "actordefs", "tough.yaml"), """
            ToughGuard:
              parent: Guard
              properties:
                health: 30000
            """);
        File.WriteAllText(Path.Combine(mod, "mapdefs", "arena.yaml"), $$"""
            things:
              {{ToughGuardFacingWest}}: { class: ToughGuard, angles: 180 }
            """);
        File.WriteAllText(Path.Combine(mod, "scripts", "hooks.lua"), script);
        return mod;
    }

    // Plays the steps on MAP01 with the mod: the HOOK and HEALTH lines the script printed
    private List<string> Play(string mod, params DemoStep[] steps)
    {
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO1.dmo"), ScriptedDemo(0, steps));

        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", "demotest 1; quit", Timeout, mod);
        var log = string.Join(Environment.NewLine, output.TakeLast(30));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(output, Has.None.Contains("Exception").And.None.StartsWith("Script game."), log);     // no hook errors

        var lines = output.Where(line => line.StartsWith("HOOK ") || line.StartsWith("HEALTH ")).ToList();
        Assert.That(lines, Is.Not.Empty, log);
        foreach (var line in lines)
            TestContext.Out.WriteLine(line);
        return lines;
    }
}
