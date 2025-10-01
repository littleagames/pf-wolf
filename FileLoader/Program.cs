using CSharpFunctionalExtensions;
using PFWolf.Common;

const string BaseDataDirectory = "D:\\projects\\Wolf3D\\PFWolf\\PFWolf-Assets";
const string BasePfWolfPackageFile = "pfwolf.pk3";

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

// TODO: Load all assets into memory, and measure the performance

// Loading assets into memory
//var palette = assetManager.Load<Palette>("wolfpal", AssetType.Palette);
//var signon = assetManager.Load<Graphic>("wolf3d-signon", AssetType.Graphic);

// TODO: Unload the assets when done


// TODO: Unload the assets when done
// Returns the data back to an asset reference

Console.ReadKey();