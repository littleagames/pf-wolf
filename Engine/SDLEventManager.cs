using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine;

public class SDLEventManager
{
    public void PollEvents()
    {
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
                    break;
                case SDL.EventType.KeyDown:
                    if (e.Key.Key == SDL.Keycode.F12)
                    {
                        //var result = SDL.SetWindowMouseGrab(_windowPtr, true);
                        // TODO: Maybe an event that the video manager can listen to
                    }
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
}
