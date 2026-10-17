using PFWolf.Assets;
using PFWolf.Entities;

namespace PFWolf.Rendering;

/// <summary>What the level's lighting needs from the game</summary>
/// <param name="HeightNumerator">The view's height numerator (CalcHeight), to turn a height back into a distance</param>
/// <param name="ClearColorTints">Forgets the actor lights' color tints (the tints they name are gone)</param>
/// <param name="TickActorLights">Runs the actors' lights for this tic's worth of time (Rendering/ActorLights.cs)</param>
internal sealed record LightingHooks(Func<int> HeightNumerator, Action ClearColorTints, Action<uint> TickActorLights);

/// <summary>
/// SHADING: the view is drawn in palette indices, so shading remaps them, Doom colormap style. A
/// light row (VideoManager.GetLightRow) holds FADESTEPS remaps of 256 entries for one light (a
/// level and a tint): step 0 is only lit, and each step after it goes further toward the fade
/// color. A pixel's step comes from its distance along the view (as CalcHeight measures it), so
/// walls take one per post, flats one per row, and sprites one per sprite.
///
/// THE LIGHT GRID: where the light falls. Each tile has a base light, a level and a tint: its
/// zone's, else the level's. On top of that the level is split into light cells, LIGHTCELLS a
/// tile each way, and each cell has light added to it (celladd, in light levels) by lights
/// moving about. Light is looked up at a point: the base light of a tile (the open tile a wall
/// is seen from, the tile a floor pixel or a sprite is on) plus the cell the point is in.
///
/// ZONE LIGHTS: light zones change as the level is played: switches and actor states set their
/// light (at once or fading over some tics) and effects make it flicker, pulse or strobe. The
/// zones tick with the actors, and each frame a zone whose light level or tint has moved gives
/// its tiles the new one in the light grid.
/// </summary>
internal sealed class Lighting(
    GameEngineManager engine,
    VideoManager video,
    MapManager map,
    LightingHooks hooks)
{
    internal const int LIGHTLEVELS = 32;    // light 0 (black) to LIGHTLEVELS - 1 (full)
    internal const int FADESTEPS = 32;      // step 0 (none) to FADESTEPS - 1 (all the fade color)

    internal static readonly byte[] noshade = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

    // What the level fades with when it has zones but no shading of its own: nothing
    internal static readonly ShadingSettings NoFade = new("#000000", 0, 16, 0, 255);

    internal ShadingSettings? levelshading;  // null: the level isn't shaded
    internal Dictionary<int, ZoneState> levelzones = [];     // RegisterZoneLightActions
    internal bool shading;                // anything to do: something is dimmed, tinted or faded
    internal byte[] lightrow = noshade;   // the level's light's row
    internal byte fader, fadeg, fadeb;
    internal long fadestart, fadeend;     // along the view, in global units (TILEGLOBAL a tile)
    internal int maxfadestep;
    internal bool inversefade;            // fade-curve inverse: maxfadestep × (1 − fadestart / nx)

    /// <summary>
    /// The map's shading, each value it leaves out taken from the default map's, else the
    /// ShadingInfo defaults; null when neither has any.
    /// </summary>
    internal static ShadingSettings? ResolveShading(MapInfo? mapInfo, DefaultMapInfo defaultMap)
    {
        ShadingInfo? map = mapInfo?.Shading, fallback = defaultMap.Shading;
        if (map == null && fallback == null)
            return null;

        return new ShadingSettings(
            map?.FadeColor ?? fallback?.FadeColor ?? "#000000",
            map?.FadeStart ?? fallback?.FadeStart ?? 0,
            map?.FadeEnd ?? fallback?.FadeEnd ?? 16,
            map?.MaxFade ?? fallback?.MaxFade ?? 100,
            map?.Light ?? fallback?.Light ?? 255,
            IsInverseCurve(map?.FadeCurve ?? fallback?.FadeCurve));
    }

    internal static bool IsInverseCurve(string? curve)
    {
        if (string.IsNullOrEmpty(curve) || curve.Equals("linear", StringComparison.OrdinalIgnoreCase))
            return false;
        if (curve.Equals("inverse", StringComparison.OrdinalIgnoreCase))
            return true;
        if (badcolors.Add("fade-curve:" + curve))
            Console.WriteLine($"Unknown shading fade-curve '{curve}' (linear or inverse); using linear");
        return false;
    }

    /// <summary>
    /// The map's light zones and the default map's, a map's zone taking each value it leaves
    /// out from the default map's zone of the same id, else the ZoneInfo defaults. A zone that
    /// only has ambient sound, in both, isn't one: its tiles keep the level's light.
    /// </summary>
    internal static Dictionary<int, ZoneState> ResolveZones(MapInfo? mapInfo, DefaultMapInfo defaultMap)
    {
        var zones = new Dictionary<int, ZoneState>();
        var mapZones = mapInfo?.Zones ?? [];
        foreach (var id in defaultMap.Zones.Keys.Union(mapZones.Keys))
        {
            ZoneInfo? map = mapZones.GetValueOrDefault(id), fallback = defaultMap.Zones.GetValueOrDefault(id);
            if (map is { HasLight: false } && fallback is not { HasLight: true } || map == null && fallback is { HasLight: false })
                continue;
            zones[id] = ZoneState.FromInfo(map, fallback);
        }
        return zones;
    }

    /// <summary>Shades the level with settings (null for none) until it's left or reloaded.</summary>
    internal void SetShading(ShadingSettings? settings)
    {
        levelshading = settings;
        RebuildShading();
    }

    // Everything shading draws from, again: after the settings change or zones come or go
    internal void RebuildShading()
    {
        var s = levelshading ?? NoFade;
        // any zone at all, since its light can change as the level is played
        shading = s.MaxFade > 0 || s.Light < 255 || levelzones.Count > 0;
        lightrow = noshade;
        foreach (var zone in levelzones.Values)
            zone.RowLevel = -1;
        griddirty = true;
        if (!shading)
            return;

        (fader, fadeg, fadeb) = ParseRgb(s.FadeColor, (0, 0, 0), "shading fade-color");
        fadestart = (long)(Math.Max(s.FadeStart, 0) * MapConstants.TILEGLOBAL);
        fadeend = Math.Max((long)(s.FadeEnd * MapConstants.TILEGLOBAL), fadestart + 1);
        maxfadestep = (int)Math.Round(Math.Clamp(s.MaxFade, 0, 100) / 100.0 * (FADESTEPS - 1));
        inversefade = s.InverseFade;

        ResetLightRows();
        ambientlevel = LightLevelOf(s.Light);
        lightrow = RowOf(0, ambientlevel);
        foreach (var zone in levelzones.Values)
            if (zone.Effect != ZoneEffect.None)
                PrewarmZoneRows(zone, zone.Low, zone.BaseLight);
        UpdateZoneLevels();
    }

    /// <summary>Gives each zone the light level and tint for the light it shows now, where that's moved.</summary>
    internal void UpdateZoneLevels()
    {
        if (!shading)
            return;
        foreach (var zone in levelzones.Values)
        {
            int level = LightLevelOf(zone.Current);
            if (zone.RowLevel >= 0 && level == zone.RowLevel && zone.Color == zone.RowColor)
                continue;

            zone.RowLevel = level;
            zone.RowColor = zone.Color;
            zone.RowTint = TintIndex(ParseRgb(zone.Color, NoTint, "zone color"));
            griddirty = true;
        }
    }

    internal static readonly HashSet<string> badcolors = [];

    // A #RRGGBB color, or fallback when there's none or it isn't one (warned about once)
    internal static (byte R, byte G, byte B) ParseRgb(string? color, (byte, byte, byte) fallback, string what)
    {
        if (string.IsNullOrEmpty(color))
            return fallback;
        try
        {
            var c = Color.FromHexRGBA(color);
            return (c.Red, c.Green, c.Blue);
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            if (badcolors.Add(color))
                Console.WriteLine($"The {what} '{color}' isn't #RRGGBB; using #{fallback.Item1:X2}{fallback.Item2:X2}{fallback.Item3:X2}");
            return fallback;
        }
    }

    // The light level (0 to LIGHTLEVELS - 1) for a light (0 to 255)
    internal static int LightLevelOf(int light) => (int)Math.Round(Math.Clamp(light, 0, 255) / 255.0 * (LIGHTLEVELS - 1));

    // The light row for a light level with a tint
    internal byte[] LevelRow(int level, (byte R, byte G, byte B) tint) =>
        shading ? video.GetLightRow(level, LIGHTLEVELS, FADESTEPS, fader, fadeg, fadeb, tint.R, tint.G, tint.B) : noshade;

    /// <summary>Where the remap for something nx away along the view (global units) starts in a light row.</summary>
    internal int ShadeOffset(long nx)
    {
        if (!shading || nx <= fadestart || maxfadestep == 0)
            return 0;
        if (inversefade)
            return (int)(maxfadestep - maxfadestep * fadestart / nx) * 256;
        if (nx >= fadeend)
            return maxfadestep * 256;
        return (int)((nx - fadestart) * maxfadestep / (fadeend - fadestart)) * 256;
    }

    /// <summary>ShadeOffset for something drawn height high (as wallheight: a story is height >> 2 pixels).</summary>
    internal int ShadeOffsetForHeight(int height) =>
        !shading ? 0 : ShadeOffset(height > 0 ? (long)hooks.HeightNumerator() * 256 / height : long.MaxValue);

    /// <summary>Whether an actor's frame is drawn unshaded: its state has the bright modifier, or the actor the BRIGHT flag.</summary>
    internal static bool IsBright(Entities.Actors.Actor actor) =>
        actor.CurrentState?.Bright == true || actor.Flags.Contains("bright", StringComparer.OrdinalIgnoreCase);

    internal void InitLevelShading(MapInfo? mapInfo)
    {
        var defaultMap = engine.GetGameInfo().DefaultMap;
        levelzones = ResolveZones(mapInfo, defaultMap);
        SetShading(ResolveShading(mapInfo, defaultMap));
    }

    internal const int LIGHTCELLSHIFT = 3;
    internal const int LIGHTCELLS = 1 << LIGHTCELLSHIFT;                    // cells a tile, each way
    internal const int CELLGLOBALSHIFT = MapConstants.TILESHIFT - LIGHTCELLSHIFT;    // global units to cells
    internal const int GRIDSHIFT = MapManager.MAPSHIFT + LIGHTCELLSHIFT;
    internal const int GRIDSIZE = 1 << GRIDSHIFT;                           // cells across the map
    internal const int MAXLIGHTLEVEL = LIGHTLEVELS - 1;

    // Each tile's base light, indexed (y << MAPSHIFT) + x like the flats: a light level, and a
    // tint (an index in tints)
    internal readonly byte[] tilelevel = new byte[MapManager.MAPAREA], tiletint = new byte[MapManager.MAPAREA];

    // Light levels added to each cell, indexed (y << GRIDSHIFT) + x in cells; the most of them any
    // one light added, and that light's tint (0: none, the tile's own)
    internal readonly byte[] celladd = new byte[GRIDSIZE * GRIDSIZE];
    internal readonly byte[] cellstrong = new byte[GRIDSIZE * GRIDSIZE], celltint = new byte[GRIDSIZE * GRIDSIZE];

    // The tints in use (0 is none), and each one's light rows by level, made as they're needed
    internal readonly List<(byte R, byte G, byte B)> tints = [];
    internal readonly List<byte[]?[]> tintrows = [];

    internal int ambientlevel;            // the level's own light level (tint 0)
    internal bool griddirty = true;       // the tiles' base lights need building again
    internal int gridversion;             // the MapManager.ZonesVersion they were built for
    internal bool haszones;               // some tile's base light isn't the level's

    internal static readonly (byte, byte, byte) NoTint = (255, 255, 255);

    /// <summary>A tint's index in tints, added if it's new.</summary>
    internal byte TintIndex((byte R, byte G, byte B) tint)
    {
        int index = tints.IndexOf(tint);
        if (index >= 0)
            return (byte)index;
        if (tints.Count > byte.MaxValue)
            return 0;                   // more tints than a tile can name: untinted
        tints.Add(tint);
        tintrows.Add(new byte[LIGHTLEVELS][]);
        return (byte)(tints.Count - 1);
    }

    /// <summary>Forgets the light rows (they're for the old fade), leaving the tints with only none.</summary>
    internal void ResetLightRows()
    {
        tints.Clear();
        tintrows.Clear();
        hooks.ClearColorTints();
        // cells tinted from the old tints mustn't name new ones
        Array.Clear(celltint);
        TintIndex(NoTint);
    }

    // A tint's light row for a light level
    internal byte[] RowOf(int tint, int level) => tintrows[tint][level] ??= LevelRow(level, tints[tint]);

    /// <summary>The index of a tile in tilelevel and tiletint, or -1 off the map.</summary>
    internal static int TileIndex(int x, int y) =>
        (uint)x < MapManager.MAPSIZE && (uint)y < MapManager.MAPSIZE ? (y << MapManager.MAPSHIFT) + x : -1;

    /// <summary>
    /// The light row at a point (global units): the base light of a tile (a TileIndex; off the
    /// map, the level's) plus the light added to the point's cell.
    /// </summary>
    internal byte[] LightAt(int tile, long gx, long gy)
    {
        if (!shading)
            return noshade;
        if (tile < 0)
            return lightrow;

        int level = tilelevel[tile], tint = tiletint[tile];
        long cx = gx >> CELLGLOBALSHIFT, cy = gy >> CELLGLOBALSHIFT;
        if ((ulong)cx < GRIDSIZE && (ulong)cy < GRIDSIZE)
        {
            long cell = (cy << GRIDSHIFT) + cx;
            level += celladd[cell];
            if (celltint[cell] != 0)
                tint = celltint[cell];
        }
        return RowOf(tint, level > MAXLIGHTLEVEL ? MAXLIGHTLEVEL : level);
    }

    /// <summary>The middle of a tile, in global units: where something placed on the tile stands.</summary>
    internal static (int X, int Y) TileCenter(int x, int y) =>
        ((x << MapConstants.TILESHIFT) + (int)MapConstants.TILEGLOBAL / 2, (y << MapConstants.TILESHIFT) + (int)MapConstants.TILEGLOBAL / 2);

    /// <summary>Brings the tiles' base lights up to date with the zone plane and the zones; called before each frame.</summary>
    internal void UpdateLightGrid()
    {
        if (!griddirty && gridversion == map.ZonesVersion)
            return;
        griddirty = false;
        gridversion = map.ZonesVersion;

        haszones = false;
        for (int y = 0; y < MapManager.MAPSIZE; y++)
            for (int x = 0; x < MapManager.MAPSIZE; x++)
            {
                int i = (y << MapManager.MAPSHIFT) + x;
                tilelevel[i] = (byte)ambientlevel;
                tiletint[i] = 0;
                if (x < map.mapwidth && y < map.mapheight
                    && map.GetZone(x, y) is var id and not 0
                    && levelzones.TryGetValue(id, out var zone) && zone.RowLevel >= 0)
                {
                    tilelevel[i] = (byte)zone.RowLevel;
                    tiletint[i] = zone.RowTint;
                    haszones |= zone.RowLevel != ambientlevel || zone.RowTint != 0;
                }
            }
    }

    // Flickering is only for show, so it doesn't draw on the game's random numbers (demos)
    internal static readonly Random zonerandom = new();

    internal static bool TryParseZoneEffect(string name, out ZoneEffect effect) =>
        Enum.TryParse(name, ignoreCase: true, out effect) && Enum.IsDefined(effect);

    /// <summary>Runs the zones' fades and effects for this tic's worth of time.</summary>
    internal void TickZoneLights(uint tics)
    {
        foreach (var zone in levelzones.Values)
            zone.Tick((int)tics, zonerandom);
        UpdateZoneLevels();
        hooks.TickActorLights(tics);      // and actors' (Rendering/ActorLights.cs)
    }

    /// <summary>The zone with this id, made (at full light) if the level has none yet.</summary>
    internal ZoneState ZoneFor(int id)
    {
        if (!levelzones.TryGetValue(id, out var zone))
        {
            levelzones[id] = zone = new ZoneState();
            RebuildShading();           // a new zone may turn shading on
        }
        return zone;
    }

    /// <summary>Sets a zone's light, fading to it over tics (0: at once).</summary>
    internal void FadeZoneLight(int id, int light, int tics)
    {
        var zone = ZoneFor(id);
        zone.FadeFrom = zone.BaseLight;
        zone.Light = Math.Clamp(light, 0, 255);
        zone.FadeTotal = zone.FadeLeft = Math.Max(tics, 0);
        PrewarmZoneRows(zone, zone.FadeFrom, zone.Light);
        UpdateZoneLevels();
    }

    /// <summary>Sets a zone's tint (null for none).</summary>
    internal void SetZoneColor(int id, string? color)
    {
        ZoneFor(id).Color = color;
        RebuildShading();               // a tint may turn shading on
    }

    internal void SetZoneEffect(int id, ZoneEffect effect)
    {
        var zone = ZoneFor(id);
        zone.SetEffect(effect);
        PrewarmZoneRows(zone, zone.Low, zone.BaseLight);
        UpdateZoneLevels();
    }

    /// <summary>Takes a zone out: its tiles get the level's light.</summary>
    internal void RemoveZone(int id)
    {
        levelzones.Remove(id);
        RebuildShading();
    }

    /// <summary>Builds the light rows a zone going between two lights will need, so it doesn't stall mid-level.</summary>
    internal void PrewarmZoneRows(ZoneState zone, int from, int to)
    {
        if (!shading)
            return;
        var tint = ParseRgb(zone.Color, (255, 255, 255), "zone color");
        for (int level = LightLevelOf(Math.Min(from, to)); level <= LightLevelOf(Math.Max(from, to)); level++)
            LevelRow(level, tint);
    }

    /*
    =============================================================================

                                    ACTIONS

    For switches (walls.yaml `switch: actions`) and actor states alike. They name the zone
    they change, whatever the switch's tag:

      A_SetZoneLight(zone, light, [tics], [color])  fades the zone to light (0-255) over tics
                                                    (left out or 0: at once); color #RRGGBB
                                                    tints it, "none" takes the tint off, left
                                                    out keeps it
      A_ToggleZoneLight(zone, light1, light2, [tics])  to light2 if it's at (or going to)
                                                    light1, else to light1
      A_SetZoneEffect(zone, effect)                 none, flicker, pulse or strobe, timed as
                                                    the zone's game-info low, tics, bright-tics

    =============================================================================
    */

    internal void RegisterZoneLightActions()
    {
        void Both(string name, Func<string, string[], bool> action)
        {
            MapTriggerRegistry.Register(name, (_, args) => action(name, args));
            Entities.Actors.ActorActionRegistry.Register(name, (Entities.Actors.Actor _, string[] args) => action(name, args));
        }

        Both("A_SetZoneLight", SetZoneLightAction);
        Both("A_ToggleZoneLight", ToggleZoneLightAction);
        Both("A_SetZoneEffect", SetZoneEffectAction);
    }

    internal bool SetZoneLightAction(string name, string[] args)
    {
        if (!ZoneArg(name, args, 0, out var zone) || !LightArg(name, args, 1, out var light) || !TicsArg(name, args, 2, out var tics))
            return false;

        switch (args.ElementAtOrDefault(3))
        {
            case null or "":
                break;
            case var none when none.Equals("none", StringComparison.OrdinalIgnoreCase):
                SetZoneColor(zone, null);
                break;
            case var color:
                if (!IsRgb(color))
                {
                    Console.WriteLine($"{name}: '{color}' isn't a #RRGGBB color.");
                    return false;
                }
                SetZoneColor(zone, color);
                break;
        }

        FadeZoneLight(zone, light, tics);
        return true;
    }

    internal bool ToggleZoneLightAction(string name, string[] args)
    {
        if (!ZoneArg(name, args, 0, out var zone) || !LightArg(name, args, 1, out var light1)
            || !LightArg(name, args, 2, out var light2) || !TicsArg(name, args, 3, out var tics))
            return false;

        FadeZoneLight(zone, ZoneFor(zone).Light == light1 ? light2 : light1, tics);
        return true;
    }

    internal bool SetZoneEffectAction(string name, string[] args)
    {
        if (!ZoneArg(name, args, 0, out var zone))
            return false;
        if (!TryParseZoneEffect(args.ElementAtOrDefault(1) ?? "", out var effect))
        {
            Console.WriteLine($"{name}: '{args.ElementAtOrDefault(1)}' isn't none, flicker, pulse or strobe.");
            return false;
        }

        SetZoneEffect(zone, effect);
        return true;
    }

    internal bool ZoneArg(string name, string[] args, int index, out int zone)
    {
        if (int.TryParse(args.ElementAtOrDefault(index), out zone) && zone is > 0 and <= ushort.MaxValue)
            return true;
        Console.WriteLine($"{name}: '{args.ElementAtOrDefault(index)}' isn't a zone (1-65535).");
        return false;
    }

    internal bool LightArg(string name, string[] args, int index, out int light)
    {
        if (int.TryParse(args.ElementAtOrDefault(index), out light) && light is >= 0 and <= 255)
            return true;
        Console.WriteLine($"{name}: '{args.ElementAtOrDefault(index)}' isn't a light (0-255).");
        return false;
    }

    // Left out (or "") is 0: at once
    internal bool TicsArg(string name, string[] args, int index, out int tics)
    {
        tics = 0;
        var arg = args.ElementAtOrDefault(index);
        if (string.IsNullOrEmpty(arg) || int.TryParse(arg, out tics) && tics >= 0)
            return true;
        Console.WriteLine($"{name}: '{arg}' isn't a number of tics.");
        return false;
    }

    internal static bool IsRgb(string color)
    {
        try { Color.FromHexRGBA(color); return color.TrimStart('#').Length == 6; }
        catch (Exception e) when (e is ArgumentException or FormatException) { return false; }
    }

    /*
    =============================================================================

                                    SAVES

    =============================================================================
    */

    internal void WriteZoneLights(BinaryWriter bw)
    {
        bw.Write(levelzones.Count);
        foreach (var (id, zone) in levelzones)
        {
            bw.Write(id);
            zone.Write(bw);
        }
    }

    internal Dictionary<int, ZoneState> ReadZoneLights(BinaryReader br)
    {
        var zones = new Dictionary<int, ZoneState>();
        for (int i = br.ReadCount(); i > 0; i--)
        {
            int id = br.ReadInt32();
            zones[id] = ZoneState.Read(br);
        }
        return zones;
    }

    /// <summary>Puts the level's zones back as a save had them (after SetupGameLevel set them from game-info).</summary>
    internal void RestoreZoneLights(Dictionary<int, ZoneState> zones)
    {
        levelzones = zones;
        RebuildShading();
    }
}
