namespace PFWolf.Tests;

public class SwitchesTests
{
    [TestCase(new string[0], false)]
    [TestCase(new[] { "hold" }, true)]
    [TestCase(new[] { "HOLD" }, true)]
    [TestCase(new[] { "other", "Hold" }, true)]
    [TestCase(new[] { "holding" }, false)]
    public void A_Door_Is_Held_Open_Only_When_Asked(string[] args, bool held)
    {
        Assert.That(SwitchActions.Hold(args), Is.EqualTo(held));
    }

    [TestCase("WALL1", "wall1", true)]
    [TestCase("WALL1", "WALL2", false)]
    [TestCase(null, "WALL1", false)]
    public void Textures_Match_Whatever_Their_Case(string? shown, string texture, bool shows)
    {
        Assert.That(SwitchActions.Shows(shown, texture), Is.EqualTo(shows));
    }

    [TestCase(-1, 5)]
    [TestCase(5, -1)]
    [TestCase(64, 5)]
    [TestCase(5, 64)]
    public void A_Smart_Switch_Aimed_Off_The_Map_Works_Nothing_But_Counts_As_Done(int x, int y)
    {
        // Arrange: off the map, nothing is looked at
        var switches = new SmartSwitches(null!, null!, null!, (_, _, _) => Assert.Fail("no switch should be thrown"));

        // Act / Assert
        Assert.That(switches.OperateSmartSwitch(x, y, SmartSwitchOp.Toggle), Is.True);
    }
}
