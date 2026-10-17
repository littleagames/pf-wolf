using PFWolf.Entities;

namespace PFWolf.Tests;

public class PlayerInputTests
{
    private static TicCmd Pressing(params ButtonType[] buttons)
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
        var cmd = Pressing(ButtonType.bt_attack, ButtonType.bt_use);

        // Act
        cmd.SetDown(ButtonType.bt_attack, false);

        // Assert
        Assert.That(cmd.IsDown(ButtonType.bt_attack), Is.False);
        Assert.That(cmd.IsDown(ButtonType.bt_use), Is.True);
        Assert.That(cmd.Buttons, Is.EqualTo(1u << (int)ButtonType.bt_use));
    }

    [Test]
    public void First_Frame_Down_Is_A_Fresh_Press()
    {
        // Arrange
        var input = new PlayerInput();

        // Act
        input.Begin(Pressing(ButtonType.bt_use));

        // Assert
        Assert.That(input.IsPressed(ButtonType.bt_use), Is.True);
        Assert.That(input.IsFreshPress(ButtonType.bt_use), Is.True);
    }

    [Test]
    public void Second_Frame_Down_Is_Held_Not_Fresh()
    {
        // Arrange
        var input = new PlayerInput();
        input.Begin(Pressing(ButtonType.bt_use));

        // Act
        input.Begin(Pressing(ButtonType.bt_use));

        // Assert
        Assert.That(input.IsHeld(ButtonType.bt_use), Is.True);
        Assert.That(input.IsFreshPress(ButtonType.bt_use), Is.False);
    }

    [Test]
    public void Releasing_Then_Pressing_Again_Is_Fresh()
    {
        // Arrange
        var input = new PlayerInput();
        input.Begin(Pressing(ButtonType.bt_use));
        input.Begin(Pressing());

        // Act
        input.Begin(Pressing(ButtonType.bt_use));

        // Assert
        Assert.That(input.IsFreshPress(ButtonType.bt_use), Is.True);
    }

    [Test]
    public void Changes_Made_During_A_Frame_Carry_Into_The_Next_Frames_Held()
    {
        // Arrange: the game drops a press mid-frame, so next frame it isn't held
        var input = new PlayerInput();
        input.Begin(Pressing(ButtonType.bt_attack));
        input.SetPressed(ButtonType.bt_attack, false);

        // Act
        input.Begin(Pressing(ButtonType.bt_attack));

        // Assert
        Assert.That(input.IsFreshPress(ButtonType.bt_attack), Is.True);
    }

    [Test]
    public void SetHeld_Marks_A_Press_As_Not_Fresh()
    {
        // Arrange
        var input = new PlayerInput();
        input.Begin(Pressing(ButtonType.bt_use));

        // Act
        input.SetHeld(ButtonType.bt_use, true);

        // Assert
        Assert.That(input.IsFreshPress(ButtonType.bt_use), Is.False);
    }

    [Test]
    public void Reset_Makes_The_Next_Press_Fresh()
    {
        // Arrange
        var input = new PlayerInput();
        input.Begin(Pressing(ButtonType.bt_use));
        input.Reset();

        // Act
        input.Begin(Pressing(ButtonType.bt_use));

        // Assert
        Assert.That(input.IsFreshPress(ButtonType.bt_use), Is.True);
    }

    [Test]
    public void RestoreButtons_Puts_Back_What_Buttons_Gave()
    {
        // Arrange
        var original = new PlayerInput();
        original.Begin(Pressing(ButtonType.bt_attack, ButtonType.bt_run));
        var restored = new PlayerInput();

        // Act
        restored.RestoreButtons(original.Buttons);
        restored.Begin(Pressing(ButtonType.bt_attack));

        // Assert
        Assert.That(restored.IsHeld(ButtonType.bt_attack), Is.True);
        Assert.That(restored.IsHeld(ButtonType.bt_run), Is.True);
    }
}
