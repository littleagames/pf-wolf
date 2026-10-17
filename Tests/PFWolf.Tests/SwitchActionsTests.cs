using PFWolf.Gameplay;

namespace PFWolf.Tests;

public class SwitchActionsTests
{
    [TestCase("north", ControlDir.North)]
    [TestCase("East", ControlDir.East)]
    [TestCase(" south ", ControlDir.South)]
    [TestCase("WEST", ControlDir.West)]
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
