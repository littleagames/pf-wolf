using Wolf3D.Constants;
using Wolf3D.Entities.Actors;
using Wolf3D.Managers;

namespace Wolf3D;

internal partial class Program
{
    /*
    =============================================================================

                                ACTOR LIGHTS

    Actors can give off light (ActorLightInfo: actordefs `light.*` properties, and a state's
    `light:` overriding them). Each frame every lit actor adds its light to the cells of the
    light grid around it (celladd), most at the actor and none at its radius. It spreads from
    the actor's tile through open floor and open doors, and lights the tiles it reaches that
    a straight line from it can see, so it doesn't shine through walls or round corners: a
    closed door or a diagonal wall is lit, but the light goes no further. Light can't make
    anything brighter than full, so actor lights only show where the level is darker than
    that, and aren't cast at all on a level with no shading.

    The light added is in whole light levels; a 4x4 ordered dither over the cells turns the
    fraction into a pattern, so its falloff doesn't show as rings.

    =============================================================================
    */

    static readonly Dictionary<string, ActorLightInfo?> lightclasses = new(StringComparer.OrdinalIgnoreCase);

    static readonly List<int> casttiles = [];           // the tiles whose cells have light added this frame
    static readonly int[] caststamp = new int[MapManager.MAPAREA], visitstamp = new int[MapManager.MAPAREA];
    static int castframe, castvisit;
    static readonly Queue<(int X, int Y)> castqueue = new();
    static bool haslights;                              // some cell has light added this frame

    // A 4x4 Bayer matrix: each cell's threshold, in 16ths of a light level
    static readonly int[] bayer4 = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    /// <summary>An actor's light state, made the first time it's asked for, with its class's light.</summary>
    static ActorLightState LightStateOf(Entities.Actors.Actor actor)
    {
        if (actor.LightState != null)
            return actor.LightState;

        if (!lightclasses.TryGetValue(actor.Name, out var info))
            lightclasses[actor.Name] = info = ActorLightInfo.Of(actor);
        return actor.LightState = new ActorLightState { Info = info };
    }

    /// <summary>The light an actor gives off now (intensity 0-255, radius in tiles), its state and effect included.</summary>
    static (int Intensity, double Radius) CurrentLight(Entities.Actors.Actor actor)
    {
        var state = LightStateOf(actor);
        if (state.Info is not { } info)
            return (0, 0);

        var light = actor.CurrentState?.Light;
        if (light?.Off == true)
            return (0, 0);

        int intensity = light?.Intensity ?? info.Intensity;
        double radius = light?.Radius ?? info.Radius;
        if (info.Effect != ZoneEffect.None)
            intensity = LightEffects.Apply(info.Effect, intensity, info.Low ?? intensity / 2, info.Tics, info.BrightTics,
                state.Phase, state.FlickerAmount);
        return (intensity, radius);
    }

    /// <summary>Runs the actors' light effects for this tic's worth of time.</summary>
    static void TickActorLights(uint tics)
    {
        foreach (var actor in LitActors())
        {
            var state = LightStateOf(actor);
            if (state.Info is { Effect: not ZoneEffect.None } info)
                LightEffects.Tick(info.Effect, (int)tics, info.Tics, ref state.Phase, ref state.FlickerAmount, zonerandom);
        }
    }

    // The actors that might give off light: those on the level, and the weapon in hand
    static IEnumerable<Entities.Actors.Actor> LitActors()
    {
        foreach (var actor in _mapManager.GetActors())
            if (!actor.IsRemoved && LightStateOf(actor).Info != null)
                yield return actor;
        if (weaponSprite != null && LightStateOf(weaponSprite).Info != null)
            yield return weaponSprite;
    }

    /// <summary>Clears last frame's actor light from the grid and adds this frame's; called before each frame.</summary>
    static void CastActorLights()
    {
        foreach (int tile in casttiles)
        {
            int x = tile & (MapManager.MAPSIZE - 1), y = tile >> MapManager.MAPSHIFT;
            for (int cy = 0; cy < LIGHTCELLS; cy++)
                Array.Clear(celladd, (((y << LIGHTCELLSHIFT) + cy) << GRIDSHIFT) + (x << LIGHTCELLSHIFT), LIGHTCELLS);
        }
        casttiles.Clear();
        castframe++;
        haslights = false;
        if (!shading)
            return;

        foreach (var actor in LitActors())
        {
            var (intensity, radius) = CurrentLight(actor);
            // the weapon in hand isn't placed in the world: it's where the player is
            bool inhand = ReferenceEquals(actor, weaponSprite);
            CastLight(inhand ? player.X : actor.X, inhand ? player.Y : actor.Y, intensity, radius);
        }
        haslights = casttiles.Count > 0;
    }

    /// <summary>Adds a light at (lx, ly) (global units) to the cells around it, as far as it spreads.</summary>
    static void CastLight(int lx, int ly, int intensity, double radius)
    {
        float levels = intensity / 255f * MAXLIGHTLEVEL;
        long r = (long)(radius * MapConstants.TILEGLOBAL);
        if (levels <= 0 || r <= 0)
            return;

        int tx = lx >> MapConstants.TILESHIFT, ty = ly >> MapConstants.TILESHIFT;
        if (TileIndex(tx, ty) < 0)
            return;

        castvisit++;
        visitstamp[TileIndex(tx, ty)] = castvisit;
        castqueue.Enqueue((tx, ty));
        while (castqueue.Count > 0)
        {
            var (x, y) = castqueue.Dequeue();
            if ((x, y) == (tx, ty) || LightSees(x, y, lx, ly))
                LightCells(x, y, lx, ly, r, levels);
            if ((x, y) != (tx, ty) && !LightPasses(x, y))
                continue;               // the light goes no further

            foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                int next = TileIndex(nx, ny);
                if (next < 0 || visitstamp[next] == castvisit || !LightEnters(nx, ny) || !TileInReach(nx, ny, lx, ly, r))
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
    static bool LightSees(int x, int y, int lx, int ly)
    {
        const long INSET = MapConstants.TILEGLOBAL / 8;
        long x0 = (long)x << MapConstants.TILESHIFT, y0 = (long)y << MapConstants.TILESHIFT;
        long nearx = Math.Clamp(lx, x0 + INSET, x0 + MapConstants.TILEGLOBAL - INSET);
        long neary = Math.Clamp(ly, y0 + INSET, y0 + MapConstants.TILEGLOBAL - INSET);
        return ClearLine(lx, ly, nearx, neary, x, y)
            || ClearLine(lx, ly, x0 + MapConstants.TILEGLOBAL / 2, y0 + MapConstants.TILEGLOBAL / 2, x, y);
    }

    // Whether every tile a line passes through, between the light's tile and tile (x, y), lets
    // light through; checked every eighth of a tile along it
    static bool ClearLine(long fromx, long fromy, long tox, long toy, int x, int y)
    {
        int startx = (int)(fromx >> MapConstants.TILESHIFT), starty = (int)(fromy >> MapConstants.TILESHIFT);
        long dx = tox - fromx, dy = toy - fromy;
        int steps = (int)(Math.Max(Math.Abs(dx), Math.Abs(dy)) / (MapConstants.TILEGLOBAL / 8)) + 1;
        for (int i = 1; i < steps; i++)
        {
            int sx = (int)((fromx + dx * i / steps) >> MapConstants.TILESHIFT);
            int sy = (int)((fromy + dy * i / steps) >> MapConstants.TILESHIFT);
            if ((sx, sy) == (startx, starty) || (sx, sy) == (x, y))
                continue;
            if (TileIndex(sx, sy) < 0 || !LightPasses(sx, sy))
                return false;
        }
        return true;
    }

    // Light can light a tile: open floor, a door or a diagonal wall (its open half), not a solid wall
    static bool LightEnters(int x, int y)
    {
        byte tile = _mapManager.tilemap[x, y];
        return tile == 0 || (tile & BIT_DOOR) != 0 || _mapManager.wallshape[x, y] != WallShape.Square;
    }

    // Light carries on through a tile: open floor, or a door that isn't shut
    static bool LightPasses(int x, int y)
    {
        byte tile = _mapManager.tilemap[x, y];
        if (tile == 0)
            return true;
        return (tile & BIT_DOOR) != 0 && doorobjlist[tile & ~BIT_DOOR].action != dooractiontypes.dr_closed;
    }

    // Whether any of a tile is within r (global units) of (lx, ly)
    static bool TileInReach(int x, int y, int lx, int ly, long r)
    {
        long x0 = (long)x << MapConstants.TILESHIFT, y0 = (long)y << MapConstants.TILESHIFT;
        long dx = Math.Max(Math.Max(x0 - lx, lx - (x0 + MapConstants.TILEGLOBAL)), 0);
        long dy = Math.Max(Math.Max(y0 - ly, ly - (y0 + MapConstants.TILEGLOBAL)), 0);
        return dx * dx + dy * dy < r * r;
    }

    // Adds a light's share to each of a tile's cells: levels at the light, falling smoothly to none at r
    static void LightCells(int x, int y, int lx, int ly, long r, float levels)
    {
        int tile = TileIndex(x, y);
        if (caststamp[tile] != castframe)
        {
            caststamp[tile] = castframe;
            casttiles.Add(tile);
        }

        long r2 = r * r;
        int cellx0 = x << LIGHTCELLSHIFT, celly0 = y << LIGHTCELLSHIFT;
        for (int cy = celly0; cy < celly0 + LIGHTCELLS; cy++)
        {
            long dy = ((long)cy << CELLGLOBALSHIFT) + HALFCELL - ly;
            for (int cx = cellx0; cx < cellx0 + LIGHTCELLS; cx++)
            {
                long dx = ((long)cx << CELLGLOBALSHIFT) + HALFCELL - lx;
                long d2 = dx * dx + dy * dy;
                if (d2 >= r2)
                    continue;

                double t = Math.Sqrt(d2) / r;
                double falloff = 1 - t * t * (3 - 2 * t);
                int add = (int)(levels * falloff + bayer4[((cy & 3) << 2) + (cx & 3)] / 16f);
                if (add <= 0)
                    continue;

                ref byte cell = ref celladd[(cy << GRIDSHIFT) + cx];
                cell = (byte)Math.Min(cell + add, MAXLIGHTLEVEL);
            }
        }
    }
}
