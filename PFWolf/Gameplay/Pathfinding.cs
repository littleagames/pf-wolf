namespace PFWolf.Gameplay;

/*
=============================================================================

                                PATHFINDING

A* over the level's tiles, for the scripts' actor:pathto (and game.onchasedir hooks that use it):
the first of the eight ways to go from one tile toward another by the shortest open way. A
straight step costs 10 and a diagonal 14, and a diagonal step needs the tile it goes to and both
tiles beside it open the stricter way (no door), as an enemy's own diagonal walk does (TryWalk).

What's open is the walker's to say (Monster.CanPassTile): walls, solid things and monster
blockers are shut; other actors aren't counted, as they move on. The goal itself is always open
(someone stands there).

It comes out the same on every machine and demo run: ties go to the way first in a fixed order,
then to the tile found first. A search looks at most MaxExpanded tiles (a 256 x 256 level's far
corner); a goal it doesn't reach that way, or can't reach at all, it heads for the tile it found
nearest the goal, unless that's where it already is.

=============================================================================
*/

internal sealed class Pathfinder
{
    internal const int StraightCost = 10, DiagonalCost = 14;

    /// <summary>The most tiles one search looks at before it settles for the nearest it found</summary>
    internal const int MaxExpanded = 8192;

    // The eight ways, in the order ties go: the straight ones first
    static readonly (ObjDir Dir, int Dx, int Dy)[] Ways =
    [
        (ObjDir.East, 1, 0), (ObjDir.North, 0, -1), (ObjDir.West, -1, 0), (ObjDir.South, 0, 1),
        (ObjDir.NorthEast, 1, -1), (ObjDir.NorthWest, -1, -1), (ObjDir.SouthWest, -1, 1), (ObjDir.SouthEast, 1, 1),
    ];

    // Kept from search to search (a search's own marked by its number), so a search doesn't
    // make garbage: the cost to each tile, the tile it came from, and whether it's been reached
    // or finished in this search
    int[] _cost = [], _from = [], _reached = [], _done = [];
    int _search;
    readonly PriorityQueue<int, (int Total, int Left, int Order)> _open = new();

    /// <summary>
    /// The way to go from (fromX, fromY) toward (toX, toY) on a level width by height: null when
    /// it's there already, or nowhere it could get to is nearer the goal
    /// </summary>
    /// <param name="open">Whether a tile can be walked onto</param>
    /// <param name="openDiagonally">Whether a tile can be stepped onto (or past) diagonally; null: no diagonal steps</param>
    internal ObjDir? FirstStep(int width, int height, int fromX, int fromY, int toX, int toY,
        Func<int, int, bool> open, Func<int, int, bool>? openDiagonally = null)
    {
        if (width <= 0 || height <= 0 || !Inside(fromX, fromY) || !Inside(toX, toY) || (fromX, fromY) == (toX, toY))
            return null;
        StartSearch(width * height);

        int start = fromY * width + fromX, goal = toY * width + toX;
        int ways = openDiagonally == null ? 4 : 8;
        int order = 0;
        _cost[start] = 0;
        _from[start] = -1;
        _reached[start] = _search;
        int startLeft = Estimate(fromX, fromY);
        _open.Enqueue(start, (startLeft, startLeft, order++));

        int nearest = start, nearestLeft = startLeft, expanded = 0;
        bool found = false;
        while (_open.TryDequeue(out int tile, out var priority))
        {
            if (_done[tile] == _search)
                continue;
            _done[tile] = _search;
            if (tile == goal)
            {
                found = true;
                break;
            }
            if (priority.Left < nearestLeft)
                (nearest, nearestLeft) = (tile, priority.Left);
            if (++expanded > MaxExpanded)
                break;

            int x = tile % width, y = tile / width;
            for (int i = 0; i < ways; i++)
            {
                var (_, dx, dy) = Ways[i];
                int nx = x + dx, ny = y + dy;
                if (!Inside(nx, ny))
                    continue;
                int next = ny * width + nx;
                if (_done[next] == _search)
                    continue;
                bool diagonal = dx != 0 && dy != 0;
                if (diagonal
                    ? !(openDiagonally!(nx, ny) || next == goal) || !openDiagonally(nx, y) || !openDiagonally(x, ny)
                    : next != goal && !open(nx, ny))
                    continue;

                int cost = _cost[tile] + (diagonal ? DiagonalCost : StraightCost);
                if (_reached[next] == _search && _cost[next] <= cost)
                    continue;
                _reached[next] = _search;
                _cost[next] = cost;
                _from[next] = tile;
                int left = Estimate(nx, ny);
                _open.Enqueue(next, (cost + left, left, order++));
            }
        }
        _open.Clear();

        // Back from where it got to, to the step off the start
        int step = found ? goal : nearest;
        if (step == start)
            return null;
        while (_from[step] != start)
            step = _from[step];
        int sx = step % width - fromX, sy = step / width - fromY;
        return Ways.First(way => way.Dx == sx && way.Dy == sy).Dir;

        bool Inside(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

        // The least it could cost from a tile to the goal (octile, or Manhattan without diagonals)
        int Estimate(int x, int y)
        {
            int dx = Math.Abs(toX - x), dy = Math.Abs(toY - y);
            return openDiagonally == null
                ? StraightCost * (dx + dy)
                : StraightCost * Math.Max(dx, dy) + (DiagonalCost - StraightCost) * Math.Min(dx, dy);
        }
    }

    // A new search's number, with the buffers big enough for the level
    void StartSearch(int tiles)
    {
        if (_cost.Length < tiles)
        {
            _cost = new int[tiles];
            _from = new int[tiles];
            _reached = new int[tiles];
            _done = new int[tiles];
            _search = 0;
        }
        if (++_search == int.MaxValue)
        {
            Array.Clear(_reached);
            Array.Clear(_done);
            _search = 1;
        }
    }

    /// <summary>A way's name as scripts give and get it: "north", "northeast"... "none" for none</summary>
    internal static string DirName(ObjDir dir) => dir == ObjDir.NoDir ? "none" : dir.ToString().ToLowerInvariant();

    /// <summary>A way from its name ("north", "southwest", "none"...); null for a name that isn't one</summary>
    internal static ObjDir? ParseDir(string? name) =>
        Enum.TryParse<ObjDir>(name?.Trim(), ignoreCase: true, out var dir) && Enum.IsDefined(dir) && !int.TryParse(name, out _) ? dir
        : string.Equals(name?.Trim(), "none", StringComparison.OrdinalIgnoreCase) ? ObjDir.NoDir
        : null;
}
