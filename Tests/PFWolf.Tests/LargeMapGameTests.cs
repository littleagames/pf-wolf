using PFWolf.Editor.Editing;
using PFWolf.Loaders;
using static PFWolf.Tests.HeadlessGame;

namespace PFWolf.Tests;

/// <summary>
/// A level bigger than 64x64 in play (Wolf3D): a mod's 128x96 MAP01 with everything past tile 64,
/// played by a scripted demo. Needs Wolf3D's data files, as DemoRegressionTests does.
/// </summary>
[Category("GameData")]
public class LargeMapGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private const int Width = 128, Height = 96;
    private const ushort Wall = 1, West = 108, East = 109, Door = 90, Cross = 52, PlayerFacingEast = 20;

    private string _workFolder = "";

    [SetUp]
    public void SetUp()
    {
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-largemap-" + Guid.NewGuid().ToString("N"));
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

    // Prints, each half second, where the player is and how many crosses are left
    private const string ProbeScript = """
        local due = 0
        game.ontick(function(tics)
          if game.time() < due then return end
          due = game.time() + 35
          local crosses = 0
          for _, a in ipairs(game.actors('Cross')) do
            if not a.removed then crosses = crosses + 1 end
          end
          local p = game.players()[1]
          print("BIG t=" .. game.time() .. " at=" .. p.tilex .. "," .. p.tiley .. " crosses=" .. crosses)
        end)
        """;

    [Test]
    public void A_Level_Bigger_Than_64_Plays_Past_Tile_64()
    {
        // Arrange: two rooms, x 70-109 and 111-125 (y 76-84), joined by a door at 110,80; the
        // player at 100,80 facing east, a cross at 118,80
        var mod = Path.Combine(_workFolder, "large");
        Directory.CreateDirectory(Path.Combine(mod, "maps"));
        Directory.CreateDirectory(Path.Combine(mod, "scripts"));
        var map = MapFiles.NewMap("Large", wall: Wall, floor: Wall, width: Width, height: Height);
        for (int y = 76; y <= 84; y++)
        {
            for (int x = 70; x <= 109; x++)
                map.MapData[0][y * Width + x] = West;
            for (int x = 111; x <= 125; x++)
                map.MapData[0][y * Width + x] = East;
        }
        map.MapData[0][80 * Width + 110] = Door;
        map.MapData[1][80 * Width + 100] = PlayerFacingEast;
        map.MapData[1][80 * Width + 118] = Cross;
        File.WriteAllBytes(Path.Combine(mod, "maps", "MAP01.wad"), EcWolfMapLoader.Save(map, "MAP01"));
        File.WriteAllText(Path.Combine(mod, "modinfo.yaml"), "name: Large\nversion: \"1\"\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(mod, "scripts", "probe.lua"), ProbeScript);

        // Run to the door, open it, wait, run on through
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO1.dmo"), ScriptedDemo(0,
            new DemoStep(0, 0, -70, 50), new DemoStep(Use, 0, 0, 2), new DemoStep(0, 0, 0, 40), new DemoStep(0, 0, -70, 50)));

        // Act
        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", "demotest 1; quit", Timeout, mod);

        // Assert
        var log = string.Join(Environment.NewLine, output.TakeLast(30));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(output, Has.None.Contains("Exception"), log);
        var lines = output.Where(line => line.StartsWith("BIG ")).ToList();
        TestContext.Out.WriteLine(string.Join(Environment.NewLine, lines));
        Assert.That(lines, Is.Not.Empty, log);
        Assert.That(lines[0], Does.Match("at=10[01],80 crosses=1"), log);      // a tic of running already
        Assert.That(lines, Has.Some.Contains("at=109,80"), "stopped at the shut door");
        Assert.That(lines[^1], Does.Contain("crosses=0"), "through the door and over the cross");
        Assert.That(lines[^1], Does.Match(@"at=12[0-5],80"), "at the east room's far wall");
    }

    [Test]
    public void A_Uwmf_Level_With_Its_Own_Wall_Door_And_Things_Plays()
    {
        // Arrange: the same two rooms as a hand-written UWMF MAP01 (maps/MAP01.uwmf): walls of a
        // texture pairing the mapdefs don't have, a door of the level's own, and the cross off its
        // tile's middle, so it's one of the level's extra things
        var mod = Path.Combine(_workFolder, "uwmf");
        Directory.CreateDirectory(Path.Combine(mod, "maps"));
        Directory.CreateDirectory(Path.Combine(mod, "scripts"));
        var spots = new List<string>();
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                spots.Add(y is >= 76 and <= 84 && x is >= 70 and <= 109 ? "{-1,0,0}"
                    : y is >= 76 and <= 84 && x is >= 111 and <= 125 ? "{-1,0,1}"
                    : x == 110 && y == 80 ? "{1,0,-1}"
                    : "{0,0,-1}");
        File.WriteAllText(Path.Combine(mod, "maps", "MAP01.uwmf"), $$"""
            namespace = "Wolf3D";
            name = "Uwmf large";
            tilesize = 64;
            width = {{Width}};
            height = {{Height}};
            tile { texturenorth = "WOOD1"; texturesouth = "WOOD1"; textureeast = "GSTONEA2"; texturewest = "GSTONEA2"; }
            tile { texturenorth = "SLOT1_1"; texturesouth = "SLOT1_1"; textureeast = "DOOR2_2"; texturewest = "DOOR2_2"; offsetvertical = true; }
            sector { texturefloor = "#707070"; textureceiling = "#383838"; }
            zone { }
            zone { }
            plane { depth = 64; }
            planemap { {{string.Join(",", spots)}} }
            thing { x = 100.5; y = 80.5; type = "$Player1Start"; angle = 0; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            thing { x = 118.3; y = 80.6; type = "Cross"; angle = 0; skill1 = true; skill2 = true; skill3 = true; skill4 = true; }
            trigger { x = 110; y = 80; action = "Door_Open"; arg1 = 16; arg2 = 300; playeruse = true; monsteruse = true; repeatable = true; activatenorth = false; activatesouth = false; }
            """);
        File.WriteAllText(Path.Combine(mod, "modinfo.yaml"), "name: Uwmf\nversion: \"1\"\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(mod, "scripts", "probe.lua"), ProbeScript);
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO1.dmo"), ScriptedDemo(0,
            new DemoStep(0, 0, -70, 50), new DemoStep(Use, 0, 0, 2), new DemoStep(0, 0, 0, 40), new DemoStep(0, 0, -70, 50)));

        // Act
        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", "demotest 1; quit", Timeout, mod);

        // Assert: as the binary level played
        var log = string.Join(Environment.NewLine, output.TakeLast(30));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(output, Has.None.Contains("Exception").And.None.Contains("left out").And.None.Contains("UWMF"), log);
        var lines = output.Where(line => line.StartsWith("BIG ")).ToList();
        TestContext.Out.WriteLine(string.Join(Environment.NewLine, lines));
        Assert.That(lines, Is.Not.Empty, log);
        Assert.That(lines[0], Does.Contain("crosses=1"), log);
        Assert.That(lines, Has.Some.Contains("at=109,80"), "stopped at the shut door");
        Assert.That(lines[^1], Does.Contain("crosses=0"), "through the level's own door and over the cross");
        Assert.That(lines[^1], Does.Match(@"at=12[0-5],80"), "at the east room's far wall");
    }
}
