using PFWolf.Assets;
using PFWolf.Assets.Sounds;
using PFWolf.Loaders;
using PFWolf.Managers;

namespace PFWolf.Editor.Data;

/// <summary>
/// A level the game's game-info plays, or one only the data files have. <paramref name="Label"/>
/// is what the level list calls it in place of its name, for a hub game's floors ("M1 F2"; empty
/// for others), and <paramref name="Unused"/> marks a hub mission's slot the game never plays.
/// </summary>
public sealed record MapEntry(string Name, string Title, string Label = "", bool Unused = false)
{
    public override string ToString() => string.IsNullOrWhiteSpace(Title) ? Name : $"{Name}  {Title}";
}

/// <summary>Where a file is in a mod (a folder or a pk3): "texts/end1.txt"</summary>
public sealed record ModEntry(string ModPath, string EntryPath)
{
    /// <summary>The mod's file or folder name</summary>
    public string ModName => Path.GetFileName(ModPath.TrimEnd('\\', '/'));

    /// <summary>
    /// Whether it's somewhere in one of these folders of the mod ("texts"): a file of its own, not
    /// one of a mod's data files (AUDIOT, VSWAP) that holds many assets
    /// </summary>
    public bool IsIn(params string[] folders)
        => folders.Any(folder => EntryPath.Replace('\\', '/').StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));
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
        LoadedPalette = palette;

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

    /// <summary>
    /// The game palette as the game loads it (not as the palette browser is editing it): what
    /// the game matches a picture file's colors to, so what the editor writes pictures in
    /// </summary>
    public PaletteColor[] LoadedPalette { get; }

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
    /// <param name="progress">Told each step as it starts, for a loading screen; on the loading thread</param>
    public static GameContent Load(string gameFolder, string requestedPack, IEnumerable<string> mods, Action<string>? progress = null)
    {
        if (!File.Exists(Path.Combine(gameFolder, AssetManager.BasePk3FileName)))
            throw new FileNotFoundException($"There's no {AssetManager.BasePk3FileName} in {gameFolder}: pick the folder PFWolf is in");

        Directory.SetCurrentDirectory(gameFolder);
        var modList = mods.ToList();
        progress?.Invoke("Picking the game");
        var game = GameTypes.PickGame(requestedPack, modList);

        var assets = new AssetManager();
        assets.Load(game.PackId, game.ReleaseId, modList, progress);
        foreach (var warning in assets.ModWarnings)
            WarningLog.Write(warning);

        progress?.Invoke("Reading the definitions");
        var packId = game.PackId;
        // A new standalone game has none yet: its levels are plain numbers until it gives some
        var mapDefs = assets.FindInGamePackIfAny<MapObjectTranslationAsset>("mapdefs") ?? new MapObjectTranslationAsset();
        var palette = assets.Find<Palette>(assets.GetGamePaletteName())?.Colors ?? [];

        // Files directly in actordefs/ belong to every pack; the pack's own come after, as in the game
        var actorDefs = new[] { "actordefs", $"{packId}/actordefs" }
            .Where(assets.Exists<ActorTranslationAsset>)
            .Select(name => assets.Find<ActorTranslationAsset>(name)!.Actors);
        return new GameContent(assets, game, mapDefs, assets.FindInGamePack<GameInfoAsset>("game-info"), palette, actorDefs)
        {
            GameFolder = Path.GetFullPath(gameFolder),
        };
    }

    /// <summary>The folder PFWolf and its pfwolf.pk3 are in; empty for a game made of definitions alone</summary>
    public string GameFolder { get; private init; } = "";

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
    /// The mod a level comes from (its maps/NAME.wad), a folder or a pk3, which saving it writes
    /// back into; null when it's the game's own
    /// </summary>
    public string? ModPathOf(string mapName) => ModPathOf(mapName, nameof(MapAsset));

    /// <summary>The mod (a folder or a pk3) an asset of this type comes from, or null when it's the game's own</summary>
    public string? ModPathOf(string assetName, string assetType) => ModFileOf(assetName, assetType)?.ModPath;

    /// <summary>
    /// The mod file (a folder's or a pk3's) the asset the game uses comes from, of the first of these
    /// types and then the ones that play in its place; null when it's the game's own
    /// </summary>
    public ModEntry? ModFileOf(string assetName, params string[] assetTypes)
    {
        var found = Assets.FindAssetOrigins(assetName).ToList();
        var origin = assetTypes
            .SelectMany(type => found.Where(asset => asset.Type == type).SelectMany(asset => asset.Origins))
            .LastOrDefault(origin => origin.Action != AssetOrigin.LeftOut);
        if (origin == null)
            return null;

        var mod = Assets.LoadedMods.LastOrDefault(mod => mod.Source.Name.Equals(origin.Source, StringComparison.OrdinalIgnoreCase));
        return mod != null ? new ModEntry(mod.FullPath, origin.Path) : null;
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

    /// <summary>
    /// Why a thing's class shows nothing when it should: it isn't in the actordefs, or its Spawn
    /// frame names a sprite the game can't find; null when it shows, or is meant not to (TNT1,
    /// no Spawn frame: a patrol point), or when there are no actordefs to check against
    /// </summary>
    public string? BrokenThingReason(string className)
    {
        if (_actors.Count == 0)
            return null;
        if (!_actors.ContainsKey(className))
            return $"{className} isn't in the actordefs";
        if (ThingSprite(className) != null || SpawnFrame(className) is not { Frames.Count: > 0 } frame
            || string.IsNullOrEmpty(frame.Sprite) || frame.Sprite == "TNT1")
            return null;
        return $"{className}'s sprite {frame.Sprite}{frame.Frames[0]} isn't found";
    }

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

    /// <summary>
    /// The damage types something in play deals, as a destructible wall's damage-types name them:
    /// Bullet, Melee and Blast (a gun's, the knife's and a blast's with none of their own), and
    /// every class's damagetype and weapon.damagetype. Empty when there are no actordefs to look in.
    /// </summary>
    public IReadOnlySet<string> DamageTypesDealt
    {
        get
        {
            if (_damageTypes == null)
            {
                _damageTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (_actors.Count > 0)
                {
                    _damageTypes.UnionWith(["Bullet", "Melee", "Blast"]);
                    foreach (var actor in _actors.Values)
                    {
                        foreach (var key in new[] { "damagetype", "weapon.damagetype" })
                        {
                            if (actor.Properties.TryGetValue(key, out var type) && Convert.ToString(type, System.Globalization.CultureInfo.InvariantCulture) is { Length: > 0 } name)
                                _damageTypes.Add(name);
                        }
                    }
                }
            }
            return _damageTypes;
        }
    }

    private HashSet<string>? _damageTypes;

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

    /// <summary>The kinds of weather a level can name (built in and game-info weather-types), then none</summary>
    public IReadOnlyList<string> WeatherNames => WeatherInfo.TypesOf(GameInfo).Keys
        .Order(StringComparer.OrdinalIgnoreCase).Append(WeatherInfo.None).ToList();

    /// <summary>The factions a thing or class can be on (factions.yaml's, and the pack's player-faction and monster-faction)</summary>
    public IReadOnlyList<string> FactionNames
    {
        get
        {
            var table = new FactionTable(Assets.FindInGamePackIfAny<FactionsAsset>("factions"));
            return table.Names.Append(table.PlayerFaction).Append(table.MonsterFaction)
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    /// <summary>The sprite color translations (translations.yaml) a thing, class, faction or player color can name</summary>
    public IReadOnlyList<string> TranslationNames =>
        Assets.FindInGamePackIfAny<TranslationsAsset>("translations")?.Translations.Keys.Order(StringComparer.OrdinalIgnoreCase).ToList() ?? [];

    private readonly Dictionary<string, List<string>> _hintMessages = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A hint text's messages (Blake Stone's InformantHints): the text split at its "^XX"s, its
    /// formatting codes left out, as the game's BlakeMonster.HintMessages reads it
    /// </summary>
    public IReadOnlyList<string> HintMessages(string textName)
    {
        if (_hintMessages.TryGetValue(textName, out var cached))
            return cached;

        var messages = new List<string>();
        if (Find<TextAsset>(textName) is { } asset)
        {
            var parts = asset.ToText().Split("^XX");
            foreach (var part in parts.Take(parts.Length - 1))     // after the last ^XX is only the end
            {
                var text = System.Text.RegularExpressions.Regex.Replace(part, @"\^[A-Z]{2}[0-9A-Fa-f]{0,2}", "");
                messages.Add(string.Join("\n", text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(line => line.Trim())).Trim('\n'));
            }
        }
        return _hintMessages[textName] = messages;
    }

    /// <summary>
    /// Whether the game ever goes to a level: game-info starts an episode there or names it as
    /// another level's next or secret next (an unlisted level only the map command reaches is left out)
    /// </summary>
    public bool IsLevelGoneTo(string mapName)
    {
        if (GameInfo == null)
            return true;
        bool Named(string? name) => name?.Equals(mapName, StringComparison.OrdinalIgnoreCase) == true;
        return GameInfo.Episodes.Values.Any(episode => Named(episode.StartMap))
               || GameInfo.Maps.Values.Any(map => Named(map.Next) || Named(map.SecretNext));
    }

    /// <summary>A raw data mod's chunks still under the generator's made-up names (RawDataMapFile.NamesToGive), read once</summary>
    public IReadOnlyDictionary<string, ModEntry> ChunksToName => _chunksToName ??= RawDataMapFile.NamesToGive(this);

    private Dictionary<string, ModEntry>? _chunksToName;

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
            {
                // A hub mission's floor (Blake Stone's) goes by its mission and floor; a slot of
                // the mission that's no floor (nothing goes to it, and it goes nowhere) is unused
                if (GameInfo!.Clusters.TryGetValue(info.Cluster, out var cluster) && cluster.Hub)
                {
                    bool unused = info.FloorNumber == 0 && string.IsNullOrEmpty(info.Next) && !IsLevelGoneTo(name);
                    maps.Add(new MapEntry(name, MapTitle(info.Name, map), unused ? $"M{info.Cluster} —" : $"M{info.Cluster} F{info.FloorNumber}", unused));
                }
                else
                    maps.Add(new MapEntry(name, MapTitle(info.Name, map)));
            }
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
