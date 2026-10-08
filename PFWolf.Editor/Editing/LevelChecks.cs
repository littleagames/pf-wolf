using PFWolf.Assets;
using PFWolf.Constants;
using PFWolf.Editor.Data;

namespace PFWolf.Editor.Editing;

public enum CheckSeverity
{
    Error,
    Warning,
}

/// <summary>Something wrong with a level, and the tile it's at (-1 when it's the level as a whole)</summary>
public sealed record LevelProblem(CheckSeverity Severity, string Message, int X = -1, int Y = -1)
{
    public bool HasTile => X >= 0;

    public override string ToString()
        => $"{(Severity == CheckSeverity.Error ? "Error" : "Warning")}: {(HasTile ? $"({X}, {Y})  " : "")}{Message}";
}

/// <summary>
/// What's wrong with a level that the game would trip over or that's likely a mistake: player
/// starts, values the mapdefs don't know, doors without walls, switches and tags that link to
/// nothing, light zones with no definition, floor on the level's edge and rooms the player
/// can't reach
/// </summary>
public static class LevelChecks
{
    /// <summary>Switch actions that act on whatever shares the switch's tag</summary>
    private static readonly string[] TaggedActions =
        ["A_OpenDoor", "A_CloseDoor", "A_ToggleDoor", "A_MoveWall", "A_SetWall", "A_Activate", "A_Deactivate", "A_SmartSwitch"];

    /// <param name="inGameInfo">Whether game-info lists the level</param>
    public static List<LevelProblem> Run(MapTiles tiles, bool inGameInfo)
    {
        var problems = new List<LevelProblem>();
        var defs = tiles.Content.MapDefs;

        if (!inGameInfo)
            problems.Add(new(CheckSeverity.Warning, "game-info doesn't list this level: only the map command gets there, and finishing it stops the game; saving the level lists it in the mod's game-info"));

        // Player starts: the game uses the last one it comes to
        var starts = new List<(int X, int Y)>();
        var unknownFloor = new Dictionary<int, (int X, int Y, int Count)>();
        var unknownObjects = new Dictionary<int, (int X, int Y, int Count)>();
        for (int y = 0; y < tiles.Height; y++)
        {
            for (int x = 0; x < tiles.Width; x++)
            {
                int tile = tiles[0, x, y], obj = tiles[1, x, y];
                var kind = tiles.KindAt(x, y);

                if (kind == TileKind.Unknown)
                    Count(unknownFloor, tile, x, y);

                if (tiles.PlayerStart(x, y) is { Deathmatch: false })
                    starts.Add((x, y));

                if (obj != 0)
                    CheckObject(tiles, problems, unknownObjects, x, y, obj, kind);

                if (kind == TileKind.Door)
                    CheckDoor(tiles, problems, x, y);

                int height = tiles[MapConstants.HEIGHTPLANE, x, y];
                if (height > 8)
                    problems.Add(new(CheckSeverity.Warning, $"height {height}: walls are 1 to 8 stories, so it's the level's height", x, y));
            }
        }

        if (starts.Count == 0)
            problems.Add(new(CheckSeverity.Error, "no player start"));
        else if (starts.Count > 1)
        {
            foreach (var (x, y) in starts.Take(starts.Count - 1))
                problems.Add(new(CheckSeverity.Warning, $"{starts.Count} player starts: the game uses the last one, at ({starts[^1].X}, {starts[^1].Y}), not this", x, y));
        }

        foreach (var (value, (x, y, count)) in unknownFloor)
            problems.Add(new(CheckSeverity.Error, $"plane 0 value {value} isn't a wall, door or floor code in the mapdefs ({count} {Tiles(count)})", x, y));
        foreach (var (value, (x, y, count)) in unknownObjects)
            problems.Add(new(CheckSeverity.Warning, $"object code {value} isn't in the mapdefs, so nothing's placed ({count} {Tiles(count)})", x, y));

        CheckTags(tiles, problems);
        CheckZones(tiles, problems);
        if (starts.Count > 0)
            CheckReach(tiles, problems, starts[^1]);

        return problems.OrderBy(problem => problem.Severity).ToList();
    }

    private static void CheckObject(MapTiles tiles, List<LevelProblem> problems, Dictionary<int, (int, int, int)> unknown, int x, int y, int obj, TileKind kind)
    {
        var defs = tiles.Content.MapDefs;
        bool solid = kind is TileKind.Wall or TileKind.Door;

        if (defs.Things.TryGetValue(obj, out var thing))
        {
            if (solid)
                problems.Add(new(CheckSeverity.Warning, $"{thing.Class} is on a {(kind == TileKind.Door ? "door" : "wall")}, so it isn't placed", x, y));
            return;
        }
        if (defs.Diagonals.ContainsKey(obj))
        {
            if (kind != TileKind.Wall)
                problems.Add(new(CheckSeverity.Warning, "a diagonal marker on a tile that isn't a wall does nothing", x, y));
            return;
        }
        if (defs.PlayerStarts.ContainsKey(obj) || defs.Triggers.ContainsKey(obj) || defs.DoorLocks.ContainsKey(obj)
            || defs.MapInfo.ContainsKey(obj >> 8))
            return;

        // The tile after a map-info code holds that code's value
        if (x > 0 && defs.MapInfo.TryGetValue(tiles[1, x - 1, y] >> 8, out var info) && MapInfoCodes.HasValue(info))
            return;

        Count(unknown, obj, x, y);
    }

    /// <summary>A door slides into the walls on either side of it</summary>
    private static void CheckDoor(MapTiles tiles, List<LevelProblem> problems, int x, int y)
    {
        var door = tiles.Door(x, y)!;
        bool Wall(int tx, int ty) => tiles.Map.Width > tx && tx >= 0 && ty >= 0 && ty < tiles.Height && tiles.KindAt(tx, ty) == TileKind.Wall;

        var (a, b, sides) = door.Vertical
            ? (Wall(x, y - 1), Wall(x, y + 1), "north and south")
            : (Wall(x - 1, y), Wall(x + 1, y), "west and east");
        if (!a || !b)
            problems.Add(new(CheckSeverity.Warning, $"door without walls on both its sides ({sides}) to slide into", x, y));
    }

    private static void CheckTags(MapTiles tiles, List<LevelProblem> problems)
    {
        var tagged = tiles.TaggedTiles();
        var switchTags = new HashSet<int>();

        for (int y = 0; y < tiles.Height; y++)
        {
            for (int x = 0; x < tiles.Width; x++)
            {
                if (tiles.Wall(x, y)?.Switch is not { } wallSwitch)
                    continue;

                int tag = tiles[MapConstants.TAGPLANE, x, y];
                bool needsTag = wallSwitch.Actions.Any(action => TaggedActions.Any(name => action.StartsWith(name, StringComparison.OrdinalIgnoreCase)));
                if (tag == 0)
                {
                    if (needsTag && string.IsNullOrEmpty(wallSwitch.Link))
                        problems.Add(new(CheckSeverity.Warning, "switch without a tag: its actions have nothing to act on", x, y));
                    continue;
                }

                switchTags.Add(tag);
                if (needsTag && tagged[tag].Count == 1)
                    problems.Add(new(CheckSeverity.Warning, $"switch with tag {tag}, which nothing else has", x, y));
            }
        }

        foreach (var (tag, tiles2) in tagged.Where(tag => !switchTags.Contains(tag.Key)))
            problems.Add(new(CheckSeverity.Warning, $"tag {tag} is on {tiles2.Count} {Tiles(tiles2.Count)}, but no switch has it", tiles2[0].X, tiles2[0].Y));
    }

    private static void CheckZones(MapTiles tiles, List<LevelProblem> problems)
    {
        var zones = tiles.Zones;
        var reported = new HashSet<int>();
        for (int y = 0; y < tiles.Height; y++)
        {
            for (int x = 0; x < tiles.Width; x++)
            {
                int zone = tiles[MapConstants.ZONEPLANE, x, y];
                if (zone != 0 && !zones.ContainsKey(zone) && reported.Add(zone))
                    problems.Add(new(CheckSeverity.Warning, $"light zone {zone} isn't defined in the level properties, so it's lit as the level", x, y));
            }
        }
    }

    /// <summary>Switch actions that move a tagged wall out of the way or change it</summary>
    private static readonly string[] WallMovingActions = ["A_MoveWall", "A_SetWall"];

    /// <summary>
    /// The open floor the player can't get to from the start: through floor and doors, and
    /// through pushwalls, walls a switch moves or changes, and diagonal walls, which open or are half open
    /// </summary>
    private static void CheckReach(MapTiles tiles, List<LevelProblem> problems, (int X, int Y) start)
    {
        var movedTags = new HashSet<int>();
        for (int y = 0; y < tiles.Height; y++)
        {
            for (int x = 0; x < tiles.Width; x++)
            {
                if (tiles.Wall(x, y)?.Switch is { } wallSwitch && tiles[MapConstants.TAGPLANE, x, y] is var tag and not 0
                    && wallSwitch.Actions.Any(action => WallMovingActions.Any(name => action.StartsWith(name, StringComparison.OrdinalIgnoreCase))))
                    movedTags.Add(tag);
            }
        }

        bool Passable(int x, int y) => tiles.KindAt(x, y) switch
        {
            TileKind.Floor or TileKind.Door => true,
            TileKind.Wall => tiles.Trigger(x, y) != null || tiles.Diagonal(x, y) != null
                             || (!tiles.IsSwitch(x, y) && movedTags.Contains(tiles[MapConstants.TAGPLANE, x, y])),
            _ => false,
        };

        var reached = Flood(tiles, start.X, start.Y, Passable);

        // Floor on the level's edge the player can get to (out of reach it does no harm). The game
        // keeps the player on the level, as id's E1M3 and Spear's floor 15 need, but it's open to the void.
        for (int y = 0; y < tiles.Height; y++)
        {
            for (int x = 0; x < tiles.Width; x++)
            {
                if (reached[y * tiles.Width + x] && (x == 0 || y == 0 || x == tiles.Width - 1 || y == tiles.Height - 1))
                    problems.Add(new(CheckSeverity.Warning, "the player can get to the level's edge here: it isn't walled in", x, y));
            }
        }

        var seen = new bool[tiles.Width * tiles.Height];
        for (int y = 0; y < tiles.Height; y++)
        {
            for (int x = 0; x < tiles.Width; x++)
            {
                if (reached[y * tiles.Width + x] || seen[y * tiles.Width + x] || tiles.KindAt(x, y) != TileKind.Floor)
                    continue;

                // A room nobody gets to: say how big, and what's in it
                var room = Flood(tiles, x, y, Passable);
                int size = 0, things = 0;
                for (int i = 0; i < room.Length; i++)
                {
                    if (!room[i])
                        continue;
                    seen[i] = true;
                    size++;
                    if (tiles.Thing(i % tiles.Width, i / tiles.Width) != null)
                        things++;
                }
                problems.Add(new(CheckSeverity.Warning,
                    $"{size} {Tiles(size)} of floor the player can't reach from the start{(things > 0 ? $", with {things} {(things == 1 ? "thing" : "things")}" : "")}", x, y));
            }
        }
    }

    private static bool[] Flood(MapTiles tiles, int x, int y, Func<int, int, bool> passable)
    {
        var reached = new bool[tiles.Width * tiles.Height];
        var queue = new Queue<(int X, int Y)>();
        reached[y * tiles.Width + x] = true;
        queue.Enqueue((x, y));
        while (queue.Count > 0)
        {
            var (tx, ty) = queue.Dequeue();
            foreach (var (nx, ny) in new[] { (tx - 1, ty), (tx + 1, ty), (tx, ty - 1), (tx, ty + 1) })
            {
                if (nx < 0 || ny < 0 || nx >= tiles.Width || ny >= tiles.Height || reached[ny * tiles.Width + nx] || !passable(nx, ny))
                    continue;
                reached[ny * tiles.Width + nx] = true;
                queue.Enqueue((nx, ny));
            }
        }
        return reached;
    }

    private static void Count(Dictionary<int, (int X, int Y, int Count)> counts, int value, int x, int y)
        => counts[value] = counts.TryGetValue(value, out var seen) ? (seen.X, seen.Y, seen.Count + 1) : (x, y, 1);

    private static string Tiles(int count) => count == 1 ? "tile" : "tiles";
}
