using PFWolf.Gameplay;

namespace PFWolf.Tests;

public class SwitchActionsTests
{
    [TestCase("north", ControlDir.di_north)]
    [TestCase("East", ControlDir.di_east)]
    [TestCase(" south ", ControlDir.di_south)]
    [TestCase("WEST", ControlDir.di_west)]
    public void A_Direction_Is_Read_By_Its_Name_In_Any_Case(string name, ControlDir dir)
    {
        Assert.That(SwitchActions.ParseDirection(name), Is.EqualTo(dir));
    }

    [TestCase("up")]
    [TestCase("")]
    [TestCase(null)]
    public void Anything_Else_Is_No_Direction(string? name)
    {
        Assert.That(SwitchActions.ParseDirection(name), Is.Null);
    }
}
