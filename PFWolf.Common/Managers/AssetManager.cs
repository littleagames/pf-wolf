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
    /// <param name="progress">Told each step as it starts ("Reading the levels"), for a loading screen; on this thread</param>
    public void Load(string gamePackId, string gameReleaseId, IEnumerable<string> modPaths, Action<string>? progress = null)
    {
        _gamePackId = gamePackId;
        _gameReleaseId = gameReleaseId;
        progress?.Invoke("Opening the mods");
        var basePk3 = new Pk3AssetSource(BasePk3FileName);
        LoadedMods = OpenMods(modPaths, gamePackId, mods =>
        {
            // The release can be a game a mod adds, so its base packs are known once the mods are open
            var info = PfWolfPk3Loader.ReadGamePackInfo(basePk3)
                ?.WithModGames(mods.Where(mod => mod.HasGames).Select(mod => (mod.DisplayName, mod.Games!)));
            BasePackIds = info?.GamePacks.ContainsKey(gameReleaseId) == true ? info.GetBasePackChain(gameReleaseId) : [];
            MenuPackIds = info?.GamePacks.ContainsKey(gameReleaseId) == true ? info.GetMenuPackChain(gameReleaseId) : [];
        });

        progress?.Invoke(LoadedMods.Count > 0 ? "Reading pfwolf.pk3 and the mods" : "Reading pfwolf.pk3");
        Dictionary<string, Asset> assets = new();
        var pfWolfBasePk3Loader = new PfWolfPk3Loader([basePk3], gamePackId, gameReleaseId,
            LoadedMods.Select(mod => mod.Source).ToList());
        assets = pfWolfBasePk3Loader.GetAssets();
        _origins = pfWolfBasePk3Loader.GetAssetOrigins();

        foreach (var kvp in assets)
        {
            if (!_assets.ContainsKey(kvp.Key))
                SetAsset(kvp.Key, kvp.Value);
        }

        var rawDataMap = FindInGamePackIfAny<RawDataMapAsset>("raw-data-map");

        // The release's data files, named by its file-pack in gamepack-info
        var gamePackInfo = Find<GamePackInfoAsset>("gamepack-info")
            ?? throw new KeyNotFoundException("gamepacks/gamepack-info.yaml is missing from pfwolf.pk3");
        string DataFile(string loaderName, Func<FileLoaderDetails, FileReference?> selectFile)
            => gamePackInfo.GetDataFile(gameReleaseId, loaderName, selectFile);

        // A standalone game has no data files: everything it has is in the pk3s
        if (!gamePackInfo.HasDataFiles(gameReleaseId))
        {
            progress?.Invoke("Reading the levels");
            LoadLevels(pfWolfBasePk3Loader, rawDataMap?.Maps ?? [], headerFile: null, dataFile: null);
            ModWarnings.AddRange(pfWolfBasePk3Loader.Warnings);
            BuildCroppedGraphics();
            BuildAnimations();
            return;
        }

        CheckDataFiles(gamePackInfo, gameReleaseId);

        progress?.Invoke("Reading the sounds and music");

        var audioLoader = new Wolf3dAudioFileLoader(
            DataFile("Wolf3DAudioFileLoader", d => d.Data),
            DataFile("Wolf3DAudioFileLoader", d => d.Header));
        AddDataFileAssets(audioLoader.GetAssets(rawDataMap?.Audio ?? [], rawDataMap?.Music ?? [],
                DataFileWarning(DataFile("Wolf3DAudioFileLoader", d => d.Data))),
            DataFile("Wolf3DAudioFileLoader", d => d.Data));

        progress?.Invoke("Reading the levels");
        LoadLevels(pfWolfBasePk3Loader, rawDataMap?.Maps ?? [],
            DataFile("Wolf3DMapFileLoader", d => d.Header),
            DataFile("Wolf3DMapFileLoader", d => d.Data));

        progress?.Invoke("Reading the pictures");
        // numFonts isn't stored in the VGAGRAPH file itself, so the file-pack gives it (font-count)
        var vgaGraphicLoader = new Wolf3dVgaFileLoader(
            DataFile("Wolf3DVgaFileLoader", d => d.Header),
            DataFile("Wolf3DVgaFileLoader", d => d.Data),
            DataFile("Wolf3DVgaFileLoader", d => d.Dict),
            numFonts: gamePackInfo.GetFileLoader(gameReleaseId, "Wolf3DVgaFileLoader").FontCount ?? 2);
        AddDataFileAssets(vgaGraphicLoader.GetAssets(rawDataMap?.Graphics ?? [], DataFileWarning(DataFile("Wolf3DVgaFileLoader", d => d.Data))),
            DataFile("Wolf3DVgaFileLoader", d => d.Data));

        progress?.Invoke("Reading the walls and sprites");
        var vswapLoader = new Wolf3dVswapFileLoader(DataFile("Wolf3DVswapFileLoader", d => d.Data));
        AddDataFileAssets(vswapLoader.GetAssets(rawDataMap?.Walls ?? [], rawDataMap?.Sprites ?? [], rawDataMap?.DigitizedAudio ?? [],
                DataFileWarning(DataFile("Wolf3DVswapFileLoader", d => d.Data))),
            DataFile("Wolf3DVswapFileLoader", d => d.Data));

        LoadMovies(gamePackInfo.FindFileLoader(gameReleaseId, "JamMovieFileLoader")?.Movies);

        ModWarnings.AddRange(pfWolfBasePk3Loader.Warnings);
        BuildCroppedGraphics();
        BuildAnimations();
    }

    /// <summary>For the tests: these assets (by name) in place of loading any, then their crops and animations</summary>
    internal void UseAssets(string gamePackId, IEnumerable<(string Name, Asset Asset)> assets)
    {
        _gamePackId = gamePackId;
        foreach (var (name, asset) in assets)
            SetAsset(GetKey(name, asset.GetType().Name), asset);
        TranslateUwmfLevels();
        BuildCroppedGraphics();
        BuildAnimations();
    }

    /// <summary>What's wrong with graphicdefs' pictures, by name: left out, or cropped past their source</summary>
    public IReadOnlyDictionary<string, string> CropProblems => _cropProblems;
    private readonly Dictionary<string, string> _cropProblems = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Turns graphicdefs' crops into pictures, once every other asset is in (the data files'
    /// sprites and walls are sources too), and before the animations, which can use them as
    /// frames. A crop replaces a picture of its name, which can be its own source. Sources are
    /// looked up before any crop is added, so a source is never another crop.
    /// </summary>
    internal void BuildCroppedGraphics()
    {
        var definitions = new Dictionary<string, GraphicDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "graphicdefs", $"{_gamePackId}/graphicdefs" })
            if (Exists<GraphicDefinitionsAsset>(name))
                foreach (var (graphic, definition) in Find<GraphicDefinitionsAsset>(name)!.Graphics)
                    definitions[graphic] = definition;

        _cropProblems.Clear();
        var built = new List<(string Key, GraphicDefinition Definition, GraphicAsset Graphic)>();
        foreach (var (name, definition) in definitions)
        {
            void Problem(string problem)
            {
                WarningLog.Write($"Picture {name} (graphicdefs): {problem}");
                _cropProblems[name] = _cropProblems.TryGetValue(name, out var earlier) ? $"{earlier}; {problem}" : problem;
            }

            if (CropOf(definition, Problem) is { } graphic)
                built.Add((GetKey(name, nameof(GraphicAsset)), definition, graphic));
        }

        foreach (var (key, definition, graphic) in built)
        {
            SetAsset(key, graphic);
            if (!_origins.TryGetValue(key, out var origins))
                _origins[key] = origins = [];
            origins.Add(new AssetOrigin("graphicdefs", key.Substring(key.IndexOf(':') + 1), $"cropped from {definition.Source}"));
        }
    }

    // A graphicdefs crop as a picture, or null (with the problem) when it can't be made
    private GraphicAsset? CropOf(GraphicDefinition definition, Action<string> problem)
    {
        if (string.IsNullOrWhiteSpace(definition.Source))
        {
            problem("it has no source, so it's left out");
            return null;
        }
        if (definition.Width <= 0 || definition.Height <= 0)
        {
            problem($"its width and height have to be more than 0 (it's {definition.Width}x{definition.Height}), so it's left out");
            return null;
        }

        var kind = definition.Kind?.Trim().ToLowerInvariant();
        if (kind is not (null or "" or GraphicDefinition.SpriteKind or GraphicDefinition.TextureKind or GraphicDefinition.GraphicKind))
        {
            problem($"kind '{definition.Kind}' isn't sprite, texture or graphic, so it's left out");
            return null;
        }

        // The source, as the kind given, else the first kind it is: sprite, texture, graphic
        var name = definition.Source.Trim();
        bool any = string.IsNullOrEmpty(kind);
        CropSource? source =
            (any || kind == GraphicDefinition.SpriteKind) && _assets.GetValueOrDefault(GetKey(name, nameof(SpriteAsset))) is SpriteAsset sprite
                ? CropSource.From(sprite)
            : (any || kind == GraphicDefinition.TextureKind) && _assets.GetValueOrDefault(GetKey(name, nameof(TextureAsset))) is TextureAsset texture
                ? CropSource.From(texture)
            : (any || kind == GraphicDefinition.GraphicKind) && _assets.GetValueOrDefault(GetKey(name, nameof(GraphicAsset))) is GraphicAsset graphic
                ? CropSource.From(graphic)
            : null;
        if (source is not { } from)
        {
            problem(any
                ? $"there's no sprite, texture or picture named {name}, so it's left out"
                : $"there's no {kind} named {name}, so it's left out");
            return null;
        }

        int x = definition.X, y = definition.Y, width = definition.Width, height = definition.Height;
        if (x >= from.Width || y >= from.Height || x + width <= 0 || y + height <= 0)
        {
            problem($"{x},{y} {width}x{height} is outside {name} ({from.Width}x{from.Height}), so it's left out");
            return null;
        }
        if (x < 0 || y < 0 || x + width > from.Width || y + height > from.Height)
            problem($"{x},{y} {width}x{height} runs past the edge of {name} ({from.Width}x{from.Height}); what's past it is see-through");

        byte? background = null;
        if (!string.IsNullOrWhiteSpace(definition.Background))
        {
            background = PaletteIndexOf(definition.Background.Trim());
            if (background == null)
                problem($"background '{definition.Background}' isn't a color name, #RRGGBB or palette index, so see-through pixels stay see-through");
        }

        return from.Crop(x, y, width, height, background);
    }

    /// <summary>
    /// A color as a palette index: a palette index as it is, else a #RRGGBB or colors.yaml name
    /// as the closest of the game's palette; null when it's none of those, or there's no palette
    /// </summary>
    private byte? PaletteIndexOf(string color)
    {
        if (byte.TryParse(color, out var index))
            return index;

        Color? rgb = null;
        if (color.StartsWith('#'))
        {
            try { rgb = Color.FromHexRGBA(color); }
            catch (Exception e) when (e is ArgumentException or FormatException) { return null; }
        }
        else if (FindInGamePackIfAny<ColorThemeAsset>("colors")?.Colors is { } colors)
            rgb = colors.FirstOrDefault(named => named.Key.Equals(color, StringComparison.OrdinalIgnoreCase)).Value;

        return rgb is { } found && GamePalette() is { Colors.Length: > 0 } palette
            ? palette.ClosestIndex(found.Red, found.Green, found.Blue)
            : null;
    }

    // The running release's palette, or null when it isn't known (the tests' own assets)
    private Palette? GamePalette()
    {
        if (!Exists<GamePackInfoAsset>("gamepack-info"))
            return null;
        var name = Find<GamePackInfoAsset>("gamepack-info")!.GetGamePalette(_gameReleaseId);
        return Exists<Palette>(name) ? Find<Palette>(name) : null;
    }


    /// <summary>
    /// Turns the UWMF levels read into planes, through the running game's mapdefs (which are
    /// only known once every pk3 is in); one that can't be is left out, with a warning, as are
    /// the parts of one the mapdefs can't make anything of
    /// </summary>
    private void TranslateUwmfLevels()
    {
        var mapdefs = FindInGamePackIfAny<MapObjectTranslationAsset>("mapdefs") ?? new MapObjectTranslationAsset();
        foreach (var (key, asset) in _assets.ToList())
        {
            if (asset is not MapAsset { NeedsTranslation: true } level)
                continue;

            var name = key[(key.IndexOf(':') + 1)..].ToUpperInvariant();
            try
            {
                var translated = Loaders.Uwmf.UwmfTranslator.ToMapAsset(level.Uwmf!, mapdefs, warning => ModWarnings.Add($"{name}: {warning}"));
                level.MapData = translated.MapData;
                level.LocalDefs = translated.LocalDefs;
                level.ExtraThings = translated.ExtraThings;
                level.Defaults = translated.Defaults;
            }
            catch (InvalidDataException e)
            {
                ModWarnings.Add($"{name} (UWMF) can't be played, so it's left out: {e.Message}");
                _assets.Remove(key);
            }
        }
    }

    /// <summary>The kinds of asset an animation's frames can be</summary>
    private static readonly Type[] AnimatableTypes = [typeof(TextureAsset), typeof(GraphicAsset)];

    /// <summary>The animations, by name (animdefs, the shared ones and the running pack's)</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<IAnimatedAsset>> Animations => _animations;
    private readonly Dictionary<string, IReadOnlyList<IAnimatedAsset>> _animations = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What's wrong with animdefs' animations, by name: left out, or with frames left out</summary>
    public IReadOnlyDictionary<string, string> AnimationProblems => _animationProblems;
    private readonly Dictionary<string, string> _animationProblems = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Turns animdefs' animations into assets, once every other asset is in (the data files'
    /// walls and pictures are frames too). An animation is added as each kind of asset (texture,
    /// picture) that has all its frames, under its own name, so it's found like one; it replaces
    /// an asset of that name, which can be one of its own frames. Frames are looked up before any
    /// animation is added, so a frame is never another animation.
    /// </summary>
    internal void BuildAnimations()
    {
        var definitions = new Dictionary<string, AnimationDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "animdefs", $"{_gamePackId}/animdefs" })
            if (Exists<AnimationDefinitionsAsset>(name))
                foreach (var (animation, definition) in Find<AnimationDefinitionsAsset>(name)!.Animations)
                    definitions[animation] = definition;

        _animations.Clear();
        _animationProblems.Clear();
        var built = new List<(string Key, Asset Animation)>();
        foreach (var (name, definition) in definitions)
        {
            void Problem(string problem)
            {
                WarningLog.Write($"Animation {name}: {problem}");
                _animationProblems[name] = _animationProblems.TryGetValue(name, out var earlier) ? $"{earlier}; {problem}" : problem;
            }

            var (frames, loops) = definition.Read(Problem);
            if (frames.Count == 0)
            {
                Problem("it has no frames, so it's left out");
                continue;
            }

            var kinds = new List<IAnimatedAsset>();
            foreach (var type in AnimatableTypes)
            {
                var assets = frames.Select(frame => _assets.GetValueOrDefault(GetKey(frame.Name, type.Name))).ToList();
                if (assets.Any(asset => asset == null))
                    continue;

                var animation = (IAnimatedAsset)Activator.CreateInstance(typeof(AnimatedAsset<>).MakeGenericType(type),
                    MakeFrames(type, frames, assets!), loops)!;
                kinds.Add(animation);
                built.Add((GetKey(name, type.Name), (Asset)animation));
            }

            if (kinds.Count == 0)
            {
                var missing = frames.Select(frame => frame.Name)
                    .Where(frame => !AnimatableTypes.Any(type => _assets.ContainsKey(GetKey(frame, type.Name))))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                Problem(missing.Count > 0
                    ? $"these frames aren't textures or pictures, so it's left out: {string.Join(", ", missing)}"
                    : "its frames have to all be textures or all be pictures, so it's left out");
                continue;
            }
            _animations[name] = kinds;
        }

        foreach (var (key, animation) in built)
        {
            SetAsset(key, animation);
            if (!_origins.TryGetValue(key, out var origins))
                _origins[key] = origins = [];
            origins.Add(new AssetOrigin("animdefs", key.Substring(key.IndexOf(':') + 1), "animated"));
        }
    }

    // A typed frame list for AnimatedAsset<type>, from the frames' assets
    private static object MakeFrames(Type type, List<(string Name, int Tics)> frames, List<Asset> assets)
    {
        var frameType = typeof(AnimationFrame<>).MakeGenericType(type);
        var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(frameType))!;
        for (int i = 0; i < frames.Count; i++)
            list.Add(Activator.CreateInstance(frameType, frames[i].Name, assets[i], frames[i].Tics));
        return list;
    }

    /// <summary>
    /// The clock (in tics, 70 a second) that <see cref="Find{T}(string)"/> plays animations by;
    /// none (the editor) shows each animation's first frame
    /// </summary>
    public Func<int>? AnimationClock { get; set; }

    /// <summary>
    /// Stops with the names of the release's data files that aren't there (its movies apart,
    /// which it can go without; the levels' pair isn't needed when the pk3s have the levels,
    /// which LoadLevels sorts out), or, for a strict-md5 release, that are another version's
    /// </summary>
    private static void CheckDataFiles(GamePackInfoAsset gamePackInfo, string gameReleaseId)
    {
        var gamePack = gamePackInfo.GetGamePack(gameReleaseId);
        var filePack = gamePackInfo.GetFilePack(gameReleaseId);
        var release = filePack?.Description ?? gamePack.Title ?? gameReleaseId;

        var missing = gamePackInfo.FindMissingDataFiles(gameReleaseId);
        if (missing.Count > 0)
            throw new DataFilesException($"{release}: these data files aren't in the game folder: {string.Join(", ", missing)}");

        if (filePack?.StrictMd5 == true)
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
    /// Where a data file's loader says it doesn't match raw-data-map (a WDC-edited VSWAP with walls added)
    /// </summary>
    private static Action<string> DataFileWarning(string dataFile) => warning => WarningLog.Write($"{dataFile}: {warning}");

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
            SetAsset(key, asset);
    }

    /// <summary>
    /// The levels in GAMEMAPS/MAPHEAD pairs, beneath the pk3s' maps/*.wad levels: the game's
    /// own pair, then level by level a pk3's pair under maps/ (named like the game's own). The
    /// game's own pair isn't needed when a pk3 supplies the levels game-info plays. A standalone
    /// game has no pair (null file names): only the pk3s' maps/*.wad.
    /// </summary>
    private void LoadLevels(PfWolfPk3Loader pk3Loader, List<string> levelNames, string? headerFile, string? dataFile)
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
            AddPair(Wolf3dMapFileLoader.FromFiles(headerFile!, dataFile!), dataFile!.ToLowerInvariant(), name => name,
                warning => ModWarnings.Add($"{dataFile}: {warning}"));

        var pk3Pair = headerFile != null && dataFile != null ? pk3Loader.FindMapFilePair(headerFile, dataFile) : null;
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

        TranslateUwmfLevels();

        // Levels game-info sends the player to that nothing supplied
        var gameInfo = FindInGamePackIfAny<GameInfoAsset>("game-info");
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
        if (headerFile != null && !ownPair && pk3Pair == null)
            throw new PfWolfMapException("Cannot open file: {0}. File does not exist, and no pk3 has these levels: " + list,
                File.Exists(headerFile) ? dataFile : headerFile);

        ModWarnings.Add($"These levels are in game-info but nothing supplies them: {list}");
    }

    /// <summary>
    /// Finds and opens the mods to load, leaving out any that are missing, repeated, can't be
    /// read or aren't for this game pack (each with a warning)
    /// </summary>
    /// <param name="opened">Called with every mod that opened, before they're picked by game pack</param>
    private List<ModSource> OpenMods(IEnumerable<string> modPaths, string gamePackId, Action<List<ModSource>> opened)
    {
        var openMods = new List<ModSource>();
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

            if (ModSource.TryOpen(fullPath, ModWarnings) is { } mod)
                openMods.Add(mod);
        }

        opened(openMods);
        var mods = new List<ModSource>();
        foreach (var mod in openMods)
        {
            if (!mod.IsForGamePack(gamePackId, BasePackIds))
            {
                ModWarnings.Add($"Mod '{mod.DisplayName}' is for {string.Join(", ", mod.ForGamePacks!)}, not {gamePackId}, so it isn't loaded");
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
            var type = _assets[key] is IAnimatedAsset animation ? $"{animation.FrameType.Name} (animated)" : _assets[key].GetType().Name;
            yield return (type, name, _origins.TryGetValue(key, out var origins) ? origins : []);
        }
    }

    /// <summary>Names of every loaded asset, for completing assetinfo</summary>
    public IEnumerable<string> AssetNames
        => _assets.Keys.Select(key => key.Substring(key.IndexOf(':') + 1)).Distinct();

    /// <summary>The names of every loaded asset of this type, as loaded (lowercase)</summary>
    public IEnumerable<string> NamesOf<T>() where T : Asset
    {
        var prefix = $"{typeof(T).Name}:".ToLowerInvariant();
        return _assets.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).Select(key => key[prefix.Length..]);
    }

    /// <summary>The Lua scripts (scripts/*.lua in pfwolf.pk3 and the mods), in the order they run</summary>
    /// <summary>Every asset of this type, under whatever name</summary>
    public IReadOnlyList<T> GetAll<T>() where T : Asset => _assets.Values.OfType<T>().Distinct().ToList();

    public IReadOnlyList<ScriptAsset> GetScripts() => _assets.Values.OfType<ScriptAsset>().OrderBy(script => script.Order).ToList();

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

        // Asked every frame (whether a sprite has views, the mapdefs): remembered as asked for
        if (_exists.TryGetValue((typeof(T), assetName), out var exists))
            return exists;
        return _exists[(typeof(T), assetName)] = _assets.ContainsKey(GetKey(assetName, typeof(T).Name));
    }

    // What Exists answered for a type and name as given; forgotten whenever an asset is added
    private readonly Dictionary<(Type, string), bool> _exists = [];

    /// <summary>
    /// The asset of this name; for an animation, its frame showing now by <see cref="AnimationClock"/>
    /// </summary>
    public T? Find<T>(string assetName) where T : Asset
        => Find<T>(assetName, AnimationClock?.Invoke() ?? 0);

    /// <summary>
    /// The asset of this name; for an animation, its frame showing <paramref name="elapsedTics"/>
    /// after it started (the level's clock, for walls and flats)
    /// </summary>
    public T? Find<T>(string assetName, int elapsedTics) where T : Asset
    {
        var found = FindAsset<T>(assetName);
        return found is IAnimatedAsset animation ? animation.FrameAssetAt(elapsedTics) as T : found as T;
    }

    /// <summary>
    /// The asset of this name; for an animation, its frame at <paramref name="tics"/> on a shared
    /// clock if it loops (so everything showing it is in step), else <paramref name="tics"/> less
    /// <paramref name="startedTics"/>, when the thing showing it changed to it (it plays once from there)
    /// </summary>
    public T? Find<T>(string assetName, int tics, int startedTics) where T : Asset
    {
        var found = FindAsset<T>(assetName);
        return found is IAnimatedAsset animation
            ? animation.FrameAssetAt(animation.Loops ? tics : tics - startedTics) as T
            : found as T;
    }

    /// <summary>
    /// The animation of this name and kind (an AnimatedAsset&lt;T&gt;), or null when it's a plain
    /// asset or there's none, without a warning
    /// </summary>
    public AnimatedAsset<T>? FindAnimation<T>(string assetName) where T : Asset
        => string.IsNullOrWhiteSpace(assetName) ? null : _assets.GetValueOrDefault(GetKey(assetName, typeof(T).Name)) as AnimatedAsset<T>;

    private Asset? FindAsset<T>(string assetName) where T : Asset
    {
        string assetType = typeof(T).Name;

        if (string.IsNullOrWhiteSpace(assetName))
        {
            if (strict)
            {
                throw new ArgumentException($"Asset name cannot be empty. Asset Type: {assetType}", nameof(assetName));
            }

            // Once per type, as below: a wall with no texture for a side is looked up every frame
            if (_reportedMissing.Add("\0" + assetType))
                WarningLog.Write($"Asset name cannot be empty. Asset Type: {assetType}");
            return null;
        }

        // The renderer looks names up many times a frame: remembered as asked for, so the
        // lowercased key isn't built again each time
        if (_found.TryGetValue((typeof(T), assetName), out var cached))
            return cached;

        var key = GetKey(assetName, assetType);

        if (_assets.TryGetValue(key, out var foundAsset))
        {
            _found[(typeof(T), assetName)] = foundAsset;
            return foundAsset;
        }

        // Once per asset: the renderer looks up a missing wall texture every frame
        if (_reportedMissing.Add(key))
            WarningLog.Write($"Asset not found: {assetName} (Type: {assetType})");
        _found[(typeof(T), assetName)] = null;
        return null;
    }

    private readonly HashSet<string> _reportedMissing = [];

    // What FindAsset found (or didn't) for a type and name as given; forgotten whenever an asset is added
    private readonly Dictionary<(Type, string), Asset?> _found = [];

    private void SetAsset(string key, Asset asset)
    {
        _assets[key] = asset;
        _found.Clear();
        _exists.Clear();
    }

    /// <summary>The running game pack ("wolf3d", "spear")</summary>
    public string GamePackId => _gamePackId;

    /// <summary>The packs the running one is built on, nearest first ([spear, wolf3d] for Spear's demo)</summary>
    public IReadOnlyList<string> BasePackIds { get; private set; } = [];

    /// <summary>The packs the running one's menus answer to in menudefs game-packs lists: its base packs, then its menus-like pack</summary>
    public IReadOnlyList<string> MenuPackIds { get; private set; } = [];

    /// <summary>
    /// Finds an asset belonging to the running game pack, e.g. "alias" -> "wolf3d/alias"
    /// </summary>
    public T? FindInGamePack<T>(string assetName) where T : Asset
        => Find<T>(GamePackName(assetName));

    /// <summary>
    /// The running game pack's asset, or null without a warning: for files a game can go
    /// without (a standalone game may have no status bar, raw-data-map or language strings)
    /// </summary>
    public T? FindInGamePackIfAny<T>(string assetName) where T : Asset
        => Exists<T>(GamePackName(assetName)) ? FindInGamePack<T>(assetName) : null;

    // "wolf3d/mapdefs" for "mapdefs", made once: the renderer asks for the mapdefs many times a frame
    private string GamePackName(string assetName)
    {
        if (!_gamePackNames.TryGetValue((_gamePackId, assetName), out var name))
            _gamePackNames[(_gamePackId, assetName)] = name = $"{_gamePackId}/{assetName}";
        return name;
    }

    private readonly Dictionary<(string, string), string> _gamePackNames = [];

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
