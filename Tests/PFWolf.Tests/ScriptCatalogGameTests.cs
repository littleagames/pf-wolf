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

    [Test]
    public void Go_To_Definition_Goes_To_The_Script_The_Game_Uses_Keeps_Changes_And_Comes_Back()
    {
        // Arrange: a second mod, loaded after the demo, whose script replaces A_FireRing
        var mod = Path.Combine(Path.GetTempPath(), "pfwolf-override-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(mod, "scripts"));
        File.WriteAllText(Path.Combine(mod, "modinfo.yaml"), "name: Override\nversion: \"1.0\"\ngame-packs: [wolf3d]\n");
        File.WriteAllText(Path.Combine(mod, "scripts", "override.lua"), "-- a change\n\nfunction A_FireRing(self) end\n");
        try
        {
            var browser = new PFWolf.Editor.ViewModels.ScriptBrowserViewModel();
            browser.Load(Load(ModPath, mod));
            var lines = new List<int>();
            browser.SelectLine += (_, line) => lines.Add(line);
            browser.SelectedItem = browser.Items.Single(item => item.Name == "fireball-patterns");
            browser.Source = "-- not saved\n" + browser.Source;

            // Act: from a call to A_FireRing in the demo's script
            bool went = browser.GoToDefinition("        A_FireRing(pile, \"Fireball\", 12)", 10, fromLine: 36);
            var there = (browser.SelectedItem?.Name, browser.Status);
            bool back = browser.GoBack();

            // Assert
            Assert.That(went, Is.True);
            Assert.That(there.Name, Is.EqualTo("override"));
            Assert.That(there.Status, Does.Contain("the one the game uses"));
            Assert.That(back, Is.True);
            Assert.That(browser.SelectedItem?.Name, Is.EqualTo("fireball-patterns"));
            Assert.That(lines, Is.EqualTo(new[] { 3, 36 }));
            Assert.That(browser.Source, Does.StartWith("-- not saved\n"), "the change made before going is kept");
            Assert.That(browser.IsChanged, Is.True);
        }
        finally
        {
            Directory.Delete(mod, recursive: true);
        }
    }

    [Test]
    public void Caller_Sites_Say_The_Actor_State_And_Frame()
    {
        // Act
        var sites = ScriptCatalog.CallerSites(Load(ModPath));

        // Assert: FireballRing's Fire state calls A_FireRing on its first frame; WallFireballAimed's
        // Fire calls A_Jump on its third (frame 2, from 0)
        Assert.That(sites["A_FireRing"], Does.Contain(new CallerSite("FireballRing", "Fire", 0)));
        Assert.That(sites["A_Home"], Does.Contain(new CallerSite("HomingFireball", "Spawn", 0)));
        Assert.That(sites["A_Sweep"], Does.Contain(new CallerSite("WallFireballSweep", "Fire", 0)));
        Assert.That(sites["A_Jump"], Does.Contain(new CallerSite("WallFireballAimed", "Fire", 2)));
        Assert.That(sites["A_JumpIfInSight"].Select(site => site.Label), Does.Contain("WallFireball: Spawn"));
    }

    [Test]
    public void The_Definitions_Window_Opens_A_Caller_At_Its_Frame()
    {
        // Arrange
        var definitions = new PFWolf.Editor.ViewModels.DefinitionsViewModel();
        definitions.Load(Load(ModPath), [ModPath]);

        // Act: as the script browser asks for A_FireBurst's caller (WallFireballFan's Fire, frame 0)
        definitions.ShowEntry(PFWolf.Editor.Definitions.DefinitionKind.Actors, ["WallFireballFan"], ["states", "Fire", "0"]);

        // Assert: the class picked, its states and Fire opened, the frame picked
        var actors = definitions.Actors;
        Assert.That(actors.SelectedItem?.Name, Is.EqualTo("WallFireballFan"));
        var row = actors.SelectedRow;
        Assert.That(row?.Key, Is.EqualTo("0"));
        Assert.That(row!.Parent?.Key, Is.EqualTo("Fire"));
        Assert.That(row.Parent!.IsExpanded && row.Parent.Parent!.IsExpanded, Is.True);
        Assert.That(definitions.Status, Is.Empty);
    }

    [Test]
    public void A_Frame_That_Is_Not_There_Says_So()
    {
        // Arrange
        var definitions = new PFWolf.Editor.ViewModels.DefinitionsViewModel();
        definitions.Load(Load(ModPath), [ModPath]);

        // Act
        definitions.ShowEntry(PFWolf.Editor.Definitions.DefinitionKind.Actors, ["WallFireballFan"], ["nostates"]);

        // Assert
        Assert.That(definitions.Status, Does.Contain("has no nostates"));
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
