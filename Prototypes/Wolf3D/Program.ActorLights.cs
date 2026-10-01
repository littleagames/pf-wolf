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

    A colored light tints what it lights. Where lights overlap, their light adds up, but a
    cell takes the tint of the strongest light there (celltint, by cellstrong). The tint
    fades toward white over TINTSTEPS steps as the light falls off, so a colored pool doesn't
    end in a hard edge.

    A_SetLight and A_LightOff change an actor's light as it plays (LightOverride, saved).

    =============================================================================
    */

    static readonly Dictionary<string, ActorLightInfo?> lightclasses = new(StringComparer.OrdinalIgnoreCase);

    const int TINTSTEPS = 4;

    // Each light color's tints (indexes in tints), from a quarter of the color to all of it;
    // forgotten with the light rows (ResetLightRows)
    static readonly Dictionary<string, byte[]> colortints = new(StringComparer.OrdinalIgnoreCase);

    static readonly List<int> casttiles = [];           // the tiles whose cells have light added this frame
    static readonly int[] caststamp = new int[MapManager.MAPAREA], visitstamp = new int[MapManager.MAPAREA];
    static int castframe, castvisit;
    static readonly Queue<(int X, int Y)> castqueue = new();
    static bool haslights;                              // some cell has light added this frame

    // A 4x4 Bayer matrix: each cell's threshold, in 16ths of a light level
    static readonly int[] bayer4 = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    /// <summary>An actor's light state, with its class's light looked up the first time it's asked for.</summary>
    static ActorLightState LightStateOf(Entities.Actors.Actor actor)
    {
        var state = actor.LightState ??= new ActorLightState();
        if (!state.Resolved)
        {
            if (!lightclasses.TryGetValue(actor.Name, out var info))
                lightclasses[actor.Name] = info = ActorLightInfo.Of(actor);
            state.Info = info;
            state.Resolved = true;
        }
        return state;
    }

    /// <summary>
    /// The light an actor gives off now (intensity 0-255, radius in tiles, color or null): its
    /// class's light or what A_SetLight set, changed by its state's `light:` and its effect.
    /// A_LightOff, or a state's `light: none`, puts it out.
    /// </summary>
    static (int Intensity, double Radius, string? Color) CurrentLight(Entities.Actors.Actor actor)
    {
        var state = LightStateOf(actor);
        var info = state.Info;
        var set = state.Override;
        var light = actor.CurrentState?.Light;
        if (set?.Off == true || light?.Off == true || (info == null && set == null))
            return (0, 0, null);

        int intensity = light?.Intensity ?? set?.Intensity ?? info!.Intensity;
        double radius = light?.Radius ?? set?.Radius ?? info!.Radius;
        string? color = light?.Color ?? (set != null ? set.Color : info!.Color);
        if (info is { Effect: not ZoneEffect.None })
            intensity = LightEffects.Apply(info.Effect, intensity, info.Low ?? intensity / 2, info.Tics, info.BrightTics,
                state.Phase, state.FlickerAmount);
        return (intensity, radius, color);
    }

    /// <summary>A light color's tints, a quarter of it to all of it; null for white (no color, or not #RRGGBB).</summary>
    static byte[]? ColorTints(string? color)
    {
        if (string.IsNullOrEmpty(color))
            return null;
        if (colortints.TryGetValue(color, out var steps))
            return steps.Length == 0 ? null : steps;

        var (r, g, b) = ParseRgb(color, NoTint, "light color");
        steps = (r, g, b) == NoTint ? [] : new byte[TINTSTEPS];
        for (int i = 0; i < steps.Length; i++)
        {
            float amount = (i + 1) / (float)TINTSTEPS;
            static byte Mix(byte to, float amount) => (byte)Math.Round(255 + (to - 255) * amount);
            steps[i] = TintIndex((Mix(r, amount), Mix(g, amount), Mix(b, amount)));
        }
        colortints[color] = steps;
        return steps.Length == 0 ? null : steps;
    }

    /*
    =============================================================================

                                    ACTIONS

    For actor states (on the actor itself) and switches (on the actors with the switch's tag):

      A_SetLight(intensity, [radius], [color])  gives the actor this light (0-255, radius in
                                                tiles, #RRGGBB or "none"); radius or color left
                                                out (or "") keep the actor's. With no arguments,
                                                puts back its class's light
      A_LightOff                                puts its light out, whatever its states say,
                                                until A_SetLight

    =============================================================================
    */

    private static void RegisterActorLightActions()
    {
        ActorActionRegistry.Register("A_SetLight", (Entities.Actors.Actor actor, string[] args) => SetLightAction(actor, args));
        ActorActionRegistry.Register("A_LightOff", (Entities.Actors.Actor actor, string[] _) => LightOff(actor));
        Entities.MapTriggerRegistry.Register("A_SetLight", (trigger, args) =>
            ForTaggedActors(trigger, actor => SetLightAction(actor, args)));
        Entities.MapTriggerRegistry.Register("A_LightOff", (trigger, _) =>
            ForTaggedActors(trigger, actor => { LightOff(actor); return true; }));
    }

    private static bool ForTaggedActors(Entities.TriggerActivation trigger, Func<Entities.Actors.Actor, bool> act)
    {
        bool any = false;
        foreach (var actor in _mapManager.TaggedActors(trigger.Tag).ToList())
            any |= act(actor);
        return any;
    }

    // The light A_SetLight starts from: what it set before, else the class's
    static (int Intensity, double Radius, string? Color) BaseLight(ActorLightState state) =>
        state.Override is { } set ? (set.Intensity, set.Radius, set.Color)
        : state.Info is { } info ? (info.Intensity, info.Radius, info.Color)
        : (0, ActorLightInfo.DefaultRadius, null);

    private static bool SetLightAction(Entities.Actors.Actor actor, string[] args)
    {
        var state = LightStateOf(actor);
        if (args.Length == 0)
        {
            state.Override = null;      // back to its class's light
            return true;
        }

        if (!int.TryParse(args[0], out var intensity) || intensity is < 0 or > 255)
        {
            Console.WriteLine($"A_SetLight: '{args[0]}' isn't an intensity (0-255).");
            return false;
        }

        var (_, radius, color) = BaseLight(state);
        var radiusArg = args.ElementAtOrDefault(1);
        if (!string.IsNullOrEmpty(radiusArg))
        {
            if (!double.TryParse(radiusArg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out radius)
                || radius < 0)
            {
                Console.WriteLine($"A_SetLight: '{radiusArg}' isn't a radius in tiles.");
                return false;
            }
        }

        switch (args.ElementAtOrDefault(2))
        {
            case null or "":
                break;
            case var none when none.Equals("none", StringComparison.OrdinalIgnoreCase):
                color = null;
                break;
            case var rgb when IsRgb(rgb):
                color = rgb;
                break;
            case var bad:
                Console.WriteLine($"A_SetLight: '{bad}' isn't a #RRGGBB color.");
                return false;
        }

        state.Override = new LightOverride(false, intensity, radius, color);
        return true;
    }

    private static void LightOff(Entities.Actors.Actor actor)
    {
        var state = LightStateOf(actor);
        var (intensity, radius, color) = BaseLight(state);
        state.Override = new LightOverride(true, intensity, radius, color);
    }

    /// <summary>The console's `lights`: each actor giving off light, and what light.</summary>
    static IEnumerable<string> DescribeActorLights()
    {
        foreach (var actor in LitActors())
        {
            var (intensity, radius, color) = CurrentLight(actor);
            var state = LightStateOf(actor);
            var where = ReferenceEquals(actor, weaponSprite) ? "in hand" : $"at {actor.TileX},{actor.TileY}";
            var text = intensity > 0 ? $"{intensity} over {radius:0.##} tiles{(color != null ? $", {color}" : "")}" : "off";
            if (state.Info is { Effect: not ZoneEffect.None } info)
                text += $", {info.Effect.ToString().ToLowerInvariant()}";
            if (state.Override != null)
                text += state.Override.Off ? " (A_LightOff)" : " (A_SetLight)";
            yield return $"{actor.Name} {where}: {text}";
        }
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
            if (!actor.IsRemoved && MightLight(actor))
                yield return actor;
        if (weaponSprite != null && MightLight(weaponSprite))
            yield return weaponSprite;
    }

    static bool MightLight(Entities.Actors.Actor actor) =>
        LightStateOf(actor) is { } state && (state.Info != null || state.Override != null);

    /// <summary>Clears last frame's actor light from the grid and adds this frame's; called before each frame.</summary>
    static void CastActorLights()
    {
        foreach (int tile in casttiles)
        {
            int x = tile & (MapManager.MAPSIZE - 1), y = tile >> MapManager.MAPSHIFT;
            for (int cy = 0; cy < LIGHTCELLS; cy++)
            {
                int start = (((y << LIGHTCELLSHIFT) + cy) << GRIDSHIFT) + (x << LIGHTCELLSHIFT);
                Array.Clear(celladd, start, LIGHTCELLS);
                Array.Clear(cellstrong, start, LIGHTCELLS);
                Array.Clear(celltint, start, LIGHTCELLS);
            }
        }
        casttiles.Clear();
        castframe++;
        haslights = false;
        if (!shading)
            return;

        foreach (var actor in LitActors())
        {
            var (intensity, radius, color) = CurrentLight(actor);
            // the weapon in hand isn't placed in the world: it's where the player is
            bool inhand = ReferenceEquals(actor, weaponSprite);
            CastLight(inhand ? player.X : actor.X, inhand ? player.Y : actor.Y, intensity, radius, ColorTints(color));
        }
        haslights = casttiles.Count > 0;
    }

    /// <summary>Adds a light at (lx, ly) (global units) to the cells around it, as far as it spreads, tinted (null: white).</summary>
    static void CastLight(int lx, int ly, int intensity, double radius, byte[]? tints)
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
                LightCells(x, y, lx, ly, r, levels, tints);
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

    // Adds a light's share to each of a tile's cells: levels at the light, falling smoothly to
    // none at r. Where it's the strongest light, the cell takes its tint, paler as it falls off.
    static void LightCells(int x, int y, int lx, int ly, long r, float levels, byte[]? tints)
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

                int i = (cy << GRIDSHIFT) + cx;
                celladd[i] = (byte)Math.Min(celladd[i] + add, MAXLIGHTLEVEL);
                if (add > cellstrong[i])
                {
                    cellstrong[i] = (byte)Math.Min(add, byte.MaxValue);
                    celltint[i] = tints == null ? (byte)0
                        : tints[Math.Clamp((int)Math.Ceiling(falloff * TINTSTEPS), 1, TINTSTEPS) - 1];
                }
            }
        }
    }
}
