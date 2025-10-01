using CSharpFunctionalExtensions;
using PFWolf.Common.Assets;
using PFWolf.Common.Loaders;
using System.IO;
using System.IO.Compression;
using static System.Runtime.InteropServices.JavaScript.JSType;
using filePath = System.String;

namespace PFWolf.Common;

public class AssetManager
{
    private const string GamePackEntryName = "gamepacks/gamepack-info";

    private Dictionary<string, Asset> _assets = [];
    private Dictionary<string, RawDataFilePack> _availableGamePacks = new Dictionary<string, RawDataFilePack>();
    private readonly Dictionary<string, filePath> pk3FilePaths;
    private readonly string gameDirectory;

    public AssetManager(filePath gameDirectory, List<filePath> pk3FilePaths)
    {
        this.pk3FilePaths = pk3FilePaths.ToDictionary(x => Path.GetFileNameWithoutExtension(x), x => x);
        this.gameDirectory = gameDirectory;
    }

    /// <summary>
    /// Adds raw game pack definitions to the available list.
    /// These packs don't use the ZipArchive loading mechanism, and are expected to be loaded from raw files with loaders provided.
    /// </summary>
    /// <param name="gamePacks"></param>
    /// <returns></returns>
    public Result AddRawDataFilePackLoaders(List<RawDataFilePack> gamePacks)
    {
        foreach (var pack in gamePacks)
        {
            _availableGamePacks[pack.PackName] = pack;
        }

        return Result.Success();
    }

    /// <summary>
    /// Load a PK3 package and register all assets
    /// </summary>
    /// <param name="pk3FileFullPath"></param>
    /// <returns></returns>
    public async Task<Result> LoadPackage(string pk3FileFullPath)
    {
        using ZipArchive archive = ZipFile.OpenRead(pk3FileFullPath);

        foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => entry.Length > 0 && entry.IsEncrypted == false))
        {
            var assetName = GetAssetReadyName(entry.Name);
            if (entry.FullName.StartsWith("actordefs/"))
            {
                AddReference(assetName, AssetType.ActorDefinition, () => new ActorDefinition());
                continue;
            }

            if (entry.FullName.StartsWith("fonts/"))
            {
                AddReference(assetName, AssetType.Font, () => new Font());
                continue;
            }

            // Get other data in the gamepacks/ directory that is not the gamepack-info asset
            if (entry.FullName.StartsWith("gamepacks/") && !entry.Name.StartsWith("gamepack-info"))
            {
                try
                {
                    var assetDataReference = YamlDataEntryLoader.Read<GamePackAssetReference>(entry.Open());
                    AddAsset(assetName, assetDataReference);
                }
                catch (YamlDotNet.Core.YamlException ex)
                {
                    return Result.Failure($"Error parsing game pack asset references from {entry.FullName}: {ex.Message}");
                }
            }

            if (entry.FullName.StartsWith("graphics/"))
            {
                // 1) Validate file is a valid graphic to load
                // 2) Load asset reference to pack, and what type it is
                // TODO: distinguish between PNG and other formats by using a "try load" for each data type of a graphic
                // Then I can use this same loader for wolf3d file formats as well
                AddReference(assetName, AssetType.Graphic, () => PngGraphicDataLoader.Load(Pk3DataFileLoader.Load(pk3FileFullPath, entry.FullName)));
                continue;
            }

            if (entry.FullName.StartsWith("maps/"))
            {
                AddReference(assetName, AssetType.Map, () => new Map());
                continue;
            }

            if (entry.FullName.StartsWith("menudefs/"))
            {
                AddReference(assetName, AssetType.MenuDefinitions, () => new MenuDefinition());
                continue;
            }

            if (entry.FullName.StartsWith("music/"))
            {
                AddReference(assetName, AssetType.ImfMusic, () => new ImfMusic());
                continue;
            }

            if (entry.FullName.StartsWith("palettes/"))
            {
                AddReference(assetName, AssetType.Palette, () => new Palette(Pk3DataFileLoader.Load(pk3FileFullPath, entry.FullName)));
                continue;
            }

            if (entry.FullName.StartsWith("scripts/"))
            {
                AddReference(assetName, AssetType.Script, () => new Script());
                continue;
            }

            if (entry.FullName.StartsWith("sounds/"))
            {
                AddReference(assetName, AssetType.DigitizedSound, () => new WavSound());
                continue;
            }

            if (entry.FullName.StartsWith("sprites/"))
            {
                AddReference(assetName, AssetType.Sprite, () => new Sprite());
                continue;
            }

            if (entry.FullName.StartsWith("text/"))
            {
                AddReference(assetName, AssetType.Text, () => new Text());
                continue;
            }

            if (entry.FullName.StartsWith("textures/"))
            {
                AddReference(assetName, AssetType.Texture, () => new Texture());
                continue;
            }
        }

        await Task.CompletedTask;
        return Result.Success();
    }

    /// <summary>
    /// Loads an asset into memory, if already loaded, it simply returns the asset.
    /// If the asset is a reference, it will load the asset using the provided loader function
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="assetName"></param>
    /// <param name="assetType"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public T Load<T>(string assetName, AssetType assetType) where T : Asset
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            throw new ArgumentException($"Asset name cannot be empty. Asset Type: {assetType}", nameof(assetName));
        }

        var key = GetKey(assetName, assetType);

        // TODO: Determine if this should just return null if not found, or throw
        if (!_assets.TryGetValue(key, out var asset))
        {
            throw new KeyNotFoundException($"Asset with name {assetName} not found.");
        }

        if (asset is AssetReference<T>)
        {
            var typedAsset = (AssetReference<T>)asset;
            var loadedAsset = typedAsset.Load();
            _assets[key] = loadedAsset; // Replace reference with loaded asset
            return loadedAsset;
        }

        return (T)asset;
    }

    private void AddReference<T>(string assetName, AssetType assetType, Func<T> assetLoader) where T : Asset
    {
        AddAsset(assetName, new AssetReference<T>(assetType, assetLoader));
    }

    private void AddAsset(string assetName, Asset asset, bool overwrite = true)
    {
        var key = GetKey(assetName, asset.Type);

        if (_assets.ContainsKey(key))
        {
            if (!overwrite)
                return;
            _assets[key] = asset;
        }
        else
        {
            _assets.Add(key, asset);
        }
    }

    public Result LoadGamePacks(Maybe<string> selectedGamePack)
    {
        if (selectedGamePack.HasNoValue)
        {
            // choose default gamepack
            return Result.Failure("Cannot determine default game pack at this time.");
        }

        GamePackDefinitions? singletonGamePackInfo = new();
        AddAsset("gamepack-info", singletonGamePackInfo);

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
            catch (InvalidOperationException e) when (e.Message.StartsWith("Sequence contains no matching element", StringComparison.InvariantCultureIgnoreCase))
            {
                return Result.Failure($"Error: No {GamePackEntryName} found in pack {gamePackPath.Key}.");
            }
            catch (InvalidOperationException e) when (e.Message.StartsWith("Sequence contains more than one matching element", StringComparison.InvariantCultureIgnoreCase))
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

        var containsBasePacks = new HashSet<filePath> { selectedGamePack.Value }; // TODO: Save this list outside, so it can be referenced elsewhere? or when loading raw files
        foundGamePack.DetermineBasePack(singletonGamePackInfo.GamePacks, containsBasePacks);
        if (!mapDefinitions.ContainsKey(foundGamePack.Name) && !string.IsNullOrWhiteSpace(foundGamePack.BasePack))
        {
            mapDefinitions[foundGamePack.Name] = mapDefinitions[foundGamePack.BasePack];
        }

        if (!mapDefinitions.TryGetValue(selectedGamePack.Value, out var selectedMapDefinition))
        {
            return Result.Failure($"Error: No map definitions found for selected game pack '{selectedGamePack.Value}'.");
        }

        AddAsset("map-definitions", selectedMapDefinition!);

        return Result.Success();
    }

    public Result LoadDataFilePack(Maybe<string> selectedGamePack)
    {
        if (selectedGamePack.HasNoValue)
        {
            // choose default gamepack
            return Result.Failure("Cannot determine default game pack at this time.");
        }

        // TODO: Get asset GamePackDefinitions singleton
        // Should this imply that LoadGamePacks has been called first?
        var gamePackDefinitions = Load<GamePackDefinitions>("gamepack-info", AssetType.GamePackDefinition);
        if (!gamePackDefinitions.HasGamePack(selectedGamePack.Value, out GamePackDefinitionDataModel foundGamePack))
        {
            return Result.Failure($"Error: Specified game pack '{selectedGamePack.Value}' not found in loaded packages.");
        }

        var containsBasePacks = new HashSet<filePath> { selectedGamePack.Value };
        foundGamePack.DetermineBasePack(gamePackDefinitions.GamePacks, containsBasePacks);

        foreach (var basePack in containsBasePacks) {
            if (_availableGamePacks.TryGetValue(basePack, out var firstPack))
            {
                var assetReferenceMap = Load<GamePackAssetReference>(foundGamePack.GamePackAssetReference, AssetType.GamePackAssetReference);
                var rawDataAssets = firstPack.LoadAssets(gameDirectory, assetReferenceMap: assetReferenceMap);
                foreach (var dataAsset in rawDataAssets) {
                    AddAsset(dataAsset.Key, dataAsset.Value, overwrite: false);
                }
                break;
            }
        }

        return Result.Success();
    }

    private static string GetAssetReadyName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return string.Empty;

        var stripExtension = fullName.LastIndexOf('.');

        if (stripExtension >= 0)
            fullName = fullName.Substring(0, stripExtension);

        return fullName.Replace('\\', '/').Trim().ToLowerInvariant();
    }

    private static string GetKey(string assetName, AssetType assetType)
        => $"{assetType}:{assetName}".ToLowerInvariant();
}