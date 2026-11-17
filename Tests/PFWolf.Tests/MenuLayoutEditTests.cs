using PFWolf.Assets;
using PFWolf.Editor.Definitions;
using PFWolf.Editor.Editing;
using PFWolf.Editor.Rendering;
using PFWolf.Loaders;
using PFWolf.Menus;
using YamlDotNet.RepresentationModel;

namespace PFWolf.Tests;

/// <summary>Parts of a menu's picture dragged, as changes to its fields (Editing/MenuLayoutEdit.cs)</summary>
public class MenuLayoutEditTests
{
    private const string Yaml = """
        position: { x: 76, y: 55 }
        indent: 24
        components:
          - id: background
            type: Background
            params:
              - color: BORDCOLOR
          - id: window
            type: Window
            params:
              - x: 68
              - y: 52
              - width: 178
              - height: 110
          - id: title
            type: Graphic
            params:
              - name: c_options
              - horizontal-orientation: center
              - y: 0
          - type: Nonsense
          - id: heading
            type: Label
            params:
              - text: Hello
              - horizontal-orientation: center
              - y: 30
        menu-items:
          - id: one
            type: MenuSwitcher
            text: One
          - id: two
            type: MenuSwitcher
            text: Two
        """;

    private static YamlMappingNode Editable() => MenuEditing.ToEditable(YamlTree.Parse(Yaml)!);

    private static MenuMetadata Menu(YamlMappingNode editable)
        => MenuMetadata.BuildFromAsset(YamlDataEntryLoader.Deserialize<MenuAsset>(YamlTree.ToText(MenuEditing.FromEditable(editable, null))), _ => { })!;

    private static string? Param(YamlMappingNode editable, int component, string key)
        => (YamlNodes.Get(((YamlSequenceNode)editable.Children[new YamlScalarNode("components")])[component], ["params", key]) as YamlScalarNode)?.Value;

    private static MenuPart Part(MenuPartKind kind, int index, int x = 0, int y = 0, int width = 10, int height = 10)
        => new(kind, index, x, y, width, height, "");

    [Test]
    public void A_Window_Moves_And_Resizes()
    {
        var editable = Editable();

        var moved = MenuLayoutEdit.Apply(editable, Menu(editable), Part(MenuPartKind.Component, 1), MenuDragMode.Move, 4, -2)!;
        var resized = MenuLayoutEdit.Apply(editable, Menu(editable), Part(MenuPartKind.Component, 1), MenuDragMode.Resize, -8, 10)!;

        Assert.That((Param(moved, 1, "x"), Param(moved, 1, "y")), Is.EqualTo(("72", "50")));
        Assert.That((Param(resized, 1, "width"), Param(resized, 1, "height")), Is.EqualTo(("170", "120")));
        Assert.That(Param(editable, 1, "x"), Is.EqualTo("68"), "the menu given is left as it was");
    }

    [Test]
    public void A_Centered_Picture_Only_Moves_Down_Unless_Unlocked()
    {
        var editable = Editable();
        var menu = Menu(editable);
        var title = Part(MenuPartKind.Component, 2, x: 100, y: 0);

        var locked = MenuLayoutEdit.Apply(editable, menu, title, MenuDragMode.Move, 6, 3)!;
        var unlocked = MenuLayoutEdit.Apply(editable, menu, title, MenuDragMode.Move, 6, 3, unlock: true)!;

        Assert.That((Param(locked, 2, "x"), Param(locked, 2, "y"), Param(locked, 2, "horizontal-orientation")), Is.EqualTo(((string?)null, "3", "center")));
        Assert.That((Param(unlocked, 2, "x"), Param(unlocked, 2, "horizontal-orientation")), Is.EqualTo(("106", (string?)null)));
        Assert.That(MenuLayoutEdit.Apply(editable, menu, title, MenuDragMode.Move, 6, 0), Is.Null, "across alone moves nothing");
    }

    [Test]
    public void Components_Count_As_The_Menu_Reads_Them()
    {
        // The Nonsense one isn't read: the menu's fourth component is the label, the list's fifth
        var editable = Editable();

        var moved = MenuLayoutEdit.Apply(editable, Menu(editable), Part(MenuPartKind.Component, 3, x: 140, y: 30), MenuDragMode.Move, 0, 5)!;

        Assert.That(Param(moved, 4, "y"), Is.EqualTo("35"));
    }

    [Test]
    public void The_Background_Stays()
    {
        var editable = Editable();

        Assert.That(MenuLayoutEdit.CanMove(Part(MenuPartKind.Component, 0), Menu(editable)), Is.False);
        Assert.That(MenuLayoutEdit.CanResize(Part(MenuPartKind.Component, 1), Menu(editable)), Is.True);
        Assert.That(MenuLayoutEdit.CanResize(Part(MenuPartKind.Component, 2), Menu(editable)), Is.False);
    }

    [Test]
    public void The_Rows_Move_Their_Position_And_Space_With_Shift()
    {
        var editable = Editable();
        var menu = Menu(editable);

        var moved = MenuLayoutEdit.Apply(editable, menu, Part(MenuPartKind.Row, 1), MenuDragMode.Move, -6, 9)!;
        var spaced = MenuLayoutEdit.Apply(editable, menu, Part(MenuPartKind.Row, 1), MenuDragMode.Spacing, 4, 3)!;

        Assert.That(YamlTree.ToText(moved), Does.Contain("x: 70").And.Contain("y: 64"));
        Assert.That((YamlNodes.Get(spaced, ["indent"]) as YamlScalarNode)?.Value, Is.EqualTo("28"));
        Assert.That((YamlNodes.Get(spaced, ["row-height"]) as YamlScalarNode)?.Value, Is.EqualTo("16"), "13, and the second row 3 lower");
    }

    [Test]
    public void The_Highlighted_Rows_Picture_Gets_A_Selection_Pic_Where_It_Is()
    {
        var editable = Editable();

        var moved = MenuLayoutEdit.Apply(editable, Menu(editable), Part(MenuPartKind.Picture, 0, x: 200, y: 60, width: 32, height: 40), MenuDragMode.Move, 5, 5)!;

        Assert.That(YamlTree.ToText((YamlMappingNode)YamlNodes.Get(moved, ["selection-pic"])!), Does.Contain("x: 205").And.Contain("y: 65"));
    }

    [Test]
    public void The_Grid_Rounds_To_Eight()
    {
        var editable = Editable();

        var moved = MenuLayoutEdit.Apply(editable, Menu(editable), Part(MenuPartKind.Component, 1), MenuDragMode.Move, 3, 3, snap: 8)!;

        Assert.That((Param(moved, 1, "x"), Param(moved, 1, "y")), Is.EqualTo(("72", "56")));
    }
}
