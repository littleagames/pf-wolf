using PFWolf.Entities;
using PFWolf.Fonts;
using PFWolf.Menus;

namespace PFWolf.Tests;

public class MenuDefsTests
{
    // Finding an item's action touches none of the managers
    private static MenuDefs Defs() => new(null!, null!, null!, null!);

    [Test]
    public void A_Label_Left_Alone_Keeps_Its_Fonts_Look()
    {
        // Arrange
        var label = new Label();

        // Act / Assert
        Assert.That(MenuPainter.LabelShadow(label), Is.Null);
        Assert.That(MenuPainter.LabelGradient(label), Is.Null);
        Assert.That(MenuPainter.LabelOutline(label), Is.Null);
        Assert.That(MenuPainter.LabelGlow(label), Is.Null);
    }

    [Test]
    public void False_Takes_Away_The_Fonts_Own()
    {
        // Arrange
        var label = new Label { Shadow = false, Gradient = false, Outline = false, Glow = false };

        // Act / Assert
        Assert.That(MenuPainter.LabelShadow(label), Is.EqualTo(FontShadow.None));
        Assert.That(MenuPainter.LabelGradient(label), Is.EqualTo(FontGradient.None));
        Assert.That(MenuPainter.LabelOutline(label), Is.EqualTo(FontOutline.None));
        Assert.That(MenuPainter.LabelGlow(label), Is.EqualTo(FontGlow.None));
    }

    [Test]
    public void Setting_Part_Of_One_Turns_It_On_With_Defaults_For_The_Rest()
    {
        // Arrange
        var label = new Label { ShadowColor = "Red", GradientTop = 10, OutlineColor = "Blue", GlowRadius = 4 };

        // Act / Assert
        Assert.That(MenuPainter.LabelShadow(label), Is.EqualTo(new FontShadow(1, 1, "Red")));
        Assert.That(MenuPainter.LabelGradient(label), Is.EqualTo(new FontGradient(10, -40)));
        Assert.That(MenuPainter.LabelOutline(label), Is.EqualTo(new FontOutline("Blue", 1)));
        Assert.That(MenuPainter.LabelGlow(label), Is.EqualTo(new FontGlow(null, 4, 50)));
    }

    [Test]
    public void Out_Of_Range_Values_Are_Kept_Sensible()
    {
        // Arrange
        var label = new Label { OutlineThickness = 0, GlowRadius = -3, GlowStrength = 500 };

        // Act / Assert
        Assert.That(MenuPainter.LabelOutline(label)!.Thickness, Is.EqualTo(1));
        Assert.That(MenuPainter.LabelGlow(label), Is.EqualTo(new FontGlow(null, 1, 100)));
    }

    [Test]
    public void An_Items_Action_Is_The_One_Registered_By_That_Name()
    {
        // Arrange
        var defs = Defs();
        Func<int, int> options = _ => 7;
        defs.RegisterAction("CP_Options", options);

        // Act
        var action = defs.MapFunction("main-menu", new MenuSwitcher("Options", true, "CP_Options"));

        // Assert
        Assert.That(action, Is.SameAs(options));
    }

    [Test]
    public void An_Unknown_Action_Or_None_Does_Nothing()
    {
        // Arrange
        var defs = Defs();

        // Act / Assert
        Assert.That(defs.MapFunction("main-menu", new MenuSwitcher("Mystery", true, "CP_Nothing")), Is.Null);
        Assert.That(defs.MapFunction("main-menu", new MenuSwitcher { Text = "Plain" }), Is.Null);
        Assert.That(defs.MapFunction("main-menu", null), Is.Null);
    }

    [Test]
    public void A_Text_Screen_Is_Made_For_Its_Script()
    {
        // Arrange
        var defs = Defs();
        string? shown = null;
        defs.TextScreen = script => _ => { shown = script; return 0; };

        // Act
        defs.MapFunction("main-menu", new MenuSwitcher { Text = "Story", Action = MenuDefs.TextScreenAction, Script = "story" })!(0);

        // Assert
        Assert.That(shown, Is.EqualTo("story"));
    }

    [Test]
    public void A_Text_Screen_Without_A_Script_Does_Nothing()
    {
        // Arrange
        var defs = Defs();
        defs.TextScreen = _ => _ => 0;

        // Act / Assert
        Assert.That(defs.MapFunction("main-menu", new MenuSwitcher { Text = "Story", Action = MenuDefs.TextScreenAction }), Is.Null);
    }

    [Test]
    public void Items_Are_Found_By_Id()
    {
        // Arrange
        ControlPanelItem[] items = [new(1, "New Game", null) { Id = "newgame" }, new(1, "Quit", null) { Id = "quit" }];

        // Act / Assert
        Assert.That(MenuDefs.FindMenuItem(items, "quit"), Is.SameAs(items[1]));
        Assert.That(MenuDefs.FindMenuItem(items, "nope"), Is.Null);
        Assert.That(MenuDefs.SelectedId(items, 0), Is.EqualTo("newgame"));
        Assert.That(MenuDefs.SelectedId(items, -1), Is.Null);       // backed out
        Assert.That(MenuDefs.SelectedId(items, 2), Is.Null);
    }
}

public class MoviePlayerTests
{
    // A run: 1 (not the end), its offset and length, then its pixels
    private static byte[] Run(int offset, params byte[] pixels) =>
        [1, 0, (byte)offset, (byte)(offset >> 8), (byte)pixels.Length, 0, .. pixels];

    private static readonly byte[] End = [0, 0, 0, 0, 0, 0];

    [Test]
    public void The_First_Frame_Fills_The_Screen_Then_Copies_Its_Runs()
    {
        // Arrange
        var screen = new byte[10];
        byte[] data = [7, .. Run(2, 1, 2, 3), .. End];

        // Act
        Assets.JamMovieAsset.DrawFrame(screen, data, fill: true);

        // Assert
        Assert.That(screen, Is.EqualTo(new byte[] { 7, 7, 1, 2, 3, 7, 7, 7, 7, 7 }));
    }

    [Test]
    public void Later_Frames_Only_Change_Their_Runs()
    {
        // Arrange
        var screen = new byte[] { 5, 5, 5, 5, 5, 5 };
        byte[] data = [.. Run(0, 9), .. Run(4, 8, 8), .. End];

        // Act
        Assets.JamMovieAsset.DrawFrame(screen, data, fill: false);

        // Assert
        Assert.That(screen, Is.EqualTo(new byte[] { 9, 5, 5, 5, 8, 8 }));
    }

    [Test]
    public void A_Run_Past_The_Screen_Is_Cut_Short()
    {
        // Arrange
        var screen = new byte[4];

        // Act
        Assets.JamMovieAsset.DrawFrame(screen, [.. Run(2, 1, 2, 3, 4), .. End], fill: false);

        // Assert
        Assert.That(screen, Is.EqualTo(new byte[] { 0, 0, 1, 2 }));
    }
}

public class TitleScreensTests
{
    private static TitleScreens WithDemos(params int[] demos) =>
        new(null!, null!, null!, null!, null!, null!, null!, null!,
            new TitleScreenHooks(demos.Contains, _ => false, () => { }, () => { }, _ => { }));

    [Test]
    public void The_Title_Loop_Plays_The_Four_Demos_In_Turn()
    {
        // Arrange
        var titles = WithDemos(0, 1, 2, 3);

        // Act / Assert
        Assert.That(Enumerable.Range(0, 6).Select(_ => titles.TakeNextTitleDemo()), Is.EqualTo(new[] { 0, 1, 2, 3, 0, 1 }));
    }

    [Test]
    public void Demos_The_Game_Lacks_Are_Skipped()
    {
        // Arrange: the Spear demo has only the first
        var titles = WithDemos(0);

        // Act / Assert
        Assert.That(Enumerable.Range(0, 3).Select(_ => titles.TakeNextTitleDemo()), Is.EqualTo(new[] { 0, 0, 0 }));
    }

    [Test]
    public void A_Mods_Demos_Past_The_Fourth_Join_In()
    {
        // Arrange: a mod adds DEMO4 and DEMO7
        var titles = WithDemos(0, 1, 2, 3, 4, 7);

        // Act / Assert
        Assert.That(Enumerable.Range(0, 7).Select(_ => titles.TakeNextTitleDemo()), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 7, 0 }));
    }
}

public class MenuJumpKeyTests
{
    // Twenty rows, eight shown at once; the ones in `off` can't be picked
    private static (ControlPanelMenuInfo, ControlPanelItem[]) Menu(int visibleRows = 8, params int[] off)
    {
        var items = Enumerable.Range(0, 20)
            .Select(i => new ControlPanelItem((short)(off.Contains(i) ? 0 : 1), $"Row {i}", null))
            .ToArray();
        return (new ControlPanelMenuInfo(24, 51, (short)items.Length, 0, 48) { VisibleRows = visibleRows }, items);
    }

    [TestCase(0, ScanCodes.PgDn, 7)]        // a screenful less a row: the old bottom row is the new top one
    [TestCase(15, ScanCodes.PgDn, 19)]      // stops at the last
    [TestCase(19, ScanCodes.PgDn, 19)]
    [TestCase(10, ScanCodes.PgUp, 3)]
    [TestCase(4, ScanCodes.PgUp, 0)]
    [TestCase(10, ScanCodes.Home, 0)]
    [TestCase(10, ScanCodes.End, 19)]
    public void Page_And_Home_And_End_Jump_The_Cursor(int from, ScanCodes key, int to)
    {
        var (info, items) = Menu();
        Assert.That(MenuLoop.JumpTarget(info, items, from, key), Is.EqualTo(to));
    }

    [Test]
    public void A_Row_That_Cant_Be_Picked_Is_Passed_Back_Towards_The_Cursor()
    {
        // Arrange: the first two and the last can't be picked
        var (info, items) = Menu(8, 0, 1, 19);

        // Act / Assert
        Assert.That(MenuLoop.JumpTarget(info, items, 10, ScanCodes.Home), Is.EqualTo(2));
        Assert.That(MenuLoop.JumpTarget(info, items, 10, ScanCodes.End), Is.EqualTo(18));
        Assert.That(MenuLoop.JumpTarget(info, items, 2, ScanCodes.PgUp), Is.EqualTo(2), "nothing above to go to");
    }

    [Test]
    public void In_A_Menu_That_Doesnt_Scroll_Page_Keys_Go_To_The_Ends()
    {
        // Arrange: all twenty shown
        var (info, items) = Menu(visibleRows: 0);

        // Act / Assert
        Assert.That(MenuLoop.JumpTarget(info, items, 10, ScanCodes.PgDn), Is.EqualTo(19));
        Assert.That(MenuLoop.JumpTarget(info, items, 10, ScanCodes.PgUp), Is.EqualTo(0));
    }
}

public class ModsMenuTests
{
    // Wolf3D's menudefs/mods.yaml: text from 72, 8 rows; the window's edge at 300, the scroll marks' text left of 262
    private static MenuMetadata WolfMods() => new() { Position = new(24, 51), Indent = 48, VisibleRows = 8 };

    [TestCase(0, 224)]
    [TestCase(8, 224)]      // they all fit: the names reach the window's edge
    [TestCase(9, 190)]      // they scroll: the names stop short of the "N more" marks
    public void Mod_Names_Keep_Clear_Of_The_Scroll_Marks(int count, int width)
    {
        Assert.That(ModsMenu.ModNameWidth(WolfMods(), count), Is.EqualTo(width));
    }

    [Test]
    public void Under_A_Highlight_Bar_Names_Stop_At_The_Bar()
    {
        // Arrange: Blake Stone's menudefs/blake/mods.yaml
        var menu = new MenuMetadata
        {
            Position = new(40, 51), Indent = 32, VisibleRows = 9,
            Cursor = new Assets.MenuCursorInfo { X = 34, Width = 196 },
        };

        // Act / Assert: the marks go right of the bar, so it's the bar's edge either way
        Assert.That(ModsMenu.ModNameWidth(menu, 4), Is.EqualTo(156));
        Assert.That(ModsMenu.ModNameWidth(menu, 20), Is.EqualTo(156));
    }

    [Test]
    public void The_Catalog_Is_On_The_Docs_Site()
    {
        // Act / Assert: PFWolf.csproj's ModCatalog
        Assert.That(ModCatalog.Url, Is.EqualTo("https://www.littleagames.com/pf-wolf/docs/mods/"));
    }

    [TestCase("https://www.littleagames.com/pf-wolf/docs/mods/", "www.littleagames.com\n/pf-wolf/docs/mods/")]
    [TestCase("https://example.com", "example.com")]
    [TestCase("http://example.com/", "example.com/")]
    public void The_Catalogs_Address_Is_Shown_Without_Its_Scheme_And_Split_Before_Its_Path(string url, string shown)
    {
        // Act / Assert
        Assert.That(ModCatalog.ShortText(url), Is.EqualTo(shown));
    }
}
