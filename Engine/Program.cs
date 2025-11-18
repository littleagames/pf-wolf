using CSharpFunctionalExtensions;
using Engine;
using PFWolf.Common;
using PFWolf.Common.Assets;
using PFWolf.Common.Scenes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

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

var defaultPalette = assetManager.SelectedGamePack.GamePalette;

var palette = assetManager.Load<Palette>(defaultPalette!);
var gameConfiguration = new GameConfigurationData
{
    ScreenResolution = new Dimension(ScreenWidth, ScreenHeight),
    DefaultPalette = palette
};

//var assets = assetManager.LoadAll();

SDLVideoManager videoManager = new SDLVideoManager(assetManager, gameConfiguration);

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

var signon = assetManager.Load<Graphic>("wolf3d-signon");
var smallFont = assetManager.Load<Font>("smallfont");
StringBuilder textBuffer = new StringBuilder(256);
SDLInputManager inputManager = new SDLInputManager();
if (!inputManager.Initialize())
{
    SDL.Quit();
    return;
}

SceneManager sceneManager = new SceneManager(videoManager, inputManager, assetManager);

sceneManager.LoadScene("MainMenuScene");

while (!quit)
{
    inputManager.PollEvents();

    if (inputManager.State.QuitPressed)
    {
        quit = true;
        break;
    }

    sceneManager.Update();

    // Calculate elapsed time
    var currentCounter = SDL.GetPerformanceCounter();
    var elapsed = (currentCounter - startCounter) / (double)frequency;

    fpsCounter.Update();

    // TODO: Make this optional via a debug mode
    videoManager.DrawFps(fpsCounter.FPS);

    videoManager.Update();

    //if (inputManager.IsKeyPressed && signonWaitingForPressAKey)
    //{
    //    signonWaitingForPressAKey = false;
    //    changed = true;
    //}
    //SDL.Delay(10);
}

videoManager.ShutDown();
SDL.Quit();

static string PtrToStringUTF8(nint ptr)
{
    if (ptr == 0)
        return string.Empty;

    // Get length up to null terminator
    int len = 0;
    while (Marshal.ReadByte(ptr, len) != 0)
        len++;

    // Copy bytes and decode as UTF-8
    byte[] buffer = new byte[len];
    Marshal.Copy(ptr, buffer, 0, len);
    return Encoding.UTF8.GetString(buffer);
}