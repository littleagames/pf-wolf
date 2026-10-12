using PFWolf.Assets;
using PFWolf.Editor.Data;

namespace PFWolf.Tests;

/// <summary>
/// The editor's script browser data over the fireball demo mod, and saving a script back into a
/// mod. Needs a built game folder (pfwolf.pk3 and Wolf3D's data files).
/// </summary>
[Category("GameData")]
public class ScriptCatalogGameTests
{
    private static string ModPath => Path.Combine(TestPaths.RepoRoot(), "examples", "mods", "fireball-demo");

    private static string? GameFolder()
    {
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        return new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
    }

    private static GameContent Load(params string[] mods)
    {
        var folder = GameFolder();
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            return GameContent.Load(folder!, "wolf3d", mods);
        }
        catch (PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"Wolf3D's data files aren't in {folder}");
            throw;
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
        }
    }

    [Test]
    public void The_Fireball_Demos_Script_Is_Listed_With_What_Calls_Its_Actions()
    {
        // Arrange
        var content = Load(ModPath);

        // Act
        var entries = ScriptCatalog.Build(content);
        var callers = ScriptCatalog.Callers(content);
        var report = ScriptCatalog.Check(ScriptCatalog.Read(content, "fireball-patterns") ?? "", "scripts/fireball-patterns.lua");

        // Assert
        var entry = entries.Single(e => e.Name == "fireball-patterns");
        Assert.That(entry.EntryPath, Is.EqualTo("scripts/fireball-patterns.lua"));
        Assert.That(content.ModPathOf(entry.Name, nameof(ScriptAsset)), Is.EqualTo(ModPath));
        Assert.That(report.Problems, Is.Empty);
        Assert.That(report.Actions.Select(action => action.Name),
            Is.EquivalentTo(new[] { "A_FireRing", "A_FireSpiral", "A_FireBurst", "A_Sweep", "A_Home" }));
        Assert.That(report.Hooks.Select(hook => hook.Name), Is.EqualTo(new[] { "game.onlevel", "game.ontick" }));
        Assert.That(callers["A_FireRing"], Does.Contain("FireballRing: Fire"));
        Assert.That(callers["A_Home"], Does.Contain("HomingFireball: Spawn"));
    }

    [Test]
    public void No_Scripts_Without_A_Mod_That_Has_Them()
    {
        // Act / Assert
        Assert.That(ScriptCatalog.Build(Load()), Is.Empty);
    }

    [Test]
    public void A_Saved_Script_Is_What_The_Game_Reads_Next_Time()
    {
        // Arrange: a copy of the mod to save into
        var copy = Path.Combine(Path.GetTempPath(), "pfwolf-scripts-" + Guid.NewGuid().ToString("N"));
        CopyFolder(ModPath, copy);
        try
        {
            var text = "function A_Saved(self) end\n";

            // Act: back into its own file, and a new one beside it
            ScriptCatalog.Save(copy, "scripts/fireball-patterns.lua", text);
            ScriptCatalog.Save(copy, ScriptCatalog.NewEntryPath("Extra"), "-- extra\n");
            var content = Load(copy);

            // Assert
            Assert.That(ScriptCatalog.Read(content, "fireball-patterns"), Is.EqualTo(text));
            Assert.That(ScriptCatalog.Build(content).Select(entry => entry.Name), Is.EquivalentTo(new[] { "fireball-patterns", "extra" }));
        }
        finally
        {
            Directory.Delete(copy, recursive: true);
        }
    }

    private static void CopyFolder(string from, string to)
    {
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
