using Wolf3D.Assets;
using Wolf3D.Constants;
using Wolf3D.Entities;
using Wolf3D.Managers;

namespace Wolf3D;

/// <summary>A level's shading, with every value filled in (see ShadingInfo)</summary>
internal record struct ShadingSettings(string FadeColor, double FadeStart, double FadeEnd, int MaxFade, int Light);

internal partial class Program
{
    /*
    =============================================================================

                                    SHADING

    The view is drawn in palette indices, so shading remaps them, Doom colormap style. A light
    row (VideoManager.GetLightRow) holds FADESTEPS remaps of 256 entries for one light (a level
    and a tint): step 0 is only lit, and each step after it goes further toward the fade color.
    A pixel's step comes from its distance along the view (as CalcHeight measures it), so walls
    take one per post, flats one per row, and sprites one per sprite.

    Which light a pixel gets comes from the zone plane: a tile in a light zone (the map's
    game-info zones) takes that zone's row, and any other tile the level's (lightrow). A wall
    face is lit by the open tile it's seen from, a floor or ceiling by its own tile, and a
    sprite by the tile it's on.

    =============================================================================
    */

    internal const int LIGHTLEVELS = 32;    // light 0 (black) to LIGHTLEVELS - 1 (full)
    internal const int FADESTEPS = 32;      // step 0 (none) to FADESTEPS - 1 (all the fade color)

    static readonly byte[] noshade = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

    // What the level fades with when it has zones but no shading of its own: nothing
    static readonly ShadingSettings NoFade = new("#000000", 0, 16, 0, 255);

    internal static ShadingSettings? levelshading;  // null: the level isn't shaded
    internal static Dictionary<int, ZoneState> levelzones = [];     // Program.ZoneLights.cs
    static bool shading;                // anything to do: something is dimmed, tinted or faded
    static byte[] lightrow = noshade;   // the level's light's row
    static byte fader, fadeg, fadeb;
    static long fadestart, fadeend;     // along the view, in global units (TILEGLOBAL a tile)
    static int maxfadestep;

    static readonly Dictionary<int, byte[]> zonerows = [];

    /// <summary>
    /// Each tile's light row, indexed (y &lt;&lt; MAPSHIFT) + x like the flats: its zone's, else
    /// the level's. Rebuilt when the zone plane or the lights change (UpdateTileLight).
    /// </summary>
    static readonly byte[][] tilelight = Enumerable.Repeat(noshade, MapManager.MAPAREA).ToArray();
    static bool tilelightdirty = true;
    static int tilelightversion;        // the MapManager.ZonesVersion tilelight was built for
    static bool haszones;               // some tile is lit other than by the level's light

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
            map?.Light ?? fallback?.Light ?? 255);
    }

    /// <summary>
    /// The map's light zones and the default map's, a map's zone taking each value it leaves
    /// out from the default map's zone of the same id, else the ZoneInfo defaults.
    /// </summary>
    internal static Dictionary<int, ZoneState> ResolveZones(MapInfo? mapInfo, DefaultMapInfo defaultMap)
    {
        var zones = new Dictionary<int, ZoneState>();
        var mapZones = mapInfo?.Zones ?? [];
        foreach (var id in defaultMap.Zones.Keys.Union(mapZones.Keys))
            zones[id] = ZoneState.FromInfo(mapZones.GetValueOrDefault(id), defaultMap.Zones.GetValueOrDefault(id));
        return zones;
    }

    /// <summary>Shades the level with settings (null for none) until it's left or reloaded.</summary>
    internal static void SetShading(ShadingSettings? settings)
    {
        levelshading = settings;
        RebuildShading();
    }

    // Everything shading draws from, again: after the settings change or zones come or go
    static void RebuildShading()
    {
        var s = levelshading ?? NoFade;
        // any zone at all, since its light can change as the level is played
        shading = s.MaxFade > 0 || s.Light < 255 || levelzones.Count > 0;
        lightrow = noshade;
        zonerows.Clear();
        foreach (var zone in levelzones.Values)
            zone.RowLevel = -1;
        tilelightdirty = true;
        if (!shading)
            return;

        (fader, fadeg, fadeb) = ParseRgb(s.FadeColor, (0, 0, 0), "shading fade-color");
        fadestart = (long)(Math.Max(s.FadeStart, 0) * MapConstants.TILEGLOBAL);
        fadeend = Math.Max((long)(s.FadeEnd * MapConstants.TILEGLOBAL), fadestart + 1);
        maxfadestep = (int)Math.Round(Math.Clamp(s.MaxFade, 0, 100) / 100.0 * (FADESTEPS - 1));

        lightrow = LevelRow(LightLevelOf(s.Light), (255, 255, 255));
        foreach (var zone in levelzones.Values)
            if (zone.Effect != ZoneEffect.None)
                PrewarmZoneRows(zone, zone.Low, zone.BaseLight);
        UpdateZoneRows();
    }

    /// <summary>Gives each zone the light row for the light it shows now, where that's moved.</summary>
    static void UpdateZoneRows()
    {
        if (!shading)
            return;
        foreach (var (id, zone) in levelzones)
        {
            int level = LightLevelOf(zone.Current);
            if (zone.Row != null && level == zone.RowLevel && zone.Color == zone.RowColor)
                continue;

            zone.Row = LevelRow(level, ParseRgb(zone.Color, (255, 255, 255), "zone color"));
            zone.RowLevel = level;
            zone.RowColor = zone.Color;
            zonerows[id] = zone.Row;
            tilelightdirty = true;
        }
    }

    static readonly HashSet<string> badcolors = [];

    // A #RRGGBB color, or fallback when there's none or it isn't one (warned about once)
    static (byte R, byte G, byte B) ParseRgb(string? color, (byte, byte, byte) fallback, string what)
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
    static int LightLevelOf(int light) => (int)Math.Round(Math.Clamp(light, 0, 255) / 255.0 * (LIGHTLEVELS - 1));

    // The light row for a light level with a tint
    static byte[] LevelRow(int level, (byte R, byte G, byte B) tint) =>
        shading ? _videoManager.GetLightRow(level, LIGHTLEVELS, FADESTEPS, fader, fadeg, fadeb, tint.R, tint.G, tint.B) : noshade;

    /// <summary>Brings tilelight up to date with the zone plane and the lights; called before each frame.</summary>
    static void UpdateTileLight()
    {
        if (!tilelightdirty && tilelightversion == _mapManager.ZonesVersion)
            return;
        tilelightdirty = false;
        tilelightversion = _mapManager.ZonesVersion;

        haszones = false;
        for (int y = 0; y < MapManager.MAPSIZE; y++)
            for (int x = 0; x < MapManager.MAPSIZE; x++)
            {
                var row = lightrow;
                if (x < _mapManager.mapwidth && y < _mapManager.mapheight
                    && _mapManager.GetZone(x, y) is var zone and not 0 && zonerows.TryGetValue(zone, out var zonerow))
                    row = zonerow;
                tilelight[(y << MapManager.MAPSHIFT) + x] = row;
                haszones |= row != lightrow;
            }
    }

    /// <summary>The light row of a tile (the level's off the map).</summary>
    static byte[] TileLight(int x, int y) =>
        (uint)x < MapManager.MAPSIZE && (uint)y < MapManager.MAPSIZE ? tilelight[(y << MapManager.MAPSHIFT) + x] : lightrow;

    /// <summary>Where the remap for something nx away along the view (global units) starts in a light row.</summary>
    static int ShadeOffset(long nx)
    {
        if (!shading || nx <= fadestart || maxfadestep == 0)
            return 0;
        if (nx >= fadeend)
            return maxfadestep * 256;
        return (int)((nx - fadestart) * maxfadestep / (fadeend - fadestart)) * 256;
    }

    /// <summary>ShadeOffset for something drawn height high (as wallheight: a story is height >> 2 pixels).</summary>
    static int ShadeOffsetForHeight(int height) =>
        !shading ? 0 : ShadeOffset(height > 0 ? (long)heightnumerator * 256 / height : long.MaxValue);

    /// <summary>Whether an actor's frame is drawn unshaded: its state has the bright modifier, or the actor the BRIGHT flag.</summary>
    internal static bool IsBright(Entities.Actors.Actor actor) =>
        actor.CurrentState?.Bright == true || actor.Flags.Contains("bright", StringComparer.OrdinalIgnoreCase);

    internal static void InitLevelShading(MapInfo? mapInfo)
    {
        var defaultMap = _gameEngineManager.GetGameInfo().DefaultMap;
        levelzones = ResolveZones(mapInfo, defaultMap);
        SetShading(ResolveShading(mapInfo, defaultMap));
    }
}
