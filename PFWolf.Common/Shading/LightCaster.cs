namespace PFWolf.Shading;

/// <summary>What casting light asks of a level: which tiles are on it, which light can light, and which it carries on through</summary>
public interface ILightMap
{
    /// <summary>The index of a tile in the light grid's tiles (y × width + x), or -1 off the level</summary>
    int TileIndexOrNone(int x, int y);

    /// <summary>Light can light a tile: open floor, a door or a diagonal wall (its open half), not a solid wall</summary>
    bool LightEnters(int x, int y);

    /// <summary>Light carries on through a tile: open floor, or a door that isn't shut</summary>
    bool LightPasses(int x, int y);
}

/// <summary>
/// Casts lights into a <see cref="LightGrid"/>'s cells, as the game's actor lights do each frame
/// (the game's Rendering/ActorLights.cs, which says what lights and where; the editor's 3D view
/// and actor preview, which cast the same way). A light adds to the cells around it, most at
/// the light and none at its radius. It spreads from the light's tile through open floor and
/// open doors, and lights the tiles it reaches that a straight line from it can see, so it
/// doesn't shine through walls or round corners: a closed door or a diagonal wall is lit, but
/// the light goes no further.
///
/// The light added is in whole light levels; a 4x4 ordered dither over the cells turns the
/// fraction into a pattern, so its falloff doesn't show as rings.
///
/// A coloured light tints what it lights. Where lights overlap, their light adds up, but a cell
/// takes the tint of the strongest light there (CellTint, by CellStrong). The tint fades toward
/// white over TintSteps steps as the light falls off, so a coloured pool doesn't end in a hard edge.
///
/// A set light doesn't add: what it reaches takes its intensity as its light, as a light zone
/// would (CellSet, the steps from each cell's base light to its own), darker than the level as
/// well as lighter. It holds all of it out to half its radius and blends into the light round
/// it over the rest; where set lights overlap, the one that holds a cell more has it.
/// </summary>
public sealed class LightCaster(LightGrid grid)
{
    const int LightCellCount = LightGrid.LightCells, LightCellShift = LightGrid.LightCellShift;
    const int CellGlobalShift = LightGrid.CellGlobalShift, MaxLightLevel = LightGrid.MaxLightLevel;
    const int HalfCell = 1 << (CellGlobalShift - 1);

    /// <summary>How many paler steps a light's tint takes as it falls off</summary>
    public const int TintSteps = 4;

    readonly List<int> casttiles = [];           // the tiles whose cells have light added this frame
    int[] caststamp = new int[MapConstants.ClassicMapArea], visitstamp = new int[MapConstants.ClassicMapArea];
    int castwidth = MapConstants.ClassicMapSize;       // the level width casttiles and the stamps are for
    int castframe, castvisit;
    readonly Queue<(int X, int Y)> castqueue = new();

    // A 4x4 Bayer matrix: each cell's threshold, in 16ths of a light level
    static readonly int[] bayer4 = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    public LightGrid Grid => grid;

    /// <summary>How many tiles have light on their cells since <see cref="BeginFrame"/></summary>
    public int LitTiles => casttiles.Count;

    /// <summary>
    /// Clears what was cast since the last time from the grid's cells, for a level of this
    /// size (a level of another size starts afresh, the grid already sized to it), ready to cast again
    /// </summary>
    public void BeginFrame(int mapArea, int mapWidth)
    {
        if (caststamp.Length != mapArea || castwidth != mapWidth)
        {
            caststamp = new int[mapArea];
            visitstamp = new int[mapArea];
            castwidth = mapWidth;
            casttiles.Clear();
        }

        foreach (int tile in casttiles)
        {
            int x = tile % castwidth, y = tile / castwidth;
            for (int cy = 0; cy < LightCellCount; cy++)
            {
                int start = grid.CellIndex(x << LightCellShift, (y << LightCellShift) + cy);
                Array.Clear(grid.CellAdd, start, LightCellCount);
                Array.Clear(grid.CellStrong, start, LightCellCount);
                Array.Clear(grid.CellTint, start, LightCellCount);
                Array.Clear(grid.CellSet, start, LightCellCount);
                Array.Clear(grid.CellSetWeight, start, LightCellCount);
            }
        }
        casttiles.Clear();
        castframe++;
    }

    /// <summary>
    /// A light colour's tints (indexes from <paramref name="tintIndex"/>), a quarter of it to all
    /// of it, kept in <paramref name="cache"/>; null for white (no colour, or not #RRGGBB)
    /// </summary>
    public static byte[]? ColorTints(string? color, Dictionary<string, byte[]> cache, Func<(byte, byte, byte), byte> tintIndex)
    {
        if (string.IsNullOrEmpty(color))
            return null;
        if (cache.TryGetValue(color, out var steps))
            return steps.Length == 0 ? null : steps;

        var (r, g, b) = ShadingResolver.ParseRgb(color, ShadingResolver.NoTint, "light color");
        steps = (r, g, b) == ShadingResolver.NoTint ? [] : new byte[TintSteps];
        for (int i = 0; i < steps.Length; i++)
        {
            float amount = (i + 1) / (float)TintSteps;
            static byte Mix(byte to, float amount) => (byte)Math.Round(255 + (to - 255) * amount);
            steps[i] = tintIndex((Mix(r, amount), Mix(g, amount), Mix(b, amount)));
        }
        cache[color] = steps;
        return steps.Length == 0 ? null : steps;
    }

    /// <summary>
    /// Adds a light at (lx, ly) (global units) to the cells around it, as far as it spreads,
    /// tinted (null: white); a set light (sets) gives them its light in place of theirs.
    /// </summary>
    public void CastLight(ILightMap map, int lx, int ly, int intensity, double radius, byte[]? tints, bool sets = false)
    {
        // a set light's is the light level a zone at its intensity would have
        float levels = sets ? ShadeMath.LightLevelOf(intensity) : intensity / 255f * MaxLightLevel;
        long r = (long)(radius * MapConstants.TileGlobal);
        // a set light at 0 is a dark one; put out (light: none, A_LightOff) its radius is 0
        if (levels <= 0 && !sets || r <= 0)
            return;

        int tx = lx >> MapConstants.TileShift, ty = ly >> MapConstants.TileShift;
        if (map.TileIndexOrNone(tx, ty) < 0)
            return;

        castvisit++;
        visitstamp[map.TileIndexOrNone(tx, ty)] = castvisit;
        castqueue.Enqueue((tx, ty));
        while (castqueue.Count > 0)
        {
            var (x, y) = castqueue.Dequeue();
            if ((x, y) == (tx, ty) || LightSees(map, x, y, lx, ly))
            {
                if (sets)
                    SetCells(map, x, y, lx, ly, r, levels, tints);
                else
                    LightCells(map, x, y, lx, ly, r, levels, tints);
            }
            if ((x, y) != (tx, ty) && !map.LightPasses(x, y))
                continue;               // the light goes no further

            foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                int next = map.TileIndexOrNone(nx, ny);
                if (next < 0 || visitstamp[next] == castvisit || !map.LightEnters(nx, ny) || !TileInReach(nx, ny, lx, ly, r))
                    continue;
                visitstamp[next] = castvisit;
                castqueue.Enqueue((nx, ny));
            }
        }
    }

    /// <summary>
    /// Whether a light at (lx, ly) shines straight on any of tile (x, y): a line to its middle,
    /// or to its point nearest the light (an eighth of a tile in from its edges), crosses
    /// nothing that stops light. Spreading tile by tile alone would let light turn corners.
    /// </summary>
    public static bool LightSees(ILightMap map, int x, int y, int lx, int ly)
    {
        const long INSET = MapConstants.TileGlobal / 8;
        long x0 = (long)x << MapConstants.TileShift, y0 = (long)y << MapConstants.TileShift;
        long nearx = Math.Clamp(lx, x0 + INSET, x0 + MapConstants.TileGlobal - INSET);
        long neary = Math.Clamp(ly, y0 + INSET, y0 + MapConstants.TileGlobal - INSET);
        return ClearLine(map, lx, ly, nearx, neary, x, y)
            || ClearLine(map, lx, ly, x0 + MapConstants.TileGlobal / 2, y0 + MapConstants.TileGlobal / 2, x, y);
    }

    // Whether every tile a line passes through, between the light's tile and tile (x, y), lets
    // light through; checked every eighth of a tile along it
    static bool ClearLine(ILightMap map, long fromx, long fromy, long tox, long toy, int x, int y)
    {
        int startx = (int)(fromx >> MapConstants.TileShift), starty = (int)(fromy >> MapConstants.TileShift);
        long dx = tox - fromx, dy = toy - fromy;
        int steps = (int)(Math.Max(Math.Abs(dx), Math.Abs(dy)) / (MapConstants.TileGlobal / 8)) + 1;
        for (int i = 1; i < steps; i++)
        {
            int sx = (int)((fromx + dx * i / steps) >> MapConstants.TileShift);
            int sy = (int)((fromy + dy * i / steps) >> MapConstants.TileShift);
            if ((sx, sy) == (startx, starty) || (sx, sy) == (x, y))
                continue;
            if (map.TileIndexOrNone(sx, sy) < 0 || !map.LightPasses(sx, sy))
                return false;
        }
        return true;
    }

    /// <summary>Whether any of a tile is within r (global units) of (lx, ly)</summary>
    public static bool TileInReach(int x, int y, int lx, int ly, long r)
    {
        long x0 = (long)x << MapConstants.TileShift, y0 = (long)y << MapConstants.TileShift;
        long dx = Math.Max(Math.Max(x0 - lx, lx - (x0 + MapConstants.TileGlobal)), 0);
        long dy = Math.Max(Math.Max(y0 - ly, ly - (y0 + MapConstants.TileGlobal)), 0);
        return dx * dx + dy * dy < r * r;
    }

    // Notes a tile as having light on its cells this frame, to clear them before the next
    void MarkCast(int tile)
    {
        if (caststamp[tile] != castframe)
        {
            caststamp[tile] = castframe;
            casttiles.Add(tile);
        }
    }

    /// <summary>How much of a set light holds a cell t of its radius out (0-1): all of it to half way, then blending to none.</summary>
    public static double SetHold(double t)
    {
        const double FULL = 0.5;
        if (t <= FULL)
            return 1;
        if (t >= 1)
            return 0;
        double u = (t - FULL) / (1 - FULL);
        return 1 - u * u * (3 - 2 * u);
    }

    // Gives a set light's light to each of a tile's cells: the steps from the tile's base light
    // to its own, as much of the way as it holds there. Where another set light holds a cell
    // more, that one keeps it; where it's the light holding most, the cell takes its tint.
    void SetCells(ILightMap map, int x, int y, int lx, int ly, long r, float levels, byte[]? tints)
    {
        int tile = map.TileIndexOrNone(x, y);
        MarkCast(tile);
        int baselevel = grid.TileLevel[tile];

        long r2 = r * r;
        int cellx0 = x << LightCellShift, celly0 = y << LightCellShift;
        for (int cy = celly0; cy < celly0 + LightCellCount; cy++)
        {
            long dy = ((long)cy << CellGlobalShift) + HalfCell - ly;
            for (int cx = cellx0; cx < cellx0 + LightCellCount; cx++)
            {
                long dx = ((long)cx << CellGlobalShift) + HalfCell - lx;
                long d2 = dx * dx + dy * dy;
                if (d2 >= r2)
                    continue;

                double hold = SetHold(Math.Sqrt(d2) / r);
                int weight = (int)Math.Round(hold * 255);
                int i = grid.CellIndex(cx, cy);
                if (weight <= 0 || weight <= grid.CellSetWeight[i])
                    continue;

                int shift = (int)Math.Floor((levels - baselevel) * hold + bayer4[((cy & 3) << 2) + (cx & 3)] / 16f);
                grid.CellSetWeight[i] = (byte)weight;
                grid.CellSet[i] = (sbyte)Math.Clamp(shift, -MaxLightLevel, MaxLightLevel);
                int strong = (int)(levels * hold);
                if (strong >= grid.CellStrong[i])
                {
                    grid.CellStrong[i] = (byte)Math.Min(strong, byte.MaxValue);
                    grid.CellTint[i] = tints == null ? (byte)0
                        : tints[Math.Clamp((int)Math.Ceiling(hold * TintSteps), 1, TintSteps) - 1];
                }
            }
        }
    }

    // Adds a light's share to each of a tile's cells: levels at the light, falling smoothly to
    // none at r. Where it's the strongest light, the cell takes its tint, paler as it falls off.
    void LightCells(ILightMap map, int x, int y, int lx, int ly, long r, float levels, byte[]? tints)
    {
        int tile = map.TileIndexOrNone(x, y);
        MarkCast(tile);

        long r2 = r * r;
        int cellx0 = x << LightCellShift, celly0 = y << LightCellShift;
        for (int cy = celly0; cy < celly0 + LightCellCount; cy++)
        {
            long dy = ((long)cy << CellGlobalShift) + HalfCell - ly;
            for (int cx = cellx0; cx < cellx0 + LightCellCount; cx++)
            {
                long dx = ((long)cx << CellGlobalShift) + HalfCell - lx;
                long d2 = dx * dx + dy * dy;
                if (d2 >= r2)
                    continue;

                double t = Math.Sqrt(d2) / r;
                double falloff = 1 - t * t * (3 - 2 * t);
                int add = (int)(levels * falloff + bayer4[((cy & 3) << 2) + (cx & 3)] / 16f);
                if (add <= 0)
                    continue;

                int i = grid.CellIndex(cx, cy);
                grid.CellAdd[i] = (byte)Math.Min(grid.CellAdd[i] + add, MaxLightLevel);
                if (add > grid.CellStrong[i])
                {
                    grid.CellStrong[i] = (byte)Math.Min(add, byte.MaxValue);
                    grid.CellTint[i] = tints == null ? (byte)0
                        : tints[Math.Clamp((int)Math.Ceiling(falloff * TintSteps), 1, TintSteps) - 1];
                }
            }
        }
    }
}
