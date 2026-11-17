using PFWolf.Assets;
using PFWolf.Entities;
using PFWolf.Loaders;
using PFWolf.Ui;

namespace PFWolf.Tests;

/// <summary>A menudef's fit-items: the skill and class menus fitting the number the game has (Ui/MenuDefs.cs Fit)</summary>
public class MenuFitTests
{
    // new-game.yaml's: laid out for four, the face needs three, eight fill the screen
    private static readonly MenuFitItems Skills = new() { Rows = 4, MinRows = 3, MaxRows = 8, Grow = ["window"], Move = ["title"] };

    [Test]
    public void The_Laid_Out_Number_Changes_Nothing()
        => Assert.That(MenuDefs.Fit(Skills, 4, 13), Is.EqualTo(default(MenuDefs.MenuFit)));

    [Test]
    public void No_Fit_Items_Changes_Nothing()
        => Assert.That(MenuDefs.Fit(null, 9, 13), Is.EqualTo(default(MenuDefs.MenuFit)));

    [Test]
    public void More_Grow_The_Window_And_Stay_Centered()
    {
        var fit = MenuDefs.Fit(Skills, 6, 13);

        Assert.That(fit.Grow, Is.EqualTo(26));
        Assert.That(fit.FrameDy, Is.EqualTo(-13));
        Assert.That(fit.ItemsDy, Is.EqualTo(-13));
        Assert.That(fit.VisibleRows, Is.Zero);
    }

    [Test]
    public void Fewer_Shrink_It_No_Smaller_Than_Min_Rows_And_Are_Centered_In_It()
    {
        var fit = MenuDefs.Fit(Skills, 1, 13);

        Assert.That(fit.Grow, Is.EqualTo(-13), "three rows' window");
        Assert.That(fit.FrameDy, Is.EqualTo(6));
        Assert.That(fit.ItemsDy, Is.EqualTo(19), "one row in the middle of four");
    }

    [Test]
    public void Without_Pictures_Min_Rows_Is_Left_Out()
    {
        // new-class.yaml's: laid out for seven with room for the class picture
        var classes = new MenuFitItems { Rows = 7, MinRows = 7, MaxRows = 10, Grow = ["window"] };

        Assert.That(MenuDefs.Fit(classes, 4, 13).Grow, Is.Zero, "a picture keeps seven rows");
        Assert.That(MenuDefs.Fit(classes, 4, 13, pictures: false).Grow, Is.EqualTo(-39));
    }

    [Test]
    public void Past_Max_Rows_They_Scroll()
    {
        var fit = MenuDefs.Fit(Skills, 12, 13);

        Assert.That(fit.Grow, Is.EqualTo(52));
        Assert.That(fit.ItemsDy, Is.EqualTo(-26));
        Assert.That(fit.VisibleRows, Is.EqualTo(8));
    }

    [Test]
    public void Grow_Components_Grow_Move_Components_Move_Others_Stay()
    {
        var fit = MenuDefs.Fit(Skills, 6, 13);
        var window = new Window { Id = "window", Y = 90, Height = 67 };
        var title = new Label { Id = "title", Y = 68 };
        var legend = new Graphic { Id = "legend", Y = 184 };

        Assert.That(MenuDefs.Fitted(window, Skills, fit), Is.EqualTo(window with { Y = 77, Height = 93 }));
        Assert.That(MenuDefs.Fitted(title, Skills, fit), Is.EqualTo(title with { Y = 55 }));
        Assert.That(MenuDefs.Fitted(legend, Skills, fit), Is.SameAs(legend));
    }

    [Test]
    public void Selection_Pic_Reads_Its_Clear_Box()
    {
        var asset = YamlDataEntryLoader.Deserialize<MenuAsset>("""
            items-source: "player-classes"
            selection-pic: { x: 200, y: 56, width: 96, height: 100, color: "MENUBACK" }
            """);

        Assert.That(asset.SelectionPic, Is.EqualTo(new MenuSelectionPic { X = 200, Y = 56, Width = 96, Height = 100, Color = "MENUBACK" }));
    }

    [Test]
    public void Selection_Pic_Without_A_Box_Leaves_It_Unset()
    {
        // Blake Stone's: its pictures are all one size, so nothing is cleared
        var asset = YamlDataEntryLoader.Deserialize<MenuAsset>("selection-pic: { x: 192, y: 77 }");

        Assert.That(asset.SelectionPic, Is.EqualTo(new MenuSelectionPic { X = 192, Y = 77 }));
    }

    [Test]
    public void Fit_Items_Reads_From_A_Menudef()
    {
        var asset = YamlDataEntryLoader.Deserialize<MenuAsset>("""
            items-source: "skills"
            fit-items:
              rows: 4
              min-rows: 3
              max-rows: 8
              grow: [ "window" ]
              move: [ "title" ]
            """);

        Assert.That(asset.FitItems, Is.Not.Null);
        Assert.That(asset.FitItems!.Rows, Is.EqualTo(4));
        Assert.That(asset.FitItems.MinRows, Is.EqualTo(3));
        Assert.That(asset.FitItems.MaxRows, Is.EqualTo(8));
        Assert.That(asset.FitItems.Grow, Is.EqualTo(new[] { "window" }));
        Assert.That(asset.FitItems.Move, Is.EqualTo(new[] { "title" }));
    }
}
