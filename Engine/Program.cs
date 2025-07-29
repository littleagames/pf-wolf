namespace Engine;

internal class Program
{

    private readonly int ScreenWidth = 640;
    private readonly int ScreenHeight = 400;
    private float Scale => ScreenWidth / 320.0f;

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
        SDL.Event e;

        while (!quit)
        {
            while (SDL.PollEvent(out e))
            {
                if (e.Type == (uint)SDL.EventType.Quit || e is { Type: (uint)SDL.EventType.KeyDown, Key.Key: SDL.Keycode.Escape })
                {
                    quit = true;
                }

            }

            // Render something here
            videoManager.Draw(Signon.SignOn, 0, 0, 320, 200, Scale);
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