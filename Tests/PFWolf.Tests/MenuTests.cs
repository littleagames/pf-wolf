namespace PFWolf.Tests;

public class MenuTests
{
    [TestCase("New Game", '\0', 'N')]
    [TestCase("load game", '\0', 'l')]       // the text's own first letter (HandleMenu folds typed letters up)
    [TestCase("Sound", 'x', 'X')]           // its own key wins, upper-cased
    [TestCase("", '\0', '\0')]
    [TestCase("   ", '\0', '\0')]
    public void ItemKey_Is_The_Short_Key_Or_The_First_Letter(string text, char shortKey, char expected)
    {
        // Arrange
        var item = new CP_itemtype(1, text, null) { shortKey = shortKey };

        // Act / Assert
        Assert.That(MenuLoop.ItemKey(item), Is.EqualTo(expected));
    }

    [TestCase(0, 5, -1, false, 0)]          // left stops at the first
    [TestCase(4, 5, 1, false, 4)]           // right stops at the last
    [TestCase(2, 5, 1, false, 3)]
    [TestCase(4, 5, 1, true, 0)]            // Enter wraps round
    [TestCase(0, 5, -1, true, 4)]
    [TestCase(3, 0, 1, true, 0)]            // nothing to choose from
    public void StepMenuChoice_Stops_Or_Wraps(int current, int count, int delta, bool wrap, int expected)
    {
        Assert.That(MenuDrawing.StepMenuChoice(current, count, delta, wrap), Is.EqualTo(expected));
    }

    [TestCase((short)0, false, "DEACTIVE")]
    [TestCase((short)0, true, "DEACTIVE")]
    [TestCase((short)1, false, "TEXTCOLOR")]
    [TestCase((short)1, true, "HIGHLIGHT")]
    [TestCase((short)2, true, "READHCOLOR")]
    [TestCase((short)3, false, "DarkGreen")]    // a locked episode
    public void MenuItemColor_Goes_By_Whether_It_Is_Active_And_Highlighted(short active, bool highlighted, string expected)
    {
        Assert.That(MenuDrawing.MenuItemColor(new CP_itemtype(active, "Item", null), highlighted), Is.EqualTo(expected));
    }

    [Test]
    public void Rows_Go_Down_By_The_Menus_Row_Height()
    {
        // Arrange
        var menu = new CP_iteminfo(x: 72, y: 40, amount: 5, curpos: 0, indent: 24) { rowHeight = 13 };

        // Act / Assert
        Assert.That(MenuDrawing.MenuItemY(menu, 0), Is.EqualTo(40));
        Assert.That(MenuDrawing.MenuItemY(menu, 3), Is.EqualTo(79));
    }
}
