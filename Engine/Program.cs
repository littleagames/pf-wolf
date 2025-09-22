using CSharpFunctionalExtensions;
using PFWolf.Common;
using PFWolf.Common.Assets;

namespace Engine;

internal class Program
{
    private const string BaseDataDirectory = "D:\\projects\\Wolf3D\\PFWolf\\PFWolf-Assets";
    private const string BasePfWolfPackageFile = "pfwolf.pk3";
    private const int ScreenWidth = 640;
    private const int ScreenHeight = 400;

    private IVideoManager videoManager;
    private AssetManager assetManager;

    private Maybe<string> selectedGamePack = Maybe<string>.None;

    internal Program(string[] args)
    {
        //

        // This parameter will be defined in the Engine args
        var gamePackArgs = args.FirstOrDefault(x => x.StartsWith("-gamepack=", StringComparison.InvariantCultureIgnoreCase))?.Split("-gamepack=");
        if (gamePackArgs?.Length == 2)
        {
            selectedGamePack = gamePackArgs[1];
            Console.WriteLine($"Game Pack specified: {selectedGamePack}");
        }
        else
        {
            Console.WriteLine("No game pack specified, using default.");
        }

        var pfWolfBasePk3Path = Path.Combine(BaseDataDirectory, BasePfWolfPackageFile);
        var gamePackPaths = new List<string> {
            pfWolfBasePk3Path
        };

        var pk3Paths = args.Where(x =>
            x.EndsWith(".pk3", StringComparison.InvariantCultureIgnoreCase)
            && Path.Exists(x)).ToList();

        gamePackPaths.AddRange(pk3Paths);
        gamePackPaths = gamePackPaths.Distinct().ToList();

        if (gamePackPaths.Count(x => x.EndsWith(BasePfWolfPackageFile, StringComparison.InvariantCultureIgnoreCase)) > 1)
        {
            Console.WriteLine($"Error: More than one '{BasePfWolfPackageFile}' requested to be loaded.");
            return;
        }

        //
        videoManager = new SDLVideoManager(ScreenWidth, ScreenHeight);
        assetManager = new AssetManager(gamePackPaths);
    }

    internal void Run()
    {
        // TODO: Load in initialization config data
        // This could be things like
        // "last chosen base game pack"
        // screen resolution? Or is that in the standard config?
        //
        assetManager.LoadGamePacks(selectedGamePack);
        
        //await assetManager.LoadPackage(BaseDataDirectory, BasePfWolfPackageFile);

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
        new Program(args).Run();

        return 0;
    }
}