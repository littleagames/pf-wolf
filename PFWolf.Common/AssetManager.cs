using CSharpFunctionalExtensions;
using PFWolf.Common.Assets;
using System.IO.Compression;
using filePath = System.String;

namespace PFWolf.Common;

public class AssetManager
{
    private const string GamePackEntryName = "gamepacks/gamepack-info";

    private Dictionary<string, Asset> _assets = [];
    private readonly Dictionary<string, filePath> pk3FilePaths;

    public AssetManager(List<filePath> pk3FilePaths)
    {
        this.pk3FilePaths = pk3FilePaths.ToDictionary(x => Path.GetFileNameWithoutExtension(x), x => x);
    }


    public Task LoadPackage(string directory, string fileName)
    {
        var pk3FileFullPath = Path.Combine(directory, fileName);
        using ZipArchive archive = ZipFile.OpenRead(pk3FileFullPath);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            try
            { 
                if (entry.Length == 0)
                    continue;

                if (entry.FullName.StartsWith("palettes/"))
                {
                    if (entry.Length == 768) // 3 bytes per color, 256 colors
                    {
                       // AddAsset(new AssetReference<Palette>(entry.Name, pk3FileFullPath, AssetType.Palette, () => Pk3DataFileLoader.Load(pk3FileFullPath, entry.FullName))); 
                        // pk3datafileloader(filepath, assetpathinfile, Pallette)
                        // So in this case it will convert to Palette when loaded

                        // This is an example of the asset loaded, but just in "Raw" data until it is mapped to a proper "Palette"
                        //_assets.Add(new RawAsset<Palette>(entry.Name, entry.Open()));

                        // This would straight be a common data model of palette
                        //_assets.Add(new Palette
                        //{
                        //    Name = entry.Name
                        //});
                        // add "loading" strategy and path to assets (isLoaded=false)
                        // this allows for loaded when needed
                    }
                    //assets.Add(new PaletteAsset
                    //{
                    //    Name = CleanName(entry.Name),
                    //    RawData = rawData
                    //});
                    continue;
                }
            }
            catch (Exception ex)
            {
                // Log the error, but continue processing other entries
                Console.WriteLine($"Error processing entry {entry.FullName}: {ex.Message}");
            }
        }

        // How should I handle this?
        // Should I use a <T> to use a specific FileLoader?
        // Then maybe a gamepack will just call this method in the file loader, and just put all of the things in the asset manager
        throw new NotImplementedException();
    }

    public T Load<T>(string assetName) where T : Asset
    {
        if (!_assets.TryGetValue(assetName, out var asset))
        {
            throw new KeyNotFoundException($"Asset with name {assetName} not found.");
        }

        if (asset is not T typedAsset)
        {
            throw new InvalidOperationException($"Asset with name {assetName} is not of type {typeof(T).Name}.");
        }

        return Activator.CreateInstance<T>();
    }

    private void AddAsset(Asset asset)
    {
        if (_assets.ContainsKey(asset.Name))
        {
            throw new InvalidOperationException($"Asset with name {asset.Name} already exists.");
        }

        _assets.Add(asset.Name, asset);
    }

    public Result LoadGamePacks(Maybe<string> selectedGamePack)
    {
        if (selectedGamePack.HasNoValue)
        {
            // choose default gamepack
            return Result.Failure("Cannot determine default game pack at this time.");
        }

        GamePackDefinitions? singletonGamePackInfo = new();
        AddAsset(singletonGamePackInfo);

        Dictionary<string, MapDefinition> mapDefinitions = [];

        foreach (var gamePackPath in this.pk3FilePaths)
        {
            using ZipArchive archive = ZipFile.OpenRead(gamePackPath.Value);
            ZipArchiveEntry gamePackInfo;
            try
            {
                gamePackInfo = archive.Entries.
                    Single(entry => entry.Length > 0 && entry.IsEncrypted == false
                    && entry.FullName.StartsWith(GamePackEntryName));
            }
            catch (InvalidOperationException e) when (e.Message.StartsWith("Sequence Contains No Elements", StringComparison.InvariantCultureIgnoreCase))
            {
                return Result.Failure($"Error: No {GamePackEntryName} found in pack {gamePackPath.Key}.");
            }
            catch (InvalidOperationException e) when (e.Message.StartsWith("Sequence Contains More Than One Element", StringComparison.InvariantCultureIgnoreCase))
            {
                return Result.Failure($"Error: More than 1 {GamePackEntryName} found in pack {gamePackPath.Key}.");
            }

            var gamePackName = Path.GetFileNameWithoutExtension(Path.GetFileName(gamePackInfo.FullName));

            GamePackDefinitions gamePackDefinition;
            
            try 
            {
                gamePackDefinition = new GamePackDefinitions(gamePackInfo.Open());
            }
            catch (YamlDotNet.Core.YamlException ex)
            {
                return Result.Failure($"Error parsing game pack definitions from {gamePackPath.Key}: {ex.Message}");
            }

            singletonGamePackInfo.AddGamePacks(gamePackDefinition.GamePacks);

            foreach (var pack in gamePackDefinition.GamePacks)
            {
                MapDefinition mapDefinition = null!;
                foreach (var defs in pack.Value.MapDefinitions)
                {
                    var definitionEntry = archive.Entries.FirstOrDefault(x => GetAssetReadyName(x.FullName) == GetAssetReadyName(defs));
                    if (definitionEntry == null)
                        continue; // Not found

                    if (mapDefinitions.TryGetValue(pack.Key, out var existingMapDefinition))
                    {
                        mapDefinition.Include(new MapDefinition(definitionEntry.Open()).Definitions);
                        continue;
                    }

                    mapDefinition = new MapDefinition(definitionEntry.Open());
                    mapDefinitions[pack.Key] = mapDefinition;
                }
            }
        }

        if (!singletonGamePackInfo.HasGamePack(selectedGamePack.Value, out GamePackDefinitionDataModel foundGamePack))
        {
            return Result.Failure($"Error: Specified game pack '{selectedGamePack.Value}' not found in loaded packages.");
        }

        foundGamePack.DetermineBasePack(singletonGamePackInfo.GamePacks, [selectedGamePack.Value]);
        if (!mapDefinitions.ContainsKey(foundGamePack.Name) && !string.IsNullOrWhiteSpace(foundGamePack.BasePack))
        {
            mapDefinitions[foundGamePack.Name] = mapDefinitions[foundGamePack.BasePack];
        }

        if (!mapDefinitions.TryGetValue(selectedGamePack.Value, out var selectedMapDefinition))
        {
            return Result.Failure($"Error: No map definitions found for selected game pack '{selectedGamePack.Value}'.");
        }

        AddAsset(selectedMapDefinition!);
        return Result.Success();
    }

    private static string GetAssetReadyName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return string.Empty;

        var stripExtension = fullName.LastIndexOf(".");

        if (stripExtension >= 0)
            fullName = fullName.Substring(0, stripExtension);

        return fullName.Replace('\\', '/').Trim().ToLowerInvariant();

        //var path = Path.GetDirectoryName(fullName);
        //var fileName = Path.GetFileNameWithoutExtension(fullName);
        //var assetReadyPath = Path.Combine(path, fileName);
        //return assetReadyPath;
    }
}