using Wolf3D.Assets;
using Wolf3D.Constants;
using Wolf3D.Enums;
using objflags = Wolf3D.Program.objflags;

namespace Wolf3D.Managers;

/// <summary>
/// The game pack's plane 0 floor codes (mapdefs floors), resolved. See <see cref="MapFloorsTranslation"/>.
/// </summary>
internal sealed record FloorCodes(int AreaTile, int NumAreas, int AmbushTile, int SecretExitTile)
{
    public static FloorCodes From(MapFloorsTranslation floors)
    {
        if (floors.AreaStart is not { } start || floors.AreaCount is not { } count)
            throw new Exception("The mapdefs floors need an area-start and an area-count");
        if (start < 1 || count is < 1 or > 255)
            throw new Exception($"The mapdefs floors' area-start ({start}) must be 1 or more and area-count ({count}) 1 to 255");
        return new FloorCodes(start, count, floors.Ambush ?? -1, floors.SecretExit ?? -1);
    }
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

struct maptype
{
    public int[] planestart;
    public UInt16[] planelength;
    public UInt16 width;
    public UInt16 height;
    public char[] name;

    public maptype()
    {
        planestart = new Int32[MapManager.MAPPLANES];
        planelength = new UInt16[MapManager.MAPPLANES];
        name = new char[16];
    }
}

internal class MapManager
{
    internal const int MAPSHIFT = 6;
    internal const int MAPSIZE = (1 << MAPSHIFT);
    internal const int MAPAREA = MAPSIZE * MAPSIZE;

    public const int NUMMAPS = 60;
    public const int MAPPLANES = 3;         // planes in a GAMEMAPS level

    /// <summary>
    /// Planes every loaded level has: walls, objects, flats (ECWolf's floor and ceiling, see
    /// <see cref="FLATPLANE"/>) and wall heights. A GAMEMAPS level has no height plane, so it's empty.
    /// </summary>
    public const int LEVELPLANES = 4;

    private readonly Lazy<AssetManager> assetManager;

    private UInt16[][] mapsegs = new ushort[LEVELPLANES][];
    private maptype[] mapheaderseg = new maptype[NUMMAPS];

    private readonly LinkedList<Entities.Actors.Actor> _actors = new();

    // The player's pawn, also held in _actors (always at the head). Null until CreatePlayer runs
    // for the current level -- LoadMap clears _actors, which drops the previous level's pawn.
    internal Entities.Actors.PlayerPawn? Player { get; private set; }

    private int _difficulty;
    private string? _enemyHealthKey;

    public MapManager(Lazy<AssetManager> assetManager)
    {
        this.assetManager = assetManager;
    }

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

    internal const int HEIGHTPLANE = 3;

    /// <summary>
    /// ECWolf's floor and ceiling plane: the low byte of a tile's value picks its floor flat and
    /// the high byte its ceiling flat, through the mapdefs flats table (see <see cref="floorflat"/>).
    /// </summary>
    internal const int FLATPLANE = 2;

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

    private readonly HashSet<int> badwallids = new();

    private FloorCodes? _floors;

    /// <summary>The plane 0 floor codes, from the mapdefs floors.</summary>
    internal FloorCodes Floors => _floors ??= FloorCodes.From(GetMapData().Floors);

    /// <param name="difficulty">The skill's place in game-info's skills (mapdefs min-skill compares against it)</param>
    /// <param name="enemyHealth">The skill's enemy-health: the actordefs property enemies' health comes from</param>
    public void LoadMap(string mapName, int difficulty, string? enemyHealth = null)
    {
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
        Player = null;
        PlayerStart = null;

        var data = GetMapData();
        foreach (var id in data.Walls.Keys.Where(id => !IsWallId(id)))
            if (badwallids.Add(id))
                Console.WriteLine($"mapdefs wall {id} is ignored: wall ids are 1 to {Program.BIT_WALL - 1}");

        for (int y = 0; y < mapheight; y++)
        {
            for (int x = 0; x < mapwidth; x++)
            {
                int tile = MAPSPOT(x, y, 0);
                if (IsWallId(tile) && data.Walls.ContainsKey(tile) || data.Doors.ContainsKey(tile))
                {
                    // solid wall (a door's tile is made a door by SpawnDoor)
                    tilemap[x, y] = (byte)tile;
                    actorat[x, y] = new Wall(tile);// (uint)tile;
                }
                else
                {
                    // area floor
                    tilemap[x, y] = 0;
                    actorat[x, y] = null;
                }

                // TODO: SpawnDoor

                int objtile = MAPSPOT(x, y, 1);
                if (data.Things.TryGetValue(objtile, out var thingXlat))
                {
                    SpawnThing(x, y, thingXlat);
                    continue;
                }

                // The player pawn is created after the map loads (Program.InitActorList), so
                // only note where it starts; with several starts, the last one scanned wins.
                if (data.PlayerStarts.TryGetValue(objtile, out var startXlat))
                {
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

        BuildWallShapes();
        BuildWallHeights();
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

    /// <summary>The trigger on this tile, if its object-plane tile is one in the mapdefs' triggers.</summary>
    internal MapTriggerTranslation? GetTrigger(int x, int y) =>
        GetMapData().Triggers.TryGetValue(MAPSPOT(x, y, 1), out var trigger) ? trigger : null;

    public void SpawnThing(int tilex, int tiley, string className) =>
        SpawnThing(tilex, tiley, new MapActorTranslation { Class = className });

    public void SpawnThing(int tilex, int tiley, MapActorTranslation thing)
    {
        // MinSkill gates enemy availability by skill (its place in game-info's skills, as the
        // legacy ScanInfoPlane's gd_medium/gd_hard checks per tile-number range); always 0 for
        // decorations/pickups, so this is a no-op there.
        if (thing.MinSkill > _difficulty)
            return;

        var actorMetaData = assetManager.Value.GetActorMetadata();

        if (!actorMetaData.Actors.TryGetValue(thing.Class, out var actor))
            return;
        var builtActor = actorMetaData.CreateActor(thing.Class, actor); // TODO: Should this just create objects?
        if (builtActor == null)
            return;

        builtActor.SetPosition(tilex, tiley);

        // SpawnNewObj (Program.WL_STATE.cs) always set areanumber from the spawn tile for
        // every actor, not just ones on an ambush tile -- without this, AreaNumber sits at
        // its byte default (0, a real area index, not an "unset" sentinel) until the actor
        // first moves through TryWalk, which recomputes it correctly. Until then, anything
        // gated on AreaNumber (SightPlayer's areabyplayer connectivity check, T_Shoot/
        // CheckSight's area check) reads the wrong area, e.g. a stationary/ambushed actor
        // that never patrols would never notice the player if area 0 isn't connected.
        builtActor.AreaNumber = (byte)(MAPSPOT(tilex, tiley, 0) - Floors.AreaTile);

        // Angles: 0=east, 45=northeast, 90=north ... 315=southeast, in objdirtypes order
        // (enemies only face the four cardinal ones, patrol points all eight).
        builtActor.Dir = (objdirtypes)((thing.Angles / 45 % 8 + 8) % 8);

        // Patrol selects the initial resolved state (mirrors SpawnStand vs SpawnPatrol):
        // "Path" for patrolling grunts, otherwise whatever CreateActor already set ("Spawn").
        if (thing.Patrol != 0 && builtActor.ResolvedStates.TryGetValue("Path", out var pathState))
        {
            builtActor.ArmState(pathState);
            builtActor.Distance = (int)MapConstants.TILEGLOBAL;
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
            // SpawnPatrol/boss spawners each did this); ghosts take the branch above and don't.
            if (!Program.loadedgame)
                Program.gamestate.killtotal++;

            // Points only for the first kill (Program.KillActor clears it)
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
                    if (VALIDAREA(MAPSPOT(tilex + 1, tiley, 0)))
                        floorTile = MAPSPOT(tilex + 1, tiley, 0);
                    if (VALIDAREA(MAPSPOT(tilex, tiley - 1, 0)))
                        floorTile = MAPSPOT(tilex, tiley - 1, 0);
                    if (VALIDAREA(MAPSPOT(tilex, tiley + 1, 0)))
                        floorTile = MAPSPOT(tilex, tiley + 1, 0);
                    if (VALIDAREA(MAPSPOT(tilex - 1, tiley, 0)))
                        floorTile = MAPSPOT(tilex - 1, tiley, 0);

                    SetMapSpot(tilex, tiley, 0, (ushort)floorTile);
                    builtActor.AreaNumber = (byte)(floorTile - Floors.AreaTile);

                    builtActor.RuntimeFlags |= objflags.FL_AMBUSH;
                }
            }
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

        _actors.AddLast(builtActor);
    }

    private static int ReadIntProperty(Entities.Actors.Actor actor, string key, int fallback) =>
        actor.Properties.TryGetValue(key, out var value) ? Convert.ToInt32(value) : fallback;

    // Runtime enemy-to-enemy morph (A_HitlerMorph, Program.EnemyAI.cs): spawns a new enemy
    // already in its Chase state at the dying source actor's exact position/facing, rather
    // than going through the tile/mapdefs-driven SpawnThing path.
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
        }

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
    private short GetScaledHealth(Entities.Actors.Actor actor)
    {
        if (!string.IsNullOrEmpty(_enemyHealthKey) && actor.Properties.TryGetValue(_enemyHealthKey, out var scaled))
            return (short)Convert.ToInt32(scaled);

        return actor.Properties.TryGetValue("health", out var flat) ? (short)Convert.ToInt32(flat) : (short)0;
    }

    internal bool VALIDAREA(int x) => (x) >= Floors.AreaTile && (x) < (Floors.AreaTile + Floors.NumAreas);

    internal int MAPSPOT(int x, int y, int plane) => (mapsegs[(plane)][((y) << MAPSHIFT) + (x)]);
    internal void SetMapSpot(int x, int y, int plane, ushort value)
    {
        (mapsegs[(plane)][((y) << MAPSHIFT) + (x)]) = value;
    }

    internal MapObjectTranslationAsset GetMapData()
    {
        //var mapSpecific = assetManager.Value.Find<MapObjectTranslationAsset>("map01/mapdefs");
        var gameInfo = assetManager.Value.FindInGamePack<MapObjectTranslationAsset>("mapdefs");
        if (gameInfo == null)
            throw new Exception("Map data not found");
        return gameInfo;
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
    internal bool IsShootableActorAt(int tilex, int tiley)
    {
        foreach (var actor in _actors)
        {
            if (!actor.IsRemoved && actor.TileX == tilex && actor.TileY == tiley
                && actor.RuntimeFlags.HasFlag(objflags.FL_SHOOTABLE))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Creates a fresh player pawn at the head of _actors, replacing any existing one. Head
    /// placement keeps the player thinking ahead of every other actor, as the legacy
    /// InitActorList did by allocating the player first.
    /// </summary>
    internal Entities.Actors.PlayerPawn CreatePlayer()
    {
        if (Player != null)
            _actors.Remove(Player);

        Player = new Entities.Actors.PlayerPawn();
        _actors.AddFirst(Player);
        return Player;
    }

    internal void DoActors(uint tics)
    {
        var node = _actors.First;
        while (node != null)
        {
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

        // Mirrors Program.DoActor (Program.WL_PLAY.cs): a frame that holds forever (-1) only
        // runs its Think each tic; Next is never consulted again.
        if (!state.HoldsForever)
        {
            ob.TicCount -= (short)tics;
            ob.AdvanceFrames();
            if (ob.IsRemoved)
                return;
            state = ob.CurrentState ?? state;
        }

        Entities.Actors.ActorActionRegistry.Invoke(state.Think, ob);
    }

    /*
    =============================================================================

                                    SAVE GAMES

    A save stores the level as it stands, not as a diff from the map file: the map planes
    (pushwalls, ambush tiles and doors rewrite them), the wall/door tilemap, what blocks each
    tile, and every actor. Loading reloads the map first (for the parts a save doesn't hold,
    such as door lock translations) and then lays this over it.

    =============================================================================
    */

    private enum SavedTile : byte { Empty, Wall, Door, Blocking }

    /// <summary>The actors a save writes, in the order it writes them -- removed ones are dropped.</summary>
    internal List<Entities.Actors.Actor> GetSavedActors() => _actors.Where(a => !a.IsRemoved).ToList();

    // Save version 8 moved wall heights from plane 2 (now ECWolf's flats) to plane 3
    internal const int FlatsSaveVersion = 8;

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
    internal static LevelSnapshot ReadLevelState(BinaryReader br, int version)
    {
        var planes = new ushort[br.ReadCount()][];
        for (int i = 0; i < planes.Length; i++)
        {
            planes[i] = new ushort[br.ReadCount()];
            for (int j = 0; j < planes[i].Length; j++)
                planes[i][j] = br.ReadUInt16();
        }

        // An older save has three planes, with its wall heights in plane 2 (nothing had flats yet)
        if (version < FlatsSaveVersion && planes.Length == MAPPLANES)
            planes = [planes[0], planes[1], new ushort[planes[FLATPLANE].Length], planes[FLATPLANE]];

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
    internal List<Entities.Actors.Actor> RestoreLevelState(LevelSnapshot level)
    {
        mapsegs = level.Planes.Select(plane => (ushort[])plane.Clone()).ToArray();
        tilemap = (byte[,])level.TileMap.Clone();
        actorat = (Actor?[,])level.ActorAt.Clone();
        BuildWallShapes();
        BuildWallHeights();
        BuildFlats(defaultfloor, defaultceiling);

        _actors.Clear();
        Player = null;

        var restored = new List<Entities.Actors.Actor>(level.Actors.Count);
        foreach (var saved in level.Actors)
        {
            Entities.Actors.Actor actor;
            if (saved.IsPlayer)
                actor = Player = new Entities.Actors.PlayerPawn();
            else
                actor = CreateRuntimeActor(saved.ClassName)!;

            saved.ApplyTo(actor);
            _actors.AddLast(actor);
            restored.Add(actor);
        }

        // The player has to think first, as it does after a normal level load.
        _actors.Remove(Player!);
        _actors.AddFirst(Player!);

        return restored;
    }
}

internal sealed record LevelSnapshot(
    ushort[][] Planes,
    byte[,] TileMap,
    Actor?[,] ActorAt,
    List<Entities.Actors.ActorSnapshot> Actors);
