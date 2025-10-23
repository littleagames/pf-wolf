using CSharpFunctionalExtensions;
using PFWolf.Common.Assets;
using PFWolf.Common.DataLoaders;
using System.IO.Compression;
using YamlDotNet.Core.Tokens;

namespace PFWolf.Common;

public class AssetManager
{
    private const string GamePackEntryName = "gamepacks/gamepack-info";

    private Dictionary<string, Asset> _assets = [];
    private Dictionary<string, RawDataFilePack> _availableGamePacks = new Dictionary<string, RawDataFilePack>();
    private readonly Dictionary<string, string> pk3FilePaths;
    private readonly string gameDirectory;

    private InitializedGamePack? _selectedGamePack = null;

    public InitializedGamePack SelectedGamePack
    {
        get
        {
            if (_selectedGamePack == null)
                throw new InvalidOperationException($"Game pack not loaded yet.");

            return _selectedGamePack;
        }
    }

    public AssetManager(string gameDirectory, List<string> pk3FilePaths)
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
                AddReference(assetName, () => new ActorDefinition());
                continue;
            }

            if (entry.FullName.StartsWith("fonts/"))
            {
                AddReference(assetName, () => new Font(new List<FontCharacter>())); // TODO: The loader should figure out what type of font it is
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
                AddReference(assetName, () => GraphicDataLoader.Load(Pk3EntryLoader.Open(pk3FileFullPath, entry.FullName), sourcePalette: Load<Palette>("wolfpal")));
                continue;
            }

            if (entry.FullName.StartsWith("maps/"))
            {
                AddReference(assetName, () => new Map());
                continue;
            }

            if (entry.FullName.StartsWith("menudefs/"))
            {
                AddReference(assetName, () => new MenuDefinition());
                continue;
            }

            if (entry.FullName.StartsWith("music/"))
            {
                AddReference(assetName, () => new ImfMusic());
                continue;
            }

            if (entry.FullName.StartsWith("palettes/"))
            {
                AddReference(assetName, () => PaletteDataLoader.Load(Pk3EntryLoader.Open(pk3FileFullPath, entry.FullName)));
                continue;
            }

            if (entry.FullName.StartsWith("scripts/"))
            {
                if (entry.Name.EndsWith(".cs"))
                {
                    //scriptAssets.Add(new UnpackedScript// TODO: This should be an "unpacked script" object that's not associated to Asset
                    //{
                    //    ScriptName = assetName,
                    //    RawData = Pk3EntryLoader.Load(entry),
                    //    Location = entry.FullName
                    //});
                    continue;
                }
                // TODO: These need to be compiled/interpreted
                //AddReference(assetName, () => new Script());
                continue;
            }

            if (entry.FullName.StartsWith("sounds/"))
            {
                AddReference(assetName, () => new WavSound());
                continue;
            }

            if (entry.FullName.StartsWith("sprites/"))
            {
                AddReference(assetName, () => new Sprite());
                continue;
            }

            if (entry.FullName.StartsWith("text/"))
            {
                AddReference(assetName, () => new Text());
                continue;
            }

            if (entry.FullName.StartsWith("textures/"))
            {
                // This gets the data from the PK3 loader, and then passes it to the texture file loader to determine type
                AddReference(assetName, () => TextureDataLoader.Load(Pk3EntryLoader.Open(pk3FileFullPath, entry.FullName)));
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
    public T Load<T>(string assetName) where T : Asset
    {
        string assetType = typeof(T).Name;

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

    public List<Asset> LoadAll()
    {
        var loadedAssets = new List<Asset>();
        foreach (var asset in _assets)
        {
            var assetValue = asset.Value;
            var assetType = assetValue.GetType();
            if (assetType.IsGenericType && assetType.GetGenericTypeDefinition() == typeof(AssetReference<>))
            {
                // Use reflection to call Load() on AssetReference<T>
                var loadMethod = assetType.GetProperty("Load")?.GetValue(assetValue) as Delegate;
                if (loadMethod != null)
                {
                    var loadedAsset = loadMethod.DynamicInvoke();
                    _assets[asset.Key] = (Asset)loadedAsset; // Replace reference with loaded asset
                    loadedAssets.Add((Asset)loadedAsset);
                }
            }
            else
            {
                loadedAssets.Add(assetValue);
            }
        }
        return loadedAssets;
    }

    private void AddReference<T>(string assetName, Func<T> assetLoader) where T : Asset
    {
        AddAsset(assetName, new AssetReference<T>(assetLoader));
    }

    private void AddAsset(string assetName, Asset asset, bool overwrite = true)
    {
        var key = GetKey(assetName, GetAssetTypeName(asset));

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
                gamePackDefinition = new GamePackDefinitions(gamePackInfo.Open(), gamePackPath.Value);
            }
            catch (YamlDotNet.Core.YamlException ex)
            {
                return Result.Failure($"Error parsing game pack definitions from {gamePackPath.Key}: {ex.Message}");
            }

            singletonGamePackInfo.AddGamePacks(gamePackDefinition.GamePacks);
        }

        if (!singletonGamePackInfo.HasGamePack(selectedGamePack.Value, out _selectedGamePack))
        {
            return Result.Failure($"Error: Specified game pack '{selectedGamePack.Value}' not found in loaded packages.");
        }

        var selectedMapDefinition = new AssetReference<MapDefinition>(() =>
        {
            MapDefinition mapDefinition = null!;
            foreach (var pack in _selectedGamePack.MapDefinitions)
            {
                using ZipArchive archive = ZipFile.OpenRead(pack.Key);
                var definitionEntry = archive.Entries.FirstOrDefault(x => GetAssetReadyName(x.FullName) == GetAssetReadyName(pack.Value));
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

            return mapDefinition;
        });


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

        var gamePackDefinitions = Load<GamePackDefinitions>("gamepack-info");
        if (!gamePackDefinitions.HasGamePack(selectedGamePack.Value, out InitializedGamePack foundGamePack))
        {
            return Result.Failure($"Error: Specified game pack '{selectedGamePack.Value}' not found in loaded packages.");
        }

        var reference = _selectedGamePack?.GamePackAssetReference;
        if (!string.IsNullOrWhiteSpace(reference))
        {
            var pack = _selectedGamePack.FindNameFromAssetReferenceMap(reference);
            // TODO: Find which InitializedGamePack.Name matches with the Assetreferencemap, which allows us to load the RawDataFilePack

            var assetReferenceMap = Load<GamePackAssetReference>(reference);
            if (_availableGamePacks.TryGetValue(pack, out var firstPack))
            {
                var rawDataAssets = firstPack.LoadAssets(gameDirectory, assetReferenceMap: assetReferenceMap);
                foreach (var dataAsset in rawDataAssets)
                {
                    AddAsset(dataAsset.Key, dataAsset.Value, overwrite: false);
                }
            }
        }

        //var assetReference = foundGamePack.GetGamePackAssetReference(); // returns source and assetreference to load
        // TODO: But this works up to the top, and I also need to the game pack's file path source
        //foreach (var basePack in foundGamePack.Definition) {
        //    if (_availableGamePacks.TryGetValue(basePack, out var firstPack))
        //    {
        //        if (string.IsNullOrWhiteSpace(foundGamePack.GamePackAssetReference))
        //        {
        //            // No specified asset reference
        //            continue;
        //        }
        //        var assetReferenceMap = Load<GamePackAssetReference>(foundGamePack.GamePackAssetReference);
        //        var result = firstPack.Validate(gameDirectory);
        //        if (result.IsFailure)
        //            throw new InvalidDataException(result.Error);
        //        var rawDataAssets = firstPack.LoadAssets(gameDirectory, assetReferenceMap: assetReferenceMap);
        //        foreach (var dataAsset in rawDataAssets) {
        //            AddAsset(dataAsset.Key, dataAsset.Value, overwrite: false);
        //        }
        //        break;
        //    }
        //}

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

    private static string GetKey(string assetName, string assetType)
        => $"{assetType}:{assetName}".ToLowerInvariant();

    private static string GetAssetTypeName(Asset asset)
    {
        var type = asset.GetType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(AssetReference<>))
        {
            var genericType = type.GetGenericArguments()[0];
            return genericType.Name;
        }
        return type.Name;
    }
}