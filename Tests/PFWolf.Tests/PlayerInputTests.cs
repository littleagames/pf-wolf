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
        var cmd = Pressing(ButtonType.Attack, ButtonType.Use);

        // Act
        cmd.SetDown(ButtonType.Attack, false);

        // Assert
        Assert.That(cmd.IsDown(ButtonType.Attack), Is.False);
        Assert.That(cmd.IsDown(ButtonType.Use), Is.True);
        Assert.That(cmd.Buttons, Is.EqualTo(1u << (int)ButtonType.Use));
    }

    [Test]
    public void First_Frame_Down_Is_A_Fresh_Press()
    {
        // Arrange
        var input = new PlayerInput();

        // Act
        input.Begin(Pressing(ButtonType.Use));

        // Assert
        Assert.That(input.IsPressed(ButtonType.Use), Is.True);
        Assert.That(input.IsFreshPress(ButtonType.Use), Is.True);
    }

    [Test]
    public void Second_Frame_Down_Is_Held_Not_Fresh()
    {
        // Arrange
        var input = new PlayerInput();
        input.Begin(Pressing(ButtonType.Use));

        // Act
        input.Begin(Pressing(ButtonType.Use));

        // Assert
        Assert.That(input.IsHeld(ButtonType.Use), Is.True);
        Assert.That(input.IsFreshPress(ButtonType.Use), Is.False);
    }

    [Test]
    public void Releasing_Then_Pressing_Again_Is_Fresh()
    {
        // Arrange
        var input = new PlayerInput();
        input.Begin(Pressing(ButtonType.Use));
        input.Begin(Pressing());

        // Act
        input.Begin(Pressing(ButtonType.Use));

        // Assert
        Assert.That(input.IsFreshPress(ButtonType.Use), Is.True);
    }

    [Test]
    public void Changes_Made_During_A_Frame_Carry_Into_The_Next_Frames_Held()
    {
        // Arrange: the game drops a press mid-frame, so next frame it isn't held
        var input = new PlayerInput();
        input.Begin(Pressing(ButtonType.Attack));
        input.SetPressed(ButtonType.Attack, false);

        // Act
        input.Begin(Pressing(ButtonType.Attack));

        // Assert
        Assert.That(input.IsFreshPress(ButtonType.Attack), Is.True);
    }

    [Test]
    public void SetHeld_Marks_A_Press_As_Not_Fresh()
    {
        // Arrange
        var input = new PlayerInput();
        input.Begin(Pressing(ButtonType.Use));

        // Act
        input.SetHeld(ButtonType.Use, true);

        // Assert
        Assert.That(input.IsFreshPress(ButtonType.Use), Is.False);
    }

    [Test]
    public void Reset_Makes_The_Next_Press_Fresh()
    {
        // Arrange
        var input = new PlayerInput();
        input.Begin(Pressing(ButtonType.Use));
        input.Reset();

        // Act
        input.Begin(Pressing(ButtonType.Use));

        // Assert
        Assert.That(input.IsFreshPress(ButtonType.Use), Is.True);
    }

    [Test]
    public void RestoreButtons_Puts_Back_What_Buttons_Gave()
    {
        // Arrange
        var original = new PlayerInput();
        original.Begin(Pressing(ButtonType.Attack, ButtonType.Run));
        var restored = new PlayerInput();

        // Act
        restored.RestoreButtons(original.Buttons);
        restored.Begin(Pressing(ButtonType.Attack));

        // Assert
        Assert.That(restored.IsHeld(ButtonType.Attack), Is.True);
        Assert.That(restored.IsHeld(ButtonType.Run), Is.True);
    }
}
