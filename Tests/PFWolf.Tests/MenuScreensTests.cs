using PFWolf.Entities;
using PFWolf.Fonts;

namespace PFWolf.Tests;

public class MenuDefsTests
{
    // Finding an item's action touches none of the managers
    private static MenuDefs Defs() => new(null!, null!, null!, null!, null!);

    [Test]
    public void A_Label_Left_Alone_Keeps_Its_Fonts_Look()
    {
        // Arrange
        var label = new Label();

        // Act / Assert
        Assert.That(MenuDefs.LabelShadow(label), Is.Null);
        Assert.That(MenuDefs.LabelGradient(label), Is.Null);
        Assert.That(MenuDefs.LabelOutline(label), Is.Null);
        Assert.That(MenuDefs.LabelGlow(label), Is.Null);
    }

    [Test]
    public void False_Takes_Away_The_Fonts_Own()
    {
        // Arrange
        var label = new Label { Shadow = false, Gradient = false, Outline = false, Glow = false };

        // Act / Assert
        Assert.That(MenuDefs.LabelShadow(label), Is.EqualTo(FontShadow.None));
        Assert.That(MenuDefs.LabelGradient(label), Is.EqualTo(FontGradient.None));
        Assert.That(MenuDefs.LabelOutline(label), Is.EqualTo(FontOutline.None));
        Assert.That(MenuDefs.LabelGlow(label), Is.EqualTo(FontGlow.None));
    }

    [Test]
    public void Setting_Part_Of_One_Turns_It_On_With_Defaults_For_The_Rest()
    {
        // Arrange
        var label = new Label { ShadowColor = "Red", GradientTop = 10, OutlineColor = "Blue", GlowRadius = 4 };

        // Act / Assert
        Assert.That(MenuDefs.LabelShadow(label), Is.EqualTo(new FontShadow(1, 1, "Red")));
        Assert.That(MenuDefs.LabelGradient(label), Is.EqualTo(new FontGradient(10, -40)));
        Assert.That(MenuDefs.LabelOutline(label), Is.EqualTo(new FontOutline("Blue", 1)));
        Assert.That(MenuDefs.LabelGlow(label), Is.EqualTo(new FontGlow(null, 4, 50)));
    }

    [Test]
    public void Out_Of_Range_Values_Are_Kept_Sensible()
    {
        // Arrange
        var label = new Label { OutlineThickness = 0, GlowRadius = -3, GlowStrength = 500 };

        // Act / Assert
        Assert.That(MenuDefs.LabelOutline(label)!.Thickness, Is.EqualTo(1));
        Assert.That(MenuDefs.LabelGlow(label), Is.EqualTo(new FontGlow(null, 1, 100)));
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
        MoviePlayer.DrawMovieFrame(screen, data, fill: true);

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
        MoviePlayer.DrawMovieFrame(screen, data, fill: false);

        // Assert
        Assert.That(screen, Is.EqualTo(new byte[] { 9, 5, 5, 5, 8, 8 }));
    }

    [Test]
    public void A_Run_Past_The_Screen_Is_Cut_Short()
    {
        // Arrange
        var screen = new byte[4];

        // Act
        MoviePlayer.DrawMovieFrame(screen, [.. Run(2, 1, 2, 3, 4), .. End], fill: false);

        // Assert
        Assert.That(screen, Is.EqualTo(new byte[] { 0, 0, 1, 2 }));
    }
}

public class TitleScreensTests
{
    private static TitleScreens WithDemos(params int[] demos) =>
        new(null!, null!, null!, null!, null!, null!, null!, null!,
            new TitleScreenHooks(demos.Contains, _ => false, () => { }, () => { }, () => { }, _ => { }));

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

public class ModsMenuTests
{
    [TestCase(0, false, 9, 1)]
    [TestCase(9, false, 9, 1)]          // a full page, no "More mods..."
    [TestCase(10, true, 8, 2)]          // the last row is "More mods..."
    [TestCase(16, true, 8, 2)]
    [TestCase(17, true, 8, 3)]
    public void Mods_Are_Paged_With_A_Row_Kept_For_More(int count, bool paged, int perPage, int pages)
    {
        Assert.That(ModsMenu.Paged(count), Is.EqualTo(paged));
        Assert.That(ModsMenu.PerPage(count), Is.EqualTo(perPage));
        Assert.That(ModsMenu.PageCount(count), Is.EqualTo(pages));
    }
}
