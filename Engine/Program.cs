using CSharpFunctionalExtensions;
using Engine;
using PFWolf.Common;
using PFWolf.Common.Assets;
using System.Reflection;

const string BaseDataDirectory = "D:\\projects\\Wolf3D\\PFWolf\\PFWolf-Assets";
const string BasePfWolfPackageFile = "pfwolf.pk3";
const int ScreenWidth = 640;
const int ScreenHeight = 400;

var pfWolfBasePk3Path = Path.Combine(BaseDataDirectory, BasePfWolfPackageFile);

// This parameter will be defined in the Engine args
var gamePackArgs = args.FirstOrDefault(x => x.StartsWith("-gamepack=", StringComparison.InvariantCultureIgnoreCase))?.Split("-gamepack=");
string? selectedGamePack = null;
if (gamePackArgs?.Length == 2)
{
    selectedGamePack = gamePackArgs[1];
    Console.WriteLine($"Game Pack specified: {selectedGamePack}");
}
else
{
    Console.WriteLine("No game pack specified, using default.");
}

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

var assetManager = new AssetManager(BaseDataDirectory, gamePackPaths);

// TODO: These might be stored in the /scripts of the pk3
// TODO: We could have the GamePack work from the SDK, and in the PK3, just like future mod packs, so it'll load them all in via the scripts assets

// Use reflection to get all types with the base class GamePack and instantiate them in a list.
List<RawDataFilePack> rawDataFilePacks = Assembly.GetAssembly(typeof(PFWolf.Common.RawDataFilePack))
    .GetTypes()
    .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(RawDataFilePack)))
    .Select(t => Activator.CreateInstance(t) as RawDataFilePack)
    .Where(gp => gp != null)
    .ToList();

assetManager.AddRawDataFilePackLoaders(rawDataFilePacks);

var result = assetManager.LoadGamePacks(selectedGamePack);

if (result.IsFailure)
{
    Console.WriteLine(result.Error);
    return;
}

foreach (var path in gamePackPaths)
{
    await assetManager.LoadPackage(path);
}

// Load in scripts from packs
// When these are loaded, they will contain more game packs, load those in too

var rawPackResult = assetManager.LoadDataFilePack(selectedGamePack);

//var gstonea1 = assetManager.Load<Texture>("gstonea1"); // from pk3
//var gstonea2 = assetManager.Load<Texture>("gstonea2"); // from vswap

var palette = assetManager.Load<Palette>("wolfpal");
IVideoManager videoManager = new SDLVideoManager(ScreenWidth, ScreenHeight, palette);

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

//var signon = assetManager.Load<Graphic>("wolf3d-signon");
//var signon = assetManager.Load<Graphic>("spear-signon", AssetType.Graphic);
var signon = assetManager.Load<Graphic>("title");

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
    //videoManager.Draw(new Graphic
    //{
    //    Data = Signon.SignOn,
    //    Dimensions = new Vector2(320, 200)
    //},
    videoManager.Draw(signon,
    // Transform
    // Position (x,y)
    // HasChanged: bool
    // TODO: Turns into "offset: Vector2"
    // TODO: Orientation: Top, TopLeft, Left, Center, etc
    position: new Vector2(0, 0),
    // Scaling = Scaling.FitToScreen
    // Scaling.StretchToFit
    // Scaling.??
    dimension: new Dimension(ScreenWidth, ScreenHeight));

    fpsCounter.Update();
    videoManager.DrawFps(fpsCounter.FPS);


    videoManager.Update();
}

videoManager.ShutDown();
SDL.Quit();