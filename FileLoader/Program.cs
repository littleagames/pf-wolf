using CSharpFunctionalExtensions;
using FileLoader;
using PFWolf.Common;
using PFWolf.Common.Assets;
using System.IO;
using System.IO.Compression;

const string BaseDataDirectory = "D:\\projects\\Wolf3D\\PFWolf\\PFWolf-Assets";
const string BasePfWolfPackageFile = "pfwolf.pk3";

var pfWolfBasePk3Path = Path.Combine(BaseDataDirectory, BasePfWolfPackageFile);

var assets = new Dictionary<string, Asset>();

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

var assetLoader = new AssetLoader();
using ZipArchive archive = ZipFile.OpenRead(pfWolfBasePk3Path);

var result = assetLoader.LoadAvailableGamePacks(gamePackPaths, selectedGamePack);


// TODO: Or this stuff is run in the launcher, and gives the player the option to pick a game pack

// TODO: AssetManager.PreLoad(types[]: [GamePackDefs, StartupConfig])
// Types.All = Enum.GetValues<AssetType>
foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => entry.Length > 0 && entry.IsEncrypted == false))
{
    Console.WriteLine(entry.FullName);
    
    if (entry.FullName.StartsWith("palettes/"))
    {
        var name = entry.Name;
        var size = entry.Length;
        var fileNameOnly = Path.GetFileNameWithoutExtension(Path.GetFileName(entry.FullName));
        assets.Add(fileNameOnly, new AssetReference<Palette>(fileNameOnly, entry.FullName));
        continue;
    }
}

var palette = Load<Palette>("wolfpal");

// TODO: Should load change the asset reference to a loaded asset in the dictionary?

// TODO: Unload, return to an asset reference?

Console.ReadKey();

Asset Load<T>(string name) where T : Asset
{
    if (!assets.TryGetValue(name, out var asset))
    {
        throw new KeyNotFoundException($"Asset with name '{name}' not found.");
    }

    if (asset is AssetReference<T> typedAsset)
    {
        // Simulate loading the palette from the entry
        using var stream = archive.GetEntry(typedAsset.Path)?.Open();
        if (stream == null)
        {
            throw new FileNotFoundException($"Asset file '{typedAsset.Path}' not found in archive.");
        }

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        var data = memoryStream.ToArray();
        return new Palette(name, data);
    }

    throw new InvalidDataException($"Unhandled type {typeof(T).Name} as an Asset");
}