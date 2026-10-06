using PFWolf.Entities;

namespace PFWolf.Tests;

public class PlayerInputTests
{
    private static TicCmd Pressing(params buttontypes[] buttons)
    {
        var cmd = new TicCmd();
        foreach (var button in buttons)
            cmd.SetDown(button, true);
        return cmd;
    }

    [Test]
    public void TicCmd_SetDown_Sets_And_Clears_One_Bit()
    {
        // Arrange
        var cmd = Pressing(buttontypes.bt_attack, buttontypes.bt_use);

        // Act
        cmd.SetDown(buttontypes.bt_attack, false);

        // Assert
        Assert.That(cmd.IsDown(buttontypes.bt_attack), Is.False);
        Assert.That(cmd.IsDown(buttontypes.bt_use), Is.True);
        Assert.That(cmd.Buttons, Is.EqualTo(1u << (int)buttontypes.bt_use));
    }

    [Test]
    public void First_Frame_Down_Is_A_Fresh_Press()
    {
        // Arrange
        var input = new PlayerInput();

        // Act
        input.Begin(Pressing(buttontypes.bt_use));

        // Assert
        Assert.That(input.IsPressed(buttontypes.bt_use), Is.True);
        Assert.That(input.IsFreshPress(buttontypes.bt_use), Is.True);
    }

    [Test]
    public void Second_Frame_Down_Is_Held_Not_Fresh()
    {
        // Arrange
        var input = new PlayerInput();
        input.Begin(Pressing(buttontypes.bt_use));

        // Act
        input.Begin(Pressing(buttontypes.bt_use));

        // Assert
        Assert.That(input.IsHeld(buttontypes.bt_use), Is.True);
        Assert.That(input.IsFreshPress(buttontypes.bt_use), Is.False);
    }

    [Test]
    public void Releasing_Then_Pressing_Again_Is_Fresh()
    {
        // Arrange
        var input = new PlayerInput();
        input.Begin(Pressing(buttontypes.bt_use));
        input.Begin(Pressing());

        // Act
        input.Begin(Pressing(buttontypes.bt_use));

        // Assert
        Assert.That(input.IsFreshPress(buttontypes.bt_use), Is.True);
    }

    [Test]
    public void Changes_Made_During_A_Frame_Carry_Into_The_Next_Frames_Held()
    {
        // Arrange: the game drops a press mid-frame, so next frame it isn't held
        var input = new PlayerInput();
        input.Begin(Pressing(buttontypes.bt_attack));
        input.SetPressed(buttontypes.bt_attack, false);

        // Act
        input.Begin(Pressing(buttontypes.bt_attack));

        // Assert
        Assert.That(input.IsFreshPress(buttontypes.bt_attack), Is.True);
    }

    [Test]
    public void SetHeld_Marks_A_Press_As_Not_Fresh()
    {
        // Arrange
        var input = new PlayerInput();
        input.Begin(Pressing(buttontypes.bt_use));

        // Act
        input.SetHeld(buttontypes.bt_use, true);

        // Assert
        Assert.That(input.IsFreshPress(buttontypes.bt_use), Is.False);
    }

    [Test]
    public void Reset_Makes_The_Next_Press_Fresh()
    {
        // Arrange
        var input = new PlayerInput();
        input.Begin(Pressing(buttontypes.bt_use));
        input.Reset();

        // Act
        input.Begin(Pressing(buttontypes.bt_use));

        // Assert
        Assert.That(input.IsFreshPress(buttontypes.bt_use), Is.True);
    }

    [Test]
    public void RestoreButtons_Puts_Back_What_Buttons_Gave()
    {
        // Arrange
        var original = new PlayerInput();
        original.Begin(Pressing(buttontypes.bt_attack, buttontypes.bt_run));
        var restored = new PlayerInput();

        // Act
        restored.RestoreButtons(original.Buttons);
        restored.Begin(Pressing(buttontypes.bt_attack));

        // Assert
        Assert.That(restored.IsHeld(buttontypes.bt_attack), Is.True);
        Assert.That(restored.IsHeld(buttontypes.bt_run), Is.True);
    }
}
