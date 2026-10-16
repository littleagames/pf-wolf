using PFWolf.Ui;

namespace PFWolf.Tests;

public class EndStringTests
{
    [Test]
    public void One_Quit_Question_Is_Always_That_One()
    {
        foreach (var r in new[] { 0, 1, 7, 255 })
            Assert.That(ControlPanel.PickEndString(["Quit?"], () => r), Is.EqualTo("Quit?"));
    }

    [Test]
    public void No_Quit_Question_Is_None()
    {
        Assert.That(ControlPanel.PickEndString([], () => 255), Is.EqualTo(""));
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(6)]
    [TestCase(9)]
    public void Any_Random_Numbers_Pick_One_Of_Them(int count)
    {
        var strings = Enumerable.Range(0, count).Select(i => $"#{i}").ToList();
        for (int r1 = 0; r1 < 256; r1++)
            foreach (var r2 in new[] { 0, 1 })
            {
                var draws = new Queue<int>([r1, r2]);
                Assert.That(strings, Does.Contain(ControlPanel.PickEndString(strings, draws.Dequeue)));
            }
    }

    [Test]
    public void Wolf3Ds_Nine_Are_Picked_As_Before()
    {
        // (random & 7) + (random & 1)
        var strings = Enumerable.Range(0, 9).Select(i => $"#{i}").ToList();
        var draws = new Queue<int>([0xFE, 0x01]);
        Assert.That(ControlPanel.PickEndString(strings, draws.Dequeue), Is.EqualTo("#7"));
    }
}
