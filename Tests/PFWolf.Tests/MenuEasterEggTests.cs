using PFWolf.Assets;
using PFWolf.Configuration;
using PFWolf.Ui;

namespace PFWolf.Tests;

/// <summary>game-info's menu-easter-eggs: which egg, if any, the held keys bring up</summary>
public class MenuEasterEggTests
{
    private static readonly TitleScreenInfo Screen = new() { Pics = [new PicPlacement { Pic = "IDGUYS1" }] };

    private static MenuEasterEggInfo Egg(params string[] keys) => new() { Keys = [.. keys], Screens = [Screen] };

    private static Func<InputCode, bool> Held(params ScanCodes[] keys) =>
        code => keys.Any(key => InputCode.FromKey(key) == code);

    [Test]
    public void An_Egg_Shows_Only_With_All_Its_Keys_Held()
    {
        // Arrange
        var eggs = new[] { Egg("I", "D") };

        // Act / Assert
        Assert.That(ControlPanel.HeldEasterEgg(eggs, Held(ScanCodes.I, ScanCodes.D))?.Egg, Is.SameAs(eggs[0]));
        Assert.That(ControlPanel.HeldEasterEgg(eggs, Held(ScanCodes.I)), Is.Null);
        Assert.That(ControlPanel.HeldEasterEgg(eggs, Held()), Is.Null);
    }

    [Test]
    public void The_Held_Egg_Comes_With_Its_Keys()
    {
        // Act
        var held = ControlPanel.HeldEasterEgg([Egg("i", "d")], Held(ScanCodes.I, ScanCodes.D));

        // Assert
        Assert.That(held?.Keys, Is.EqualTo(new[] { InputCode.FromKey(ScanCodes.I), InputCode.FromKey(ScanCodes.D) }));
    }

    [Test]
    public void The_First_Egg_Whose_Keys_Are_Held_Wins()
    {
        // Arrange
        var eggs = new[] { Egg("B", "J"), Egg("I", "D"), Egg("I") };

        // Act
        var held = ControlPanel.HeldEasterEgg(eggs, Held(ScanCodes.I, ScanCodes.D));

        // Assert
        Assert.That(held?.Egg, Is.SameAs(eggs[1]));
    }

    [Test]
    public void An_Egg_With_No_Keys_Or_No_Screens_Never_Shows()
    {
        // Arrange
        var eggs = new[] { new MenuEasterEggInfo { Screens = [Screen] }, new MenuEasterEggInfo { Keys = ["I"] } };

        // Act / Assert
        Assert.That(ControlPanel.HeldEasterEgg(eggs, _ => true), Is.Null);
    }

    [Test]
    public void An_Egg_With_An_Unknown_Key_Never_Shows_And_Says_Which()
    {
        // Arrange
        var unknown = new List<string>();

        // Act
        var held = ControlPanel.HeldEasterEgg([Egg("I", "NotAKey")], _ => true, unknown.Add);

        // Assert
        Assert.That(held, Is.Null);
        Assert.That(unknown, Is.EqualTo(new[] { "NotAKey" }));
    }
}
