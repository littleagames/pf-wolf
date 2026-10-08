using PFWolf.Assets;
using PFWolf.Exceptions;
using PFWolf.Loaders;

namespace PFWolf.Managers;

/// <summary>
/// Every asset the running game pack sees: pfwolf.pk3's, the mods' over them, then those read
/// from the game's data files (VSWAP, VGAGRAPH, GAMEMAPS...) beneath both
/// </summary>
public class AssetManager
{
    Dictionary<string, Asset> _assets = new Dictionary<string, Asset>();
    private string _gamePackId = "";
    private string _gameReleaseId = "";
    private bool strict = false;

    /// <summary>
    /// Sets whether you must specify if an asset exists, and will hard fail, or just quietly continue
    /// </summary>
    /// <param name="strict">If true, the application will prevent further process if the asset does not exist</param>
    public void SetStrictAssets(bool strict)
    {
        this.strict = strict;
    }

    public const string BasePk3FileName = "pfwolf.pk3";

    // Each asset's history, by key: which sources added, replaced or merged into it
    private Dictionary<string, List<AssetOrigin>> _origins = [];

    /// <summary>The mods loaded over pfwolf.pk3, in load order</summary>
    public IReadOnlyList<ModSource> LoadedMods { get; private set; } = [];

    /// <summary>What went wrong finding or loading the mods</summary>
    public List<string> ModWarnings { get; } = [];

    /// <param name="gamePackId">The running game pack ("wolf3d", "spear"), whose gamepacks/ assets override the shared ones</param>
    /// <param name="gameReleaseId">The running release's key in gamepacks/gamepack-info.yaml ("wolf3d-apogee")</param>
    /// <param name="modPaths">Mods to load over pfwolf.pk3, in load order: paths, or names in the mods folder</param>
    public void Load(string gamePackId, string gameReleaseId, IEnumerable<string> modPaths)
    {
        _gamePackId = gamePackId;
        _gameReleaseId = gameReleaseId;
        var basePk3 = new Pk3AssetSource(BasePk3FileName);
        BasePackIds = PfWolfPk3Loader.ReadBasePackIds(basePk3, gameReleaseId);
        LoadedMods = OpenMods(modPaths, gamePackId);

        Dictionary<string, Asset> assets = new();
        var pfWolfBasePk3Loader = new PfWolfPk3Loader([basePk3], gamePackId, gameReleaseId,
            LoadedMods.Select(mod => mod.Source).ToList());
        assets = pfWolfBasePk3Loader.GetAssets();
        _origins = pfWolfBasePk3Loader.GetAssetOrigins();

        foreach (var kvp in assets)
        {
            if (!_assets.ContainsKey(kvp.Key))
                _assets[kvp.Key] = kvp.Value;
        }

        var rawDataMap = FindInGamePack<RawDataMapAsset>("raw-data-map");

        // The release's data files, named by its file-pack in gamepack-info
        var gamePackInfo = Find<GamePackInfoAsset>("gamepack-info")
            ?? throw new KeyNotFoundException("gamepacks/gamepack-info.yaml is missing from pfwolf.pk3");
        string DataFile(string loaderName, Func<FileLoaderDetails, FileReference?> selectFile)
            => gamePackInfo.GetDataFile(gameReleaseId, loaderName, selectFile);
        CheckDataFiles(gamePackInfo, gameReleaseId);

        var audioLoader = new Wolf3dAudioFileLoader(
            DataFile("Wolf3DAudioFileLoader", d => d.Data),
            DataFile("Wolf3DAudioFileLoader", d => d.Header));
        AddDataFileAssets(audioLoader.GetAssets(rawDataMap?.Audio ?? [], rawDataMap?.Music ?? []),
            DataFile("Wolf3DAudioFileLoader", d => d.Data));

        LoadLevels(pfWolfBasePk3Loader, rawDataMap?.Maps ?? [],
            DataFile("Wolf3DMapFileLoader", d => d.Header),
            DataFile("Wolf3DMapFileLoader", d => d.Data));

        // numFonts isn't stored in the VGAGRAPH file itself, so the file-pack gives it (font-count)
        var vgaGraphicLoader = new Wolf3dVgaFileLoader(
            DataFile("Wolf3DVgaFileLoader", d => d.Header),
            DataFile("Wolf3DVgaFileLoader", d => d.Data),
            DataFile("Wolf3DVgaFileLoader", d => d.Dict),
            numFonts: gamePackInfo.GetFileLoader(gameReleaseId, "Wolf3DVgaFileLoader").FontCount ?? 2);
        AddDataFileAssets(vgaGraphicLoader.GetAssets(rawDataMap?.Graphics ?? []), DataFile("Wolf3DVgaFileLoader", d => d.Data));

        var vswapLoader = new Wolf3dVswapFileLoader(DataFile("Wolf3DVswapFileLoader", d => d.Data));
        AddDataFileAssets(vswapLoader.GetAssets(rawDataMap?.Walls ?? [], rawDataMap?.Sprites ?? [], rawDataMap?.DigitizedAudio ?? []),
            DataFile("Wolf3DVswapFileLoader", d => d.Data));

        LoadMovies(gamePackInfo.FindFileLoader(gameReleaseId, "JamMovieFileLoader")?.Movies);

        ModWarnings.AddRange(pfWolfBasePk3Loader.Warnings);
    }

    /// <summary>
    /// Stops with the names of the release's data files that aren't there (its movies apart,
    /// which it can go without; the levels' pair isn't needed when the pk3s have the levels,
    /// which LoadLevels sorts out), or, for a strict-md5 release, that are another version's
    /// </summary>
    private static void CheckDataFiles(GamePackInfoAsset gamePackInfo, string gameReleaseId)
    {
        var gamePack = gamePackInfo.GetGamePack(gameReleaseId);
        var release = gamePack.FilePack?.Description ?? gamePack.Title ?? gameReleaseId;

        var missing = gamePackInfo.FindMissingDataFiles(gameReleaseId);
        if (missing.Count > 0)
            throw new DataFilesException($"{release}: these data files aren't in the game folder: {string.Join(", ", missing)}");

        if (gamePack.FilePack?.StrictMd5 == true)
        {
            var mismatched = gamePackInfo.FindMismatchedDataFiles(gameReleaseId);
            if (mismatched.Count > 0)
                throw new DataFilesException(
                    $"These data files aren't {release}'s, the only version that can be played: {string.Join(", ", mismatched)}");
        }
    }

    /// <summary>
    /// The release's JAM movies (Blake Stone's), by the names its file-pack gives them. One
    /// that's missing or can't be read is left out, and the game goes on without it.
    /// </summary>
    private void LoadMovies(Dictionary<string, FileReference>? movies)
    {
        foreach (var (name, file) in movies ?? [])
        {
            if (string.IsNullOrWhiteSpace(file.File) || !File.Exists(file.File))
            {
                WarningLog.Write($"Movie {name}: {file.File} isn't there, so it won't play");
                continue;
            }

            try
            {
                AddDataFileAssets(new Dictionary<string, Asset> { [name] = new JamMovieAsset(File.ReadAllBytes(file.File)) }, file.File);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                WarningLog.Write($"Movie {name}: {file.File} can't be read, so it won't play: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Adds the assets read from one of the game's data files (VSWAP.WL6), apart from those the
    /// pk3s already have: theirs replace the data file's
    /// </summary>
    private void AddDataFileAssets(Dictionary<string, Asset> assets, string dataFile)
    {
        foreach (var kvp in assets)
            AddBeneathPk3s(GetKey(kvp.Key, kvp.Value.GetType().Name), kvp.Value, [new AssetOrigin(dataFile, kvp.Key, "added")]);
    }

    /// <summary>
    /// Adds an asset read from data files, with how it came to be, unless a pk3 already has it
    /// </summary>
    private void AddBeneathPk3s(string key, Asset asset, List<AssetOrigin> history)
    {
        if (!_origins.TryGetValue(key, out var origins))
            _origins[key] = origins = [];

        // The data files come first in the history, since what the pk3s did was done over them
        if (origins.Count > 0 && origins[0].Action == "added")
            origins[0] = origins[0] with { Action = "replaced" };
        origins.InsertRange(0, history);

        if (!_assets.ContainsKey(key))
            _assets[key] = asset;
    }

    /// <summary>
    /// The levels in GAMEMAPS/MAPHEAD pairs, beneath the pk3s' maps/*.wad levels: the game's
    /// own pair, then level by level a pk3's pair under maps/ (named like the game's own). The
    /// game's own pair isn't needed when a pk3 supplies the levels game-info plays.
    /// </summary>
    private void LoadLevels(PfWolfPk3Loader pk3Loader, List<string> levelNames, string headerFile, string dataFile)
    {
        var levels = new Dictionary<string, (Asset Map, List<AssetOrigin> History)>();
        void AddPair(Wolf3dMapFileLoader loader, string source, Func<string, string> path, Action<string> warn)
        {
            foreach (var (name, map) in loader.GetAssets(levelNames, warn))
            {
                if (levels.TryGetValue(name, out var level))
                    levels[name] = (map, [.. level.History, new AssetOrigin(source, path(name), "replaced")]);
                else
                    levels[name] = (map, [new AssetOrigin(source, path(name), "added")]);
            }
        }

        var ownPair = File.Exists(headerFile) && File.Exists(dataFile);
        if (ownPair)
            AddPair(Wolf3dMapFileLoader.FromFiles(headerFile, dataFile), dataFile.ToLowerInvariant(), name => name,
                warning => ModWarnings.Add($"{dataFile}: {warning}"));

        var pk3Pair = pk3Loader.FindMapFilePair(headerFile, dataFile);
        if (pk3Pair is { } pair)
        {
            var where = $"{pair.Data.Source.Name}: {pair.Data.FullName}";
            try
            {
                AddPair(new Wolf3dMapFileLoader(pair.Header.Open().ToArray(), pair.Data.Open().ToArray()), pair.Data.Source.Name,
                    name => $"{pair.Data.FullName} ({name})", warning => ModWarnings.Add($"{where}: {warning}"));
            }
            catch (InvalidDataException e)
            {
                ModWarnings.Add($"{where} can't be read, so it's left out: {e.Message}");
            }
        }

        foreach (var (name, level) in levels)
            AddBeneathPk3s(GetKey(name, nameof(MapAsset)), level.Map, level.History);

        // Levels game-info sends the player to that nothing supplied
        var gameInfo = FindInGamePack<GameInfoAsset>("game-info");
        if (gameInfo == null)
            return;

        var missing = gameInfo.Maps.Keys
            .Concat(gameInfo.Maps.Values.SelectMany(map => new[] { map.Next, map.SecretNext }))
            .Concat(gameInfo.Episodes.Values.Select(episode => episode.StartMap))
            .Where(name => !string.IsNullOrWhiteSpace(name) && !Exists<MapAsset>(name!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missing.Count == 0)
            return;

        var list = string.Join(", ", missing.Take(8)) + (missing.Count > 8 ? $" and {missing.Count - 8} more" : "");
        if (!ownPair && pk3Pair == null)
            throw new PfWolfMapException("Cannot open file: {0}. File does not exist, and no pk3 has these levels: " + list,
                File.Exists(headerFile) ? dataFile : headerFile);

        ModWarnings.Add($"These levels are in game-info but nothing supplies them: {list}");
    }

    /// <summary>
    /// Finds and opens the mods to load, leaving out any that are missing, repeated, can't be
    /// read or aren't for this game pack (each with a warning)
    /// </summary>
    private List<ModSource> OpenMods(IEnumerable<string> modPaths, string gamePackId)
    {
        var mods = new List<ModSource>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(BasePk3FileName) };
        foreach (var modPath in modPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            var fullPath = ModSource.ResolvePath(modPath);
            if (fullPath == null)
            {
                ModWarnings.Add($"Mod '{modPath}' isn't there, or in {ModSource.ModsFolder}, so it isn't loaded");
                continue;
            }
            if (!seen.Add(fullPath))
                continue;

            var mod = ModSource.TryOpen(fullPath, ModWarnings);
            if (mod == null)
                continue;
            if (!mod.IsForGamePack(gamePackId, BasePackIds))
            {
                ModWarnings.Add($"Mod '{mod.DisplayName}' is for {string.Join(", ", mod.Info.GamePacks!)}, not {gamePackId}, so it isn't loaded");
                continue;
            }

            mods.Add(mod);
        }

        foreach (var warning in ModWarnings)
            WarningLog.Write(warning);
        return mods;
    }

    /// <summary>
    /// Every asset with this name, of any type, with where it came from: e.g. a wall's
    /// picture from VSWAP.WL6, replaced by a mod's PNG
    /// </summary>
    public IEnumerable<(string Type, string Name, IReadOnlyList<AssetOrigin> Origins)> FindAssetOrigins(string assetName)
    {
        var name = assetName.ToLowerInvariant();
        foreach (var key in _assets.Keys.Where(key => key.EndsWith(":" + name)).OrderBy(key => key, StringComparer.Ordinal))
        {
            var type = _assets[key].GetType().Name;
            yield return (type, name, _origins.TryGetValue(key, out var origins) ? origins : []);
        }
    }

    /// <summary>Names of every loaded asset, for completing assetinfo</summary>
    public IEnumerable<string> AssetNames
        => _assets.Keys.Select(key => key.Substring(key.IndexOf(':') + 1)).Distinct();

    /// <summary>How many differently named assets there are of any of these types</summary>
    public int CountNames(params Type[] assetTypes)
    {
        var prefixes = assetTypes.Select(type => $"{type.Name}:".ToLowerInvariant()).ToList();
        return _assets.Keys
            .Where(key => prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
            .Select(key => key.Substring(key.IndexOf(':') + 1))
            .Distinct()
            .Count();
    }

    public bool Exists<T>(string assetName) where T : Asset
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return false;

        return _assets.ContainsKey(GetKey(assetName, typeof(T).Name));
    }

    public T? Find<T>(string assetName) where T : Asset
    {
        string assetType = typeof(T).Name;

        if (string.IsNullOrWhiteSpace(assetName))
        {
            if (strict)
            {
                throw new ArgumentException($"Asset name cannot be empty. Asset Type: {assetType}", nameof(assetName));
            }

            WarningLog.Write($"Asset name cannot be empty. Asset Type: {assetType}");
            return null;
        }

        var key = GetKey(assetName, assetType);

        if (_assets.TryGetValue(key, out var foundAsset))
            return foundAsset as T;

        WarningLog.Write($"Asset not found: {assetName} (Type: {assetType})");
        return null;
    }

    /// <summary>The running game pack ("wolf3d", "spear")</summary>
    public string GamePackId => _gamePackId;

    /// <summary>The packs the running one is built on, nearest first ([spear, wolf3d] for Spear's demo)</summary>
    public IReadOnlyList<string> BasePackIds { get; private set; } = [];

    /// <summary>
    /// Finds an asset belonging to the running game pack, e.g. "alias" -> "wolf3d/alias"
    /// </summary>
    public T? FindInGamePack<T>(string assetName) where T : Asset
        => Find<T>($"{_gamePackId}/{assetName}");

    /// <summary>
    /// Palette asset name the running release uses (its game-palette in gamepack-info)
    /// </summary>
    public string GetGamePaletteName()
    {
        var gamePackInfo = Find<GamePackInfoAsset>("gamepack-info")
            ?? throw new KeyNotFoundException("gamepacks/gamepack-info.yaml is missing from pfwolf.pk3");
        return gamePackInfo.GetGamePalette(_gameReleaseId);
    }

    /// <summary>
    /// The running release's title in gamepack-info ("Spear of Destiny"), or null when it has none
    /// </summary>
    public string? GetGameTitle()
    {
        var gamePackInfo = Find<GamePackInfoAsset>("gamepack-info");
        return gamePackInfo != null && gamePackInfo.GamePacks.TryGetValue(_gameReleaseId, out var gamePack)
            ? gamePack.Title
            : null;
    }

    /// <summary>
    /// The running release's data files as gamepack-info describes them ("Wolfenstein 3D v1.4
    /// Apogee"), else its title, or null when it has neither
    /// </summary>
    public string? GetGameDescription()
    {
        var gamePackInfo = Find<GamePackInfoAsset>("gamepack-info");
        return gamePackInfo != null && gamePackInfo.GamePacks.TryGetValue(_gameReleaseId, out var gamePack)
            ? (string.IsNullOrWhiteSpace(gamePack.FilePack?.Description) ? gamePack.Title : gamePack.FilePack.Description)
            : null;
    }

    /// <summary>
    /// Names of every menu defined in menudefs/, without the packs' own versions (menudefs/{pack}/)
    /// </summary>
    public IEnumerable<string> GetMenuNames()
    {
        var prefix = $"{nameof(MenuAsset)}:".ToLowerInvariant();
        return _assets.Keys
            .Where(key => key.StartsWith(prefix))
            .Select(key => key.Substring(prefix.Length))
            .Where(name => !name.Contains('/'))
            .ToList();
    }

    private static string GetKey(string assetName, string assetType)
        => $"{assetType}:{assetName}".ToLowerInvariant();
}
