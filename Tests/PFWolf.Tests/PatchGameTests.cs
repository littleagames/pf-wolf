using PFWolf.Editor.Editing;
using PFWolf.Loaders;
using static PFWolf.Tests.HeadlessGame;

namespace PFWolf.Tests;

/// <summary>
/// Wall patches in play (Wolf3D): a mod's one-room MAP01 puts patches in plane 0's high byte, a
/// scripted demo plays it, and a script prints the patches and plane 0 codes it finds. Needs
/// Wolf3D's data files, as DemoRegressionTests does.
/// </summary>
[Category("GameData")]
public class PatchGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private const ushort Wall = 1, Area = 108, PushWall = 98, PlayerFacingWest = 22;
    private const int Sign = 5, Ignored = 7, Moved = 9;

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

    [Test]
    public void A_Patch_Stays_On_Its_Wall_Moves_With_A_Pushwall_And_Leaves_Plane_0_Its_Codes()
    {
        // Arrange: a sign on the room's north wall, a patch on open floor (ignored), and a
        // patched pushwall west of the player, who uses it
        var mod = WriteRoom();
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO1.dmo"),
            ScriptedDemo(0, new DemoStep(0, 0, 0, 10), new DemoStep(Use, 0, 0, 3), new DemoStep(0, 0, 0, 400)));

        // Act
        var (exitCode, output) = HeadlessGame.Run(_workFolder, "wolf3d", "demotest 1; quit", Timeout, mod);

        // Assert
        var log = string.Join(Environment.NewLine, output.TakeLast(30));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(output, Has.None.Contains("Exception"), log);
        Assert.That(output, Has.Some.Contains("16,32, which isn't a wall, is ignored"), log);

        var first = output.First(line => line.StartsWith("PATCHES "));
        var last = output.Last(line => line.StartsWith("PATCHES "));
        // (a patched wall that kept its high byte wouldn't be a wall at all, nor a pushwall)
        Assert.That(first, Does.Contain($"sign={Sign} floor=0 push={Moved} pushed=0"), first);
        Assert.That(last, Does.Contain($"sign={Sign} floor=0 push=0 pushed={Moved}"), last);
    }

    // Prints, each second: the patches on the sign's wall, the patched floor tile, and where the
    // pushwall starts and where it ends up
    private const string ProbeScript = """
        local due = 0
        game.ontick(function(tics)
          if game.time() < due then return end
          due = game.time() + 70
          print("PATCHES t=" .. game.time() .. " sign=" .. game.patch(16, 29) .. " floor=" .. game.patch(16, 32)
            .. " push=" .. game.patch(9, 32) .. " pushed=" .. game.patch(7, 32))
        end)
        """;

    /// <summary>
    /// A mod in the work folder: MAP01 as one room (x 10-22, y 30-34) with the player at its west
    /// end facing a pushwall (9,32) that has two tiles of floor behind it, and the patches
    /// </summary>
    private string WriteRoom()
    {
        var mod = Path.Combine(_workFolder, "patches");
        Directory.CreateDirectory(Path.Combine(mod, "maps"));
        Directory.CreateDirectory(Path.Combine(mod, "mapdefs"));
        Directory.CreateDirectory(Path.Combine(mod, "scripts"));

        var map = MapFiles.NewMap("Patches", wall: Wall, floor: Wall);
        var walls = map.MapData[0];
        for (int y = 30; y <= 34; y++)
            for (int x = 10; x <= 22; x++)
                walls[y * 64 + x] = Area;
        walls[32 * 64 + 7] = walls[32 * 64 + 8] = Area;
        walls[29 * 64 + 16] = Wall | Sign << 8;
        walls[32 * 64 + 16] = Area | Ignored << 8;
        walls[32 * 64 + 9] = Wall | Moved << 8;
        map.MapData[1][32 * 64 + 9] = PushWall;
        map.MapData[1][32 * 64 + 10] = PlayerFacingWest;
        File.WriteAllBytes(Path.Combine(mod, "maps", "MAP01.wad"), EcWolfMapLoader.Save(map, "MAP01"));

        File.WriteAllText(Path.Combine(mod, "modinfo.yaml"), "name: Patches\nversion: \"1\"\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(mod, "mapdefs", "patches.yaml"), $$"""
            patches:
              {{Sign}}: { texture: GSTEAGL1, sides: [south] }
              {{Ignored}}: { texture: GSTEAGL1 }
              {{Moved}}: { texture: GSTEAGL1 }
            """);
        File.WriteAllText(Path.Combine(mod, "scripts", "probe.lua"), ProbeScript);
        return mod;
    }
}
