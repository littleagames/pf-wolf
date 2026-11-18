using PFWolf.Assets;
using PFWolf.Editor.Data;
using PFWolf.Menus;
using YamlDotNet.Serialization;

namespace PFWolf.Tests;

/// <summary>
/// The editor's menu catalog (Data/MenuCatalog.cs) against the game as loaded: every menu of every
/// game, the Multiplayer mod and each example mod with menus, merged again from its files, reads
/// as the asset the game has. Needs a built game folder with the data files.
/// </summary>
[Category("GameData")]
public class MenuCatalogGameTests
{
    private static readonly Dictionary<string, GameContent> Loaded = [];

    private static GameContent Load(string game, string? mod = null)
    {
        var key = $"{game}|{mod}";
        if (Loaded.TryGetValue(key, out var cached))
            return cached;

        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        var folder = new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");

        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            return Loaded[key] = GameContent.Load(folder!, game, mod != null ? [Path.Combine(TestPaths.RepoRoot(), mod)] : []);
        }
        catch (PFWolf.Exceptions.DataFilesException)
        {
            Assert.Ignore($"{game}'s data files aren't in {folder}");
            throw;
        }
        finally
        {
            Directory.SetCurrentDirectory(workingFolder);
        }
    }

    private static IEnumerable<TestCaseData> Games()
    {
        foreach (var game in new[] { "wolf3d", "wolf3d-shareware", "spear", "spear-demo", "blake", "blake-shareware", "planetstrike" })
            yield return new TestCaseData(game, null).SetArgDisplayNames(game);
        yield return new TestCaseData("wolf3d", "multiplayer-pk3").SetArgDisplayNames("wolf3d+multiplayer-pk3");
        foreach (var mod in Directory.GetDirectories(Path.Combine(TestPaths.RepoRoot(), "examples", "mods")).Order(StringComparer.Ordinal)
                     .Where(mod => Directory.Exists(Path.Combine(mod, "menudefs"))))
        {
            var info = File.ReadLines(Path.Combine(mod, "modinfo.yaml")).FirstOrDefault(l => l.StartsWith("game-packs:"));
            var pack = info?.Split(':', 2)[1].Trim(' ', '[', ']').Split(',')[0].Trim() is { Length: > 0 } first ? first : "wolf3d";
            yield return new TestCaseData(pack, Path.Combine("examples", "mods", Path.GetFileName(mod))).SetArgDisplayNames(Path.GetFileName(mod));
        }
    }

    private static readonly ISerializer Serializer = new SerializerBuilder().DisableAliases()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve).Build();

    [TestCaseSource(nameof(Games))]
    public void Every_Menu_Merged_Again_Is_What_The_Game_Has(string game, string? mod)
    {
        // Arrange
        var content = Load(game, mod);

        // Act
        var entries = MenuCatalog.Build(content);

        // Assert: each one's files merge into the asset the game loaded
        Assert.That(entries, Has.Count.GreaterThan(5));
        foreach (var entry in entries)
        {
            var build = MenuCatalog.Merge(content, entry);
            Assert.That(build.Error, Is.Null, entry.Name);
            Assert.That(entry.Layers, Is.Not.Empty, entry.Name);
            Assert.That(Serializer.Serialize(build.Asset), Is.EqualTo(Serializer.Serialize(content.Find<MenuAsset>(entry.AssetName))), entry.Name);
        }
    }

    [TestCaseSource(nameof(Games))]
    public void The_Menus_The_Game_Gets_To_Have_No_Problems(string game, string? mod)
    {
        // Arrange
        var content = Load(game, mod);
        var entries = MenuCatalog.Build(content);
        var names = entries.Select(entry => entry.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var problems = entries.Where(entry => entry.Reached).SelectMany(entry => MenuCatalog.Problems(content, MenuCatalog.Merge(content, entry), names)
            .Select(problem => $"{entry.Name}: {problem}")).ToList();

        // Assert (Blake Stone has Wolf3D's multiplayer menus, which it never opens)
        Assert.That(problems, Is.Empty);
        Assert.That(entries.Single(entry => entry.Name == "main-menu").Reached);
    }

    [Test]
    public void The_Engines_Menus_Are_Each_Opened_By_It()
    {
        // Arrange
        var content = Load("wolf3d");

        // Act
        var main = MenuCatalog.Build(content).Single(entry => entry.Name == "main-menu");

        // Assert
        Assert.That(main.Uses.Select(use => use.Label), Has.Some.StartsWith("The engine"));
        Assert.That(main.Layers.Select(layer => layer.Label), Is.EqualTo(new[] { "pfwolf.pk3: menudefs/main-menu.yaml" }));
    }

    [Test]
    public void A_Mods_Unsaved_Text_Is_Laid_Over_The_Menu()
    {
        // Arrange: Blake's own main menu, with a mod's overlay changing one item's text
        var content = Load("blake");
        var entry = MenuCatalog.Build(content).Single(entry => entry.Name == "main-menu");
        var overlay = new MenuLayer("probe", Path.GetTempPath(), "menudefs/main-menu.yaml");

        // Act
        var build = MenuCatalog.Merge(content, [.. entry.Layers, overlay],
            layer => layer == overlay ? "menu-items:\n  - id: quit\n    text: \"Bye\"\n" : null);

        // Assert
        Assert.That(entry.AssetName, Is.EqualTo("blake/main-menu"));
        Assert.That(build.Error, Is.Null);
        Assert.That(build.Menu!.MenuItems.Single(item => item.Id == "quit").Text, Is.EqualTo("Bye"));
        Assert.That(build.Menu.MenuItems, Has.Count.EqualTo(content.Find<MenuAsset>("blake/main-menu")!.MenuItems!.Count));
    }

    [Test]
    public void A_Menu_Item_Naming_A_Menu_That_Isnt_There_Is_A_Problem()
    {
        // Arrange
        var content = Load("wolf3d");
        var entry = MenuCatalog.Build(content).Single(entry => entry.Name == "options");
        var overlay = new MenuLayer("probe", Path.GetTempPath(), "menudefs/options.yaml");
        var build = MenuCatalog.Merge(content, [.. entry.Layers, overlay],
            layer => layer == overlay ? "menu-items:\n  - id: extra\n    type: MenuSwitcher\n    text: More\n    action: CP_Menu\n    menu: nowhere\n  - id: odd\n    type: MenuSwitcher\n    text: Odd\n    action: CP_Nothing\n" : null);

        // Act
        var problems = MenuCatalog.Problems(content, build, ["options", "main-menu"]);

        // Assert
        Assert.That(problems, Has.Some.Contains("'nowhere'").And.Some.Contains("CP_Nothing"));
    }
}
