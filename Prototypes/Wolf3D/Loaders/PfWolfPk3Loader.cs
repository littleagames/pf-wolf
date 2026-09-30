using Wolf3D.Assets;
using Wolf3D.Assets.Sounds;
using Wolf3D.Entities.Actors;
using YamlDotNet.RepresentationModel;

namespace Wolf3D.Loaders;

/// <summary>
/// Where one step of an asset came from: a source ("pfwolf.pk3", "mymod.pk3", "vswap.wl6"), the
/// file in it, and whether that step added the asset, replaced it, merged into it, or couldn't
/// be loaded and was left out
/// </summary>
internal record AssetOrigin(string Source, string Path, string Action)
{
    public const string LeftOut = "left out";
}

internal class PfWolfPk3Loader
{
    private Dictionary<string, Asset> _assets = [];
    private readonly string? _gamePackId;
    private readonly string _gameReleaseId;
    // The running release's base-pack, whose folders load first under the running pack's names
    private readonly string? _basePackId;

    // Each asset's history, by key, for the assetinfo command
    private readonly Dictionary<string, List<AssetOrigin>> _origins = [];
    // The file being loaded, so AddAsset and MergeAsset can record it
    private AssetSourceEntry? _currentEntry;

    // Every YAML asset's document so far, by key, so a mod's file can be laid over it.
    // Only kept when there are mods to load.
    private readonly Dictionary<string, YamlMappingNode> _yamlTrees = [];
    private readonly bool _keepYamlTrees;

    // Files directly in maps/ other than .wad levels, in load order
    private readonly List<AssetSourceEntry> _mapDataFiles = [];

    /// <summary>
    /// Folders holding one subfolder per game pack (actordefs/wolf3d/, gamepacks/spear/)
    /// </summary>
    private static readonly string[] GamePackFolders = ["gamepacks/", "actordefs/", "mapdefs/"];

    /// <summary>
    /// Files a mod keeps at its root that pfwolf.pk3 keeps in gamepacks/{pack}/
    /// </summary>
    private static readonly HashSet<string> ModRootGamePackFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "game-info.yaml", "colors.yaml", "fonts.yaml", "hud-messages.yaml", "statusbar.yaml", "intermission.yaml", "alias.yaml", "raw-data-map.yaml",
    };

    /// <summary>
    /// The running release's palette, which PNG graphics and sprites are matched to.
    /// Only read when those references load in GetAssets, after gamepack-info is parsed.
    /// </summary>
    private string GamePalette => Load<GamePackInfoAsset>("gamepack-info").GetGamePalette(_gameReleaseId);

    /// <summary>
    /// Files that couldn't be loaded (mostly mods'): each was left out, or fell back to what it replaced
    /// </summary>
    public List<string> Warnings { get; } = [];

    /// <param name="sources">Where the base assets are read from, in load order (pfwolf.pk3). A later
    /// source's asset replaces, or merges into, an earlier one of the same name.</param>
    /// <param name="gamePackId">The running game pack; other packs' folders are skipped so their
    /// assets can't overwrite its own, apart from its base-pack's. Null loads every pack.</param>
    /// <param name="gameReleaseId">The running release's key in gamepacks/gamepack-info.yaml</param>
    /// <param name="modSources">Mods, in load order, laid over the base assets (see LoadModEntries)</param>
    public PfWolfPk3Loader(IReadOnlyList<IAssetSource> sources, string? gamePackId, string gameReleaseId,
        IReadOnlyList<IAssetSource>? modSources = null)
    {
        _gamePackId = gamePackId;
        _gameReleaseId = gameReleaseId;
        modSources ??= [];
        _keepYamlTrees = modSources.Count > 0;

        // Read first: the running release's base-pack decides which pack folders load, and in what order
        _currentEntry = sources
            .SelectMany(source => source.EntryPaths.Select(path => new AssetSourceEntry(source, path)))
            .FirstOrDefault(e => e.FullName.StartsWith("gamepacks/gamepack-info"));
        var gamePackInfo = _currentEntry == null ? null : ReadGamePackInfo(_currentEntry);
        if (gamePackInfo != null)
        {
            AddAsset("gamepack-info", gamePackInfo);
            if (!string.IsNullOrWhiteSpace(gamePackId) && gamePackInfo.GamePacks.ContainsKey(gameReleaseId))
                _basePackId = gamePackInfo.GetGamePack(gameReleaseId).BasePack;
        }

        foreach (var (entry, fullName) in GetEntriesInLoadOrder(sources))
            LoadEntry(entry, fullName, isMod: false);

        LoadModEntries(modSources);
        _currentEntry = null;
    }

    private void LoadEntry(AssetSourceEntry entry, string fullName, bool isMod)
    {
        _currentEntry = entry;
        var assetName = GetAssetReadyName(entry.Name);
        if (fullName.StartsWith("gamepacks/gamepack-info"))
            return;
        if (fullName.StartsWith("gamepacks/") && fullName.Contains("alias"))
        {
            var uniqueName = GetAssetReadyName(fullName, ignoreFirstDirectory: true);
            LoadYaml(entry, uniqueName, isMod, mergeLevels: 0, YamlDataEntryLoader.Deserialize<AliasAsset>);
            return;
        }
        if (fullName.StartsWith("gamepacks/") && fullName.Contains("game-info"))
        {
            var uniqueName = GetAssetReadyName(fullName, ignoreFirstDirectory: true);
            LoadYaml(entry, uniqueName, isMod, mergeLevels: 0, YamlDataEntryLoader.Deserialize<GameInfoAsset>);
            return;
        }
        if (fullName.StartsWith("gamepacks/") && fullName.Contains("raw-data-map"))
        {
            var uniqueName = GetAssetReadyName(fullName, ignoreFirstDirectory: true);
            LoadYaml(entry, uniqueName, isMod, mergeLevels: 1, YamlDataEntryLoader.Deserialize<RawDataMapAsset>);
            return;
        }
        if (fullName.StartsWith("gamepacks/") && entry.Name.Equals("colors.yaml", StringComparison.OrdinalIgnoreCase))
        {
            var uniqueName = GetAssetReadyName(fullName, ignoreFirstDirectory: true);
            try
            {
                LoadYaml(entry, uniqueName, isMod, mergeLevels: 1,
                    yaml => new ColorThemeAsset(YamlDataEntryLoader.Deserialize<Dictionary<string, string>>(yaml)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading colors from '{fullName}': {ex.Message}");
                throw;
            }
            return;
        }
        if (fullName.StartsWith("gamepacks/") && entry.Name.Equals("fonts.yaml", StringComparison.OrdinalIgnoreCase))
        {
            var uniqueName = GetAssetReadyName(fullName, ignoreFirstDirectory: true);
            LoadYaml(entry, uniqueName, isMod, mergeLevels: 1,
                yaml => new FontDefinitionsAsset(YamlDataEntryLoader.Deserialize<Dictionary<string, FontDefinition>>(yaml)));
            return;
        }
        if (fullName.StartsWith("gamepacks/") && entry.Name.Equals("hud-messages.yaml", StringComparison.OrdinalIgnoreCase))
        {
            var uniqueName = GetAssetReadyName(fullName, ignoreFirstDirectory: true);
            LoadYaml(entry, uniqueName, isMod, mergeLevels: 1,
                yaml => new HudMessageStylesAsset(YamlDataEntryLoader.Deserialize<Dictionary<string, HudMessageStyleDefinition>>(yaml)));
            return;
        }
        if (fullName.StartsWith("gamepacks/") && entry.Name.Equals("statusbar.yaml", StringComparison.OrdinalIgnoreCase))
        {
            var uniqueName = GetAssetReadyName(fullName, ignoreFirstDirectory: true);
            LoadYaml(entry, uniqueName, isMod, mergeLevels: 1,
                yaml => new StatusBarAsset(YamlDataEntryLoader.Deserialize<Dictionary<string, StatusBarElement>>(yaml)));
            return;
        }
        if (fullName.StartsWith("gamepacks/") && entry.Name.Equals("intermission.yaml", StringComparison.OrdinalIgnoreCase))
        {
            var uniqueName = GetAssetReadyName(fullName, ignoreFirstDirectory: true);
            LoadYaml(entry, uniqueName, isMod, mergeLevels: 2, YamlDataEntryLoader.Deserialize<IntermissionAsset>);
            return;
        }
        if (fullName.StartsWith("language/")
            || (fullName.StartsWith("gamepacks/") && fullName.Contains("/language/")))
        {
            // language/en-us -> "language/en-us", gamepacks/wolf3d/language/en-us -> "wolf3d/language/en-us"
            var uniqueName = GetAssetReadyName(fullName, ignoreFirstDirectory: fullName.StartsWith("gamepacks/"));
            LoadYaml(entry, uniqueName, isMod, mergeLevels: 1,
                yaml => new LanguageAsset(YamlDataEntryLoader.Deserialize<Dictionary<string, string>>(yaml)));
            return;
        }

        if (fullName.StartsWith("actordefs/"))
        {
            // actordefs/{pack}/*.yaml -> "{pack}/actordefs"; files directly in actordefs/
            // (native.yaml, deathcam.yaml) -> "actordefs", shared by every pack
            var uniqueName = GetPackUniqueAssetName(fullName);
            LoadYaml(entry, uniqueName, isMod, mergeLevels: 1,
                yaml => new ActorTranslationAsset(YamlDataEntryLoader.Deserialize<Dictionary<string, ActorData>>(yaml)));
            return;
        }

        if (fullName.StartsWith("menudefs/"))
        {
            var data = YamlDataEntryLoader.Read<MenuAsset>(entry.Open());
            AddAsset(assetName, data);
            return;
        }

        if (fullName.StartsWith("mapdefs/"))
        {
            // TODO: Get the folder after mapdefs to determine the mapdef type (Wolf3d, spear), if there's a second folder, then its map01, map02
            // If there is no folders, then it is the base/default
            var uniqueName = GetPackUniqueAssetName(fullName);
            LoadYaml(entry, uniqueName, isMod, mergeLevels: 2, YamlDataEntryLoader.Deserialize<MapObjectTranslationAsset>);
            return;
        }

        if (fullName.StartsWith("graphics/"))
        {
            // 1) Validate file is a valid graphic to load
            // 2) Load asset reference to pack, and what type it is
            // TODO: distinguish between PNG and other formats by using a "try load" for each data type of a graphic
            // Then I can use this same loader for wolf3d file formats as well
            try
            {
                AddReference(assetName, () => GraphicDataLoader.Load(entry.Open(), sourcePalette: Load<Palette>(GamePalette)));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading asset '{assetName}': {e.Message}");
            }
            return;
        }

        if (fullName.StartsWith("fonts/"))
        {
            // Wolf3D-format font files, used by name like SmallFont and LargeFont (a file of
            // the same name replaces one of those)
            try
            {
                using var stream = entry.Open();
                AddAsset(assetName, FontAsset.FromFile(stream.ToArray()));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error loading font '{fullName}': {e.Message}");
            }
            return;
        }

        if (fullName.StartsWith("textures/") || fullName.StartsWith("flats/"))
        {
            // wall textures of any size (a VSWAP wall of the same name is replaced); flats/ is
            // ECWolf's folder for floor and ceiling textures, which are textures like any other
            AddReference(assetName, () => TextureAsset.FromGraphic(
                GraphicDataLoader.Load(entry.Open(), sourcePalette: Load<Palette>(GamePalette))));
            return;
        }

        if (fullName.StartsWith("palettes/"))
        {
            AddReference(assetName, () => PaletteDataLoader.Load(entry.Open()));
            return;
        }

        if (fullName.StartsWith("sounds/") && fullName.Contains("sound-seq"))
        {
            LoadYaml(entry, assetName, isMod, mergeLevels: 0, YamlDataEntryLoader.Deserialize<SoundSequenceAsset>);
            return;
        }

        if (fullName.StartsWith("sprites/"))
        {
            AddReference(assetName, () => PngSpriteDataLoader.Load(entry.Open(), sourcePalette: Load<Palette>(GamePalette)));
            return;
        }

        if (fullName.StartsWith("maps/"))
        {
            // maps/NAME.wad, an ECWolf binary map, is the level NAME: in place of the game's own
            // level of that name, or a new one for game-info to send the player to
            if (entry.Name.EndsWith(".wad", StringComparison.OrdinalIgnoreCase))
                AddReference(assetName, () => EcWolfMapLoader.Load(entry.Open().ToArray()));
            else if (fullName.IndexOf('/', "maps/".Length) < 0)
                _mapDataFiles.Add(entry);       // maybe half of a GAMEMAPS/MAPHEAD pair (FindMapFilePair)
            return;
        }
    }

    /// <summary>
    /// Reads a YAML file into an asset. From the base pk3, with <paramref name="mergeLevels"/> it
    /// merges into an earlier asset of the same name through that asset's Merge, which combines
    /// that many levels down (see YamlTree.MergeLevels); with 0 it replaces it. A mod's file is
    /// laid over the document loaded under that name so far (see YamlTree), and what that reads
    /// as replaces the asset, so a mod only has to give what it changes.
    /// </summary>
    private void LoadYaml(AssetSourceEntry entry, string assetName, bool isMod, int mergeLevels, Func<string, Asset> read)
    {
        var yaml = YamlDataEntryLoader.ReadText(entry.Open());
        var asset = read(yaml);
        if (!_keepYamlTrees)
        {
            if (mergeLevels > 0)
                MergeAsset(assetName, asset);
            else
                AddAsset(assetName, asset);
            return;
        }

        var key = GetKey(assetName, GetAssetTypeName(asset));
        var tree = YamlTree.Parse(yaml);

        if (!isMod)
        {
            if (mergeLevels > 0)
                MergeAsset(assetName, asset);
            else
                AddAsset(assetName, asset);

            if (tree == null)
                return;
            if (mergeLevels > 0 && _yamlTrees.TryGetValue(key, out var existingTree))
                YamlTree.MergeLevels(existingTree, tree, mergeLevels);
            else
                _yamlTrees[key] = tree;
            return;
        }

        if (tree != null && _yamlTrees.TryGetValue(key, out var baseTree))
        {
            // Merged into a copy, so a file that doesn't read as the asset leaves the tree as it was
            var merged = YamlTree.Clone(baseTree);
            YamlTree.DeepMerge(merged, tree);
            asset = read(YamlTree.ToText(merged));
            _yamlTrees[key] = merged;
            AddAsset(assetName, asset, action: "merged");
            return;
        }

        if (tree != null)
            _yamlTrees[key] = tree;
        AddAsset(assetName, asset);
    }

    /// <summary>
    /// Loads the mods' files after everything in pfwolf.pk3. Mods keep everything at their root
    /// and apply to whichever game pack is running, so their data files load as if they were in
    /// the running pack's folders (see GetModEntryPath). A file that can't be read is left out.
    /// </summary>
    private void LoadModEntries(IReadOnlyList<IAssetSource> modSources)
    {
        if (modSources.Count > 0 && string.IsNullOrWhiteSpace(_gamePackId))
        {
            Warn("Mods need a game pack to load into; none are loaded");
            return;
        }

        foreach (var source in modSources)
        {
            var skippedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in source.EntryPaths)
            {
                var entry = new AssetSourceEntry(source, path);
                var fullName = GetModEntryPath(entry, skippedFolders);
                if (fullName == null)
                    continue;

                try
                {
                    LoadEntry(entry, fullName, isMod: true);
                }
                catch (Exception e)
                {
                    Warn($"{source.Name}: {path} can't be loaded, so it's left out: {e.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Where a mod's file loads as if it were in pfwolf.pk3 (actordefs/x.yaml ->
    /// actordefs/{pack}/x.yaml, language/en-us.yaml -> gamepacks/{pack}/language/en-us.yaml,
    /// game-info.yaml -> gamepacks/{pack}/game-info.yaml), or null to leave it out
    /// </summary>
    private string? GetModEntryPath(AssetSourceEntry entry, HashSet<string> skippedFolders)
    {
        var path = entry.FullName;
        var slash = path.IndexOf('/');
        if (slash < 0)
            return ModRootGamePackFiles.Contains(path) ? $"gamepacks/{_gamePackId}/{path.ToLowerInvariant()}" : null;

        var folder = path.Substring(0, slash + 1).ToLowerInvariant();
        var rest = path.Substring(slash + 1);
        switch (folder)
        {
            case "actordefs/":
            case "mapdefs/":
            case "language/":
                if (rest.Contains('/'))
                {
                    WarnOnce(entry.Source, skippedFolders, path.Substring(0, path.LastIndexOf('/') + 1),
                        "mods keep these files directly in actordefs/, mapdefs/ and language/, without subfolders");
                    return null;
                }
                return folder == "language/"
                    ? $"gamepacks/{_gamePackId}/language/{rest}"
                    : $"{folder}{_gamePackId}/{rest}";

            case "menudefs/":
                WarnOnce(entry.Source, skippedFolders, folder, "mods can't change menus yet");
                return null;

            case "gamepacks/":
                WarnOnce(entry.Source, skippedFolders, folder,
                    "mods keep game-info.yaml, colors.yaml and the rest at their root, for whichever game is running");
                return null;

            default:
                // graphics/, sprites/, fonts/, palettes/, sounds/: named by file name, as in pfwolf.pk3
                return folder + rest;
        }
    }

    private void WarnOnce(IAssetSource source, HashSet<string> warnedFolders, string folder, string reason)
    {
        if (warnedFolders.Add(folder))
            Warn($"{source.Name}: {folder} is left out: {reason}");
    }

    private void Warn(string message)
    {
        Console.WriteLine(message);
        Warnings.Add(message);
    }

    // TODO: Identify this one as a unique, there should only be one of these
    private static GamePackInfoAsset ReadGamePackInfo(AssetSourceEntry entry)
    {
        try
        {
            var data = YamlDataEntryLoader.Read<Dictionary<string, GamePack>>(entry.Open());
            return new GamePackInfoAsset(data);
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

    /// <summary>
    /// The entries to load, each with the path to load it under. Without a running pack that's
    /// every entry as-is. With one, other packs' folders are left out, except the base pack's:
    /// those come first and are renamed into the running pack's folder (actordefs/wolf3d/guards.yaml
    /// -> actordefs/spear/guards.yaml), so the running pack's own files, loaded after, override or
    /// merge into them under the same asset names. Within each of those two groups, sources keep
    /// their order.
    /// </summary>
    private IEnumerable<(AssetSourceEntry Entry, string FullName)> GetEntriesInLoadOrder(IReadOnlyList<IAssetSource> sources)
    {
        var basePackEntries = new List<(AssetSourceEntry, string)>();
        var entries = new List<(AssetSourceEntry, string)>();

        foreach (var entry in sources.SelectMany(source => source.EntryPaths.Select(path => new AssetSourceEntry(source, path))))
        {
            var packFolder = GetGamePackFolder(entry.FullName);
            if (string.IsNullOrWhiteSpace(_gamePackId) || packFolder == null
                || packFolder.Value.Pack.Equals(_gamePackId, StringComparison.OrdinalIgnoreCase))
            {
                entries.Add((entry, entry.FullName));
            }
            else if (packFolder.Value.Pack.Equals(_basePackId, StringComparison.OrdinalIgnoreCase))
            {
                var (folder, pack) = packFolder.Value;
                basePackEntries.Add((entry, folder + _gamePackId + entry.FullName.Substring(folder.Length + pack.Length)));
            }
        }

        return basePackEntries.Concat(entries);
    }

    /// <summary>
    /// For an entry inside a per-pack folder's pack subfolder (actordefs/wolf3d/guards.yaml),
    /// that folder ("actordefs/") and pack ("wolf3d"). Null otherwise, including files directly
    /// in those folders (gamepacks/gamepack-info.yaml), which belong to every pack.
    /// </summary>
    private static (string Folder, string Pack)? GetGamePackFolder(string fullName)
    {
        foreach (var folder in GamePackFolders)
        {
            if (!fullName.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                continue;

            var packFolderEnd = fullName.IndexOf('/', folder.Length);
            if (packFolderEnd < 0)
                return null;

            return (folder, fullName.Substring(folder.Length, packFolderEnd - folder.Length));
        }

        return null;
    }

    private void AddReference<T>(string assetName, Func<T> assetLoader) where T : Asset
    {
        // A file that won't load falls back to the one it replaced, so a mod's broken picture
        // doesn't leave the game without one
        Func<T>? fallback = _assets.GetValueOrDefault(GetKey(assetName, typeof(T).Name)) switch
        {
            AssetReference<T> reference => reference.Load,
            T loaded => () => loaded,
            _ => null,
        };
        if (fallback != null)
        {
            var load = assetLoader;
            var where = _currentEntry == null ? assetName : $"{_currentEntry.Source.Name}: {_currentEntry.FullName}";
            assetLoader = () =>
            {
                try
                {
                    return load();
                }
                catch (Exception e)
                {
                    Warn($"{where} can't be loaded, so the one it replaces is used: {e.Message}");
                    return fallback();
                }
            };
        }

        AddAsset(assetName, new AssetReference<T>(assetLoader));
    }

    private void AddAsset(string assetName, Asset asset, bool overwrite = true, string? action = null)
    {
        var key = GetKey(assetName, GetAssetTypeName(asset));

        if (!_assets.TryAdd(key, asset))
        {
            if (!overwrite)
                return;
            _assets[key] = asset;
            RecordOrigin(key, action ?? "replaced");
            return;
        }

        RecordOrigin(key, action ?? "added");
    }

    private void MergeAsset(string assetName, Asset asset, bool overwrite = true)
    {
        var key = GetKey(assetName, GetAssetTypeName(asset));

        if (_assets.TryGetValue(key, out var existingAsset))
        {
            existingAsset.Merge(asset);
            RecordOrigin(key, "merged");
            return;
        }

        AddAsset(assetName, asset);
    }

    private void RecordOrigin(string key, string action)
    {
        if (_currentEntry == null)
            return;

        var origin = new AssetOrigin(_currentEntry.Source.Name, _currentEntry.FullName, action);
        if (!_origins.TryGetValue(key, out var origins))
            _origins[key] = origins = [];
        origins.Add(origin);
    }

    /// <summary>
    /// A GAMEMAPS/MAPHEAD pair to use in place of the game's own: the last source with both
    /// directly in maps/, named as the running release names them (maps/maphead.wl6 and
    /// maps/gamemaps.wl6). A source with only one of them is warned about and skipped.
    /// </summary>
    public (AssetSourceEntry Header, AssetSourceEntry Data)? FindMapFilePair(string headerName, string dataName)
    {
        (AssetSourceEntry Header, AssetSourceEntry Data)? pair = null;
        foreach (var files in _mapDataFiles.GroupBy(entry => entry.Source))
        {
            var header = files.LastOrDefault(entry => entry.Name.Equals(headerName, StringComparison.OrdinalIgnoreCase));
            var data = files.LastOrDefault(entry => entry.Name.Equals(dataName, StringComparison.OrdinalIgnoreCase));
            if (header != null && data != null)
                pair = (header, data);
            else if (header != null || data != null)
                Warn($"{files.Key.Name}: maps/{header?.Name ?? data!.Name} is left out: it needs maps/{(header == null ? headerName : dataName)} beside it");
        }

        return pair;
    }

    /// <summary>Each asset's history, by the same keys as GetAssets</summary>
    public Dictionary<string, List<AssetOrigin>> GetAssetOrigins() => _origins;

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
                // Left out, so the game's own data file (a GAMEMAPS level, say) can fill in for it
                var reason = (e as System.Reflection.TargetInvocationException)?.InnerException?.Message ?? e.Message;
                var where = asset.Key;
                if (_origins.TryGetValue(asset.Key, out var origins) && origins.Count > 0)
                {
                    where = $"{origins[^1].Source}: {origins[^1].Path}";
                    origins[^1] = origins[^1] with { Action = AssetOrigin.LeftOut };
                }
                Warn($"{where} can't be loaded, so it's left out: {reason}");
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
