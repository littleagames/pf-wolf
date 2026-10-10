using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Enums;
using objflags = PFWolf.Program.objflags;

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
    internal const int MAPSHIFT = MapConstants.MAPSHIFT;
    internal const int MAPSIZE = MapConstants.MAPSIZE;
    internal const int MAPAREA = MapConstants.MAPAREA;

    public const int NUMMAPS = 60;
    public const int MAPPLANES = MapConstants.MAPPLANES;

    /// <inheritdoc cref="MapConstants.LEVELPLANES"/>
    public const int LEVELPLANES = MapConstants.LEVELPLANES;

    private readonly Lazy<AssetManager> assetManager;

    private UInt16[][] mapsegs = new ushort[LEVELPLANES][];

    private readonly LinkedList<Entities.Actors.Actor> _actors = new();

    // The acting player's pawn (Program.ActAs), held in _actors with every other player's at
    // its head, in player order. Null until CreatePlayer runs for the current level -- LoadMap
    // clears _actors, which drops the previous level's pawns.
    internal Entities.Actors.PlayerPawn? Player => Program.playerstate.Pawn;

    /// <summary>Every player's pawn on this level, in player order</summary>
    internal IEnumerable<Entities.Actors.PlayerPawn> Players =>
        Program.players.Select(p => p.Pawn).OfType<Entities.Actors.PlayerPawn>();

    // Drops every player's pawn (the level's actors are going)
    private static void ForgetPawns()
    {
        foreach (var p in Program.players)
            p.Pawn = null;
    }

    private int _difficulty;
    private string? _enemyHealthKey;

    public MapManager(Lazy<AssetManager> assetManager)
    {
        this.assetManager = assetManager;
        AI = new LevelAI(this);
    }

    /// <summary>What the level does on its own: outlets, warp sites, once-a-level drops, talking (see <see cref="LevelAI"/>)</summary>
    internal LevelAI AI { get; }

    internal ushort mapwidth, mapheight;
    internal byte[,] tilemap;

    /// <summary>
    /// Each wall tile's shape: Square, or a 45 degree diagonal from a mapdefs diagonal marker on
    /// the object plane. Not saved: it's rebuilt from the planes whenever they're loaded or restored.
    /// </summary>
    internal WallShape[,] wallshape = new WallShape[MAPSIZE, MAPSIZE];

    /// <summary>
    /// Each tile's wall height in stories from the height plane (plane 3), or 0 where it has
    /// none and the map's default applies (see <see cref="WallStories"/>). Rebuilt from the
    /// planes whenever they're loaded or restored, like <see cref="wallshape"/>.
    /// </summary>
    internal byte[,] storymap = new byte[MAPSIZE, MAPSIZE];
    private int maxtilestories;

    internal const int HEIGHTPLANE = MapConstants.HEIGHTPLANE;

    /// <summary>
    /// Each tile's tag, 0 for none: a switch acts on the doors, walls and actors that share its
    /// tag. An actor takes its tag from the tile it spawns on (<see cref="Entities.Actors.Actor.Tag"/>).
    /// </summary>
    internal const int TAGPLANE = MapConstants.TAGPLANE;

    /// <summary>
    /// Each tile's light zone, 0 for none (the map's light): the map's game-info zones say how
    /// each is lit (Program.WL_SHADE.cs).
    /// </summary>
    internal const int ZONEPLANE = MapConstants.ZONEPLANE;

    /// <summary>Goes up whenever the zone plane may have changed: a level loaded or restored, or a zone set.</summary>
    internal int ZonesVersion { get; private set; }

    /// <summary>
    /// ECWolf's floor and ceiling plane: the low byte of a tile's value picks its floor flat and
    /// the high byte its ceiling flat, through the mapdefs flats table (see <see cref="floorflat"/>).
    /// </summary>
    internal const int FLATPLANE = MapConstants.FLATPLANE;

    /// <summary>
    /// Each tile's floor and ceiling texture: its flat plane indices through the mapdefs flats
    /// table, else the map's default-floor and default-ceiling; null where there's none (the
    /// floor or ceiling color shows). Indexed (y &lt;&lt; MAPSHIFT) + x. Flats don't move, so these
    /// are only built when a level is loaded or restored.
    /// </summary>
    internal TextureAsset?[] floorflat = new TextureAsset?[MAPAREA], ceilingflat = new TextureAsset?[MAPAREA];

    /// <summary>Whether any tile has a floor flat, or a ceiling flat.</summary>
    internal bool hasfloorflats, hasceilingflats;
    internal bool[,] spotvis;
    internal Actor?[,] actorat;

    /// <summary>
    /// What the player has seen of this level, for the automap: floor the view rays have crossed
    /// and the wall faces they've hit. Unlike spotvis it lasts for the whole level, and it's saved.
    /// </summary>
    internal SeenFlags[,] seen;

    /// <summary>Adds the floor tiles visible this frame (spotvis, after the walls are traced) to <see cref="seen"/>.</summary>
    internal void MarkSeenFromSpotvis()
    {
        for (int x = 0; x < MAPSIZE; x++)
            for (int y = 0; y < MAPSIZE; y++)
                if (spotvis[x, y])
                    seen[x, y] |= SeenFlags.Floor;
    }

    /// <summary>The seen map as one byte per tile, column by column, for save games.</summary>
    internal byte[] GetSeenBytes()
    {
        var bytes = new byte[MAPSIZE * MAPSIZE];
        for (int x = 0; x < MAPSIZE; x++)
            for (int y = 0; y < MAPSIZE; y++)
                bytes[x * MAPSIZE + y] = (byte)seen[x, y];
        return bytes;
    }

    internal void SetSeenBytes(byte[] bytes)
    {
        for (int x = 0; x < MAPSIZE; x++)
            for (int y = 0; y < MAPSIZE; y++)
                seen[x, y] = (SeenFlags)bytes[x * MAPSIZE + y];
    }

    public ushort GetTile(int x, int y, int plane)
    {
        return mapsegs[0][y * mapwidth + x];
    }

    // A wall's id is kept in tilemap below the BIT_WALL and BIT_DOOR flags
    private static bool IsWallId(int tile) => tile is > 0 and < Program.BIT_WALL;

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

        mapwidth = mapAsset.Width;
        mapheight = mapAsset.Height;
        // Clone each plane: SpawnDoor and other setup code mutate mapsegs in place
        // (e.g. SetMapSpot), and mapAsset is a cached singleton reused for every
        // load of this level, so writing through the original array would
        // permanently corrupt the cached map data (doors would vanish on replay).
        mapsegs = new ushort[mapAsset.MapData.Length][];
        for (int i = 0; i < mapAsset.MapData.Length; i++)
            mapsegs[i] = (ushort[])mapAsset.MapData[i].Clone();
        floorflat = new TextureAsset?[MAPAREA];     // BuildFlats fills these in, once the level's defaults are known
        ceilingflat = new TextureAsset?[MAPAREA];
        hasfloorflats = hasceilingflats = false;

#if USE_FEATUREFLAGS
    const int MXX = MAPSIZE - 1;
    
    // Read feature flags data from map corners and overwrite corners with adjacent tiles
    ffDataTopLeft     = MAPSPOT(0,   0,   0); MAPSPOT(0,   0,   0) = MAPSPOT(1,       0,       0);
    ffDataTopRight    = MAPSPOT(MXX, 0,   0); MAPSPOT(MXX, 0,   0) = MAPSPOT(MXX,     1,       0);
    ffDataBottomRight = MAPSPOT(MXX, MXX, 0); MAPSPOT(MXX, MXX, 0) = MAPSPOT(MXX - 1, MXX,     0);
    ffDataBottomLeft  = MAPSPOT(0,   MXX, 0); MAPSPOT(0,   MXX, 0) = MAPSPOT(0,       MXX - 1, 0);
#endif

        tilemap = new byte[MAPSIZE, MAPSIZE]; // wall values only
        spotvis = new bool[MAPSIZE, MAPSIZE];
        seen = new SeenFlags[MAPSIZE, MAPSIZE];
        actorat = new Actor?[MAPSIZE, MAPSIZE];

        // Every actor lives in _actors and nothing else clears this list, so reloading a level
        // (death with lives left, replaying a level in a new game, etc.) would otherwise pile
        // the new level's actors on top of the previous load's instead of replacing them.
        _actors.Clear();
        ForgetPawns();
        PlayerStart = null;
        DeathmatchStarts.Clear();

        var data = GetMapData();
        UnhideAreas();
        ReadMapInfo(fillFlats: true);
        foreach (var id in data.Walls.Keys.Where(id => !IsWallId(id)))
            if (badwallids.Add(id))
                Console.WriteLine($"mapdefs wall {id} is ignored: wall ids are 1 to {Program.BIT_WALL - 1}");
        foreach (var (id, wall) in data.Walls)
            if (wall.Switch is { To: not 0 } wallSwitch && !(IsWallId(wallSwitch.To) && data.Walls.ContainsKey(wallSwitch.To))
                && badswitches.Add(id))
                Console.WriteLine($"mapdefs wall {id}'s switch goes to {wallSwitch.To}, which isn't a wall; it won't change when used");

        for (int y = 0; y < mapheight; y++)
        {
            for (int x = 0; x < mapwidth; x++)
            {
                int tile = MAPSPOT(x, y, 0);
                bool solid = IsWallId(tile) && data.Walls.ContainsKey(tile) || data.Doors.ContainsKey(tile);
                if (solid)
                {
                    // solid wall (a door's tile is made a door by SpawnDoor)
                    tilemap[x, y] = (byte)tile;
                    actorat[x, y] = new Wall(tile);// (uint)tile;
                }
                else
                {
                    // area floor (unless a patroller already scanned is heading onto it)
                    tilemap[x, y] = 0;
                    if (actorat[x, y] is not ActorMark)
                        actorat[x, y] = null;
                }

                // TODO: SpawnDoor

                if (infotiles[y * MAPSIZE + x])
                    continue;

                // Things only stand on open floor: an object-plane value on a wall or door is
                // something else (Blake Stone's door locks, switch and teleporter data)
                int objtile = MAPSPOT(x, y, 1);
                if (!solid && data.Things.TryGetValue(objtile, out var thingXlat))
                {
                    SpawnThing(x, y, thingXlat);
                    continue;
                }

                // The player pawn is created after the map loads (Program.InitActorList), so
                // only note where it starts; with several starts, the last one scanned wins.
                if (data.PlayerStarts.TryGetValue(objtile, out var startXlat))
                {
                    if (startXlat.Deathmatch)
                        DeathmatchStarts.Add(new MapPlayerStart(x, y, startXlat.Angles));
                    else
                        PlayerStart = new MapPlayerStart(x, y, startXlat.Angles);
                    continue;
                }

                if (data.Triggers.TryGetValue(objtile, out var triggerXlat))
                {
                    if (triggerXlat.Secret && !Program.loadedgame)
                        Program.gamestate.secrettotal++;
                    continue;
                }
            }
        }

        SealEdge(mapName, data);
        BuildWallShapes();
        BuildWallHeights();
        ZonesVersion++;
    }

    /// <summary>
    /// Makes the open tiles round the map's edge solid: view traces and movement only stop at
    /// walls, and would run off the map. They're a wall id the mapdefs don't give, so they're
    /// drawn as nothing (the floor and ceiling show through) but can't be crossed.
    /// </summary>
    private void SealEdge(string mapName, MapObjectTranslationAsset data)
    {
        int edgeWall = Enumerable.Range(1, Program.BIT_WALL - 1).Reverse().FirstOrDefault(id => !data.Walls.ContainsKey(id), 1);
        int sealedTiles = 0;
        for (int y = 0; y < mapheight; y++)
        {
            for (int x = 0; x < mapwidth; x++)
            {
                if ((x != 0 && y != 0 && x != mapwidth - 1 && y != mapheight - 1) || tilemap[x, y] != 0)
                    continue;
                tilemap[x, y] = (byte)edgeWall;
                actorat[x, y] = new Wall(edgeWall);
                sealedTiles++;
            }
        }

        if (sealedTiles > 0)
            Console.WriteLine($"{mapName}: {sealedTiles} tiles round the map's edge aren't mapdefs walls or doors; they're made solid");
    }

    /// <summary>Turns the hidden area codes on plane 0 into the plain area codes (mapdefs floors hidden-area-start).</summary>
    private void UnhideAreas()
    {
        if (Floors.HiddenAreaTile <= 0)
            return;

        var plane = mapsegs[0];
        for (int i = 0; i < plane.Length; i++)
            plane[i] = (ushort)Floors.Unhidden(plane[i]);
    }

    /// <summary>
    /// Object-plane tiles that hold map info (mapdefs map-info) or its value, rather than a
    /// thing. Indexed (y &lt;&lt; MAPSHIFT) + x. Rebuilt with the planes, so it isn't saved.
    /// </summary>
    private bool[] infotiles = new bool[MAPAREA];

    internal record MapHint(int Message, byte Area);

    /// <summary>
    /// The map's hints (map-info `hint:<text>` codes), by text: each the message's number in the
    /// text and the room it's in (0xff for none), in map order. Read with the map, so not saved.
    /// </summary>
    internal Dictionary<string, List<MapHint>> Hints { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The map's own ceiling and floor colors (a map-info ceiling-floor-colors code), as palette indices.</summary>
    internal byte? MapCeilingColor { get; private set; }
    internal byte? MapFloorColor { get; private set; }

    /// <summary>
    /// Reads the map-info codes on the object plane (mapdefs map-info): marks their tiles in
    /// <see cref="infotiles"/>, takes the map's colors, and with <paramref name="fillFlats"/>
    /// gives its flats to every flat plane tile still at 0. As in Blake Stone, only a map's first
    /// code of each kind counts.
    /// </summary>
    private void ReadMapInfo(bool fillFlats)
    {
        infotiles = new bool[MAPAREA];
        MapCeilingColor = MapFloorColor = null;
        Hints = new(StringComparer.OrdinalIgnoreCase);

        var objects = mapsegs[1];
        var links = new Dictionary<int, ushort>();      // linked tile -> its tag
        if (fillFlats)          // a fresh level: a restored one has its tags already
            LinkObjectPlaneSwitches(objects, links);

        var codes = GetMapData().MapInfo;
        if (codes.Count == 0)
            return;

        bool gotFlats = false;
        for (int i = 0; i < MAPAREA && i < objects.Length; i++)
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
                byte area = onWall ? (byte)0xff : SpawnArea(i % MAPSIZE, i / MAPSIZE);
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
                    var flats = mapsegs[FLATPLANE];
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
        for (int i = 0; i < MAPAREA && i < objects.Length && i < walllayer.Length; i++)
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
        if (x <= 0 || y <= 0 || x >= mapwidth - 1 || y >= mapheight - 1)
            return;

        int target = (y << MAPSHIFT) + x;
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
                mapsegs[TAGPLANE][spot] = tag;
                int sx = spot & (MAPSIZE - 1), sy = spot >> MAPSHIFT;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = sx + dx, ny = sy + dy;
                    int next = (ny << MAPSHIFT) + nx;
                    if (nx >= 0 && ny >= 0 && nx < mapwidth && ny < mapheight
                        && linked.Contains(mapsegs[1][next]) && seen.Add(next))
                        open.Enqueue(next);
                }
            }
        }
        mapsegs[TAGPLANE][codeTile] = tag;
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
            if (tx >= 0 && ty >= 0 && tx < mapwidth && ty < mapheight && VALIDAREA(MAPSPOT(tx, ty, 0)))
                return (byte)(MAPSPOT(tx, ty, 0) - Floors.AreaTile);
        }
        return 0;
    }

    /// <summary>
    /// Sets <see cref="storymap"/> from the height plane. Values outside 1 to MAXWALLSTORIES
    /// (such as an ECWolf info plane) are left to the map's default.
    /// </summary>
    private void BuildWallHeights()
    {
        storymap = new byte[MAPSIZE, MAPSIZE];
        maxtilestories = 0;

        for (int y = 0; y < mapheight; y++)
            for (int x = 0; x < mapwidth; x++)
            {
                int stories = MAPSPOT(x, y, HEIGHTPLANE);
                if (stories is >= 1 and <= Program.MAXWALLSTORIES)
                {
                    storymap[x, y] = (byte)stories;
                    maxtilestories = Math.Max(maxtilestories, stories);
                }
            }
    }

    /// <summary>How many stories tall the wall on this tile is.</summary>
    internal int WallStories(int x, int y) => storymap[x, y] != 0 ? storymap[x, y] : Program.wallstories;

    /// <summary>The tallest wall anywhere on the level.</summary>
    internal int MaxWallStories => Math.Max(maxtilestories, Program.wallstories);

    /// <summary>
    /// Sets a tile's height on the height plane, so it's saved with the level; 0 gives it back
    /// to the map's default.
    /// </summary>
    internal void SetWallStories(int x, int y, int stories)
    {
        SetMapSpot(x, y, HEIGHTPLANE, (ushort)stories);
        storymap[x, y] = (byte)stories;
        if (stories > maxtilestories)
            maxtilestories = stories;
    }

    /// <summary>The light zone a tile is in, 0 for none.</summary>
    internal ushort GetZone(int x, int y) => (ushort)MAPSPOT(x, y, ZONEPLANE);

    /// <summary>Puts a tile in a light zone on the zone plane, so it's saved with the level; 0 takes it out.</summary>
    internal void SetZone(int x, int y, ushort zone)
    {
        SetMapSpot(x, y, ZONEPLANE, zone);
        ZonesVersion++;
    }

    /// <summary>The tag on a tile, 0 for none.</summary>
    internal ushort GetTag(int x, int y) => (ushort)MAPSPOT(x, y, TAGPLANE);

    /// <summary>
    /// Tags a tile on the tag plane, so it's saved with the level; 0 clears it. An actor keeps
    /// the tag it spawned with, so this doesn't change the tags of any actors on the tile.
    /// </summary>
    internal void SetTag(int x, int y, ushort tag) => SetMapSpot(x, y, TAGPLANE, tag);

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

        for (int y = 0; y < mapheight; y++)
            for (int x = 0; x < mapwidth; x++)
                if (GetTag(x, y) == tag)
                    yield return (x, y);
    }

    /// <summary>Moves a tile's height to another tile, as a pushwall slides.</summary>
    internal void MoveWallStories(int fromx, int fromy, int tox, int toy)
    {
        int stories = storymap[fromx, fromy];
        SetWallStories(fromx, fromy, 0);
        SetWallStories(tox, toy, stories);
    }

    private string? defaultfloor, defaultceiling;

    internal string? DefaultFloor => defaultfloor;
    internal string? DefaultCeiling => defaultceiling;

    /// <summary>Sets a tile's value on the flat plane (floor index | ceiling index &lt;&lt; 8), so it's saved with the level.</summary>
    internal void SetFlats(int x, int y, ushort value)
    {
        SetMapSpot(x, y, FLATPLANE, value);
        BuildFlats(defaultfloor, defaultceiling);
    }

    /// <summary>
    /// Sets <see cref="floorflat"/> and <see cref="ceilingflat"/> from the flat plane, as ECWolf
    /// does: the low byte is the floor's index in the mapdefs flats table and the high byte the
    /// ceiling's. An index the table doesn't list (or whose texture isn't found) gets the map's
    /// default; with no default either, the tile keeps the floor or ceiling color.
    /// </summary>
    internal void BuildFlats(string? defaultFloor, string? defaultCeiling)
    {
        defaultfloor = defaultFloor;
        defaultceiling = defaultCeiling;

        var flats = GetMapData().Flats;
        var floors = new TextureAsset?[256];
        var ceilings = new TextureAsset?[256];
        var floorDefault = FindFlat(defaultFloor);
        var ceilingDefault = FindFlat(defaultCeiling);
        for (int i = 0; i < 256; i++)
        {
            floors[i] = (flats.Floor.TryGetValue(i, out var floor) ? FindFlat(floor) : null) ?? floorDefault;
            ceilings[i] = (flats.Ceiling.TryGetValue(i, out var ceiling) ? FindFlat(ceiling) : null) ?? ceilingDefault;
        }

        floorflat = new TextureAsset?[MAPAREA];
        ceilingflat = new TextureAsset?[MAPAREA];
        hasfloorflats = hasceilingflats = false;
        for (int y = 0; y < mapheight; y++)
            for (int x = 0; x < mapwidth; x++)
            {
                int spot = MAPSPOT(x, y, FLATPLANE);
                int i = (y << MAPSHIFT) + x;
                floorflat[i] = floors[spot & 0xff];
                ceilingflat[i] = ceilings[spot >> 8];
                hasfloorflats |= floorflat[i] != null;
                hasceilingflats |= ceilingflat[i] != null;
            }
    }

    /// <summary>The name of the texture on a tile's floor or ceiling, for the console.</summary>
    internal string FlatName(int x, int y, bool ceiling)
    {
        int spot = MAPSPOT(x, y, FLATPLANE);
        int index = ceiling ? spot >> 8 : spot & 0xff;
        var table = ceiling ? GetMapData().Flats.Ceiling : GetMapData().Flats.Floor;
        var name = table.TryGetValue(index, out var listed) && FindFlat(listed) != null ? listed
            : ceiling ? defaultceiling : defaultfloor;
        return $"{index} {(FindFlat(name) != null ? name : "(color)")}";
    }

    private readonly HashSet<string> missingflats = new(StringComparer.OrdinalIgnoreCase);

    // A flat is any texture: a VSWAP wall, or a picture in the pk3's textures/ or flats/
    private TextureAsset? FindFlat(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        var texture = assetManager.Value.Find<TextureAsset>(name);
        if (texture is { Width: > 0, Height: > 0 } && texture.RawData.Length >= texture.Width * texture.Height)
            return texture;
        if (missingflats.Add(name))
            Console.WriteLine($"Flat texture \"{name}\" wasn't found");
        return null;
    }

    /// <summary>Sets <see cref="wallshape"/> from the diagonal markers on the object plane.</summary>
    private void BuildWallShapes()
    {
        var diagonals = GetMapData().Diagonals;
        wallshape = new WallShape[MAPSIZE, MAPSIZE];

        for (int y = 0; y < mapheight; y++)
            for (int x = 0; x < mapwidth; x++)
                wallshape[x, y] = IsPlainWall(x, y) && diagonals.TryGetValue(MAPSPOT(x, y, 1), out var diagonal)
                    ? diagonal.Shape
                    : WallShape.Square;
    }

    /// <summary>
    /// A solid wall tile that can take a shape: not a door, not a moving pushwall, and not open
    /// floor. A wall beside a door (SpawnDoor sets BIT_WALL on it, for the door-side texture) counts.
    /// Plane 0 has to hold a mapdefs wall too: before SpawnDoor runs, a door tile's raw tilemap
    /// value (90-101 in Wolf3D) looks just like a wall id with BIT_WALL set.
    /// </summary>
    internal bool IsPlainWall(int x, int y)
    {
        var tile = tilemap[x, y];
        var spot = MAPSPOT(x, y, 0);
        return IsWallId(spot) && !GetMapData().Doors.ContainsKey(spot) && GetMapData().Walls.ContainsKey(spot)
            && (tile & Program.BIT_DOOR) == 0 && (tile & ~Program.BIT_WALL) != 0;
    }

    /// <summary>The diagonal marker on this tile, if its shape came from one.</summary>
    internal MapDiagonalTranslation? GetDiagonal(int x, int y) =>
        wallshape[x, y] != WallShape.Square && GetMapData().Diagonals.TryGetValue(MAPSPOT(x, y, 1), out var diagonal)
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
        wallshape[x, y] = shape;
        return marker;
    }

    internal record MapPlayerStart(int TileX, int TileY, int Angle);

    /// <summary>Where the loaded map puts the player (its object-plane player start); null if it has none.</summary>
    internal MapPlayerStart? PlayerStart { get; private set; }

    /// <summary>The map's deathmatch starts (mapdefs player-starts with `deathmatch: true`), if it has any</summary>
    internal List<MapPlayerStart> DeathmatchStarts { get; } = [];

    /// <summary>Whether the map's enemies are left out as it loads (a deathmatch without monsters)</summary>
    internal bool NoMonsters { get; set; }

    /// <summary>The trigger on this tile, if its object-plane tile is one in the mapdefs' triggers.</summary>
    internal MapTriggerTranslation? GetTrigger(int x, int y) =>
        GetMapData().Triggers.TryGetValue(MAPSPOT(x, y, 1), out var trigger) ? trigger : null;

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
        var builtActor = actorMetaData.CreateActor(thing.Class, actor); // TODO: Should this just create objects?
        if (builtActor == null)
            return null;

        // A random spawner (`spawn.random: [A, B]`) is one of its classes, picked at random
        // (Blake Stone's bio-techs: half of them informants)
        if (builtActor.PropertyStrings("spawn.random") is { Count: > 0 } choices)
        {
            var choice = choices[Program.US_RndT() % choices.Count];
            if (!actorMetaData.Actors.TryGetValue(choice, out var chosen) || actorMetaData.CreateActor(choice, chosen) is not { } chosenActor)
            {
                Console.WriteLine($"{thing.Class}'s spawn.random names \"{choice}\", which isn't an actor");
                return null;
            }
            builtActor = chosenActor;
        }

        // A deathmatch without monsters (Program.Deathmatch.cs) leaves the map's enemies out
        if (NoMonsters && builtActor is Entities.Actors.Monster)
            return null;

        builtActor.SetPosition(tilex, tiley);

        // SpawnNewObj (Program.WL_STATE.cs) always set areanumber from the spawn tile for
        // every actor, not just ones on an ambush tile -- without this, AreaNumber sits at
        // its byte default (0, a real area index, not an "unset" sentinel) until the actor
        // first moves through TryWalk, which recomputes it correctly. Until then, anything
        // gated on AreaNumber (SightPlayer's areabyplayer connectivity check, T_Shoot/
        // CheckSight's area check) reads the wrong area, e.g. a stationary/ambushed actor
        // that never patrols would never notice the player if area 0 isn't connected.
        builtActor.AreaNumber = SpawnArea(tilex, tiley);
        builtActor.Tag = GetTag(tilex, tiley);

        // Angles: 0=east, 45=northeast, 90=north ... 315=southeast, in objdirtypes order
        // (enemies only face the four cardinal ones, patrol points all eight); -1 faces no way
        // at all, so it sees all round (Blake Stone's aliens)
        builtActor.Dir = thing.Angles < 0 ? objdirtypes.nodir : (objdirtypes)((thing.Angles / 45 % 8 + 8) % 8);

        // Patrol selects the initial resolved state (mirrors SpawnStand vs SpawnPatrol):
        // "Path" for patrolling grunts, otherwise whatever CreateActor already set ("Spawn").
        if (thing.Patrol != 0 && builtActor.ResolvedStates.TryGetValue("Path", out var pathState))
        {
            builtActor.ArmState(pathState);
            builtActor.Distance = (int)MapConstants.TILEGLOBAL;
            builtActor.Active = activetypes.ac_yes;     // a patroller walks wherever the player is

            // As SpawnPatrol did: it's already on its way to the next tile, which is its tile from
            // now on (it leaves the spawn tile's centre), or reaching it would snap it back a tile
            switch (builtActor.Dir)
            {
                case objdirtypes.east: builtActor.TileX++; break;
                case objdirtypes.north: builtActor.TileY--; break;
                case objdirtypes.west: builtActor.TileX--; break;
                case objdirtypes.south: builtActor.TileY++; break;
            }
            builtActor.SyncPosition();
        }

        // AMBUSH actors (the bosses, the Pac-Man ghosts) are always spawned ambush-ready,
        // whatever the floor tile beneath them, unlike grunts, which only ambush when placed
        // on an ambush tile.
        var alwaysAmbush = builtActor.Flags.Contains("AMBUSH", StringComparer.OrdinalIgnoreCase);
        var hasHealth = builtActor.Properties.ContainsKey("health") || builtActor.Properties.Keys.Any(k => k.StartsWith("health.", StringComparison.Ordinal));

        if (!hasHealth && builtActor.Properties.ContainsKey("speed"))
        {
            // A mover with no health (the Pac-Man ghosts, matching SpawnGhosts never assigning
            // hitpoints): never shootable, and not counted toward the kill ratio.
            builtActor.Speed = ReadIntProperty(builtActor, "speed", Program.SPDDOG);
            if (alwaysAmbush)
                builtActor.RuntimeFlags |= objflags.FL_AMBUSH;
        }
        else if (hasHealth)
        {
            // A grunt or boss enemy (has scaled health), as opposed to a plain decoration/pickup.
            builtActor.Hitpoints = GetScaledHealth(builtActor);
            builtActor.Speed = ReadIntProperty(builtActor, "speed", Program.SPDPATROL);
            builtActor.RuntimeFlags |= objflags.FL_SHOOTABLE;

            // Every killable enemy counts toward the level's kill ratio (the old SpawnStand/
            // SpawnPatrol/boss spawners each did this); ghosts take the branch above and don't,
            // and nor do those that aren't kills (Monster.IsKill: NOTCOUNTED ones, informants)
            if (countKill && !Program.loadedgame && (builtActor is not Entities.Actors.Monster m || m.IsKill))
                Program.gamestate.killtotal++;

            // Points only for the first kill (Monster.Kill clears it)
            if (builtActor.Flags.Contains("POINTSONCE", StringComparer.OrdinalIgnoreCase))
                builtActor.RuntimeFlags |= objflags.FL_BONUS;

            if (alwaysAmbush)
            {
                builtActor.RuntimeFlags |= objflags.FL_AMBUSH;

                // The legacy boss spawners (SpawnGift, SpawnBoss, ...) all face nodir, which
                // CheckSight takes as seeing all around; a map facing would blind them to a
                // player approaching from behind.
                builtActor.Dir = objdirtypes.nodir;
            }
            else
            {
                // Grunts only ambush if placed directly on an ambush floor tile (SpawnStand
                // in Program.WL_ACT2.cs), unlike AMBUSH actors, which always are.
                var floorTile = MAPSPOT(tilex, tiley, 0);
                if (floorTile == Floors.AmbushTile)
                {
                    TakeNeighbourArea(builtActor, tilex, tiley);
                    builtActor.RuntimeFlags |= objflags.FL_AMBUSH;
                }
            }

            // A floors actor-code under it (Planet Strike's): what it carries, its cloak, its link
            if (Floors.ActorCodes.TryGetValue(MAPSPOT(tilex, tiley, 0), out var code))
            {
                if (!string.IsNullOrEmpty(code.Drop))
                    builtActor.CarriedDrops.Add(code.Drop);
                builtActor.Cloaked |= code.Cloak;
                if (code.Ambush)
                    builtActor.RuntimeFlags |= objflags.FL_AMBUSH;
                if (code.Link.Equals("east", StringComparison.OrdinalIgnoreCase) && tilex + 1 < mapwidth)
                {
                    builtActor.DeathLink = (ushort)MAPSPOT(tilex + 1, tiley, 1);
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
        // (Program.WL_AGENT.cs) bumps treasurecount on pickup off the same flag.
        if (!Program.loadedgame && builtActor.Flags.Contains("COUNTITEM", StringComparer.OrdinalIgnoreCase))
            Program.gamestate.treasuretotal++;

        //if (builtActor.Properties.Keys.Any(x => x.StartsWith("inventory")))
        //{
        //    newstatobj.flags = objflags.FL_BONUS;
        //}

        // Solid statics (barrels, pillars, tables, ...; `flags: [SOLID]` in actordefs, inherited
        // through `parent:`) make their whole tile impassable to the player, enemies and
        // projectiles, as the original's `block` statics did. The blocking marker is the same
        // "something is here" entry walls and doors use, so every actorat[,] check sees it.
        if (builtActor.Flags.Any(f => f.Equals("SOLID", StringComparison.OrdinalIgnoreCase)))
            actorat[tilex, tiley] = new BlockingActor();
        else if (Program.IsWallSprite(builtActor) && actorat[tilex, tiley] == null)
            actorat[tilex, tiley] = new WallSpriteBlocker();

        _actors.AddLast(builtActor);
        return builtActor;
    }

    // A floor code that isn't an area (ambush, actor-codes) under an actor becomes a neighbouring
    // tile's area code, which the actor is then in (SpawnStand's ambush handling)
    private void TakeNeighbourArea(Entities.Actors.Actor actor, int tilex, int tiley) =>
        actor.AreaNumber = (byte)(ClearFloorCode(tilex, tiley) - Floors.AreaTile);

    /// <summary>Turns a floor code that isn't an area into a neighbouring tile's area code, which it returns.</summary>
    internal int ClearFloorCode(int tilex, int tiley)
    {
        var floorTile = MAPSPOT(tilex, tiley, 0);
        if (VALIDAREA(MAPSPOT(tilex + 1, tiley, 0)))
            floorTile = MAPSPOT(tilex + 1, tiley, 0);
        if (VALIDAREA(MAPSPOT(tilex, tiley - 1, 0)))
            floorTile = MAPSPOT(tilex, tiley - 1, 0);
        if (VALIDAREA(MAPSPOT(tilex, tiley + 1, 0)))
            floorTile = MAPSPOT(tilex, tiley + 1, 0);
        if (VALIDAREA(MAPSPOT(tilex - 1, tiley, 0)))
            floorTile = MAPSPOT(tilex - 1, tiley, 0);

        SetMapSpot(tilex, tiley, 0, (ushort)floorTile);
        return floorTile;
    }

    private static int ReadIntProperty(Entities.Actors.Actor actor, string key, int fallback) =>
        actor.Properties.TryGetValue(key, out var value) ? Convert.ToInt32(value) : fallback;

    // Runtime enemy-to-enemy morph (A_HitlerMorph, Monster.HitlerMorph): spawns a new enemy
    // already in its Chase state at the dying source actor's exact position/facing, rather
    // than going through the tile/mapdefs-driven SpawnThing path.
    /// <summary>
    /// SpawnNewObj's first countdown for a new actor's frame: a random part of the frame's tics
    /// (none for a frame that holds). Demos keep v1.4's 0 to tics-1, which can end the frame on
    /// the first tic; otherwise 1 to tics, Wolf4SDL's fix for that (Chris' "moonwalk" fix).
    /// </summary>
    internal static short SpawnTicCount(Entities.Actors.ActorStateFrame? frame)
    {
        if (frame == null || frame.HoldsForever || frame.TicTime <= 0)
            return (short)Math.Max(frame?.TicTime ?? 0, (short)0);

        var count = Program.US_RndT() % frame.TicTime;
        return (short)(Program.demorecord || Program.demoplayback ? count : count + 1);
    }

    internal Entities.Actors.Actor? SpawnMorphedEnemy(string className, Entities.Actors.Actor source)
    {
        var actorMetaData = assetManager.Value.GetActorMetadata();
        if (!actorMetaData.Actors.TryGetValue(className, out var actor))
            return null;

        var builtActor = actorMetaData.CreateActor(className, actor);
        if (builtActor == null)
            return null;

        builtActor.SetPosition(source.TileX, source.TileY);
        builtActor.X = source.X;
        builtActor.Y = source.Y;
        builtActor.Distance = source.Distance;
        builtActor.Dir = source.Dir;
        builtActor.AreaNumber = source.AreaNumber;
        // Hitler stuck-with-nodir fix (Program.WL_ACT2.cs's A_HitlerMorph): the morphed
        // actor must remain markable even if the dying source had FL_NONMARK set.
        builtActor.RuntimeFlags = (source.RuntimeFlags & ~objflags.FL_NONMARK) | objflags.FL_SHOOTABLE;
        builtActor.Hitpoints = GetScaledHealth(builtActor);

        if (builtActor.ResolvedStates.TryGetValue("Chase", out var chaseState))
        {
            builtActor.ArmState(chaseState);
            builtActor.TicCount = SpawnTicCount(chaseState);
        }
        if (builtActor is Entities.Actors.Monster morphed)
            MarkActorTile(morphed);

        _actors.AddLast(builtActor);
        return builtActor;
    }

    // Projectiles and their smoke trail spawn every few tics, and AssetManager.GetActorMetadata
    // rebuilds its result on every call, so keep one for the runtime spawners below.
    private ActorMetadata? _runtimeActorMetadata;

    /// <summary>
    /// Spawns a runtime projectile or effect (Rocket, Smoke, Needle, Fire --
    /// actordefs/wolf3d/projectiles.yaml) at another actor's exact fixed-point position. It is
    /// an "active" actor (free-moving, drawn through the exact-position path, never marked in
    /// actorat); callers set Angle/Speed.
    /// </summary>
    internal Entities.Actors.Actor? SpawnAtActor(string className, Entities.Actors.Actor source)
    {
        var builtActor = CreateRuntimeActor(className);
        if (builtActor == null)
            return null;

        builtActor.SetPosition(source.TileX, source.TileY);
        builtActor.X = source.X;
        builtActor.Y = source.Y;
        builtActor.Dir = objdirtypes.nodir;
        builtActor.Active = activetypes.ac_yes;
        builtActor.RuntimeFlags = objflags.FL_NEVERMARK;

        _actors.AddLast(builtActor);
        return builtActor;
    }

    private ActorMetadata RuntimeActorMetadata => _runtimeActorMetadata ??= assetManager.Value.GetActorMetadata();

    /// <summary>Builds an actordefs class in its Spawn state, without placing it or adding it to _actors.</summary>
    private Entities.Actors.Actor? CreateRuntimeActor(string className) =>
        RuntimeActorMetadata.Actors.TryGetValue(className, out var actor)
            ? RuntimeActorMetadata.CreateActor(className, actor)
            : null;

    // Skill-scaled health: the property the skill's enemy-health names (e.g. "health.normal"),
    // for enemies whose hitpoints vary by skill (vanilla's starthitpoints table), or a flat
    // "health" for the rest.
    internal short GetScaledHealth(Entities.Actors.Actor actor)
    {
        if (!string.IsNullOrEmpty(_enemyHealthKey) && actor.Properties.TryGetValue(_enemyHealthKey, out var scaled))
            return (short)Convert.ToInt32(scaled);

        return actor.Properties.TryGetValue("health", out var flat) ? (short)Convert.ToInt32(flat) : (short)0;
    }

    internal bool VALIDAREA(int x) => (x) >= Floors.AreaTile && (x) < (Floors.AreaTile + Floors.NumAreas);

    /// <summary>The area on a tile, or -1 for none (off the map, or a floor code that isn't an area)</summary>
    internal int AreaAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= MAPSIZE || y >= MAPSIZE)
            return -1;
        int spot = MAPSPOT(x, y, 0);
        return VALIDAREA(spot) ? spot - Floors.AreaTile : -1;
    }

    internal int MAPSPOT(int x, int y, int plane) => (mapsegs[(plane)][((y) << MAPSHIFT) + (x)]);
    internal void SetMapSpot(int x, int y, int plane, ushort value)
    {
        (mapsegs[(plane)][((y) << MAPSHIFT) + (x)]) = value;
    }

    // A game with no mapdefs (a stand-alone game whose mapdefs/ were taken out): its levels are
    // open floor, sealed at the edge, with the player in the middle
    private static readonly MapObjectTranslationAsset NoMapData = new();

    internal MapObjectTranslationAsset GetMapData()
    {
        //var mapSpecific = assetManager.Value.Find<MapObjectTranslationAsset>("map01/mapdefs");
        return assetManager.Value.FindInGamePackIfAny<MapObjectTranslationAsset>("mapdefs") ?? NoMapData;
    }

    internal void RemoveActor(Entities.Actors.Inventory builtActor)
    {
        _actors.Remove(builtActor);
    }

    internal LinkedList<Entities.Actors.Actor> GetActors()
    {
        return _actors;
    }

    // actorat[,] only tracks walls and doors; the questions it used to answer about actors
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
                && actor.Flags.Contains("PATROLPOINT", StringComparer.OrdinalIgnoreCase))
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

    /// <summary>True if a living (FL_SHOOTABLE) actor occupies the tile -- corpses don't count.</summary>
    /// <summary>The living (FL_SHOOTABLE) actors on a tile</summary>
    internal IEnumerable<Entities.Actors.Monster> ShootableActorsAt(int tilex, int tiley) =>
        _actors.OfType<Entities.Actors.Monster>().Where(a => !a.IsRemoved && a.TileX == tilex && a.TileY == tiley && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE));

    /// <summary>The living (FL_SHOOTABLE) actors within <paramref name="reach"/> (global units, each way) of a point, nearest first</summary>
    internal IEnumerable<Entities.Actors.Monster> ShootableActorsNear(int x, int y, long reach) =>
        _actors.OfType<Entities.Actors.Monster>().Where(a => !a.IsRemoved && a.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE)
                && Math.Abs((long)a.X - x) <= reach && Math.Abs((long)a.Y - y) <= reach)
            .OrderBy(a => Math.Max(Math.Abs((long)a.X - x), Math.Abs((long)a.Y - y)));

    /// <summary>True if a living (FL_SHOOTABLE) actor that's in the way occupies the tile</summary>
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
        actor.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE) && !actor.Flags.Contains("NOTSOLID", StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a fresh pawn for a player, replacing any they had, at the head of _actors after
    /// the players before them. Head placement keeps the players thinking ahead of every other
    /// actor, as the legacy InitActorList did by allocating the player first.
    /// </summary>
    internal Entities.Actors.PlayerPawn CreatePlayer(Entities.PlayerState state)
    {
        if (state.Pawn != null)
            _actors.Remove(state.Pawn);

        var pawn = new Entities.Actors.PlayerPawn { State = state };
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

    internal void DoActors(uint tics)
    {
        var node = _actors.First;
        while (node != null)
        {
            // A player thinks as the acting player: the game's player code is then theirs
            if (node.Value is Entities.Actors.PlayerPawn pawn)
            {
                using (Program.ActAs(pawn.State))
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
        if (ob is Entities.Actors.Monster { SleepsOutOfReach: true } && ob.Active == activetypes.ac_no
            && ob.AreaNumber < Floors.NumAreas && Program.areabyplayer[ob.AreaNumber] == 0)
            return;

        // It's lifted off its tile while it thinks, and put back on the one it ends up on
        bool marks = ob is Entities.Actors.Monster && (ob.RuntimeFlags & (objflags.FL_NONMARK | objflags.FL_NEVERMARK)) == 0;
        if (marks)
            UnmarkActorTile(ob.TileX, ob.TileY);

        // Mirrors Program.DoActor (Program.WL_PLAY.cs): a frame that holds forever (-1) only
        // runs its Think each tic; Next is never consulted again. So does one with no countdown
        // at all, as the original's ticcount 0: a demo's spawn can leave a patroller that way
        // (SpawnTicCount), walking on its first frame until it changes state.
        if (!state.HoldsForever && !(ob.TicCount == 0 && state.TicTime > 0))
        {
            ob.TicCount -= (short)tics;
            ob.AdvanceFrames();
            if (ob.IsRemoved)
                return;
            state = ob.CurrentState ?? state;
        }

        Entities.Actors.ActorActionRegistry.Invoke(state.Think, ob);

        if (ob is Entities.Actors.Monster monster && !ob.IsRemoved)
            MarkActorTile(monster);
    }

    /*
    The original kept each enemy in actorat[] too, on the tile it was on as of its last think:
    it overwrote a door's entry there (so a door can't close on it) and a door opening all the
    way cleared it. Other enemies' moves (Monster.CheckSide, CHECKDIAG), the player's
    (TryMove), and doors closing all read it from there, which demos depend on.
    */

    /// <summary>Puts a living or dead enemy in actorat[] where it stands, as DoActor did after its think</summary>
    internal void MarkActorTile(Entities.Actors.Monster ob)
    {
        if (ob.RuntimeFlags.HasFlag(objflags.FL_NEVERMARK))
            return;

        ref var spot = ref actorat[ob.TileX, ob.TileY];
        if (spot is Wall or BlockingActor)
            return;                 // never on a wall or a solid thing (or a wall sprite's tile)
        if (ob.RuntimeFlags.HasFlag(objflags.FL_NONMARK) && spot != null)
            return;                 // a corpse only takes an empty tile
        spot = ob.Mark;
    }

    /// <summary>Clears an enemy's (or a door's) entry from actorat[], as DoActor and KillActor did</summary>
    internal void UnmarkActorTile(int tilex, int tiley)
    {
        if (actorat[tilex, tiley] is ActorMark or Door)
            actorat[tilex, tiley] = null;
    }

    /// <summary>The enemy marked on a tile (MarkActorTile), if any</summary>
    internal Entities.Actors.Monster? ActorMarkAt(int tilex, int tiley) => (actorat[tilex, tiley] as ActorMark)?.Who;

    /*
    =============================================================================

                                    SAVE GAMES

    A save stores the level as it stands, not as a diff from the map file: the map planes
    (pushwalls, ambush tiles and doors rewrite them), the wall/door tilemap, what blocks each
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

        for (int x = 0; x < MAPSIZE; x++)
        {
            for (int y = 0; y < MAPSIZE; y++)
            {
                bw.Write(tilemap[x, y]);
                switch (actorat[x, y])
                {
                    case Wall wall:
                        bw.Write((byte)SavedTile.Wall);
                        bw.Write(wall.wall);
                        break;
                    case Door door:
                        bw.Write((byte)SavedTile.Door);
                        bw.Write(door.door);
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

        var tiles = new byte[MAPSIZE, MAPSIZE];
        var blocking = new Actor?[MAPSIZE, MAPSIZE];
        for (int x = 0; x < MAPSIZE; x++)
        {
            for (int y = 0; y < MAPSIZE; y++)
            {
                tiles[x, y] = br.ReadByte();
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

        return new LevelSnapshot(planes, tiles, blocking, actors);
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
            || level.Planes.Where((plane, i) => plane.Length != mapAsset.MapData[i].Length).Any())
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
        tilemap = (byte[,])level.TileMap.Clone();
        actorat = (Actor?[,])level.ActorAt.Clone();
        ReadMapInfo(fillFlats: false);      // the saved flat plane already has the map's flats
        BuildWallShapes();
        BuildWallHeights();
        BuildFlats(defaultfloor, defaultceiling);
        ZonesVersion++;

        _actors.Clear();
        ForgetPawns();

        // A save is a single player's game: its pawn is the local player's
        var local = Program.localplayer;
        var restored = new List<Entities.Actors.Actor>(level.Actors.Count);
        foreach (var saved in level.Actors)
        {
            Entities.Actors.Actor actor;
            if (saved.IsPlayer && (ownerOf?.Invoke(restored.Count) ?? local) is { } owner)
                actor = owner.Pawn = new Entities.Actors.PlayerPawn { State = owner };
            else
                actor = CreateRuntimeActor(saved.ClassName)!;

            saved.ApplyTo(actor);
            _actors.AddLast(actor);
            restored.Add(actor);
        }

        // Saves don't hold the enemies' actorat[] marks: put them back where they stand
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
    byte[,] TileMap,
    Actor?[,] ActorAt,
    List<Entities.Actors.ActorSnapshot> Actors);
