using PFWolf.Editor.Editing;
using PFWolf.Loaders;
using static PFWolf.Tests.HeadlessGame;

namespace PFWolf.Tests;

/// <summary>
/// Wall patches in play (Wolf3D): a mod's one-room MAP01 puts patches in plane 0's high byte, a
/// scripted demo plays it, and a script prints the patches it finds. The player starts at the
/// room's west end (10,32) facing west, at the WallId (9,32). Needs Wolf3D's data files, as
/// DemoRegressionTests does.
/// </summary>
[Category("GameData")]
public class PatchGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private const ushort Wall = 1, Area = 108, PushWall = 98, PlayerFacingWest = 22;

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-patches-" + Guid.NewGuid().ToString("N"));
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

    // Tiles the probe reports: the WallId the player faces, one on the room's north WallId, open
    // floor in the room, and the floor two tiles past the faced WallId
    private const int Faced = 9 + 32 * 64, North = 16 + 29 * 64, Floor = 16 + 32 * 64, Beyond = 7 + 32 * 64;

    [Test]
    public void A_Patch_Stays_On_Its_Wall_Moves_With_A_Pushwall_And_Leaves_Plane_0_Its_Codes()
    {
        // Arrange: a sign on the room's north WallId, a patch on open floor (ignored), and a
        // patched pushwall in front of the player, who uses it
        var mod = WriteRoom("""
            patches:
              5: { texture: GSTEAGL1, sides: [south] }
              7: { texture: GSTEAGL1 }
              9: { texture: GSTEAGL1 }
            """, new() { [North] = Wall | 5 << 8, [Floor] = Area | 7 << 8, [Faced] = Wall | 9 << 8 }, pushwall: true);

        // Act
        var (output, first, last) = Play(mod, new DemoStep(0, 0, 0, 10), new DemoStep(Use, 0, 0, 3), new DemoStep(0, 0, 0, 400));

        // Assert (a patched WallId that kept its high byte wouldn't be a WallId at all, nor a pushwall)
        Assert.That(output, Has.Some.Contains("16,32, which isn't a wall, is ignored"));
        Assert.That(first, Does.Contain("faced=9 north=5 floor=0 beyond=0"), first);
        Assert.That(last, Does.Contain("faced=0 north=5 floor=0 beyond=9"), last);
    }

    [Test]
    public void A_Patch_Switch_Toggles_And_Its_Linked_Patches_Flip_With_It()
    {
        // Arrange: a two-way switch patch on the faced WallId, and one sharing its tag (3) on the
        // north WallId; the player uses it twice
        var mod = WriteRoom("""
            patches:
              10: { texture: GSTEAGL1, switch: { to: 11 } }
              11: { texture: GSTEAGL2, switch: { to: 10 } }
            """, new() { [Faced] = Wall | 10 << 8, [North] = Wall | 10 << 8 }, tags: new() { [Faced] = 3, [North] = 3 });

        // Act
        var (_, first, last, lines) = PlayAll(mod, new DemoStep(0, 0, 0, 10), new DemoStep(Use, 0, 0, 3),
            new DemoStep(0, 0, 0, 140), new DemoStep(Use, 0, 0, 3), new DemoStep(0, 0, 0, 140));

        // Assert
        Assert.That(first, Does.Contain("faced=10 north=10"), first);
        Assert.That(lines, Has.Some.Contains("faced=11 north=11"), "thrown, with its linked patch");
        Assert.That(last, Does.Contain("faced=10 north=10"), "thrown back");
    }

    [Test]
    public void A_Patch_That_Gives_What_The_Player_Has_No_Use_For_Stays_Put()
    {
        // Arrange: a first aid kit on the WallId, taken off when used, and the player's health full
        var mod = WriteRoom("""
            patches:
              20:
                texture: GSTEAGL1
                switch:
                  remove: true
                  actions:
                    - A_GiveInventory("FirstAidKit")
            """, new() { [Faced] = Wall | 20 << 8 });

        // Act
        var (_, _, last) = Play(mod, new DemoStep(0, 0, 0, 10), new DemoStep(Use, 0, 0, 3), new DemoStep(0, 0, 0, 140));

        // Assert
        Assert.That(last, Does.Contain("faced=20"), last);
    }

    [Test]
    public void A_Patch_Gives_Or_Spawns_An_Item_And_Comes_Off()
    {
        // Arrange: a patch on the faced WallId that gives ammo and drops a first aid kit (which the
        // player, at full health, leaves lying), taken off when used
        var mod = WriteRoom("""
            patches:
              21:
                texture: GSTEAGL1
                switch:
                  remove: true
                  actions:
                    - A_GiveInventory("Clip", 4)
                    - A_SpawnItem("FirstAidKit")
            """, new() { [Faced] = Wall | 21 << 8 });

        // Act
        var (_, first, last) = Play(mod, new DemoStep(0, 0, 0, 10), new DemoStep(Use, 0, 0, 3), new DemoStep(0, 0, 0, 140));

        // Assert: the patch is gone, and the kit lies where the player stood to use it
        Assert.That(first, Does.Contain("faced=21").And.EndsWith("kits="), first);
        Assert.That(last, Does.Contain("faced=0").And.EndsWith("kits=10,32"), last);
    }

    [Test]
    public void The_Patch_Demo_Loads_With_Its_Patches_And_Nothing_Wrong()
    {
        // Arrange: examples/mods/patch-demo, with a probe of its own alongside reading its lever
        // (tag 1), button, first aid and the alarm lamp's tiles
        var demo = Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "patch-demo");
        var probe = Path.Combine(_workFolder, "probe");
        Directory.CreateDirectory(Path.Combine(probe, "scripts"));
        File.WriteAllText(Path.Combine(probe, "modinfo.yaml"), "name: Probe\nversion: \"1\"\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(probe, "scripts", "probe.lua"), """
            game.ontick(function(tics)
              if game.time() > 35 then return end
              print("DEMO lever=" .. game.patch(30, 27) .. " button=" .. game.patch(28, 27) .. " firstaid=" .. game.patch(24, 27)
                .. " alarm=" .. game.patch(30, 21) .. " push=" .. game.patch(20, 23) .. " tagged=" .. #game.taggedtiles(1))
            end)
            """);
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO1.dmo"), ScriptedDemo(0, new DemoStep(0, 0, 0, 70)));

        // Act
        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", "demotest 1; quit", Timeout, demo, probe);

        // Assert
        var log = string.Join(Environment.NewLine, output.TakeLast(30));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(output, Has.None.Contains("Exception").And.None.Contains("patch").And.None.Contains("left out"), log);
        Assert.That(output, Has.Some.EqualTo("DEMO lever=9 button=7 firstaid=5 alarm=4 push=2 tagged=2"), log);
    }

    // Prints, each half second: the patches on the tiles the tests look at, and the first aid
    // kits lying about (their tiles)
    private const string ProbeScript = """
        local due = 0
        game.ontick(function(tics)
          if game.time() < due then return end
          due = game.time() + 35
          local kits = {}
          for _, a in ipairs(game.actors('FirstAidKit')) do
            if not a.removed then kits[#kits + 1] = a.tilex .. "," .. a.tiley end
          end
          print("PATCHES t=" .. game.time() .. " faced=" .. game.patch(9, 32) .. " north=" .. game.patch(16, 29)
            .. " floor=" .. game.patch(16, 32) .. " beyond=" .. game.patch(7, 32) .. " kits=" .. table.concat(kits, ";"))
        end)
        """;

    /// <summary>
    /// A mod in the work folder: MAP01 as one room (x 10-22, y 30-34) with the player at (10,32)
    /// facing west, plane 0 values and tags (plane 4) at the tiles given, and the patches. With
    /// <paramref name="pushwall"/>, the faced WallId (9,32) is a pushwall with two tiles of floor behind it.
    /// </summary>
    private string WriteRoom(string patchesYaml, Dictionary<int, int> walls, Dictionary<int, int>? tags = null, bool pushwall = false)
    {
        var mod = Path.Combine(_workFolder, "patches");
        Directory.CreateDirectory(Path.Combine(mod, "maps"));
        Directory.CreateDirectory(Path.Combine(mod, "mapdefs"));
        Directory.CreateDirectory(Path.Combine(mod, "scripts"));

        var map = MapFiles.NewMap("Patches", wall: Wall, floor: Wall);
        var plane0 = map.MapData[0];
        for (int y = 30; y <= 34; y++)
            for (int x = 10; x <= 22; x++)
                plane0[y * 64 + x] = Area;
        if (pushwall)
        {
            plane0[32 * 64 + 7] = plane0[32 * 64 + 8] = Area;
            map.MapData[1][Faced] = PushWall;
        }
        foreach (var (spot, value) in walls)
            plane0[spot] = (ushort)value;
        foreach (var (spot, tag) in tags ?? [])
            map.MapData[4][spot] = (ushort)tag;
        map.MapData[1][32 * 64 + 10] = PlayerFacingWest;
        File.WriteAllBytes(Path.Combine(mod, "maps", "MAP01.wad"), EcWolfMapLoader.Save(map, "MAP01"));

        File.WriteAllText(Path.Combine(mod, "modinfo.yaml"), "name: Patches\nversion: \"1\"\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(mod, "mapdefs", "patches.yaml"), patchesYaml);
        File.WriteAllText(Path.Combine(mod, "scripts", "probe.lua"), ProbeScript);
        return mod;
    }

    private (List<string> Output, string First, string Last) Play(string mod, params DemoStep[] steps)
    {
        var (output, first, last, _) = PlayAll(mod, steps);
        return (output, first, last);
    }

    // Plays the steps on MAP01 with the mod: the game's output, and the probe's lines
    private (List<string> Output, string First, string Last, List<string> Lines) PlayAll(string mod, params DemoStep[] steps)
    {
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO1.dmo"), ScriptedDemo(0, steps));

        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", "demotest 1; quit", Timeout, mod);
        var log = string.Join(Environment.NewLine, output.TakeLast(30));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(output, Has.None.Contains("Exception").And.None.Contains("isn't a patch").And.None.Contains("isn't an"), log);

        var lines = output.Where(line => line.StartsWith("PATCHES ")).ToList();
        Assert.That(lines, Is.Not.Empty, log);
        TestContext.Out.WriteLine(string.Join(Environment.NewLine, lines));
        return (output, lines[0], lines[^1], lines);
    }
}
