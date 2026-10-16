using PFWolf.Assets;
using PFWolf.Entities;
using PFWolf.Loaders;

namespace PFWolf.Tests;

public class MenuHooksTests
{
    private const string Options = """
        music: "WONDERIN"
        position: { x: 52, y: 82 }
        components:
          - { id: "background", type: "Background", params: [ { color: "BORDCOLOR" } ] }
          - { id: "title", type: "Graphic", params: [ { name: "c_options" }, { y: 0 } ] }
        menu-items:
          - { id: "sound", type: "MenuSwitcher", text: "$MENU_SOUND", action: "CP_Sound" }
          - { id: "control", type: "MenuSwitcher", text: "$MENU_CONTROL", action: "CP_Control" }
          - { id: "mods", type: "MenuSwitcher", text: "$MENU_MODS", action: "CP_Mods" }
        """;

    private const string ModOptions = """
        music: "MENUSONG"
        components:
          - { id: "title", params: [ { name: "M_OPTIONS" }, { y: 2 } ] }
        menu-items:
          - { id: "mods", remove: true }
          - { id: "control", text: "Controls" }
          - { id: "credits", type: "MenuSwitcher", text: "Credits", action: "CP_Article", article: "CREDITS", after: "sound" }
        """;

    private static MenuAsset Merge(string baseYaml, string modYaml)
    {
        var tree = YamlTree.Parse(baseYaml)!;
        MenuAsset.MergeYaml(tree, YamlTree.Parse(modYaml)!, deep: true);
        return YamlDataEntryLoader.Deserialize<MenuAsset>(YamlTree.ToText(tree));
    }

    [Test]
    public void A_Mods_Menu_Changes_Removes_And_Adds_Items_By_Id()
    {
        // Act
        var menu = Merge(Options, ModOptions);

        // Assert: the mods item gone, credits after sound, control renamed but still its action
        Assert.That(menu.MenuItems!.Select(i => i.Id), Is.EqualTo(new[] { "sound", "credits", "control" }));
        Assert.That(menu.MenuItems![2].Text, Is.EqualTo("Controls"));
        Assert.That(menu.MenuItems![2].Action, Is.EqualTo("CP_Control"));
        Assert.That(menu.MenuItems![1].Article, Is.EqualTo("CREDITS"));
        Assert.That(menu.Music, Is.EqualTo("MENUSONG"));
    }

    [Test]
    public void A_Component_Is_Changed_By_Its_Id_And_Keeps_Its_Type()
    {
        var menu = Merge(Options, ModOptions);
        var title = menu.Components!.Single(c => c.Id == "title");
        Assert.That(title.Type, Is.EqualTo("Graphic"));
        Assert.That(title.Params.Select(p => p.First()), Is.EqualTo(new[] { KeyValuePair.Create("name", "M_OPTIONS"), KeyValuePair.Create("y", "2") }));
        Assert.That(menu.Components!.Count, Is.EqualTo(2));
    }

    [Test]
    public void Control_Rows_Are_Matched_By_Their_Control_And_Can_Go_Before_One()
    {
        // Arrange
        const string customize = """
            menu-items:
              - { type: "HeaderMenuItem", text: "$STR_CTL_ACTION" }
              - { type: "ControlMenuItem", control: "attack", text: "$STR_CTL_ATTACK" }
              - { type: "ControlMenuItem", control: "use", text: "$STR_CTL_USE" }
              - { type: "ControlMenuItem", control: "slot1", text: "$STR_CTL_KNIFE" }
            """;
        const string mod = """
            menu-items:
              - { type: "ControlMenuItem", control: "reload", text: "Reload", before: "slot1" }
              - { control: "slot1", text: "Knife!" }
            """;

        // Act
        var menu = Merge(customize, mod);

        // Assert
        Assert.That(menu.MenuItems!.Select(i => i.Control ?? i.Text), Is.EqualTo(new[] { "$STR_CTL_ACTION", "attack", "use", "reload", "slot1" }));
        Assert.That(menu.MenuItems![4].Text, Is.EqualTo("Knife!"));
    }

    [Test]
    public void A_List_Tagged_Replace_Replaces_The_Whole()
    {
        var menu = Merge(Options, "menu-items: !replace\n  - { id: \"quit\", type: \"MenuSwitcher\", text: \"Quit\" }\n");
        Assert.That(menu.MenuItems!.Select(i => i.Id), Is.EqualTo(new[] { "quit" }));
    }

    private static PfWolfPk3Loader Load(Dictionary<string, string> baseFiles, Dictionary<string, string> modFiles)
    {
        var basePk3 = new MemoryAssetSource("pfwolf.pk3", new(baseFiles) { ["gamepacks/gamepack-info.yaml"] = "alpha:\n  title: Alpha\n  game-palette: pal\n" });
        return new PfWolfPk3Loader([basePk3], "alpha", "alpha", [new MemoryAssetSource("mod", modFiles)]);
    }

    [Test]
    public void A_Mods_Menudefs_Merge_Over_The_Shared_Menu_And_Can_Add_Their_Own()
    {
        // Act
        var loader = Load(new() { ["menudefs/options.yaml"] = Options },
            new() { ["menudefs/options.yaml"] = ModOptions, ["menudefs/extras.yaml"] = "menu-items:\n  - { id: \"a\", type: \"MenuSwitcher\", text: \"A\" }\n" });

        // Assert
        Assert.That(loader.Load<MenuAsset>("options").MenuItems!.Select(i => i.Id), Is.EqualTo(new[] { "sound", "credits", "control" }));
        Assert.That(loader.Load<MenuAsset>("extras").MenuItems!.Single().Id, Is.EqualTo("a"));
        Assert.That(loader.Warnings, Is.Empty);
    }

    [Test]
    public void A_Mods_Menu_Merges_Over_The_Running_Packs_Own_When_It_Has_One()
    {
        // Act
        var loader = Load(new() { ["menudefs/options.yaml"] = Options, ["menudefs/alpha/options.yaml"] = Options },
            new() { ["menudefs/options.yaml"] = "menu-items:\n  - { id: \"mods\", remove: true }\n" });

        // Assert: the pack's own changed, the shared one as it was
        Assert.That(loader.Load<MenuAsset>("alpha/options").MenuItems!.Count, Is.EqualTo(2));
        Assert.That(loader.Load<MenuAsset>("options").MenuItems!.Count, Is.EqualTo(3));
    }

    [Test]
    public void A_Mods_Items_Can_Open_A_Menu_Show_A_Text_Or_Call_A_Script()
    {
        // Arrange
        var called = new List<string>();
        var defs = new MenuDefs(null!, null!, null!, null!, null!)
        {
            SubMenu = name => { called.Add("menu " + name); return 0; },
            Article = name => { called.Add("article " + name); return 0; },
            Script = name => { called.Add("lua " + name); return 0; },
        };

        // Act
        defs.MapFunction("m", new MenuSwitcher { Action = "CP_Menu", Menu = "extras" })!(0);
        defs.MapFunction("m", new MenuSwitcher { Action = "CP_Article", Article = "CREDITS" })!(0);
        defs.MapFunction("m", new MenuSwitcher { Action = "CP_Lua", Function = "OnCredits" })!(0);

        // Assert
        Assert.That(called, Is.EqualTo(new[] { "menu extras", "article CREDITS", "lua OnCredits" }));
        Assert.That(defs.MapFunction("m", new MenuSwitcher { Action = "CP_Menu" }), Is.Null);     // no menu: nothing to open
    }
}
