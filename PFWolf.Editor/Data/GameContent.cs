using PFWolf.Assets;
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

    private GameContent(AssetManager assets, GameType game, MapObjectTranslationAsset mapDefs)
    {
        Assets = assets;
        Game = game;
        MapDefs = mapDefs;
        GameInfo = assets.FindInGamePack<GameInfoAsset>("game-info");

        var palette = assets.Find<Palette>(assets.GetGamePaletteName());
        Palette = palette?.Colors ?? [];

        // Files directly in actordefs/ belong to every pack; the pack's own come after, as in the game
        foreach (var actors in new[] { Find<ActorTranslationAsset>("actordefs"), Find<ActorTranslationAsset>($"{PackId}/actordefs") })
            foreach (var (name, data) in actors?.Actors ?? [])
                _actors[name] = ActorData.Combine(_actors.GetValueOrDefault(name), data);

        Maps = ListMaps();
    }

    public AssetManager Assets { get; }
    public GameType Game { get; }
    public string PackId => GameTypes.GetGamePackId(Game);
    public MapObjectTranslationAsset MapDefs { get; }
    public GameInfoAsset? GameInfo { get; }
    public PaletteColor[] Palette { get; }
    public IReadOnlyList<MapEntry> Maps { get; }

    /// <summary>The game's title from gamepack-info ("Spear of Destiny"), or its pack id</summary>
    public string Title => Assets.GetGameDescription() ?? PackId;

    /// <summary>
    /// Loads the game whose pfwolf.pk3 and data files are in <paramref name="gameFolder"/>. Like the
    /// game, it reads them from the working folder, which it changes to that one. Warnings go to
    /// WarningLog; a game that can't load throws (DataFilesException for missing data files).
    /// </summary>
    /// <param name="requestedPack">A game pack id ("spear"), or empty to pick by the data files</param>
    public static GameContent Load(string gameFolder, string requestedPack, IEnumerable<string> mods)
    {
        if (!File.Exists(Path.Combine(gameFolder, AssetManager.BasePk3FileName)))
            throw new FileNotFoundException($"There's no {AssetManager.BasePk3FileName} in {gameFolder}: pick the folder PFWolf is in");

        Directory.SetCurrentDirectory(gameFolder);
        var game = GameTypes.PickGameType(GameTypes.ParseGameType(requestedPack));

        var assets = new AssetManager();
        assets.Load(GameTypes.GetGamePackId(game), GameTypes.GetReleaseId(game), mods);
        foreach (var warning in assets.ModWarnings)
            WarningLog.Write(warning);

        var mapDefs = assets.FindInGamePack<MapObjectTranslationAsset>("mapdefs")
            ?? throw new InvalidDataException($"{GameTypes.GetGamePackId(game)} has no mapdefs");
        return new GameContent(assets, game, mapDefs);
    }

    /// <summary>An asset, or null without the "not found" warning AssetManager.Find gives</summary>
    public T? Find<T>(string name) where T : Asset
        => Assets.Exists<T>(name) ? Assets.Find<T>(name) : null;

    public MapAsset? FindMap(string name) => Find<MapAsset>(name);

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
            sprite = Find<SpriteAsset>(name + "0") ?? Find<SpriteAsset>(name + "1");
        }

        _thingSprites[className] = sprite;
        return sprite;
    }

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
