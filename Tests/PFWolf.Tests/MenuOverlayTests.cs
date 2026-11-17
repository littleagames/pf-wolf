using PFWolf.Editor.Definitions;
using PFWolf.Editor.Editing;
using PFWolf.Loaders;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Tests;

/// <summary>A mod's menudefs file worked out from an edited menu (Editing/MenuOverlay.cs)</summary>
public class MenuOverlayTests
{
    private const string Under = """
        position: { x: 76, y: 55 }
        indent: 24
        music: WONDERIN
        components:
          - id: background
            type: Background
            params:
              - color: BORDCOLOR
          - type: Window
            params:
              - x: 68
              - y: 52
          - id: title
            type: Graphic
            params:
              - name: c_options
              - y: 0
        menu-items:
          - id: newgame
            type: MenuSwitcher
            text: New Game
            action: CP_NewGame
          - id: options
            type: MenuSwitcher
            text: Options
            action: CP_Options
          - id: quit
            type: MenuSwitcher
            text: Quit
            action: CP_Quit
        """;

    private static YamlMappingNode Menu(string yaml) => YamlTree.Parse(yaml)!;

    // The edited menu: the one beneath changed by `change`
    private static YamlMappingNode Edited(Action<YamlMappingNode> change)
    {
        var menu = Menu(Under);
        change(menu);
        return menu;
    }

    private static YamlSequenceNode Items(YamlMappingNode menu) => (YamlSequenceNode)menu.Children[new YamlScalarNode("menu-items")];
    private static YamlSequenceNode Components(YamlMappingNode menu) => (YamlSequenceNode)menu.Children[new YamlScalarNode("components")];

    // Plans the overlay and checks the game makes the edited menu from it
    private static (YamlMappingNode Overlay, List<string> Notes) Plan(YamlMappingNode edited)
    {
        var under = Menu(Under);
        var (overlay, notes) = MenuOverlay.Plan(under, edited);
        Assert.That(YamlNodes.Equal(MenuOverlay.Apply(under, overlay), edited), Is.True, YamlTree.ToText(overlay));
        return (overlay, notes);
    }

    [Test]
    public void A_Changed_Key_Is_All_That_Is_Written()
    {
        var (overlay, notes) = Plan(Edited(menu => menu.Children[new YamlScalarNode("indent")] = new YamlScalarNode("30")));

        Assert.That(MenuOverlay.ToText(overlay).Trim(), Is.EqualTo("indent: 30"));
        Assert.That(notes, Is.Empty);
    }

    [Test]
    public void A_Key_Taken_Out_Is_Removed()
    {
        var (overlay, _) = Plan(Edited(menu => menu.Children.Remove(new YamlScalarNode("music"))));

        Assert.That(YamlTree.IsRemove(overlay.Children[new YamlScalarNode("music")]));
        Assert.That(overlay.Children, Has.Count.EqualTo(1));
    }

    [Test]
    public void A_Changed_Item_Is_Its_Id_And_What_Changed()
    {
        var (overlay, _) = Plan(Edited(menu => ((YamlMappingNode)Items(menu)[1]).Children[new YamlScalarNode("text")] = new YamlScalarNode("Settings")));

        Assert.That(MenuOverlay.ToText(overlay).Trim(), Is.EqualTo("menu-items:\n- id: options\n  text: Settings"));
    }

    [Test]
    public void An_Item_Taken_Out_Is_Removed_By_Id()
    {
        var (overlay, _) = Plan(Edited(menu => Items(menu).Children.RemoveAt(1)));

        Assert.That(MenuOverlay.ToText(overlay).Trim(), Is.EqualTo("menu-items:\n- id: options\n  remove: true"));
    }

    [Test]
    public void A_New_Item_In_The_Middle_Goes_After_The_One_Before_It()
    {
        var (overlay, _) = Plan(Edited(menu => Items(menu).Children.Insert(1, new YamlMappingNode
        {
            { "id", "mymenu" }, { "type", "MenuSwitcher" }, { "text", "Mine" }, { "action", "CP_Menu" }, { "menu", "mine" },
        })));

        var text = YamlTree.ToText(overlay);
        Assert.That(text, Does.Contain("id: mymenu").And.Contain("menu: mine"));
        Assert.That(text, Does.Contain("after: newgame"));
    }

    [Test]
    public void Items_Moved_Say_Where_They_Go()
    {
        var (overlay, notes) = Plan(Edited(menu =>
        {
            var quit = Items(menu)[2];
            Items(menu).Children.RemoveAt(2);
            Items(menu).Children.Insert(0, quit);
        }));

        Assert.That(YamlTree.ToText(overlay), Does.Contain("before: newgame").Or.Contain("after: quit"));
        Assert.That(notes, Is.Empty);
    }

    [Test]
    public void A_Component_With_An_Id_Changes_Its_Params_Whole()
    {
        var (overlay, _) = Plan(Edited(menu =>
            ((YamlMappingNode)Components(menu)[2]).Children[new YamlScalarNode("params")] = new YamlSequenceNode(
                new YamlMappingNode { { "name", "c_options" } }, new YamlMappingNode { { "y", "4" } })));

        var text = YamlTree.ToText(overlay);
        Assert.That(text, Does.Contain("id: title").And.Contain("y: 4").And.Not.Contain("Window"));
    }

    [Test]
    public void A_Component_Without_An_Id_Changed_Writes_Its_List_Whole()
    {
        var (overlay, notes) = Plan(Edited(menu =>
            ((YamlMappingNode)Components(menu)[1]).Children[new YamlScalarNode("params")] = new YamlSequenceNode(
                new YamlMappingNode { { "x", "60" } }, new YamlMappingNode { { "y", "52" } })));

        Assert.That(YamlTree.IsReplace(overlay.Children[new YamlScalarNode("components")]));
        Assert.That(overlay.Children.ContainsKey(new YamlScalarNode("menu-items")), Is.False);
        Assert.That(notes, Has.Count.EqualTo(1).And.Some.Contains("!replace"));
    }

    [Test]
    public void A_New_Menu_Is_Written_Whole()
    {
        var menu = Menu(Under);

        var (overlay, notes) = MenuOverlay.Plan(null, menu);

        Assert.That(YamlNodes.Equal(overlay, menu));
        Assert.That(notes, Is.Empty);
    }

    [Test]
    public void Nothing_Changed_Is_Nothing_Written()
    {
        var (overlay, _) = Plan(Menu(Under));

        Assert.That(overlay.Children, Is.Empty);
    }
}
