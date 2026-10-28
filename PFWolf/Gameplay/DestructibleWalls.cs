using PFWolf.Entities;

namespace PFWolf.Gameplay;

/// <summary>
/// Walls taken away as the level's played: by a switch, a floor trigger, an actor's state or a
/// script (A_RemoveWall, game.removewall, the removewall command). What's left is open floor,
/// joined to the floor round it (docs/dev/destructible-walls-plan.md).
/// </summary>
internal sealed class DestructibleWalls(MapManager map, Doors doors, GameSession session)
{
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
}
