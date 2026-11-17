using PFWolf.Assets;
using PFWolf.Definitions;
using PFWolf.Editor.Data;
using PFWolf.Editor.Definitions;
using PFWolf.Editor.Editing;
using PFWolf.Editor.ViewModels;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Tests;

/// <summary>
/// The menu window's fields and saving (menu editor phase 2) against the games: every menu goes
/// through the fields and back unchanged, and an edit saved into a mod is what the game reads.
/// Needs a built game folder with the data files.
/// </summary>
[Category("GameData")]
public class MenuSavingGameTests
{
    private static readonly Dictionary<string, GameContent> Loaded = [];

    private static GameContent Load(string game)
    {
        if (Loaded.TryGetValue(game, out var cached))
            return cached;
        var gameBin = Path.Combine(TestPaths.RepoRoot(), "PFWolf", "bin", "x64");
        var folder = new[] { Environment.GetEnvironmentVariable("PFWOLF_GAMEDATA"), Path.Combine(gameBin, "Debug", "net10.0"), Path.Combine(gameBin, "Release", "net10.0") }
            .FirstOrDefault(folder => !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "pfwolf.pk3")));
        if (folder == null)
            Assert.Ignore("No built game folder with pfwolf.pk3; build PFWolf for x64 or set PFWOLF_GAMEDATA");
        var workingFolder = Directory.GetCurrentDirectory();
        try
        {
            return Loaded[game] = GameContent.Load(folder!, game, []);
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

    // A menu's fields as the window makes them, opened all the way down
    private static FieldNode Fields(YamlMappingNode tree)
    {
        var root = new FieldNode(MenuSchema.Build(), "menu", MenuEditing.ToEditable(tree), null, null, new FieldContext(), isEntry: true);
        Open(root);
        return root;
    }

    private static void Open(FieldNode node)
    {
        node.EnsureChildren();
        foreach (var child in node.Children)
            Open(child);
    }

    [TestCase("wolf3d")]
    [TestCase("spear")]
    [TestCase("blake")]
    [TestCase("planetstrike")]
    public void Every_Menu_Goes_Through_The_Fields_Unchanged(string game)
    {
        var content = Load(game);
        foreach (var entry in MenuCatalog.Build(content))
        {
            var tree = MenuCatalog.Merge(content, entry).Tree!;

            var errors = new List<string>();
            var built = (YamlMappingNode)Fields(tree).Build(errors)!;
            var back = MenuEditing.FromEditable(built, tree);

            Assert.That(errors, Is.Empty, entry.Name);
            Assert.That(YamlNodes.Equal(back, tree), Is.True, $"{entry.Name}:\n{YamlTree.ToText(back)}");
        }
    }

    [Test]
    public void An_Edit_Saved_Into_A_Mod_Is_What_The_Game_Reads()
    {
        // Arrange: Wolf3D's options menu with a row's text changed, a Window's params changed and a row added
        var content = Load("wolf3d");
        var entry = MenuCatalog.Build(content).Single(entry => entry.Name == "options");
        var tree = MenuCatalog.Merge(content, entry).Tree!;
        var edited = MenuEditing.ToEditable(tree);
        var items = (YamlSequenceNode)edited.Children[new YamlScalarNode("menu-items")];
        ((YamlMappingNode)items[0]).Children[new YamlScalarNode("text")] = new YamlScalarNode("Sounds");
        items.Children.Insert(1, new YamlMappingNode { { "id", "mine" }, { "type", "MenuSwitcher" }, { "text", "Mine" }, { "action", "CP_Menu" }, { "menu", "mine" } });
        var window = ((YamlSequenceNode)edited.Children[new YamlScalarNode("components")]).Children.OfType<YamlMappingNode>()
            .First(component => (component.Children[new YamlScalarNode("type")] as YamlScalarNode)?.Value == "Window");
        ((YamlMappingNode)window.Children[new YamlScalarNode("params")]).Children[new YamlScalarNode("height")] = new YamlScalarNode("150");
        var menu = MenuEditing.FromEditable(edited, tree);

        var mod = Path.Combine(Path.GetTempPath(), $"pfwolf-menu-save-{Guid.NewGuid():N}");
        Directory.CreateDirectory(mod);
        try
        {
            // Act
            var write = MenuSaver.Plan(content, "options", entry.Layers, menu, mod);
            MenuSaver.Save(write);

            // Assert: the file as the game merges it is the edited menu
            var saved = new MenuLayer("mod", mod, write.EntryPath);
            var read = MenuCatalog.Merge(content, [.. entry.Layers, saved]);
            Assert.That(YamlNodes.Equal(read.Tree, menu), Is.True, write.Text);
            Assert.That(write.EntryPath, Is.EqualTo("menudefs/options.yaml"));
            Assert.That(write.Text, Does.Contain("id: mine").And.Contain("after:"));
            Assert.That(write.Text, Does.Not.Contain("CP_Sound"), "an item changed is written as only what changed");
            TestContext.Out.WriteLine(write.Text);
            TestContext.Out.WriteLine(string.Join("\n", write.Notes));
        }
        finally
        {
            Directory.Delete(mod, recursive: true);
        }
    }

    [Test]
    public void A_New_Menu_Is_Saved_Whole_As_The_Mods_Own()
    {
        // Arrange
        var content = Load("wolf3d");
        var menu = YamlTree.Parse("position: { x: 76, y: 70 }\nmenu-items:\n  - id: back\n    type: MenuSwitcher\n    text: Back\n")!;
        var mod = Path.Combine(Path.GetTempPath(), $"pfwolf-menu-new-{Guid.NewGuid():N}");
        Directory.CreateDirectory(mod);
        try
        {
            // Act
            var write = MenuSaver.Plan(content, "my-menu", [], menu, mod);
            MenuSaver.Save(write);

            // Assert
            Assert.That(write.EntryPath, Is.EqualTo("menudefs/my-menu.yaml"));
            Assert.That(File.ReadAllText(Path.Combine(mod, "menudefs", "my-menu.yaml")), Does.StartWith("# The my-menu menu, this mod's own").And.Contain("text: Back"));
            var read = MenuCatalog.Merge(content, [new MenuLayer("mod", mod, write.EntryPath)]);
            Assert.That(YamlNodes.Equal(read.Tree, menu), Is.True);
        }
        finally
        {
            Directory.Delete(mod, recursive: true);
        }
    }

    [Test]
    public void Saving_Into_Pfwolf_Pk3_Is_Refused()
    {
        var content = Load("wolf3d");
        var entry = MenuCatalog.Build(content).Single(entry => entry.Name == "options");
        var tree = MenuCatalog.Merge(content, entry).Tree!;

        Assert.Throws<UnauthorizedAccessException>(() =>
            MenuSaver.Plan(content, "options", entry.Layers, tree, Path.Combine(content.GameFolder, AssetManager.BasePk3FileName)));
    }
}
