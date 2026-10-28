using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities;
using PFWolf.Enums;

namespace PFWolf.Gameplay;

/// <summary>What breaking walls needs from the game</summary>
/// <param name="PlaySoundAt">A sound from a tile</param>
/// <param name="CompleteObjective">One more of a level objective done (Gameplay/Objectives.cs)</param>
internal sealed record DestructibleWallHooks(Action<string, int, int>? PlaySoundAt = null, Action<string>? CompleteObjective = null);

/*
=============================================================================

                            DESTRUCTIBLE WALLS

Walls taken away as the level's played: at once by a switch, a floor trigger, an actor's state or
a script (A_RemoveWall, game.removewall, the removewall command), or broken by hits. A walls.yaml
wall with a `destructible:` block (MapDestructibleTranslation) lists the damage types that hurt it;
a hit of one of them adds its damage on the wall's tile (MapManager.WallDamage, saved) until the
wall's health is reached, and it breaks: its sound, then it turns into its `to` wall (a more
cracked stage) or is gone, leaving open floor joined to the floor round it, with its `spawn` on
the tile; its actions run on its tile's tag, as a switch's.

The hits: a missile's or projectile's flying into it (its `damagetype`), a blast reaching it
(A_Explode: its source's damagetype, else Blast), a player's gunshot striking it (their weapon's
`weapon.damagetype`, else Bullet) and their knife's blow (else Melee). A hit's damage is only
rolled once the wall takes it, so a level without destructible walls draws the same random
numbers as before (the demos stay in step).

=============================================================================
*/

internal sealed class DestructibleWalls(MapManager map, Doors doors, GameSession session, DestructibleWallHooks? hooks = null)
{
    /// <summary>A blast's damage type when its source gives none (as Missiles.BlastDamageType)</summary>
    internal const string BlastType = "Blast";
    /// <summary>A player's gunshot's damage type when their weapon gives none</summary>
    internal const string BulletType = "Bullet";
    /// <summary>A player's knife blow's damage type when their weapon gives none</summary>
    internal const string MeleeType = "Melee";

    // North and west first: the tile takes the area there, as a door's takes the one west or north of it
    static readonly (int X, int Y)[] Sides = [(0, -1), (-1, 0), (0, 1), (1, 0)];

    internal void Register()
    {
        // A_RemoveWall(["arch"]): the tagged walls are gone at once (an actor's: those with its own
        // map tag). With arch, a tall wall only loses its bottom story, the rest staying up as an arch.
        MapTriggerRegistry.Register("A_RemoveWall", (trigger, args) => RemoveTagged(trigger.Tag, (trigger.TileX, trigger.TileY), Arch(args)));
        Entities.Actors.ActorActionRegistry.Register("A_RemoveWall", (Entities.Actors.Actor ob, string[] args) => RemoveTagged(ob.Tag, null, Arch(args)));
    }

    static bool Arch(string[] args) => args.Any(a => a.Trim().Equals("arch", StringComparison.OrdinalIgnoreCase));

    // The walls with the tag, besides the switch's own tile, that can go: false if none did
    bool RemoveTagged(ushort tag, (int X, int Y)? own, bool arch)
    {
        bool any = false;
        foreach (var (x, y) in map.TaggedTiles(tag).Where(t => t != own).ToList())
            if (WhyNot(x, y) == null)
                any |= Remove(x, y, arch);
        return any;
    }

    /// <summary>
    /// Why the wall on a tile can't be taken away, or null if it can: it has to be a solid wall
    /// (not a door, open floor or a moving pushwall), off the map's edge
    /// </summary>
    internal string? WhyNot(int x, int y)
    {
        if (!map.InMap(x, y))
            return $"{x},{y} isn't on the map";
        int tile = map.TileMap[x, y];
        if ((tile & GameConstants.BitDoor) != 0)
            return $"{x},{y} is a door";
        if (tile == GameConstants.BitWall)
            return $"{x},{y} is a moving pushwall";
        if (tile == 0)
            return $"{x},{y} isn't a wall";
        if (x == 0 || y == 0 || x == map.MapWidth - 1 || y == map.MapHeight - 1)
            return $"{x},{y} is on the map's edge";
        return null;
    }

    /// <summary>
    /// Takes the wall off a tile (one <see cref="WhyNot"/> passes). The open floor beside it gives
    /// the tile its area, and the areas it stood between are joined for good, as a door that never
    /// shuts joins them. Anything inside it (a trap shooter) goes to its Death state, or away if it
    /// has none; a secret trigger on it counts as found.
    /// </summary>
    internal bool Remove(int x, int y, bool arch = false)
    {
        if (WhyNot(x, y) != null)
            return false;

        // the areas of the open floor round it, before it's floor itself: those whose floor codes
        // are areas first (the tile takes the first), then any found round a floor code that isn't
        // one (an ambush tile, Blake Stone's door trigger)
        var areas = new List<int>();
        var nearby = new List<int>();
        foreach (var (dx, dy) in Sides)
        {
            int nx = x + dx, ny = y + dy;
            if (!map.InMap(nx, ny) || map.TileMap[nx, ny] != 0)
                continue;
            int code = map.MapSpot(nx, ny, 0);
            if (map.ValidArea(code))
                areas.Add(code - map.Floors.AreaTile);
            else if (doors.AreaNumberAt(nx, ny) is >= 0 and var near)
                nearby.Add(near);
        }
        areas = [.. areas.Concat(nearby).Distinct()];

        foreach (var actor in map.GetActors().Where(a => !a.IsRemoved && a is not Entities.Actors.PlayerPawn && a.TileX == x && a.TileY == y).ToList())
        {
            if (actor.ResolvedStates.ContainsKey("Death"))
                actor.SetState("Death");
            else
                map.MarkForRemoval(actor);
        }

        int spot = map.MapSpot(x, y, 1);
        if (map.WallShapes[x, y] == WallShape.Square && spot != 0 && map.GetMapData().Triggers.TryGetValue(spot, out var trigger))
        {
            if (trigger.Secret)
                session.GameState.SecretCount++;
            map.SetMapSpot(x, y, 1, 0);
        }

        map.WallDamage.Remove(map.TileIndex(x, y));
        map.RemoveWall(x, y, areas.Count > 0 ? areas[0] : 0, arch);
        JoinAreas(areas);
        return true;
    }

    // Each area joined to the others for good: AreaConnect counts it once more, as an open door
    void JoinAreas(List<int> areas)
    {
        var connect = session.Areas.AreaConnect;
        bool joined = false;
        for (int i = 0; i < areas.Count; i++)
            for (int j = i + 1; j < areas.Count; j++)
            {
                int a = areas[i], b = areas[j];
                if (a >= connect.GetLength(0) || b >= connect.GetLength(0))
                    continue;
                if (connect[a, b] < byte.MaxValue)
                    connect[a, b]++;
                if (connect[b, a] < byte.MaxValue)
                    connect[b, a]++;
                joined = true;
            }

        if (joined && map.Players.Any(pawn => pawn.AreaNumber < map.Floors.NumAreas))
            doors.ConnectAreas();
    }

    /// <summary>The destructible block of the wall on a tile, or null for none (or not a wall)</summary>
    internal MapDestructibleTranslation? DestructibleAt(int x, int y)
    {
        if (!map.InMap(x, y))
            return null;
        int tile = map.TileMap[x, y];
        if (tile == 0 || tile == GameConstants.BitWall || (tile & GameConstants.BitDoor) != 0)
            return null;
        return map.GetMapData().Walls.TryGetValue(tile & ~GameConstants.BitWall, out var wall) ? wall.Destructible : null;
    }

    /// <summary>The objective of each destructible wall on the level that has one, for the level's objective counts</summary>
    internal IEnumerable<string> LevelObjectives()
    {
        for (int y = 0; y < map.MapHeight; y++)
            for (int x = 0; x < map.MapWidth; x++)
                if (DestructibleAt(x, y)?.Objective is { Length: > 0 } objective)
                    yield return objective;
    }

    /// <summary>Whether a hit of that damage type, by that source, hurts the wall on a tile</summary>
    internal bool Hurts(int x, int y, string? damageType, Entities.Actors.Actor? source) =>
        damageType is { Length: > 0 } && DestructibleAt(x, y) is { } wall
        && wall.DamageTypes.Any(type => type.Trim().Equals(damageType, StringComparison.OrdinalIgnoreCase))
        && (!wall.PlayersOnly || PlayerBehind(source) != null)
        && WhyNot(x, y) == null;

    /// <summary>
    /// A hit on the wall on a tile, of a damage type, by a source (a player, a missile, a blast):
    /// if the type hurts it, <paramref name="damage"/> is rolled and added to what it's taken, and
    /// it breaks once that reaches its health (or at once with none). True if it broke.
    /// </summary>
    internal bool Hit(int x, int y, string? damageType, Entities.Actors.Actor? source, Func<int> damage)
    {
        if (!Hurts(x, y, damageType, source))
            return false;

        var wall = DestructibleAt(x, y)!;
        if (wall.Health > 0)
        {
            int tile = map.TileIndex(x, y);
            int taken = map.WallDamage.GetValueOrDefault(tile) + Math.Max(damage(), 0);
            if (taken < wall.Health)
            {
                map.WallDamage[tile] = taken;
                return false;
            }
        }
        return Break(x, y, PlayerBehind(source));
    }

    /// <summary>
    /// A missile or projectile that's flown into a wall: the first wall its box reaches into (a
    /// diagonal only on its solid side) that its damagetype hurts takes the hit
    /// </summary>
    internal bool HitByMissile(Entities.Actors.Actor missile, Func<int> damage)
    {
        var type = DamageType(missile);
        if (type == null)
            return false;

        const int Size = GameConstants.ProjSize;
        int xl = (missile.X - Size) >> MapConstants.TileShift, xh = (missile.X + Size) >> MapConstants.TileShift;
        int yl = (missile.Y - Size) >> MapConstants.TileShift, yh = (missile.Y + Size) >> MapConstants.TileShift;
        for (int y = yl; y <= yh; y++)
            for (int x = xl; x <= xh; x++)
            {
                if (!map.InMap(x, y) || map.ActorAt[x, y] is not Wall)
                    continue;
                if (map.WallShapes[x, y] is var shape and not WallShape.Square && !Diagonals.BoxHitsDiagonal(shape, x, y, missile.X, missile.Y, Size))
                    continue;
                if (Hurts(x, y, type, missile))
                    return Hit(x, y, type, missile, damage);
            }
        return false;
    }

    /// <summary>A blast (A_Explode) reaching the wall on a tile: of its source's damagetype, else Blast</summary>
    internal bool HitByBlast(Entities.Actors.Actor blast, int x, int y, Func<int> damage) =>
        Hit(x, y, DamageType(blast) ?? BlastType, blast, damage);

    // An actor's actordefs damagetype, or null
    static string? DamageType(Entities.Actors.Actor actor) =>
        actor.Properties.TryGetValue("damagetype", out var type) && type?.ToString() is { Length: > 0 } name ? name : null;

    // The player a hit comes from: them, or who fired the missile; null for an enemy's
    static Entities.Actors.PlayerPawn? PlayerBehind(Entities.Actors.Actor? source) =>
        source as Entities.Actors.PlayerPawn ?? source?.Shooter as Entities.Actors.PlayerPawn;

    /// <summary>
    /// Breaks the wall on a tile as its destructible block says: its sound, secret and objective,
    /// then its `to` wall, or gone (its flats and spawn on the floor left), then its actions, as
    /// <paramref name="by"/> (else the first player) used a switch there. A wall without a block
    /// is just gone. False if nothing can break there (<see cref="WhyNot"/>).
    /// </summary>
    internal bool Break(int x, int y, Entities.Actors.PlayerPawn? by = null)
    {
        if (WhyNot(x, y) != null)
            return false;
        var wall = DestructibleAt(x, y);
        if (wall == null)
            return Remove(x, y);

        map.WallDamage.Remove(map.TileIndex(x, y));
        if (wall.Sound is { Length: > 0 } sound)
            hooks?.PlaySoundAt?.Invoke(sound, x, y);
        if (wall.Secret)
            session.GameState.SecretCount++;
        if (wall.Objective is { Length: > 0 } objective)
            hooks?.CompleteObjective?.Invoke(objective);

        if (wall.To is > 0 and < GameConstants.BitWall && map.GetMapData().Walls.ContainsKey(wall.To))
        {
            // the next stage: the door-side mark kept, as A_SetWall keeps it
            map.TileMap[x, y] = (ushort)(wall.To | (map.TileMap[x, y] & GameConstants.BitWall));
            map.ActorAt[x, y] = new Wall(wall.To);
            map.RestartAnimations(x, y, session.GameState.TimeCount);
        }
        else
        {
            Remove(x, y, wall.Leaves.Trim().Equals("arch", StringComparison.OrdinalIgnoreCase));
            if (wall.Floor != null || wall.Ceiling != null)
            {
                int flats = map.MapSpot(x, y, MapManager.FlatPlane);
                int floor = wall.Floor ?? flats & 0xff, ceiling = wall.Ceiling ?? flats >> 8;
                map.SetFlats(x, y, (ushort)((floor & 0xff) | (ceiling & 0xff) << 8));
            }
            foreach (var className in wall.Spawn.Where(c => !string.IsNullOrWhiteSpace(c)))
                if (map.SpawnThing(x, y, new MapActorTranslation { Class = className.Trim() }, countKill: false) == null)
                    Console.WriteLine($"Destructible wall at {x},{y}: '{className}' isn't an actor.");
        }

        RunActions(wall.Actions, x, y, by);
        return true;
    }

    // A broken wall's actions, as if the player who broke it had used a switch on its tile
    void RunActions(List<string> actions, int x, int y, Entities.Actors.PlayerPawn? by)
    {
        if (actions.Count == 0 || (by ?? map.Players.FirstOrDefault()) is not { } activator)
            return;
        var activation = new TriggerActivation(x, y, PlayerMovement.FacingDir(activator.Angle), activator, map.GetTag(x, y));
        foreach (var action in actions)
            MapTriggerRegistry.Invoke(action, activation);
    }
}
