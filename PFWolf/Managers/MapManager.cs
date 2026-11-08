using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;


namespace PFWolf.Managers;

/// <summary>
/// The game pack's plane 0 floor codes (mapdefs floors), resolved. See <see cref="MapFloorsTranslation"/>.
/// </summary>
internal sealed record FloorCodes(int AreaTile, int NumAreas, int AmbushTile, int SecretExitTile, int HiddenAreaTile)
{
    /// <summary>Floor codes that set off a trigger action when the player steps onto them</summary>
    public IReadOnlyDictionary<int, string> Triggers { get; init; } = new Dictionary<int, string>();

    /// <summary>Floor codes that do something to the enemy standing on them (MapFloorsTranslation.ActorCodes)</summary>
    public IReadOnlyDictionary<int, Assets.MapActorCodeTranslation> ActorCodes { get; init; } = new Dictionary<int, Assets.MapActorCodeTranslation>();

    /// <summary>Areas (from 0) enemies never walk onto, nor off once on them (MapFloorsTranslation.MonsterKeepOut)</summary>
    public IReadOnlySet<int> MonsterKeepOut { get; init; } = new HashSet<int>();

    /// <summary>
    /// No floor codes at all (mapdefs with no floors, as in a new stand-alone game): no tile is
    /// an area, so every enemy hears and wakes as though it were in the player's area
    /// </summary>
    public static readonly FloorCodes NoAreas = new(-1, 0, -1, -1, -1);

    private static bool _warnedNoAreas;

    public static FloorCodes From(MapFloorsTranslation floors)
    {
        if (floors.AreaStart is not { } start || floors.AreaCount is not { } count)
        {
            if (!_warnedNoAreas)
                Console.WriteLine("The mapdefs floors have no area-start and area-count: the level has no areas, so sound carries everywhere");
            _warnedNoAreas = true;
            return NoAreas;
        }
        if (start < 1 || count is < 1 or > 255)
            throw new Exception($"The mapdefs floors' area-start ({start}) must be 1 or more and area-count ({count}) 1 to 255");
        return new FloorCodes(start, count, floors.Ambush ?? -1, floors.SecretExit ?? -1, floors.HiddenAreaStart ?? -1)
        {
            Triggers = floors.Triggers ?? new Dictionary<int, string>(),
            ActorCodes = floors.ActorCodes ?? new Dictionary<int, Assets.MapActorCodeTranslation>(),
            MonsterKeepOut = (floors.MonsterKeepOut ?? []).ToHashSet(),
        };
    }

    /// <summary>The plain area code for a hidden one (see MapFloorsTranslation.HiddenAreaStart); any other code as it is.</summary>
    public int Unhidden(int tile) =>
        HiddenAreaTile > 0 && tile >= HiddenAreaTile && tile < HiddenAreaTile + NumAreas ? tile - HiddenAreaTile + AreaTile : tile;
}
/// <summary>What the player has seen of a map tile (MapManager.seen), for the automap.</summary>
[Flags]
internal enum SeenFlags : byte
{
    None = 0,
    /// <summary>A view ray crossed the tile (open floor, or an open door).</summary>
    Floor = 1,
    /// <summary>A view ray hit this side of the tile's wall or door.</summary>
    NorthFace = 2,
    SouthFace = 4,
    WestFace = 8,
    EastFace = 16,
    /// <summary>A view ray hit a diagonal wall's 45 degree face (MapManager.wallshape).</summary>
    DiagonalFace = 32,
}

internal class MapManager
{
    public const int NumMaps = 60;
    public const int MapPlanes = MapConstants.MapPlanes;

    /// <inheritdoc cref="MapConstants.LevelPlanes"/>
    public const int LevelPlanes = MapConstants.LevelPlanes;

    private readonly Lazy<AssetManager> assetManager;
    private readonly Lazy<FactionManager> factionManager;
    private readonly GameSession session;

    /// <summary>
    /// The story a wall's block runs up to where plane 3 doesn't say (game-info wall-height, else
    /// a UWMF level's own, else the default map's); its block starts at story 1
    /// </summary>
    internal int LevelWallStories = 1;

    /// <summary>
    /// The player's story (game-info story): a tile's block that takes it in is solid (a wall),
    /// one that ends below it is floor, one that starts above it is overhead. Set before LoadMap;
    /// the `story` cheat changes it with <see cref="ApplyStory"/>.
    /// </summary>
    internal int Story = 1;

    /// <summary>
    /// Which of a pushwall's stories move for an A_PushWall or A_MoveWall that doesn't say
    /// (game-info pushwall-stories, the level's else the default map's): set before LoadMap
    /// </summary>
    internal Gameplay.PushwallStories PushwallStories = Gameplay.PushwallStories.All;

    /// <summary>
    /// The level's wall height from game-info (null: the level's own, else the default map's),
    /// for LoadMap to settle <see cref="LevelWallStories"/> before it works out what's solid
    /// </summary>
    internal int? WallHeightSetting;
    internal int DefaultWallHeight = 1;

    private UInt16[][] mapsegs = new ushort[LevelPlanes][];

    private readonly LinkedList<Entities.Actors.Actor> _actors = new();

    /// <summary>Goes up each time the level's actors are put on afresh (a map loaded, a level restored): which level it is</summary>
    internal int LevelSerial { get; set; }       // (set by tests standing for a new level)

    /// <summary>An actor has been put on the level as it plays (SpawnThing, SpawnAtActor...): the scripts' game.onactorspawn</summary>
    internal Action<Entities.Actors.Actor>? ActorAdded { get; set; }

    // The acting player's pawn (GameSession.ActAs), held in _actors with every other player's at
    // its head, in player order. Null until CreatePlayer runs for the current level -- LoadMap
    // clears _actors, which drops the previous level's pawns.
    internal Entities.Actors.PlayerPawn? Player => session.PlayerState.Pawn;

    /// <summary>Every player's pawn on this level, in player order</summary>
    internal IReadOnlyList<Entities.Actors.PlayerPawn> Players
    {
        get
        {
            // Asked many times a tic: the same array while the pawns are the same, a new one
            // (never the old one changed, which someone may be going through) when they aren't
            var players = session.Players;
            int count = 0;
            bool same = true;
            foreach (var p in players)
            {
                if (p.Pawn is not { } pawn)
                    continue;
                same &= count < _pawns.Length && ReferenceEquals(_pawns[count], pawn);
                count++;
            }
            if (same && count == _pawns.Length)
                return _pawns;

            var pawns = new Entities.Actors.PlayerPawn[count];
            count = 0;
            foreach (var p in players)
                if (p.Pawn is { } pawn)
                    pawns[count++] = pawn;
            return _pawns = pawns;
        }
    }

    private Entities.Actors.PlayerPawn[] _pawns = [];

    // Drops every player's pawn (the level's actors are going)
    private void ForgetPawns()
    {
        foreach (var p in session.Players)
            p.Pawn = null;
    }

    private int _difficulty;
    private string? _enemyHealthKey;

    public MapManager(Lazy<AssetManager> assetManager, Lazy<FactionManager> factionManager, GameSession session,
        Lazy<Entities.Actors.ActorWorld>? actorWorld)
    {
        this.assetManager = assetManager;
        this.factionManager = factionManager;
        this.session = session;
        this.actorWorld = actorWorld;
        Scrollers = new Rendering.Scrollers(this);
    }

    /// <summary>The level's scrolling walls and flats, by tag (Rendering/Scrollers.cs)</summary>
    internal Rendering.Scrollers Scrollers { get; }

    // What the actors use of the game, taken when the first actor is made: it's built after the
    // map (it needs the map). Null in tests that make maps with no world
    private readonly Lazy<Entities.Actors.ActorWorld>? actorWorld;

    /// <summary>
    /// The level's size in tiles (MapConstants.MinMapSize to MaxMapSize each way; Wolf3D's are
    /// 64x64). Every per-tile array is [MapWidth, MapHeight], and every per-tile list is indexed
    /// <see cref="TileIndex"/>, row by row like the planes.
    /// </summary>
    internal ushort MapWidth = MapConstants.ClassicMapSize, MapHeight = MapConstants.ClassicMapSize;

    /// <summary>How many tiles the level has</summary>
    internal int MapArea => MapWidth * MapHeight;

    /// <summary>A tile's place in the planes and the other per-tile lists: row by row</summary>
    internal int TileIndex(int x, int y) => y * MapWidth + x;

    /// <summary>Whether a tile is on the level</summary>
    internal bool InMap(int x, int y) => (uint)x < MapWidth && (uint)y < MapHeight;

    /// <summary>Whether a tile is on the map's edge, which is always solid</summary>
    internal bool OnEdge(int x, int y) => x == 0 || y == 0 || x == MapWidth - 1 || y == MapHeight - 1;

    /// <summary>A tile's <see cref="TileIndex"/>, or -1 when it's off the level</summary>
    internal int TileIndexOrNone(int x, int y) => InMap(x, y) ? y * MapWidth + x : -1;

    internal ushort[,] TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize];

    /// <summary>
    /// Each wall tile's shape: Square, or a 45 degree diagonal from a mapdefs diagonal marker on
    /// the object plane. Not saved: it's rebuilt from the planes whenever they're loaded or restored.
    /// </summary>
    internal WallShape[,] WallShapes = new WallShape[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize];

    /// <summary>
    /// Each wall tile's patch (mapdefs patches), 0 for none. A map keeps it in the high byte of
    /// plane 0 (MapConstants.PatchShift), which LoadMap takes off into here, so plane 0 holds only
    /// the tiles' own codes. Saved with the level.
    /// </summary>
    internal byte[,] PatchMap = new byte[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize];

    internal const int HeightPlane = MapConstants.HeightPlane;

    /// <summary>
    /// Each tile's tag, 0 for none: a switch acts on the doors, walls and actors that share its
    /// tag. An actor takes its tag from the tile it spawns on (<see cref="Entities.Actors.Actor.Tag"/>).
    /// </summary>
    internal const int TagPlane = MapConstants.TagPlane;

    /// <summary>
    /// Each tile's light zone, 0 for none (the map's light): the map's game-info zones say how
    /// each is lit (Rendering/Lighting.cs).
    /// </summary>
    internal const int ZonePlane = MapConstants.ZonePlane;
    internal const int StoryPlane = MapConstants.StoryPlane;

    /// <summary>Goes up whenever the zone plane may have changed: a level loaded or restored, or a zone set.</summary>
    internal int ZonesVersion { get; private set; }

    /// <summary>
    /// ECWolf's floor and ceiling plane: the low byte of a tile's value picks its floor flat and
    /// the high byte its ceiling flat, through the mapdefs flats table (see <see cref="FloorFlat"/>).
    /// </summary>
    internal const int FlatPlane = MapConstants.FlatPlane;

    /// <summary>
    /// Each tile's floor and ceiling texture: its flat plane indices through the mapdefs flats
    /// table, else the map's default-floor and default-ceiling; null where there's none (the
    /// floor or ceiling color shows). Indexed <see cref="TileIndex"/>. Flats don't move, so these
    /// are only built when a level is loaded or restored; an animated flat's tiles are set to its
    /// frame each frame (AnimateFlats).
    /// </summary>
    internal TextureAsset?[] FloorFlat = new TextureAsset?[MapConstants.ClassicMapArea], CeilingFlat = new TextureAsset?[MapConstants.ClassicMapArea];

    // The tiles whose floor or ceiling is an animation (animdefs), with it
    private readonly List<(int Index, AnimatedAsset<TextureAsset> Animation)> animatedfloors = [], animatedceilings = [];

    /// <summary>Whether any tile has a floor flat, or a ceiling flat.</summary>
    internal bool HasFloorFlats, HasCeilingFlats;

    /// <summary>
    /// Each tile's own ceiling sky and floor sky (mapdefs flats skies / floor-skies): 1 + its
    /// index in <see cref="TileSkies"/>, 0 for none. Indexed <see cref="TileIndex"/>; built with the flats.
    /// </summary>
    internal byte[] CeilingSky = new byte[MapConstants.ClassicMapArea], FloorSky = new byte[MapConstants.ClassicMapArea];

    /// <summary>The names of the skies tiles show (CeilingSky and FloorSky's), as the tiles name them (after any A_ChangeTexture)</summary>
    internal readonly List<string> TileSkies = [];

    /// <summary>Whether any tile has a ceiling sky, or a floor sky, of its own.</summary>
    internal bool HasCeilingSkies, HasFloorSkies;

    private void ClearTileSkies()
    {
        CeilingSky = new byte[MapArea];
        FloorSky = new byte[MapArea];
        TileSkies.Clear();
        HasCeilingSkies = HasFloorSkies = false;
    }
    internal bool[,] Spotvis = new bool[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize];
    internal Actor?[,] ActorAt = new Actor?[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize];

    /// <summary>
    /// What the player has seen of this level, for the automap: floor the view rays have crossed
    /// and the wall faces they've hit. Unlike spotvis it lasts for the whole level, and it's saved.
    /// </summary>
    internal SeenFlags[,] Seen = new SeenFlags[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize];

    /// <summary>Adds the floor tiles visible this frame (spotvis, after the walls are traced) to <see cref="Seen"/>.</summary>
    internal void MarkSeenFromSpotvis()
    {
        for (int x = 0; x < MapWidth; x++)
            for (int y = 0; y < MapHeight; y++)
                if (Spotvis[x, y])
                    Seen[x, y] |= SeenFlags.Floor;
    }

    /// <summary>The seen map as one byte per tile, column by column, for save games.</summary>
    internal byte[] GetSeenBytes()
    {
        var bytes = new byte[MapArea];
        for (int x = 0; x < MapWidth; x++)
            for (int y = 0; y < MapHeight; y++)
                bytes[x * MapHeight + y] = (byte)Seen[x, y];
        return bytes;
    }

    internal void SetSeenBytes(byte[] bytes)
    {
        for (int x = 0; x < MapWidth; x++)
            for (int y = 0; y < MapHeight; y++)
                Seen[x, y] = (SeenFlags)bytes[x * MapHeight + y];
    }

    public ushort GetTile(int x, int y, int plane)
    {
        return mapsegs[0][y * MapWidth + x];
    }

    // A wall's id is kept in TileMap below the BitWall and BitDoor flags
    private static bool IsWallId(int tile) => tile is > 0 and < GameConstants.BitWall;

    private readonly HashSet<int> badwallids = new(), badswitches = new();

    private FloorCodes? _floors;

    /// <summary>The plane 0 floor codes, from the mapdefs floors.</summary>
    internal FloorCodes Floors => _floors ??= FloorCodes.From(GetMapData().Floors);

    /// <param name="difficulty">The skill's place in game-info's skills (mapdefs min-skill compares against it)</param>
    /// <param name="enemyHealth">The skill's enemy-health: the actordefs property enemies' health comes from</param>
    /// <param name="floorNumber">The map's game-info floor-number, which map-info tag-links name floors by</param>
    public void LoadMap(string mapName, int difficulty, string? enemyHealth = null, int floorNumber = -1)
    {
        CurrentFloorNumber = floorNumber;
        _difficulty = difficulty;
        _enemyHealthKey = enemyHealth;
        _floors = null; // read again, in case the mapdefs changed

        var mapAsset = assetManager.Value.Find<MapAsset>(mapName);
        if (mapAsset == null)
            throw new Exception($"Map not found {mapName}");

        // A UWMF level's own walls, doors, flats and triggers, over the game's mapdefs while it's played
        _levelMapData = mapAsset.LocalDefs != null ? PackMapData().With(mapAsset.LocalDefs) : null;
        LevelDefaults = mapAsset.Defaults;
        if (MapConstants.CheckMapSize(mapAsset.Width, mapAsset.Height) is { } badSize)
            throw new Exception($"Map {mapName} can't be played: {badSize}");
        MapWidth = mapAsset.Width;
        MapHeight = mapAsset.Height;
        // The level's wall height: game-info's, else its own (a UWMF level's), else the default map's
        LevelWallStories = Math.Clamp(WallHeightSetting ?? LevelDefaults.WallStories ?? DefaultWallHeight, 1, GameConstants.MaxWallStories);
        // Clone each plane: SpawnDoor and other setup code mutate mapsegs in place
        // (e.g. SetMapSpot), and mapAsset is a cached singleton reused for every
        // load of this level, so writing through the original array would
        // permanently corrupt the cached map data (doors would vanish on replay).
        mapsegs = new ushort[mapAsset.MapData.Length][];
        for (int i = 0; i < mapAsset.MapData.Length; i++)
            mapsegs[i] = (ushort[])mapAsset.MapData[i].Clone();
        SplitPatches();
        FloorFlat = new TextureAsset?[MapArea];     // BuildFlats fills these in, once the level's defaults are known
        CeilingFlat = new TextureAsset?[MapArea];
        HasFloorFlats = HasCeilingFlats = false;
        ClearTileSkies();
        animatedfloors.Clear();
        animatedceilings.Clear();
        ClearTextureChanges();
        ObjectiveCounts = new(StringComparer.OrdinalIgnoreCase);     // counted afresh as the level's set up (Gameplay/Objectives.cs)
        WallDamage = [];
        CrumblingWalls = [];

#if USE_FEATUREFLAGS
    const int MXX = MAPSIZE - 1;
    
    // Read feature flags data from map corners and overwrite corners with adjacent tiles
    ffDataTopLeft     = MAPSPOT(0,   0,   0); MAPSPOT(0,   0,   0) = MAPSPOT(1,       0,       0);
    ffDataTopRight    = MAPSPOT(MXX, 0,   0); MAPSPOT(MXX, 0,   0) = MAPSPOT(MXX,     1,       0);
    ffDataBottomRight = MAPSPOT(MXX, MXX, 0); MAPSPOT(MXX, MXX, 0) = MAPSPOT(MXX - 1, MXX,     0);
    ffDataBottomLeft  = MAPSPOT(0,   MXX, 0); MAPSPOT(0,   MXX, 0) = MAPSPOT(0,       MXX - 1, 0);
#endif

        TileMap = new ushort[MapWidth, MapHeight]; // wall values only
        Spotvis = new bool[MapWidth, MapHeight];
        Seen = new SeenFlags[MapWidth, MapHeight];
        ActorAt = new Actor?[MapWidth, MapHeight];
        storygrids.Clear();

        // Every actor lives in _actors and nothing else clears this list, so reloading a level
        // (death with lives left, replaying a level in a new game, etc.) would otherwise pile
        // the new level's actors on top of the previous load's instead of replacing them.
        _actors.Clear();
        LevelSerial++;
        ForgetPawns();
        PlayerStart = null;
        EntranceStart = false;
        DeathmatchStarts.Clear();
        foreach (var starts in TeamStarts)
            starts.Clear();

        var data = GetMapData();
        UnhideAreas();
        ReadMapInfo(fillFlats: true);
        foreach (var id in data.Walls.Keys.Where(id => !IsWallId(id)))
            if (badwallids.Add(id))
                Console.WriteLine($"mapdefs wall {id} is ignored: wall ids are 1 to {GameConstants.BitWall - 1}");
        foreach (var (id, wall) in data.Walls)
            if (wall.Switch is { To: not 0 } wallSwitch && !(IsWallId(wallSwitch.To) && data.Walls.ContainsKey(wallSwitch.To))
                && badswitches.Add(id))
                Console.WriteLine($"mapdefs wall {id}'s switch goes to {wallSwitch.To}, which isn't a wall; it won't change when used");
        foreach (var id in data.Patches.Keys.Where(id => id is < 1 or > 255))
            if (badpatches.Add(id))
                Console.WriteLine($"mapdefs patch {id} is ignored: patch ids are 1 to 255");
        foreach (var (id, patch) in data.Patches)
            if (patch.Switch is { To: not 0, Remove: false } patchSwitch && !data.Patches.ContainsKey(patchSwitch.To)
                && badpatchswitches.Add(id))
                Console.WriteLine($"mapdefs patch {id}'s switch goes to {patchSwitch.To}, which isn't a patch; it won't change when used");

        // Two wall ids the mapdefs don't give: the open map's sealed edge (drawn as nothing) and a
        // floor tile's block that's solid at the player's story (drawn with its flats sides)
        var unused = Enumerable.Range(1, GameConstants.BitWall - 1).Reverse().Where(id => !data.Walls.ContainsKey(id)).Take(2).ToList();
        EdgeWall = unused.Count > 0 ? unused[0] : 1;
        BlockWall = unused.Count > 1 ? unused[1] : 0;
        BuildBlocks();

        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                int tile = MapSpot(x, y, 0);
                bool door = data.Doors.ContainsKey(tile);
                bool wallCode = IsWallId(tile) && data.Walls.ContainsKey(tile);
                // a wall, or a floor tile with a block, is solid where its block takes in the player's
                // story; a wall round the map's edge always is, so nothing leaves the map
                bool solid = door || (wallCode || BlockStops[x, y] != 0 && BlockWall != 0) && (BlocksStory(x, y) || wallCode && OnEdge(x, y));
                CheckPatch(mapName, x, y, isWall: wallCode, data);
                if (solid)
                {
                    // solid wall (a door's tile is made a door by SpawnDoor)
                    int id = door || wallCode ? tile : BlockWall;
                    TileMap[x, y] = (ushort)id;
                    ActorAt[x, y] = new Wall(id);// (uint)tile;
                    // a destructible wall that's a secret counts toward the total (found as it breaks)
                    if (!session.LoadedGame && wallCode && data.Walls.TryGetValue(tile, out var wallDef) && wallDef.Destructible is { Secret: true })
                        session.GameState.SecretTotal++;
                }
                else
                {
                    // area floor (unless a patroller already scanned is heading onto it)
                    TileMap[x, y] = 0;
                    if (ActorAt[x, y] is not ActorMark)
                        ActorAt[x, y] = null;
                }

                // TODO: SpawnDoor

                if (infotiles[TileIndex(x, y)])
                    continue;

                // Things only stand on open floor: an object-plane value on a wall or door is
                // something else (Blake Stone's door locks, switch and teleporter data), unless
                // the thing is one made to sit in a wall (`in-wall: true`, a trap shooter)
                int objtile = MapSpot(x, y, 1);
                if (data.Things.TryGetValue(objtile, out var thingXlat) && (!solid || thingXlat.InWall && !data.Doors.ContainsKey(tile)))
                {
                    SpawnThing(x, y, thingXlat);
                    continue;
                }

                // The player pawn is created after the map loads (LevelLoop.InitActorList), so
                // only note where it starts; with several starts, the last one scanned wins. An
                // entrance's start (mapdefs entrance) wins over them when the level is entered
                // by it (GameState.Entrance), and is passed over otherwise.
                if (data.PlayerStarts.TryGetValue(objtile, out var startXlat))
                {
                    int team = Array.FindIndex(Gameplay.GameModes.TeamNames, t => t.Equals(startXlat.Team, StringComparison.OrdinalIgnoreCase));
                    if (team >= 0)
                        TeamStarts[team].Add(new MapPlayerStart(x, y, startXlat.Angles));
                    else if (startXlat.Deathmatch)
                        DeathmatchStarts.Add(new MapPlayerStart(x, y, startXlat.Angles));
                    else if (startXlat.Entrance.Length > 0)
                    {
                        if (startXlat.Entrance.Equals(session.GameState.Entrance, StringComparison.OrdinalIgnoreCase))
                        {
                            PlayerStart = new MapPlayerStart(x, y, startXlat.Angles);
                            EntranceStart = true;
                        }
                    }
                    else if (!EntranceStart)
                        PlayerStart = new MapPlayerStart(x, y, startXlat.Angles);
                    continue;
                }

                if (data.Triggers.TryGetValue(objtile, out var triggerXlat))
                {
                    if (triggerXlat.Secret && !session.LoadedGame)
                        session.GameState.SecretTotal++;
                    // what shows on its tile (mapdefs trigger `class`)
                    if (!string.IsNullOrEmpty(triggerXlat.Class))
                        SpawnThing(x, y, new MapActorTranslation { Class = triggerXlat.Class }, countKill: false);
                    continue;
                }
            }
        }

        SpawnExtraThings(mapName, mapAsset.ExtraThings);
        SealEdge(mapName, data);
        BuildWallShapes();
        BuildBlockAreas();
        ZonesVersion++;
    }

    private readonly HashSet<string> missingclasses = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The things the object plane can't hold (a UWMF level's: a second on a tile, one off a
    /// tile's middle, one on skills min-skill can't say, or a class with no mapdefs code), by
    /// class, after the plane's own
    /// </summary>
    private void SpawnExtraThings(string mapName, IReadOnlyList<MapThing> things)
    {
        foreach (var thing in things)
        {
            if (!thing.OnSkill(_difficulty) || !InMap(thing.TileX, thing.TileY))
                continue;

            // its own story, not its tile's plane 6 (the object plane's thing's)
            var actor = SpawnThing(thing.TileX, thing.TileY,
                new MapActorTranslation { Class = thing.Class, Angles = thing.Angle, Patrol = thing.Patrol ? 1 : 0 },
                story: ThingStory(thing.TileX, thing.TileY, thing.Story));
            if (actor == null)
            {
                if (missingclasses.Add(thing.Class))
                    Console.WriteLine($"{mapName}: thing class {thing.Class} isn't an actor (actordefs); it's left out");
                continue;
            }
            if (thing.Ambush)
                actor.RuntimeFlags |= ObjFlags.Ambush;

            // Off its tile's middle: where the level puts it (a patroller is on its way already)
            bool centered = Math.Abs(thing.X - thing.TileX - 0.5) < 0.01 && Math.Abs(thing.Y - thing.TileY - 0.5) < 0.01;
            if (!centered && !thing.Patrol)
            {
                actor.X = (int)(thing.X * MapConstants.TileGlobal);
                actor.Y = (int)(thing.Y * MapConstants.TileGlobal);
            }
        }
    }

    /// <summary>
    /// The wall id <see cref="SealEdge"/> makes the open tiles round the map's edge (0: none yet).
    /// It's drawn as nothing, so the floor and ceiling past it are those tiles' own.
    /// </summary>
    internal int EdgeWall { get; set; }

    /// <summary>
    /// Makes the open tiles round the map's edge solid: view traces and movement only stop at
    /// walls, and would run off the map. They're a wall id the mapdefs don't give, so they're
    /// drawn as nothing (the floor and ceiling show through) but can't be crossed.
    /// </summary>
    private void SealEdge(string mapName, MapObjectTranslationAsset data)
    {
        int edgeWall = EdgeWall;
        int sealedTiles = 0;
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                if ((x != 0 && y != 0 && x != MapWidth - 1 && y != MapHeight - 1) || TileMap[x, y] != 0)
                    continue;
                TileMap[x, y] = (ushort)edgeWall;
                ActorAt[x, y] = new Wall(edgeWall);
                sealedTiles++;
            }
        }

        if (sealedTiles > 0)
            Console.WriteLine($"{mapName}: {sealedTiles} tiles round the map's edge aren't mapdefs walls or doors; they're made solid");
    }

    /// <summary>
    /// Turns the hidden area codes on plane 0 into the plain area codes (mapdefs floors
    /// hidden-area-start), keeping which tiles were hidden (<see cref="IsHiddenArea"/>)
    /// </summary>
    private void UnhideAreas()
    {
        var plane = mapsegs[0];
        hiddenTiles = new bool[plane.Length];
        if (Floors.HiddenAreaTile <= 0)
            return;

        for (int i = 0; i < plane.Length; i++)
        {
            var plain = (ushort)Floors.Unhidden(plane[i]);
            hiddenTiles[i] = plain != plane[i];
            plane[i] = plain;
        }
    }

    // Tiles whose area was a hidden one (UnhideAreas), indexed TileIndex
    private bool[] hiddenTiles = [];

    /// <summary>Whether a tile is in a hidden area (Blake Stone's rooms kept off the automap's "exposed areas")</summary>
    internal bool IsHiddenArea(int x, int y) => InMap(x, y) && TileIndex(x, y) is var i && i < hiddenTiles.Length && hiddenTiles[i];

    /// <summary>
    /// Object-plane tiles that hold map info (mapdefs map-info) or its value, rather than a
    /// thing. Indexed <see cref="TileIndex"/>. Rebuilt with the planes, so it isn't saved.
    /// </summary>
    private bool[] infotiles = new bool[MapConstants.ClassicMapArea];

    internal record MapHint(int Message, byte Area);

    /*
    Tiles enemies can't walk onto, though players can (Midnight Assault's BLOCK_ENEMIES 501 and
    BLOCK_BOSSES 502): an object-plane thing whose class has `blocktile: monsters` keeps every
    monster off its tile, `blocktile: bosses` only those with the BOSS flag. `blocktile: players`
    keeps players off it on foot, but not riding a vehicle, and `blocktile: all` both monsters and
    players on foot (water a boat crosses). Such a thing is never spawned; its tile is marked as
    the map is read, so a loaded game has it too.
    */

    /// <summary>A tile's blocker: none, every monster, bosses only, players on foot, or monsters and players on foot. Indexed <see cref="TileIndex"/>.</summary>
    internal enum MonsterBlock : byte { None, Monsters, Bosses, Players, All }

    private MonsterBlock[] monsterblocks = new MonsterBlock[MapConstants.ClassicMapArea];

    // Docks (Gameplay/Vehicles.cs): an object-plane thing whose class has `docktile: true`
    // marks where a vehicle can't go and its rider gets off. Never spawned, marked as the map's read.
    private bool[] docks = new bool[MapConstants.ClassicMapArea];

    /// <summary>Whether a tile is a dock, where a vehicle can't go and its rider gets off</summary>
    internal bool IsDock(int x, int y) => InMap(x, y) && TileIndex(x, y) < docks.Length && docks[TileIndex(x, y)];

    /// <summary>Marks a tile a dock (as the map's docktile things do)</summary>
    internal void SetDock(int x, int y, bool dock)
    {
        if (docks.Length != MapArea)
            docks = new bool[MapArea];
        if (InMap(x, y))
            docks[TileIndex(x, y)] = dock;
    }

    // A class that only marks its tile (blocktile, docktile) is never spawned
    static bool MarksTileOnly(Assets.ActorMetadata metadata, string className) =>
        metadata.TryGetProperty(className, "blocktile", out var blocktile) && BlockKind(blocktile?.ToString()) != MonsterBlock.None
        || metadata.TryGetProperty(className, "docktile", out var docktile) && IsTrue(docktile);

    static bool IsTrue(object? value) => value is true || bool.TryParse(value?.ToString(), out var b) && b;

    /// <summary>
    /// Whether a monster (a boss, or any) can't walk onto a tile: its blocktile marker keeps it
    /// off, or it's in one of the floors' monster-keep-out areas (Blake Stone's elevators)
    /// </summary>
    internal bool BlocksMonster(int x, int y, bool boss) =>
        InMap(x, y) && (InMonsterKeepOut(x, y) || TileIndex(x, y) < monsterblocks.Length && monsterblocks[TileIndex(x, y)] switch
        {
            MonsterBlock.Monsters or MonsterBlock.All => true,
            MonsterBlock.Bosses => boss,
            _ => false,
        });

    /// <summary>Whether a player on foot can't walk onto a tile: its blocktile marker is `players` or `all`</summary>
    internal bool BlocksPlayer(int x, int y) =>
        InMap(x, y) && TileIndex(x, y) < monsterblocks.Length && monsterblocks[TileIndex(x, y)] is MonsterBlock.Players or MonsterBlock.All;

    /// <summary>Whether a tile is in one of the floors' monster-keep-out areas (bstone's ElevatorFloor)</summary>
    internal bool InMonsterKeepOut(int x, int y) =>
        _floors is { MonsterKeepOut.Count: > 0 } floors && AreaAt(x, y) is >= 0 and var area && floors.MonsterKeepOut.Contains(area);

    /// <summary>Marks a tile's monster blocker (as the map's blocktile things do)</summary>
    internal void SetMonsterBlock(int x, int y, MonsterBlock block)
    {
        if (monsterblocks.Length != MapArea)
            monsterblocks = new MonsterBlock[MapArea];
        if (InMap(x, y))
            monsterblocks[TileIndex(x, y)] = block;
    }

    /// <summary>Takes away every monster blocker of a kind (A_ClearMonsterBlocks: Greed's gong); returns whether there were any</summary>
    internal bool ClearMonsterBlocks(MonsterBlock kind)
    {
        bool any = false;
        for (int i = 0; i < monsterblocks.Length; i++)
        {
            if (monsterblocks[i] != kind)
                continue;
            monsterblocks[i] = MonsterBlock.None;
            any = true;
        }
        return any;
    }


    // The things whose class has a `blocktile` mark their tiles (MonsterBlock)
    private void ReadMonsterBlocks(ushort[] objects)
    {
        monsterblocks = new MonsterBlock[MapArea];
        docks = new bool[MapArea];
        var things = GetMapData().Things;
        if (things.Count == 0)
            return;
        var metadata = assetManager.Value.GetActorMetadata();
        var kinds = new Dictionary<int, (MonsterBlock Block, bool Dock)>();
        for (int i = 0; i < MapArea && i < objects.Length; i++)
        {
            int id = objects[i];
            if (id == 0)
                continue;
            if (!kinds.TryGetValue(id, out var kind))
            {
                kind = things.TryGetValue(id, out var thing)
                    ? (metadata.TryGetProperty(thing.Class, "blocktile", out var value) ? BlockKind(value?.ToString()) : MonsterBlock.None,
                       metadata.TryGetProperty(thing.Class, "docktile", out var dock) && IsTrue(dock))
                    : (MonsterBlock.None, false);
                kinds[id] = kind;
            }
            monsterblocks[i] = kind.Block;
            docks[i] = kind.Dock;
        }
    }

    internal static MonsterBlock BlockKind(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "monsters" or "enemies" => MonsterBlock.Monsters,
        "bosses" => MonsterBlock.Bosses,
        "players" => MonsterBlock.Players,
        "all" => MonsterBlock.All,
        _ => MonsterBlock.None,
    };

    /// <summary>
    /// The map's hints (map-info `hint:<text>` codes), by text: each the message's number in the
    /// text and the room it's in (0xff for none), in map order. Read with the map, so not saved.
    /// </summary>
    internal Dictionary<string, List<MapHint>> Hints { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The map's own ceiling and floor colors, as palette indices: its first map-info
    /// ceiling-floor-colors code's as it loads, then the last one the player stepped onto
    /// (<see cref="StepOntoColorsCode"/>). Saved with the level.
    /// </summary>
    internal byte? MapCeilingColor { get; set; }
    internal byte? MapFloorColor { get; set; }

    /// <summary>
    /// The level shows game-info's alt-ceiling-color and alt-floor-color now (A_Warp
    /// "swapcolors"). Saved with the level.
    /// </summary>
    internal bool AltFlatColors { get; set; }

    /// <summary>
    /// The player stepped onto (x, y): a ceiling-floor-colors code there (exactly the code, its
    /// low byte 0) gives the level the colors in the tile after it, as Blake Stone changes them
    /// on the fly (bstone's Thrust)
    /// </summary>
    internal void StepOntoColorsCode(int x, int y)
    {
        int code = MapSpot(x, y, 1);
        if (code == 0 || (code & 0xff) != 0 || x + 1 >= MapWidth
            || !GetMapData().MapInfo.TryGetValue(code >> 8, out var kind) || !kind.Equals(MapInfoCodes.CeilingFloorColors, StringComparison.OrdinalIgnoreCase))
            return;
        int value = MapSpot(x + 1, y, 1);
        MapCeilingColor = (byte)(value >> 8);
        MapFloorColor = (byte)(value & 0xff);
    }

    /// <summary>
    /// Reads the map-info codes on the object plane (mapdefs map-info): marks their tiles in
    /// <see cref="infotiles"/>, takes the map's colors, and with <paramref name="fillFlats"/>
    /// gives its flats to every flat plane tile still at 0. As in Blake Stone, only a map's first
    /// code of each kind counts.
    /// </summary>
    private void ReadMapInfo(bool fillFlats)
    {
        infotiles = new bool[MapArea];
        MapCeilingColor = MapFloorColor = null;
        AltFlatColors = false;
        Hints = new(StringComparer.OrdinalIgnoreCase);

        var objects = mapsegs[1];
        ReadMonsterBlocks(objects);
        var links = new Dictionary<int, ushort>();      // linked tile -> its tag
        if (fillFlats)          // a fresh level: a restored one has its tags already
            LinkObjectPlaneSwitches(objects, links);

        var codes = GetMapData().MapInfo;
        if (codes.Count == 0)
            return;

        bool gotFlats = false;
        for (int i = 0; i < MapArea && i < objects.Length; i++)
        {
            if (!codes.TryGetValue(objects[i] >> 8, out var kind))
                continue;

            infotiles[i] = true;
            if (MapInfoCodes.HintText(kind) is { } hintText)
            {
                // the hint's number, and the room it's in: its floor's, or a neighbour's on a floor
                // code that isn't a room; 0xff when it's on a wall or door (a general hint)
                int spot = mapsegs[0][i];
                bool onWall = IsWallId(spot) && GetMapData().Walls.ContainsKey(spot) || GetMapData().Doors.ContainsKey(spot);
                byte area = onWall ? (byte)0xff : SpawnArea(i % MapWidth, i / MapWidth);
                if (!Hints.TryGetValue(hintText, out var list))
                    Hints[hintText] = list = [];
                list.Add(new MapHint(objects[i] & 0xff, area));
                continue;
            }
            if (!MapInfoCodes.HasValue(kind) || i + 1 >= objects.Length)
                continue;

            int codeTile = i;
            var value = objects[++i];
            infotiles[i] = true;
            if (kind.Equals(MapInfoCodes.TagLink, StringComparison.OrdinalIgnoreCase))
            {
                if (fillFlats)          // a fresh level: a restored one has its tags already
                    LinkTags(codeTile, objects[codeTile] & 0xff, value >> 8, value & 0xff, links);
            }
            else if (kind.Equals(MapInfoCodes.CeilingFloorColors, StringComparison.OrdinalIgnoreCase))
            {
                if (MapCeilingColor == null)
                {
                    MapCeilingColor = (byte)(value >> 8);
                    MapFloorColor = (byte)(value & 0xff);
                }
            }
            else if (kind.Equals(MapInfoCodes.CeilingFloorFlats, StringComparison.OrdinalIgnoreCase) && !gotFlats)
            {
                gotFlats = true;
                if (fillFlats)
                {
                    var flats = mapsegs[FlatPlane];
                    for (int t = 0; t < flats.Length; t++)
                        if (flats[t] == 0)
                            flats[t] = value;
                }
            }
        }
    }

    /// <summary>
    /// Switches whose walls.yaml switch has `link: object-plane`: each gets the tag of the tile
    /// its object-plane value names (x high byte, y low byte), as a tag-link on this floor, and
    /// that value is no thing.
    /// </summary>
    private void LinkObjectPlaneSwitches(ushort[] objects, Dictionary<int, ushort> links)
    {
        var walls = GetMapData().Walls;
        var walllayer = mapsegs[0];
        for (int i = 0; i < MapArea && i < objects.Length && i < walllayer.Length; i++)
        {
            // A smart switch floor trigger's value is the tile it works, not a thing
            if (objects[i] != 0 && Floors.Triggers.TryGetValue(walllayer[i], out var floorAction)
                && floorAction.StartsWith("A_SmartSwitch", StringComparison.OrdinalIgnoreCase))
            {
                infotiles[i] = true;
                continue;
            }
            if (objects[i] == 0 || !IsWallId(walllayer[i]) || !walls.TryGetValue(walllayer[i], out var wall)
                || wall.Switch is not { } wallSwitch || !wallSwitch.Link.Equals("object-plane", StringComparison.OrdinalIgnoreCase))
                continue;
            infotiles[i] = true;
            LinkTags(i, 0xff, objects[i] >> 8, objects[i] & 0xff, links);
        }
    }

    // Tags map-info tag-links hand out: counted down from the top, clear of tags a map author gives
    private const ushort FirstLinkTag = 0xffff;

    /// <summary>
    /// A map-info tag-link from the code's tile to (x, y) on floor <paramref name="floor"/>: on
    /// this floor, both tiles get the target's tag (a new one, unless another link already
    /// tagged it), and so do the linked-things joined to the target through their neighbours.
    /// </summary>
    private void LinkTags(int codeTile, int floor, int x, int y, Dictionary<int, ushort> links)
    {
        if (floor != 0xff && floor != CurrentFloorNumber)
            return;     // on another floor: for when floors keep their state
        if (x <= 0 || y <= 0 || x >= MapWidth - 1 || y >= MapHeight - 1)
            return;

        int target = TileIndex(x, y);
        if (!links.TryGetValue(target, out var tag))
        {
            tag = (ushort)(FirstLinkTag - links.Values.Distinct().Count());
            var linked = GetMapData().LinkedThings;
            var open = new Queue<int>([target]);
            var seen = new HashSet<int> { target };
            while (open.Count > 0)
            {
                int spot = open.Dequeue();
                links[spot] = tag;
                mapsegs[TagPlane][spot] = tag;
                int sx = spot % MapWidth, sy = spot / MapWidth;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = sx + dx, ny = sy + dy;
                    int next = TileIndex(nx, ny);
                    if (nx >= 0 && ny >= 0 && nx < MapWidth && ny < MapHeight
                        && linked.Contains(mapsegs[1][next]) && seen.Add(next))
                        open.Enqueue(next);
                }
            }
        }
        mapsegs[TagPlane][codeTile] = tag;
    }

    /// <summary>The floor number (game-info floor-number) of the level loading, for map-info tag-links</summary>
    internal int CurrentFloorNumber { get; private set; }

    /// <summary>
    /// The area a thing spawning on this tile is in: its floor code's, else a neighbouring tile's
    /// (a floor code that isn't an area, like Blake Stone's 157 and 158), else area 0.
    /// </summary>
    internal byte SpawnArea(int x, int y)
    {
        foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (0, -1), (0, 1), (-1, 0) })
        {
            int tx = x + dx, ty = y + dy;
            if (tx >= 0 && ty >= 0 && tx < MapWidth && ty < MapHeight && ValidArea(FloorCode(tx, ty)))
                return (byte)(FloorCode(tx, ty) - Floors.AreaTile);
        }
        return 0;
    }

    /*
    =============================================================================

                                    BLOCKS

    A tile's block runs from a start story to a stop story (plane 3: start in the high byte, stop
    in the low byte, each 1 to 8; a start of 0 is 1). A tile can hold a second block above the
    first with a gap between (its start in bits 12-15, its stop in bits 4-7: 0x7385 is stories 3
    to 5 and 7 to 8); it's solid if either takes in the story. With no block of its own, a wall tile's runs
    from story 1 to the level's wall height and a floor tile has none. Against the player's story
    (Story) a block that takes it in is solid, one that ends below it is floor, one that starts
    above it is overhead. A plane-0 wall whose block doesn't take in the story is walked on (or
    under): it keeps its wall id on plane 0 for drawing, and takes a neighbour's area
    (FloorCode). A floor tile whose block does is solid: BlockWall in TileMap, drawn with its
    flats sides (BlockSideTexture).

    =============================================================================
    */

    /// <summary>
    /// Each tile's own block from the height plane: start and stop stories, 0 where it has none
    /// (its kind's default, <see cref="BlockSpan"/>). Rebuilt from the planes whenever they're
    /// loaded or restored, like <see cref="WallShapes"/>.
    /// </summary>
    internal byte[,] BlockStarts = new byte[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize];
    internal byte[,] BlockStops = new byte[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize];

    /// <summary>Each tile's second block, above its first with a gap between: 0 where it has none</summary>
    internal byte[,] UpperStarts = new byte[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize];
    internal byte[,] UpperStops = new byte[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize];
    private int maxtilestories;

    /// <summary>
    /// A height-plane value's first block, or null for none: see <see cref="DecodeBlocks"/>
    /// </summary>
    internal static (int Start, int Stop)? DecodeBlock(int value) => DecodeBlocks(value).Lower;

    /// <summary>
    /// A height-plane value's blocks, null for none: 0, or anything that isn't a start and stop
    /// of 1 to 8 with the start no higher than the stop (such as an ECWolf info plane's values).
    /// A second block (its start in bits 12-15, its stop in bits 4-7) must start above the
    /// first's stop: touching it, the two are one block; anything else and the value's no block.
    /// </summary>
    internal static ((int Start, int Stop)? Lower, (int Start, int Stop)? Upper) DecodeBlocks(int value) =>
        Definitions.StoryBlocks.Decode(value);

    /// <summary>A block as a height-plane value: a plain stop when it starts at story 1, as tall walls always were</summary>
    internal static ushort EncodeBlock(int start, int stop) => Definitions.StoryBlocks.Encode(start, stop);

    /// <summary>Two blocks as a height-plane value (the second above the first with a gap; a stop of 0: none)</summary>
    internal static ushort EncodeBlocks(int start, int stop, int start2, int stop2) =>
        Definitions.StoryBlocks.Encode(start, stop, start2, stop2);

    /// <summary>Sets <see cref="BlockStarts"/>, <see cref="BlockStops"/> and the upper blocks from the height plane</summary>
    private void BuildBlocks()
    {
        BlockStarts = new byte[MapWidth, MapHeight];
        BlockStops = new byte[MapWidth, MapHeight];
        UpperStarts = new byte[MapWidth, MapHeight];
        UpperStops = new byte[MapWidth, MapHeight];
        maxtilestories = 0;

        for (int y = 0; y < MapHeight; y++)
            for (int x = 0; x < MapWidth; x++)
                StoreBlocks(x, y, MapSpot(x, y, HeightPlane));
    }

    // Puts a height-plane value's blocks in BlockStarts and the rest
    private void StoreBlocks(int x, int y, int value)
    {
        var (lower, upper) = DecodeBlocks(value);
        (BlockStarts[x, y], BlockStops[x, y]) = lower is var (s, e) ? ((byte)s, (byte)e) : ((byte)0, (byte)0);
        (UpperStarts[x, y], UpperStops[x, y]) = upper is var (s2, e2) ? ((byte)s2, (byte)e2) : ((byte)0, (byte)0);
        maxtilestories = Math.Max(maxtilestories, Math.Max(BlockStops[x, y], UpperStops[x, y]));
    }

    /// <summary>Whether plane 0 holds a mapdefs wall here (solid or not at the player's story)</summary>
    internal bool IsWallCode(int x, int y) => IsWallId(MapSpot(x, y, 0)) && GetMapData().Walls.ContainsKey(MapSpot(x, y, 0));

    /// <summary>
    /// A tile's block: its own from the height plane, else a wall tile's default (story 1 to the
    /// level's wall height), else null (open floor, all the way down)
    /// </summary>
    internal (int Start, int Stop)? BlockSpan(int x, int y) =>
        BlockStops[x, y] != 0 ? (BlockStarts[x, y], BlockStops[x, y])
        : IsWallCode(x, y) || TileMap[x, y] is > 0 and < GameConstants.BitDoor and not GameConstants.BitWall ? (1, LevelWallStories)
        : null;

    /// <summary>
    /// A tile's blocks, bottom up: its own, a wall's default (story 1 to the level's wall height)
    /// if it has none; whatever TileMap holds (a moving pushwall's tiles have only their own)
    /// </summary>
    internal List<(int Start, int Stop)> TileBlocks(int x, int y)
    {
        var blocks = new List<(int Start, int Stop)>(2);
        if (BlockStops[x, y] != 0)
            blocks.Add((BlockStarts[x, y], BlockStops[x, y]));
        else if (IsWallCode(x, y))
            blocks.Add((1, LevelWallStories));
        if (UpperStops[x, y] != 0)
            blocks.Add((UpperStarts[x, y], UpperStops[x, y]));
        return blocks;
    }

    /// <summary>Blocks put together bottom up, joined where they overlap or touch</summary>
    internal static List<(int Start, int Stop)> JoinBlocks(IEnumerable<(int Start, int Stop)> blocks)
    {
        var joined = new List<(int Start, int Stop)>();
        foreach (var (start, stop) in blocks.OrderBy(b => b.Start))
        {
            if (joined.Count > 0 && start <= joined[^1].Stop + 1)
                joined[^1] = (joined[^1].Start, Math.Max(joined[^1].Stop, stop));
            else
                joined.Add((start, stop));
        }
        return joined;
    }

    /// <summary>
    /// Sets a tile's blocks (as <see cref="SetBlocks"/>) from a list joined bottom up: none gives it
    /// back its default; more than two are joined from the top down until two are left
    /// </summary>
    internal void SetTileBlocks(int x, int y, List<(int Start, int Stop)> blocks)
    {
        blocks = JoinBlocks(blocks);
        while (blocks.Count > 2)
        {
            blocks[^2] = (blocks[^2].Start, blocks[^1].Stop);
            blocks.RemoveAt(blocks.Count - 1);
        }
        if (blocks.Count == 0)
            SetBlock(x, y, 0, 0);
        else if (blocks.Count == 1)
            SetBlock(x, y, blocks[0].Start, blocks[0].Stop);
        else
            SetBlocks(x, y, blocks[0].Start, blocks[0].Stop, blocks[1].Start, blocks[1].Stop);
    }

    /// <summary>A tile's second block, above its first with a gap between; null for none</summary>
    internal (int Start, int Stop)? UpperSpan(int x, int y) =>
        UpperStops[x, y] != 0 ? (UpperStarts[x, y], UpperStops[x, y]) : null;

    /// <summary>A tile's blocks and what each is at the player's story, for the console</summary>
    internal string DescribeBlock(int x, int y)
    {
        if (BlockSpan(x, y) is not var (start, stop))
            return "no block: open floor";
        string Describe(int s, int e) => $"stories {s} to {e}"
            + (s <= Story && Story <= e ? ", solid" : e < Story ? ", floor below the player" : ", overhead");
        return Describe(start, stop) + (BlockStops[x, y] == 0 ? " (the level's wall height)" : "")
            + (UpperSpan(x, y) is var (s2, e2) ? "; " + Describe(s2, e2) : "");
    }

    /// <summary>Whether either of a tile's blocks takes in the player's story: it's solid</summary>
    internal bool BlocksStory(int x, int y) => SolidSpan(x, y) != null;

    /// <summary>The block of a tile that takes in the player's story, null if neither does</summary>
    internal (int Start, int Stop)? SolidSpan(int x, int y)
    {
        if (BlockSpan(x, y) is var (start, stop) && start <= Story && Story <= stop)
            return (start, stop);
        if (UpperSpan(x, y) is var (s2, e2) && s2 <= Story && Story <= e2)
            return (s2, e2);
        return null;
    }

    /// <summary>
    /// The story a tile's wall (or a door's lintel) runs up to: its block's stop (the upper one's,
    /// if it has two), else the level's wall height
    /// </summary>
    internal int WallStories(int x, int y) =>
        UpperStops[x, y] != 0 ? UpperStops[x, y] : BlockStops[x, y] != 0 ? BlockStops[x, y] : LevelWallStories;

    /// <summary>The story a tile's (first) block starts at: 1 unless the height plane says otherwise</summary>
    internal int BlockStart(int x, int y) => BlockStops[x, y] != 0 ? BlockStarts[x, y] : 1;

    /// <summary>Whether an open tile has a block of its own starting above the player's story: an arch over it</summary>
    internal bool IsOverhead(int x, int y) => TileMap[x, y] == 0
        && (BlockStops[x, y] != 0 && BlockStarts[x, y] > Story || UpperStops[x, y] != 0 && UpperStarts[x, y] > Story);

    /// <summary>
    /// Whether any tile has a block of its own (plane 3) that takes in the player's story: a level
    /// whose walls are lower than the story is meant that way when it has some (a rooftop's parapets)
    /// </summary>
    internal bool AnyOwnBlockOnStory()
    {
        for (int y = 0; y < MapHeight; y++)
            for (int x = 0; x < MapWidth; x++)
                if (BlockStops[x, y] != 0 && BlockStarts[x, y] <= Story && Story <= BlockStops[x, y]
                    || UpperStops[x, y] != 0 && UpperStarts[x, y] <= Story && Story <= UpperStops[x, y])
                    return true;
        return false;
    }

    /// <summary>The tallest block anywhere on the level.</summary>
    internal int MaxWallStories => Math.Max(maxtilestories, LevelWallStories);

    /// <summary>
    /// Sets a tile's block on the height plane, so it's saved with the level; a stop of 0 gives it
    /// back to its kind's default. It's the tile's only block. Doesn't change what's solid: see
    /// <see cref="ApplyStory"/>.
    /// </summary>
    internal void SetBlock(int x, int y, int start, int stop) => SetBlocks(x, y, start, stop, 0, 0);

    /// <summary>
    /// Sets a tile's two blocks on the height plane (a second stop of 0: just the first), as
    /// <see cref="SetBlock"/>. A second that doesn't start above the first is dropped; one
    /// touching it joins it.
    /// </summary>
    internal void SetBlocks(int x, int y, int start, int stop, int start2, int stop2)
    {
        if (stop2 != 0 && (stop == 0 || start2 <= stop || start2 > stop2))
            stop2 = 0;
        else if (stop2 != 0 && start2 == stop + 1)
            (stop, stop2) = (stop2, 0);
        ushort value = EncodeBlocks(start, stop, start2, stop2);
        SetMapSpot(x, y, HeightPlane, value);
        StoreBlocks(x, y, value);
        UpdateSpan(x, y);
    }

    /// <summary>
    /// Every tile's <see cref="BlockSpan"/> (start and stop stories, a stop of 0 for none) and
    /// <see cref="UpperSpan"/>, by <see cref="TileIndex"/>, for the renderer to read quickly. Kept
    /// up to date by SetBlock and rebuilt with the blocks' areas (load, restore, <see cref="ApplyStory"/>).
    /// </summary>
    internal byte[] SpanStart = [], SpanStop = [], UpperSpanStart = [], UpperSpanStop = [];

    /// <summary>Whether a point z texels up from the ground on a tile is inside one of its blocks (from the spans)</summary>
    internal bool InBlock(int x, int y, float z)
    {
        int i = TileIndex(x, y);
        if ((uint)i >= (uint)SpanStop.Length || z < 0)
            return false;
        int story = (int)(z / GameConstants.TextureSize) + 1;
        return SpanStop[i] != 0 && SpanStart[i] <= story && story <= SpanStop[i]
            || UpperSpanStop[i] != 0 && UpperSpanStart[i] <= story && story <= UpperSpanStop[i];
    }

    /// <summary>
    /// The floor under a point z texels up from the ground on a tile: the top (texels up from the
    /// ground) of its highest block that ends at or below z, else the ground (0)
    /// </summary>
    internal float FloorUnder(int x, int y, float z)
    {
        int i = TileIndex(x, y);
        if ((uint)i >= (uint)SpanStop.Length)
            return 0;
        float floor = 0;
        if (SpanStop[i] != 0 && SpanStop[i] * GameConstants.TextureSize <= z + 0.5f)
            floor = SpanStop[i] * GameConstants.TextureSize;
        if (UpperSpanStop[i] != 0 && UpperSpanStop[i] * GameConstants.TextureSize <= z + 0.5f)
            floor = UpperSpanStop[i] * GameConstants.TextureSize;
        return floor;
    }

    private void RebuildSpans()
    {
        SpanStart = new byte[MapArea];
        SpanStop = new byte[MapArea];
        UpperSpanStart = new byte[MapArea];
        UpperSpanStop = new byte[MapArea];
        for (int y = 0; y < MapHeight; y++)
            for (int x = 0; x < MapWidth; x++)
                UpdateSpan(x, y);
    }

    private void UpdateSpan(int x, int y)
    {
        int i = TileIndex(x, y);
        if ((uint)i >= (uint)SpanStop.Length)
            return;
        (SpanStart[i], SpanStop[i]) = BlockSpan(x, y) is var (start, stop) ? ((byte)start, (byte)stop) : ((byte)0, (byte)0);
        (UpperSpanStart[i], UpperSpanStop[i]) = (UpperStarts[x, y], UpperStops[x, y]);
    }

    /// <summary>Sets a tile's wall height (a block from story 1); 0 gives it back to the default</summary>
    internal void SetWallStories(int x, int y, int stories) => SetBlock(x, y, 1, stories);

    /// <summary>
    /// The wall id <see cref="LoadMap"/> puts in TileMap for a floor tile whose block is solid at the
    /// player's story: one the mapdefs don't give, drawn with the tile's flats sides
    /// </summary>
    internal int BlockWall { get; set; }

    // Each walkable wall tile's area (a plane-0 wall whose block doesn't take in the player's
    // story), -1 elsewhere: the nearest area code across walkable tiles
    private short[] blockareas = [];

    /// <summary>
    /// A tile's plane-0 code for its area: plane 0, except on a wall tile that's walked on at the
    /// player's story (its plane 0 is its wall id), which takes the area of the nearest floor
    /// </summary>
    internal int FloorCode(int x, int y)
    {
        int i = TileIndex(x, y);
        return (uint)i < (uint)blockareas.Length && blockareas[i] >= 0 ? Floors.AreaTile + blockareas[i] : MapSpot(x, y, 0);
    }

    /// <summary>
    /// Works out the areas of the wall tiles walked on at the player's story (<see cref="FloorCode"/>),
    /// and every tile's span (<see cref="SpanStart"/>)
    /// </summary>
    private void BuildBlockAreas()
    {
        RebuildSpans();
        blockareas = new short[MapArea];
        Array.Fill(blockareas, (short)-1);
        var queue = new Queue<(int X, int Y, short Area)>();
        bool any = false;
        for (int y = 0; y < MapHeight; y++)
            for (int x = 0; x < MapWidth; x++)
            {
                if (TileMap[x, y] == 0 && IsWallCode(x, y))
                    any = true;
                else if (TileMap[x, y] == 0 && ValidArea(MapSpot(x, y, 0)))
                    queue.Enqueue((x, y, (short)(MapSpot(x, y, 0) - Floors.AreaTile)));
            }
        if (!any)
        {
            blockareas = [];
            return;
        }

        // out from every area tile at once, across the walkable wall tiles: each takes the nearest
        while (queue.Count > 0)
        {
            var (x, y, area) = queue.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (!InMap(nx, ny) || TileMap[nx, ny] != 0 || !IsWallCode(nx, ny) || blockareas[TileIndex(nx, ny)] >= 0)
                    continue;
                blockareas[TileIndex(nx, ny)] = area;
                queue.Enqueue((nx, ny, area));
            }
        }

        // walled off from every area: area 0
        for (int y = 0; y < MapHeight; y++)
            for (int x = 0; x < MapWidth; x++)
                if (TileMap[x, y] == 0 && IsWallCode(x, y) && blockareas[TileIndex(x, y)] < 0)
                    blockareas[TileIndex(x, y)] = 0;
    }

    /// <summary>
    /// Makes what's solid fit the player's story (after <see cref="Story"/> or a block changes):
    /// each wall tile or floor tile with a block is solid if its block takes in the story, and
    /// walked on if not. Doors, moving pushwalls and the map's sealed edge stay as they are, and a
    /// wall already solid keeps its TileMap value (a thrown switch, a door-side mark).
    /// </summary>
    internal void ApplyStory()
    {
        if (mapsegs.Length == 0 || mapsegs[0] == null)
            return;                     // no level loaded
        for (int y = 0; y < MapHeight; y++)
            for (int x = 0; x < MapWidth; x++)
            {
                var tile = TileMap[x, y];
                if ((tile & GameConstants.BitDoor) != 0 || tile == GameConstants.BitWall || (EdgeWall != 0 && tile == EdgeWall) || OnEdge(x, y))
                    continue;                   // the map's edge stays solid, whatever the story (LoadMap, SealEdge)
                bool wall = IsWallCode(x, y);
                if (!wall && BlockStops[x, y] == 0)
                {
                    if (tile == BlockWall && BlockWall != 0)
                        (TileMap[x, y], ActorAt[x, y]) = (0, null);    // a floor block that's gone
                    continue;
                }

                bool solid = BlocksStory(x, y);
                if (solid && tile == 0)
                {
                    int id = wall ? MapSpot(x, y, 0) : BlockWall;
                    TileMap[x, y] = (ushort)id;
                    ActorAt[x, y] = new Wall(id);
                }
                else if (!solid && tile != 0)
                {
                    TileMap[x, y] = 0;
                    if (ActorAt[x, y] is Wall)
                        ActorAt[x, y] = null;
                }
            }
        BuildBlockAreas();
    }

    /// <summary>
    /// The texture on the sides of a floor tile's block: its floor index's flats sides, else (with
    /// <paramref name="neighbours"/>) the face of a wall beside it (north, east, south, west);
    /// null when there's neither
    /// </summary>
    internal string? BlockSideTexture(int x, int y, bool neighbours = true)
    {
        var data = GetMapData();
        if (data.Flats.Sides.TryGetValue(MapSpot(x, y, FlatPlane) & 0xff, out var side) && !string.IsNullOrEmpty(side))
            return ChangedTexture(x, y, side);
        if (!neighbours)
            return null;
        foreach (var (dx, dy, face) in new[] { (0, -1, "south"), (1, 0, "west"), (0, 1, "north"), (-1, 0, "east") })
        {
            int nx = x + dx, ny = y + dy;
            if (InMap(nx, ny) && IsWallCode(nx, ny) && data.Walls.TryGetValue(MapSpot(nx, ny, 0), out var wall))
                return wall.WallFace(face);
        }
        return null;
    }

    /// <summary>
    /// The story a thing on a tile stands on: its thing-story plane value (plane 6, 1 to 8), else
    /// the floor under it. That's the player's story where the tile's walkable on it, or inside
    /// a block solid on it (a thing in a wall); else just over the highest block top under the
    /// player's story; else story 1, the ground (a street under the roofs).
    /// </summary>
    internal int ThingStory(int x, int y) => ThingStory(x, y, MapSpot(x, y, StoryPlane));

    /// <summary>A thing's story, as <see cref="ThingStory(int, int)"/>, with its own setting (a UWMF extra thing's) in place of plane 6</summary>
    internal int ThingStory(int x, int y, int set)
    {
        if (set is >= 1 and <= GameConstants.MaxWallStories)
            return set;
        int floor = 1;
        foreach (var (start, stop) in TileBlocks(x, y))
        {
            if (start <= Story && Story <= stop)
                return Story;
            if (stop < Story)
                floor = Math.Max(floor, stop + 1);
        }
        return TileMap[x, y] != 0 || floor == Story ? Story : floor;
    }

    /// <summary>The story an actor stands on: its own, or the player's (an actor's Story of 0)</summary>
    internal int StoryOf(Entities.Actors.Actor actor) => actor.Story == 0 ? Story : actor.Story;

    /// <summary>Whether an actor is on the player's story (and so a pickup can be taken, a collision felt)</summary>
    internal bool OnPlayersStory(Entities.Actors.Actor actor) => actor.Story == 0 || actor.Story == Story;

    /// <summary>The light zone a tile is in, 0 for none.</summary>
    internal ushort GetZone(int x, int y) => (ushort)MapSpot(x, y, ZonePlane);

    /// <summary>Puts a tile in a light zone on the zone plane, so it's saved with the level; 0 takes it out.</summary>
    internal void SetZone(int x, int y, ushort zone)
    {
        SetMapSpot(x, y, ZonePlane, zone);
        ZonesVersion++;
    }

    /// <summary>The tag on a tile, 0 for none.</summary>
    internal ushort GetTag(int x, int y) => (ushort)MapSpot(x, y, TagPlane);

    /// <summary>
    /// Tags a tile on the tag plane, so it's saved with the level; 0 clears it. An actor keeps
    /// the tag it spawned with, so this doesn't change the tags of any actors on the tile.
    /// </summary>
    internal void SetTag(int x, int y, ushort tag) => SetMapSpot(x, y, TagPlane, tag);

    /// <summary>
    /// Moves a tile's tag to another tile, as a pushwall slides, so a switch can push it again.
    /// An untagged pushwall leaves the tiles it crosses as they were.
    /// </summary>
    internal void MoveTag(int fromx, int fromy, int tox, int toy)
    {
        var tag = GetTag(fromx, fromy);
        if (tag == 0)
            return;
        SetTag(fromx, fromy, 0);
        SetTag(tox, toy, tag);
    }

    /// <summary>The live actors with this tag; none for tag 0, which is no tag.</summary>
    internal IEnumerable<Entities.Actors.Actor> TaggedActors(ushort tag) =>
        tag == 0 ? [] : _actors.Where(a => a.Tag == tag && !a.IsRemoved);

    /// <summary>The tiles with this tag, row by row; none for tag 0, which is no tag.</summary>
    internal IEnumerable<(int X, int Y)> TaggedTiles(ushort tag)
    {
        if (tag == 0)
            yield break;

        for (int y = 0; y < MapHeight; y++)
            for (int x = 0; x < MapWidth; x++)
                if (GetTag(x, y) == tag)
                    yield return (x, y);
    }

    /// <summary>
    /// Takes each tile's patch off the high byte of plane 0 into <see cref="PatchMap"/>, leaving
    /// plane 0 its own codes (walls, doors, floors), as everything else reads them
    /// </summary>
    private void SplitPatches()
    {
        PatchMap = new byte[MapWidth, MapHeight];
        var plane = mapsegs[0];
        for (int i = 0; i < plane.Length && i < MapArea; i++)
        {
            PatchMap[i % MapWidth, i / MapWidth] = (byte)(plane[i] >> MapConstants.PatchShift);
            plane[i] &= MapConstants.Plane0CodeMask;
        }
    }

    private readonly HashSet<int> badpatches = new(), badpatchswitches = new();
    private readonly HashSet<string> misplacedpatches = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Drops a patch from a tile that isn't a wall (open floor or a door), and says so once a
    /// map; one mapdefs doesn't list is kept (a mod may add it) but reported once.
    /// </summary>
    private void CheckPatch(string mapName, int x, int y, bool isWall, MapObjectTranslationAsset data)
    {
        int patch = PatchMap[x, y];
        if (patch == 0)
            return;

        if (!isWall)
        {
            PatchMap[x, y] = 0;
            if (misplacedpatches.Add(mapName))
                Console.WriteLine($"{mapName}: a patch on {x},{y}, which isn't a wall, is ignored (patches go on walls only)");
            return;
        }

        if (!data.Patches.ContainsKey(patch) && badpatches.Add(patch))
            Console.WriteLine($"{mapName}: patch {patch} on {x},{y} isn't in the mapdefs patches; it's drawn as nothing");
    }

    /// <summary>The patch on a tile, 0 for none.</summary>
    internal byte GetPatch(int x, int y) => PatchMap[x, y];

    /// <summary>Puts a patch on a tile, so it's saved with the level; 0 takes it off.</summary>
    internal void SetPatch(int x, int y, byte patch) => PatchMap[x, y] = patch;

    /// <summary>Moves a tile's patch to another tile, as a pushwall slides.</summary>
    internal void MovePatch(int fromx, int fromy, int tox, int toy)
    {
        var patch = PatchMap[fromx, fromy];
        if (patch == 0)
            return;
        PatchMap[fromx, fromy] = 0;
        PatchMap[tox, toy] = patch;
    }

    /// <summary>Moves a tile's blocks to another tile, as a pushwall slides.</summary>
    internal void MoveWallStories(int fromx, int fromy, int tox, int toy)
    {
        int start = BlockStarts[fromx, fromy], stop = BlockStops[fromx, fromy];
        int start2 = UpperStarts[fromx, fromy], stop2 = UpperStops[fromx, fromy];
        SetBlock(fromx, fromy, 0, 0);
        SetBlocks(tox, toy, start, stop, start2, stop2);
    }

    private string? defaultfloor, defaultceiling;

    internal string? DefaultFloor => defaultfloor;
    internal string? DefaultCeiling => defaultceiling;

    /// <summary>Sets a tile's value on the flat plane (floor index | ceiling index &lt;&lt; 8), so it's saved with the level.</summary>
    internal void SetFlats(int x, int y, ushort value)
    {
        SetMapSpot(x, y, FlatPlane, value);
        BuildFlats(defaultfloor, defaultceiling);
    }

    /// <summary>
    /// Sets <see cref="FloorFlat"/> and <see cref="CeilingFlat"/> from the flat plane, as ECWolf
    /// does: the low byte is the floor's index in the mapdefs flats table and the high byte the
    /// ceiling's. An index the table doesn't list (or whose texture isn't found) gets the map's
    /// default; with no default either, the tile keeps the floor or ceiling color.
    /// </summary>
    internal void BuildFlats(string? defaultFloor, string? defaultCeiling)
    {
        defaultfloor = defaultFloor;
        defaultceiling = defaultCeiling;

        var found = new Dictionary<string, Asset?>(StringComparer.OrdinalIgnoreCase);
        Asset? Flat(string? name) => name == null ? null
            : found.TryGetValue(name, out var flat) ? flat : found[name] = FindFlat(name);

        FloorFlat = new TextureAsset?[MapArea];
        CeilingFlat = new TextureAsset?[MapArea];
        HasFloorFlats = HasCeilingFlats = false;
        animatedfloors.Clear();
        animatedceilings.Clear();
        ClearTileSkies();

        // the skies tiles show, each named once
        var skyslots = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        byte SkySlot(string? name)
        {
            if (name == null)
                return 0;
            if (!skyslots.TryGetValue(name, out var slot))
            {
                if (TileSkies.Count >= byte.MaxValue)
                    return 0;
                TileSkies.Add(name);
                skyslots[name] = slot = (byte)TileSkies.Count;
            }
            return slot;
        }

        for (int y = 0; y < MapHeight; y++)
            for (int x = 0; x < MapWidth; x++)
            {
                int i = TileIndex(x, y);
                FloorFlat[i] = SetFlat(Flat(FlatTextureName(x, y, ceiling: false)), i, animatedfloors);
                CeilingFlat[i] = SetFlat(Flat(FlatTextureName(x, y, ceiling: true)), i, animatedceilings);
                HasFloorFlats |= FloorFlat[i] != null;
                HasCeilingFlats |= CeilingFlat[i] != null;
                FloorSky[i] = SkySlot(TileSkyName(x, y, ceiling: false));
                CeilingSky[i] = SkySlot(TileSkyName(x, y, ceiling: true));
                HasFloorSkies |= FloorSky[i] != 0;
                HasCeilingSkies |= CeilingSky[i] != 0;
            }
    }

    /// <summary>
    /// The sky a tile shows through its ceiling (or floor) of its own: its flat-plane index's
    /// entry in the mapdefs flats skies (or floor-skies), through any A_ChangeTexture on the
    /// tile; null for none (the level's sky, if any, shows where there's no ceiling flat)
    /// </summary>
    internal string? TileSkyName(int x, int y, bool ceiling)
    {
        int spot = MapSpot(x, y, FlatPlane);
        int index = ceiling ? spot >> 8 : spot & 0xff;
        var flats = GetMapData().Flats;
        var table = ceiling ? flats.Skies : flats.FloorSkies;
        if (!table.TryGetValue(index, out var name) || string.IsNullOrEmpty(name)
            || name.Equals("none", StringComparison.OrdinalIgnoreCase))
            return null;
        return ChangedTexture(x, y, name);
    }

    // A tile's flat to start with: an animation's first frame, the tile noted to animate
    private static TextureAsset? SetFlat(Asset? flat, int index, List<(int, AnimatedAsset<TextureAsset>)> animated)
    {
        if (flat is not AnimatedAsset<TextureAsset> animation)
            return flat as TextureAsset;
        animated.Add((index, animation));
        return animation.FrameAt(0);
    }

    /// <summary>
    /// Sets the tiles with an animated floor or ceiling (animdefs) to its frame <paramref name="tics"/>
    /// into the level: a looping one on the same frame on every tile, one that plays once from when
    /// its tile last changed (<see cref="AnimStart"/>). Once a frame, before the view is drawn.
    /// </summary>
    internal void AnimateFlats(int tics)
    {
        foreach (var (i, animation) in animatedfloors)
            FloorFlat[i] = animation.FrameAt(animation.Loops ? tics : tics - AnimStart[i]);
        foreach (var (i, animation) in animatedceilings)
            CeilingFlat[i] = animation.FrameAt(animation.Loops ? tics : tics - AnimStart[i]);
    }

    /// <summary>
    /// The texture on a tile's floor or ceiling: its flat plane index through the mapdefs flats
    /// table, else the map's default, through any A_ChangeTexture on the tile; null when it has
    /// none (the floor or ceiling color shows)
    /// </summary>
    internal string? FlatTextureName(int x, int y, bool ceiling)
    {
        int spot = MapSpot(x, y, FlatPlane);
        int index = ceiling ? spot >> 8 : spot & 0xff;
        var flats = GetMapData().Flats;
        var table = ceiling ? flats.Ceiling : flats.Floor;
        var defaultName = ceiling ? defaultceiling : defaultfloor;
        // an index that opens onto a sky of its own doesn't get the default
        bool skyIndex = (ceiling ? flats.Skies : flats.FloorSkies).ContainsKey(index);
        var name = table.TryGetValue(index, out var listed) && FindFlat(listed) != null ? listed
            : !skyIndex && FindFlat(defaultName) != null ? defaultName : null;
        return name == null ? null : ChangedTexture(x, y, name);
    }

    /// <summary>The name of the texture on a tile's floor or ceiling, for the console.</summary>
    internal string FlatName(int x, int y, bool ceiling)
    {
        int spot = MapSpot(x, y, FlatPlane);
        int index = ceiling ? spot >> 8 : spot & 0xff;
        var name = FlatTextureName(x, y, ceiling);
        var sky = TileSkyName(x, y, ceiling);
        var shown = name != null && FindFlat(name) != null ? name : sky != null ? null : "(color)";
        return $"{index} {shown}{(sky == null ? "" : shown == null ? $"sky {sky}" : $" over sky {sky}")}";
    }

    /*
    =============================================================================

                        TEXTURE CHANGES AND ANIMATION STARTS

    A_ChangeTexture("FROM", "TO") swaps one texture for another on a tile's wall faces, door
    faces and floor and ceiling, by name, whatever walls.yaml, doors.yaml or the flats give it.
    Whenever a tile changes (a switch flips, A_SetWall, A_ChangeTexture), it notes the level tic:
    an animation that plays once (animdefs, ending in stop) starts there on that tile. A looping
    one goes by the level's clock alone, in step everywhere. Both are saved with the level.

    =============================================================================
    */

    // Per tile (TileIndex): the textures changed on it, from -> to
    private Dictionary<int, Dictionary<string, string>> texturechanges = [];

    /// <summary>The level's objectives (game-info objectives) by name: how many are done, of how many. Saved with the level.</summary>
    internal Dictionary<string, ObjectiveCount> ObjectiveCounts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Per tile (<see cref="TileIndex"/>): the damage a destructible wall there has taken toward its
    /// health (Gameplay/DestructibleWalls.cs); tiles that have taken none aren't in it. Saved with the level.
    /// </summary>
    internal Dictionary<int, int> WallDamage = [];

    /// <summary>
    /// The walls crumbling now (Gameplay/DestructibleWalls.cs), in the order they started: each
    /// shows its animation until level tic EndTic, then breaks. Saved with the level.
    /// </summary>
    internal List<CrumblingWall> CrumblingWalls = [];

    /// <summary>Per tile (<see cref="TileIndex"/>): the level tic it last changed, 0 for never.</summary>
    internal int[] AnimStart = new int[MapConstants.ClassicMapArea];

    /// <summary>The texture a tile shows in place of <paramref name="name"/> (itself, unless A_ChangeTexture swapped it).</summary>
    internal string ChangedTexture(int x, int y, string name)
        => texturechanges.Count == 0 || !texturechanges.TryGetValue(TileIndex(x, y), out var changes)
            ? name
            : changes.GetValueOrDefault(name, name);

    /// <summary>Notes a tile changed at level tic <paramref name="tic"/>: an animation that plays once on it starts over.</summary>
    internal void RestartAnimations(int x, int y, int tic) => AnimStart[TileIndex(x, y)] = tic;

    /// <summary>
    /// Makes a tile show <paramref name="to"/> where it showed <paramref name="from"/> (as it is
    /// now, after earlier changes), from level tic <paramref name="tic"/>. Its flats are rebuilt
    /// by the caller (<see cref="RebuildFlats"/>).
    /// </summary>
    internal void ChangeTexture(int x, int y, string from, string to, int tic)
    {
        int i = TileIndex(x, y);
        if (!texturechanges.TryGetValue(i, out var changes))
            texturechanges[i] = changes = new(StringComparer.OrdinalIgnoreCase);

        // What showed as FROM shows as TO now, whether FROM was the original or a change already
        foreach (var original in changes.Where(c => c.Value.Equals(from, StringComparison.OrdinalIgnoreCase)).Select(c => c.Key).ToList())
            changes[original] = to;
        if (!changes.ContainsKey(from))
            changes[from] = to;
        AnimStart[i] = tic;
    }

    /// <summary>Takes back every change on a tile that shows <paramref name="to"/> (a crumbling wall's animation, once it's broken)</summary>
    internal void UndoTextureChanges(int x, int y, string to)
    {
        int i = TileIndex(x, y);
        if (!texturechanges.TryGetValue(i, out var changes))
            return;
        foreach (var from in changes.Where(c => c.Value.Equals(to, StringComparison.OrdinalIgnoreCase)).Select(c => c.Key).ToList())
            changes.Remove(from);
        if (changes.Count == 0)
            texturechanges.Remove(i);
    }

    /// <summary>Rebuilds the flats, after A_ChangeTexture changed some.</summary>
    internal void RebuildFlats() => BuildFlats(defaultfloor, defaultceiling);

    private void ClearTextureChanges()
    {
        texturechanges = [];
        AnimStart = new int[MapArea];
    }

    // Saved after the actors: the tiles that have changed, with when and what
    internal void WriteTextureChanges(BinaryWriter bw)
    {
        var starts = Enumerable.Range(0, MapArea).Where(i => AnimStart[i] != 0).ToList();
        bw.Write(starts.Count);
        foreach (var i in starts)
        {
            bw.Write(i);
            bw.Write(AnimStart[i]);
        }

        bw.Write(texturechanges.Count);
        foreach (var (i, changes) in texturechanges)
        {
            bw.Write(i);
            bw.Write(changes.Count);
            foreach (var (from, to) in changes)
            {
                bw.Write(from);
                bw.Write(to);
            }
        }
    }

    internal static (int[] Starts, Dictionary<int, Dictionary<string, string>> Changes) ReadTextureChanges(BinaryReader br, int mapArea)
    {
        int Tile(BinaryReader br)
        {
            int i = br.ReadInt32();
            if (i < 0 || i >= mapArea)
                throw new InvalidDataException($"Bad tile {i} in the texture changes.");
            return i;
        }

        var starts = new int[mapArea];
        for (int n = br.ReadCount(); n > 0; n--)
            starts[Tile(br)] = br.ReadInt32();

        var changes = new Dictionary<int, Dictionary<string, string>>();
        for (int n = br.ReadCount(); n > 0; n--)
        {
            var tile = changes[Tile(br)] = new(StringComparer.OrdinalIgnoreCase);
            for (int m = br.ReadCount(); m > 0; m--)
                tile[br.ReadString()] = br.ReadString();
        }
        return (starts, changes);
    }

    private readonly HashSet<string> missingflats = new(StringComparer.OrdinalIgnoreCase);

    // A flat is any texture: a VSWAP wall, or a picture in the pk3's textures/ or flats/; or an
    // animation of them (animdefs), returned as the AnimatedAsset when every frame will do
    private Asset? FindFlat(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        static bool Usable(TextureAsset? texture)
            => texture is { Width: > 0, Height: > 0 } && texture.RawData.Length >= texture.Width * texture.Height;

        if (assetManager.Value.FindAnimation<TextureAsset>(name) is { } animation)
        {
            if (animation.Frames.All(frame => Usable(frame.Asset)))
                return animation;
            if (missingflats.Add(name))
                Console.WriteLine($"Flat animation \"{name}\" has a frame that isn't a usable texture");
            return null;
        }

        var texture = assetManager.Value.Find<TextureAsset>(name);
        if (Usable(texture))
            return texture;
        if (missingflats.Add(name))
            Console.WriteLine($"Flat texture \"{name}\" wasn't found");
        return null;
    }

    /// <summary>Sets <see cref="WallShapes"/> from the diagonal markers on the object plane.</summary>
    private void BuildWallShapes()
    {
        var diagonals = GetMapData().Diagonals;
        WallShapes = new WallShape[MapWidth, MapHeight];

        for (int y = 0; y < MapHeight; y++)
            for (int x = 0; x < MapWidth; x++)
                WallShapes[x, y] = IsPlainWall(x, y) && diagonals.TryGetValue(MapSpot(x, y, 1), out var diagonal)
                    ? diagonal.Shape
                    : WallShape.Square;
    }

    /// <summary>
    /// A solid wall tile that can take a shape: not a door, not a moving pushwall, and not open
    /// floor. A wall beside a door (SpawnDoor sets BitWall on it, for the door-side texture) counts.
    /// Plane 0 has to hold a mapdefs wall too: before SpawnDoor runs, a door tile's raw TileMap
    /// value (90-101 in Wolf3D) looks just like a wall id with BitWall set.
    /// </summary>
    internal bool IsPlainWall(int x, int y)
    {
        var tile = TileMap[x, y];
        var spot = MapSpot(x, y, 0);
        return IsWallId(spot) && !GetMapData().Doors.ContainsKey(spot) && GetMapData().Walls.ContainsKey(spot)
            && (tile & GameConstants.BitDoor) == 0 && (tile & ~GameConstants.BitWall) != 0;
    }

    /// <summary>The diagonal marker on this tile, if its shape came from one.</summary>
    internal MapDiagonalTranslation? GetDiagonal(int x, int y) =>
        WallShapes[x, y] != WallShape.Square && GetMapData().Diagonals.TryGetValue(MapSpot(x, y, 1), out var diagonal)
            ? diagonal
            : null;

    /// <summary>
    /// Gives a wall tile a shape by writing the first mapdefs marker for it onto the object plane,
    /// so it's saved with the level like a map-authored one. Square clears the marker. Returns the
    /// marker id written (0 for Square), or null if the mapdefs have no marker for that shape.
    /// </summary>
    internal int? SetWallShape(int x, int y, WallShape shape)
    {
        int marker = 0;
        if (shape != WallShape.Square)
        {
            var match = GetMapData().Diagonals.Where(d => d.Value.Shape == shape).Select(d => (int?)d.Key).Min();
            if (match == null)
                return null;
            marker = match.Value;
        }

        SetMapSpot(x, y, 1, (ushort)marker);
        WallShapes[x, y] = shape;
        return marker;
    }

    /// <summary>
    /// Takes the wall off a tile for good, leaving open floor in <paramref name="area"/> (plane 0),
    /// as a pushwall leaves the tile it slides off (Gameplay/DestructibleWalls.cs joins the areas
    /// round it and sees to what was in it). Its diagonal shape, door-side mark and patch go with
    /// it; its tag, flats and any A_ChangeTexture on its floor stay. With <paramref name="arch"/>,
    /// a wall taller than the player's story only loses up to that one: the rest stays up as an
    /// arch (a block from the story above).
    /// </summary>
    internal void RemoveWall(int x, int y, int area, bool arch)
    {
        // what's above the player's story, of each block
        var (lower, upper) = (BlockSpan(x, y), UpperSpan(x, y));
        (int, int)? Above((int Start, int Stop)? block) =>
            block is var (s, e) && e > Story ? (Math.Max(s, Story + 1), e) : null;
        var kept = new[] { Above(lower), Above(upper) }.Where(b => b != null).Select(b => b!.Value).ToArray();
        if (blockareas.Length > 0)
            blockareas[TileIndex(x, y)] = -1;
        if (WallShapes[x, y] != WallShape.Square)
            SetWallShape(x, y, WallShape.Square);

        TileMap[x, y] = 0;
        if (ActorAt[x, y] is Wall)
            ActorAt[x, y] = null;
        SetMapSpot(x, y, 0, (ushort)(Floors.AreaTile + area));
        PatchMap[x, y] = 0;
        if (arch && kept.Length == 2)
            SetBlocks(x, y, kept[0].Item1, kept[0].Item2, kept[1].Item1, kept[1].Item2);
        else if (arch && kept.Length == 1)
            SetBlock(x, y, kept[0].Item1, kept[0].Item2);
        else
            SetBlock(x, y, 0, 0);
    }

    internal record MapPlayerStart(int TileX, int TileY, int Angle);

    /// <summary>Where the loaded map puts the player (its object-plane player start); null if it has none.</summary>
    internal MapPlayerStart? PlayerStart { get; private set; }

    /// <summary>Whether <see cref="PlayerStart"/> is the start of the entrance the level's entered by (GameState.Entrance)</summary>
    internal bool EntranceStart { get; private set; }

    /// <summary>The map's deathmatch starts (mapdefs player-starts with `deathmatch: true`), if it has any</summary>
    internal List<MapPlayerStart> DeathmatchStarts { get; } = [];

    /// <summary>Each team's starts (mapdefs player-starts with `team: red` or `blue`), red's first</summary>
    internal List<MapPlayerStart>[] TeamStarts { get; } = [[], []];

    /// <summary>Whether the map's enemies are left out as it loads (a deathmatch without monsters)</summary>
    internal bool NoMonsters { get; set; }

    /// <summary>The trigger on this tile, if its object-plane tile is one in the mapdefs' triggers.</summary>
    internal MapTriggerTranslation? GetTrigger(int x, int y) =>
        GetMapData().Triggers.TryGetValue(MapSpot(x, y, 1), out var trigger) ? trigger : null;

    public void SpawnThing(int tilex, int tiley, string className, int story = 0) =>
        SpawnThing(tilex, tiley, new MapActorTranslation { Class = className }, story: story);

    /// <param name="countKill">
    /// Whether a killable enemy adds to the level's kill total: not for one appearing mid-level
    /// out of another that already counted for it (an alien out of its canister)
    /// </param>
    /// <param name="story">The story it stands on (1 to 8); 0 for its tile's (<see cref="ThingStory(int, int)"/>)</param>
    public Entities.Actors.Actor? SpawnThing(int tilex, int tiley, MapActorTranslation thing, bool countKill = true, int story = 0)
    {
        // MinSkill gates enemy availability by skill (its place in game-info's skills, as the
        // legacy ScanInfoPlane's gd_medium/gd_hard checks per tile-number range); always 0 for
        // decorations/pickups, so this is a no-op there.
        if (thing.MinSkill > _difficulty)
            return string.IsNullOrEmpty(thing.Else) ? null
                : SpawnThing(tilex, tiley, thing with { Class = thing.Else, MinSkill = 0, Else = "" }, countKill, story);

        var actorMetaData = assetManager.Value.GetActorMetadata();

        if (!actorMetaData.Actors.TryGetValue(thing.Class, out var actor))
            return null;
        // A monster blocker or a dock only marks its tile (ReadMonsterBlocks); there's nothing to spawn
        if (MarksTileOnly(actorMetaData, thing.Class))
            return null;
        var builtActor = Joining(actorMetaData.CreateActor(thing.Class, actor)); // TODO: Should this just create objects?
        if (builtActor == null)
            return null;

        // A random spawner (`spawn.random: [A, B]`) is one of its classes, picked at random
        // (Blake Stone's bio-techs: half of them informants)
        if (builtActor.PropertyStrings("spawn.random") is { Count: > 0 } choices)
        {
            var choice = choices[session.Random.RndT() % choices.Count];
            if (!actorMetaData.Actors.TryGetValue(choice, out var chosen) || Joining(actorMetaData.CreateActor(choice, chosen)) is not { } chosenActor)
            {
                Console.WriteLine($"{thing.Class}'s spawn.random names \"{choice}\", which isn't an actor");
                return null;
            }
            builtActor = chosenActor;
        }

        // A deathmatch without monsters (Gameplay/Deathmatch.cs) leaves the map's enemies out
        if (NoMonsters && builtActor is Entities.Actors.Monster)
            return null;

        builtActor.Faction = factionManager.Value.SpawnFaction(builtActor, thing.Faction);
        builtActor.Translation = TranslationManager.SpawnTranslation(builtActor, thing.Translation);
        builtActor.SetPosition(tilex, tiley);

        // SpawnNewObj (WL_STATE.C) always set areanumber from the spawn tile for
        // every actor, not just ones on an ambush tile -- without this, AreaNumber sits at
        // its byte default (0, a real area index, not an "unset" sentinel) until the actor
        // first moves through TryWalk, which recomputes it correctly. Until then, anything
        // gated on AreaNumber (SightPlayer's AreaByPlayer connectivity check, T_Shoot/
        // CheckSight's area check) reads the wrong area, e.g. a stationary/ambushed actor
        // that never patrols would never notice the player if area 0 isn't connected.
        builtActor.AreaNumber = SpawnArea(tilex, tiley);
        builtActor.Tag = GetTag(tilex, tiley);
        if (story is < 1 or > GameConstants.MaxWallStories)
            story = ThingStory(tilex, tiley);
        builtActor.Story = story == Story ? 0 : story;     // 0: on the player's

        // Angles: 0=east, 45=northeast, 90=north ... 315=southeast, in ObjDir order
        // (enemies only face the four cardinal ones, patrol points all eight); -1 faces no way
        // at all, so it sees all round (Blake Stone's aliens)
        builtActor.Dir = thing.Angles < 0 ? ObjDir.NoDir : (ObjDir)((thing.Angles / 45 % 8 + 8) % 8);

        // Patrol selects the initial resolved state (mirrors SpawnStand vs SpawnPatrol):
        // "Path" for patrolling grunts, otherwise whatever CreateActor already set ("Spawn").
        if (thing.Patrol != 0 && builtActor.ResolvedStates.TryGetValue("Path", out var pathState))
        {
            builtActor.ArmState(pathState);
            builtActor.Distance = (int)MapConstants.TileGlobal;
            builtActor.Active = ActiveType.Yes;     // a patroller walks wherever the player is

            // As SpawnPatrol did: it's already on its way to the next tile, which is its tile from
            // now on (it leaves the spawn tile's centre), or reaching it would snap it back a tile
            switch (builtActor.Dir)
            {
                case ObjDir.East: builtActor.TileX++; break;
                case ObjDir.North: builtActor.TileY--; break;
                case ObjDir.West: builtActor.TileX--; break;
                case ObjDir.South: builtActor.TileY++; break;
            }
            builtActor.SyncPosition();
        }

        // AMBUSH actors (the bosses, the Pac-Man ghosts) are always spawned ambush-ready,
        // whatever the floor tile beneath them, unlike grunts, which only ambush when placed
        // on an ambush tile.
        var alwaysAmbush = builtActor.HasFlag("AMBUSH");
        var hasHealth = builtActor.Properties.Keys.Any(Definitions.ActorProperties.IsHealthKey);

        if (!hasHealth && builtActor.Properties.ContainsKey("speed"))
        {
            // A mover with no health (the Pac-Man ghosts, matching SpawnGhosts never assigning
            // hitpoints): never shootable, and not counted toward the kill ratio.
            builtActor.Speed = ReadIntProperty(builtActor, "speed", GameConstants.SpdDog);
            if (alwaysAmbush)
                builtActor.RuntimeFlags |= ObjFlags.Ambush;
        }
        else if (hasHealth)
        {
            // A grunt or boss enemy (has scaled health), as opposed to a plain decoration/pickup.
            builtActor.Hitpoints = GetScaledHealth(builtActor);
            builtActor.Speed = ReadIntProperty(builtActor, "speed", GameConstants.SpdPatrol);
            builtActor.RuntimeFlags |= ObjFlags.Shootable;

            // Every killable enemy counts toward the level's kill ratio (the old SpawnStand/
            // SpawnPatrol/boss spawners each did this); ghosts take the branch above and don't,
            // and nor do those that aren't kills (Monster.IsKill: NOTCOUNTED ones, informants)
            if (countKill && !session.LoadedGame && (builtActor is not Entities.Actors.Monster m || m.CountsAsKill))
            {
                session.GameState.KillTotal++;
                session.GameState.PointsTotal += KillWorth(builtActor);
            }

            // Points only for the first kill (Monster.Kill clears it)
            if (builtActor.HasFlag("POINTSONCE"))
                builtActor.RuntimeFlags |= ObjFlags.Bonus;

            if (alwaysAmbush)
            {
                builtActor.RuntimeFlags |= ObjFlags.Ambush;

                // The legacy boss spawners (SpawnGift, SpawnBoss, ...) all face nodir, which
                // CheckSight takes as seeing all around; a map facing would blind them to a
                // player approaching from behind.
                builtActor.Dir = ObjDir.NoDir;
            }
            else
            {
                // Grunts only ambush if placed directly on an ambush floor tile (SpawnStand
                // in WL_ACT2.C), unlike AMBUSH actors, which always are.
                var floorTile = MapSpot(tilex, tiley, 0);
                if (floorTile == Floors.AmbushTile)
                {
                    TakeNeighbourArea(builtActor, tilex, tiley);
                    builtActor.RuntimeFlags |= ObjFlags.Ambush;
                }
            }

            // A floors actor-code under it (Planet Strike's): what it carries, its cloak, its link
            if (Floors.ActorCodes.TryGetValue(MapSpot(tilex, tiley, 0), out var code))
            {
                if (!string.IsNullOrEmpty(code.Drop))
                    builtActor.CarriedDrops.Add(code.Drop);
                builtActor.Cloaked |= code.Cloak;
                if (code.Ambush)
                    builtActor.RuntimeFlags |= ObjFlags.Ambush;
                if (code.Link.Equals("east", StringComparison.OrdinalIgnoreCase) && tilex + 1 < MapWidth)
                {
                    builtActor.DeathLink = (ushort)MapSpot(tilex + 1, tiley, 1);
                    SetMapSpot(tilex + 1, tiley, 1, 0);     // a tile, not a thing
                }
                TakeNeighbourArea(builtActor, tilex, tiley);
            }

            (builtActor as Entities.Actors.Monster)?.OnSpawned(tilex, tiley);
        }

        // An enemy starts a random way into its first frame (patrollers out of step), as
        // SpawnNewObj did; these draws come before play starts, so demos rely on them too
        if (builtActor is Entities.Actors.Monster spawnedMonster)
        {
            builtActor.TicCount = SpawnTicCount(builtActor.CurrentState);
            MarkActorTile(spawnedMonster);      // a patroller's on the tile it's heading for
        }

        // Treasure (ScoreItem and the 1-up) counts toward the level's treasure ratio; GetBonus
        // (Gameplay/Pickups.cs) bumps TreasureCount on pickup off the same flag.
        if (!session.LoadedGame && builtActor.HasFlag("COUNTITEM"))
        {
            session.GameState.TreasureTotal++;
            session.GameState.PointsTotal += ItemWorth(builtActor);
        }

        //if (builtActor.Properties.Keys.Any(x => x.StartsWith("inventory")))
        //{
        //    newstatobj.flags = ObjFlags.Bonus;
        //}

        // Solid statics (barrels, pillars, tables, ...; `flags: [SOLID]` in actordefs, inherited
        // through `parent:`) make their whole tile impassable to the player, enemies and
        // projectiles, as the original's `block` statics did. The blocking marker is the same
        // "something is here" entry walls and doors use, so every ActorAt[,] check sees it.
        // On another story, its story's grid takes it (StoryGrid).
        MarkSolidThing(builtActor, tilex, tiley);

        _actors.AddLast(builtActor);
        ActorAdded?.Invoke(builtActor);
        return builtActor;
    }

    // A floor code that isn't an area (ambush, actor-codes) under an actor becomes a neighbouring
    // tile's area code, which the actor is then in (SpawnStand's ambush handling)
    private void TakeNeighbourArea(Entities.Actors.Actor actor, int tilex, int tiley) =>
        actor.AreaNumber = (byte)(ClearFloorCode(tilex, tiley) - Floors.AreaTile);

    /// <summary>Turns a floor code that isn't an area into a neighbouring tile's area code, which it returns.</summary>
    internal int ClearFloorCode(int tilex, int tiley)
    {
        var floorTile = MapSpot(tilex, tiley, 0);
        if (ValidArea(MapSpot(tilex + 1, tiley, 0)))
            floorTile = MapSpot(tilex + 1, tiley, 0);
        if (ValidArea(MapSpot(tilex, tiley - 1, 0)))
            floorTile = MapSpot(tilex, tiley - 1, 0);
        if (ValidArea(MapSpot(tilex, tiley + 1, 0)))
            floorTile = MapSpot(tilex, tiley + 1, 0);
        if (ValidArea(MapSpot(tilex - 1, tiley, 0)))
            floorTile = MapSpot(tilex - 1, tiley, 0);

        SetMapSpot(tilex, tiley, 0, (ushort)floorTile);
        return floorTile;
    }

    private static int ReadIntProperty(Entities.Actors.Actor actor, string key, int fallback) =>
        actor.Properties.TryGetValue(key, out var value) ? Convert.ToInt32(value) : fallback;

    /// <summary>
    /// SpawnNewObj's first countdown for a new actor's frame: a random part of the frame's tics
    /// (none for a frame that holds). Demos keep v1.4's 0 to tics-1, which can end the frame on
    /// the first tic; otherwise 1 to tics, Wolf4SDL's fix for that (Chris' "moonwalk" fix).
    /// </summary>
    /// <summary>
    /// The points a counted enemy adds to the level's points total: its `points`, or for a
    /// sleeper the enemy it becomes (`monster.becomes`), which is the kill (bstone's total_points)
    /// </summary>
    internal int KillWorth(Entities.Actors.Actor actor)
    {
        if (actor.Properties.TryGetValue("monster.becomes", out var becomes) && becomes?.ToString() is { Length: > 0 } className
            && assetManager.Value.GetActorMetadata().Actors.TryGetValue(className, out var data)
            && data.Properties.TryGetValue("points", out var worth))
            return Convert.ToInt32(worth);
        return actor.Properties.TryGetValue("points", out var points) ? Convert.ToInt32(points) : 0;
    }

    /// <summary>The points a counted treasure item adds to the level's points total: what picking it up gives</summary>
    internal static int ItemWorth(Entities.Actors.Actor item) =>
        item is Entities.Actors.ScoreItem && item.Properties.TryGetValue("inventory.amount", out var amount) ? Convert.ToInt32(amount) : 0;

    internal short SpawnTicCount(Entities.Actors.ActorStateFrame? frame)
    {
        if (frame == null || frame.HoldsForever || frame.TicTime <= 0)
            return (short)Math.Max(frame?.TicTime ?? 0, (short)0);

        var count = session.Random.RndT() % frame.TicTime;
        return (short)(session.DemoRecord || session.DemoPlayback ? count : count + 1);
    }

    // Runtime enemy-to-enemy morph (A_Morph, Monster.Morph): spawns a new enemy already in its
    // Chase state at the dying source actor's exact position/facing, rather than going through
    // the tile/mapdefs-driven SpawnThing path.
    internal Entities.Actors.Actor? SpawnMorphedEnemy(string className, Entities.Actors.Actor source)
    {
        var actorMetaData = assetManager.Value.GetActorMetadata();
        if (!actorMetaData.Actors.TryGetValue(className, out var actor))
            return null;

        var builtActor = Joining(actorMetaData.CreateActor(className, actor));
        if (builtActor == null)
            return null;

        builtActor.SetPosition(source.TileX, source.TileY);
        builtActor.X = source.X;
        builtActor.Y = source.Y;
        builtActor.Distance = source.Distance;
        builtActor.Dir = source.Dir;
        builtActor.AreaNumber = source.AreaNumber;
        builtActor.Story = source.Story;
        // Hitler stuck-with-nodir fix (vanilla's A_HitlerMorph): the morphed
        // actor must remain markable even if the dying source had NonMark set.
        builtActor.RuntimeFlags = (source.RuntimeFlags & ~ObjFlags.NonMark) | ObjFlags.Shootable;
        builtActor.Hitpoints = GetScaledHealth(builtActor);
        builtActor.Faction = factionManager.Value.InheritedFaction(builtActor, source);
        builtActor.Translation = TranslationManager.ClassTranslation(builtActor) ?? source.Translation;    // Hitler keeps Mecha Hitler's colors

        if (builtActor.ResolvedStates.TryGetValue("Chase", out var chaseState))
        {
            builtActor.ArmState(chaseState);
            builtActor.TicCount = SpawnTicCount(chaseState);
        }
        if (builtActor is Entities.Actors.Monster morphed)
            MarkActorTile(morphed);

        _actors.AddLast(builtActor);
        ActorAdded?.Invoke(builtActor);
        return builtActor;
    }

    // Projectiles and their smoke trail spawn every few tics, and AssetManager.GetActorMetadata
    // rebuilds its result on every call, so keep one for the runtime spawners below.
    private ActorMetadata? _runtimeActorMetadata;

    /// <summary>
    /// Spawns a runtime projectile or effect (Rocket, Smoke, Needle, Fire --
    /// actordefs/wolf3d/projectiles.yaml) at another actor's exact fixed-point position. It is
    /// an "active" actor (free-moving, drawn through the exact-position path, never marked in
    /// ActorAt); callers set Angle/Speed.
    /// </summary>
    internal Entities.Actors.Actor? SpawnAtActor(string className, Entities.Actors.Actor source)
    {
        var builtActor = CreateRuntimeActor(className);
        if (builtActor == null)
            return null;

        builtActor.SetPosition(source.TileX, source.TileY);
        builtActor.X = source.X;
        builtActor.Y = source.Y;
        builtActor.Dir = ObjDir.NoDir;
        builtActor.Active = ActiveType.Yes;
        builtActor.RuntimeFlags = ObjFlags.NeverMark;
        builtActor.Faction = factionManager.Value.InheritedFaction(builtActor, source);
        builtActor.Translation = TranslationManager.ClassTranslation(builtActor);

        _actors.AddLast(builtActor);
        ActorAdded?.Invoke(builtActor);
        return builtActor;
    }

    /// <summary>
    /// An effect of the class at a point (global coordinates), as SpawnAtActor puts one at an
    /// actor (a shot's puff where it struck), taking its side from that actor; null if there's no
    /// such class
    /// </summary>
    internal Entities.Actors.Actor? SpawnAt(string className, int x, int y, Entities.Actors.Actor source)
    {
        var spawned = SpawnAtActor(className, source);
        if (spawned == null)
            return null;
        spawned.SetPosition(x >> MapConstants.TileShift, y >> MapConstants.TileShift);
        spawned.X = x;
        spawned.Y = y;
        return spawned;
    }

    /// <summary>
    /// A_Replace: the actor becomes one of another class where it stands, facing the same way
    /// with its tag; what its tile's blocking was goes with it, and the new one blocks as its
    /// class does (a closed gate opened). An item the level's total counted (COUNTITEM) is taken
    /// off it, as it can no longer be had (a dog tag found before, greyed out), and the new one
    /// counted if it's one. Null, and nothing changed, if there's no such class.
    /// </summary>
    internal Entities.Actors.Actor? ReplaceActor(Entities.Actors.Actor old, string className)
    {
        if (!IsActorClass(className))
            return null;
        int x = old.TileX, y = old.TileY;
        MarkForRemoval(old);
        if (!session.LoadedGame && old.HasFlag("COUNTITEM") && session.GameState.TreasureTotal > 0)
        {
            session.GameState.TreasureTotal--;
            session.GameState.PointsTotal = Math.Max(session.GameState.PointsTotal - ItemWorth(old), 0);
        }
        var grid = GridOf(old);
        if (grid[x, y] is BlockingActor || grid[x, y] is ActorMark mark && ReferenceEquals(mark.Who, old))
            grid[x, y] = null;
        int angles = old.Dir == ObjDir.NoDir ? -1 : (int)old.Dir * 45;
        var replaced = SpawnThing(x, y, new MapActorTranslation { Class = className, Angles = angles }, countKill: false,
            story: StoryOf(old));
        if (replaced != null)
            replaced.Tag = old.Tag;
        return replaced;
    }

    private ActorMetadata RuntimeActorMetadata => _runtimeActorMetadata ??= assetManager.Value.GetActorMetadata();

    /// <summary>Whether there's an actordefs class of that name</summary>
    internal bool IsActorClass(string className) => RuntimeActorMetadata.Actors.ContainsKey(className);

    /// <summary>Builds an actordefs class in its Spawn state, without placing it or adding it to _actors.</summary>
    private Entities.Actors.Actor? CreateRuntimeActor(string className) =>
        RuntimeActorMetadata.Actors.TryGetValue(className, out var actor)
            ? Joining(RuntimeActorMetadata.CreateActor(className, actor))
            : null;

    // What the actors use of the game (see actorWorld)
    internal Entities.Actors.ActorWorld World => actorWorld?.Value!;

    // An actor made for this level: it uses the game through the world
    private Entities.Actors.Actor Joining(Entities.Actors.Actor actor)
    {
        actor.World = World;
        return actor;
    }

    // Skill-scaled health: the property the skill's enemy-health names (e.g. "health.normal"),
    // for enemies whose hitpoints vary by skill (vanilla's starthitpoints table), or a flat
    // "health" for the rest.
    internal short GetScaledHealth(Entities.Actors.Actor actor)
    {
        if (!string.IsNullOrEmpty(_enemyHealthKey) && actor.Properties.TryGetValue(_enemyHealthKey, out var scaled))
            return (short)Convert.ToInt32(scaled);

        return actor.Properties.TryGetValue("health", out var flat) ? (short)Convert.ToInt32(flat) : (short)0;
    }

    /// <summary>
    /// The health bars to show (statusbar.yaml enemy-health): the health left and full health of
    /// each living enemy whose class has `healthbar: true` and that has noticed someone, in the
    /// level's order (Midnight Assault's tank and commanders)
    /// </summary>
    internal IEnumerable<(int Left, int Full)> HealthBars() =>
        _actors.OfType<Entities.Actors.Monster>()
            .Where(m => !m.IsRemoved && m.Hitpoints > 0 && m.RuntimeFlags.Has(ObjFlags.Shootable | ObjFlags.AttackMode)
                && m.Properties.TryGetValue("healthbar", out var bar) && (bar is true || bool.TryParse(bar?.ToString(), out var b) && b))
            .Select(m => ((int)m.Hitpoints, (int)GetScaledHealth(m)));

    internal bool ValidArea(int x) => (x) >= Floors.AreaTile && (x) < (Floors.AreaTile + Floors.NumAreas);

    /// <summary>The area on a tile, or -1 for none (off the map, or a floor code that isn't an area)</summary>
    internal int AreaAt(int x, int y)
    {
        if (!InMap(x, y))
            return -1;
        int spot = FloorCode(x, y);
        return ValidArea(spot) ? spot - Floors.AreaTile : -1;
    }

    internal int MapSpot(int x, int y, int plane) => mapsegs[plane][y * MapWidth + x];
    internal void SetMapSpot(int x, int y, int plane, ushort value)
    {
        mapsegs[plane][y * MapWidth + x] = value;
    }

    // A game with no mapdefs (a stand-alone game whose mapdefs/ were taken out): its levels are
    // open floor, sealed at the edge, with the player in the middle
    private static readonly MapObjectTranslationAsset NoMapData = new();

    // The loaded level's mapdefs with its own definitions over them (MapAsset.LocalDefs), or null
    // when it has none and the game's are used as they are
    private MapObjectTranslationAsset? _levelMapData;

    /// <summary>What the loaded level's file says about it (sky, wall height), under its game-info entry</summary>
    internal MapLevelDefaults LevelDefaults { get; private set; } = new();

    /// <summary>The mapdefs the loaded level is read through: the game's, with the level's own over them</summary>
    internal MapObjectTranslationAsset GetMapData() => _levelMapData ?? PackMapData();

    private MapObjectTranslationAsset PackMapData()
        => assetManager.Value.FindInGamePackIfAny<MapObjectTranslationAsset>("mapdefs") ?? NoMapData;

    internal void RemoveActor(Entities.Actors.Inventory builtActor)
    {
        _actors.Remove(builtActor);
    }

    internal LinkedList<Entities.Actors.Actor> GetActors()
    {
        return _actors;
    }

    // ActorAt[,] only tracks walls and doors; the questions it used to answer about actors
    // ("is something standing on that tile?") are answered from _actors instead. An actor
    // occupies its TileX/TileY, which -- as in the original -- moves to the destination tile the
    // instant a step begins.

    /// <summary>Enemies are the actors with a "Chase" state (guards, dogs, bosses, ghosts).</summary>
    internal static bool IsEnemy(Entities.Actors.Actor actor) => actor.ResolvedStates.ContainsKey("Chase");

    /// <summary>The patrol point (a PATROLPOINT actor, e.g. an arrow) on a tile, if there is one.</summary>
    internal Entities.Actors.Actor? PatrolPointAt(int tilex, int tiley)
    {
        foreach (var actor in _actors)
        {
            if (!actor.IsRemoved && actor.TileX == tilex && actor.TileY == tiley
                && actor.HasFlag("PATROLPOINT"))
                return actor;
        }
        return null;
    }

    /// <summary>
    /// The enemies on a tile, living or dead: corpses keep occupying their tile, which is what
    /// stops a door closing or a pushwall sliding onto them.
    /// </summary>
    internal IEnumerable<Entities.Actors.Actor> EnemiesAt(int tilex, int tiley)
    {
        foreach (var actor in _actors)
        {
            if (!actor.IsRemoved && actor.TileX == tilex && actor.TileY == tiley && IsEnemy(actor))
                yield return actor;
        }
    }

    /// <summary>True if a living (Shootable) actor occupies the tile -- corpses don't count.</summary>
    /// <summary>The living (Shootable) actors on a tile</summary>
    internal IEnumerable<Entities.Actors.Monster> ShootableActorsAt(int tilex, int tiley) =>
        _actors.OfType<Entities.Actors.Monster>().Where(a => !a.IsRemoved && a.TileX == tilex && a.TileY == tiley && a.RuntimeFlags.Has(ObjFlags.Shootable));

    /// <summary>The living (Shootable) actors within <paramref name="reach"/> (global units, each way) of a point, nearest first</summary>
    internal IEnumerable<Entities.Actors.Monster> ShootableActorsNear(int x, int y, long reach) =>
        _actors.OfType<Entities.Actors.Monster>().Where(a => !a.IsRemoved && a.RuntimeFlags.Has(ObjFlags.Shootable)
                && Math.Abs((long)a.X - x) <= reach && Math.Abs((long)a.Y - y) <= reach)
            .OrderBy(a => Math.Max(Math.Abs((long)a.X - x), Math.Abs((long)a.Y - y)));

    /// <summary>The dead (no longer Shootable) enemies within <paramref name="reach"/> (global units, each way) of a point, nearest first</summary>
    internal IEnumerable<Entities.Actors.Monster> CorpsesNear(int x, int y, long reach) =>
        _actors.OfType<Entities.Actors.Monster>().Where(a => !a.IsRemoved && !a.RuntimeFlags.Has(ObjFlags.Shootable)
                && Math.Abs((long)a.X - x) <= reach && Math.Abs((long)a.Y - y) <= reach)
            .OrderBy(a => Math.Max(Math.Abs((long)a.X - x), Math.Abs((long)a.Y - y)));

    /// <summary>True if a living (Shootable) actor that's in the way occupies the tile</summary>
    internal bool IsShootableActorAt(int tilex, int tiley, int story = 0)
    {
        if (story == 0)
            story = Story;          // the player's
        foreach (var actor in _actors)
        {
            if (!actor.IsRemoved && actor.TileX == tilex && actor.TileY == tiley && IsSolidActor(actor) && StoryOf(actor) == story)
                return true;
        }

        return false;
    }

    /// <summary>
    /// A living actor in the way of the player and other actors: shootable, and not NOTSOLID
    /// (Blake Stone's hanging turrets and electro-spheres, which things pass under or through)
    /// </summary>
    internal static bool IsSolidActor(Entities.Actors.Actor actor) =>
        actor.RuntimeFlags.Has(ObjFlags.Shootable) && !actor.HasFlag("NOTSOLID");

    /// <summary>
    /// Creates a fresh pawn for a player, replacing any they had, at the head of _actors after
    /// the players before them. Head placement keeps the players thinking ahead of every other
    /// actor, as the legacy InitActorList did by allocating the player first.
    /// </summary>
    internal Entities.Actors.PlayerPawn CreatePlayer(Entities.PlayerState state)
    {
        if (state.Pawn != null)
            _actors.Remove(state.Pawn);

        var pawn = new Entities.Actors.PlayerPawn { State = state, World = World };
        pawn.Faction = factionManager.Value.PlayerFaction(state.PlayerClass);
        state.Pawn = pawn;
        PlacePawn(pawn);
        return pawn;
    }

    // Puts a pawn at the head of _actors, behind the players before it
    private void PlacePawn(Entities.Actors.PlayerPawn pawn)
    {
        var after = _actors.First;
        LinkedListNode<Entities.Actors.Actor>? last = null;
        while (after != null && after.Value is Entities.Actors.PlayerPawn other && other.State.Number < pawn.State.Number)
        {
            last = after;
            after = after.Next;
        }
        if (last == null)
            _actors.AddFirst(pawn);
        else
            _actors.AddAfter(last, pawn);
    }

    /// <summary>
    /// The living enemies (shootable Monsters on a side), as of the start of this frame: who
    /// else an enemy could go after besides the players, when <see cref="HasRivalries"/>
    /// </summary>
    internal List<Entities.Actors.Monster> Combatants { get; } = [];

    /// <summary>
    /// Whether, this frame, any of the enemies is hostile to another (factions.yaml): only then
    /// do enemies look for each other. Without, they go after the players alone, as they always have.
    /// </summary>
    internal bool HasRivalries { get; private set; }

    // Lists the frame's combatants, and whether any side among them is hostile to another
    private void FindCombatants()
    {
        Combatants.Clear();
        var sides = _combatantSides;
        sides.Clear();
        foreach (var actor in _actors)
        {
            if (actor is Entities.Actors.Monster monster && !actor.IsRemoved && actor.Faction != null
                && actor.RuntimeFlags.Has(ObjFlags.Shootable))
            {
                Combatants.Add(monster);
                sides.Add(actor.Faction);
            }
        }

        HasRivalries = sides.Count > 1 && AnyRivals(sides);
    }

    // The sides FindCombatants found, kept to be filled again each frame
    private readonly HashSet<string> _combatantSides = new(StringComparer.OrdinalIgnoreCase);

    // Apart from FindCombatants, so the lambdas' closure is only made with two sides or more
    private bool AnyRivals(HashSet<string> sides)
    {
        var table = factionManager.Value.Table;
        return sides.Any(side => sides.Any(other => table.IsHostile(side, other)));
    }

    internal void DoActors(uint tics)
    {
        FindCombatants();

        var node = _actors.First;
        while (node != null)
        {
            // A player thinks as the acting player: the game's player code is then theirs
            if (node.Value is Entities.Actors.PlayerPawn pawn)
            {
                using (session.ActAs(pawn.State))
                    DoActor(pawn, tics);
            }
            else
                DoActor(node.Value, tics);

            // Read Next only after the actor has run, so anything it appended (a rocket's smoke,
            // a thrown projectile) is still visited this frame -- but before unlinking, since
            // removing a node clears its own Next.
            var next = node.Next;
            if (node.Value.IsRemoved)
                _actors.Remove(node);
            node = next;
        }
    }

    /// <summary>
    /// Flags an actor to be dropped from _actors once its current tic finishes. Deferred rather
    /// than immediate so an actor can safely remove itself from inside its own Think/Action.
    /// </summary>
    internal void MarkForRemoval(Entities.Actors.Actor actor) => actor.IsRemoved = true;

    internal void DoActor(Entities.Actors.Actor ob, uint tics)
    {
        var state = ob.CurrentState;
        if (state == null || ob.IsRemoved)
            return;

        // As the original: an enemy that has never been on screen (nor patrols) does nothing
        // while its area isn't connected to the player's (or seen from it through a see-through door)
        if (ob is Entities.Actors.Monster { SleepsOutOfReach: true } && ob.Active == ActiveType.No
            && ob.AreaNumber < Floors.NumAreas && session.Areas.AreaSeenByPlayer[ob.AreaNumber] == 0)
            return;

        // It's lifted off its tile while it thinks, and put back on the one it ends up on
        bool marks = ob is Entities.Actors.Monster && (ob.RuntimeFlags & (ObjFlags.NonMark | ObjFlags.NeverMark)) == 0;
        if (marks)
            UnmarkActorTile((Entities.Actors.Monster)ob);

        // Mirrors vanilla DoActor (WL_PLAY.C): a frame that holds forever (-1) only
        // runs its Think each tic; Next is never consulted again. So does one with no countdown
        // at all, as the original's TicCount 0: a demo's spawn can leave a patroller that way
        // (SpawnTicCount), walking on its first frame until it changes state.
        if (!state.HoldsForever && !(ob.TicCount == 0 && state.TicTime > 0))
        {
            ob.TicCount -= (short)tics;
            ob.AdvanceFrames();
            if (ob.IsRemoved)
                return;
            state = ob.CurrentState ?? state;
        }

        // An enemy after someone checks it still has someone to be after (and, with rivals
        // about, whether someone else is nearer)
        if (ob is Entities.Actors.Monster fighter && fighter.BeforeThink(tics))
            state = ob.CurrentState ?? state;

        // An ally following the players walks after them in place of its think
        if (ob is not Entities.Actors.Monster follower || !follower.ThinkInstead(tics))
            Entities.Actors.ActorActionRegistry.Invoke(state.Think, ob);

        if (ob is Entities.Actors.Monster monster && !ob.IsRemoved)
            MarkActorTile(monster);
    }

    /*
    The original kept each enemy in ActorAt[] too, on the tile it was on as of its last think:
    it overwrote a door's entry there (so a door can't close on it) and a door opening all the
    way cleared it. Other enemies' moves (Monster.CheckSide, CheckDiag), the player's
    (TryMove), and doors closing all read it from there, which demos depend on.
    */

    /// <summary>Puts a living or dead enemy in ActorAt[] where it stands, as DoActor did after its think</summary>
    internal void MarkActorTile(Entities.Actors.Monster ob)
    {
        if (ob.RuntimeFlags.Has(ObjFlags.NeverMark))
            return;

        bool ours = OnPlayersStory(ob);
        if (!ours && StorySolid(ob.TileX, ob.TileY, ob.Story))
            return;                 // inside a block on its story
        ref var spot = ref (ours ? ActorAt : StoryGrid(ob.Story))[ob.TileX, ob.TileY];
        if (spot is Wall or BlockingActor)
            return;                 // never on a wall or a solid thing (or a wall sprite's tile)
        if (ob.RuntimeFlags.Has(ObjFlags.NonMark) && spot != null)
            return;                 // a corpse only takes an empty tile
        spot = ob.Mark;
    }

    /// <summary>Clears an enemy's (or a door's) entry from ActorAt[], as DoActor and KillActor did</summary>
    internal void UnmarkActorTile(int tilex, int tiley)
    {
        if (ActorAt[tilex, tiley] is ActorMark or Door)
            ActorAt[tilex, tiley] = null;
    }

    /// <summary>
    /// Clears the entry on an enemy's tile, on its own story: <see cref="UnmarkActorTile(int, int)"/>
    /// on the player's, else whoever's marked there on its story's grid
    /// </summary>
    internal void UnmarkActorTile(Entities.Actors.Monster ob)
    {
        if (OnPlayersStory(ob))
            UnmarkActorTile(ob.TileX, ob.TileY);
        else if (storygrids.TryGetValue(ob.Story, out var grid) && grid[ob.TileX, ob.TileY] is ActorMark)
            grid[ob.TileX, ob.TileY] = null;
    }

    /// <summary>The enemy marked on a tile (MarkActorTile), if any</summary>
    internal Entities.Actors.Monster? ActorMarkAt(int tilex, int tiley) => (ActorAt[tilex, tiley] as ActorMark)?.Who;

    /*
    =============================================================================

                                OTHER STORIES

    ActorAt[] is the player's story's: its walls (TileMap), doors, solid things and enemies. Each
    other story that has a thing on it gets a grid of its own for its solid things and enemies
    (StoryGrid), and what's solid on it comes from the tiles' blocks (StorySolid): a block that
    takes the story in, a door's tile (its frame below, its lintel above: doors work on the
    player's story only), a moving pushwall's, and the map's edge. ActorOn puts the two together
    as ActorAt has it, so an enemy on another story walks its own floor with the same checks.

    =============================================================================
    */

    // Each other story's solid things and enemies, by story (made when one's needed)
    private readonly Dictionary<int, Actor?[,]> storygrids = [];

    // What ActorOn gives for a tile solid on another story
    private static readonly Wall StoryWall = new();

    /// <summary>The grid of a story other than the player's (made on first use)</summary>
    internal Actor?[,] StoryGrid(int story)
    {
        if (!storygrids.TryGetValue(story, out var grid) || grid.GetLength(0) != MapWidth || grid.GetLength(1) != MapHeight)
            storygrids[story] = grid = new Actor?[MapWidth, MapHeight];
        return grid;
    }

    /// <summary>The grid an actor's marked on: ActorAt[] on the player's story, else its story's (<see cref="StoryGrid"/>)</summary>
    internal Actor?[,] GridOf(Entities.Actors.Actor actor) => OnPlayersStory(actor) ? ActorAt : StoryGrid(actor.Story);

    /// <summary>
    /// Whether a tile is solid on a story: on the player's, TileMap; on another, one of its blocks
    /// takes the story in, or it's a door's or a moving pushwall's tile, or on the map's edge
    /// </summary>
    internal bool StorySolid(int x, int y, int story)
    {
        if (story == 0 || story == Story)
            return TileMap[x, y] != 0;
        if (OnEdge(x, y) || (TileMap[x, y] & GameConstants.BitDoor) != 0 || TileMap[x, y] == GameConstants.BitWall)
            return true;
        int i = TileIndex(x, y);
        return (uint)i < (uint)SpanStop.Length
            && (SpanStop[i] != 0 && SpanStart[i] <= story && story <= SpanStop[i]
                || UpperSpanStop[i] != 0 && UpperSpanStart[i] <= story && story <= UpperSpanStop[i]);
    }

    /// <summary>
    /// What's on a tile on a story (0: the player's), as ActorAt[] has it on the player's: a Wall
    /// where it's solid, else a solid thing or an enemy marked there, else null
    /// </summary>
    internal Actor? ActorOn(int x, int y, int story)
    {
        if (story == 0 || story == Story)
            return ActorAt[x, y];
        if (StorySolid(x, y, story))
            return StoryWall;
        return storygrids.TryGetValue(story, out var grid) && x < grid.GetLength(0) && y < grid.GetLength(1) ? grid[x, y] : null;
    }

    /// <summary>What's on a tile on an actor's story, as <see cref="ActorOn"/></summary>
    internal Actor? ActorOn(int x, int y, Entities.Actors.Actor who) => ActorOn(x, y, who.Story);

    // A solid thing's (or a blocking wall sprite's) mark on its tile, on its own story
    private void MarkSolidThing(Entities.Actors.Actor thing, int tilex, int tiley)
    {
        bool ours = OnPlayersStory(thing);
        var grid = ours ? ActorAt : StoryGrid(thing.Story);
        if (thing.Flags.Any(f => f.Equals("SOLID", StringComparison.OrdinalIgnoreCase)))
        {
            if (ours || !StorySolid(tilex, tiley, thing.Story))
                grid[tilex, tiley] = new BlockingActor();
        }
        else if (WallSprites.IsWallSprite(thing) && !WallSprites.IsPassable(thing) && grid[tilex, tiley] == null
            && (ours || !StorySolid(tilex, tiley, thing.Story)))
            grid[tilex, tiley] = new WallSpriteBlocker();
    }

    /// <summary>
    /// Puts every thing off the player's story back on its story's grid (after the player's story
    /// changes, a thing's story is set, or a game's loaded); one now on the player's story (its own
    /// story is the player's new one) is marked in ActorAt[], and one that's left it is taken out
    /// </summary>
    internal void RebuildStoryGrids()
    {
        storygrids.Clear();
        foreach (var actor in _actors)
        {
            if (actor.IsRemoved || actor is Entities.Actors.PlayerPawn || actor.Story == 0 || !InMap(actor.TileX, actor.TileY))
                continue;
            int x = actor.TileX, y = actor.TileY;
            if (actor is Entities.Actors.Monster monster)
            {
                if (ActorAt[x, y] is ActorMark mark && ReferenceEquals(mark.Who, monster) && !OnPlayersStory(monster))
                    ActorAt[x, y] = null;           // it was on the player's old story
                MarkActorTile(monster);
            }
            else if (OnPlayersStory(actor))
            {
                if (ActorAt[x, y] == null)
                    MarkSolidThing(actor, x, y);
            }
            else
            {
                // A solid thing that was on the player's old story: off ActorAt[], unless another's there
                if (ActorAt[x, y] is BlockingActor and not WallSpriteBlocker && IsSolidThing(actor)
                    && !_actors.Any(a => !a.IsRemoved && a.TileX == x && a.TileY == y && OnPlayersStory(a) && IsSolidThing(a)))
                    ActorAt[x, y] = null;
                MarkSolidThing(actor, x, y);
            }
        }
    }

    /// <summary>Puts things on a story (1 to 8), each marked on its new story's grid (or ActorAt[], the player's)</summary>
    internal void MoveToStory(IReadOnlyList<Entities.Actors.Actor> things, int story)
    {
        foreach (var thing in things)
        {
            if (thing is Entities.Actors.Monster monster)
                UnmarkActorTile(monster);
            thing.Story = story == Story ? 0 : story;
        }
        RebuildStoryGrids();
        foreach (var thing in things)
        {
            if (!OnPlayersStory(thing) || !InMap(thing.TileX, thing.TileY))
                continue;
            if (thing is Entities.Actors.Monster monster)
                MarkActorTile(monster);
            else if (ActorAt[thing.TileX, thing.TileY] == null)
                MarkSolidThing(thing, thing.TileX, thing.TileY);
        }
    }

    private static bool IsSolidThing(Entities.Actors.Actor thing) =>
        thing is not Entities.Actors.Monster && thing.Flags.Any(f => f.Equals("SOLID", StringComparison.OrdinalIgnoreCase));

    /*
    =============================================================================

                                    SAVE GAMES

    A save stores the level as it stands, not as a diff from the map file: the map planes
    (MovingWalls, ambush tiles and doors rewrite them), the wall/door TileMap, what blocks each
    tile, and every actor. Loading reloads the map first (for the parts a save doesn't hold,
    such as door lock translations) and then lays this over it.

    =============================================================================
    */

    private enum SavedTile : byte { Empty, Wall, Door, Blocking, WallSprite }

    /// <summary>The actors a save writes, in the order it writes them -- removed ones are dropped.</summary>
    internal List<Entities.Actors.Actor> GetSavedActors() => _actors.Where(a => !a.IsRemoved).ToList();

    internal void WriteLevelState(BinaryWriter bw)
    {
        bw.Write(mapsegs.Length);
        foreach (var plane in mapsegs)
        {
            bw.Write(plane.Length);
            foreach (var spot in plane)
                bw.Write(spot);
        }

        bw.Write(MapWidth);
        bw.Write(MapHeight);
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                bw.Write(TileMap[x, y]);
                switch (ActorAt[x, y])
                {
                    case Wall wall:
                        bw.Write((byte)SavedTile.Wall);
                        bw.Write(wall.WallId);
                        break;
                    case Door door:
                        bw.Write((byte)SavedTile.Door);
                        bw.Write(door.DoorNumber);
                        break;
                    case WallSpriteBlocker:
                        bw.Write((byte)SavedTile.WallSprite);
                        break;
                    case BlockingActor:
                        bw.Write((byte)SavedTile.Blocking);
                        break;
                    default:
                        bw.Write((byte)SavedTile.Empty);
                        break;
                }
            }
        }

        var actors = GetSavedActors();
        bw.Write(actors.Count);
        foreach (var actor in actors)
            Entities.Actors.ActorSnapshot.Capture(actor).Write(bw);

        // Who each is after (an enemy's target) and who fired it (a projectile's shooter), by
        // their places in the list above; -1 for none
        int IndexOf(Entities.Actors.Actor? other) => other == null ? -1 : actors.IndexOf(other);
        foreach (var actor in actors)
        {
            bw.Write(IndexOf((actor as Entities.Actors.Monster)?.TargetIfAny));
            bw.Write(IndexOf(actor.Shooter));
        }

        WriteTextureChanges(bw);

        for (int x = 0; x < MapWidth; x++)
            for (int y = 0; y < MapHeight; y++)
                bw.Write(PatchMap[x, y]);

        bw.Write(ObjectiveCounts.Count);
        foreach (var (name, count) in ObjectiveCounts)
        {
            bw.Write(name);
            bw.Write(count.Done);
            bw.Write(count.Total);
        }

        bw.Write(WallDamage.Count);
        foreach (var (tile, damage) in WallDamage)
        {
            bw.Write(tile);
            bw.Write(damage);
        }

        bw.Write(CrumblingWalls.Count);
        foreach (var wall in CrumblingWalls)
        {
            bw.Write(wall.X);
            bw.Write(wall.Y);
            bw.Write(wall.EndTic);
            bw.Write(wall.Animation);
        }
    }

    /// <summary>Parses what <see cref="WriteLevelState"/> wrote, without touching the loaded level.</summary>
    internal static LevelSnapshot ReadLevelState(BinaryReader br)
    {
        var planes = new ushort[br.ReadCount()][];
        for (int i = 0; i < planes.Length; i++)
        {
            planes[i] = new ushort[br.ReadCount()];
            for (int j = 0; j < planes[i].Length; j++)
                planes[i][j] = br.ReadUInt16();
        }

        int width = br.ReadUInt16(), height = br.ReadUInt16();
        if (MapConstants.CheckMapSize(width, height) is { } badSize)
            throw new InvalidDataException($"The saved level is the wrong size: {badSize}.");
        if (planes.Any(plane => plane.Length != width * height))
            throw new InvalidDataException($"The saved level's planes aren't {width}x{height}.");

        var tiles = new ushort[width, height];
        var blocking = new Actor?[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                tiles[x, y] = br.ReadUInt16();
                blocking[x, y] = (SavedTile)br.ReadByte() switch
                {
                    SavedTile.Empty => null,
                    SavedTile.Wall => new Wall(br.ReadInt32()),
                    SavedTile.Door => new Door(br.ReadInt32()),
                    SavedTile.Blocking => new BlockingActor(),
                    SavedTile.WallSprite => new WallSpriteBlocker(),
                    var unknown => throw new InvalidDataException($"Unknown tile marker {unknown}."),
                };
            }
        }

        var actors = new List<Entities.Actors.ActorSnapshot>();
        for (int i = br.ReadCount(); i > 0; i--)
            actors.Add(Entities.Actors.ActorSnapshot.Read(br));
        var links = actors.Select(_ => (Target: br.ReadInt32(), Shooter: br.ReadInt32())).ToList();

        var (starts, changes) = ReadTextureChanges(br, width * height);

        var patches = new byte[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                patches[x, y] = br.ReadByte();

        var objectives = new Dictionary<string, ObjectiveCount>(StringComparer.OrdinalIgnoreCase);
        for (int i = br.ReadCount(); i > 0; i--)
            objectives[br.ReadString()] = new ObjectiveCount { Done = br.ReadInt32(), Total = br.ReadInt32() };

        var wallDamage = new Dictionary<int, int>();
        for (int i = br.ReadCount(); i > 0; i--)
        {
            int tile = br.ReadInt32();
            if (tile < 0 || tile >= width * height)
                throw new InvalidDataException($"Bad tile {tile} in the wall damage.");
            wallDamage[tile] = br.ReadInt32();
        }

        var crumbling = new List<CrumblingWall>();
        for (int i = br.ReadCount(); i > 0; i--)
        {
            var wall = new CrumblingWall(br.ReadInt32(), br.ReadInt32(), br.ReadInt32(), br.ReadString());
            if (wall.X < 0 || wall.Y < 0 || wall.X >= width || wall.Y >= height)
                throw new InvalidDataException($"Bad tile {wall.X},{wall.Y} in the crumbling walls.");
            crumbling.Add(wall);
        }

        return new LevelSnapshot(planes, tiles, blocking, actors, links, starts, changes, patches, objectives, wallDamage, crumbling);
    }

    /// <summary>
    /// Why <paramref name="level"/> can't be restored onto <paramref name="mapName"/> with the
    /// current actordefs, or null if it can. Checked before anything is changed, so a save
    /// from an incompatible mod or map edit fails cleanly instead of leaving a half-loaded level.
    /// </summary>
    internal string? CheckLevelState(LevelSnapshot level, string mapName)
    {
        var mapAsset = assetManager.Value.Find<MapAsset>(mapName);
        if (mapAsset == null)
            return $"Map \"{mapName}\" was not found.";

        if (level.Planes.Length != mapAsset.MapData.Length
            || level.Planes.Where((plane, i) => plane.Length != mapAsset.MapData[i].Length).Any()
            || level.TileMap.GetLength(0) != mapAsset.Width || level.TileMap.GetLength(1) != mapAsset.Height)
            return $"Map \"{mapName}\" has changed since the game was saved.";

        if (level.Actors.Count(a => a.IsPlayer) != 1)
            return "The save has no player.";

        var missing = level.Actors.FirstOrDefault(a => !a.IsPlayer && !RuntimeActorMetadata.Actors.ContainsKey(a.ClassName));
        if (missing != null)
            return $"Actor \"{missing.ClassName}\" no longer exists.";

        return null;
    }

    /// <summary>
    /// Replaces the loaded level's state with a saved one (after <see cref="CheckLevelState"/>
    /// passed). Returns the restored actors in saved order, so references to them (such as
    /// the player's last attacker) can be looked up by index.
    /// </summary>
    internal List<Entities.Actors.Actor> RestoreLevelState(LevelSnapshot level, Func<int, Entities.PlayerState?>? ownerOf = null)
    {
        mapsegs = level.Planes.Select(plane => (ushort[])plane.Clone()).ToArray();
        TileMap = (ushort[,])level.TileMap.Clone();
        PatchMap = (byte[,])level.Patches.Clone();
        ActorAt = (Actor?[,])level.ActorAt.Clone();
        // copies, so a hub level's kept snapshot doesn't change with the live level
        AnimStart = (int[])level.AnimationStarts.Clone();
        texturechanges = level.TextureChanges.ToDictionary(c => c.Key,
            c => new Dictionary<string, string>(c.Value, StringComparer.OrdinalIgnoreCase));
        ObjectiveCounts = level.Objectives.ToDictionary(o => o.Key, o => new ObjectiveCount { Done = o.Value.Done, Total = o.Value.Total },
            StringComparer.OrdinalIgnoreCase);
        WallDamage = new Dictionary<int, int>(level.WallDamage);
        CrumblingWalls = [.. level.CrumblingWalls];
        ReadMapInfo(fillFlats: false);      // the saved flat plane already has the map's flats
        BuildWallShapes();
        BuildBlocks();
        BuildBlockAreas();
        BuildFlats(defaultfloor, defaultceiling);
        ZonesVersion++;

        _actors.Clear();
        LevelSerial++;
        ForgetPawns();

        // A save is a single player's game: its pawn is the local player's
        var local = session.LocalPlayer;
        var restored = new List<Entities.Actors.Actor>(level.Actors.Count);
        foreach (var saved in level.Actors)
        {
            Entities.Actors.Actor actor;
            if (saved.IsPlayer && (ownerOf?.Invoke(restored.Count) ?? local) is { } owner)
                actor = owner.Pawn = new Entities.Actors.PlayerPawn { State = owner, World = World };
            else
                actor = CreateRuntimeActor(saved.ClassName)!;

            saved.ApplyTo(actor);
            _actors.AddLast(actor);
            restored.Add(actor);
        }

        // Who each was after, and who fired each projectile
        Entities.Actors.Actor? At(int index) => index >= 0 && index < restored.Count ? restored[index] : null;
        for (int i = 0; i < restored.Count && i < level.Links.Count; i++)
        {
            if (restored[i] is Entities.Actors.Monster monster)
                monster.Target = At(level.Links[i].Target)!;
            restored[i].Shooter = At(level.Links[i].Shooter);
        }

        // Saves don't hold the enemies' ActorAt[] marks: put them back where they stand (and
        // the things on other stories on theirs: they aren't in ActorAt[])
        storygrids.Clear();
        foreach (var monster in restored.OfType<Entities.Actors.Monster>())
            MarkActorTile(monster);
        foreach (var thing in restored)
            if (thing is not Entities.Actors.Monster && !OnPlayersStory(thing) && InMap(thing.TileX, thing.TileY))
                MarkSolidThing(thing, thing.TileX, thing.TileY);

        // The player has to think first, as it does after a normal level load (with several,
        // they're already first, in the order they think)
        if (ownerOf == null)
        {
            _actors.Remove(local.Pawn!);
            _actors.AddFirst(local.Pawn!);
        }

        return restored;
    }
}

internal sealed record LevelSnapshot(
    ushort[][] Planes,
    ushort[,] TileMap,
    Actor?[,] ActorAt,
    List<Entities.Actors.ActorSnapshot> Actors,
    List<(int Target, int Shooter)> Links,
    int[] AnimationStarts,
    Dictionary<int, Dictionary<string, string>> TextureChanges,
    byte[,] Patches,
    Dictionary<string, ObjectiveCount> Objectives,
    Dictionary<int, int> WallDamage,
    List<CrumblingWall> CrumblingWalls);

/// <summary>A wall showing its crumble animation (MapManager.CrumblingWalls) until level tic EndTic, when it breaks</summary>
internal sealed record CrumblingWall(int X, int Y, int EndTic, string Animation);

/// <summary>How many of a level objective are done, of how many (Gameplay/Objectives.cs)</summary>
internal sealed class ObjectiveCount
{
    public int Done, Total;
    public int Left => Math.Max(Total - Done, 0);
}
