using PFWolf.Configuration;
using SDL2;

namespace PFWolf.Tests;

public class InputCodeTests
{
    [TestCase("Mouse 1", InputDevice.MouseButton, 1)]
    [TestCase("mouse5", InputDevice.MouseButton, 5)]
    [TestCase("Wheel Up", InputDevice.MouseWheel, 0)]
    [TestCase("wheel down", InputDevice.MouseWheel, 1)]
    [TestCase("Joy 1", InputDevice.JoyButton, 0)]
    [TestCase("Joy 32", InputDevice.JoyButton, 31)]
    [TestCase("Pad A", InputDevice.PadButton, (int)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A)]
    [TestCase("  pad start ", InputDevice.PadButton, (int)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_START)]
    [TestCase("Pad RT", InputDevice.PadAxis, (int)SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERRIGHT * 2 + 1)]
    [TestCase("Pad LStick Up", InputDevice.PadAxis, (int)SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_LEFTY * 2)]
    public void TryParse_Reads_Non_Key_Names(string text, object device, int code)
    {
        // Act
        var parsed = InputCode.TryParse(text, out var input);

        // Assert
        Assert.That(parsed, Is.True);
        Assert.That(input, Is.EqualTo(new InputCode((InputDevice)device, code)));
    }

    [TestCase("Mouse 0")]
    [TestCase("Mouse 6")]
    [TestCase("Joy 0")]
    [TestCase("Joy 33")]
    [TestCase("Pad Nothing")]
    [TestCase("not a key at all")]
    [TestCase("")]
    public void TryParse_Rejects_Unknown_Names(string text)
    {
        // Act
        var parsed = InputCode.TryParse(text, out var input);

        // Assert
        Assert.That(parsed, Is.False);
        Assert.That(input.IsNone, Is.True);
    }

    [Test]
    public void TryParse_Reads_Key_Names_From_SDL()
    {
        // Act
        var parsed = InputCode.TryParse("Space", out var input);

        // Assert
        Assert.That(parsed, Is.True);
        Assert.That(input.Key, Is.EqualTo(ScanCodes.sc_Space));
    }

    [Test]
    public void Every_Name_Round_Trips_Through_TryParse()
    {
        foreach (var name in InputCode.AllNames())
        {
            Assert.That(InputCode.TryParse(name, out var input), Is.True, name);
            Assert.That(input.ToString(), Is.EqualTo(name).IgnoreCase, name);
        }
    }

    [Test]
    public void FromPadAxis_Refuses_A_Trigger_Pushed_Negative()
    {
        // Act
        var input = InputCode.FromPadAxis(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_TRIGGERLEFT, positive: false);

        // Assert
        Assert.That(input.IsNone, Is.True);
    }

    [Test]
    public void PadAxis_Knows_Its_Axis_And_Direction()
    {
        // Act
        var input = InputCode.FromPadAxis(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX, positive: true);

        // Assert
        Assert.That(input.Axis, Is.EqualTo(SDL.SDL_GameControllerAxis.SDL_CONTROLLER_AXIS_RIGHTX));
        Assert.That(input.IsPositive, Is.True);
        Assert.That(input.IsController, Is.True);
    }

    [Test]
    public void Mouse_And_Keys_Are_Not_Controller_Inputs()
    {
        Assert.That(InputCode.FromMouseButton(1).IsController, Is.False);
        Assert.That(InputCode.FromKey(ScanCodes.sc_A).IsController, Is.False);
        Assert.That(InputCode.WheelUp.IsController, Is.False);
        Assert.That(InputCode.FromJoyButton(0).IsController, Is.True);
    }
}
