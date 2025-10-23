
using PFWolf.Common;

namespace Engine;

internal class SDLInputManager
{
    public InputState State { get; } = new InputState();

    public bool IsKeyPressed { get; private set; } = false;

    // TODO: Key mapping
        // Menu schema
        // In-Game schema

    public void PollEvents()
    {
        var keyboardState = SDL.GetKeyboardState(out var numKeys);
        while (SDL.PollEvent(out SDL.Event e))
        {
            SDL.Log($"{((SDL.EventType)e.Type)} event polled.");
            switch ((SDL.EventType)e.Type)
            {
                case SDL.EventType.Quit:
                    break;
                case SDL.EventType.TextInput:
                    break;
                case SDL.EventType.KeyUp:
                    IsKeyPressed = !keyboardState.Contains(true);
                    break;
                case SDL.EventType.KeyDown:
                    IsKeyPressed = keyboardState.Contains(true);
                    if (e.Key.Key == SDL.Keycode.F12)
                    {
                        //var result = SDL.SetWindowMouseGrab(_windowPtr, true);
                        // TODO: Maybe an event that the video manager can listen to
                    }
                    //State.KeyPressed.Add(e.Key.Key);
                    break;
                case SDL.EventType.MouseButtonDown:
                    break;
                case SDL.EventType.MouseButtonUp:
                    break;
                case SDL.EventType.MouseWheel:
                    break;
                case SDL.EventType.MouseMotion:
                    SDL.Log($"Mouse moved to ({e.Motion.X}, {e.Motion.Y})");
                        // Can track position of mouse on the screen
                    break;
            }
        }
    }

    internal bool Initialize()
    {
        if (!SDL.InitSubSystem(SDL.InitFlags.Gamepad))
        {
            SDL.LogError(SDL.LogCategory.Video, "Unable to initialize gamepad sub system");
        }

        if (!SDL.InitSubSystem(SDL.InitFlags.Joystick))
        {
            SDL.LogError(SDL.LogCategory.Video, "Unable to initialize joystick sub system");
        }

        return true;
    }
}
