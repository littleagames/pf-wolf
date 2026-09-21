using Wolf3D.Assets;
using Wolf3D.Constants;
using objflags = Wolf3D.Program.objflags;

namespace Wolf3D.Managers;

public class MapDataConstants
{
    public const int ICONARROWS = 90;
    public const int PUSHABLETILE = 98;
    public const int EXITTILE = 99;          // at end of castle
    public const int AREATILE = 107;         // first of NUMAREAS floor tiles
    public const int NUMAREAS = 37;
    public const int ELEVATORTILE = 21;
    public const int AMBUSHTILE = 106;
    public const int ALTELEVATORTILE = 107;
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
    public const int MAPPLANES = 3;

    private readonly Lazy<AssetManager> assetManager;

    private UInt16[][] mapsegs = new ushort[MAPPLANES][];
    private maptype[] mapheaderseg = new maptype[NUMMAPS];

    private readonly LinkedList<Entities.Actors.Actor> _actors = new();

    // The player's pawn, also held in _actors (always at the head). Null until CreatePlayer runs
    // for the current level -- LoadMap clears _actors, which drops the previous level's pawn.
    internal Entities.Actors.PlayerPawn? Player { get; private set; }

    // The unique story bosses (actordefs/wolf3d/bosses.yaml): always spawned ambush-ready and
    // stationary regardless of the floor tile beneath them, unlike the rank-and-file grunts.
    private static readonly HashSet<string> BossActorNames = new(StringComparer.Ordinal)
    {
        "Hans", "Gretel", "Schabbs", "Gift", "Fat", "FakeHitler", "MechaHitler", "RealHitler"
    };

    // The Pac-Man bonus-level ghosts (actordefs/wolf3d/ghosts.yaml): never shootable/killable,
    // matching SpawnGhosts never assigning hitpoints in the legacy source.
    private static readonly HashSet<string> GhostActorNames = new(StringComparer.Ordinal)
    {
        "Blinky", "Clyde", "Pinky", "Inky"
    };

    private int _difficulty;

    public MapManager(Lazy<AssetManager> assetManager)
    {
        this.assetManager = assetManager;
    }

    internal ushort mapwidth, mapheight;
    internal byte[,] tilemap;
    internal bool[,] spotvis;
    internal Actor?[,] actorat;

    public ushort GetTile(int x, int y, int plane)
    {
        return mapsegs[0][y * mapwidth + x];
    }

    public void LoadMap(string mapName, int difficulty)
    {
        _difficulty = difficulty;

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
        actorat = new Actor?[MAPSIZE, MAPSIZE];

        // Every actor lives in _actors and nothing else clears this list, so reloading a level
        // (death with lives left, replaying a level in a new game, etc.) would otherwise pile
        // the new level's actors on top of the previous load's instead of replacing them.
        _actors.Clear();
        Player = null;

        var data = GetMapData();

        for (int y = 0; y < mapheight; y++)
        {
            for (int x = 0; x < mapwidth; x++)
            {
                int tile = MAPSPOT(x, y, 0);
                if (tile < MapDataConstants.AMBUSHTILE)
                {
                    // solid wall
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
            }
        }
    }

    public void SpawnThing(int tilex, int tiley, string className) =>
        SpawnThing(tilex, tiley, new MapActorTranslation { Class = className });

    public void SpawnThing(int tilex, int tiley, MapActorTranslation thing)
    {
        // MinSkill gates enemy availability by difficulty (Program.WL_GAME.cs's old
        // ScanInfoPlane checked `gamestate.difficulty < difficultytypes.gd_medium/gd_hard`
        // per tile-number range); always 0 for decorations/pickups, so this is a no-op there.
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
        builtActor.AreaNumber = (byte)(MAPSPOT(tilex, tiley, 0) - MapDataConstants.AREATILE);

        // Angles: 0=east, 90=north, 180=west, 270=south (Program.WL_GAME.cs's
        // `(objdirtypes)(dir * 2)`, dir 0..3 over east/north/west/south).
        builtActor.Dir = (thing.Angles / 90) switch
        {
            1 => objdirtypes.north,
            2 => objdirtypes.west,
            3 => objdirtypes.south,
            _ => objdirtypes.east,
        };

        // Patrol selects the initial resolved state (mirrors SpawnStand vs SpawnPatrol):
        // "Path" for patrolling grunts, otherwise whatever CreateActor already set ("Spawn").
        if (thing.Patrol != 0 && builtActor.ResolvedStates.TryGetValue("Path", out var pathState))
        {
            builtActor.CurrentState = pathState;
            builtActor.TicCount = pathState.TicTime;
            builtActor.Distance = (int)MapConstants.TILEGLOBAL;
        }

        var isBoss = BossActorNames.Contains(thing.Class);
        var isGhost = GhostActorNames.Contains(thing.Class);

        if (isGhost)
        {
            builtActor.Speed = ReadIntProperty(builtActor, "speed", Program.SPDDOG);
            builtActor.RuntimeFlags |= objflags.FL_AMBUSH;
        }
        else if (isBoss || builtActor.Properties.ContainsKey("health") || builtActor.Properties.Keys.Any(k => k.StartsWith("health.", StringComparison.Ordinal)))
        {
            // A grunt or boss enemy (has scaled health), as opposed to a plain decoration/pickup.
            builtActor.Hitpoints = GetScaledHealth(builtActor);
            builtActor.Speed = ReadIntProperty(builtActor, "speed", Program.SPDPATROL);
            builtActor.RuntimeFlags |= objflags.FL_SHOOTABLE;

            // Every killable enemy counts toward the level's kill ratio (the old SpawnStand/
            // SpawnPatrol/boss spawners each did this); ghosts take the branch above and don't.
            if (!Program.loadedgame)
                Program.gamestate.killtotal++;

            if (isBoss)
            {
                builtActor.RuntimeFlags |= objflags.FL_AMBUSH;
            }
            else
            {
                // Grunts only ambush if placed directly on an ambush floor tile (SpawnStand
                // in Program.WL_ACT2.cs), unlike bosses which are always ambush-ready.
                var floorTile = MAPSPOT(tilex, tiley, 0);
                if (floorTile == MapDataConstants.AMBUSHTILE)
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
                    builtActor.AreaNumber = (byte)(floorTile - MapDataConstants.AREATILE);

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
            builtActor.CurrentState = chaseState;
            builtActor.TicCount = chaseState.TicTime;
        }

        _actors.AddLast(builtActor);
        return builtActor;
    }

    // Projectiles and their smoke trail spawn every few tics, and AssetManager.GetActorMetadata
    // rebuilds its result on every call, so keep one for the runtime spawners below.
    private ActorMetadata? _runtimeActorMetadata;

    /// <summary>
    /// Spawns a runtime projectile or effect (Rocket, Smoke, Boom, Needle, Fire --
    /// actordefs/wolf3d/projectiles.yaml) at another actor's exact fixed-point position. It is
    /// an "active" actor (free-moving, drawn through the exact-position path, never marked in
    /// actorat); callers set Angle/Speed.
    /// </summary>
    internal Entities.Actors.Actor? SpawnAtActor(string className, Entities.Actors.Actor source)
    {
        _runtimeActorMetadata ??= assetManager.Value.GetActorMetadata();
        if (!_runtimeActorMetadata.Actors.TryGetValue(className, out var actor))
            return null;

        var builtActor = _runtimeActorMetadata.CreateActor(className, actor);
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

    private static readonly string[] DifficultyHealthKeys = ["health.baby", "health.easy", "health.normal", "health.hard"];

    // Difficulty-scaled health: "health.baby"/"health.easy"/"health.normal"/"health.hard" for
    // enemies whose hitpoints vary by skill (Program.WL_ACT2.cs's starthitpoints table), or a
    // flat "health" for the rest. _difficulty is difficultytypes' ordinal (0=baby..3=hard).
    private short GetScaledHealth(Entities.Actors.Actor actor)
    {
        var key = DifficultyHealthKeys[Math.Clamp(_difficulty, 0, 3)];
        if (actor.Properties.TryGetValue(key, out var scaled))
            return (short)Convert.ToInt32(scaled);

        return actor.Properties.TryGetValue("health", out var flat) ? (short)Convert.ToInt32(flat) : (short)0;
    }

    internal static bool VALIDAREA(int x) => (x) >= MapDataConstants.AREATILE && (x) < (MapDataConstants.AREATILE + MapDataConstants.NUMAREAS);

    internal int MAPSPOT(int x, int y, int plane) => (mapsegs[(plane)][((y) << MAPSHIFT) + (x)]);
    internal void SetMapSpot(int x, int y, int plane, ushort value)
    {
        (mapsegs[(plane)][((y) << MAPSHIFT) + (x)]) = value;
    }

    internal MapObjectTranslationAsset GetMapData()
    {
        //var mapSpecific = assetManager.Value.Find<MapObjectTranslationAsset>("map01/mapdefs");
        var gameInfo = assetManager.Value.Find<MapObjectTranslationAsset>("wolf3d/mapdefs");
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

        // Mirrors Program.DoActor (Program.WL_PLAY.cs): a frame with TicTime == 0 holds forever
        // -- once TicCount sticks at 0, only Think runs each tic, Next is never consulted again.
        if (ob.TicCount == 0)
        {
            Entities.Actors.ActorActionRegistry.Invoke(state.Think, ob);
            return;
        }

        ob.TicCount -= (short)tics;
        while (ob.TicCount <= 0)
        {
            Entities.Actors.ActorActionRegistry.Invoke(state.Action, ob);
            if (ob.IsRemoved)
                return;

            state = state.Next;
            if (state == null)
                return; // the resolver never leaves Next null in practice; defensive only.
            ob.CurrentState = state;

            if (state.TicTime == 0)
            {
                ob.TicCount = 0;
                break;
            }
            ob.TicCount += state.TicTime;
        }

        Entities.Actors.ActorActionRegistry.Invoke(state.Think, ob);
    }
}
