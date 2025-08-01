﻿using PFWolf.Common;

namespace Engine;

internal class Program
{

    private readonly int ScreenWidth = 640;
    private readonly int ScreenHeight = 400;

    private IVideoManager videoManager;

    internal Program()
    {
        videoManager = new SDLVideoManager(ScreenWidth, ScreenHeight);
    }

    internal void Run()
    {
        if (!SDL.Init(0))
        {
            SDL.LogError(SDL.LogCategory.Video, "Unable to initialize SDL main.");
            SDL.Quit();
            return;
        }

        if (!videoManager.Initialize())
        {
            SDL.Quit();
            return;
        }


        bool quit = false;

        while (!quit)
        {
            while (SDL.PollEvent(out SDL.Event e))
            {
                if (e.Type == (uint)SDL.EventType.Quit || e is { Type: (uint)SDL.EventType.KeyDown, Key.Key: SDL.Keycode.Escape })
                {
                    quit = true;
                }

            }

            // Render something here
            videoManager.Draw(new Graphic
            {
                Data = Signon.SignOn,
                Dimensions = new Vector2(320, 200)
            },
            // Transform
                // Position (x,y)
                // HasChanged: bool
            // TODO: Turns into "offset: Vector2"
            // TODO: Orientation: Top, TopLeft, Left, Center, etc
            position: new Vector2(0, 0),
            // Scaling = Scaling.FitToScreen
            // Scaling.StretchToFit
            // Scaling.??
            dimension: new Vector2(ScreenWidth, ScreenHeight));
            videoManager.Update();
        }

        videoManager.ShutDown();
        SDL.Quit();
    }

    [STAThread]
    static int Main(string[] args)
    {
        new Program().Run();

        return 0;
    }
}