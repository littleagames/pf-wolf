using PFWolf.Common;

namespace Engine;

internal class Program
{
    private const string BaseDataDirectory = "D:\\projects\\Wolf3D\\PFWolf\\PFWolf-Assets";
    private const string BasePfWolfPackageFile = "pfwolf.pk3";
    private const int ScreenWidth = 640;
    private const int ScreenHeight = 400;

    private IVideoManager videoManager;
    private IAssetManager assetManager;

    internal Program()
    {
        videoManager = new SDLVideoManager(ScreenWidth, ScreenHeight);
        assetManager = new AssetManager();
    }

    internal void Run()
    {
        // TODO: Load in initialization config data
        // This could be things like
        // "last chosen base game pack"
        // screen resolution? Or is that in the standard config?
        //

        assetManager.LoadPackage(BaseDataDirectory, BasePfWolfPackageFile);

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
        var startCounter = SDL.GetPerformanceCounter();
        var frequency = SDL.GetPerformanceFrequency();
        var fpsCounter = new FpsCounter();

        while (!quit)
        {
            while (SDL.PollEvent(out SDL.Event e))
            {
                if (e.Type == (uint)SDL.EventType.Quit || e is { Type: (uint)SDL.EventType.KeyDown, Key.Key: SDL.Keycode.Escape })
                {
                    quit = true;
                }

            }

            // Calculate elapsed time
            var currentCounter = SDL.GetPerformanceCounter();
            var elapsed = (currentCounter - startCounter) / (double)frequency;

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
            
            fpsCounter.Update();
            videoManager.DrawFps(fpsCounter.FPS);


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