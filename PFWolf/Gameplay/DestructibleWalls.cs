using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Entities;
using PFWolf.Enums;

namespace PFWolf.Gameplay;

/// <summary>What breaking walls needs from the game</summary>
/// <param name="PlaySoundAt">A sound from a tile</param>
/// <param name="CompleteObjective">One more of a level objective done (Gameplay/Objectives.cs)</param>
/// <param name="AnimationTics">How long an animdefs animation that plays once lasts, in tics; null for none (or one that loops)</param>
/// <param name="MayBreak">
/// Whether a wall may break (game.onwallbreak): its tile, its wall id, the damage type that broke it
/// (null for an action or a script) and the player behind it (null for none)
/// </param>
internal sealed record DestructibleWallHooks(
    Action<string, int, int>? PlaySoundAt = null,
    Action<string>? CompleteObjective = null,
    Func<string, int?>? AnimationTics = null,
    Func<int, int, int, string?, Entities.Actors.PlayerPawn?, bool>? MayBreak = null);

/*
=============================================================================

                            DESTRUCTIBLE WALLS

Walls taken away as the level's played: at once by a switch, a floor trigger, an actor's state or
a script (A_RemoveWall, game.removewall, the removewall command), or broken by hits. A walls.yaml
wall with a `destructible:` block (MapDestructibleTranslation) lists the damage types that hurt it;
a hit of one of them adds its damage on the wall's tile (MapManager.WallDamage, saved) until the
wall's health is reached, and it breaks (as A_BreakWall breaks it): its sound, and its actions
run on its tile's tag, as a switch's; with an `animation` (animdefs, playing once) it crumbles,
solid, until that's played (MapManager.CrumblingWalls, saved; Tick); then it turns into its `to`
wall (a more cracked stage) or is gone, leaving open floor joined to the floor round it, with its
`spawn` on the tile. A script's game.onwallbreak can keep it standing.

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

        // A_BreakWall(["animation"][, "sound"]): the tagged walls break as their destructible block
        // says; one without crumbles with the animation and sound given (left out: gone at once)
        MapTriggerRegistry.Register("A_BreakWall", (trigger, args) =>
            ForTagged(trigger.Tag, (trigger.TileX, trigger.TileY), (x, y) => Break(x, y, trigger.Activator as Entities.Actors.PlayerPawn,
                animation: NonEmpty(args.ElementAtOrDefault(0)), sound: NonEmpty(args.ElementAtOrDefault(1)))));
        Entities.Actors.ActorActionRegistry.Register("A_BreakWall", (Entities.Actors.Actor ob, string[] args) =>
            ForTagged(ob.Tag, null, (x, y) => Break(x, y, null, animation: NonEmpty(args.ElementAtOrDefault(0)), sound: NonEmpty(args.ElementAtOrDefault(1)))));

        // A_DamageWall(damage[, "type"]): the tagged destructible walls take that much, as a hit of
        // the type would deal (left out: whatever types they list), breaking once it's their health
        MapTriggerRegistry.Register("A_DamageWall", (trigger, args) => DamageTagged(trigger.Tag, (trigger.TileX, trigger.TileY), trigger.Activator, args));
        Entities.Actors.ActorActionRegistry.Register("A_DamageWall", (Entities.Actors.Actor ob, string[] args) => DamageTagged(ob.Tag, null, ob, args));
    }

    static bool Arch(string[] args) => args.Any(a => a.Trim().Equals("arch", StringComparison.OrdinalIgnoreCase));

    static string? NonEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // The walls with the tag, besides the switch's own tile, that can go: false if none did
    bool RemoveTagged(ushort tag, (int X, int Y)? own, bool arch) => ForTagged(tag, own, (x, y) => Remove(x, y, arch));

    // Something done to each wall with the tag that can go, besides the switch's own tile: whether any took it
    bool ForTagged(ushort tag, (int X, int Y)? own, Func<int, int, bool> act)
    {
        bool any = false;
        foreach (var (x, y) in map.TaggedTiles(tag).Where(t => t != own).ToList())
            if (WhyNot(x, y) == null)
                any |= act(x, y);
        return any;
    }

    bool DamageTagged(ushort tag, (int X, int Y)? own, Entities.Actors.Actor? source, string[] args)
    {
        if (!int.TryParse(args.ElementAtOrDefault(0), out var damage) || damage <= 0)
        {
            Console.WriteLine($"A_DamageWall: '{args.ElementAtOrDefault(0)}' isn't an amount of damage.");
            return false;
        }
        var type = NonEmpty(args.ElementAtOrDefault(1));
        bool any = false;
        foreach (var (x, y) in map.TaggedTiles(tag).Where(t => t != own).ToList())
            if (Hurts(x, y, type, source, anyType: type == null))
            {
                Hit(x, y, type, source, () => damage, anyType: type == null);
                any = true;
            }
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
        StopCrumbling(x, y);
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

    /// <summary>
    /// Whether a hit of that damage type (with <paramref name="anyType"/>, whatever it is), by that
    /// source, hurts the wall on a tile: a destructible wall that lists the type, isn't players-only
    /// to an enemy's hit, and isn't crumbling already
    /// </summary>
    internal bool Hurts(int x, int y, string? damageType, Entities.Actors.Actor? source, bool anyType = false) =>
        DestructibleAt(x, y) is { } wall
        && (anyType || damageType is { Length: > 0 } && wall.DamageTypes.Any(type => type.Trim().Equals(damageType, StringComparison.OrdinalIgnoreCase)))
        && (!wall.PlayersOnly || PlayerBehind(source) != null)
        && WhyNot(x, y) == null && !IsCrumbling(x, y);

    /// <summary>
    /// A hit on the wall on a tile, of a damage type, by a source (a player, a missile, a blast):
    /// if the type hurts it, <paramref name="damage"/> is rolled and added to what it's taken, and
    /// it breaks once that reaches its health (or at once with none). True if it broke.
    /// </summary>
    internal bool Hit(int x, int y, string? damageType, Entities.Actors.Actor? source, Func<int> damage, bool anyType = false)
    {
        if (!Hurts(x, y, damageType, source, anyType))
            return false;

        var wall = DestructibleAt(x, y)!;
        int tile = map.TileIndex(x, y);
        int taken = 0;
        if (wall.Health > 0)
        {
            taken = map.WallDamage.GetValueOrDefault(tile) + Math.Max(damage(), 0);
            if (taken < wall.Health)
            {
                map.WallDamage[tile] = taken;
                return false;
            }
        }
        if (Break(x, y, PlayerBehind(source), damageType))
            return true;
        if (taken > 0)
            map.WallDamage[tile] = taken;       // kept standing (game.onwallbreak): the damage stays
        return false;
    }

    /// <summary>
    /// How much more damage the wall on a tile takes before it breaks (its health less what it's
    /// taken; 1 for one with no health, which any hit of its types breaks); null for a wall that
    /// isn't destructible
    /// </summary>
    internal int? HealthLeft(int x, int y) =>
        DestructibleAt(x, y) is { } wall
            ? wall.Health > 0 ? Math.Max(wall.Health - map.WallDamage.GetValueOrDefault(map.TileIndex(x, y)), 0) : 1
            : null;

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
    /// Breaks the wall on a tile as its destructible block says: unless a script's onwallbreak
    /// keeps it standing, its sound, secret, objective and actions (as <paramref name="by"/>, else
    /// the first player, used a switch there); then, after its animation if it has one, its `to`
    /// wall, or gone (its flats and spawn on the floor left). A wall without a block crumbles with
    /// <paramref name="animation"/> and <paramref name="sound"/> if given, else is just gone. False
    /// if nothing broke (<see cref="WhyNot"/>, crumbling already, or kept standing).
    /// </summary>
    internal bool Break(int x, int y, Entities.Actors.PlayerPawn? by = null, string? damageType = null, string? animation = null, string? sound = null)
    {
        if (WhyNot(x, y) != null || IsCrumbling(x, y))
            return false;
        if (hooks?.MayBreak?.Invoke(x, y, map.TileMap[x, y] & ~GameConstants.BitWall, damageType, by) == false)
            return false;

        var wall = DestructibleAt(x, y);
        map.WallDamage.Remove(map.TileIndex(x, y));
        if ((wall?.Sound is { Length: > 0 } own ? own : sound) is { } playing)
            hooks?.PlaySoundAt?.Invoke(playing, x, y);
        if (wall != null)
        {
            if (wall.Secret)
                session.GameState.SecretCount++;
            if (wall.Objective is { Length: > 0 } objective)
                hooks?.CompleteObjective?.Invoke(objective);
            RunActions(wall.Actions, x, y, by);
        }

        var crumble = wall?.Animation is { Length: > 0 } ownAnimation ? ownAnimation.Trim() : animation;
        if (crumble != null && WhyNot(x, y) == null)
        {
            if (hooks?.AnimationTics?.Invoke(crumble) is > 0 and var tics)
            {
                StartCrumbling(x, y, crumble, tics);
                return true;
            }
            Console.WriteLine($"Destructible wall at {x},{y}: '{crumble}' isn't an animdefs animation that plays once; it breaks at once.");
        }

        Finish(x, y);
        return true;
    }

    // What breaking leaves of the wall on a tile, now (its animation over): its `to` wall, or open
    // floor with its flats and spawn
    void Finish(int x, int y)
    {
        if (WhyNot(x, y) != null)
            return;     // an action took it already
        var wall = DestructibleAt(x, y);
        if (wall?.To is > 0 and < GameConstants.BitWall && map.GetMapData().Walls.ContainsKey(wall.To))
        {
            // the next stage: the door-side mark kept, as A_SetWall keeps it
            map.TileMap[x, y] = (ushort)(wall.To | (map.TileMap[x, y] & GameConstants.BitWall));
            map.ActorAt[x, y] = new Wall(wall.To);
            map.RestartAnimations(x, y, session.GameState.TimeCount);
            return;
        }

        Remove(x, y, wall?.Leaves.Trim().Equals("arch", StringComparison.OrdinalIgnoreCase) == true);
        if (wall == null)
            return;
        if (wall.FloorFlat != null || wall.CeilingFlat != null)
        {
            int flats = map.MapSpot(x, y, MapManager.FlatPlane);
            int floor = wall.FloorFlat ?? flats & 0xff, ceiling = wall.CeilingFlat ?? flats >> 8;
            map.SetFlats(x, y, (ushort)((floor & 0xff) | (ceiling & 0xff) << 8));
        }
        foreach (var className in wall.Spawn.Where(c => !string.IsNullOrWhiteSpace(c)))
            if (map.SpawnThing(x, y, new MapActorTranslation { Class = className.Trim() }, countKill: false) == null)
                Console.WriteLine($"Destructible wall at {x},{y}: '{className}' isn't an actor.");
    }

    /// <summary>Whether the wall on a tile is crumbling now</summary>
    internal bool IsCrumbling(int x, int y) => map.CrumblingWalls.Any(c => c.X == x && c.Y == y);

    // Every face of the wall shows the animation (from now: it plays once) until it's played
    void StartCrumbling(int x, int y, string animation, int tics)
    {
        int now = session.GameState.TimeCount;
        if (map.GetMapData().Walls.TryGetValue(map.TileMap[x, y] & ~GameConstants.BitWall, out var faces))
        {
            var shown = new[] { "north", "south", "east", "west" }.Select(faces.WallFace)
                .Append(map.GetDiagonal(x, y)?.Texture ?? "")
                .Where(name => name.Length > 0)
                .Select(name => map.ChangedTexture(x, y, name))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var name in shown)
                map.ChangeTexture(x, y, name, animation, now);
        }
        map.CrumblingWalls.Add(new CrumblingWall(x, y, now + tics, animation));
    }

    // A wall that stops crumbling (taken away meanwhile, or done): its faces back as they were
    void StopCrumbling(int x, int y)
    {
        foreach (var wall in map.CrumblingWalls.Where(c => c.X == x && c.Y == y).ToList())
        {
            map.CrumblingWalls.Remove(wall);
            map.UndoTextureChanges(x, y, wall.Animation);
        }
    }

    /// <summary>Each frame the world moves: the walls whose animation has played break, in the order they started</summary>
    internal void Tick()
    {
        if (map.CrumblingWalls.Count == 0)
            return;
        int now = session.GameState.TimeCount;
        foreach (var wall in map.CrumblingWalls.Where(c => now >= c.EndTic).ToList())
        {
            StopCrumbling(wall.X, wall.Y);
            Finish(wall.X, wall.Y);
        }
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
