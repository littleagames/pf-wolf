using System.IO.Compression;
using Wolf3D.Assets;
using Wolf3D.Assets.Sounds;
using Wolf3D.Entities.Actors;

namespace Wolf3D.Loaders;

internal class PfWolfPk3Loader
{
    private Dictionary<string, Asset> _assets = [];
    private readonly string? _gamePackId;
    private readonly string _gameReleaseId;

    /// <summary>
    /// Folders holding one subfolder per game pack (actordefs/wolf3d/, gamepacks/spear/)
    /// </summary>
    private static readonly string[] GamePackFolders = ["gamepacks/", "actordefs/", "mapdefs/"];

    /// <summary>
    /// The running release's palette, which PNG graphics and sprites are matched to.
    /// Only read when those references load in GetAssets, after gamepack-info is parsed.
    /// </summary>
    private string GamePalette => Load<GamePackInfoAsset>("gamepack-info").GetGamePalette(_gameReleaseId);

    /// <param name="gamePackId">The running game pack; other packs' folders are skipped so their
    /// assets can't overwrite its own. Null loads every pack.</param>
    /// <param name="gameReleaseId">The running release's key in gamepacks/gamepack-info.yaml</param>
    public PfWolfPk3Loader(string pk3File, string? gamePackId, string gameReleaseId)
    {
        _gamePackId = gamePackId;
        _gameReleaseId = gameReleaseId;

        using ZipArchive archive = ZipFile.OpenRead(pk3File);

        foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => entry.Length > 0 && entry.IsEncrypted == false))
        {
            if (IsOtherGamePack(entry.FullName))
                continue;

            var assetName = GetAssetReadyName(entry.Name);
            if (entry.FullName.StartsWith("gamepacks/gamepack-info"))
            {
                // TODO: Identify this one as a unique, there should only be one of these
                try
                {
                    //Dictionary<string, GamePack>
                    var data = YamlDataEntryLoader.Read<Dictionary<string, GamePack>>(entry.Open());
                    AddAsset("gamepack-info", new GamePackInfoAsset(data));
                    continue;
                }   
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing YAML from '{entry.FullName}': {ex.GetType().Name}");
                    Console.WriteLine($"Message: {ex.Message}");
                    if (ex.InnerException != null)
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    throw;
                }
            }
            if (entry.FullName.StartsWith("gamepacks/") && entry.FullName.Contains("alias"))
            {
                var uniqueName = GetAssetReadyName(entry.FullName, ignoreFirstDirectory: true);
                var data = YamlDataEntryLoader.Read<AliasAsset>(entry.Open());
                AddAsset(uniqueName, data);
                continue;
            }
            if (entry.FullName.StartsWith("gamepacks/") && entry.FullName.Contains("game-info"))
            {
                var uniqueName = GetAssetReadyName(entry.FullName, ignoreFirstDirectory: true);
                var data = YamlDataEntryLoader.Read<GameInfoAsset>(entry.Open());
                AddAsset(uniqueName, data);
                continue;
            }
            if (entry.FullName.StartsWith("gamepacks/") && entry.FullName.Contains("raw-data-map"))
            {
                var uniqueName = GetAssetReadyName(entry.FullName, ignoreFirstDirectory: true);
                var data = YamlDataEntryLoader.Read<RawDataMapAsset>(entry.Open());
                MergeAsset(uniqueName, data);
                continue;
            }
            if (entry.FullName.StartsWith("gamepacks/") && entry.Name.Equals("colors.yaml", StringComparison.OrdinalIgnoreCase))
            {
                var uniqueName = GetAssetReadyName(entry.FullName, ignoreFirstDirectory: true);
                try
                {
                    var data = YamlDataEntryLoader.Read<Dictionary<string, string>>(entry.Open());
                    MergeAsset(uniqueName, new ColorThemeAsset(data));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading colors from '{entry.FullName}': {ex.Message}");
                    throw;
                }
                continue;
            }
            if (entry.FullName.StartsWith("language/")
                || (entry.FullName.StartsWith("gamepacks/") && entry.FullName.Contains("/language/")))
            {
                // language/en-us -> "language/en-us", gamepacks/wolf3d/language/en-us -> "wolf3d/language/en-us"
                var uniqueName = GetAssetReadyName(entry.FullName, ignoreFirstDirectory: entry.FullName.StartsWith("gamepacks/"));
                var data = YamlDataEntryLoader.Read<Dictionary<string, string>>(entry.Open());
                MergeAsset(uniqueName, new LanguageAsset(data));
                continue;
            }

            if (entry.FullName.StartsWith("actordefs/"))
            {
                // TODO: Move "native.yaml" to parent directory
                var uniqueName = GetPackUniqueAssetName(entry.FullName);
                var data = YamlDataEntryLoader.Read<Dictionary<string, ActorData>>(entry.Open());
                MergeAsset(uniqueName, new ActorTranslationAsset(data));
                continue;
            }

            if (entry.FullName.StartsWith("menudefs/"))
            {
                var data = YamlDataEntryLoader.Read<MenuAsset>(entry.Open());
                AddAsset(assetName, data);
                continue;
            }

            if (entry.FullName.StartsWith("mapdefs/"))
            {
                // TODO: Get the folder after mapdefs to determine the mapdef type (Wolf3d, spear), if there's a second folder, then its map01, map02
                // If there is no folders, then it is the base/default
                var uniqueName = GetPackUniqueAssetName(entry.FullName);
                var data = YamlDataEntryLoader.Read<MapObjectTranslationAsset>(entry.Open());
                MergeAsset(uniqueName, data);
                continue;
            }

            if (entry.FullName.StartsWith("graphics/"))
            {
                // 1) Validate file is a valid graphic to load
                // 2) Load asset reference to pack, and what type it is
                // TODO: distinguish between PNG and other formats by using a "try load" for each data type of a graphic
                // Then I can use this same loader for wolf3d file formats as well
                try
                {
                    AddReference(assetName, () => GraphicDataLoader.Load(Pk3EntryLoader.Open(pk3File, entry.FullName), sourcePalette: Load<Palette>(GamePalette)));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error loading asset '{assetName}': {e.Message}");
                }
                continue;
            }

            if (entry.FullName.StartsWith("palettes/"))
            {
                AddReference(assetName, () => PaletteDataLoader.Load(Pk3EntryLoader.Open(pk3File, entry.FullName)));
                continue;
            }

            if (entry.FullName.StartsWith("sounds/") && entry.FullName.Contains("sound-seq"))
            {
                var data = YamlDataEntryLoader.Read<SoundSequenceAsset>(entry.Open());
                AddAsset(assetName, data);
                continue;
            }

            if (entry.FullName.StartsWith("sprites/"))
            {
                AddReference(assetName, () => PngSpriteDataLoader.Load(Pk3EntryLoader.Open(pk3File, entry.FullName), sourcePalette: Load<Palette>(GamePalette)));
                continue;
            }
        }
    }

    /// <summary>
    /// True for an entry inside another game pack's subfolder of a per-pack folder.
    /// Files directly in those folders (gamepacks/gamepack-info.yaml) belong to every pack.
    /// </summary>
    private bool IsOtherGamePack(string fullName)
    {
        if (string.IsNullOrWhiteSpace(_gamePackId))
            return false;

        foreach (var folder in GamePackFolders)
        {
            if (!fullName.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                continue;

            var packFolderEnd = fullName.IndexOf('/', folder.Length);
            if (packFolderEnd < 0)
                return false;

            var packFolder = fullName.Substring(folder.Length, packFolderEnd - folder.Length);
            return !packFolder.Equals(_gamePackId, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private void AddReference<T>(string assetName, Func<T> assetLoader) where T : Asset
    {
        AddAsset(assetName, new AssetReference<T>(assetLoader));
    }

    private void AddAsset(string assetName, Asset asset, bool overwrite = true)
    {
        var key = GetKey(assetName, GetAssetTypeName(asset));

        if (!_assets.TryAdd(key, asset))
        {
            if (!overwrite)
                return;
            _assets[key] = asset;
        }
    }

    private void MergeAsset(string assetName, Asset asset, bool overwrite = true)
    {
        var key = GetKey(assetName, GetAssetTypeName(asset));

        if (_assets.TryGetValue(key, out var existingAsset))
        {
            existingAsset.Merge(asset);
            return;
        }

        AddAsset(assetName, asset);
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

    public Dictionary<string, Asset> GetAssets()
    {
        var loadedAssets = new Dictionary<string, Asset>();
        foreach (var asset in _assets)
        {
            try
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
                        loadedAssets.Add(asset.Key, (Asset)loadedAsset);
                    }
                }
                else
                {
                    loadedAssets.Add(asset.Key, assetValue);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading asset '{asset.Key}': {e.Message}");
                continue;
            }
        }

        return loadedAssets;
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

    private static string GetAssetReadyName(string fullName, bool ignoreFirstDirectory = false)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return string.Empty;

        var stripExtension = fullName.LastIndexOf('.');

        if (stripExtension >= 0)
            fullName = fullName.Substring(0, stripExtension);

        var fullAssetName = fullName.Replace('\\', '/').Trim().ToLowerInvariant();
        if (ignoreFirstDirectory)
        {
            var parts = fullAssetName.Split('/');
            if (parts.Length > 1)
            {
                fullAssetName = string.Join("/", parts.Skip(1));
            }
        }
        return fullAssetName;
    }

    public static string GetPackUniqueAssetName(string fullname)
    {
        var directory = Path.GetDirectoryName(fullname) ?? "";
        var parts = directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Join("/", parts.Skip(1).Append(parts[0]));
    }
}
