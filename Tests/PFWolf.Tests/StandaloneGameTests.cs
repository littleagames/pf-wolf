using System.Diagnostics;
using System.IO.Compression;
using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Loaders;

namespace PFWolf.Tests;

/// <summary>
/// Standalone games: a mod whose gamepack-info.yaml adds a game built on `standalone` runs with no
/// game's data files, and starts from none of Wolf3D's definitions. Uses examples/mods/standalone-demo
/// and a pfwolf.pk3 zipped from pfwolf-pk3/ in a temporary folder holding nothing else, so these
/// need no game data.
/// </summary>
public class StandaloneGameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private string _workFolder = "";
    private string _startFolder = "";

    private static string ModPath => Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "standalone-demo");

    [SetUp]
    public void SetUp()
    {
        _startFolder = Directory.GetCurrentDirectory();
        _workFolder = Path.Combine(Path.GetTempPath(), "pfwolf-standalone-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workFolder);
        ZipFile.CreateFromDirectory(TestPaths.Pk3SourceFolder(), Path.Combine(_workFolder, AssetManager.BasePk3FileName));
    }

    [TearDown]
    public void TearDown()
    {
        Directory.SetCurrentDirectory(_startFolder);
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
    public void A_Star_In_Game_Packs_Takes_In_Every_Pack_The_List_Does_Not_Leave_Out()
    {
        // Arrange
        string[] list = ["*", "!wolf3d-shareware"];

        // Act / Assert
        Assert.That(GamePackList.Includes(list, "wolf3d", []), Is.True);
        Assert.That(GamePackList.Includes(list, "spear", ["wolf3d"]), Is.True);
        Assert.That(GamePackList.Includes(list, "my-game", ["standalone"]), Is.True);
        Assert.That(GamePackList.Includes(list, "wolf3d-shareware", ["wolf3d"]), Is.False);
        Assert.That(GamePackList.Includes(["wolf3d"], "my-game", ["standalone"]), Is.False);
    }

    [Test]
    public void A_Mod_Adds_Games_But_Leaves_The_Ones_There_Are()
    {
        // Arrange
        var info = new GamePackInfoAsset(new() { ["wolf3d"] = new GamePack { Title = "Wolfenstein 3D" } });
        var modGames = new GamePackInfoAsset(new()
        {
            ["wolf3d"] = new GamePack { Title = "Hijacked" },
            ["my-game"] = new GamePack { Title = "My Game", BasePack = "standalone" },
        });
        var warnings = new List<string>();

        // Act
        var merged = info.WithModGames([("my mod", modGames)], warnings.Add);

        // Assert
        Assert.That(merged.GamePacks["wolf3d"].Title, Is.EqualTo("Wolfenstein 3D"));
        Assert.That(merged.GamePacks["my-game"].Title, Is.EqualTo("My Game"));
        Assert.That(warnings, Has.One.Contains("wolf3d"));
    }

    [Test]
    public void A_Game_Plays_The_Data_Files_And_Palette_Of_Its_Nearest_Base_Pack()
    {
        // Arrange
        var filePack = new FilePack { Description = "Wolf3D's files" };
        var info = new GamePackInfoAsset(new()
        {
            ["wolf3d"] = new GamePack { GamePalette = "wolfpal", FilePack = filePack },
            ["standalone"] = new GamePack { GamePalette = "wolfpal" },
            ["conversion"] = new GamePack { BasePack = "wolf3d" },
            ["my-game"] = new GamePack { BasePack = "standalone", GamePalette = "mypal" },
        });

        // Act / Assert
        Assert.That(info.GetFilePack("conversion"), Is.SameAs(filePack));
        Assert.That(info.HasDataFiles("conversion"), Is.True);
        Assert.That(info.GetGamePalette("conversion"), Is.EqualTo("wolfpal"));
        Assert.That(info.HasDataFiles("my-game"), Is.False);
        Assert.That(info.GetGamePalette("my-game"), Is.EqualTo("mypal"));
    }

    [Test]
    public void The_Mod_Runs_Its_Own_Game_Unless_Another_Is_Asked_For()
    {
        // Arrange
        Directory.SetCurrentDirectory(_workFolder);

        // Act
        var dropped = GameTypes.PickGame("", [ModPath]);
        var named = GameTypes.PickGame("standalone-demo", [ModPath]);
        var other = GameTypes.PickGame("wolf3d", [ModPath]);

        // Assert
        Assert.That(dropped, Is.EqualTo(GameSelection.ModGame("standalone-demo", ModPath)));
        Assert.That(named, Is.EqualTo(dropped));
        Assert.That(other.Type, Is.EqualTo(GameType.Wolf3D));
    }

    [Test]
    public void The_Editor_Loads_The_Game_With_No_Data_Files_And_Nothing_Of_Wolf3D()
    {
        // Act
        var content = GameContent.Load(_workFolder, "", [ModPath]);

        // Assert
        Assert.That(content.PackId, Is.EqualTo("standalone-demo"));
        Assert.That(content.Title, Is.EqualTo("Standalone Demo"));
        Assert.That(content.Maps.Select(map => map.Name), Is.EqualTo(new[] { "MAP01" }));
        Assert.That(content.ModPathOf("MAP01"), Is.EqualTo(ModPath));
        Assert.That(content.MapDefs.Walls.Keys, Is.EquivalentTo(new[] { 1, 2, 3, 4 }));
        Assert.That(content.ThingSpriteName("Lamp"), Is.EqualTo("LAMPA0"));
        Assert.That(content.Find<TextureAsset>("SDSTON1"), Is.Not.Null);

        // Wolf3D's classes, walls, deathmatch arenas and player sprites stay out
        Assert.That(content.Actors.Keys, Has.None.EqualTo("Guard").And.None.EqualTo("Pistol"));
        Assert.That(content.Find<TextureAsset>("GSTONEA1"), Is.Null);
        Assert.That(content.HasMap("DM01"), Is.False);
        Assert.That(content.Find<SpriteAsset>("PLMPA1"), Is.Null);
    }

    [Test]
    public void The_Game_Plays_Its_Level_Through_To_The_Exit()
    {
        // Arrange: a demo walking from the start through both doors to the exit switch, using each
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO0.dmo"), ScriptedDemo(
            (0, -35, 60), (Use, 0, 2), (0, 0, 80),      // to the airlock door, and open it
            (0, -35, 70), (Attack, 0, 3), (0, 0, 20),   // into the hall, firing the blaster
            (0, -35, 90), (Use, 0, 2), (0, 0, 80),      // up to the vault door, and open it
            (0, -35, 60), (Use, 0, 2), (0, 0, 200)));   // up to the switch, and throw it

        // Act
        var (exitCode, output) = RunGame("demotest 0; quit", demos);
        var result = output.FirstOrDefault(line => line.StartsWith("demotest 0:"));

        // Assert
        Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));
        Assert.That(result, Is.Not.Null, string.Join(Environment.NewLine, output.TakeLast(30)));
        var data = result!.Split(' ')[3].Split('/').Select(int.Parse).ToArray();
        Assert.That(data[0], Is.LessThan(data[1]), $"The level didn't end before the demo ran out: {result}");
        Assert.That(output, Has.None.Contains("aren't in the game folder"));
    }

    [Test]
    public void A_New_Game_Loads_In_The_Editor_And_Saves_Back_Into_Its_Pk3()
    {
        // Arrange
        var request = new PFWolf.Editor.Editing.NewModRequest(Path.Combine(_workFolder, "mods", "new-game.pk3"), "New Game", "new-game", BasePack: null);
        PFWolf.Editor.Editing.NewMod.Create(request, PFWolf.Editor.Editing.NewMod.ReadDefaultPalette(_workFolder));

        // Act: load it, change a tile, save, and load again
        var content = GameContent.Load(_workFolder, "", [request.Path]);
        var document = new PFWolf.Editor.Editing.MapDocument("MAP01", content.FindMap("MAP01")!.DeepCopy()) { SaveFolder = content.ModPathOf("MAP01") };
        EditorMaps.Set(document, 0, 10, 10, 7);
        PFWolf.Editor.Editing.MapFiles.Save(document, document.SaveFolder!);
        var reloaded = GameContent.Load(_workFolder, "", [request.Path]);

        // Assert
        Assert.That(content.PackId, Is.EqualTo("new-game"));
        Assert.That(content.Title, Is.EqualTo("New Game"));
        Assert.That(content.MapDefs.Walls.Keys, Is.EquivalentTo(new[] { 1, 2, 3, 4 }), "the example game's walls");
        Assert.That(content.ThingSpriteName("Lamp"), Is.EqualTo("LAMPA0"));
        Assert.That(content.Palette, Has.Length.EqualTo(256));
        Assert.That(content.ModPathOf("wolfpal", nameof(PFWolf.Assets.Palette)), Is.EqualTo(request.Path), "its own copy of the palette");
        Assert.That(document.SaveFolder, Is.EqualTo(request.Path));
        Assert.That(reloaded.FindMap("MAP01")!.MapData[0][10 * 64 + 10], Is.EqualTo(7));
        Assert.That(File.Exists(request.Path + PFWolf.Editor.Editing.ModFiles.BackupExtension), Is.True);
    }

    [Test]
    public void A_New_Game_Plays_Its_Level_Through_To_The_Exit_As_It_Is_Made()
    {
        // Arrange: what the editor's New mod makes, with the example game's walk to the exit
        var request = new PFWolf.Editor.Editing.NewModRequest(Path.Combine(_workFolder, "mods", "new-game.pk3"), "New Game", "new-game", BasePack: null);
        PFWolf.Editor.Editing.NewMod.Create(request, PFWolf.Editor.Editing.NewMod.ReadDefaultPalette(_workFolder));
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO0.dmo"), ScriptedDemo(
            (0, -35, 60), (Use, 0, 2), (0, 0, 80),
            (0, -35, 70), (Attack, 0, 3), (0, 0, 20),
            (0, -35, 90), (Use, 0, 2), (0, 0, 80),
            (0, -35, 60), (Use, 0, 2), (0, 0, 200)));

        // Act
        var (exitCode, output) = RunGame("demotest 0; quit", demos, request.Path);
        var result = output.FirstOrDefault(line => line.StartsWith("demotest 0:"));

        // Assert
        Assert.That(exitCode, Is.Zero, string.Join(Environment.NewLine, output.TakeLast(30)));
        Assert.That(result, Is.Not.Null, string.Join(Environment.NewLine, output.TakeLast(30)));
        var data = result!.Split(' ')[3].Split('/').Select(int.Parse).ToArray();
        Assert.That(data[0], Is.LessThan(data[1]), $"The level didn't end before the demo ran out: {result}");
        Assert.That(output, Has.None.Contains("Exception"));
    }

    [Test]
    public void A_Game_With_No_Mapdefs_Still_Plays_Its_Level()
    {
        // Arrange: a level whose numbers mean nothing: no walls (so not even its edge is solid),
        // no floor areas, no player start; a few tics walking about it, using and firing
        var mod = Path.Combine(_workFolder, "bare-game.pk3");
        PFWolf.Editor.Editing.ModFiles.CreateArchive(mod,
        [
            ("gamepack-info.yaml", "bare-game:\n  title: Bare Game\n  base-pack: standalone\n"u8.ToArray()),
            ("game-info.yaml", "skills:\n  NORMAL:\n    name: Normal\nepisodes:\n  EP01:\n    name: Bare\n    start-map: MAP01\nmaps:\n  MAP01:\n    name: Level 1\n    floor-number: 1\nmenu-music: \"\"\n"u8.ToArray()),
            (PFWolf.Editor.Editing.MapFiles.EntryPath("MAP01"),
                EcWolfMapLoader.Save(PFWolf.Editor.Editing.MapFiles.NewMap("Level 1", wall: 1, floor: 0), "MAP01")),
        ]);
        var demos = Directory.CreateDirectory(Path.Combine(_workFolder, "demos")).FullName;
        File.WriteAllBytes(Path.Combine(demos, "DEMO0.dmo"), ScriptedDemo((0, -35, 200), (Use, 0, 2), (Attack, 0, 3), (0, 0, 20)));

        // Act
        var (exitCode, output) = RunGame("demotest 0; quit", demos, mod);

        // Assert
        var log = string.Join(Environment.NewLine, output.TakeLast(30));
        Assert.That(exitCode, Is.Zero, log);
        Assert.That(output, Has.Some.StartsWith("demotest 0: data"), log);
        Assert.That(output, Has.Some.Contains("has no mapdefs player start"), log);
        Assert.That(output, Has.Some.Contains("made solid"), log);
        Assert.That(output, Has.None.Contains("Exception"), log);
    }

    private const byte Attack = 1, Use = 8;

    /// <summary>id's demo format: MAP01, the length, then each tic's buttons and forward/back move</summary>
    private static byte[] ScriptedDemo(params (byte Buttons, sbyte Forward, int Tics)[] steps)
    {
        var data = new List<byte> { 0, 0, 0, 0 };
        foreach (var (buttons, forward, tics) in steps)
            for (var i = 0; i < tics; i++)
                data.AddRange([buttons, 0, unchecked((byte)forward)]);

        var bytes = data.ToArray();
        BitConverter.GetBytes((short)bytes.Length).CopyTo(bytes, 1);
        return bytes;
    }

    /// <summary>The game built beside the tests, run headless with the mod in the work folder</summary>
    private (int ExitCode, List<string> Output) RunGame(string commands, string demos, string? mod = null)
    {
        mod ??= ModPath;
        var exe = Path.Combine(TestContext.CurrentContext.TestDirectory, "PFWolf.exe");
        Assert.That(File.Exists(exe), Is.True, $"{exe} wasn't built beside the tests");

        var start = new ProcessStartInfo(exe)
        {
            WorkingDirectory = _workFolder,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[]
        {
            "--nowait", "--file", mod,
            "--configdir", Path.Combine(_workFolder, "config"),
            "--savedir", Path.Combine(_workFolder, "saves"),
            "--demodir", demos,
            "--exec", commands,
        })
            start.ArgumentList.Add(arg);
        start.Environment["SDL_VIDEODRIVER"] = "dummy";
        start.Environment["SDL_AUDIODRIVER"] = "dummy";

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(Timeout))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail($"The game didn't finish within {Timeout.TotalMinutes} minutes");
        }

        var output = (stdout.Result + stderr.Result)
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        return (process.ExitCode, output);
    }
}
