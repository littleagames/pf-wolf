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

    /// <summary>How many 64 unit stories tall the level's walls are, where plane 3 doesn't say (tall walls; mapdefs wall-height)</summary>
    internal int LevelWallStories = 1;

    private UInt16[][] mapsegs = new ushort[LevelPlanes][];

    private readonly LinkedList<Entities.Actors.Actor> _actors = new();

    /// <summary>Goes up each time the level's actors are put on afresh (a map loaded, a level restored): which level it is</summary>
    internal int LevelSerial { get; private set; }

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

    /// <summary>A tile's <see cref="TileIndex"/>, or -1 when it's off the level</summary>
    internal int TileIndexOrNone(int x, int y) => InMap(x, y) ? y * MapWidth + x : -1;

    internal ushort[,] TileMap = new ushort[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize];

    /// <summary>
    /// Each wall tile's shape: Square, or a 45 degree diagonal from a mapdefs diagonal marker on
    /// the object plane. Not saved: it's rebuilt from the planes whenever they're loaded or restored.
    /// </summary>
    internal WallShape[,] WallShapes = new WallShape[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize];

    /// <summary>
    /// Each tile's wall height in stories from the height plane (plane 3), or 0 where it has
    /// none and the map's default applies (see <see cref="WallStories"/>). Rebuilt from the
    /// planes whenever they're loaded or restored, like <see cref="WallShapes"/>.
    /// </summary>
    internal byte[,] StoryMap = new byte[MapConstants.ClassicMapSize, MapConstants.ClassicMapSize];
    private int maxtilestories;

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
        animatedfloors.Clear();
        animatedceilings.Clear();
        ClearTextureChanges();
        ObjectiveCounts = new(StringComparer.OrdinalIgnoreCase);     // counted afresh as the level's set up (Gameplay/Objectives.cs)

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

        // Every actor lives in _actors and nothing else clears this list, so reloading a level
        // (death with lives left, replaying a level in a new game, etc.) would otherwise pile
        // the new level's actors on top of the previous load's instead of replacing them.
        _actors.Clear();
        LevelSerial++;
        ForgetPawns();
        PlayerStart = null;
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

        bool entranceStart = false;     // the start of the entrance the level's entered by has been found
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                int tile = MapSpot(x, y, 0);
                bool solid = IsWallId(tile) && data.Walls.ContainsKey(tile) || data.Doors.ContainsKey(tile);
                CheckPatch(mapName, x, y, isWall: solid && !data.Doors.ContainsKey(tile), data);
                if (solid)
                {
                    // solid wall (a door's tile is made a door by SpawnDoor)
                    TileMap[x, y] = (ushort)tile;
                    ActorAt[x, y] = new Wall(tile);// (uint)tile;
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
                            entranceStart = true;
                        }
                    }
                    else if (!entranceStart)
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
        BuildWallHeights();
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

            var actor = SpawnThing(thing.TileX, thing.TileY,
                new MapActorTranslation { Class = thing.Class, Angles = thing.Angle, Patrol = thing.Patrol ? 1 : 0 });
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
    /// Makes the open tiles round the map's edge solid: view traces and movement only stop at
    /// walls, and would run off the map. They're a wall id the mapdefs don't give, so they're
    /// drawn as nothing (the floor and ceiling show through) but can't be crossed.
    /// </summary>
    private void SealEdge(string mapName, MapObjectTranslationAsset data)
    {
        int edgeWall = Enumerable.Range(1, GameConstants.BitWall - 1).Reverse().FirstOrDefault(id => !data.Walls.ContainsKey(id), 1);
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
    monster off its tile, `blocktile: bosses` only those with the BOSS flag. Such a thing is
    never spawned; its tile is marked as the map is read, so a loaded game has it too.
    */

    /// <summary>A tile's monster blocker: none, every monster, or bosses only. Indexed <see cref="TileIndex"/>.</summary>
    internal enum MonsterBlock : byte { None, Monsters, Bosses }

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
            MonsterBlock.Monsters => true,
            MonsterBlock.Bosses => boss,
            _ => false,
        });

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

    /// <summary>
    /// The level's enemies wander rather than chase (A_SetMonsterWander: End of Destiny's Greed,
    /// until the gong): Monster.SelectWanderDir. For the level only, as the monster blockers
    /// cleared are: a saved game loads without them, for a script to set again.
    /// </summary>
    internal bool MonstersWander { get; set; }

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
        MonstersWander = false;
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
            if (tx >= 0 && ty >= 0 && tx < MapWidth && ty < MapHeight && ValidArea(MapSpot(tx, ty, 0)))
                return (byte)(MapSpot(tx, ty, 0) - Floors.AreaTile);
        }
        return 0;
    }

    /// <summary>
    /// Sets <see cref="StoryMap"/> from the height plane. Values outside 1 to MaxWallStories
    /// (such as an ECWolf info plane) are left to the map's default.
    /// </summary>
    private void BuildWallHeights()
    {
        StoryMap = new byte[MapWidth, MapHeight];
        maxtilestories = 0;

        for (int y = 0; y < MapHeight; y++)
            for (int x = 0; x < MapWidth; x++)
            {
                int stories = MapSpot(x, y, HeightPlane);
                if (stories is >= 1 and <= GameConstants.MaxWallStories)
                {
                    StoryMap[x, y] = (byte)stories;
                    maxtilestories = Math.Max(maxtilestories, stories);
                }
            }
    }

    /// <summary>How many stories tall the wall on this tile is.</summary>
    internal int WallStories(int x, int y) => StoryMap[x, y] != 0 ? StoryMap[x, y] : LevelWallStories;

    /// <summary>The tallest wall anywhere on the level.</summary>
    internal int MaxWallStories => Math.Max(maxtilestories, LevelWallStories);

    /// <summary>
    /// Sets a tile's height on the height plane, so it's saved with the level; 0 gives it back
    /// to the map's default.
    /// </summary>
    internal void SetWallStories(int x, int y, int stories)
    {
        SetMapSpot(x, y, HeightPlane, (ushort)stories);
        StoryMap[x, y] = (byte)stories;
        if (stories > maxtilestories)
            maxtilestories = stories;
    }

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

    /// <summary>Moves a tile's height to another tile, as a pushwall slides.</summary>
    internal void MoveWallStories(int fromx, int fromy, int tox, int toy)
    {
        int stories = StoryMap[fromx, fromy];
        SetWallStories(fromx, fromy, 0);
        SetWallStories(tox, toy, stories);
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
        for (int y = 0; y < MapHeight; y++)
            for (int x = 0; x < MapWidth; x++)
            {
                int i = TileIndex(x, y);
                FloorFlat[i] = SetFlat(Flat(FlatTextureName(x, y, ceiling: false)), i, animatedfloors);
                CeilingFlat[i] = SetFlat(Flat(FlatTextureName(x, y, ceiling: true)), i, animatedceilings);
                HasFloorFlats |= FloorFlat[i] != null;
                HasCeilingFlats |= CeilingFlat[i] != null;
            }
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
        var table = ceiling ? GetMapData().Flats.Ceiling : GetMapData().Flats.Floor;
        var defaultName = ceiling ? defaultceiling : defaultfloor;
        var name = table.TryGetValue(index, out var listed) && FindFlat(listed) != null ? listed
            : FindFlat(defaultName) != null ? defaultName : null;
        return name == null ? null : ChangedTexture(x, y, name);
    }

    /// <summary>The name of the texture on a tile's floor or ceiling, for the console.</summary>
    internal string FlatName(int x, int y, bool ceiling)
    {
        int spot = MapSpot(x, y, FlatPlane);
        int index = ceiling ? spot >> 8 : spot & 0xff;
        var name = FlatTextureName(x, y, ceiling);
        return $"{index} {(name != null && FindFlat(name) != null ? name : "(color)")}";
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
    /// a wall taller than a story only loses its bottom one: the rest stays up as an arch.
    /// </summary>
    internal void RemoveWall(int x, int y, int area, bool arch)
    {
        int stories = WallStories(x, y);
        if (WallShapes[x, y] != WallShape.Square)
            SetWallShape(x, y, WallShape.Square);

        TileMap[x, y] = 0;
        if (ActorAt[x, y] is Wall)
            ActorAt[x, y] = null;
        SetMapSpot(x, y, 0, (ushort)(Floors.AreaTile + area));
        PatchMap[x, y] = 0;
        SetWallStories(x, y, arch && stories > 1 ? stories : 0);
    }

    internal record MapPlayerStart(int TileX, int TileY, int Angle);

    /// <summary>Where the loaded map puts the player (its object-plane player start); null if it has none.</summary>
    internal MapPlayerStart? PlayerStart { get; private set; }

    /// <summary>The map's deathmatch starts (mapdefs player-starts with `deathmatch: true`), if it has any</summary>
    internal List<MapPlayerStart> DeathmatchStarts { get; } = [];

    /// <summary>Each team's starts (mapdefs player-starts with `team: red` or `blue`), red's first</summary>
    internal List<MapPlayerStart>[] TeamStarts { get; } = [[], []];

    /// <summary>Whether the map's enemies are left out as it loads (a deathmatch without monsters)</summary>
    internal bool NoMonsters { get; set; }

    /// <summary>The trigger on this tile, if its object-plane tile is one in the mapdefs' triggers.</summary>
    internal MapTriggerTranslation? GetTrigger(int x, int y) =>
        GetMapData().Triggers.TryGetValue(MapSpot(x, y, 1), out var trigger) ? trigger : null;

    public void SpawnThing(int tilex, int tiley, string className) =>
        SpawnThing(tilex, tiley, new MapActorTranslation { Class = className });

    /// <param name="countKill">
    /// Whether a killable enemy adds to the level's kill total: not for one appearing mid-level
    /// out of another that already counted for it (an alien out of its canister)
    /// </param>
    public Entities.Actors.Actor? SpawnThing(int tilex, int tiley, MapActorTranslation thing, bool countKill = true)
    {
        // MinSkill gates enemy availability by skill (its place in game-info's skills, as the
        // legacy ScanInfoPlane's gd_medium/gd_hard checks per tile-number range); always 0 for
        // decorations/pickups, so this is a no-op there.
        if (thing.MinSkill > _difficulty)
            return string.IsNullOrEmpty(thing.Else) ? null
                : SpawnThing(tilex, tiley, thing with { Class = thing.Else, MinSkill = 0, Else = "" }, countKill);

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
        if (builtActor.Flags.Any(f => f.Equals("SOLID", StringComparison.OrdinalIgnoreCase)))
            ActorAt[tilex, tiley] = new BlockingActor();
        else if (WallSprites.IsWallSprite(builtActor) && !WallSprites.IsPassable(builtActor) && ActorAt[tilex, tiley] == null)
            ActorAt[tilex, tiley] = new WallSpriteBlocker();

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
        if (ActorAt[x, y] is BlockingActor || ActorAt[x, y] is ActorMark mark && ReferenceEquals(mark.Who, old))
            ActorAt[x, y] = null;
        int angles = old.Dir == ObjDir.NoDir ? -1 : (int)old.Dir * 45;
        var replaced = SpawnThing(x, y, new MapActorTranslation { Class = className, Angles = angles }, countKill: false);
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
        int spot = MapSpot(x, y, 0);
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
    internal bool IsShootableActorAt(int tilex, int tiley)
    {
        foreach (var actor in _actors)
        {
            if (!actor.IsRemoved && actor.TileX == tilex && actor.TileY == tiley && IsSolidActor(actor))
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
        // while its area isn't connected to the player's
        if (ob is Entities.Actors.Monster { SleepsOutOfReach: true } && ob.Active == ActiveType.No
            && ob.AreaNumber < Floors.NumAreas && session.Areas.AreaByPlayer[ob.AreaNumber] == 0)
            return;

        // It's lifted off its tile while it thinks, and put back on the one it ends up on
        bool marks = ob is Entities.Actors.Monster && (ob.RuntimeFlags & (ObjFlags.NonMark | ObjFlags.NeverMark)) == 0;
        if (marks)
            UnmarkActorTile(ob.TileX, ob.TileY);

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

        ref var spot = ref ActorAt[ob.TileX, ob.TileY];
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

    /// <summary>The enemy marked on a tile (MarkActorTile), if any</summary>
    internal Entities.Actors.Monster? ActorMarkAt(int tilex, int tiley) => (ActorAt[tilex, tiley] as ActorMark)?.Who;

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

        return new LevelSnapshot(planes, tiles, blocking, actors, links, starts, changes, patches, objectives);
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
        ReadMapInfo(fillFlats: false);      // the saved flat plane already has the map's flats
        BuildWallShapes();
        BuildWallHeights();
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

        // Saves don't hold the enemies' ActorAt[] marks: put them back where they stand
        foreach (var monster in restored.OfType<Entities.Actors.Monster>())
            MarkActorTile(monster);

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
    Dictionary<string, ObjectiveCount> Objectives);

/// <summary>How many of a level objective are done, of how many (Gameplay/Objectives.cs)</summary>
internal sealed class ObjectiveCount
{
    public int Done, Total;
    public int Left => Math.Max(Total - Done, 0);
}
