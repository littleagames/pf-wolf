using System.Text.RegularExpressions;
using PFWolf.Menus;

namespace PFWolf.Tests;

/// <summary>
/// PFWolf.Common's MenuNames (what the editor's menu window knows of the game's menus) against the
/// game's own code and pfwolf.pk3, so the two can't drift
/// </summary>
public class MenuNamesTests
{
    private static string GameSource(string file) => File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), "PFWolf", file));

    [Test]
    public void The_Actions_Are_The_Ones_The_Game_Registers()
    {
        // Arrange: GameHost.Ui's RegisterMenuDefs list of (name, method) pairs
        var source = GameSource("GameHost.Ui.cs");
        int start = source.IndexOf("(string Name, Func<int, int> Action)[] actions =", StringComparison.Ordinal);
        var list = source[start..source.IndexOf("];", start, StringComparison.Ordinal)];

        // Act
        var registered = Regex.Matches(list, @"\(""(?<name>\w+)"",\s*\w+\)").Select(m => m.Groups["name"].Value).ToList();

        // Assert
        Assert.That(registered, Is.EquivalentTo(MenuNames.Actions.Keys));
        Assert.That(MenuNames.ActionMenus.Keys, Is.SubsetOf(MenuNames.Actions.Keys));
    }

    [Test]
    public void Testing_A_Menu_Starts_The_Game_Past_The_Title_On_It()
    {
        // Act
        var arguments = PFWolf.Editor.Editing.GameLaunch.MenuArguments("blake", "my-menu", ["mods/mine"]);

        // Assert
        Assert.That(arguments, Is.EqualTo(new[] { "--game", "blake", "--nowait", "--exec", "menu my-menu", "--file", "mods/mine" }));
    }

    [Test]
    public void The_Menu_Command_Opens_Each_Engine_Menu_By_The_Action_That_Does()
    {
        // Every engine menu but the main menu and the jukebox is opened by one of the game's actions
        // (Ui/ControlPanel.cs RunPendingMenu), or else as a CP_Menu item would
        var opened = MenuNames.ActionMenus.Values.SelectMany(menus => menus).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.That(MenuNames.EngineUses.Keys.Where(name => !opened.Contains(name)), Is.EquivalentTo(new[] { "main-menu", "jukebox" }));
    }

    [Test]
    public void The_Items_Sources_Are_The_Ones_The_Game_Builds()
    {
        // Arrange: MenuDefs builds episodes and skills itself; the rest are registered
        var registered = Regex.Matches(GameSource("GameHost.Ui.cs"), @"RegisterSource\(""(?<name>[\w-]+)""")
            .Select(m => m.Groups["name"].Value);

        // Assert
        Assert.That(registered.Concat(["episodes", "skills"]), Is.EquivalentTo(MenuNames.ItemsSources.Keys));
    }

    [Test]
    public void The_Menus_The_Engine_Opens_Are_Each_In_Pfwolf_Pk3()
    {
        var menudefs = Path.Combine(TestPaths.Pk3SourceFolder(), "menudefs");
        foreach (var name in MenuNames.EngineUses.Keys.Concat(MenuNames.RootMenus).Concat(MenuNames.ActionMenus.Values.SelectMany(menus => menus)))
            Assert.That(File.Exists(Path.Combine(menudefs, $"{name}.yaml")), name);
    }

    [Test]
    public void Every_Menu_The_Game_Code_Opens_By_Name_Is_One_The_Engine_Uses()
    {
        // Arrange: LoadMenu("..."), DrawMenuComponents("...") and the load/save screen's two
        var names = Directory.GetFiles(Path.Combine(TestPaths.RepoRoot(), "PFWolf", "Ui"), "*.cs")
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"(?:LoadMenu|DrawMenuComponents)\(""(?<name>[\w-]+)""")
                .Select(m => m.Groups["name"].Value))
            .Concat(["load-game", "save-game"])
            .Distinct();

        // Assert
        Assert.That(names, Is.SubsetOf(MenuNames.EngineUses.Keys));
    }
}
