using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Loaders;
using PFWolf.Managers;

namespace PFWolf.Editor.Data;

/// <summary>A level the game's game-info plays, or one only the data files have</summary>
public sealed record MapEntry(string Name, string Title)
{
    public override string ToString() => string.IsNullOrWhiteSpace(Title) ? Name : $"{Name}  {Title}";
}

/// <summary>
/// A game's assets as the engine sees them: pfwolf.pk3, the mods over it and the game's data
/// files beneath, merged by PFWolf.Common's AssetManager. Loaded from a PFWolf folder.
/// </summary>
public sealed class GameContent
{
    private readonly Dictionary<string, SpriteAsset?> _thingSprites = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ActorData> _actors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _rotating = new(StringComparer.OrdinalIgnoreCase);

    private GameContent(AssetManager assets, GameSelection game, MapObjectTranslationAsset mapDefs, GameInfoAsset? gameInfo,
        PaletteColor[] palette, IEnumerable<Dictionary<string, ActorData>> actorDefs)
    {
        Assets = assets;
        Game = game;
        MapDefs = mapDefs;
        GameInfo = gameInfo;
        Palette = palette;

        foreach (var actors in actorDefs)
            foreach (var (name, data) in actors)
                _actors[name] = ActorData.Combine(_actors.GetValueOrDefault(name), data);

        Maps = ListMaps();
        MusicNames = assets.AssetNames
            .Where(name => !name.Contains('/') && (assets.Exists<Wolf3dImfAudio>(name) || assets.Exists<MusicFileAsset>(name)))
            .Select(name => name.ToUpperInvariant())
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    public AssetManager Assets { get; }
    /// <summary>The game loaded: one of PFWolf's own, or a game a mod adds (a standalone game)</summary>
    public GameSelection Game { get; }
    public string PackId => Game.PackId;
    public MapObjectTranslationAsset MapDefs { get; }
    public GameInfoAsset? GameInfo { get; }
    /// <summary>The game palette the editor draws in: as loaded, or as the palette browser is editing it</summary>
    public PaletteColor[] Palette { get; private set; }

    /// <summary>Draws in these colors from now on (pictures made before keep the old ones: make a new ArtCache)</summary>
    public void SetPalette(PaletteColor[] colors) => Palette = (PaletteColor[])colors.Clone();
    public IReadOnlyList<MapEntry> Maps { get; }

    /// <summary>The game's title from gamepack-info ("Spear of Destiny"), or its pack id</summary>
    public string Title => Assets.GetGameDescription() ?? PackId;

    /// <summary>
    /// Loads the game whose pfwolf.pk3 and data files are in <paramref name="gameFolder"/>. Like the
    /// game, it reads them from the working folder, which it changes to that one. Warnings go to
    /// WarningLog; a game that can't load throws (DataFilesException for missing data files).
    /// </summary>
    /// <param name="requestedPack">A game pack id ("spear", or a game a mod adds), or empty to pick by
    /// the data files, or by the mods when one adds a game (a standalone game needs no data files)</param>
    public static GameContent Load(string gameFolder, string requestedPack, IEnumerable<string> mods)
    {
        if (!File.Exists(Path.Combine(gameFolder, AssetManager.BasePk3FileName)))
            throw new FileNotFoundException($"There's no {AssetManager.BasePk3FileName} in {gameFolder}: pick the folder PFWolf is in");

        Directory.SetCurrentDirectory(gameFolder);
        var modList = mods.ToList();
        var game = GameTypes.PickGame(requestedPack, modList);

        var assets = new AssetManager();
        assets.Load(game.PackId, game.ReleaseId, modList);
        foreach (var warning in assets.ModWarnings)
            WarningLog.Write(warning);

        var packId = game.PackId;
        var mapDefs = assets.FindInGamePack<MapObjectTranslationAsset>("mapdefs")
            ?? throw new InvalidDataException($"{packId} has no mapdefs");
        var palette = assets.Find<Palette>(assets.GetGamePaletteName())?.Colors ?? [];

        // Files directly in actordefs/ belong to every pack; the pack's own come after, as in the game
        var actorDefs = new[] { "actordefs", $"{packId}/actordefs" }
            .Where(assets.Exists<ActorTranslationAsset>)
            .Select(name => assets.Find<ActorTranslationAsset>(name)!.Actors);
        return new GameContent(assets, game, mapDefs, assets.FindInGamePack<GameInfoAsset>("game-info"), palette, actorDefs);
    }

    /// <summary>
    /// A game made of just these definitions, with no pictures or levels of its own: for
    /// reading levels against mapdefs without a game folder (the tests)
    /// </summary>
    public static GameContent FromDefinitions(MapObjectTranslationAsset mapDefs, GameInfoAsset? gameInfo = null,
        Dictionary<string, ActorData>? actors = null)
        => new(new AssetManager(), GameSelection.Of(GameType.Wolf3D), mapDefs, gameInfo, [], actors != null ? [actors] : []);

    /// <summary>An asset, or null without the "not found" warning AssetManager.Find gives</summary>
    public T? Find<T>(string name) where T : Asset
        => Assets.Exists<T>(name) ? Assets.Find<T>(name) : null;

    public MapAsset? FindMap(string name) => Find<MapAsset>(name);

    /// <summary>
    /// The mod folder a level comes from (its maps/NAME.wad), or null when it's the game's own or
    /// a zipped mod's, which the editor doesn't write into
    /// </summary>
    public string? ModFolderOf(string mapName) => ModFolderOf(mapName, nameof(MapAsset));

    /// <summary>The mod folder an asset of this type comes from, or null when it's the game's own or a zipped mod's</summary>
    public string? ModFolderOf(string assetName, string assetType)
    {
        var origin = Assets.FindAssetOrigins(assetName)
            .Where(asset => asset.Type == assetType)
            .SelectMany(asset => asset.Origins)
            .LastOrDefault(origin => origin.Action != AssetOrigin.LeftOut);
        if (origin == null)
            return null;

        return Assets.LoadedMods
            .LastOrDefault(mod => mod.Source.Name.Equals(origin.Source, StringComparison.OrdinalIgnoreCase) && Directory.Exists(mod.FullPath))
            ?.FullPath;
    }

    /// <summary>Whether the mods hold a level of this name, or the game does</summary>
    public bool HasMap(string name) => Assets.Exists<MapAsset>(name);

    /// <summary>
    /// The picture a thing's class shows when it spawns: the first frame of its Spawn state
    /// (from its own class or the nearest parent with one), facing the viewer
    /// </summary>
    public SpriteAsset? ThingSprite(string className)
    {
        if (_thingSprites.TryGetValue(className, out var cached))
            return cached;

        SpriteAsset? sprite = null;
        if (SpawnFrame(className) is { } frame && frame.Frames.Count > 0)
        {
            // 0 for a thing that looks the same from every side; one that turns has 1 (its front) to 8
            var name = $"{frame.Sprite}{frame.Frames[0]}";
            sprite = Find<SpriteAsset>(name + "0");
            _thingSpriteNames[className] = name + "0";
            if (sprite == null && Find<SpriteAsset>(name + "1") is { } front)
            {
                sprite = front;
                _rotating.Add(className);
                _thingSpriteNames[className] = name + "1";
            }
        }

        _thingSprites[className] = sprite;
        return sprite;
    }

    private readonly Dictionary<string, string> _thingSpriteNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The name of the sprite <see cref="ThingSprite"/> shows ("GARDA1"), or null when it has none</summary>
    public string? ThingSpriteName(string className)
        => ThingSprite(className) != null ? _thingSpriteNames.GetValueOrDefault(className) : null;

    /// <summary>A thing seen differently from each side (an enemy), so which way it faces matters</summary>
    public bool IsRotating(string className)
    {
        ThingSprite(className);
        return _rotating.Contains(className);
    }

    /// <summary>The actordefs classes, the pack's own over the shared ones</summary>
    public IReadOnlyDictionary<string, ActorData> Actors => _actors;

    /// <summary>How many skills game-info has (mapdefs min-skill counts them from 0, easiest first)</summary>
    public int SkillCount => GameInfo?.Skills.Count ?? 4;

    /// <summary>A class's flags, its parents' included (flags are inherited), upper-cased</summary>
    public HashSet<string> ClassFlags(string className)
    {
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>();
        for (var name = className; !string.IsNullOrWhiteSpace(name) && visited.Add(name) && _actors.TryGetValue(name, out var actor); name = actor.Parent)
            flags.UnionWith(actor.Flags);
        return flags;
    }

    /// <summary>A class property, from the class or its nearest parent that has it</summary>
    public object? ClassProperty(string className, string key)
    {
        var visited = new HashSet<string>();
        for (var name = className; !string.IsNullOrWhiteSpace(name) && visited.Add(name) && _actors.TryGetValue(name, out var actor); name = actor.Parent)
        {
            if (actor.Properties.TryGetValue(key, out var value))
                return value;
        }
        return null;
    }

    /// <summary>A flat panel standing in its tile (actordefs WALLSPRITE), not a billboard</summary>
    public bool IsWallSprite(string className)
    {
        if (!_wallSprites.TryGetValue(className, out var isWallSprite))
            _wallSprites[className] = isWallSprite = ClassFlags(className).Contains("WALLSPRITE");
        return isWallSprite;
    }

    private readonly Dictionary<string, bool> _wallSprites = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How far a wall sprite's panel sits toward its front, in texels (32 = the tile's edge)</summary>
    public int WallSpriteOffset(string className)
        => int.TryParse(Convert.ToString(ClassProperty(className, "wallsprite.offset"), System.Globalization.CultureInfo.InvariantCulture), out var offset)
            ? Math.Clamp(offset, 0, 32)
            : 0;

    /// <summary>The level's game-info entry as loaded (the base game's, with the mods' merged in), or null when it has none</summary>
    public MapInfo? MapInfoOf(string mapName)
        => GameInfo?.Maps.FirstOrDefault(map => map.Key.Equals(mapName, StringComparison.OrdinalIgnoreCase)).Value;

    public DefaultMapInfo DefaultMapInfo => GameInfo?.DefaultMap ?? new DefaultMapInfo();

    /// <summary>The names of every music asset, for the level properties</summary>
    public IReadOnlyList<string> MusicNames { get; private set; } = [];

    private ActorStatesData? SpawnFrame(string className)
    {
        var visited = new HashSet<string>();
        for (var name = className; !string.IsNullOrWhiteSpace(name) && visited.Add(name) && _actors.TryGetValue(name, out var actor); name = actor.Parent)
        {
            if (actor.States.TryGetValue("Spawn", out var states) && states.OfType<ActorStatesData>().FirstOrDefault() is { } first)
                return first;
        }
        return null;
    }

    /// <summary>
    /// The levels game-info plays (episodes in order, then the rest it lists), named as it names
    /// them, followed by any other level the assets have
    /// </summary>
    private List<MapEntry> ListMaps()
    {
        var maps = new List<MapEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, info) in GameInfo?.Maps ?? [])
        {
            if (seen.Add(name) && FindMap(name) is { } map)
                maps.Add(new MapEntry(name, MapTitle(info.Name, map)));
        }

        foreach (var name in Assets.AssetNames.Where(name => Assets.Exists<MapAsset>(name) && !seen.Contains(name)).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (seen.Add(name) && FindMap(name) is { } map)
                maps.Add(new MapEntry(name, MapTitle(null, map)));
        }

        return maps;
    }

    // Game-info's name when it's plain text, else the name in the level's own header
    private static string MapTitle(string? gameInfoName, MapAsset map)
        => !string.IsNullOrWhiteSpace(gameInfoName) && !gameInfoName.StartsWith('$')
            ? gameInfoName
            : map.Name?.TrimEnd('\0', ' ') ?? "";
}
